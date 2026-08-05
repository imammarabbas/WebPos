using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/store")]
[Authorize]
[TenantAuthorize]
public sealed class StoreController(IStoreStatusService storeStatusService) : ControllerBase
{
    private readonly IStoreStatusService _storeStatusService =
        storeStatusService ?? throw new ArgumentNullException(nameof(storeStatusService));

    [HttpGet("status")]
    [ProducesResponseType(typeof(StoreStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StoreStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await _storeStatusService.GetStatusAsync(cancellationToken));
    }
}
