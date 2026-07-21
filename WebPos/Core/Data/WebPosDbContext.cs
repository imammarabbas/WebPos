using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Data;

public class WebPosDbContext : DbContext
{
    private static readonly HashSet<Type> GlobalEntityTypes = [typeof(Tenant)];

    private readonly ITenantService _tenantService;

    public WebPosDbContext(
        DbContextOptions<WebPosDbContext> options,
        ITenantService? tenantService = null)
        : base(options)
    {
        _tenantService = tenantService ?? DesignTimeTenantService.Instance;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Terminal> Terminals => Set<Terminal>();

    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();

    public DbSet<ShiftExpense> ShiftExpenses => Set<ShiftExpense>();

    public DbSet<Party> Parties => Set<Party>();

    public DbSet<PartyLedger> PartyLedgers => Set<PartyLedger>();

    public DbSet<GeneralLedgerEntry> GeneralLedgerEntries => Set<GeneralLedgerEntry>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();

    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

    public DbSet<DailyMilkCollection> DailyMilkCollections => Set<DailyMilkCollection>();

    public DbSet<ProductionLog> ProductionLogs => Set<ProductionLog>();

    public DbSet<ProductionConsumptionItem> ProductionConsumptionItems => Set<ProductionConsumptionItem>();

    public DbSet<ProductionYieldItem> ProductionYieldItems => Set<ProductionYieldItem>();

    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();

    public DbSet<SalesItem> SalesItems => Set<SalesItem>();

    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();

    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();

    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();

    public DbSet<PurchaseReturnItem> PurchaseReturnItems => Set<PurchaseReturnItem>();

    public DbSet<DamagedStockLog> DamagedStockLogs => Set<DamagedStockLog>();

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampAddedEntitiesWithTenant();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampAddedEntitiesWithTenant()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>()
                     .Where(entry => entry.State == EntityState.Added))
        {
            if (entry.Entity.TenantId != Guid.Empty)
            {
                if (_tenantService.IsResolved
                    && entry.Entity.TenantId != _tenantService.TenantId)
                {
                    throw new InvalidOperationException(
                        $"Cannot add '{entry.Metadata.DisplayName()}' for a different tenant.");
                }

                // Explicit IDs are retained for trusted import/system workflows.
                continue;
            }

            if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"Cannot add tenant-scoped entity '{entry.Metadata.DisplayName()}' without a resolved TenantId.");
            }

            entry.Entity.TenantId = _tenantService.TenantId;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureTenant(modelBuilder.Entity<Tenant>());
        ConfigureRole(modelBuilder.Entity<Role>());
        ConfigureUser(modelBuilder.Entity<User>());
        ConfigureTerminal(modelBuilder.Entity<Terminal>());
        ConfigureCashierShift(modelBuilder.Entity<CashierShift>());
        ConfigureShiftExpense(modelBuilder.Entity<ShiftExpense>());
        ConfigureParty(modelBuilder.Entity<Party>());
        ConfigurePartyLedger(modelBuilder.Entity<PartyLedger>());
        ConfigureGeneralLedgerEntry(modelBuilder.Entity<GeneralLedgerEntry>());
        ConfigureCategory(modelBuilder.Entity<Category>());
        ConfigureProduct(modelBuilder.Entity<Product>());
        ConfigurePurchaseOrder(modelBuilder.Entity<PurchaseOrder>());
        ConfigureProductBatch(modelBuilder.Entity<ProductBatch>());
        ConfigurePurchaseItem(modelBuilder.Entity<PurchaseItem>());
        ConfigureDailyMilkCollection(modelBuilder.Entity<DailyMilkCollection>());
        ConfigureProductionLog(modelBuilder.Entity<ProductionLog>());
        ConfigureProductionConsumptionItem(modelBuilder.Entity<ProductionConsumptionItem>());
        ConfigureProductionYieldItem(modelBuilder.Entity<ProductionYieldItem>());
        ConfigureSalesInvoice(modelBuilder.Entity<SalesInvoice>());
        ConfigureSalesItem(modelBuilder.Entity<SalesItem>());
        ConfigureSalesReturn(modelBuilder.Entity<SalesReturn>());
        ConfigureSalesReturnItem(modelBuilder.Entity<SalesReturnItem>());
        ConfigurePurchaseReturn(modelBuilder.Entity<PurchaseReturn>());
        ConfigurePurchaseReturnItem(modelBuilder.Entity<PurchaseReturnItem>());
        ConfigureDamagedStockLog(modelBuilder.Entity<DamagedStockLog>());

        ApplyTenantFilter<Role>(modelBuilder);
        ApplyTenantFilter<User>(modelBuilder);
        ApplyTenantFilter<Terminal>(modelBuilder);
        ApplyTenantFilter<CashierShift>(modelBuilder);
        ApplyTenantFilter<ShiftExpense>(modelBuilder);
        ApplyTenantFilter<Party>(modelBuilder);
        ApplyTenantFilter<PartyLedger>(modelBuilder);
        ApplyTenantFilter<GeneralLedgerEntry>(modelBuilder);
        ApplyTenantFilter<Category>(modelBuilder);
        ApplyTenantFilter<Product>(modelBuilder);
        ApplyTenantFilter<PurchaseOrder>(modelBuilder);
        ApplyTenantFilter<ProductBatch>(modelBuilder);
        ApplyTenantFilter<PurchaseItem>(modelBuilder);
        ApplyTenantFilter<DailyMilkCollection>(modelBuilder);
        ApplyTenantFilter<ProductionLog>(modelBuilder);
        ApplyTenantFilter<ProductionConsumptionItem>(modelBuilder);
        ApplyTenantFilter<ProductionYieldItem>(modelBuilder);
        ApplyTenantFilter<SalesInvoice>(modelBuilder);
        ApplyTenantFilter<SalesItem>(modelBuilder);
        ApplyTenantFilter<SalesReturn>(modelBuilder);
        ApplyTenantFilter<SalesReturnItem>(modelBuilder);
        ApplyTenantFilter<PurchaseReturn>(modelBuilder);
        ApplyTenantFilter<PurchaseReturnItem>(modelBuilder);
        ApplyTenantFilter<DamagedStockLog>(modelBuilder);
        ValidateTenantModel(modelBuilder);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : BaseEntity
    {
        modelBuilder.Entity<TEntity>()
            .Property(entity => entity.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        modelBuilder.Entity<TEntity>()
            .HasIndex(entity => entity.TenantId);

        modelBuilder.Entity<TEntity>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity =>
                _tenantService.IsResolved
                && entity.TenantId == _tenantService.TenantId);
    }

    private static void ValidateTenantModel(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (GlobalEntityTypes.Contains(entityType.ClrType))
            {
                continue;
            }

            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                throw new InvalidOperationException(
                    $"Entity '{entityType.DisplayName()}' must inherit from BaseEntity or be explicitly classified as global.");
            }

            var tenantProperty = entityType.FindProperty(nameof(BaseEntity.TenantId));
            if (tenantProperty is null
                || tenantProperty.ClrType != typeof(Guid)
                || tenantProperty.IsNullable)
            {
                throw new InvalidOperationException(
                    $"Tenant-scoped entity '{entityType.DisplayName()}' must define a required Guid TenantId.");
            }

            if (!entityType.GetDeclaredQueryFilters().Any())
            {
                throw new InvalidOperationException(
                    $"Tenant-scoped entity '{entityType.DisplayName()}' is missing its global tenant query filter.");
            }
        }
    }

    private static void ConfigureTenant(EntityTypeBuilder<Tenant> entity)
    {
        entity.ToTable("tenants");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        entity.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        entity.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasIndex(e => e.Slug).IsUnique();
    }

    private sealed class DesignTimeTenantService : ITenantService
    {
        public static DesignTimeTenantService Instance { get; } = new();

        public Guid TenantId => Guid.Empty;

        public bool IsResolved => true;
    }

    private static void ConfigureRole(EntityTypeBuilder<Role> entity)
    {
        entity.ToTable("roles");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.RoleName).HasColumnName("role_name").HasMaxLength(50).IsRequired();
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasIndex(e => e.RoleName).IsUnique();
    }

    private static void ConfigureUser(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("users");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
        entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        entity.Property(e => e.PinHash).HasColumnName("pin_hash").HasMaxLength(64).IsRequired();
        entity.Property(e => e.RoleId).HasColumnName("role_id").IsRequired();
        entity.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        entity.HasIndex(e => e.Username).IsUnique();
        entity.HasIndex(e => e.PinHash)
            .IsUnique()
            .HasFilter("\"pin_hash\" <> ''");

        entity.HasOne(e => e.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureTerminal(EntityTypeBuilder<Terminal> entity)
    {
        entity.ToTable("terminals");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.TerminalName).HasColumnName("terminal_name").HasMaxLength(50).IsRequired();
        entity.Property(e => e.MacAddress).HasColumnName("mac_address").HasMaxLength(100).IsRequired();
        entity.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        entity.Property(e => e.LastSyncTime).HasColumnName("last_sync_time").IsRequired();

        entity.HasIndex(e => e.TerminalName).IsUnique();
        entity.HasIndex(e => e.MacAddress).IsUnique();
    }

    private static void ConfigureCashierShift(EntityTypeBuilder<CashierShift> entity)
    {
        entity.ToTable("cashier_shifts");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.TerminalId).HasColumnName("terminal_id").IsRequired();
        entity.Property(e => e.CashierId).HasColumnName("cashier_id").IsRequired();
        entity.Property(e => e.OpenedAt).HasColumnName("opened_at").IsRequired();
        entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
        MapPaisa(entity.Property(e => e.OpeningCashPaisa)).HasColumnName("opening_cash_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.ExpectedCashPaisa)).HasColumnName("expected_cash_paisa");
        MapPaisa(entity.Property(e => e.ActualBlindCashPaisa)).HasColumnName("actual_blind_cash_paisa");
        MapPaisa(entity.Property(e => e.DiscrepancyPaisa)).HasColumnName("discrepancy_paisa");
        entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();

        entity.HasIndex(e => e.TerminalId)
            .IsUnique()
            .HasFilter("\"status\" = 'OPEN'")
            .HasDatabaseName("UX_cashier_shifts_open_terminal");
        entity.HasIndex(e => e.CashierId)
            .IsUnique()
            .HasFilter("\"status\" = 'OPEN'")
            .HasDatabaseName("UX_cashier_shifts_open_cashier");

        entity.HasOne<Terminal>()
            .WithMany()
            .HasForeignKey(e => e.TerminalId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CashierId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureShiftExpense(EntityTypeBuilder<ShiftExpense> entity)
    {
        entity.ToTable("shift_expenses");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ShiftId).HasColumnName("shift_id").IsRequired();
        entity.Property(e => e.VoucherNo).HasColumnName("voucher_no").HasMaxLength(50).IsRequired();
        entity.Property(e => e.Description).HasColumnName("description").IsRequired();
        MapPaisa(entity.Property(e => e.AmountPaisa)).HasColumnName("amount_paisa").IsRequired();
        entity.Property(e => e.ExpenseCategory).HasColumnName("expense_category").HasMaxLength(50).IsRequired();
        entity.Property(e => e.ReceiptReference).HasColumnName("receipt_reference").HasMaxLength(100).IsRequired();
        entity.Property(e => e.PaymentMethod).HasColumnName("payment_method").HasMaxLength(20).IsRequired();
        entity.Property(e => e.IsRecurring).HasColumnName("is_recurring").IsRequired();
        entity.Property(e => e.LoggedByUserId).HasColumnName("logged_by_user_id").IsRequired();
        entity.Property(e => e.LoggedAt).HasColumnName("logged_at").IsRequired();

        entity.HasIndex(e => e.VoucherNo).IsUnique();
        entity.HasIndex(e => e.ExpenseCategory);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.LoggedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<CashierShift>()
            .WithMany(s => s.ShiftExpenses)
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureGeneralLedgerEntry(EntityTypeBuilder<GeneralLedgerEntry> entity)
    {
        entity.ToTable("general_ledger_entries");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.TransactionGroupId).HasColumnName("transaction_group_id").IsRequired();
        entity.Property(e => e.AccountCode).HasColumnName("account_code").HasMaxLength(50).IsRequired();
        MapPaisa(entity.Property(e => e.DebitPaisa)).HasColumnName("debit_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.CreditPaisa)).HasColumnName("credit_paisa").IsRequired();
        entity.Property(e => e.TransactionType).HasColumnName("transaction_type").HasMaxLength(30).IsRequired();
        entity.Property(e => e.ReferenceNo).HasColumnName("reference_no").HasMaxLength(100).IsRequired();
        entity.Property(e => e.ReferenceDetails).HasColumnName("reference_details").HasMaxLength(255).IsRequired();
        entity.Property(e => e.ShiftId).HasColumnName("shift_id");
        entity.Property(e => e.PartyId).HasColumnName("party_id");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasIndex(e => e.TransactionGroupId);
        entity.HasIndex(e => e.AccountCode);
        entity.HasIndex(e => e.ReferenceNo);
        entity.HasIndex(e => e.CreatedAt);
    }

    private static void ConfigureParty(EntityTypeBuilder<Party> entity)
    {
        entity.ToTable("parties");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.PartyType).HasColumnName("party_type").HasMaxLength(20).IsRequired();
        entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
        entity.Property(e => e.Address).HasColumnName("address");
        MapPaisa(entity.Property(e => e.CreditLimitPaisa)).HasColumnName("credit_limit_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.CurrentBalancePaisa)).HasColumnName("current_balance_paisa").IsRequired();
        entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").IsRequired().HasDefaultValue(false);
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        entity.HasIndex(e => new { e.TenantId, e.UpdatedAt })
            .HasDatabaseName("IX_parties_tenant_updated_at");

        entity.HasIndex(e => new { e.TenantId, e.PhoneNumber })
            .IsUnique()
            .HasDatabaseName("UX_parties_tenant_phone");

        entity.HasIndex(e => new { e.TenantId, e.PartyType, e.Name })
            .IsUnique()
            .HasDatabaseName("UX_parties_tenant_type_name");
    }

    private static void ConfigurePartyLedger(EntityTypeBuilder<PartyLedger> entity)
    {
        entity.ToTable("party_ledgers");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.PartyId).HasColumnName("party_id").IsRequired();
        entity.Property(e => e.InvoiceNo).HasColumnName("invoice_no").HasMaxLength(100);
        entity.Property(e => e.PurchaseOrderId).HasColumnName("purchase_order_id");
        entity.Property(e => e.Type).HasColumnName("transaction_type").HasMaxLength(20).IsRequired();
        entity.Property(e => e.PaymentMethod).HasColumnName("payment_channel").HasMaxLength(20).IsRequired();
        MapPaisa(entity.Property(e => e.OldBalancePaisa)).HasColumnName("old_balance_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.TransactionAmountPaisa)).HasColumnName("amount_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.NewBalancePaisa)).HasColumnName("running_balance_paisa").IsRequired();
        entity.Property(e => e.ReferenceDetails).HasColumnName("reference_number").HasMaxLength(100).IsRequired();
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        entity.HasOne(e => e.Party)
            .WithMany(p => p.Ledgers)
            .HasForeignKey(e => e.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<SalesInvoice>()
            .WithMany()
            .HasForeignKey(e => e.InvoiceNo)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<PurchaseOrder>()
            .WithMany()
            .HasForeignKey(e => e.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCategory(EntityTypeBuilder<Category> entity)
    {
        entity.ToTable("categories");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        entity.Property(e => e.ParentCategoryId).HasColumnName("parent_category_id");
        entity.Property(e => e.TargetMarginPercentage).HasColumnName("target_margin_percentage").IsRequired();
        entity.Property(e => e.ShowOnWebshop).HasColumnName("show_on_webshop").IsRequired();
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasOne(e => e.ParentCategory)
            .WithMany(c => c.InverseParentCategory)
            .HasForeignKey(e => e.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureProduct(EntityTypeBuilder<Product> entity)
    {
        entity.ToTable("products");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.CategoryId).HasColumnName("category_id");
        entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        entity.Property(e => e.Sku).HasColumnName("sku").HasMaxLength(50).IsRequired();
        entity.Property(e => e.Barcode).HasColumnName("barcode").HasMaxLength(100).IsRequired();
        entity.Property(e => e.Brand).HasColumnName("brand").HasMaxLength(100);
        entity.Property(e => e.BaseUnit).HasColumnName("base_unit").HasMaxLength(20).IsRequired();
        entity.Property(e => e.ConversionMultiplier).HasColumnName("conversion_multiplier").IsRequired();
        entity.Property(e => e.ShowOnWebshop).HasColumnName("show_on_webshop").IsRequired();
        entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").IsRequired().HasDefaultValue(false);
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        entity.HasIndex(e => new { e.TenantId, e.UpdatedAt })
            .HasDatabaseName("IX_products_tenant_updated_at");

        entity.HasIndex(e => e.Sku).IsUnique();
        entity.HasIndex(e => e.Barcode).IsUnique();

        entity.HasOne(e => e.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePurchaseOrder(EntityTypeBuilder<PurchaseOrder> entity)
    {
        entity.ToTable("purchase_orders");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.SupplierInvoiceNo).HasColumnName("supplier_invoice_no").HasMaxLength(100).IsRequired();
        entity.Property(e => e.SupplierId).HasColumnName("supplier_id").IsRequired();
        entity.Property(e => e.ReceiverId).HasColumnName("receiver_id").IsRequired();
        MapPaisa(entity.Property(e => e.SubTotalPaisa)).HasColumnName("sub_total_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.DiscountPaisa)).HasColumnName("discount_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.NetPayablePaisa)).HasColumnName("net_payable_paisa").IsRequired();
        entity.Property(e => e.PaymentStatus).HasColumnName("payment_status").HasMaxLength(20).IsRequired();
        entity.Property(e => e.IsReceived).HasColumnName("is_received").IsRequired();
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Receiver)
            .WithMany()
            .HasForeignKey(e => e.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureProductBatch(EntityTypeBuilder<ProductBatch> entity)
    {
        entity.ToTable("product_batches");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        entity.Property(e => e.BatchNumber).HasColumnName("batch_number").HasMaxLength(100).IsRequired();
        entity.Property(e => e.ExpiryDate).HasColumnName("expiry_date");
        MapPaisa(entity.Property(e => e.CostPricePaisa)).HasColumnName("cost_price_paisa").IsRequired();
        entity.Ignore(e => e.PurchasePricePaisa);
        MapPaisa(entity.Property(e => e.RetailPricePaisa)).HasColumnName("retail_price_paisa").IsRequired();
        MapNumeric(entity.Property(e => e.InitialQty)).HasColumnName("initial_qty").IsRequired();
        MapNumeric(entity.Property(e => e.CurrentQty)).HasColumnName("current_qty").IsRequired();
        entity.Property(e => e.SupplierId).HasColumnName("supplier_id").IsRequired();
        entity.Property(e => e.RackLocation).HasColumnName("rack_location").HasMaxLength(50);
        entity.Property(e => e.PurchaseOrderId).HasColumnName("purchase_order_id");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasOne(e => e.Product)
            .WithMany(p => p.Batches)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.PurchaseOrder)
            .WithMany(o => o.ProductBatches)
            .HasForeignKey(e => e.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePurchaseItem(EntityTypeBuilder<PurchaseItem> entity)
    {
        entity.ToTable("purchase_items");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
        entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        MapNumeric(entity.Property(e => e.QuantityReceived)).HasColumnName("quantity_received").IsRequired();
        MapNumeric(entity.Property(e => e.BonusQuantity)).HasColumnName("bonus_quantity").IsRequired();
        MapPaisa(entity.Property(e => e.CostPricePerUnitPaisa)).HasColumnName("cost_price_per_unit_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.RetailPricePerUnitPaisa)).HasColumnName("retail_price_per_unit_paisa").IsRequired();
        entity.Property(e => e.BatchNumber).HasColumnName("batch_number").HasMaxLength(100).IsRequired();
        entity.Property(e => e.ExpiryDate).HasColumnName("expiry_date");
        entity.Property(e => e.RackLocation).HasColumnName("rack_location").HasMaxLength(50);
        entity.Property(e => e.BatchId).HasColumnName("batch_id");

        entity.HasOne(e => e.PurchaseOrder)
            .WithMany(o => o.Items)
            .HasForeignKey(e => e.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Batch)
            .WithMany(b => b.PurchaseItems)
            .HasForeignKey(e => e.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureDailyMilkCollection(EntityTypeBuilder<DailyMilkCollection> entity)
    {
        entity.ToTable("daily_milk_collections");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.SupplierId).HasColumnName("supplier_id").IsRequired();
        entity.Property(e => e.MilkType).HasColumnName("milk_type").HasMaxLength(20).IsRequired();
        MapNumeric(entity.Property(e => e.LitersReceived)).HasColumnName("liters_received").IsRequired();
        MapNumeric(entity.Property(e => e.FatPercent)).HasColumnName("fat_percent");
        MapNumeric(entity.Property(e => e.SnfPercent)).HasColumnName("snf_percent");
        MapPaisa(entity.Property(e => e.RatePerLiterPaisa)).HasColumnName("rate_per_liter_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.TotalCreditPaisa)).HasColumnName("total_credit_paisa").IsRequired();
        entity.Property(e => e.CollectionTime).HasColumnName("collection_time").IsRequired();

        entity.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureProductionLog(EntityTypeBuilder<ProductionLog> entity)
    {
        entity.ToTable("production_logs");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.BatchReference).HasColumnName("batch_reference").HasMaxLength(100).IsRequired();
        entity.Property(e => e.OperatorId).HasColumnName("operator_id").IsRequired();
        MapPaisa(entity.Property(e => e.AdditionalOverheadPaisa)).HasColumnName("additional_overhead_paisa").IsRequired();
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasIndex(e => e.BatchReference).IsUnique();

        entity.HasOne(e => e.Operator)
            .WithMany()
            .HasForeignKey(e => e.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureProductionConsumptionItem(EntityTypeBuilder<ProductionConsumptionItem> entity)
    {
        entity.ToTable("production_consumption_items");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ProductionLogId).HasColumnName("production_log_id").IsRequired();
        entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        MapNumeric(entity.Property(e => e.QuantityConsumed)).HasColumnName("quantity_consumed").IsRequired();

        entity.HasOne(e => e.ProductionLog)
            .WithMany(l => l.ConsumptionItems)
            .HasForeignKey(e => e.ProductionLogId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureProductionYieldItem(EntityTypeBuilder<ProductionYieldItem> entity)
    {
        entity.ToTable("production_yield_items");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ProductionLogId).HasColumnName("production_log_id").IsRequired();
        entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        MapNumeric(entity.Property(e => e.QuantityProduced)).HasColumnName("quantity_produced").IsRequired();
        MapPaisa(entity.Property(e => e.CalculatedCostPricePaisa)).HasColumnName("calculated_cost_price_paisa").IsRequired();
        entity.Property(e => e.TargetBatchId).HasColumnName("target_batch_id");

        entity.HasOne(e => e.ProductionLog)
            .WithMany(l => l.YieldItems)
            .HasForeignKey(e => e.ProductionLogId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.TargetBatch)
            .WithMany(b => b.ProductionYieldItems)
            .HasForeignKey(e => e.TargetBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSalesInvoice(EntityTypeBuilder<SalesInvoice> entity)
    {
        entity.ToTable("sales_invoices");

        entity.HasKey(e => e.InvoiceNo);

        entity.Property(e => e.InvoiceNo)
            .HasColumnName("invoice_no")
            .HasMaxLength(100)
            .ValueGeneratedNever()
            .IsRequired();
        entity.Property(e => e.ShiftId).HasColumnName("shift_id").IsRequired();
        entity.Property(e => e.TerminalId).HasColumnName("terminal_id");
        entity.Property(e => e.CashierId).HasColumnName("cashier_id").IsRequired();
        entity.Property(e => e.CustomerId).HasColumnName("customer_id");
        MapPaisa(entity.Property(e => e.TotalAmountPaisa)).HasColumnName("total_amount_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.TaxAmountPaisa)).HasColumnName("tax_amount_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.DiscountAmountPaisa)).HasColumnName("discount_amount_paisa").IsRequired();
        entity.Property(e => e.DiscountReason).HasColumnName("discount_reason").HasMaxLength(255);
        entity.Property(e => e.ReceiptNumber).HasColumnName("receipt_number").HasMaxLength(100).IsRequired();
        entity.Property(e => e.PaymentMethod).HasColumnName("payment_method").HasMaxLength(50).IsRequired();
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasOne(e => e.Shift)
            .WithMany()
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Terminal)
            .WithMany()
            .HasForeignKey(e => e.TerminalId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Cashier)
            .WithMany()
            .HasForeignKey(e => e.CashierId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSalesItem(EntityTypeBuilder<SalesItem> entity)
    {
        entity.ToTable("sales_items");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.InvoiceNo).HasColumnName("invoice_no").HasMaxLength(100).IsRequired();
        entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        entity.Property(e => e.BatchId).HasColumnName("batch_id").IsRequired();
        MapNumeric(entity.Property(e => e.Quantity)).HasColumnName("quantity").IsRequired();
        MapPaisa(entity.Property(e => e.UnitPricePaisa)).HasColumnName("unit_price_paisa").IsRequired();
        MapPaisa(entity.Property(e => e.DiscountAppliedPaisa)).HasColumnName("discount_applied_paisa").IsRequired();

        entity.HasOne(e => e.Invoice)
            .WithMany(i => i.Items)
            .HasForeignKey(e => e.InvoiceNo)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Batch)
            .WithMany(b => b.SalesItems)
            .HasForeignKey(e => e.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSalesReturn(EntityTypeBuilder<SalesReturn> entity)
    {
        entity.ToTable("sales_returns");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.OriginalInvoiceNo).HasColumnName("original_invoice_no").HasMaxLength(100).IsRequired();
        entity.Property(e => e.CashierId).HasColumnName("cashier_id").IsRequired();
        entity.Property(e => e.CustomerId).HasColumnName("customer_id");
        MapPaisa(entity.Property(e => e.TotalRefundPaisa)).HasColumnName("total_refund_paisa").IsRequired();
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasOne(e => e.OriginalInvoice)
            .WithMany(i => i.SalesReturns)
            .HasForeignKey(e => e.OriginalInvoiceNo)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Cashier)
            .WithMany()
            .HasForeignKey(e => e.CashierId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSalesReturnItem(EntityTypeBuilder<SalesReturnItem> entity)
    {
        entity.ToTable("sales_return_items");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.SalesReturnId).HasColumnName("sales_return_id").IsRequired();
        entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        entity.Property(e => e.BatchId).HasColumnName("batch_id").IsRequired();
        MapNumeric(entity.Property(e => e.Quantity)).HasColumnName("quantity").IsRequired();
        MapPaisa(entity.Property(e => e.RefundUnitPricePaisa)).HasColumnName("refund_unit_price_paisa").IsRequired();
        entity.Property(e => e.ReturnCondition).HasColumnName("return_condition").HasMaxLength(20).IsRequired();

        entity.HasOne(e => e.SalesReturn)
            .WithMany(r => r.Items)
            .HasForeignKey(e => e.SalesReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Batch)
            .WithMany(b => b.SalesReturnItems)
            .HasForeignKey(e => e.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePurchaseReturn(EntityTypeBuilder<PurchaseReturn> entity)
    {
        entity.ToTable("purchase_returns");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.OriginalPurchaseOrderId).HasColumnName("original_purchase_order_id").IsRequired();
        entity.Property(e => e.ManagerId).HasColumnName("manager_id").IsRequired();
        entity.Property(e => e.SupplierId).HasColumnName("supplier_id").IsRequired();
        MapPaisa(entity.Property(e => e.TotalCreditDeductionPaisa)).HasColumnName("total_credit_deduction_paisa").IsRequired();
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasOne(e => e.OriginalPurchaseOrder)
            .WithMany(o => o.PurchaseReturns)
            .HasForeignKey(e => e.OriginalPurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePurchaseReturnItem(EntityTypeBuilder<PurchaseReturnItem> entity)
    {
        entity.ToTable("purchase_return_items");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.PurchaseReturnId).HasColumnName("purchase_return_id").IsRequired();
        entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        entity.Property(e => e.BatchId).HasColumnName("batch_id").IsRequired();
        MapNumeric(entity.Property(e => e.Quantity)).HasColumnName("quantity").IsRequired();
        MapPaisa(entity.Property(e => e.CostPerUnitPaisa)).HasColumnName("cost_per_unit_paisa").IsRequired();

        entity.HasOne(e => e.PurchaseReturn)
            .WithMany(r => r.Items)
            .HasForeignKey(e => e.PurchaseReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Batch)
            .WithMany(b => b.PurchaseReturnItems)
            .HasForeignKey(e => e.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureDamagedStockLog(EntityTypeBuilder<DamagedStockLog> entity)
    {
        entity.ToTable("damaged_stock_logs");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        entity.Property(e => e.BatchId).HasColumnName("batch_id").IsRequired();
        MapNumeric(entity.Property(e => e.Quantity)).HasColumnName("quantity").IsRequired();
        entity.Property(e => e.ReasonCode).HasColumnName("reason_code").HasMaxLength(50).IsRequired();
        entity.Property(e => e.LoggedBy).HasColumnName("logged_by").IsRequired();
        MapPaisa(entity.Property(e => e.WriteOffLossPaisa)).HasColumnName("write_off_loss_paisa").IsRequired();
        entity.Property(e => e.LoggedAt).HasColumnName("logged_at").IsRequired();

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Batch)
            .WithMany(b => b.DamagedStockLogs)
            .HasForeignKey(e => e.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.LoggedByUser)
            .WithMany()
            .HasForeignKey(e => e.LoggedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static PropertyBuilder<long> MapPaisa(PropertyBuilder<long> property) =>
        property.HasColumnType("bigint");

    private static PropertyBuilder<long?> MapPaisa(PropertyBuilder<long?> property) =>
        property.HasColumnType("bigint");

    private static PropertyBuilder<decimal> MapNumeric(PropertyBuilder<decimal> property) =>
        property.HasColumnType("numeric(12,3)");

    private static PropertyBuilder<decimal?> MapNumeric(PropertyBuilder<decimal?> property) =>
        property.HasColumnType("numeric(12,3)");
}
