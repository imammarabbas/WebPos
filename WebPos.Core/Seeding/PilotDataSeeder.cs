using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPos.Core;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Models;
using WebPos.Core.Security;

namespace WebPos.Core.Seeding;

/// <summary>
/// One-time pilot identity + Cone Mart catalog seed for go-live.
/// </summary>
public static class PilotDataSeeder
{
    public const string StoreName = "Cone Mart";

    public static readonly Guid PilotTerminalId =
        Guid.Parse("00000000-0000-0000-0000-000000000010");

    public static readonly Guid DairyCategoryId =
        Guid.Parse("00000000-0000-0000-0000-000000000401");

    public static readonly Guid GroceryCategoryId =
        Guid.Parse("00000000-0000-0000-0000-000000000402");

    public static readonly Guid BakeryCategoryId =
        Guid.Parse("00000000-0000-0000-0000-000000000403");

    public static readonly Guid BuffaloProductId =
        Guid.Parse("00000000-0000-0000-0000-000000000101");

    public static readonly Guid CowProductId =
        Guid.Parse("00000000-0000-0000-0000-000000000102");

    public static readonly Guid BananaProductId =
        Guid.Parse("00000000-0000-0000-0000-000000000103");

    public static readonly Guid SamosaProductId =
        Guid.Parse("00000000-0000-0000-0000-000000000104");

    public static readonly Guid BuffaloBatchId =
        Guid.Parse("00000000-0000-0000-0000-000000000201");

    public static readonly Guid CowBatchId =
        Guid.Parse("00000000-0000-0000-0000-000000000202");

    public static readonly Guid BananaBatchId =
        Guid.Parse("00000000-0000-0000-0000-000000000203");

    public static readonly Guid SamosaBatchId =
        Guid.Parse("00000000-0000-0000-0000-000000000204");

    public static readonly Guid PilotSupplierId =
        Guid.Parse("00000000-0000-0000-0000-000000000301");

    public static readonly Guid PilotCustomerId =
        Guid.Parse("00000000-0000-0000-0000-000000000501");

    public static async Task SeedAsync(
        WebPosDbContext context,
        IPinHasher pinHasher,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pinHasher);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid masterTenantId = TenantDefaults.MasterTenantId;
        string adminPassword = configuration["Pilot:AdminPassword"] ?? "admin123";
        string ownerPassword = configuration["Pilot:OwnerPassword"] ?? "ammar123";
        string managerPin = configuration["Pilot:ManagerPin"] ?? "8642";
        string ownerPin = configuration["Pilot:OwnerPin"] ?? "9753";
        string cashierPin = configuration["Pilot:CashierPin"] ?? "2468";
        string abcCashierPin = configuration["Pilot:AbcCashierPin"] ?? "1357";
        string? pilotTerminalIdRaw = configuration["Pilot:TerminalId"];
        Guid pilotTerminalId = Guid.TryParse(pilotTerminalIdRaw, out Guid parsed)
            ? parsed
            : PilotTerminalId;

