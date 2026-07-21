using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
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
    ISalesReturnService salesReturnService) : ControllerBase
{
    private readonly ISalesService _salesService =
        salesService ?? throw new ArgumentNullException(nameof(salesService));
    private readonly ISalesReturnService _salesReturnService =
        salesReturnService
        ?? throw new ArgumentNullException(nameof(salesReturnService));

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
}
