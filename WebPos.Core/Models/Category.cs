using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class Category : BaseEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? ParentCategoryId { get; set; }

    public int TargetMarginPercentage { get; set; }

    public bool ShowOnWebshop { get; set; }

    /// <summary>When true, category appears on the POS Quick Items strip.</summary>
    public bool ShowOnPosQuick { get; set; }

    /// <summary>Lower values sort first on the POS Quick Items strip.</summary>
    public int PosQuickSort { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Category? ParentCategory { get; set; }

    public ICollection<Category> InverseParentCategory { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
