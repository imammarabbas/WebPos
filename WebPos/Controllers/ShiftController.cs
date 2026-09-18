using System.Security.Claims;
using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Client.Sdk.Security;
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

    [HttpGet("suggested-opening")]
    [ProducesResponseType(typeof(SuggestedOpeningCashDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SuggestedOpeningCashDto>> GetSuggestedOpening(
        [FromQuery] Guid? terminalId,
        CancellationToken cancellationToken)
    {
        Guid resolvedTerminalId = terminalId is Guid id && id != Guid.Empty
            ? id
            : ResolveEnrollmentTerminalId();
        if (resolvedTerminalId == Guid.Empty)
        {
            return BadRequest(new { error = "Terminal id is required." });
        }

        SuggestedOpeningCashDto suggestion = await _shiftService.GetSuggestedOpeningCashAsync(
            resolvedTerminalId,
            cancellationToken);
        return Ok(suggestion);
    }

    [HttpGet("open")]
    [ProducesResponseType(typeof(OpenShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<OpenShiftDto>> GetOpen(
        [FromQuery] Guid? terminalId,
        CancellationToken cancellationToken)
    {
        Guid resolvedTerminalId = terminalId is Guid id && id != Guid.Empty
            ? id
            : ResolveEnrollmentTerminalId();
        if (resolvedTerminalId == Guid.Empty)
        {
            return BadRequest(new { error = "Terminal id is required." });
        }

        OpenShiftDto? open = await _shiftService.GetOpenShiftForTerminalAsync(
            resolvedTerminalId,
            cancellationToken);
        return open is null ? NoContent() : Ok(open);
    }

    [HttpGet("open-all")]
    [Authorize(Roles = "Owner,Manager,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<OpenShiftDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OpenShiftDto>>> ListOpen(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<OpenShiftDto> open =
            await _shiftService.ListOpenShiftsAsync(cancellationToken);
        return Ok(open);
    }

    [HttpPost("{shiftId:guid}/force-close")]
    [Authorize(Roles = "Owner,Manager,Admin")]
    [ProducesResponseType(typeof(CashVarianceReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashVarianceReport>> ForceClose(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        CashVarianceReport report =
            await _shiftService.ForceCloseShiftAsync(shiftId, cancellationToken);
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

    private Guid ResolveEnrollmentTerminalId()
    {
        string? raw = User.FindFirstValue(EnrollmentCertificateValidator.TerminalClaimType);
        return Guid.TryParse(raw, out Guid terminalId) ? terminalId : Guid.Empty;
    }
}
