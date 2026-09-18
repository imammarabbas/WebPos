using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Services;
using WebPos.Core.Validation;

namespace WebPos.IntegrationTests.Services;

public sealed class PartyServiceTests
{
    [Fact]
    public async Task CreateSupplier_ShouldInitializeOpeningLedger()
    {
        await using PartyHarness harness = await PartyHarness.CreateAsync();

        PartyDto supplier = await harness.PartyService.CreatePartyAsync(
            new CreatePartyRequest
            {
                Role = PartyTypes.Supplier,
                Name = "Fresh Dairy Farm",
                PhoneNumber = "03001234567",
                Address = "Lahore",
                CreditLimitPaisa = 500_000
            });

        supplier.Role.Should().Be(PartyTypes.Supplier);
        supplier.CurrentBalancePaisa.Should().Be(0);

        PartyLedger opening = await harness.Context.PartyLedgers
            .SingleAsync(entry =>
                entry.PartyId == supplier.Id && entry.Type == "OPENING");
        opening.TransactionAmountPaisa.Should().Be(0);
        opening.NewBalancePaisa.Should().Be(0);
        opening.ReferenceDetails.Should().Be("SUPPLIER_INIT");
        opening.TenantId.Should().Be(harness.TenantId);
    }

    [Fact]
    public async Task CreateParty_ShouldRejectDuplicatePhoneInSameTenant()
    {
        await using PartyHarness harness = await PartyHarness.CreateAsync();
        await harness.PartyService.CreatePartyAsync(
            new CreatePartyRequest
            {
                Role = PartyTypes.Customer,
                Name = "Walk-in A",
                PhoneNumber = "03007654321",
                CreditLimitPaisa = 0
            });

        Func<Task> duplicate = () => harness.PartyService.CreatePartyAsync(
            new CreatePartyRequest
            {
                Role = PartyTypes.Supplier,
                Name = "Different Name",
                PhoneNumber = "03007654321",
                CreditLimitPaisa = 0
            });

        await duplicate.Should().ThrowAsync<ValidationException>()
            .WithMessage("*phone number*");
    }

    [Fact]
    public async Task GetSupplierAsync_ShouldRejectCustomerParty()
    {
        await using PartyHarness harness = await PartyHarness.CreateAsync();
        PartyDto customer = await harness.PartyService.CreatePartyAsync(
            new CreatePartyRequest
            {
                Role = PartyTypes.Customer,
                Name = "Retail Customer",
                PhoneNumber = "03001112233",
                CreditLimitPaisa = 10_000
            });

        Func<Task> act = () => harness.PartyService.GetSupplierAsync(customer.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a SUPPLIER*");
    }

    private sealed class PartyHarness : IAsyncDisposable
    {
        private PartyHarness(
            WebPosDbContext context,
            IPartyService partyService,
            Guid tenantId)
        {
            Context = context;
            PartyService = partyService;
            TenantId = tenantId;
        }

        public WebPosDbContext Context { get; }

        public IPartyService PartyService { get; }

        public Guid TenantId { get; }

        public static async Task<PartyHarness> CreateAsync()
        {
            Guid tenantId = TenantDefaults.MasterTenantId;
            DbContextOptions<WebPosDbContext> options =
                new DbContextOptionsBuilder<WebPosDbContext>()
                    .UseInMemoryDatabase($"WebPos_Party_{Guid.NewGuid():N}")
                    .Options;

            var tenantService = new TestTenantService(tenantId);
            var context = new WebPosDbContext(options, tenantService);
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Master Tenant",
                Slug = $"master-{tenantId:N}",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();

            IDbContextFactory<WebPosDbContext> dbFactory =
                new TestDbContextFactory(options, tenantService);
            var ambient = new AmbientDbContextAccessor();
            var transactionService = new TransactionService(dbFactory);
            IPartyLedgerService ledgerService = new PartyLedgerService(
                dbFactory,
                transactionService,
                tenantService);
            IValidator<CreatePartyRequest> validator =
                new CreatePartyRequestValidator(dbFactory, tenantService);
            IPartyService partyService = new PartyService(
                dbFactory,
                ambient,
                transactionService,
                ledgerService,
                tenantService,
                validator);

            return new PartyHarness(context, partyService, tenantId);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<WebPosDbContext> options,
        ITenantService tenant) : IDbContextFactory<WebPosDbContext>
    {
        public WebPosDbContext CreateDbContext() => new(options, tenant);
    }

    private sealed class TestTenantService(Guid tenantId) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }
}
