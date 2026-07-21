using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;

const string MigrationId = "20260716162915_AddMultiTenantFoundation";
string[] tenantTables =
[
    "roles",
    "users",
    "terminals",
    "cashier_shifts",
    "shift_expenses",
    "parties",
    "party_ledgers",
    "general_ledger_entries",
    "categories",
    "products",
    "purchase_orders",
    "product_batches",
    "purchase_items",
    "daily_milk_collections",
    "production_logs",
    "production_consumption_items",
    "production_yield_items",
    "sales_invoices",
    "sales_items",
    "sales_returns",
    "sales_return_items",
    "purchase_returns",
    "purchase_return_items",
    "damaged_stock_logs"
];

string connectionString = LoadConnectionString();
Guid masterTenantId = TenantDefaults.MasterTenantId;

await using NpgsqlConnection connection = new(connectionString);
await connection.OpenAsync();

await VerifyMigrationAppliedAsync(connection);

long totalRows = 0;
foreach (string table in tenantTables)
{
    await VerifyTenantSchemaAsync(connection, table);
    long tableRows = await VerifyMasterTenantDataAsync(
        connection,
        table,
        masterTenantId);
    totalRows += tableRows;
    Console.WriteLine($"PASS {table}: schema valid; {tableRows} Master Tenant rows.");
}

long directProductCount = await ExecuteScalarAsync<long>(
    connection,
    """
    SELECT COUNT(*)
    FROM "products"
    WHERE "tenant_id" = @masterTenantId;
    """,
    new NpgsqlParameter("masterTenantId", masterTenantId));

DbContextOptions<WebPosDbContext> options =
    new DbContextOptionsBuilder<WebPosDbContext>()
        .UseNpgsql(connectionString)
        .Options;
await using WebPosDbContext context = new(
    options,
    new MasterTenantService(masterTenantId));
int filteredProductCount = await context.Products.CountAsync();

if (filteredProductCount != directProductCount)
{
    throw new InvalidOperationException(
        $"Product tenant-filter sanity check failed: expected {directProductCount}, returned {filteredProductCount}.");
}

Console.WriteLine(
    $"PASS verification complete: {tenantTables.Length} tables, {totalRows} existing rows, " +
    $"{filteredProductCount} Master Tenant products.");
return;

static string LoadConnectionString()
{
    string? environmentConnection =
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    if (!string.IsNullOrWhiteSpace(environmentConnection))
    {
        return environmentConnection;
    }

    string settingsPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "WebPos",
        "appsettings.json");
    using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
    string? connectionString = settings.RootElement
        .GetProperty("ConnectionStrings")
        .GetProperty("DefaultConnection")
        .GetString();

    return !string.IsNullOrWhiteSpace(connectionString)
        ? connectionString
        : throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection is not configured.");
}

static async Task VerifyMigrationAppliedAsync(NpgsqlConnection connection)
{
    bool isApplied = await ExecuteScalarAsync<bool>(
        connection,
        """
        SELECT EXISTS (
            SELECT 1
            FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = @migrationId
        );
        """,
        new NpgsqlParameter("migrationId", MigrationId));

    if (!isApplied)
    {
        throw new InvalidOperationException(
            $"Required migration '{MigrationId}' is not applied.");
    }
}

static async Task VerifyTenantSchemaAsync(
    NpgsqlConnection connection,
    string table)
{
    bool hasRequiredColumn = await ExecuteScalarAsync<bool>(
        connection,
        """
        SELECT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = @table
              AND column_name = 'tenant_id'
              AND data_type = 'uuid'
              AND is_nullable = 'NO'
        );
        """,
        new NpgsqlParameter("table", table));

    bool hasIndex = await ExecuteScalarAsync<bool>(
        connection,
        """
        SELECT EXISTS (
            SELECT 1
            FROM pg_indexes
            WHERE schemaname = current_schema()
              AND tablename = @table
              AND indexname = @indexName
              AND indexdef LIKE '%(tenant_id)%'
        );
        """,
        new NpgsqlParameter("table", table),
        new NpgsqlParameter("indexName", $"IX_{table}_tenant_id"));

    bool hasForeignKey = await ExecuteScalarAsync<bool>(
        connection,
        """
        SELECT EXISTS (
            SELECT 1
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON kcu.constraint_schema = tc.constraint_schema
             AND kcu.constraint_name = tc.constraint_name
            JOIN information_schema.constraint_column_usage ccu
              ON ccu.constraint_schema = tc.constraint_schema
             AND ccu.constraint_name = tc.constraint_name
            WHERE tc.table_schema = current_schema()
              AND tc.table_name = @table
              AND tc.constraint_type = 'FOREIGN KEY'
              AND tc.constraint_name = @foreignKeyName
              AND kcu.column_name = 'tenant_id'
              AND ccu.table_name = 'tenants'
              AND ccu.column_name = 'id'
        );
        """,
        new NpgsqlParameter("table", table),
        new NpgsqlParameter(
            "foreignKeyName",
            $"FK_{table}_tenants_tenant_id"));

    if (!hasRequiredColumn || !hasIndex || !hasForeignKey)
    {
        throw new InvalidOperationException(
            $"Schema verification failed for '{table}': " +
            $"column={hasRequiredColumn}, index={hasIndex}, foreignKey={hasForeignKey}.");
    }
}

static async Task<long> VerifyMasterTenantDataAsync(
    NpgsqlConnection connection,
    string table,
    Guid masterTenantId)
{
    // Table names are selected exclusively from the fixed whitelist above.
    long invalidRows = await ExecuteScalarAsync<long>(
        connection,
        $"""
         SELECT COUNT(*)
         FROM "{table}"
         WHERE "tenant_id" IS NULL OR "tenant_id" <> @masterTenantId;
         """,
        new NpgsqlParameter("masterTenantId", masterTenantId));

    if (invalidRows != 0)
    {
        throw new InvalidOperationException(
            $"Data verification failed for '{table}': {invalidRows} rows are not assigned to the Master Tenant.");
    }

    return await ExecuteScalarAsync<long>(
        connection,
        $"""SELECT COUNT(*) FROM "{table}";""");
}

static async Task<T> ExecuteScalarAsync<T>(
    NpgsqlConnection connection,
    string sql,
    params NpgsqlParameter[] parameters)
{
    await using NpgsqlCommand command = new(sql, connection);
    command.Parameters.AddRange(parameters);
    object? value = await command.ExecuteScalarAsync();
    return value is T result
        ? result
        : throw new InvalidOperationException(
            $"Unexpected scalar result for verification query: {value ?? "NULL"}.");
}

file sealed class MasterTenantService(Guid tenantId) : ITenantService
{
    public Guid TenantId { get; } = tenantId;

    public bool IsResolved => true;
}
