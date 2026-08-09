using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/store")]
[Authorize]
[TenantAuthorize]
public sealed class StoreController(
    IStoreStatusService storeStatusService,
    IStoreBrandingService storeBrandingService) : ControllerBase
{
    private readonly IStoreStatusService _storeStatusService =
        storeStatusService ?? throw new ArgumentNullException(nameof(storeStatusService));
    private readonly IStoreBrandingService _storeBrandingService =
        storeBrandingService ?? throw new ArgumentNullException(nameof(storeBrandingService));

    [HttpGet("status")]
    [ProducesResponseType(typeof(StoreStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StoreStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await _storeStatusService.GetStatusAsync(cancellationToken));
    }

    [HttpGet("branding")]
    [ProducesResponseType(typeof(StoreBrandingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StoreBrandingDto>> GetBranding(CancellationToken cancellationToken)
    {
        return Ok(await _storeBrandingService.GetAsync(cancellationToken));
    }

    [HttpPut("branding")]
    [Authorize(Roles = "Owner,Manager")]
    [ProducesResponseType(typeof(StoreBrandingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StoreBrandingDto>> UpdateBranding(
        [FromBody] UpdateStoreBrandingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _storeBrandingService.UpdateAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}
