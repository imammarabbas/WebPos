using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;

namespace WebPos.Core.Validation;

public sealed class CreatePartyRequestValidator : AbstractValidator<CreatePartyRequest>
{
    public CreatePartyRequestValidator(
        WebPosDbContext dbContext,
        ITenantService tenantService)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(tenantService);

        RuleFor(request => request.Role)
            .NotEmpty()
            .Must(PartyTypes.IsKnown)
            .WithMessage("Role must be CUSTOMER or SUPPLIER.");

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100)
            .MustAsync(async (request, name, cancellationToken) =>
            {
                if (!tenantService.IsResolved || tenantService.TenantId == Guid.Empty)
                {
                    return false;
                }

                string normalizedName = name.Trim();
                string normalizedRole = PartyTypes.Normalize(request.Role);
                return !await dbContext.Parties.AnyAsync(
                    party =>
                        party.TenantId == tenantService.TenantId
                        && party.PartyType == normalizedRole
                        && party.Name == normalizedName,
                    cancellationToken);
            })
            .WithMessage(
                "A party with this name and role already exists for the current tenant.");

        RuleFor(request => request.PhoneNumber)
            .NotEmpty()
            .MaximumLength(20)
            .MustAsync(async (phone, cancellationToken) =>
            {
                if (!tenantService.IsResolved || tenantService.TenantId == Guid.Empty)
                {
                    return false;
                }

                string normalizedPhone = phone.Trim();
                return !await dbContext.Parties.AnyAsync(
                    party =>
                        party.TenantId == tenantService.TenantId
                        && party.PhoneNumber == normalizedPhone,
                    cancellationToken);
            })
            .WithMessage(
                "A party with this phone number already exists for the current tenant.");

        RuleFor(request => request.CreditLimitPaisa)
            .GreaterThanOrEqualTo(0);
    }
}
