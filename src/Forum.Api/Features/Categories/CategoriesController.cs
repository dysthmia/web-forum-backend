using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Api.Features.Categories;

[ApiController]
[Route("api/categories")]
public class CategoriesController(CategoriesService categoriesService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetList(CancellationToken cancellationToken) =>
        Ok(await categoriesService.GetListAsync(cancellationToken));

    [AllowAnonymous]
    [HttpGet("{slug}")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var result = await categoriesService.GetBySlugAsync(slug, cancellationToken);
        return result.Failure == CategoryFailure.None
            ? Ok(result.Category)
            : NotFound(new ProblemDetails { Title = "Category was not found." });
    }

    [Authorize(Policy = "Admin")]
    [HttpPost]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categoriesService.CreateAsync(request, cancellationToken);
        return result.Failure switch
        {
            CategoryFailure.None => CreatedAtAction(nameof(GetBySlug), new { slug = result.Category!.Slug }, result.Category),
            CategoryFailure.DuplicateSlug => Conflict(new ProblemDetails { Title = "Category slug is already in use." }),
            _ => BadRequest(new ProblemDetails { Title = "Provide a valid category name and slug." })
        };
    }

    [Authorize(Policy = "Admin")]
    [HttpPut("{categoryId:long}")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(long categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categoriesService.UpdateAsync(categoryId, request, cancellationToken);
        return result.Failure switch
        {
            CategoryFailure.None => Ok(result.Category),
            CategoryFailure.DuplicateSlug => Conflict(new ProblemDetails { Title = "Category slug is already in use." }),
            CategoryFailure.NotFound => NotFound(new ProblemDetails { Title = "Category was not found." }),
            _ => BadRequest(new ProblemDetails { Title = "Provide a valid category name and slug." })
        };
    }

    [Authorize(Policy = "Admin")]
    [HttpDelete("{categoryId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long categoryId, CancellationToken cancellationToken)
    {
        var failure = await categoriesService.DeleteAsync(categoryId, cancellationToken);
        return failure switch
        {
            CategoryFailure.None => NoContent(),
            CategoryFailure.HasTopics => Conflict(new ProblemDetails
            {
                Title = "A category with topics cannot be deleted."
            }),
            _ => NotFound(new ProblemDetails { Title = "Category was not found." })
        };
    }
}
