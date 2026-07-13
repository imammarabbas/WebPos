using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;

namespace WebPos.Controllers;

[ApiController]
[Route("api/sales")]
public sealed class SalesController : ControllerBase
{
    private readonly ISalesService _salesService;

    public SalesController(ISalesService salesService)
    {
        _salesService = salesService ?? throw new ArgumentNullException(nameof(salesService));
    }

    [HttpPost("complete")]
    [ProducesResponseType(typeof(CompleteSaleResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status426UpgradeRequired)]
    public async Task<ActionResult<CompleteSaleResult>> CompleteSale(
        [FromBody] CompleteSaleRequest request,
        CancellationToken cancellationToken)
    {
        CompleteSaleResult result = await _salesService.CompleteSaleAsync(request, cancellationToken);
        return Ok(result);
    }
}
