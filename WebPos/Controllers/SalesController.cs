using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Core.Services;
using WebPos.Filters;
using WebPos.Mapping;
using ApiCompleteSaleRequest = Common.Models.CompleteSaleRequest;
using ApiCompleteSaleResult = Common.Models.CompleteSaleResult;
using ApiReturnItemsRequest = Common.Models.ReturnItemsRequest;
using ApiReturnItemsResult = Common.Models.ReturnItemsResult;

namespace WebPos.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize]
[TenantAuthorize]
public sealed class SalesController(
    ISalesService salesService,
    ISalesReturnService salesReturnService,
    ISalesInvoiceQueryService invoiceQueryService) : ControllerBase
{
    private readonly ISalesService _salesService =
        salesService ?? throw new ArgumentNullException(nameof(salesService));
    private readonly ISalesReturnService _salesReturnService =
        salesReturnService
        ?? throw new ArgumentNullException(nameof(salesReturnService));
    private readonly ISalesInvoiceQueryService _invoiceQueryService =
        invoiceQueryService ?? throw new ArgumentNullException(nameof(invoiceQueryService));

    [HttpGet("invoices")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesInvoiceSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SalesInvoiceSummaryDto>>> ListInvoices(
        [FromQuery] Guid? shiftId = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SalesInvoiceSummary> invoices = shiftId is Guid id && id != Guid.Empty
            ? await _invoiceQueryService.ListByShiftAsync(id, limit, cancellationToken)
            : await _invoiceQueryService.ListRecentAsync(limit, cancellationToken);

        return Ok(invoices.Select(ToSummaryDto).ToList());
    }

    [HttpGet("invoices/search")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesInvoiceSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SalesInvoiceSummaryDto>>> SearchInvoices(
        [FromQuery] string? invoice,
        [FromQuery] string? customer,
        [FromQuery] string? product,
        [FromQuery] int limit = 40,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invoice)
            && string.IsNullOrWhiteSpace(customer)
            && string.IsNullOrWhiteSpace(product))
        {
            return BadRequest("Provide at least one of invoice, customer, or product.");
        }

        IReadOnlyList<SalesInvoiceSummary> invoices =
            await _invoiceQueryService.SearchAsync(
                new SalesInvoiceSearchQuery
                {
                    InvoiceQuery = invoice,
                    CustomerQuery = customer,
                    ProductQuery = product,
                    Limit = limit
                },
                cancellationToken);

        return Ok(invoices.Select(ToSummaryDto).ToList());
    }

    [HttpGet("invoices/{invoiceNo}")]
    [ProducesResponseType(typeof(SalesInvoiceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalesInvoiceDetailDto>> GetInvoice(
        string invoiceNo,
        CancellationToken cancellationToken)
    {
        SalesInvoiceDetail? detail =
            await _invoiceQueryService.GetByInvoiceNoAsync(invoiceNo, cancellationToken);

        if (detail is null)
        {
            return NotFound();
        }

        return Ok(ToDetailDto(detail));
    }

    [HttpPost("complete")]
    [ProducesResponseType(typeof(ApiCompleteSaleResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status426UpgradeRequired)]
    public async Task<ActionResult<ApiCompleteSaleResult>> CompleteSale(
        [FromBody] ApiCompleteSaleRequest request,
        CancellationToken cancellationToken)
    {
        WebPos.Core.Abstractions.CompleteSaleResult result =
            await _salesService.CompleteSaleAsync(
                ApiModelMapper.ToCore(request),
                cancellationToken);
        return Ok(ApiModelMapper.ToCommon(result));
    }

    [HttpPost("return")]
    [ProducesResponseType(typeof(ApiReturnItemsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status426UpgradeRequired)]
    public async Task<ActionResult<ApiReturnItemsResult>> ReturnItems(
        [FromBody] ApiReturnItemsRequest request,
        CancellationToken cancellationToken)
    {
        WebPos.Core.Abstractions.ReturnItemsResult result =
            await _salesReturnService.ReturnItemsAsync(
                ApiModelMapper.ToCore(request),
                cancellationToken);
        return Ok(ApiModelMapper.ToCommon(result));
    }

    private static SalesInvoiceSummaryDto ToSummaryDto(SalesInvoiceSummary invoice) =>
        new()
        {
            InvoiceNo = invoice.InvoiceNo,
            ReceiptNumber = invoice.ReceiptNumber,
            CreatedAt = invoice.CreatedAt,
            TerminalId = invoice.TerminalId,
            TotalAmountPaisa = invoice.TotalAmountPaisa,
            GrossAmountPaisa = invoice.GrossAmountPaisa,
            DiscountAmountPaisa = invoice.DiscountAmountPaisa,
            TaxAmountPaisa = invoice.TaxAmountPaisa,
            PaymentMethod = invoice.PaymentMethod,
            CustomerId = invoice.CustomerId,
            CustomerName = invoice.CustomerName,
            CustomerPhone = invoice.CustomerPhone,
            ItemCount = invoice.ItemCount,
            ReturnedAmountPaisa = invoice.ReturnedAmountPaisa,
            ProductLabels = invoice.ProductLabels
        };

    private static SalesInvoiceDetailDto ToDetailDto(SalesInvoiceDetail detail) =>
        new()
        {
            InvoiceNo = detail.InvoiceNo,
            ReceiptNumber = detail.ReceiptNumber,
            CreatedAt = detail.CreatedAt,
            ShiftId = detail.ShiftId,
            TerminalId = detail.TerminalId,
            CashierId = detail.CashierId,
            TotalAmountPaisa = detail.TotalAmountPaisa,
            GrossAmountPaisa = detail.GrossAmountPaisa,
            DiscountAmountPaisa = detail.DiscountAmountPaisa,
            TaxAmountPaisa = detail.TaxAmountPaisa,
            PaymentMethod = detail.PaymentMethod,
            CustomerId = detail.CustomerId,
            CustomerName = detail.CustomerName,
            CustomerPhone = detail.CustomerPhone,
            Lines = detail.Lines.Select(line => new SalesInvoiceLineDto
            {
                ProductId = line.ProductId,
                BatchId = line.BatchId,
                BatchNumber = line.BatchNumber,
                ProductName = line.ProductName,
                IsLoose = line.IsLoose,
                QuantitySold = line.QuantitySold,
                QuantityReturned = line.QuantityReturned,
                ReturnableQuantity = line.ReturnableQuantity,
                UnitPricePaisa = line.UnitPricePaisa
            }).ToList()
        };
}
