using Microsoft.EntityFrameworkCore;
using WebPos.Core.Data;
using WebPos.Core.Models;
namespace WebPos.Core.Services;
public interface ISalesInvoiceQueryService
{
    Task<IReadOnlyList<SalesInvoiceSummary>> ListByShiftAsync(
        Guid shiftId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesInvoiceSummary>> ListRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesInvoiceSummary>> SearchAsync(
        SalesInvoiceSearchQuery query,
        CancellationToken cancellationToken = default);
    Task<SalesInvoiceDetail?> GetByInvoiceNoAsync(
        string invoiceNo,
        CancellationToken cancellationToken = default);
}
public sealed class SalesInvoiceSearchQuery
{
    public string? InvoiceQuery { get; init; }
    public string? CustomerQuery { get; init; }
    public string? ProductQuery { get; init; }
    public DateTimeOffset? Since { get; init; }
    public int Limit { get; init; } = 40;
}
public sealed class SalesInvoiceSummary
{
    public required string InvoiceNo { get; init; }
    public required string ReceiptNumber { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public Guid? TerminalId { get; init; }
    public long TotalAmountPaisa { get; init; }
    public long GrossAmountPaisa { get; init; }
    public long DiscountAmountPaisa { get; init; }
    public long TaxAmountPaisa { get; init; }
    public required string PaymentMethod { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public int ItemCount { get; init; }
    public long ReturnedAmountPaisa { get; init; }
    public required IReadOnlyList<string> ProductLabels { get; init; }
}
public sealed class SalesInvoiceLineDetail
{
    public Guid ProductId { get; init; }
    public Guid BatchId { get; init; }
    public required string BatchNumber { get; init; }
    public required string ProductName { get; init; }
    public bool IsLoose { get; init; }
    public decimal QuantitySold { get; init; }
    public decimal QuantityReturned { get; init; }
    public decimal ReturnableQuantity { get; init; }
    public long UnitPricePaisa { get; init; }
}
public sealed class SalesInvoiceDetail
{
    public required string InvoiceNo { get; init; }
    public required string ReceiptNumber { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public Guid ShiftId { get; init; }
    public Guid? TerminalId { get; init; }
    public Guid CashierId { get; init; }
    public long TotalAmountPaisa { get; init; }
    public long GrossAmountPaisa { get; init; }
    public long DiscountAmountPaisa { get; init; }
    public long TaxAmountPaisa { get; init; }
    public required string PaymentMethod { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public required IReadOnlyList<SalesInvoiceLineDetail> Lines { get; init; }
}
public sealed class SalesInvoiceQueryService(WebPosDbContext context) : ISalesInvoiceQueryService
{
    private static readonly TimeSpan DefaultSearchWindow = TimeSpan.FromDays(14);
    private readonly WebPosDbContext _context =
        context ?? throw new ArgumentNullException(nameof(context));
    public async Task<IReadOnlyList<SalesInvoiceSummary>> ListByShiftAsync(
        Guid shiftId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (shiftId == Guid.Empty)
        {
            throw new ArgumentException("Shift id is required.", nameof(shiftId));
        }
        int take = Math.Clamp(limit, 1, 200);
        List<SalesInvoice> invoices = await _context.SalesInvoices
            .AsNoTracking()
            .Include(invoice => invoice.Items)
                .ThenInclude(item => item.Product)
            .Include(invoice => invoice.Customer)
            .Include(invoice => invoice.SalesReturns)
            .Where(invoice => invoice.ShiftId == shiftId)
            .OrderByDescending(invoice => invoice.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return invoices.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<SalesInvoiceSummary>> ListRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        int take = Math.Clamp(limit, 1, 200);
        List<SalesInvoice> invoices = await _context.SalesInvoices
            .AsNoTracking()
            .Include(invoice => invoice.Items)
                .ThenInclude(item => item.Product)
            .Include(invoice => invoice.Customer)
            .Include(invoice => invoice.SalesReturns)
            .OrderByDescending(invoice => invoice.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return invoices.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<SalesInvoiceSummary>> SearchAsync(
        SalesInvoiceSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        string? invoiceQ = NormalizeContains(query.InvoiceQuery?.Trim().TrimStart('#'));
        string? customerQ = NormalizeContains(query.CustomerQuery);
        string? productQ = NormalizeContains(query.ProductQuery);
        if (invoiceQ is null && customerQ is null && productQ is null)
        {
            return [];
        }
        DateTimeOffset since = query.Since ?? DateTimeOffset.UtcNow.Subtract(DefaultSearchWindow);
        int take = Math.Clamp(query.Limit, 1, 200);
        IQueryable<SalesInvoice> invoicesQuery = _context.SalesInvoices
            .AsNoTracking()
            .Include(invoice => invoice.Items)
                .ThenInclude(item => item.Product)
            .Include(invoice => invoice.Customer)
            .Include(invoice => invoice.SalesReturns)
            .Where(invoice => invoice.CreatedAt >= since);
        if (invoiceQ is not null)
        {
            invoicesQuery = invoicesQuery.Where(invoice =>
                EF.Functions.Like(invoice.InvoiceNo, invoiceQ)
                || EF.Functions.Like(invoice.ReceiptNumber, invoiceQ));
        }
        if (customerQ is not null)
        {
            invoicesQuery = invoicesQuery.Where(invoice =>
                invoice.Customer != null
                && (EF.Functions.Like(invoice.Customer.Name, customerQ)
                    || EF.Functions.Like(invoice.Customer.PhoneNumber, customerQ)));
        }
        if (productQ is not null)
        {
            invoicesQuery = invoicesQuery.Where(invoice =>
                invoice.Items.Any(item =>
                    item.Product != null
                    && (EF.Functions.Like(item.Product.Name, productQ)
                        || EF.Functions.Like(item.Product.Sku, productQ)
                        || EF.Functions.Like(item.Product.Barcode, productQ)
                        || EF.Functions.Like(item.Product.ShortCode, productQ))));
        }
        List<SalesInvoice> invoices = await invoicesQuery
            .OrderByDescending(invoice => invoice.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return invoices.Select(ToSummary).ToList();
    }
    public async Task<SalesInvoiceDetail?> GetByInvoiceNoAsync(
        string invoiceNo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceNo))
        {
            throw new ArgumentException("Invoice number is required.", nameof(invoiceNo));
        }
        SalesInvoice? invoice = await _context.SalesInvoices
            .AsNoTracking()
            .Include(i => i.Items)
                .ThenInclude(item => item.Product)
            .Include(i => i.Items)
                .ThenInclude(item => item.Batch)
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo, cancellationToken);
        if (invoice is null)
        {
            return null;
        }
        List<SalesReturnItem> priorReturns = await (
            from ri in _context.SalesReturnItems.AsNoTracking()
            join sr in _context.SalesReturns.AsNoTracking() on ri.SalesReturnId equals sr.Id
            where sr.OriginalInvoiceNo == invoiceNo
            select ri).ToListAsync(cancellationToken);
        long gross = invoice.Items.Sum(item =>
            (long)Math.Round(item.Quantity * item.UnitPricePaisa, MidpointRounding.AwayFromZero));
        List<SalesInvoiceLineDetail> lines = invoice.Items.Select(item =>
        {
            decimal returned = priorReturns
                .Where(r => r.ProductId == item.ProductId && r.BatchId == item.BatchId)
                .Sum(r => r.Quantity);
            decimal returnable = Math.Max(0m, item.Quantity - returned);
            return new SalesInvoiceLineDetail
            {
                ProductId = item.ProductId,
                BatchId = item.BatchId,
                BatchNumber = item.Batch?.BatchNumber ?? string.Empty,
                ProductName = item.Product?.Name ?? "Product",
                IsLoose = item.Product?.IsLoose ?? false,
                QuantitySold = item.Quantity,
                QuantityReturned = returned,
                ReturnableQuantity = returnable,
                UnitPricePaisa = item.UnitPricePaisa
            };
        }).ToList();
        return new SalesInvoiceDetail
        {
            InvoiceNo = invoice.InvoiceNo,
            ReceiptNumber = invoice.ReceiptNumber,
            CreatedAt = invoice.CreatedAt,
            ShiftId = invoice.ShiftId,
            TerminalId = invoice.TerminalId,
            CashierId = invoice.CashierId,
            TotalAmountPaisa = invoice.TotalAmountPaisa,
            GrossAmountPaisa = gross,
            DiscountAmountPaisa = invoice.DiscountAmountPaisa,
            TaxAmountPaisa = invoice.TaxAmountPaisa,
            PaymentMethod = invoice.PaymentMethod,
            CustomerId = invoice.CustomerId,
            CustomerName = invoice.Customer?.Name,
            CustomerPhone = invoice.Customer?.PhoneNumber,
            Lines = lines
        };
    }
    private static SalesInvoiceSummary ToSummary(SalesInvoice invoice)
    {
        long gross = invoice.Items.Sum(item =>
            (long)Math.Round(item.Quantity * item.UnitPricePaisa, MidpointRounding.AwayFromZero));
        return new SalesInvoiceSummary
        {
            InvoiceNo = invoice.InvoiceNo,
            ReceiptNumber = invoice.ReceiptNumber,
            CreatedAt = invoice.CreatedAt,
            TerminalId = invoice.TerminalId,
            TotalAmountPaisa = invoice.TotalAmountPaisa,
            GrossAmountPaisa = gross,
            DiscountAmountPaisa = invoice.DiscountAmountPaisa,
            TaxAmountPaisa = invoice.TaxAmountPaisa,
            PaymentMethod = invoice.PaymentMethod,
            CustomerId = invoice.CustomerId,
            CustomerName = invoice.Customer?.Name,
            CustomerPhone = invoice.Customer?.PhoneNumber,
            ItemCount = invoice.Items.Count,
            ReturnedAmountPaisa = invoice.SalesReturns.Sum(r => r.TotalRefundPaisa),
            ProductLabels = BuildProductLabels(invoice.Items)
        };
    }
    private static IReadOnlyList<string> BuildProductLabels(IEnumerable<SalesItem> items)
    {
        List<string> labels = [];
        foreach (SalesItem item in items)
        {
            if (labels.Count >= 3)
            {
                break;
            }
            string? shortCode = item.Product?.ShortCode?.Trim();
            if (!string.IsNullOrWhiteSpace(shortCode))
            {
                labels.Add(shortCode);
                continue;
            }
            string name = item.Product?.Name?.Trim() ?? "Item";
            labels.Add(name.Length <= 8 ? name : name[..8]);
        }
        return labels;
    }
    private static string? NormalizeContains(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        string trimmed = raw.Trim();
        return $"%{EscapeLike(trimmed)}%";
    }
    private static string EscapeLike(string value) =>
        value
            .Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);
}
