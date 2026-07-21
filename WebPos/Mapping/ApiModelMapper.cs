using Common.Models;
using CoreSaleLine = WebPos.Core.Abstractions.SaleLineRequest;
using CoreCompleteSaleRequest = WebPos.Core.Abstractions.CompleteSaleRequest;
using CoreCompleteSaleResult = WebPos.Core.Abstractions.CompleteSaleResult;
using CoreReturnLine = WebPos.Core.Abstractions.ReturnLineRequest;
using CoreReturnItemsRequest = WebPos.Core.Abstractions.ReturnItemsRequest;
using CoreReturnItemsResult = WebPos.Core.Abstractions.ReturnItemsResult;

namespace WebPos.Mapping;

internal static class ApiModelMapper
{
    public static CoreCompleteSaleRequest ToCore(CompleteSaleRequest request) =>
        new()
        {
            InvoiceNo = request.InvoiceNo,
            ShiftId = request.ShiftId,
            TerminalId = request.TerminalId,
            CashierId = request.CashierId,
            CustomerId = request.CustomerId,
            PaymentMethod = request.PaymentMethod,
            DiscountAmountPaisa = request.DiscountAmountPaisa,
            DiscountReason = request.DiscountReason,
            Lines = request.Lines.Select(l => new CoreSaleLine
            {
                ProductId = l.ProductId,
                BatchId = l.BatchId,
                BatchNumber = l.BatchNumber,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                UnitPricePaisa = l.UnitPricePaisa,
                DiscountAppliedPaisa = l.DiscountAppliedPaisa
            }).ToList()
        };

    public static CompleteSaleResult ToCommon(CoreCompleteSaleResult result) =>
        new()
        {
            InvoiceNo = result.InvoiceNo,
            ReceiptNumber = result.ReceiptNumber,
            TotalAmountPaisa = result.TotalAmountPaisa,
            GrossAmountPaisa = result.GrossAmountPaisa,
            DiscountAmountPaisa = result.DiscountAmountPaisa,
            TransactionGroupId = result.TransactionGroupId
        };

    public static CoreReturnItemsRequest ToCore(ReturnItemsRequest request) =>
        new()
        {
            OriginalInvoiceNo = request.OriginalInvoiceNo,
            CashierId = request.CashierId,
            Items = request.Items.Select(i => new CoreReturnLine
            {
                ProductId = i.ProductId,
                BatchId = i.BatchId,
                Quantity = i.Quantity,
                ReturnCondition = i.ReturnCondition
            }).ToList()
        };

    public static ReturnItemsResult ToCommon(CoreReturnItemsResult result) =>
        new()
        {
            SalesReturnId = result.SalesReturnId,
            OriginalInvoiceNo = result.OriginalInvoiceNo,
            TotalRefundPaisa = result.TotalRefundPaisa,
            TransactionGroupId = result.TransactionGroupId
        };
}
