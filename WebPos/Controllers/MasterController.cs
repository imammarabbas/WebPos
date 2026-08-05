using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/master")]
[Authorize]
[TenantAuthorize]
public sealed class MasterController(IMasterOverviewService overviewService) : ControllerBase
{
    private readonly IMasterOverviewService _overviewService =
        overviewService ?? throw new ArgumentNullException(nameof(overviewService));

    [HttpGet("overview")]
    [ProducesResponseType(typeof(MasterOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MasterOverviewDto>> Overview(CancellationToken cancellationToken)
    {
        return Ok(await _overviewService.GetOverviewAsync(cancellationToken));
    }
}

