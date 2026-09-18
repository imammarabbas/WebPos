using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/cash")]
[Authorize(Roles = "Owner,Manager")]
[TenantAuthorize]
public sealed class CashController(
    ICashTransferService cashTransferService) : ControllerBase
{
    private readonly ICashTransferService _cashTransferService =
        cashTransferService ?? throw new ArgumentNullException(nameof(cashTransferService));

    /// <summary>
    /// Moves cash between accounts. Till outflows are limited to min(GL, ExpectedCash).
    /// Mid-shift cash drops are allowed; shift close is never required.
    /// </summary>
    [HttpPost("transfer")]
    [ProducesResponseType(typeof(CashTransferResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashTransferResult>> Transfer(
        [FromBody] CashTransferApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AmountPaisa <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        try
        {
            CashTransferResult result = await _cashTransferService.TransferAsync(
                request.FromAccountId,
                request.ToAccountId,
                request.AmountPaisa,
                request.Notes,
                request.ShiftId,
                cancellationToken);
            return Ok(result);
        }
        catch (TillDiscrepancyException ex)
        {
            return BadRequest(new { error = ex.Message, code = "TILL_DISCREPANCY", gapPaisa = ex.GapPaisa });
        }
        catch (InsufficientCashBalanceException ex)
        {
            return BadRequest(new { error = ex.Message, code = "INSUFFICIENT_FUNDS" });
        }
    }

    /// <summary>
    /// Posts a cash shortage expense to write off till GL that exceeds physical drawer ExpectedCash.
    /// </summary>
    [HttpPost("till-shortage")]
    [ProducesResponseType(typeof(TillShortageResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TillShortageResult>> RecordTillShortage(
        [FromBody] TillShortageApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ShortagePaisa <= 0)
        {
            return BadRequest("Shortage must be greater than zero.");
        }

        Guid? userId = TryUserId();

        try
        {
            TillShortageResult result = await _cashTransferService.RecordTillShortageAsync(
                new RecordTillShortageRequest
                {
                    ShiftId = request.ShiftId,
                    TillCashAccountId = request.TillCashAccountId,
                    ShortagePaisa = request.ShortagePaisa,
                    Notes = request.Notes,
                    LoggedByUserId = userId
                },
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Recognizes physical cash in the till: raises ExpectedCash (Available).
    /// Debits till GL only for excess beyond the current ledger-over-drawer gap.
    /// </summary>
    [HttpPost("till-cash-in")]
    [ProducesResponseType(typeof(TillCashMovementResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TillCashMovementResult>> RecordTillCashIn(
        [FromBody] TillCashInApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AmountPaisa <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        try
        {
            TillCashMovementResult result = await _cashTransferService.RecordTillCashInAsync(
                new RecordTillCashInRequest
                {
                    ShiftId = request.ShiftId,
                    TillCashAccountId = request.TillCashAccountId,
                    AmountPaisa = request.AmountPaisa,
                    Reason = request.Reason,
                    Note = request.Note,
                    CreatedByUserId = TryUserId()
                },
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException
                                       or InsufficientCashBalanceException)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Physical cash leaving the till without a business document.
    /// </summary>
    [HttpPost("till-cash-out")]
    [ProducesResponseType(typeof(TillCashMovementResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TillCashMovementResult>> RecordTillCashOut(
        [FromBody] TillCashOutApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AmountPaisa <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        try
        {
            TillCashMovementResult result = await _cashTransferService.RecordTillCashOutAsync(
                new RecordTillCashOutRequest
                {
                    ShiftId = request.ShiftId,
                    TillCashAccountId = request.TillCashAccountId,
                    AmountPaisa = request.AmountPaisa,
                    Reason = request.Reason,
                    Note = request.Note,
                    CreatedByUserId = TryUserId()
                },
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException
                                       or InsufficientCashBalanceException)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("till-cash-movements/{movementId:guid}/reconcile")]
    [ProducesResponseType(typeof(TillCashMovementResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TillCashMovementResult>> ReconcileTillCashMovement(
        Guid movementId,
        [FromBody] ReconcileTillCashMovementApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            TillCashMovementResult result = await _cashTransferService.ReconcileTillCashMovementAsync(
                new ReconcileTillCashMovementRequest
                {
                    MovementId = movementId,
                    LinkedReferenceType = request.LinkedReferenceType,
                    LinkedReferenceId = request.LinkedReferenceId,
                    ReconciledByUserId = TryUserId(),
                    Notes = request.Notes
                },
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("till-cash-movements/{movementId:guid}/reverse")]
    [ProducesResponseType(typeof(TillCashMovementResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TillCashMovementResult>> ReverseTillCashMovement(
        Guid movementId,
        [FromBody] ReverseTillCashMovementApiRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            TillCashMovementResult result = await _cashTransferService.ReverseTillCashMovementAsync(
                movementId,
                TryUserId(),
                request?.Notes,
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("till-cash-movements")]
    [ProducesResponseType(typeof(IReadOnlyList<TillCashMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TillCashMovementDto>>> ListTillCashMovements(
        [FromQuery] Guid? shiftId,
        [FromQuery] Guid? tillCashAccountId,
        [FromQuery] bool unreconciledOnly = false,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TillCashMovementDto> rows = await _cashTransferService.ListTillCashMovementsAsync(
            shiftId,
            tillCashAccountId,
            unreconciledOnly,
            limit,
            cancellationToken);
        return Ok(rows);
    }

    [HttpPost("owner-investment")]
    [ProducesResponseType(typeof(CapitalFundingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CapitalFundingResult>> RecordOwnerInvestment(
        [FromBody] CapitalFundingApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AmountPaisa <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        try
        {
            CapitalFundingResult result = await _cashTransferService.RecordOwnerInvestmentAsync(
                ToFundingRequest(request),
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException
                                       or InsufficientCashBalanceException)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("loan-received")]
    [ProducesResponseType(typeof(CapitalFundingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CapitalFundingResult>> RecordLoanReceived(
        [FromBody] CapitalFundingApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AmountPaisa <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        try
        {
            CapitalFundingResult result = await _cashTransferService.RecordLoanReceivedAsync(
                ToFundingRequest(request),
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException
                                       or InsufficientCashBalanceException)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("loan-repayment")]
    [ProducesResponseType(typeof(CapitalFundingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CapitalFundingResult>> RecordLoanRepayment(
        [FromBody] CapitalFundingApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AmountPaisa <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        try
        {
            CapitalFundingResult result = await _cashTransferService.RecordLoanRepaymentAsync(
                ToFundingRequest(request),
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException
                                       or InsufficientCashBalanceException)
        {
            return BadRequest(ex.Message);
        }
    }

    private CapitalFundingRequest ToFundingRequest(CapitalFundingApiRequest request) =>
        new()
        {
            CashAccountId = request.CashAccountId,
            AmountPaisa = request.AmountPaisa,
            Note = request.Note,
            ShiftId = request.ShiftId,
            CreatedByUserId = TryUserId()
        };

    private Guid? TryUserId()
    {
        string? raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out Guid parsed) ? parsed : null;
    }
}

public sealed class CashTransferApiRequest
{
    public Guid FromAccountId { get; init; }

    public Guid ToAccountId { get; init; }

    public long AmountPaisa { get; init; }

    public string? Notes { get; init; }

    public Guid? ShiftId { get; init; }
}

public sealed class TillShortageApiRequest
{
    public Guid ShiftId { get; init; }

    public Guid TillCashAccountId { get; init; }

    public long ShortagePaisa { get; init; }

    public string? Notes { get; init; }
}

public sealed class TillCashInApiRequest
{
    public Guid ShiftId { get; init; }

    public Guid TillCashAccountId { get; init; }

    public long AmountPaisa { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string? Note { get; init; }
}

public sealed class TillCashOutApiRequest
{
    public Guid ShiftId { get; init; }

    public Guid TillCashAccountId { get; init; }

    public long AmountPaisa { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string? Note { get; init; }
}

public sealed class ReconcileTillCashMovementApiRequest
{
    public string LinkedReferenceType { get; init; } = string.Empty;

    public Guid LinkedReferenceId { get; init; }

    public string? Notes { get; init; }
}

public sealed class ReverseTillCashMovementApiRequest
{
    public string? Notes { get; init; }
}

public sealed class CapitalFundingApiRequest
{
    public Guid CashAccountId { get; init; }

    public long AmountPaisa { get; init; }

    public string? Note { get; init; }

    public Guid? ShiftId { get; init; }
}
