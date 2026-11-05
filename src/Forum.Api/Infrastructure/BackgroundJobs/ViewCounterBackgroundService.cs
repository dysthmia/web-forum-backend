using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;

namespace Forum.Api.Infrastructure.BackgroundJobs;

public sealed class ViewCounterBackgroundService(
    IServiceScopeFactory scopeFactory,
    ViewCounterService viewCounterService,
    RedisOptions options,
    ILogger<ViewCounterBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await FlushSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(options.ViewFlushInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await FlushSafelyAsync(stoppingToken);
        }
    }

    private async Task FlushSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var topicId in viewCounterService.GetDirtyTopicIdsAsync().WithCancellation(cancellationToken))
            {
                var pendingViews = await viewCounterService.TakePendingViewsAsync(topicId, cancellationToken);
                if (pendingViews <= 0)
                {
                    continue;
                }

                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ForumDbContext>();
                await dbContext.Topics
                    .Where(topic => topic.Id == topicId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(topic => topic.ViewCount, topic => topic.ViewCount + pendingViews),
                        cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to flush pending topic view counters.");
        }
    }
}
