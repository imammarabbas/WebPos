using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize]
[TenantAuthorize]
public sealed class PurchasesController(IPurchaseService purchaseService) : ControllerBase
{
    private readonly IPurchaseService _purchaseService =
        purchaseService ?? throw new ArgumentNullException(nameof(purchaseService));

    [HttpPost]
    [ProducesResponseType(typeof(CreatePurchaseOrderResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreatePurchaseOrderResult>> Create(
        [FromBody] CreatePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        CreatePurchaseOrderResult result =
            await _purchaseService.CreatePurchaseOrderAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PurchaseOrderSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PurchaseOrderSummaryDto>>> List(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _purchaseService.ListAsync(limit, cancellationToken));
    }

    [HttpGet("{purchaseOrderId:guid}")]
    [ProducesResponseType(typeof(PurchaseOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Get(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        PurchaseOrderDetailDto? detail =
            await _purchaseService.GetAsync(purchaseOrderId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPut("{purchaseOrderId:guid}")]
    [ProducesResponseType(typeof(PurchaseOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderDetailDto>> UpdateOpen(
        Guid purchaseOrderId,
        [FromBody] UpdateOpenPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            PurchaseOrderDetailDto detail = await _purchaseService.UpdateOpenPurchaseAsync(
                purchaseOrderId,
                request,
                cancellationToken);
            return Ok(detail);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{purchaseOrderId:guid}/receive")]
    [ProducesResponseType(typeof(ReceiveStockResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReceiveStockResult>> Receive(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        ReceiveStockResult result =
            await _purchaseService.ReceiveStockAsync(purchaseOrderId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("quick-receive")]
    [ProducesResponseType(typeof(ReceiveStockResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ReceiveStockResult>> QuickReceive(
        [FromBody] QuickReceiveRequest request,
        CancellationToken cancellationToken)
    {
        ReceiveStockResult result =
            await _purchaseService.QuickReceiveAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Ad-hoc / direct supplier intake (alias of quick-receive).
    /// Creates the purchase order and receives stock in one step — no pre-existing PO.
    /// </summary>
    [HttpPost("direct-receive")]
    [ProducesResponseType(typeof(ReceiveStockResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<ReceiveStockResult>> DirectReceive(
        [FromBody] QuickReceiveRequest request,
        CancellationToken cancellationToken) =>
        QuickReceive(request, cancellationToken);
}
