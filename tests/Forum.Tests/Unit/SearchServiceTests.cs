using Forum.Api.Features.Search;
using Forum.Api.Features.Topics;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forum.Tests.Unit;

public class SearchServiceTests
{
    [Fact]
    public async Task SearchAsync_FindsRussianEnglishTitlesAndPostContent()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext);
        var service = CreateService(dbContext);

        var russian = await service.SearchAsync(new SearchRequest { Query = "разработка" }, CancellationToken.None);
        var english = await service.SearchAsync(new SearchRequest { Query = "guide" }, CancellationToken.None);
        var post = await service.SearchAsync(new SearchRequest { Query = "Redis" }, CancellationToken.None);

        Assert.Equal(SearchFailure.None, russian.Failure);
        Assert.Equal("Разработка API", Assert.Single(russian.Response!.Items).Title);
        Assert.Equal("English guide", Assert.Single(english.Response!.Items).Title);
        var postMatch = Assert.Single(post.Response!.Items);
        Assert.Equal("Announcements", postMatch.Title);
        Assert.Contains("Redis", postMatch.Fragment);
    }

    [Fact]
    public async Task SearchAsync_ExcludesDeletedTopicsAndPostsAndPaginates()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext);
        var service = CreateService(dbContext);

        var hidden = await service.SearchAsync(new SearchRequest { Query = "hidden" }, CancellationToken.None);
        var paged = await service.SearchAsync(
            new SearchRequest { Query = "common", Page = 2, PageSize = 1 },
            CancellationToken.None);

        Assert.Empty(hidden.Response!.Items);
        Assert.Equal(2, paged.Response!.TotalCount);
        Assert.Single(paged.Response.Items);
        Assert.Equal(2, paged.Response.Page);
        Assert.Equal(1, paged.Response.PageSize);
    }

    [Fact]
    public async Task SearchAsync_TreatsSqlLikeInputAsSearchText()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext);

        var result = await CreateService(dbContext).SearchAsync(
            new SearchRequest { Query = "' OR 1=1 --" },
            CancellationToken.None);

        Assert.Equal(SearchFailure.None, result.Failure);
        Assert.Empty(result.Response!.Items);
    }

    private static SearchService CreateService(ForumDbContext dbContext) =>
        new(dbContext, new TopicPaginationOptions { DefaultPageSize = 10, MaxPageSize = 20 });

    private static async Task SeedAsync(ForumDbContext dbContext)
    {
        var category = new Category { Name = "General", Slug = "general" };
        var author = new User
        {
            Username = "author",
            Email = "author@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.AddRange(category, author);
        await dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var russianTopic = CreateTopic(category.Id, author.Id, "Разработка API", now.AddMinutes(-1));
        var englishTopic = CreateTopic(category.Id, author.Id, "English guide", now);
        var postMatchTopic = CreateTopic(category.Id, author.Id, "Announcements", now.AddMinutes(-2));
        var deletedTopic = CreateTopic(category.Id, author.Id, "Hidden topic", now.AddMinutes(-3));
        deletedTopic.DeletedAt = now;
        var deletedPostTopic = CreateTopic(category.Id, author.Id, "Visible topic", now.AddMinutes(-4));
        var firstCommonTopic = CreateTopic(category.Id, author.Id, "Common one", now.AddMinutes(-5));
        var secondCommonTopic = CreateTopic(category.Id, author.Id, "Common two", now.AddMinutes(-6));
        dbContext.AddRange(russianTopic, englishTopic, postMatchTopic, deletedTopic, deletedPostTopic, firstCommonTopic, secondCommonTopic);
        await dbContext.SaveChangesAsync();

        dbContext.AddRange(
            CreatePost(russianTopic.Id, author.Id, "Первое сообщение"),
            CreatePost(englishTopic.Id, author.Id, "First message"),
            CreatePost(postMatchTopic.Id, author.Id, "How to configure Redis cache"),
            CreatePost(deletedTopic.Id, author.Id, "hidden"),
            CreatePost(deletedPostTopic.Id, author.Id, "hidden", deleted: true),
            CreatePost(firstCommonTopic.Id, author.Id, "First common post"),
            CreatePost(secondCommonTopic.Id, author.Id, "Second common post"));
        await dbContext.SaveChangesAsync();
    }

    private static Topic CreateTopic(long categoryId, long authorId, string title, DateTime createdAt) => new()
    {
        CategoryId = categoryId,
        AuthorId = authorId,
        Title = title,
        CreatedAt = createdAt,
        UpdatedAt = createdAt
    };

    private static Post CreatePost(long topicId, long authorId, string content, bool deleted = false) => new()
    {
        TopicId = topicId,
        AuthorId = authorId,
        Position = 1,
        Content = content,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        DeletedAt = deleted ? DateTime.UtcNow : null
    };

    private static ForumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ForumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ForumDbContext(options);
    }
}