        if (!await context.Tenants.AnyAsync(t => t.Id == masterTenantId, cancellationToken))
        {
            context.Tenants.Add(new Tenant
            {
                Id = masterTenantId,
                Name = StoreName,
                PosDisplayName = StoreName,
                Slug = "cone-mart",
                IsActive = true,
                CreatedAt = now
            });
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded master tenant {TenantId} ({StoreName})", masterTenantId, StoreName);
        }
        else
        {
            Tenant master = await context.Tenants
                .IgnoreQueryFilters()
                .SingleAsync(t => t.Id == masterTenantId, cancellationToken);
            if (string.Equals(master.Name, "Copenhagen Mart", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(master.Name))
            {
                master.Name = StoreName;
                logger.LogInformation("Renamed master tenant store name to {StoreName}.", StoreName);
            }

            if (string.IsNullOrWhiteSpace(master.PosDisplayName)
                || string.Equals(master.PosDisplayName, "Copenhagen Mart", StringComparison.OrdinalIgnoreCase))
            {
                master.PosDisplayName = string.IsNullOrWhiteSpace(master.Name) ? StoreName : master.Name;
            }
        }

        Role ownerRole = await EnsureRoleAsync(context, masterTenantId, "Owner", now, cancellationToken);
        Role managerRole = await EnsureManagerRoleAsync(context, masterTenantId, now, logger, cancellationToken);
        Role cashierRole = await EnsureRoleAsync(context, masterTenantId, "Cashier", now, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await UpsertWebUserAsync(
            context,
            pinHasher,
            configuration,
            masterTenantId,
            username: "ammar",
            password: ownerPassword,
            pin: ownerPin,
            roleId: ownerRole.Id,
            now,
            logger,
            "Seeded owner user ammar. Change Pilot:OwnerPassword / Pilot:OwnerPin before production use.",
            cancellationToken);

        await UpsertWebUserAsync(
            context,
            pinHasher,
            configuration,
            masterTenantId,
            username: "admin",
            password: adminPassword,
            pin: managerPin,
            roleId: managerRole.Id,
            now,
            logger,
            "Seeded manager user admin. Change Pilot:AdminPassword / Pilot:ManagerPin before production use.",
            cancellationToken);

        if (ShouldForceResetPilotCredentials(configuration))
        {
            await DeactivateDuplicateWebUsersAsync(
                context,
                masterTenantId,
                ["admin", "ammar"],
                now,
                logger,
                cancellationToken);
        }

        await UpsertCashierUserAsync(
            context,
            pinHasher,
            masterTenantId,
            username: "abc",
            pin: abcCashierPin,
            roleId: cashierRole.Id,
            now,
            logger,
            "Seeded cashier abc with PIN (Pilot:AbcCashierPin).",
            cancellationToken);

        await UpsertCashierUserAsync(
            context,
            pinHasher,
            masterTenantId,
            username: "cashier",
            pin: cashierPin,
            roleId: cashierRole.Id,
            now,
            logger,
            "Seeded legacy cashier with PIN hash (Pilot:CashierPin).",
            cancellationToken);

        Terminal? terminal = await context.Terminals
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == pilotTerminalId, cancellationToken);
        if (terminal is null)
        {
            context.Terminals.Add(new Terminal
            {
                Id = pilotTerminalId,
                TenantId = masterTenantId,
                TerminalName = $"{StoreName} POS",
                MacAddress = "PILOT-TERMINAL-001",
                IsActive = true,
                LastSyncTime = now
            });
            logger.LogInformation("Seeded pilot terminal {TerminalId}", pilotTerminalId);
        }
        else
        {
            // After DB restore, terminal may point at a different tenant than pilot admin users.
            // Master login ignores tenant; enrollment does not — keep them aligned.
            if (terminal.TenantId != masterTenantId)
            {
                logger.LogWarning(
                    "Rebinding pilot terminal {TerminalId} from tenant {OldTenantId} to master {MasterTenantId}.",
                    pilotTerminalId,
                    terminal.TenantId,
                    masterTenantId);
                terminal.TenantId = masterTenantId;
            }

            if (!terminal.IsActive)
            {
                terminal.IsActive = true;
                logger.LogInformation("Reactivated pilot terminal {TerminalId}.", pilotTerminalId);
            }

            string expectedName = $"{StoreName} POS";
            if (!string.Equals(terminal.TerminalName, expectedName, StringComparison.Ordinal))
            {
                terminal.TerminalName = expectedName;
                logger.LogInformation("Updated pilot terminal name to {StoreName} POS", StoreName);
            }
        }

        await SeedCatalogAsync(context, masterTenantId, now, logger, cancellationToken);
        await EnsurePilotCustomerAsync(context, masterTenantId, now, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await CashAccountSeeder.SeedAsync(context, logger, cancellationToken);
    }

    private static async Task<Role> EnsureRoleAsync(
        WebPosDbContext context,
        Guid tenantId,
        string roleName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Role? role = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.RoleName == roleName && r.TenantId == tenantId,
                cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RoleName = roleName,
            CreatedAt = now
        };
        context.Roles.Add(role);
        return role;
    }

    /// <summary>
    /// Prefer an existing Manager role; otherwise rename legacy Admin → Manager in place.
    /// </summary>
    private static async Task<Role> EnsureManagerRoleAsync(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset now,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        Role? manager = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.RoleName == "Manager" && r.TenantId == tenantId,
                cancellationToken);
        if (manager is not null)
        {
            return manager;
        }

