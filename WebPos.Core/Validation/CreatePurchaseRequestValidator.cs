using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;

namespace WebPos.Core.Validation;

public sealed class CreatePurchaseRequestValidator : AbstractValidator<CreatePurchaseRequest>
{
    public CreatePurchaseRequestValidator(
        WebPosDbContext dbContext,
        ITenantService tenantService)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(tenantService);

        RuleFor(request => request.SupplierId)
            .NotEmpty()
            .MustAsync(async (supplierId, cancellationToken) =>
            {
                if (!tenantService.IsResolved || tenantService.TenantId == Guid.Empty)
                {
                    return false;
                }

                return await dbContext.Parties.AnyAsync(
                    party =>
                        party.Id == supplierId
                        && party.TenantId == tenantService.TenantId
                        && party.PartyType == PartyTypes.Supplier,
                    cancellationToken);
            })
            .WithMessage(
                "Supplier must be a SUPPLIER party that belongs to the current tenant.");

        RuleFor(request => request.ReceiverId)
            .NotEmpty()
            .MustAsync(async (receiverId, cancellationToken) =>
            {
                if (!tenantService.IsResolved || tenantService.TenantId == Guid.Empty)
                {
                    return false;
                }

                return await dbContext.Users.AnyAsync(
                    user =>
                        user.Id == receiverId
                        && user.TenantId == tenantService.TenantId
                        && user.IsActive,
                    cancellationToken);
            })
            .WithMessage(
                "Receiver must be an active user that belongs to the current tenant.");

        RuleFor(request => request.SupplierInvoiceNo)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.PurchaseDate)
            .Must(purchaseDate =>
                purchaseDate.ToUniversalTime().Date <= DateTime.UtcNow.Date)
            .WithMessage("PurchaseDate must not be in the future.");

        RuleFor(request => request.DiscountPaisa)
            .GreaterThanOrEqualTo(0);

        RuleFor(request => request.Lines)
            .NotEmpty()
            .WithMessage("At least one purchase line is required.");

        RuleForEach(request => request.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0)
                .WithMessage("Quantity must be positive.");

            line.RuleFor(l => l.BonusQuantity)
                .GreaterThanOrEqualTo(0);

            line.RuleFor(l => l.PurchasePricePaisa)
                .GreaterThan(0)
                .WithMessage("PurchasePrice must be greater than zero.");

            line.RuleFor(l => l.RetailPricePaisa)
                .GreaterThanOrEqualTo(0);

            line.RuleFor(l => l.BatchNumber)
                .MaximumLength(100);

            line.RuleFor(l => l.RackLocation)
                .MaximumLength(50);
        });

        RuleFor(request => request.Lines)
            .MustAsync(async (lines, cancellationToken) =>
            {
                if (!tenantService.IsResolved || tenantService.TenantId == Guid.Empty)
                {
                    return false;
                }

                if (lines is null || lines.Count == 0)
                {
                    return true;
                }

                Guid[] productIds = lines
                    .Select(line => line.ProductId)
                    .Distinct()
                    .ToArray();

                int found = await dbContext.Products.CountAsync(
                    product =>
                        productIds.Contains(product.Id)
                        && product.TenantId == tenantService.TenantId,
                    cancellationToken);

                return found == productIds.Length;
            })
            .WithMessage(
                "Every product on the purchase must belong to the current tenant.");
    }
}
