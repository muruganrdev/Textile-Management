using FluentValidation;
using SareeStore.Application.Common.Exceptions;
using ValidationException = SareeStore.Application.Common.Exceptions.ValidationException;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Cart;
using SareeStore.Application.Services.Interfaces;
using SareeStore.Domain.Entities;

namespace SareeStore.Application.Services;

/// <summary>
/// Application service implementing shopping cart business logic.
/// </summary>
public class CartService : ICartService
{
    private readonly ICartRepository _cartRepository;
    private readonly IProductRepository _productRepository;
    private readonly IValidator<AddToCartRequest> _addToCartValidator;
    private readonly IValidator<UpdateCartItemRequest> _updateCartItemValidator;

    public CartService(
        ICartRepository cartRepository,
        IProductRepository productRepository,
        IValidator<AddToCartRequest> addToCartValidator,
        IValidator<UpdateCartItemRequest> updateCartItemValidator)
    {
        _cartRepository = cartRepository;
        _productRepository = productRepository;
        _addToCartValidator = addToCartValidator;
        _updateCartItemValidator = updateCartItemValidator;
    }

    public async Task<CartResponse> GetCartAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetByUserIdWithDetailsAsync(userId, cancellationToken);
        if (cart is null)
        {
            return new CartResponse
            {
                CartId = 0,
                Items = new List<CartItemResponse>(),
                TotalItems = 0,
                SubTotal = 0.00m
            };
        }

        var itemResponses = cart.Items.Select(item =>
        {
            var product = item.Product;
            var availableStock = product?.Inventory?.StockQuantity ?? 0;
            var isProductActive = product?.IsActive ?? false;
            var effectivePrice = product is not null ? (product.DiscountPrice ?? product.Price) : 0.00m;
            var lineTotal = effectivePrice * item.Quantity;
            var isAvailable = isProductActive && availableStock >= item.Quantity;

            string? availabilityMessage = null;
            if (!isProductActive)
            {
                availabilityMessage = "Product is no longer available";
            }
            else if (availableStock == 0)
            {
                availabilityMessage = "Out of stock";
            }
            else if (availableStock < item.Quantity)
            {
                availabilityMessage = $"Only {availableStock} item(s) left in stock";
            }

            var primaryImageUrl = product?.Images?.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                ?? product?.Images?.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl;

            return new CartItemResponse
            {
                ProductId = item.ProductId,
                ProductTitle = product?.Title ?? string.Empty,
                SKU = product?.SKU ?? string.Empty,
                PrimaryImageUrl = primaryImageUrl,
                BasePrice = product?.Price ?? 0.00m,
                DiscountPrice = product?.DiscountPrice,
                EffectiveUnitPrice = effectivePrice,
                Quantity = item.Quantity,
                LineTotal = lineTotal,
                AvailableStock = availableStock,
                IsAvailable = isAvailable,
                AvailabilityMessage = availabilityMessage
            };
        }).ToList();

        return new CartResponse
        {
            CartId = cart.Id,
            Items = itemResponses,
            TotalItems = itemResponses.Sum(i => i.Quantity),
            SubTotal = itemResponses.Sum(i => i.LineTotal)
        };
    }

    public async Task<CartResponse> AddItemAsync(
        int userId,
        AddToCartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _addToCartValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            throw new NotFoundException("Product", request.ProductId);
        }

        if (!product.IsActive)
        {
            throw new ValidationException("Inactive products cannot be added to the cart.");
        }

        var availableStock = product.Inventory?.StockQuantity ?? 0;
        if (availableStock <= 0)
        {
            throw new ValidationException("Out-of-stock products cannot be added to the cart.");
        }

        var cart = await _cartRepository.GetByUserIdAsync(userId, cancellationToken);
        if (cart is null)
        {
            cart = await _cartRepository.CreateAsync(new Cart { UserId = userId }, cancellationToken);
        }

        var existingItem = await _cartRepository.GetItemAsync(cart.Id, request.ProductId, cancellationToken);
        if (existingItem is not null)
        {
            var newQuantity = existingItem.Quantity + request.Quantity;
            if (newQuantity > availableStock)
            {
                throw new ValidationException(
                    $"Cannot add {request.Quantity} item(s). Total quantity in cart ({newQuantity}) would exceed available stock ({availableStock}).");
            }

            existingItem.Quantity = newQuantity;
            await _cartRepository.UpdateItemAsync(existingItem, cancellationToken);
        }
        else
        {
            if (request.Quantity > availableStock)
            {
                throw new ValidationException(
                    $"Requested quantity ({request.Quantity}) exceeds available stock ({availableStock}).");
            }

            var newItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = request.ProductId,
                Quantity = request.Quantity
            };
            await _cartRepository.AddItemAsync(newItem, cancellationToken);
        }

        return await GetCartAsync(userId, cancellationToken);
    }

    public async Task<CartResponse> UpdateItemQuantityAsync(
        int userId,
        int productId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _updateCartItemValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var cart = await _cartRepository.GetByUserIdAsync(userId, cancellationToken);
        if (cart is null)
        {
            throw new NotFoundException("Cart was not found for the customer.");
        }

        var item = await _cartRepository.GetItemAsync(cart.Id, productId, cancellationToken);
        if (item is null)
        {
            throw new NotFoundException($"Cart item for product ID {productId} was not found.");
        }

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            throw new NotFoundException("Product", productId);
        }

        if (!product.IsActive)
        {
            throw new ValidationException("Inactive products cannot be updated in cart.");
        }

        var availableStock = product.Inventory?.StockQuantity ?? 0;
        if (request.Quantity > availableStock)
        {
            throw new ValidationException(
                $"Requested quantity ({request.Quantity}) exceeds available stock ({availableStock}).");
        }

        item.Quantity = request.Quantity;
        await _cartRepository.UpdateItemAsync(item, cancellationToken);

        return await GetCartAsync(userId, cancellationToken);
    }

    public async Task<CartResponse> RemoveItemAsync(
        int userId,
        int productId,
        CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId, cancellationToken);
        if (cart is null)
        {
            throw new NotFoundException("Cart was not found for the customer.");
        }

        var item = await _cartRepository.GetItemAsync(cart.Id, productId, cancellationToken);
        if (item is null)
        {
            throw new NotFoundException($"Cart item for product ID {productId} was not found.");
        }

        await _cartRepository.RemoveItemAsync(item, cancellationToken);

        return await GetCartAsync(userId, cancellationToken);
    }

    public async Task<CartResponse> ClearCartAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId, cancellationToken);
        if (cart is not null)
        {
            await _cartRepository.ClearAsync(cart.Id, cancellationToken);
        }

        return await GetCartAsync(userId, cancellationToken);
    }
}
