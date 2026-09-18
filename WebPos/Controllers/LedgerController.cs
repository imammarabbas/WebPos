using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/ledger")]
[Authorize]
[TenantAuthorize]
public sealed class LedgerController(ICashAccountService cashAccountService) : ControllerBase
{
    private readonly ICashAccountService _cashAccountService =
        cashAccountService ?? throw new ArgumentNullException(nameof(cashAccountService));

    [HttpGet("entries")]
    [ProducesResponseType(typeof(CashAccountLedgerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashAccountLedgerDto>> GetEntries(
        [FromQuery(Name = "account_code")] string? accountCodeSnake,
        [FromQuery] string? accountCode,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        string? code = !string.IsNullOrWhiteSpace(accountCodeSnake)
            ? accountCodeSnake
            : accountCode;

        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest("Query 'account_code' is required.");
        }

        if (to < from)
        {
            return BadRequest("Query 'to' must be on or after 'from'.");
        }

        CashAccountLedgerDto ledger = await _cashAccountService.GetLedgerByAccountCodeAsync(
            code,
            from,
            to,
            cancellationToken);
        return Ok(ledger);
    }
}
