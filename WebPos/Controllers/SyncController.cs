using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/sync")]
[Authorize]
[TenantAuthorize]
[ServiceFilter(typeof(SyncSchemaVersionFilter))]
public sealed class SyncController(ISyncService syncService) : ControllerBase
{
    private readonly ISyncService _syncService =
        syncService ?? throw new ArgumentNullException(nameof(syncService));

    [HttpGet("bootstrap")]
    [ProducesResponseType(typeof(SyncBootstrapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status406NotAcceptable)]
    public async Task<ActionResult<SyncBootstrapResponse>> Bootstrap(
        CancellationToken cancellationToken)
    {
        SyncBootstrapResponse response =
            await _syncService.BootstrapAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("delta")]
    [ProducesResponseType(typeof(SyncDeltaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status406NotAcceptable)]
    public async Task<ActionResult<SyncDeltaResponse>> Delta(
        [FromQuery] DateTimeOffset lastSync,
        CancellationToken cancellationToken)
    {
        SyncDeltaResponse response =
            await _syncService.GetDeltaAsync(lastSync, cancellationToken);
        return Ok(response);
    }
}
