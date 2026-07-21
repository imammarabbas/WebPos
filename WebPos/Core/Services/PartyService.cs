using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class PartyService : IPartyService
{
    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly IPartyLedgerService _partyLedgerService;
    private readonly ITenantService _tenantService;
    private readonly IValidator<CreatePartyRequest> _createValidator;

    public PartyService(
        WebPosDbContext context,
        ITransactionService transactionService,
        IPartyLedgerService partyLedgerService,
        ITenantService tenantService,
        IValidator<CreatePartyRequest> createValidator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _transactionService = transactionService
            ?? throw new ArgumentNullException(nameof(transactionService));
        _partyLedgerService = partyLedgerService
            ?? throw new ArgumentNullException(nameof(partyLedgerService));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
        _createValidator = createValidator
            ?? throw new ArgumentNullException(nameof(createValidator));
    }

    public Task<PartyDto> CreatePartyAsync(
        CreatePartyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            ValidationResult validation =
                await _createValidator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            string role = PartyTypes.Normalize(request.Role);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid partyId = Guid.NewGuid();

            Party party = new()
            {
                Id = partyId,
                TenantId = _tenantService.TenantId,
                PartyType = role,
                Name = request.Name.Trim(),
                PhoneNumber = request.PhoneNumber.Trim(),
                Address = request.Address?.Trim() ?? string.Empty,
                CreditLimitPaisa = request.CreditLimitPaisa,
                CurrentBalancePaisa = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            _context.Parties.Add(party);

            // Persist party before ledger init so FK lookups succeed in-transaction.
            await _context.SaveChangesAsync(ct);

            if (role == PartyTypes.Supplier)
            {
                await _partyLedgerService.InitializeSupplierLedgerAsync(partyId, ct);
            }

            return ToDto(party);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<PartyDto>> GetPartiesAsync(
        string? role,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        IQueryable<Party> query = _context.Parties.AsNoTracking()
            .Where(party => party.TenantId == _tenantService.TenantId);

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!PartyTypes.IsKnown(role))
            {
                throw new InvalidOperationException(
                    "Role must be CUSTOMER or SUPPLIER.");
            }

            string normalizedRole = PartyTypes.Normalize(role);
            query = query.Where(party => party.PartyType == normalizedRole);
        }

        List<Party> parties = await query
            .OrderBy(party => party.Name)
            .ToListAsync(cancellationToken);

        return parties.Select(ToDto).ToList();
    }

    public async Task<PartyDto> GetPartyAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        Party party = await _context.Parties.AsNoTracking()
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.Id == partyId
                    && candidate.TenantId == _tenantService.TenantId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Party was not found for the current tenant.");

        return ToDto(party);
    }

    public async Task<PartyDto> GetSupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        Party party = await _context.Parties.AsNoTracking()
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.Id == supplierId
                    && candidate.TenantId == _tenantService.TenantId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Supplier was not found for the current tenant.");

        if (!string.Equals(
                party.PartyType,
                PartyTypes.Supplier,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected party is not a SUPPLIER.");
        }

        return ToDto(party);
    }

    public Task<IReadOnlyList<PartyDto>> GetSuppliersAsync(
        CancellationToken cancellationToken = default) =>
        GetPartiesAsync(PartyTypes.Supplier, cancellationToken);

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is not resolved for party operations.");
        }
    }

    private static PartyDto ToDto(Party party) =>
        new()
        {
            Id = party.Id,
            Role = party.PartyType,
            Name = party.Name,
            PhoneNumber = party.PhoneNumber,
            Address = party.Address,
            CreditLimitPaisa = party.CreditLimitPaisa,
            CurrentBalancePaisa = party.CurrentBalancePaisa
        };
}
