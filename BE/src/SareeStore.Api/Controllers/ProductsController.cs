using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.DTOs.Products;
using SareeStore.Application.Services.Interfaces;

namespace SareeStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", "application/problem+json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Retrieves all products with optional inclusion of inactive records.
    /// </summary>
    [HttpGet]
    [EndpointSummary("Get all products")]
    [EndpointDescription("Returns a list of all products. Inactive products can be included via query parameter.")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var products = await _productService.GetAllAsync(includeInactive, cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Retrieves a specific product by its unique identifier.
    /// </summary>
    [HttpGet("{id:int}")]
    [EndpointSummary("Get product by ID")]
    [EndpointDescription("Returns product details for the given ID or 404 if not found.")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var product = await _productService.GetByIdAsync(id, cancellationToken);
        return Ok(product);
    }

    /// <summary>
    /// Retrieves all active products belonging to a specific category.
    /// </summary>
    [HttpGet("category/{categoryId:int}")]
    [EndpointSummary("Get products by category")]
    [EndpointDescription("Returns all active products assigned to the specified category ID.")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetByCategory(
        int categoryId,
        CancellationToken cancellationToken = default)
    {
        var products = await _productService.GetByCategoryIdAsync(categoryId, cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Creates a new product and initializes inventory.
    /// </summary>
    [HttpPost]
    [EndpointSummary("Create product")]
    [EndpointDescription("Creates a new saree product. Validates category, uniqueness of SKU/Slug, and initializes stock inventory.")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await _productService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing product.
    /// </summary>
    [HttpPut("{id:int}")]
    [EndpointSummary("Update product")]
    [EndpointDescription("Updates product properties while ensuring SKU/Slug uniqueness across other products.")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Update(
        int id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await _productService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deactivates a product (soft delete).
    /// </summary>
    [HttpDelete("{id:int}")]
    [EndpointSummary("Deactivate product")]
    [EndpointDescription("Soft-deactivates the product to maintain referential integrity with historical orders.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        int id,
        CancellationToken cancellationToken = default)
    {
        await _productService.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
