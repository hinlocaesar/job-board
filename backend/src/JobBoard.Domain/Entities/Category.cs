namespace JobBoard.Domain.Entities;

/// <summary>
/// Job category. Source of truth for filtering in the marketplace DB;
/// Umbraco holds the matching SEO description page (joined by <see cref="Slug"/>).
/// </summary>
public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string? ShortDescription { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Key of the Umbraco landing page that expands on this category.</summary>
    public Guid? UmbracoContentKey { get; set; }

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
