namespace Common.Models;



public sealed class SalesInvoiceSummaryDto

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



    public IReadOnlyList<string> ProductLabels { get; init; } = [];

}



public sealed class SalesInvoiceLineDto

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



public sealed class SalesInvoiceDetailDto

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



    public required IReadOnlyList<SalesInvoiceLineDto> Lines { get; init; }

}


