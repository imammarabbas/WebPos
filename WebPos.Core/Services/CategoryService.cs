using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class CategoryService(
    IDbContextFactory<WebPosDbContext> dbFactory,
    ITenantService tenantService) : ICategoryService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory =
        dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    private readonly ITenantService _tenantService =
        tenantService ?? throw new ArgumentNullException(nameof(tenantService));

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        List<Category> categories = await context.Categories
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

        return ExecuteInOwnTransactionAsync(async (context, ct) =>
        {
            Category category = new()
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantService.TenantId,
                Name = request.Name.Trim(),
                ParentCategoryId = request.ParentCategoryId,
                TargetMarginPercentage = request.TargetMarginPercentage,
                ShowOnWebshop = request.ShowOnWebshop,
                ShowOnPosQuick = request.ShowOnPosQuick,
                PosQuickSort = request.PosQuickSort,
                CreatedAt = DateTimeOffset.UtcNow
            };
            context.Categories.Add(category);
            await context.SaveChangesAsync(ct);
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

        return ExecuteInOwnTransactionAsync(async (context, ct) =>
        {
            Category category = await context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == categoryId, ct)
                ?? throw new KeyNotFoundException("Category was not found.");

            category.Name = request.Name.Trim();
            category.ParentCategoryId = request.ParentCategoryId;
            category.TargetMarginPercentage = request.TargetMarginPercentage;
            category.ShowOnWebshop = request.ShowOnWebshop;
            category.ShowOnPosQuick = request.ShowOnPosQuick;
            category.PosQuickSort = request.PosQuickSort;
            await context.SaveChangesAsync(ct);
            return ToDto(category);
        }, cancellationToken);
    }

    private async Task<T> ExecuteInOwnTransactionAsync<T>(
        Func<WebPosDbContext, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (!context.Database.IsRelational())
        {
            T inMemoryResult = await action(context, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return inMemoryResult;
        }

        await using IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            T result = await action(context, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            throw;
        }
    }

    private static CategoryDto ToDto(Category category) =>
        new()
        {
            Id = category.Id,
            Name = category.Name,
            ParentCategoryId = category.ParentCategoryId,
            TargetMarginPercentage = category.TargetMarginPercentage,
            ShowOnWebshop = category.ShowOnWebshop,
            ShowOnPosQuick = category.ShowOnPosQuick,
            PosQuickSort = category.PosQuickSort,
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
