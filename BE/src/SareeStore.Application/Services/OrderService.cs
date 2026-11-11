using System.Security.Cryptography;
using FluentValidation;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Orders;
using SareeStore.Application.Services.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using ValidationException = SareeStore.Application.Common.Exceptions.ValidationException;

namespace SareeStore.Application.Services;

/// <summary>
/// Service implementation for order creation, checkout validation, and customer order retrieval.
/// </summary>
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICartRepository _cartRepository;
    private readonly IValidator<CreateOrderRequest> _createOrderValidator;
    private readonly IValidator<CancelOrderRequest> _cancelOrderValidator;
    private readonly IRefundService _refundService;

    public OrderService(
        IOrderRepository orderRepository,
        ICartRepository cartRepository,
        IValidator<CreateOrderRequest> createOrderValidator,
        IValidator<CancelOrderRequest> cancelOrderValidator,
        IRefundService refundService)
    {
        _orderRepository = orderRepository;
        _cartRepository = cartRepository;
        _createOrderValidator = createOrderValidator;
        _cancelOrderValidator = cancelOrderValidator;
        _refundService = refundService;
    }

    public async Task<OrderResponse> CreateOrderAsync(
        int customerId,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validate request payload
        var validationResult = await _createOrderValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new ValidationException(errors);
        }

        // 2. Retrieve customer's active cart
        var cart = await _cartRepository.GetByUserIdWithDetailsAsync(customerId, cancellationToken);
        if (cart == null || cart.Items.Count == 0)
        {
            throw new ValidationException("Cannot checkout with an empty cart.");
        }

        // 3. Validate shipping address ownership
        var address = await _orderRepository.GetCustomerAddressAsync(request.ShippingAddressId, customerId, cancellationToken);
        if (address == null)
        {
            throw new ValidationException("The selected delivery address does not exist or does not belong to the authenticated customer.");
        }

        // 4. Validate items: active status, available stock, and compute selling prices
        var orderItems = new List<OrderItem>();
        decimal subTotal = 0m;

        foreach (var cartItem in cart.Items)
        {
            var product = cartItem.Product;
            if (product == null)
            {
                throw new ValidationException($"Product data for cart item {cartItem.Id} could not be loaded.");
            }

            if (!product.IsActive)
            {
                throw new ValidationException($"Product '{product.Title}' is inactive and cannot be ordered.");
            }

            var availableStock = product.Inventory?.StockQuantity ?? 0;
            if (availableStock < cartItem.Quantity)
            {
                throw new ValidationException(
                    $"Insufficient stock for product '{product.Title}'. Requested: {cartItem.Quantity}, Available: {availableStock}.");
            }

            // Price must come authoritative from the Product entity
            var regularPrice = product.Price;
            var unitPrice = product.DiscountPrice ?? product.Price;
            var itemDiscount = regularPrice - unitPrice;
            var lineTotal = unitPrice * cartItem.Quantity;
            subTotal += lineTotal;

            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductTitleSnapshot = product.Title,
                ProductSkuSnapshot = product.SKU,
                RegularPrice = regularPrice,
                UnitPrice = unitPrice,
                DiscountAmount = itemDiscount,
                Quantity = cartItem.Quantity,
                LineTotal = lineTotal
            });
        }

        // 5. Pricing totals (standard default shipping = 0.00m, no coupon discount)
        const decimal discountAmount = 0.00m;
        const decimal shippingFee = 0.00m;
        var totalAmount = subTotal - discountAmount + shippingFee;

        // 6. Generate unique server-side OrderNumber
        var orderNumber = await GenerateUniqueOrderNumberAsync(cancellationToken);

        // 7. Assemble Order with historical snapshots
        var order = new Order
        {
            CustomerId = customerId,
            OrderNumber = orderNumber,
            SubTotal = subTotal,
            DiscountAmount = discountAmount,
            ShippingFee = shippingFee,
            TotalAmount = totalAmount,
            OrderStatus = OrderStatus.Placed,
            PaymentStatus = PaymentStatus.Pending,
            OrderDateUtc = DateTime.UtcNow,

            // Frozen delivery address snapshot
            ShippingRecipientName = address.RecipientName,
            ShippingPhoneNumber = address.PhoneNumber,
            ShippingAddressLine1 = address.AddressLine1,
            ShippingAddressLine2 = address.AddressLine2,
            ShippingCity = address.City,
            ShippingState = address.State,
            ShippingPostalCode = address.PostalCode,
            ShippingCountry = string.IsNullOrWhiteSpace(address.Country) ? "India" : address.Country,

            OrderItems = orderItems
        };

        // 8. Create initial OrderStatusHistory with FromStatus null
        order.StatusHistories.Add(new OrderStatusHistory
        {
            ProcessedByUserId = null,
            FromStatus = null,
            ToStatus = OrderStatus.Placed,
            Remarks = "Order placed by customer",
            ChangedAtUtc = DateTime.UtcNow
        });

        // 9. Create initial Payment entry
        order.Payments.Add(new Payment
        {
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            Amount = totalAmount
        });

        // 10. Persist order, deduct inventory, and clear cart atomically
        var savedOrder = await _orderRepository.CreateOrderWithCartClearingAsync(order, cart.Id, GenerateOrderNumber, cancellationToken);

        return MapToOrderResponse(savedOrder, cart.Items.ToList());
    }

    public async Task<IReadOnlyList<OrderResponse>> GetMyOrdersAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetByCustomerIdAsync(customerId, cancellationToken);
        return orders.Select(o => MapToOrderResponse(o, null)).ToList();
    }

    public async Task<OrderResponse> GetOrderByIdAsync(
        int orderId,
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAndCustomerIdAsync(orderId, customerId, cancellationToken);
        if (order == null)
        {
            throw new NotFoundException($"Order with ID {orderId} was not found.");
        }

        return MapToOrderResponse(order, null);
    }

    public async Task<CancelOrderResponse> CancelOrderAsync(
        int customerId,
        int orderId,
        CancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validate request
        var validationResult = await _cancelOrderValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new ValidationException(errors);
        }

        // 2. Execute transactional cancellation and inventory restoration
        var cancelledOrder = await _orderRepository.CancelOrderWithStockRestorationAsync(
            orderId, customerId, null, request.Reason, cancellationToken);

        // 3. Evaluate refund eligibility
        return await ProcessCancellationRefundAsync(cancelledOrder, request.Reason, cancellationToken);
    }

    public async Task<CancelOrderResponse> AdminCancelOrderAsync(
        int adminUserId,
        int orderId,
        CancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validate request
        var validationResult = await _cancelOrderValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new ValidationException(errors);
        }

        // 2. Execute transactional cancellation and inventory restoration
        var cancelledOrder = await _orderRepository.CancelOrderWithStockRestorationAsync(
            orderId, null, adminUserId, request.Reason, cancellationToken);

        // 3. Evaluate refund eligibility
        return await ProcessCancellationRefundAsync(cancelledOrder, request.Reason, cancellationToken);
    }

    private async Task<CancelOrderResponse> ProcessCancellationRefundAsync(
        Order order,
        string reason,
        CancellationToken cancellationToken)
    {
        // 1. Check if a durable refund intent was created during the atomic cancellation transaction
        var refundIntent = order.Refunds.FirstOrDefault(r =>
            r.Status == RefundStatus.Pending ||
            r.Status == RefundStatus.Processing ||
            r.Status == RefundStatus.Success);

        if (refundIntent != null)
        {
            RefundResponse refundResponse;
            if (refundIntent.Status == RefundStatus.Success)
            {
                refundResponse = new RefundResponse
                {
                    Id = refundIntent.Id,
                    OrderId = refundIntent.OrderId,
                    PaymentId = refundIntent.PaymentId,
                    RefundReference = refundIntent.RefundReference,
                    Amount = refundIntent.Amount,
                    Currency = refundIntent.Currency,
                    Status = refundIntent.Status.ToString(),
                    Reason = refundIntent.Reason,
                    FailureReason = refundIntent.FailureReason,
                    CreatedAtUtc = refundIntent.CreatedAtUtc,
                    RefundedAtUtc = refundIntent.RefundedAtUtc
                };
            }
            else
            {
                // Execute provider initiation outside of the database transaction
                refundResponse = await _refundService.ProcessPendingRefundAsync(refundIntent.Id, cancellationToken);
            }

            return new CancelOrderResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderStatus = order.OrderStatus.ToString(),
                PaymentStatus = order.PaymentStatus.ToString(),
                IsStockRestored = order.IsStockRestored,
                StockRestoredAtUtc = order.StockRestoredAtUtc,
                CancellationReason = order.CancellationReason,
                RefundInitiated = true,
                RefundStatus = refundResponse.Status,
                RefundAmount = refundResponse.Amount,
                RefundReference = refundResponse.RefundReference
            };
        }

        // 2. Fallback check for paid online payment
        var paidPayment = order.Payments.FirstOrDefault(p => p.PaymentStatus == PaymentStatus.Paid);
        if (paidPayment != null && paidPayment.Amount > 0)
        {
            var refundResponse = await _refundService.InitiateRefundForOrderCancellationAsync(
                order.Id, paidPayment.Id, paidPayment.Amount, reason, cancellationToken);

            return new CancelOrderResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderStatus = order.OrderStatus.ToString(),
                PaymentStatus = order.PaymentStatus.ToString(),
                IsStockRestored = order.IsStockRestored,
                StockRestoredAtUtc = order.StockRestoredAtUtc,
                CancellationReason = order.CancellationReason,
                RefundInitiated = true,
                RefundStatus = refundResponse.Status,
                RefundAmount = refundResponse.Amount,
                RefundReference = refundResponse.RefundReference
            };
        }

        return new CancelOrderResponse
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            OrderStatus = order.OrderStatus.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            IsStockRestored = order.IsStockRestored,
            StockRestoredAtUtc = order.StockRestoredAtUtc,
            CancellationReason = order.CancellationReason,
            RefundInitiated = false,
            RefundStatus = null,
            RefundAmount = null,
            RefundReference = null
        };
    }

    private static string GenerateOrderNumber()
    {
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomHex = RandomNumberGenerator.GetHexString(8).ToUpperInvariant();
        return $"ORD-{datePart}-{randomHex}";
    }

    private async Task<string> GenerateUniqueOrderNumberAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var orderNumber = GenerateOrderNumber();
            var exists = await _orderRepository.OrderNumberExistsAsync(orderNumber, cancellationToken);
            if (!exists)
            {
                return orderNumber;
            }
        }
    }

    private static OrderResponse MapToOrderResponse(Order order, List<CartItem>? cartItems)
    {
        var payment = order.Payments.FirstOrDefault();

        return new OrderResponse
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            SubTotal = order.SubTotal,
            DiscountAmount = order.DiscountAmount,
            ShippingFee = order.ShippingFee,
            TotalAmount = order.TotalAmount,
            OrderStatus = order.OrderStatus.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            PaymentMethod = payment?.PaymentMethod.ToString() ?? PaymentMethod.COD.ToString(),
            ShippingRecipientName = order.ShippingRecipientName,
            ShippingPhoneNumber = order.ShippingPhoneNumber,
            ShippingAddressLine1 = order.ShippingAddressLine1,
            ShippingAddressLine2 = order.ShippingAddressLine2,
            ShippingCity = order.ShippingCity,
            ShippingState = order.ShippingState,
            ShippingPostalCode = order.ShippingPostalCode,
            ShippingCountry = order.ShippingCountry,
            OrderDateUtc = order.OrderDateUtc,
            Items = order.OrderItems.Select(oi =>
            {
                // Primary image lookup from eager-loaded Product if present, or fallback from cartItems
                var primaryImage = oi.Product?.Images?.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                                   ?? oi.Product?.Images?.FirstOrDefault()?.ImageUrl
                                   ?? cartItems?.FirstOrDefault(ci => ci.ProductId == oi.ProductId)?.Product?.Images?.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                                   ?? cartItems?.FirstOrDefault(ci => ci.ProductId == oi.ProductId)?.Product?.Images?.FirstOrDefault()?.ImageUrl;

                return new OrderItemResponse
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductTitle = oi.ProductTitleSnapshot,
                    ProductSku = oi.ProductSkuSnapshot,
                    RegularPrice = oi.RegularPrice,
                    UnitPrice = oi.UnitPrice,
                    DiscountAmount = oi.DiscountAmount,
                    Quantity = oi.Quantity,
                    LineTotal = oi.LineTotal,
                    PrimaryImageUrl = primaryImage
                };
            }).ToList()
        };
    }
}
