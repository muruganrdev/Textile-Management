using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.DTOs.Cart;
using SareeStore.Application.Services.Interfaces;

namespace SareeStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Customer")]
[Produces("application/json", "application/problem+json")]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    /// <summary>
    /// Retrieves the authenticated customer's shopping cart.
    /// </summary>
    [HttpGet]
    [EndpointSummary("Get shopping cart")]
    [EndpointDescription("Returns the current authenticated customer's shopping cart with fresh product details, line totals, and stock status.")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CartResponse>> GetCart(CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var cart = await _cartService.GetCartAsync(customerId, cancellationToken);
        return Ok(cart);
    }

    /// <summary>
    /// Adds a product to the authenticated customer's cart or increments its quantity.
    /// </summary>
    [HttpPost("items")]
    [EndpointSummary("Add item to cart")]
    [EndpointDescription("Adds a saree product to the cart. If the product already exists, increments the quantity. Validates active status and available stock.")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartResponse>> AddItem(
        [FromBody] AddToCartRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var cart = await _cartService.AddItemAsync(customerId, request, cancellationToken);
        return Ok(cart);
    }

    /// <summary>
    /// Updates the quantity of a specific product in the customer's cart.
    /// </summary>
    [HttpPut("items/{productId:int}")]
    [EndpointSummary("Update cart item quantity")]
    [EndpointDescription("Updates the quantity for a specific product in the cart. Validates available inventory stock.")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartResponse>> UpdateItemQuantity(
        int productId,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var cart = await _cartService.UpdateItemQuantityAsync(customerId, productId, request, cancellationToken);
        return Ok(cart);
    }

    /// <summary>
    /// Removes a specific product from the customer's cart.
    /// </summary>
    [HttpDelete("items/{productId:int}")]
    [EndpointSummary("Remove item from cart")]
    [EndpointDescription("Removes a specific product from the customer's shopping cart.")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartResponse>> RemoveItem(
        int productId,
        CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var cart = await _cartService.RemoveItemAsync(customerId, productId, cancellationToken);
        return Ok(cart);
    }

    /// <summary>
    /// Clears all items from the customer's shopping cart.
    /// </summary>
    [HttpDelete]
    [EndpointSummary("Clear shopping cart")]
    [EndpointDescription("Empties the customer's shopping cart completely.")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CartResponse>> ClearCart(CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var cart = await _cartService.ClearCartAsync(customerId, cancellationToken);
        return Ok(cart);
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Customer identifier could not be determined from authentication claims.");
        }

        return userId;
    }
}
