using Forum.Api.Common.Authorization;
using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Forum.Api.Features.Topics;

public enum CreateTopicFailure
{
    None,
    InvalidRequest,
    CategoryNotFound,
    AuthorNotFound,
    Banned,
    CreationFailed
}

public record CreateTopicResult(CreateTopicResponse? Response, CreateTopicFailure Failure)
{
    public static CreateTopicResult Succeeded(CreateTopicResponse response) => new(response, CreateTopicFailure.None);

    public static CreateTopicResult Failed(CreateTopicFailure failure) => new(null, failure);
}

public class TopicsService(ForumDbContext dbContext, ActiveUserBanService banService)
{
    public async Task<CreateTopicResult> CreateAsync(
        long authorId,
        CreateTopicRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryPrepare(request, out var title, out var content))
        {
            return CreateTopicResult.Failed(CreateTopicFailure.InvalidRequest);
        }

        if (await banService.IsBannedAsync(authorId, cancellationToken))
        {
            return CreateTopicResult.Failed(CreateTopicFailure.Banned);
        }

        if (!await dbContext.Categories.AnyAsync(category => category.Id == request.CategoryId, cancellationToken))
        {
            return CreateTopicResult.Failed(CreateTopicFailure.CategoryNotFound);
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == authorId, cancellationToken))
        {
            return CreateTopicResult.Failed(CreateTopicFailure.AuthorNotFound);
        }

        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational())
        {
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        await using (transaction)
        {
            try
            {
                var now = DateTime.UtcNow;
                var topic = new Topic
                {
                    CategoryId = request.CategoryId,
                    AuthorId = authorId,
                    Title = title,
                    ViewCount = 0,
                    IsClosed = false,
                    IsPinned = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                dbContext.Topics.Add(topic);
                await dbContext.SaveChangesAsync(cancellationToken);

                dbContext.Posts.Add(new Post
                {
                    TopicId = topic.Id,
                    AuthorId = authorId,
                    Position = 1,
                    Content = content,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                await dbContext.SaveChangesAsync(cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return CreateTopicResult.Succeeded(new CreateTopicResponse(topic.Id));
            }
            catch (DbUpdateException)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }

                return CreateTopicResult.Failed(CreateTopicFailure.CreationFailed);
            }
        }
    }

    private static bool TryPrepare(CreateTopicRequest request, out string title, out string content)
    {
        title = request.Title?.Trim() ?? string.Empty;
        content = request.Content?.Trim() ?? string.Empty;

        return request.CategoryId > 0 && title.Length is > 0 and <= 200 && content.Length > 0;
    }
}
