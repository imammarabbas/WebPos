using System.Text;
using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/reporting")]
[Authorize(Roles = "Owner,Manager")]
[TenantAuthorize]
public sealed class ReportingController(IReportingService reportingService) : ControllerBase
{
    private readonly IReportingService _reportingService =
        reportingService ?? throw new ArgumentNullException(nameof(reportingService));

    [HttpGet("bi")]
    [ProducesResponseType(typeof(BiDashboardResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BiDashboardResult>> BiDashboard(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? terminalId,
        [FromQuery] string? paymentMethod,
        [FromQuery] bool compare = true,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            return BadRequest("Query 'to' must be on or after 'from'.");
        }

        BiDashboardResult result = await _reportingService.GetBiDashboardAsync(
            new BiDashboardQuery
            {
                From = from,
                To = to,
                CategoryId = categoryId,
                TerminalId = terminalId,
                PaymentMethod = paymentMethod,
                ComparePrevious = compare
            },
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("sales-by-cashier")]
    [ProducesResponseType(typeof(IReadOnlyList<CashierSalesSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<CashierSalesSummaryDto>>> SalesByCashier(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            return BadRequest("Query 'to' must be on or after 'from'.");
        }

        IReadOnlyList<CashierSalesSummaryDto> rows =
            await _reportingService.GetSalesByCashierAsync(from, to, cancellationToken);
        return Ok(rows);
    }

    [HttpGet("pnl")]
    [ProducesResponseType(typeof(ProfitAndLossSummary), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfitAndLossSummary>> ProfitAndLoss(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest("Query 'to' must be on or after 'from'.");
        }

        return Ok(await _reportingService.GetProfitAndLossAsync(from, to, cancellationToken));
    }

    [HttpGet("profitability/products")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductProfitSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductProfitSummary>>> ProductProfitability(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest("Query 'to' must be on or after 'from'.");
        }

        return Ok(await _reportingService.GetGrossProfitByProductAsync(from, to, cancellationToken));
    }

    [HttpGet("profitability/categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryProfitSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryProfitSummary>>> CategoryProfitability(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest("Query 'to' must be on or after 'from'.");
        }

        return Ok(await _reportingService.GetGrossProfitByCategoryAsync(from, to, cancellationToken));
    }

    [HttpGet("expenses")]
    [ProducesResponseType(typeof(IReadOnlyList<ExpenseCategorySummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExpenseCategorySummary>>> Expenses(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest("Query 'to' must be on or after 'from'.");
        }

        return Ok(await _reportingService.GetExpenseSummaryByCategoryAsync(from, to, cancellationToken));
    }

    [HttpGet("cash-flow")]
    [ProducesResponseType(typeof(IReadOnlyList<AccountCashFlowSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccountCashFlowSummary>>> CashFlow(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest("Query 'to' must be on or after 'from'.");
        }

        return Ok(await _reportingService.GetCashFlowByAccountAsync(from, to, cancellationToken));
    }

    [HttpGet("expenses/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportExpenses(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            return BadRequest("Query 'to' must be on or after 'from'.");
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            string json = await _reportingService.ExportExpenseReportJsonAsync(from, to, cancellationToken);
            return File(Encoding.UTF8.GetBytes(json), "application/json", "expenses.json");
        }

        string csv = await _reportingService.ExportExpenseReportCsvAsync(from, to, cancellationToken);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "expenses.csv");
    }
}
