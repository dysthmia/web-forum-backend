using Forum.Api.Features.Categories;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class CategoriesServiceTests
{
    [Fact]
    public async Task CreateAsync_ReturnsConflict_WhenSlugAlreadyExists()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Categories.Add(new Category { Name = "General", Slug = "general" });
        await dbContext.SaveChangesAsync();

        var result = await new CategoriesService(dbContext).CreateAsync(
            new CreateCategoryRequest { Name = "Other", Slug = "general" },
            CancellationToken.None);

        Assert.Equal(CategoryFailure.DuplicateSlug, result.Failure);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsConflict_WhenCategoryHasTopics()
    {
        await using var dbContext = CreateDbContext();
        var category = new Category { Name = "General", Slug = "general" };
        var user = new User
        {
            Username = "author",
            Email = "author@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.AddRange(category, user);
        await dbContext.SaveChangesAsync();
        dbContext.Topics.Add(new Topic
        {
            CategoryId = category.Id,
            AuthorId = user.Id,
            Title = "Topic",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var failure = await new CategoriesService(dbContext).DeleteAsync(category.Id, CancellationToken.None);

        Assert.Equal(CategoryFailure.HasTopics, failure);
        Assert.True(await dbContext.Categories.AnyAsync(item => item.Id == category.Id));
    }

    [Fact]
    public async Task GetListAsync_ReturnsPublicCategoryFieldsInNameOrder()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Categories.AddRange(
            new Category { Name = "Zeta", Slug = "zeta" },
            new Category { Name = "Alpha", Slug = "alpha", Description = "First" });
        await dbContext.SaveChangesAsync();

        var categories = await new CategoriesService(dbContext).GetListAsync(CancellationToken.None);

        Assert.Collection(categories,
            category => Assert.Equal("Alpha", category.Name),
            category => Assert.Equal("Zeta", category.Name));
    }

    private static ForumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ForumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ForumDbContext(options);
    }
}
