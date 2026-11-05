using System.Text.Json;
using Forum.Api.Features.Categories;
using Forum.Api.Features.Topics;
using StackExchange.Redis;

namespace Forum.Api.Infrastructure.Redis;

public sealed class CacheService(
    IConnectionMultiplexer connectionMultiplexer,
    RedisOptions options,
    ILogger<CacheService> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CategoryResponse>?> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var value = await GetAsync(GetCategoriesKey(), cancellationToken);
        return value is null ? null : JsonSerializer.Deserialize<List<CategoryResponse>>(value, SerializerOptions);
    }

    public Task SetCategoriesAsync(IReadOnlyList<CategoryResponse> categories, CancellationToken cancellationToken) =>
        SetAsync(GetCategoriesKey(), JsonSerializer.Serialize(categories, SerializerOptions), cancellationToken);

    public async Task<PagedResponse<TopicListItemResponse>?> GetCategoryTopicsAsync(
        long categoryId,
        string sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var value = await GetAsync(GetCategoryTopicsKey(categoryId, sort, page, pageSize), cancellationToken);
        return value is null
            ? null
            : JsonSerializer.Deserialize<PagedResponse<TopicListItemResponse>>(value, SerializerOptions);
    }

    public async Task SetCategoryTopicsAsync(
        long categoryId,
        string sort,
        int page,
        int pageSize,
        PagedResponse<TopicListItemResponse> response,
        CancellationToken cancellationToken)
    {
        var cacheKey = GetCategoryTopicsKey(categoryId, sort, page, pageSize);
        var tagKey = GetCategoryTagKey(categoryId);

        try
        {
            var database = connectionMultiplexer.GetDatabase();
            var batch = database.CreateBatch();
            var setValue = batch.StringSetAsync(cacheKey, JsonSerializer.Serialize(response, SerializerOptions), options.CacheTtl);
            var addTag = batch.SetAddAsync(tagKey, cacheKey.ToString());
            var expireTag = batch.KeyExpireAsync(tagKey, options.CacheTtl);
            batch.Execute();
            await Task.WhenAll(setValue, addTag, expireTag).WaitAsync(cancellationToken);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Unable to cache category topics.");
        }
    }

    public async Task InvalidateCategoryAsync(long categoryId, CancellationToken cancellationToken)
    {
        try
        {
            var database = connectionMultiplexer.GetDatabase();
            var tagKey = GetCategoryTagKey(categoryId);
            var cacheKeys = await database.SetMembersAsync(tagKey).WaitAsync(cancellationToken);
            if (cacheKeys.Length > 0)
            {
                await database.KeyDeleteAsync(cacheKeys.Select(key => (RedisKey)key.ToString()).ToArray()).WaitAsync(cancellationToken);
            }

            await database.KeyDeleteAsync(tagKey).WaitAsync(cancellationToken);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Unable to invalidate category topics cache.");
        }
    }

    public Task InvalidateCategoriesAsync(CancellationToken cancellationToken) =>
        DeleteAsync(GetCategoriesKey(), cancellationToken);

    private async Task<string?> GetAsync(RedisKey key, CancellationToken cancellationToken)
    {
        try
        {
            var value = await connectionMultiplexer.GetDatabase().StringGetAsync(key).WaitAsync(cancellationToken);
            return value.HasValue ? value.ToString() : null;
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Unable to read Redis cache.");
            return null;
        }
    }

    private async Task SetAsync(RedisKey key, string value, CancellationToken cancellationToken)
    {
        try
        {
            await connectionMultiplexer.GetDatabase().StringSetAsync(key, value, options.CacheTtl).WaitAsync(cancellationToken);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Unable to write Redis cache.");
        }
    }

    private async Task DeleteAsync(RedisKey key, CancellationToken cancellationToken)
    {
        try
        {
            await connectionMultiplexer.GetDatabase().KeyDeleteAsync(key).WaitAsync(cancellationToken);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Unable to invalidate Redis cache.");
        }
    }

    private RedisKey GetCategoriesKey() => $"{options.CacheKeyPrefix}:categories";

    private RedisKey GetCategoryTagKey(long categoryId) => $"{options.CacheKeyPrefix}:category:{categoryId}:keys";

    private RedisKey GetCategoryTopicsKey(long categoryId, string sort, int page, int pageSize) =>
        $"{options.CacheKeyPrefix}:category:{categoryId}:topics:{sort}:{page}:{pageSize}";
}
