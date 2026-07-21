using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Services;

namespace WebPos.IntegrationTests.Infrastructure;

/// <summary>
/// Shared local PostgreSQL fixture for integration tests. Resets the WebPos_Test database
/// once per collection, then creates real (non-mocked) service graphs per test scope.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Matches WebPos/appsettings.json DefaultConnection (local WebPos_Test database).
    private const string ConnectionString =
        "Server=localhost;Port=5432;Database=WebPos_Test;User Id=postgres;Password=sa;";

    private DbContextOptions<WebPosDbContext>? _options;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<WebPosDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Drops and recreates the test database schema so each collection starts clean.
    /// </summary>
    public async Task ResetDatabase()
    {
        if (_options is null)
        {
            throw new InvalidOperationException("PostgresFixture has not been initialized.");
        }

        await using WebPosDbContext context = new(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Creates a disposable scope with a fresh <see cref="WebPosDbContext"/> and real
    /// <see cref="ITransactionService"/> / <see cref="ISalesService"/> instances.
    /// </summary>
    public IntegrationTestScope CreateScope()
    {
        if (_options is null)
        {
            throw new InvalidOperationException("PostgresFixture has not been initialized.");
        }

        return new IntegrationTestScope(_options);
    }
}

/// <summary>
/// Owns one DbContext and the domain services that share it for a single test.
/// </summary>
public sealed class IntegrationTestScope : IAsyncDisposable, IDisposable
{
    public WebPosDbContext DbContext { get; }

    public ITransactionService TransactionService { get; }

    public IPartyLedgerService PartyLedgerService { get; }

    public ISalesService SalesService { get; }

    public ISalesReturnService SalesReturnService { get; }

    public IntegrationTestScope(DbContextOptions<WebPosDbContext> options)
    {
        ITenantService tenantService = new FixedTenantService(TenantDefaults.MasterTenantId);
        DbContext = new WebPosDbContext(options, tenantService);
        TransactionService = new TransactionService(DbContext);
        PartyLedgerService = new PartyLedgerService(
            DbContext,
            TransactionService,
            tenantService);
        SalesService = new SalesService(DbContext, TransactionService, PartyLedgerService);
        SalesReturnService = new SalesReturnService(
            DbContext,
            TransactionService,
            PartyLedgerService);
    }

    private sealed class FixedTenantService(Guid tenantId) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }

    public void Dispose() => DbContext.Dispose();

    public ValueTask DisposeAsync() => DbContext.DisposeAsync();
}
