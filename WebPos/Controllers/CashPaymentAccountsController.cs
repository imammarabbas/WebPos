using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Filters;

namespace WebPos.Controllers;

/// <summary>
/// Cashier-accessible payment account listing for POS online/bank settlement.
/// Shares the <c>api/cash</c> route prefix with <see cref="CashController"/>.
/// </summary>
[ApiController]
[Route("api/cash")]
[Authorize]
[TenantAuthorize]
public sealed class CashPaymentAccountsController(ICashAccountService cashAccountService) : ControllerBase
{
    private readonly ICashAccountService _cashAccountService =
        cashAccountService ?? throw new ArgumentNullException(nameof(cashAccountService));

    /// <summary>
    /// Active non-till cash accounts suitable for Online / Bank POS payment selection.
    /// </summary>
    [HttpGet("payment-accounts")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentAccountDto>>> ListPaymentAccounts(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CashAccountDto> accounts = await _cashAccountService.ListAsync(
            includeInactive: false,
            cancellationToken);

        List<PaymentAccountDto> paymentAccounts = accounts
            .Where(a => a.Type != CashAccountType.Till)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new PaymentAccountDto
            {
                Id = a.Id,
                Name = a.Name,
                Type = CashAccountTypes.ToStored(a.Type),
                AccountCode = a.AccountCode,
                PaymentMethodKey = a.PaymentMethodKey
            })
            .ToList();

        return Ok(paymentAccounts);
    }
}
