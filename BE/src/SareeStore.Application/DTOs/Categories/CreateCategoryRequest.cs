namespace SareeStore.Application.DTOs.Categories;

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
