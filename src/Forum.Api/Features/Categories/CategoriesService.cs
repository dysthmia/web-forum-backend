using Forum.Api.Infrastructure.Persistence;
using Forum.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Forum.Api.Features.Categories;

public enum CategoryFailure
{
    None,
    NotFound,
    DuplicateSlug,
    InvalidRequest,
    HasTopics
}

public record CategoryResult(CategoryResponse? Category, CategoryFailure Failure)
{
    public static CategoryResult Success(CategoryResponse category) => new(category, CategoryFailure.None);

    public static CategoryResult Failed(CategoryFailure failure) => new(null, failure);
}

public class CategoriesService(ForumDbContext dbContext)
{
    public async Task<IReadOnlyList<CategoryResponse>> GetListAsync(CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new CategoryResponse(
                category.Id,
                category.Name,
                category.Slug,
                category.Description))
            .ToListAsync(cancellationToken);

    public async Task<CategoryResult> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.AsNoTracking()
            .Where(item => item.Slug == slug)
            .Select(item => new CategoryResponse(
                item.Id,
                item.Name,
                item.Slug,
                item.Description))
            .SingleOrDefaultAsync(cancellationToken);

        return category is null
            ? CategoryResult.Failed(CategoryFailure.NotFound)
            : CategoryResult.Success(category);
    }

    public async Task<CategoryResult> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!TryPrepare(request.Name, request.Slug, request.Description, out var name, out var slug, out var description))
        {
            return CategoryResult.Failed(CategoryFailure.InvalidRequest);
        }

        if (await dbContext.Categories.AnyAsync(category => category.Slug == slug, cancellationToken))
        {
            return CategoryResult.Failed(CategoryFailure.DuplicateSlug);
        }

        var category = new Category { Name = name, Slug = slug, Description = description };
        dbContext.Categories.Add(category);

        if (!await TrySaveAsync(cancellationToken))
        {
            return CategoryResult.Failed(CategoryFailure.DuplicateSlug);
        }

        return CategoryResult.Success(ToResponse(category));
    }

    public async Task<CategoryResult> UpdateAsync(long categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!TryPrepare(request.Name, request.Slug, request.Description, out var name, out var slug, out var description))
        {
            return CategoryResult.Failed(CategoryFailure.InvalidRequest);
        }

        var category = await dbContext.Categories.SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return CategoryResult.Failed(CategoryFailure.NotFound);
        }

        if (await dbContext.Categories.AnyAsync(item => item.Id != categoryId && item.Slug == slug, cancellationToken))
        {
            return CategoryResult.Failed(CategoryFailure.DuplicateSlug);
        }

        category.Name = name;
        category.Slug = slug;
        category.Description = description;

        return await TrySaveAsync(cancellationToken)
            ? CategoryResult.Success(ToResponse(category))
            : CategoryResult.Failed(CategoryFailure.DuplicateSlug);
    }

    public async Task<CategoryFailure> DeleteAsync(long categoryId, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return CategoryFailure.NotFound;
        }

        if (await dbContext.Topics.AnyAsync(topic => topic.CategoryId == categoryId, cancellationToken))
        {
            return CategoryFailure.HasTopics;
        }

        dbContext.Categories.Remove(category);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return CategoryFailure.None;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            return CategoryFailure.HasTopics;
        }
    }

    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }

    private static bool TryPrepare(
        string? rawName,
        string? rawSlug,
        string? rawDescription,
        out string name,
        out string slug,
        out string? description)
    {
        name = rawName?.Trim() ?? string.Empty;
        slug = rawSlug?.Trim() ?? string.Empty;
        description = string.IsNullOrWhiteSpace(rawDescription) ? null : rawDescription.Trim();

        return name.Length is > 0 and <= 100
               && slug.Length is > 0 and <= 100
               && (description is null || description.Length <= 1000);
    }

    private static CategoryResponse ToResponse(Category category) =>
        new(category.Id, category.Name, category.Slug, category.Description);
}
