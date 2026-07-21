using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Services;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/shift")]
[Authorize]
[TenantAuthorize]
public sealed class ShiftController(IShiftService shiftService) : ControllerBase
{
    private readonly IShiftService _shiftService =
        shiftService ?? throw new ArgumentNullException(nameof(shiftService));

    [HttpPost("start")]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftDto>> Start(
        [FromBody] StartShiftRequest request,
        CancellationToken cancellationToken)
    {
        ShiftDto shift = await _shiftService.StartShiftAsync(request, cancellationToken);
        return Ok(shift);
    }

    [HttpPost("close")]
    [ProducesResponseType(typeof(CashVarianceReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashVarianceReport>> Close(
        [FromBody] CloseShiftRequest request,
        CancellationToken cancellationToken)
    {
        CashVarianceReport report =
            await _shiftService.CloseShiftAsync(request, cancellationToken);
        return Ok(report);
    }

    [HttpGet("{shiftId:guid}/reconciliation")]
    [ProducesResponseType(typeof(CashVarianceReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CashVarianceReport>> GetReconciliation(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        CashVarianceReport report =
            await _shiftService.GetCashReconciliationAsync(shiftId, cancellationToken);
        return Ok(report);
    }
}
