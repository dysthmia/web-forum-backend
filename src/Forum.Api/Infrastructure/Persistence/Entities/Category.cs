namespace Forum.Api.Infrastructure.Persistence.Entities;

public class Category
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }
}
