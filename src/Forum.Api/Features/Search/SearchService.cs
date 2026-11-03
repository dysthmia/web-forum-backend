using Forum.Api.Features.Topics;
using Forum.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Forum.Api.Features.Search;

public enum SearchFailure
{
    None,
    InvalidRequest
}

public record SearchResult(SearchResponse? Response, SearchFailure Failure)
{
    public static SearchResult Succeeded(SearchResponse response) => new(response, SearchFailure.None);

    public static SearchResult Failed(SearchFailure failure) => new(null, failure);
}

public class SearchService(ForumDbContext dbContext, TopicPaginationOptions paginationOptions)
{
    public async Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim() ?? string.Empty;
        if (query.Length is 0 or > 200 || !TryGetPage(request, out var page, out var pageSize, out var skip))
        {
            return SearchResult.Failed(SearchFailure.InvalidRequest);
        }

        return dbContext.Database.IsRelational()
            ? await SearchPostgreSqlAsync(query, page, pageSize, skip, cancellationToken)
            : await SearchInMemoryAsync(query, page, pageSize, skip, cancellationToken);
    }

    private async Task<SearchResult> SearchPostgreSqlAsync(
        string query,
        int page,
        int pageSize,
        int skip,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<SearchDatabaseRow>($"""
            WITH search_query AS (
                SELECT
                    websearch_to_tsquery('russian', {query})
                    || websearch_to_tsquery('english', {query}) AS value
            ),
            topic_matches AS (
                SELECT
                    topic.id AS topic_id,
                    ts_rank_cd(
                        to_tsvector('russian', coalesce(topic.title, ''))
                        || to_tsvector('english', coalesce(topic.title, '')),
                        search_query.value) AS relevance,
                    ts_headline(
                        'russian',
                        topic.title,
                        search_query.value,
                        'StartSel=, StopSel=, MaxFragments=1, MaxWords=15, MinWords=5') AS fragment
                FROM topics AS topic
                CROSS JOIN search_query
                WHERE topic.deleted_at IS NULL
                  AND (
                      to_tsvector('russian', coalesce(topic.title, ''))
                      || to_tsvector('english', coalesce(topic.title, ''))
                  ) @@ search_query.value
            ),
            post_candidates AS (
                SELECT
                    post.topic_id,
                    ts_rank_cd(
                        to_tsvector('russian', coalesce(post.content, ''))
                        || to_tsvector('english', coalesce(post.content, '')),
                        search_query.value) AS relevance,
                    ts_headline(
                        'russian',
                        post.content,
                        search_query.value,
                        'StartSel=, StopSel=, MaxFragments=1, MaxWords=15, MinWords=5') AS fragment
                FROM posts AS post
                INNER JOIN topics AS topic ON topic.id = post.topic_id
                CROSS JOIN search_query
                WHERE post.deleted_at IS NULL
                  AND topic.deleted_at IS NULL
                  AND (
                      to_tsvector('russian', coalesce(post.content, ''))
                      || to_tsvector('english', coalesce(post.content, ''))
                  ) @@ search_query.value
            ),
            post_matches AS (
                SELECT DISTINCT ON (topic_id) topic_id, relevance, fragment
                FROM post_candidates
                ORDER BY topic_id, relevance DESC
            )
            SELECT
                topic.id AS "TopicId",
                topic.category_id AS "CategoryId",
                topic.title AS "Title",
                COALESCE(topic_match.fragment, post_match.fragment) AS "Fragment",
                COALESCE(topic_match.relevance, 0) + COALESCE(post_match.relevance, 0) AS "Relevance",
                topic.created_at AS "CreatedAt",
                COUNT(*) OVER() AS "TotalCount"
            FROM topics AS topic
            LEFT JOIN topic_matches AS topic_match ON topic_match.topic_id = topic.id
            LEFT JOIN post_matches AS post_match ON post_match.topic_id = topic.id
            WHERE topic_match.topic_id IS NOT NULL OR post_match.topic_id IS NOT NULL
            ORDER BY "Relevance" DESC, topic.created_at DESC
            OFFSET {skip}
            LIMIT {pageSize}
            """).ToListAsync(cancellationToken);

        var totalCount = rows.FirstOrDefault()?.TotalCount ?? 0;
        var items = rows.Select(row => new SearchTopicResponse(
            row.TopicId,
            row.CategoryId,
            row.Title,
            row.Fragment,
            row.Relevance,
            row.CreatedAt)).ToList();
        return SearchResult.Succeeded(new SearchResponse(items, page, pageSize, checked((int)totalCount)));
    }

    private async Task<SearchResult> SearchInMemoryAsync(
        string query,
        int page,
        int pageSize,
        int skip,
        CancellationToken cancellationToken)
    {
        var topics = await dbContext.Topics.AsNoTracking()
            .Where(topic => topic.DeletedAt == null)
            .Select(topic => new { topic.Id, topic.CategoryId, topic.Title, topic.CreatedAt })
            .ToListAsync(cancellationToken);
        var activeTopicIds = topics.Select(topic => topic.Id).ToHashSet();
        var posts = await dbContext.Posts.AsNoTracking()
            .Where(post => post.DeletedAt == null && activeTopicIds.Contains(post.TopicId))
            .Select(post => new { post.TopicId, post.Content })
            .ToListAsync(cancellationToken);

        var matches = topics.Select(topic =>
        {
            var titleMatch = Contains(topic.Title, query);
            var matchingPost = posts.FirstOrDefault(post => post.TopicId == topic.Id && Contains(post.Content, query));
            var relevance = (titleMatch ? 2 : 0) + (matchingPost is null ? 0 : 1);
            var fragment = titleMatch ? CreateFragment(topic.Title, query) : CreateFragment(matchingPost?.Content ?? string.Empty, query);

            return new SearchTopicResponse(topic.Id, topic.CategoryId, topic.Title, fragment, relevance, topic.CreatedAt);
        })
        .Where(item => item.Relevance > 0)
        .OrderByDescending(item => item.Relevance)
        .ThenByDescending(item => item.CreatedAt)
        .ToList();

        return SearchResult.Succeeded(new SearchResponse(
            matches.Skip(skip).Take(pageSize).ToList(),
            page,
            pageSize,
            matches.Count));
    }

    private bool TryGetPage(SearchRequest request, out int page, out int pageSize, out int skip)
    {
        page = request.Page;
        pageSize = Math.Min(request.PageSize ?? paginationOptions.DefaultPageSize, paginationOptions.MaxPageSize);
        skip = 0;
        if (page < 1 || pageSize < 1)
        {
            return false;
        }

        var skipValue = ((long)page - 1) * pageSize;
        if (skipValue > int.MaxValue)
        {
            return false;
        }

        skip = (int)skipValue;
        return true;
    }

    private static bool Contains(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string CreateFragment(string value, string query)
    {
        const int fragmentLength = 160;
        var index = value.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0 || value.Length <= fragmentLength)
        {
            return value;
        }

        var start = Math.Max(0, index - 40);
        var length = Math.Min(fragmentLength, value.Length - start);
        return $"{(start > 0 ? "…" : string.Empty)}{value.Substring(start, length)}{(start + length < value.Length ? "…" : string.Empty)}";
    }
}

public class SearchDatabaseRow
{
    public long TopicId { get; init; }

    public long CategoryId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Fragment { get; init; } = string.Empty;

    public float Relevance { get; init; }

    public DateTime CreatedAt { get; init; }

    public long TotalCount { get; init; }
}
