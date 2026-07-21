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
}
