using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class CategoryService(
    WebPosDbContext context,
    ITenantService tenantService,
    ITransactionService transactionService) : ICategoryService
{
    private readonly WebPosDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly ITenantService _tenantService =
        tenantService ?? throw new ArgumentNullException(nameof(tenantService));
    private readonly ITransactionService _transactionService =
        transactionService ?? throw new ArgumentNullException(nameof(transactionService));

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        List<Category> categories = await _context.Categories
            .AsNoTracking()
            .Include(c => c.Products)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return categories.Select(ToDto).ToList();
    }

    public Task<CategoryDto> CreateAsync(
        UpsertCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenant();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            Category category = new()
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantService.TenantId,
                Name = request.Name.Trim(),
                ParentCategoryId = request.ParentCategoryId,
                TargetMarginPercentage = request.TargetMarginPercentage,
                ShowOnWebshop = request.ShowOnWebshop,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync(ct);
            return ToDto(category);
        }, cancellationToken);
    }

    public Task<CategoryDto> UpdateAsync(
        Guid categoryId,
        UpsertCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenant();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            Category category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == categoryId, ct)
                ?? throw new KeyNotFoundException("Category was not found.");

            category.Name = request.Name.Trim();
            category.ParentCategoryId = request.ParentCategoryId;
            category.TargetMarginPercentage = request.TargetMarginPercentage;
            category.ShowOnWebshop = request.ShowOnWebshop;
            await _context.SaveChangesAsync(ct);
            return ToDto(category);
        }, cancellationToken);
    }

    private static CategoryDto ToDto(Category category) =>
        new()
        {
            Id = category.Id,
            Name = category.Name,
            ParentCategoryId = category.ParentCategoryId,
            TargetMarginPercentage = category.TargetMarginPercentage,
            ShowOnWebshop = category.ShowOnWebshop,
            ProductCount = category.Products?.Count ?? 0
        };

    private void EnsureTenant()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required for categories.");
        }
    }
}