        Role? legacyAdmin = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.RoleName == "Admin" && r.TenantId == tenantId,
                cancellationToken);
        if (legacyAdmin is not null)
        {
            legacyAdmin.RoleName = "Manager";
            logger.LogInformation("Renamed legacy Admin role to Manager.");
            return legacyAdmin;
        }

        return await EnsureRoleAsync(context, tenantId, "Manager", now, cancellationToken);
    }

    private static async Task UpsertWebUserAsync(
        WebPosDbContext context,
        IPinHasher pinHasher,
        IConfiguration configuration,
        Guid tenantId,
        string username,
        string password,
        string pin,
        Guid roleId,
        DateTimeOffset now,
        ILogger logger,
        string createMessage,
        CancellationToken cancellationToken)
    {
        User? user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                u => u.Username == username && u.TenantId == tenantId,
                cancellationToken);

        string passwordHash = CryptoHelper.HashPassword(password);
        string pinHash = pinHasher.HashPin(pin);
        if (user is null)
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Username = username,
                PasswordHash = passwordHash,
                PinHash = pinHash,
                RoleId = roleId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            logger.LogWarning("{Message}", createMessage);
            return;
        }

        user.RoleId = roleId;
        user.IsActive = true;
        bool forceReset = ShouldForceResetPilotCredentials(configuration);
        bool passwordChanged = forceReset
            || !CryptoHelper.VerifyPassword(password, user.PasswordHash);
        if (passwordChanged)
        {
            user.PasswordHash = passwordHash;
            logger.LogInformation(
                "Refreshed password hash for user {Username}{Reason}.",
                username,
                forceReset ? " (forced pilot reset)" : string.Empty);
        }

        if (forceReset || !string.Equals(user.PinHash, pinHash, StringComparison.Ordinal))
        {
            user.PinHash = pinHash;
            logger.LogInformation("Refreshed terminal PIN hash for user {Username}.", username);
        }

        user.UpdatedAt = now;
    }

    /// <summary>
    /// After restore, orphan admin/ammar rows on non-master tenants confuse enrollment.
    /// Keep the master-tenant pilot users; deactivate the rest when force-resetting.
    /// </summary>
    private static async Task DeactivateDuplicateWebUsersAsync(
        WebPosDbContext context,
        Guid masterTenantId,
        IReadOnlyList<string> usernames,
        DateTimeOffset now,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        List<string> normalized = usernames
            .Select(u => u.ToLowerInvariant())
            .Distinct()
            .ToList();

        List<User> orphans = await context.Users
            .IgnoreQueryFilters()
            .Where(u =>
                u.IsActive
                && u.TenantId != masterTenantId
                && normalized.Contains(u.Username.ToLower()))
            .ToListAsync(cancellationToken);

        foreach (User orphan in orphans)
        {
            orphan.IsActive = false;
            orphan.UpdatedAt = now;
            logger.LogWarning(
                "Deactivated duplicate pilot user {Username} on tenant {TenantId} (keeping master tenant).",
                orphan.Username,
                orphan.TenantId);
        }
    }

    private static bool ShouldForceResetPilotCredentials(IConfiguration configuration) =>
        configuration.GetValue("Pilot:ForceResetPasswords", false)
        || configuration.GetValue("Security:AllowInsecureDevDefaults", false)
        || string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development",
            StringComparison.OrdinalIgnoreCase);

    private static async Task UpsertCashierUserAsync(
        WebPosDbContext context,
        IPinHasher pinHasher,
        Guid tenantId,
        string username,
        string pin,
        Guid roleId,
        DateTimeOffset now,
        ILogger logger,
        string createMessage,
        CancellationToken cancellationToken)
    {
        User? user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                u => u.Username == username && u.TenantId == tenantId,
                cancellationToken);

        string pinHash = pinHasher.HashPin(pin);
        if (user is null)
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Username = username,
                PasswordHash = CryptoHelper.HashPassword(pin),
                PinHash = pinHash,
                RoleId = roleId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            logger.LogInformation("{Message}", createMessage);
            return;
        }

        user.RoleId = roleId;
        user.IsActive = true;
        if (!string.Equals(user.PinHash, pinHash, StringComparison.Ordinal))
        {
            user.PinHash = pinHash;
            user.PasswordHash = CryptoHelper.HashPassword(pin);
            user.UpdatedAt = now;
            logger.LogInformation("Refreshed PIN hash for cashier {Username}.", username);
        }
    }

    private static async Task SeedCatalogAsync(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset now,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await EnsureSupplierAsync(context, tenantId, now, cancellationToken);
        await EnsureCategoriesAsync(context, tenantId, now, cancellationToken);

        await EnsureProductAsync(
            context,
            tenantId,
            BuffaloProductId,
            DairyCategoryId,
            name: "Buffalo Milk",
            sku: "MILK-BUF",
            barcode: "8900000000001",
            shortCode: "1001",
            isLoose: false,
            baseUnit: "L",
            purchaseUnit: null,
            conversionMultiplier: 1,
            now,
            cancellationToken);

        await EnsureProductAsync(
            context,
            tenantId,
            CowProductId,
            DairyCategoryId,
            name: "Cow Milk",
            sku: "MILK-COW",
            barcode: "8900000000002",
            shortCode: "1002",
            isLoose: false,
            baseUnit: "L",
            purchaseUnit: null,
            conversionMultiplier: 1,
            now,
            cancellationToken);

        await EnsureProductAsync(
            context,
            tenantId,
            BananaProductId,
            GroceryCategoryId,
            name: "Banana",
            sku: "LOOSE-BAN",
            barcode: "LOOSE-2001",
            shortCode: "2001",
            isLoose: true,
            baseUnit: "g",
            purchaseUnit: "kg",
            conversionMultiplier: 1000,
            now,
            cancellationToken);

        await EnsureProductAsync(
            context,
            tenantId,
            SamosaProductId,
            BakeryCategoryId,
            name: "Samosa",
            sku: "LOOSE-SAM",
            barcode: "LOOSE-2002",
            shortCode: "2002",
            isLoose: true,
            baseUnit: "pcs",
            purchaseUnit: null,
            conversionMultiplier: 1,
            now,
            cancellationToken);

        await EnsureBatchAsync(
            context,
            tenantId,
            BuffaloBatchId,
            BuffaloProductId,
            batchNumber: "BUF-PILOT-01",
            retailPricePaisa: 22_000,
            costPricePaisa: 18_000,
            qty: 100m,
            rack: "COLD-1",
            now,
            cancellationToken);

        await EnsureBatchAsync(
            context,
            tenantId,
            CowBatchId,
            CowProductId,
            batchNumber: "COW-PILOT-01",
            retailPricePaisa: 18_000,
            costPricePaisa: 14_000,
            qty: 100m,
            rack: "COLD-1",
            now,
            cancellationToken);

        await EnsureBatchAsync(
            context,
            tenantId,
            BananaBatchId,
            BananaProductId,
            batchNumber: "BAN-PILOT-01",
            // Prices in sale units (paisa per gram); 280 Rs/kg ≈ 28 paisa/g.
            retailPricePaisa: 28,
            costPricePaisa: 22,
            qty: 50_000m,
            rack: "PRODUCE-1",
            now,
            cancellationToken);

        await EnsureBatchAsync(
            context,
            tenantId,
            SamosaBatchId,
            SamosaProductId,
            batchNumber: "SAM-PILOT-01",
            retailPricePaisa: 1_800,
            costPricePaisa: 1_200,
            qty: 200m,
            rack: "BAKERY-1",
            now,
            cancellationToken);

        logger.LogInformation(
            "Pilot catalog ready for {StoreName} (milk + loose items with short codes).",
            StoreName);
        await Task.CompletedTask;
    }

    private static async Task EnsureSupplierAsync(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await context.Parties
                .IgnoreQueryFilters()
                .AnyAsync(p => p.Id == PilotSupplierId, cancellationToken))
        {
            return;
        }

        context.Parties.Add(new Party
        {
            Id = PilotSupplierId,
            TenantId = tenantId,
            PartyType = "SUPPLIER",
            Name = "Pilot Dairy Farm",
            PhoneNumber = "03001234567",
            Address = "Pilot Farm",
            CreditLimitPaisa = 0,
            CurrentBalancePaisa = 0,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static async Task EnsureCategoriesAsync(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await EnsureCategoryAsync(context, tenantId, DairyCategoryId, "Dairy", now, cancellationToken);
        await EnsureCategoryAsync(context, tenantId, GroceryCategoryId, "Grocery", now, cancellationToken);
        await EnsureCategoryAsync(context, tenantId, BakeryCategoryId, "Bakery", now, cancellationToken);
    }

    private static async Task EnsureCategoryAsync(
        WebPosDbContext context,
        Guid tenantId,
        Guid categoryId,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Category? existing = await context.Categories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);

        if (existing is not null)
        {
            return;
        }

        context.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = name,
            TargetMarginPercentage = 15,
            ShowOnWebshop = false,
            CreatedAt = now
        });
    }

    private static async Task EnsureProductAsync(
        WebPosDbContext context,
        Guid tenantId,
        Guid productId,
        Guid categoryId,
        string name,
        string sku,
        string barcode,
        string shortCode,
        bool isLoose,
        string baseUnit,
        string? purchaseUnit,
        int conversionMultiplier,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Product? existing = await context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        string buyUnit = purchaseUnit?.Trim() ?? string.Empty;
        int multiplier = UnitConversion.NormalizeMultiplier(conversionMultiplier);

        if (existing is null)
        {
            context.Products.Add(new Product
            {
                Id = productId,
                TenantId = tenantId,
                CategoryId = categoryId,
                Name = name,
                Sku = sku,
                Barcode = barcode,
                ShortCode = shortCode,
                IsLoose = isLoose,
                Brand = StoreName,
                BaseUnit = baseUnit,
                PurchaseUnit = buyUnit,
                ConversionMultiplier = multiplier,
                ShowOnWebshop = false,
                CreatedAt = now,
                UpdatedAt = now
            });
            return;
        }

        bool changed = false;
        if (existing.CategoryId != categoryId)
        {
            existing.CategoryId = categoryId;
            changed = true;
        }

        if (!string.Equals(existing.BaseUnit, baseUnit, StringComparison.Ordinal))
        {
            existing.BaseUnit = baseUnit;
            changed = true;
        }

        if (!string.Equals(existing.PurchaseUnit, buyUnit, StringComparison.Ordinal))
        {
            existing.PurchaseUnit = buyUnit;
            changed = true;
        }

        if (existing.ConversionMultiplier != multiplier)
        {
            existing.ConversionMultiplier = multiplier;
            changed = true;
        }

        if (existing.IsLoose != isLoose)
        {
            existing.IsLoose = isLoose;
            changed = true;
        }

        if (changed)
        {
            existing.UpdatedAt = now;
        }
    }

    private static async Task EnsureBatchAsync(
        WebPosDbContext context,
        Guid tenantId,
        Guid batchId,
        Guid productId,
        string batchNumber,
        long retailPricePaisa,
        long costPricePaisa,
        decimal qty,
        string rack,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await context.ProductBatches
                .IgnoreQueryFilters()
                .AnyAsync(b => b.Id == batchId, cancellationToken))
        {
            return;
        }

        context.ProductBatches.Add(new ProductBatch
        {
            Id = batchId,
            TenantId = tenantId,
            ProductId = productId,
            BatchNumber = batchNumber,
            ExpiryDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(2)),
            CostPricePaisa = costPricePaisa,
            RetailPricePaisa = retailPricePaisa,
            InitialQty = qty,
            CurrentQty = qty,
            SupplierId = PilotSupplierId,
            RackLocation = rack,
            CreatedAt = now
        });
    }

    private static async Task EnsurePilotCustomerAsync(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await context.Parties
                .IgnoreQueryFilters()
                .AnyAsync(p => p.Id == PilotCustomerId, cancellationToken))
        {
            return;
        }

        context.Parties.Add(new Party
        {
            Id = PilotCustomerId,
            TenantId = tenantId,
            PartyType = "CUSTOMER",
            Name = "Cone Mart Regular",
            PhoneNumber = "03009998877",
            Address = "Cone Mart",
            CreditLimitPaisa = 500_000,
            CurrentBalancePaisa = 0,
            CreatedAt = now,
            UpdatedAt = now
        });
        await Task.CompletedTask;
    }
}
