using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/purchase-returns")]
[Authorize]
[TenantAuthorize]
public sealed class PurchaseReturnsController(
    IPurchaseReturnService purchaseReturnService) : ControllerBase
{
    private readonly IPurchaseReturnService _purchaseReturnService =
        purchaseReturnService
        ?? throw new ArgumentNullException(nameof(purchaseReturnService));

    [HttpPost]
    [ProducesResponseType(typeof(CreatePurchaseReturnResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreatePurchaseReturnResult>> Create(
        [FromBody] CreatePurchaseReturnRequest request,
        CancellationToken cancellationToken)
    {
        CreatePurchaseReturnResult result =
            await _purchaseReturnService.CreatePurchaseReturnAsync(
                request,
                cancellationToken);
        return Ok(result);
    }
}
