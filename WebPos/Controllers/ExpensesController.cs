using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize(Roles = "Owner,Manager")]
[TenantAuthorize]
public sealed class ExpensesController(IExpenseService expenseService) : ControllerBase
{
    private readonly IExpenseService _expenseService =
        expenseService ?? throw new ArgumentNullException(nameof(expenseService));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExpenseListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExpenseListItemDto>>> List(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest("Query 'to' must be on or after 'from'.");
        }

        IReadOnlyList<ExpenseListItemDto> rows =
            await _expenseService.ListExpensesAsync(from, to, cancellationToken);
        return Ok(rows);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RecordExpenseResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecordExpenseResult>> Create(
        [FromBody] CreateExpenseApiRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveUserId(out Guid userId))
        {
            return Unauthorized();
        }

        RecordExpenseResult result = await _expenseService.RecordShiftExpenseAsync(
            new RecordExpenseRequest
            {
                ShiftId = request.ShiftId,
                LoggedByUserId = userId,
                VoucherNo = request.VoucherNo,
                Description = request.Description,
                ExpenseCategory = request.ExpenseCategory,
                ReceiptReference = request.ReceiptReference ?? string.Empty,
                PaymentMethod = request.PaymentMethod,
                AmountPaisa = request.AmountPaisa,
                IsRecurring = request.IsRecurring
            },
            cancellationToken);

        return Ok(result);
    }

    private bool TryResolveUserId(out Guid userId)
    {
        string? raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}

public sealed class CreateExpenseApiRequest
{
    public Guid? ShiftId { get; init; }

    public string? VoucherNo { get; init; }

    public required string Description { get; init; }

    public required string ExpenseCategory { get; init; }

    public string? ReceiptReference { get; init; }

    public required string PaymentMethod { get; init; }

    public long AmountPaisa { get; init; }

    public bool IsRecurring { get; init; }
}
