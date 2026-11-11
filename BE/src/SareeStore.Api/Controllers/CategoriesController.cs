using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.DTOs.Categories;
using SareeStore.Application.Services.Interfaces;

namespace SareeStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", "application/problem+json")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// Retrieves all categories with product counts.
    /// </summary>
    [HttpGet]
    [EndpointSummary("Get all categories")]
    [EndpointDescription("Returns all categories. Inactive categories can be included via query parameter.")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var categories = await _categoryService.GetAllAsync(includeInactive, cancellationToken);
        return Ok(categories);
    }

    /// <summary>
    /// Retrieves a category by its ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [EndpointSummary("Get category by ID")]
    [EndpointDescription("Returns category details for the specified ID or 404 if not found.")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var category = await _categoryService.GetByIdAsync(id, cancellationToken);
        return Ok(category);
    }

    /// <summary>
    /// Retrieves a category by its URL slug.
    /// </summary>
    [HttpGet("slug/{slug}")]
    [EndpointSummary("Get category by slug")]
    [EndpointDescription("Returns category details for the specified URL slug or 404 if not found.")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> GetBySlug(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var category = await _categoryService.GetBySlugAsync(slug, cancellationToken);
        return Ok(category);
    }

    /// <summary>
    /// Creates a new category.
    /// </summary>
    [HttpPost]
    [EndpointSummary("Create category")]
    [EndpointDescription("Creates a new product category. Validates unique name and slug.")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await _categoryService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing category.
    /// </summary>
    [HttpPut("{id:int}")]
    [EndpointSummary("Update category")]
    [EndpointDescription("Updates category details while verifying uniqueness of name/slug across other records.")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Update(
        int id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await _categoryService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deactivates a category (soft delete).
    /// </summary>
    [HttpDelete("{id:int}")]
    [EndpointSummary("Deactivate category")]
    [EndpointDescription("Soft-deactivates the category preserving referential integrity with associated sarees.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        int id,
        CancellationToken cancellationToken = default)
    {
        await _categoryService.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
