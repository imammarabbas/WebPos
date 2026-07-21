using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/parties")]
[Authorize]
[TenantAuthorize]
public sealed class PartiesController(IPartyService partyService) : ControllerBase
{
    private readonly IPartyService _partyService =
        partyService ?? throw new ArgumentNullException(nameof(partyService));

    [HttpPost]
    [ProducesResponseType(typeof(PartyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PartyDto>> Create(
        [FromBody] CreatePartyRequest request,
        CancellationToken cancellationToken)
    {
        PartyDto party =
            await _partyService.CreatePartyAsync(request, cancellationToken);
        return Ok(party);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PartyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PartyDto>>> List(
        [FromQuery] string? role,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PartyDto> parties =
            await _partyService.GetPartiesAsync(role, cancellationToken);
        return Ok(parties);
    }

    [HttpGet("suppliers")]
    [ProducesResponseType(typeof(IReadOnlyList<PartyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PartyDto>>> ListSuppliers(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PartyDto> suppliers =
            await _partyService.GetSuppliersAsync(cancellationToken);
        return Ok(suppliers);
    }

    [HttpGet("{partyId:guid}")]
    [ProducesResponseType(typeof(PartyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyDto>> Get(
        Guid partyId,
        CancellationToken cancellationToken)
    {
        PartyDto party =
            await _partyService.GetPartyAsync(partyId, cancellationToken);
        return Ok(party);
    }
}
