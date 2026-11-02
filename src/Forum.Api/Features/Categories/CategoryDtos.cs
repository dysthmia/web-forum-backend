using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Categories;

public record CategoryResponse(long Id, string Name, string Slug, string? Description);

public class CreateCategoryRequest
{
    [Required]
    [StringLength(100)]
    public string? Name { get; init; }

    [Required]
    [StringLength(100)]
    public string? Slug { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }
}

public class UpdateCategoryRequest
{
    [Required]
    [StringLength(100)]
    public string? Name { get; init; }

    [Required]
    [StringLength(100)]
    public string? Slug { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }
}
