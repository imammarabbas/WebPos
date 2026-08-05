using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Abstractions;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/parties")]
[Authorize]
[TenantAuthorize]
public sealed class PartiesController(
    IPartyService partyService,
    IPartyLedgerService partyLedgerService) : ControllerBase
{
    private readonly IPartyService _partyService =
        partyService ?? throw new ArgumentNullException(nameof(partyService));
    private readonly IPartyLedgerService _partyLedgerService =
        partyLedgerService ?? throw new ArgumentNullException(nameof(partyLedgerService));

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

    [HttpGet("{partyId:guid}/ledger")]
    [ProducesResponseType(typeof(IReadOnlyList<PartyLedgerEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PartyLedgerEntryDto>>> ListLedger(
        Guid partyId,
        [FromQuery] int limit = 50,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PartyLedgerEntryDto> entries =
            await _partyLedgerService.ListByPartyAsync(
                partyId,
                limit,
                from,
                to,
                cancellationToken);
        return Ok(entries);
    }

    [HttpGet("{partyId:guid}/slips/{year:int}/{month:int}")]
    [ProducesResponseType(typeof(PartyLedgerSlipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyLedgerSlipDto>> GetMonthlySlip(
        Guid partyId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        PartyLedgerSlipDto slip =
            await _partyLedgerService.GetSlipAsync(partyId, year, month, cancellationToken);
        return Ok(slip);
    }

    [HttpGet("{partyId:guid}/open-slips")]
    [ProducesResponseType(typeof(IReadOnlyList<OpenPartySlipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OpenPartySlipDto>>> ListOpenSlips(
        Guid partyId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<OpenPartySlipDto> slips =
            await _partyLedgerService.ListOpenSlipsAsync(partyId, cancellationToken);
        return Ok(slips);
    }

    [HttpPut("{partyId:guid}")]
    [ProducesResponseType(typeof(PartyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyDto>> Update(
        Guid partyId,
        [FromBody] UpdatePartyRequest request,
        CancellationToken cancellationToken)
    {
        PartyDto party =
            await _partyService.UpdatePartyAsync(partyId, request, cancellationToken);
        return Ok(party);
    }
}
