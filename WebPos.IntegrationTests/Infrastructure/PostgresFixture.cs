using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Services;
using WebPos.Core.Validation;

namespace WebPos.IntegrationTests.Infrastructure;

/// <summary>
/// Shared local PostgreSQL fixture for integration tests. Resets the WebPos_Test database
/// once per collection, then creates real (non-mocked) service graphs per test scope.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Matches WebPos/appsettings.json DefaultConnection (local WebPos_Test database).
    private const string ConnectionString =
        "Server=localhost;Port=5432;Database=WebPos_Test;User Id=postgres;Password=sa;Include Error Detail=true";

    private DbContextOptions<WebPosDbContext>? _options;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<WebPosDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsAssembly("WebPos"))
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

    public ITenantService TenantService { get; }

    public ITransactionService TransactionService { get; }

    public IPartyLedgerService PartyLedgerService { get; }

    public IPartyService PartyService { get; }

    public IProcurementService ProcurementService { get; }

    public ISalesService SalesService { get; }

    public ISalesReturnService SalesReturnService { get; }

    public IReportingService ReportingService { get; }

    public ICashAccountService CashAccountService { get; }

    public ICashTransferService CashTransferService { get; }

    public IntegrationTestScope(DbContextOptions<WebPosDbContext> options)
    {
        TenantService = new FixedTenantService(TenantDefaults.MasterTenantId);
        DbContext = new WebPosDbContext(options, TenantService);
        TransactionService = new TransactionService(DbContext);
        PartyLedgerService = new PartyLedgerService(
            DbContext,
            TransactionService,
            TenantService);
        PartyService = new PartyService(
            DbContext,
            TransactionService,
            PartyLedgerService,
            TenantService,
            new CreatePartyRequestValidator(DbContext, TenantService));
        CashAccountService = new CashAccountService(DbContext, TenantService);
        CashTransferService = new CashTransferService(
            DbContext,
            TransactionService,
            TenantService);
        ProcurementService = new ProcurementService(
            DbContext,
            TransactionService,
            PartyLedgerService,
            PartyService,
            CashAccountService,
            TenantService);
        SalesService = new SalesService(
            DbContext,
            TransactionService,
            PartyLedgerService,
            CashAccountService);
        SalesReturnService = new SalesReturnService(
            DbContext,
            TransactionService,
            PartyLedgerService);
        ReportingService = new ReportingService(
            new TestDbContextFactory(options, TenantService),
            TenantService);
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<WebPosDbContext> options,
        ITenantService tenant) : IDbContextFactory<WebPosDbContext>
    {
        public WebPosDbContext CreateDbContext() => new(options, tenant);
    }

    private sealed class FixedTenantService(Guid tenantId) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }

    public void Dispose() => DbContext.Dispose();

    public ValueTask DisposeAsync() => DbContext.DisposeAsync();
}
