using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.DTOs.Collections;
using SareeStore.Application.Services.Interfaces;

namespace SareeStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", "application/problem+json")]
public class CollectionsController : ControllerBase
{
    private readonly ICollectionService _collectionService;

    public CollectionsController(ICollectionService collectionService)
    {
        _collectionService = collectionService;
    }

    /// <summary>
    /// Retrieves all curated collections.
    /// </summary>
    [HttpGet]
    [EndpointSummary("Get all collections")]
    [EndpointDescription("Returns all collections. Inactive collections can be included via query parameter.")]
    [ProducesResponseType(typeof(IReadOnlyList<CollectionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CollectionResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var collections = await _collectionService.GetAllAsync(includeInactive, cancellationToken);
        return Ok(collections);
    }

    /// <summary>
    /// Retrieves a collection by its unique ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [EndpointSummary("Get collection by ID")]
    [EndpointDescription("Returns collection details for the specified ID or 404 if not found.")]
    [ProducesResponseType(typeof(CollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CollectionResponse>> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var collection = await _collectionService.GetByIdAsync(id, cancellationToken);
        return Ok(collection);
    }

    /// <summary>
    /// Retrieves a collection by its URL slug.
    /// </summary>
    [HttpGet("slug/{slug}")]
    [EndpointSummary("Get collection by slug")]
    [EndpointDescription("Returns collection details for the specified URL slug or 404 if not found.")]
    [ProducesResponseType(typeof(CollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CollectionResponse>> GetBySlug(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var collection = await _collectionService.GetBySlugAsync(slug, cancellationToken);
        return Ok(collection);
    }

    /// <summary>
    /// Creates a new curated collection.
    /// </summary>
    [HttpPost]
    [EndpointSummary("Create collection")]
    [EndpointDescription("Creates a new collection (e.g. Bridal Festive, Royal Silk). Validates name and unique slug.")]
    [ProducesResponseType(typeof(CollectionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CollectionResponse>> Create(
        [FromBody] CreateCollectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await _collectionService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing collection.
    /// </summary>
    [HttpPut("{id:int}")]
    [EndpointSummary("Update collection")]
    [EndpointDescription("Updates collection details while ensuring name/slug uniqueness across other records.")]
    [ProducesResponseType(typeof(CollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CollectionResponse>> Update(
        int id,
        [FromBody] UpdateCollectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await _collectionService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deactivates a collection (soft delete).
    /// </summary>
    [HttpDelete("{id:int}")]
    [EndpointSummary("Deactivate collection")]
    [EndpointDescription("Soft-deactivates the collection maintaining existing many-to-many relationship mappings.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        int id,
        CancellationToken cancellationToken = default)
    {
        await _collectionService.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
