using WebPos.Core.Abstractions;

namespace WebPos.Core.Abstractions;

public sealed class UpsertCategoryRequest
{
    public required string Name { get; init; }

    public Guid? ParentCategoryId { get; init; }

    public int TargetMarginPercentage { get; init; }

    public bool ShowOnWebshop { get; init; }

    public bool ShowOnPosQuick { get; init; }

    public int PosQuickSort { get; init; }
}

public sealed class CategoryDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public Guid? ParentCategoryId { get; init; }

    public int TargetMarginPercentage { get; init; }

    public bool ShowOnWebshop { get; init; }

    public bool ShowOnPosQuick { get; init; }

    public int PosQuickSort { get; init; }

    public int ProductCount { get; init; }
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateAsync(UpsertCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryDto> UpdateAsync(Guid categoryId, UpsertCategoryRequest request, CancellationToken cancellationToken = default);
}

