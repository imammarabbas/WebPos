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
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;
    private readonly IAmbientDbContextAccessor _ambient;
    private readonly ITransactionService _transactionService;
    private readonly IPartyLedgerService _partyLedgerService;
    private readonly ITenantService _tenantService;
    private readonly IValidator<CreatePartyRequest> _createValidator;

    public PartyService(
        IDbContextFactory<WebPosDbContext> dbFactory,
        IAmbientDbContextAccessor ambient,
        ITransactionService transactionService,
        IPartyLedgerService partyLedgerService,
        ITenantService tenantService,
        IValidator<CreatePartyRequest> createValidator)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _ambient = ambient ?? throw new ArgumentNullException(nameof(ambient));
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

            WebPosDbContext context = _ambient.Required;
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
                Email = NormalizeEmail(request.Email),
                Address = request.Address?.Trim() ?? string.Empty,
                CreditLimitPaisa = request.CreditLimitPaisa,
                CurrentBalancePaisa = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Parties.Add(party);

            await context.SaveChangesAsync(ct);

            if (role == PartyTypes.Supplier)
            {
                await _partyLedgerService.InitializeSupplierLedgerAsync(partyId, ct);
            }

            return ToDto(party);
        }, cancellationToken);
    }

    public Task<PartyDto> UpdatePartyAsync(
        Guid partyId,
        UpdatePartyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            WebPosDbContext context = _ambient.Required;
            Party party = await context.Parties
                .FirstOrDefaultAsync(
                    candidate =>
                        candidate.Id == partyId
                        && candidate.TenantId == _tenantService.TenantId,
                    ct)
                ?? throw new KeyNotFoundException(
                    "Party was not found for the current tenant.");

            party.Name = request.Name.Trim();
            party.PhoneNumber = request.PhoneNumber.Trim();
            party.Email = NormalizeEmail(request.Email);
            party.Address = request.Address?.Trim() ?? string.Empty;
            party.CreditLimitPaisa = request.CreditLimitPaisa;
            party.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(ct);
            return ToDto(party);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<PartyDto>> GetPartiesAsync(
        string? role,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            IQueryable<Party> query = context.Parties.AsNoTracking()
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
                .ToListAsync(ct);

            return (IReadOnlyList<PartyDto>)parties.Select(ToDto).ToList();
        }, cancellationToken);
    }

    public Task<PartyDto> GetPartyAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            Party party = await context.Parties.AsNoTracking()
                .FirstOrDefaultAsync(
                    candidate =>
                        candidate.Id == partyId
                        && candidate.TenantId == _tenantService.TenantId,
                    ct)
                ?? throw new KeyNotFoundException(
                    "Party was not found for the current tenant.");

            return ToDto(party);
        }, cancellationToken);
    }

    public Task<PartyDto> GetSupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            Party party = await context.Parties.AsNoTracking()
                .FirstOrDefaultAsync(
                    candidate =>
                        candidate.Id == supplierId
                        && candidate.TenantId == _tenantService.TenantId,
                    ct)
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
        }, cancellationToken);
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
            Email = party.Email,
            Address = party.Address,
            CreditLimitPaisa = party.CreditLimitPaisa,
            CurrentBalancePaisa = party.CurrentBalancePaisa
        };

    private static string? NormalizeEmail(string? email)
    {
        string? trimmed = email?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
