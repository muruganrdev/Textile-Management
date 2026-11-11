using NSubstitute;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Orders;
using SareeStore.Application.Services;
using SareeStore.Application.Services.Interfaces;
using SareeStore.Application.Validators;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;

namespace SareeStore.UnitTests.Services;

public class OrderServiceTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly ICartRepository _cartRepository = Substitute.For<ICartRepository>();
    private readonly IRefundService _refundService = Substitute.For<IRefundService>();
    private readonly CreateOrderRequestValidator _validator = new();
    private readonly CancelOrderRequestValidator _cancelValidator = new();
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _orderService = new OrderService(
            _orderRepository,
            _cartRepository,
            _validator,
            _cancelValidator,
            _refundService);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenCartIsEmpty_ShouldThrowValidationException()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 10, PaymentMethod = PaymentMethod.COD };

        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(new Cart { Id = 5, UserId = customerId, Items = new List<CartItem>() });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _orderService.CreateOrderAsync(customerId, request));

        Assert.Contains("Cannot checkout with an empty cart", ex.Message);
        await _orderRepository.DidNotReceive().CreateOrderWithCartClearingAsync(Arg.Any<Order>(), Arg.Any<int>(), Arg.Any<Func<string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderAsync_WhenCartDoesNotExist_ShouldThrowValidationException()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 10, PaymentMethod = PaymentMethod.COD };

        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns((Cart?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _orderService.CreateOrderAsync(customerId, request));

        Assert.Contains("Cannot checkout with an empty cart", ex.Message);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenAddressDoesNotBelongToCustomer_ShouldThrowValidationException()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 999, PaymentMethod = PaymentMethod.COD };

        var cart = CreateTestCart(customerId, 1, 10, 5000m, null);
        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(cart);

        _orderRepository.GetCustomerAddressAsync(999, customerId, Arg.Any<CancellationToken>())
            .Returns((Address?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _orderService.CreateOrderAsync(customerId, request));

        Assert.Contains("delivery address does not exist or does not belong", ex.Message);
        await _orderRepository.DidNotReceive().CreateOrderWithCartClearingAsync(Arg.Any<Order>(), Arg.Any<int>(), Arg.Any<Func<string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderAsync_WhenProductIsInactive_ShouldThrowValidationException()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 10, PaymentMethod = PaymentMethod.COD };

        var cart = CreateTestCart(customerId, 1, 10, 5000m, null);
        cart.Items.First().Product.IsActive = false;

        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        _orderRepository.GetCustomerAddressAsync(10, customerId, Arg.Any<CancellationToken>())
            .Returns(CreateTestAddress(10, customerId));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _orderService.CreateOrderAsync(customerId, request));

        Assert.Contains("inactive and cannot be ordered", ex.Message);
        await _orderRepository.DidNotReceive().CreateOrderWithCartClearingAsync(Arg.Any<Order>(), Arg.Any<int>(), Arg.Any<Func<string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderAsync_WhenStockIsInsufficient_ShouldThrowValidationException()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 10, PaymentMethod = PaymentMethod.COD };

        // Cart quantity 3, stock only 2
        var cart = CreateTestCart(customerId, 3, 2, 5000m, null);

        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        _orderRepository.GetCustomerAddressAsync(10, customerId, Arg.Any<CancellationToken>())
            .Returns(CreateTestAddress(10, customerId));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _orderService.CreateOrderAsync(customerId, request));

        Assert.Contains("Insufficient stock", ex.Message);
        await _orderRepository.DidNotReceive().CreateOrderWithCartClearingAsync(Arg.Any<Order>(), Arg.Any<int>(), Arg.Any<Func<string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderAsync_WhenProductHasRegularPrice_ShouldCalculateTotalsCorrectly()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 10, PaymentMethod = PaymentMethod.COD };

        var cart = CreateTestCart(customerId, quantity: 2, stock: 10, price: 4000m, discountPrice: null);

        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        _orderRepository.GetCustomerAddressAsync(10, customerId, Arg.Any<CancellationToken>())
            .Returns(CreateTestAddress(10, customerId));
        _orderRepository.OrderNumberExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _orderRepository.CreateOrderWithCartClearingAsync(Arg.Any<Order>(), cart.Id, Arg.Any<Func<string>?>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<Order>());

        // Act
        var response = await _orderService.CreateOrderAsync(customerId, request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(8000m, response.SubTotal); // 4000 * 2
        Assert.Equal(0.00m, response.DiscountAmount);
        Assert.Equal(0.00m, response.ShippingFee);
        Assert.Equal(8000m, response.TotalAmount);
        Assert.Single(response.Items);
        Assert.Equal(4000m, response.Items[0].RegularPrice);
        Assert.Equal(4000m, response.Items[0].UnitPrice);
        Assert.Equal(0.00m, response.Items[0].DiscountAmount);
        Assert.Equal(8000m, response.Items[0].LineTotal);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenProductHasDiscountPrice_ShouldUseDiscountPriceForCalculation()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 10, PaymentMethod = PaymentMethod.UPI };

        // Base price 6000, discount price 4800, qty 2
        var cart = CreateTestCart(customerId, quantity: 2, stock: 10, price: 6000m, discountPrice: 4800m);

        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        _orderRepository.GetCustomerAddressAsync(10, customerId, Arg.Any<CancellationToken>())
            .Returns(CreateTestAddress(10, customerId));
        _orderRepository.OrderNumberExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _orderRepository.CreateOrderWithCartClearingAsync(Arg.Any<Order>(), cart.Id, Arg.Any<Func<string>>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<Order>());

        // Act
        var response = await _orderService.CreateOrderAsync(customerId, request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(9600m, response.SubTotal); // 4800 * 2
        Assert.Equal(9600m, response.TotalAmount);
        Assert.Equal(6000m, response.Items[0].RegularPrice);
        Assert.Equal(4800m, response.Items[0].UnitPrice);
        Assert.Equal(1200m, response.Items[0].DiscountAmount);
        Assert.Equal(9600m, response.Items[0].LineTotal);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenValid_ShouldPreserveHistoricalSnapshotsAndCallCartClearing()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 10, PaymentMethod = PaymentMethod.CreditCard };

        var cart = CreateTestCart(customerId, quantity: 1, stock: 5, price: 12000m, discountPrice: 10500m);
        var address = CreateTestAddress(10, customerId);

        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        _orderRepository.GetCustomerAddressAsync(10, customerId, Arg.Any<CancellationToken>())
            .Returns(address);
        _orderRepository.OrderNumberExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        Order? capturedOrder = null;
        int capturedCartId = 0;
        _orderRepository.CreateOrderWithCartClearingAsync(
            Arg.Do<Order>(o => capturedOrder = o),
            Arg.Do<int>(cId => capturedCartId = cId),
            Arg.Any<Func<string>>(),
            Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<Order>());

        // Act
        var response = await _orderService.CreateOrderAsync(customerId, request);

        // Assert
        Assert.NotNull(capturedOrder);
        Assert.Equal(cart.Id, capturedCartId);
        Assert.StartsWith("ORD-", capturedOrder.OrderNumber);
        Assert.Equal(customerId, capturedOrder.CustomerId);
        Assert.Equal(OrderStatus.Placed, capturedOrder.OrderStatus);
        Assert.Equal(PaymentStatus.Pending, capturedOrder.PaymentStatus);

        // Address snapshots frozen
        Assert.Equal(address.RecipientName, capturedOrder.ShippingRecipientName);
        Assert.Equal(address.PhoneNumber, capturedOrder.ShippingPhoneNumber);
        Assert.Equal(address.AddressLine1, capturedOrder.ShippingAddressLine1);
        Assert.Equal(address.City, capturedOrder.ShippingCity);
        Assert.Equal(address.State, capturedOrder.ShippingState);
        Assert.Equal(address.PostalCode, capturedOrder.ShippingPostalCode);

        // Order item snapshots frozen
        var item = capturedOrder.OrderItems.First();
        Assert.Equal("Kanchipuram Silk Saree", item.ProductTitleSnapshot);
        Assert.Equal("SKU-TEST-100", item.ProductSkuSnapshot);
        Assert.Equal(12000m, item.RegularPrice);
        Assert.Equal(10500m, item.UnitPrice);
        Assert.Equal(1500m, item.DiscountAmount);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(10500m, item.LineTotal);

        // Initial status history with FromStatus null
        Assert.Single(capturedOrder.StatusHistories);
        var history = capturedOrder.StatusHistories.First();
        Assert.Null(history.FromStatus);
        Assert.Equal(OrderStatus.Placed, history.ToStatus);
        Assert.Null(history.ProcessedByUserId);

        // Initial payment record
        Assert.Single(capturedOrder.Payments);
        var payment = capturedOrder.Payments.First();
        Assert.Equal(PaymentMethod.CreditCard, payment.PaymentMethod);
        Assert.Equal(PaymentStatus.Pending, payment.PaymentStatus);
        Assert.Equal(10500m, payment.Amount);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenRepositoryThrows_CartRemainsIntactAndErrorPropagates()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 10, PaymentMethod = PaymentMethod.COD };

        var cart = CreateTestCart(customerId, quantity: 1, stock: 5, price: 5000m, discountPrice: null);

        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        _orderRepository.GetCustomerAddressAsync(10, customerId, Arg.Any<CancellationToken>())
            .Returns(CreateTestAddress(10, customerId));
        _orderRepository.OrderNumberExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _orderRepository.CreateOrderWithCartClearingAsync(Arg.Any<Order>(), Arg.Any<int>(), Arg.Any<Func<string>>(), Arg.Any<CancellationToken>())
            .Returns<Order>(_ => throw new InvalidOperationException("Database deadlock simulation"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.CreateOrderAsync(customerId, request));

        Assert.Equal("Database deadlock simulation", ex.Message);
        // In-memory cart items were not modified by service
        Assert.Single(cart.Items);
    }

    [Fact]
    public async Task GetOrderByIdAsync_WhenOrderNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _orderRepository.GetByIdAndCustomerIdAsync(100, 1, Arg.Any<CancellationToken>())
            .Returns((Order?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _orderService.GetOrderByIdAsync(100, 1));
    }

    [Fact]
    public async Task CreateOrderAsync_WhenInventoryConcurrencyConflictOccurs_ShouldThrowConflictException()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 10, PaymentMethod = PaymentMethod.COD };
        var cart = CreateTestCart(customerId, quantity: 1, stock: 1, price: 5000m, discountPrice: null);

        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        _orderRepository.GetCustomerAddressAsync(10, customerId, Arg.Any<CancellationToken>())
            .Returns(CreateTestAddress(10, customerId));

        _orderRepository.CreateOrderWithCartClearingAsync(Arg.Any<Order>(), cart.Id, Arg.Any<Func<string>?>(), Arg.Any<CancellationToken>())
            .Returns<Order>(_ => throw new ConflictException("The inventory for one or more items in your cart was updated by another customer."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _orderService.CreateOrderAsync(customerId, request));

        Assert.Contains("inventory for one or more items", ex.Message);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenCartAlreadyClaimedByConcurrentCheckout_ShouldThrowConflictException()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 10, PaymentMethod = PaymentMethod.COD };
        var cart = CreateTestCart(customerId, quantity: 1, stock: 5, price: 5000m, discountPrice: null);

        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        _orderRepository.GetCustomerAddressAsync(10, customerId, Arg.Any<CancellationToken>())
            .Returns(CreateTestAddress(10, customerId));

        _orderRepository.CreateOrderWithCartClearingAsync(Arg.Any<Order>(), cart.Id, Arg.Any<Func<string>?>(), Arg.Any<CancellationToken>())
            .Returns<Order>(_ => throw new ConflictException("Your cart has already been checked out or is empty."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _orderService.CreateOrderAsync(customerId, request));

        Assert.Contains("already been checked out", ex.Message);
    }

    [Fact]
    public async Task CreateOrderAsync_PriceSnapshotFormulas_ShouldCalculateExactAmounts()
    {
        // Arrange
        const int customerId = 1;
        var request = new CreateOrderRequest { ShippingAddressId = 10, PaymentMethod = PaymentMethod.COD };
        // Regular price: 15000, discount price: 12500 -> discount per unit = 2500, qty: 3 -> lineTotal = 37500
        var cart = CreateTestCart(customerId, quantity: 3, stock: 10, price: 15000m, discountPrice: 12500m);

        _cartRepository.GetByUserIdWithDetailsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(cart);
        _orderRepository.GetCustomerAddressAsync(10, customerId, Arg.Any<CancellationToken>())
            .Returns(CreateTestAddress(10, customerId));

        Order? capturedOrder = null;
        _orderRepository.CreateOrderWithCartClearingAsync(
            Arg.Do<Order>(o => capturedOrder = o),
            Arg.Any<int>(),
            Arg.Any<Func<string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<Order>());

        // Act
        var response = await _orderService.CreateOrderAsync(customerId, request);

        // Assert
        Assert.NotNull(capturedOrder);
        var item = capturedOrder.OrderItems.First();
        Assert.Equal(15000m, item.RegularPrice);
        Assert.Equal(12500m, item.UnitPrice);
        Assert.Equal(2500m, item.DiscountAmount); // RegularPrice - UnitPrice
        Assert.Equal(3, item.Quantity);
        Assert.Equal(37500m, item.LineTotal);     // UnitPrice * Quantity

        Assert.Equal(37500m, response.SubTotal);
        Assert.Equal(37500m, response.TotalAmount);
        Assert.Equal(15000m, response.Items[0].RegularPrice);
        Assert.Equal(12500m, response.Items[0].UnitPrice);
        Assert.Equal(2500m, response.Items[0].DiscountAmount);
        Assert.Equal(37500m, response.Items[0].LineTotal);
    }

    private static Cart CreateTestCart(int customerId, int quantity, int stock, decimal price, decimal? discountPrice)
    {
        var product = new Product
        {
            Id = 100,
            Title = "Kanchipuram Silk Saree",
            SKU = "SKU-TEST-100",
            Price = price,
            DiscountPrice = discountPrice,
            IsActive = true,
            Inventory = new Inventory
            {
                ProductId = 100,
                StockQuantity = stock
            },
            Images = new List<ProductImage>
            {
                new ProductImage { ImageUrl = "https://images.sareestore.com/saree1.jpg", IsPrimary = true }
            }
        };

        return new Cart
        {
            Id = 50,
            UserId = customerId,
            Items = new List<CartItem>
            {
                new CartItem
                {
                    Id = 201,
                    CartId = 50,
                    ProductId = product.Id,
                    Quantity = quantity,
                    Product = product
                }
            }
        };
    }

    private static Address CreateTestAddress(int addressId, int customerId)
    {
        return new Address
        {
            Id = addressId,
            UserId = customerId,
            RecipientName = "Murugan R",
            PhoneNumber = "+919876543210",
            AddressLine1 = "12 Temple Street",
            City = "Chennai",
            State = "Tamil Nadu",
            PostalCode = "600001",
            Country = "India"
        };
    }

    [Fact]
    public async Task CancelOrderAsync_WhenReasonIsEmpty_ThrowsValidationException()
    {
        var request = new CancelOrderRequest { Reason = "" };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _orderService.CancelOrderAsync(1, 10, request));

        Assert.True(ex.Errors.ContainsKey("Reason"));
    }

    [Fact]
    public async Task CancelOrderAsync_WhenReasonExceeds500Chars_ThrowsValidationException()
    {
        var request = new CancelOrderRequest { Reason = new string('x', 501) };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _orderService.CancelOrderAsync(1, 10, request));

        Assert.True(ex.Errors.ContainsKey("Reason"));
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsPlacedAndUnpaid_CancelsWithoutRefund()
    {
        const int customerId = 5;
        const int orderId = 20;
        var request = new CancelOrderRequest { Reason = "Ordered by mistake" };

        var cancelledOrder = new Order
        {
            Id = orderId,
            CustomerId = customerId,
            OrderNumber = "ORD-20261104-TEST",
            OrderStatus = OrderStatus.Cancelled,
            PaymentStatus = PaymentStatus.Pending,
            IsStockRestored = true,
            StockRestoredAtUtc = DateTime.UtcNow,
            CancellationReason = request.Reason,
            Payments = new List<Payment>
            {
                new() { Id = 1, OrderId = orderId, PaymentStatus = PaymentStatus.Pending, Amount = 5000m }
            }
        };

        _orderRepository.CancelOrderWithStockRestorationAsync(orderId, customerId, null, request.Reason, Arg.Any<CancellationToken>())
            .Returns(cancelledOrder);

        var result = await _orderService.CancelOrderAsync(customerId, orderId, request);

        Assert.Equal(orderId, result.OrderId);
        Assert.Equal("Cancelled", result.OrderStatus);
        Assert.True(result.IsStockRestored);
        Assert.False(result.RefundInitiated);
        Assert.Null(result.RefundStatus);
        await _refundService.DidNotReceiveWithAnyArgs().InitiateRefundForOrderCancellationAsync(default, default, default, default!);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsConfirmedAndPaid_InitiatesAutomatedRefund()
    {
        const int customerId = 5;
        const int orderId = 20;
        const int paymentId = 88;
        const decimal amount = 8500m;
        var request = new CancelOrderRequest { Reason = "Found better saree elsewhere" };

        var cancelledOrder = new Order
        {
            Id = orderId,
            CustomerId = customerId,
            OrderNumber = "ORD-20261104-PAID",
            OrderStatus = OrderStatus.Cancelled,
            PaymentStatus = PaymentStatus.Paid,
            IsStockRestored = true,
            StockRestoredAtUtc = DateTime.UtcNow,
            CancellationReason = request.Reason,
            Payments = new List<Payment>
            {
                new() { Id = paymentId, OrderId = orderId, PaymentStatus = PaymentStatus.Paid, Amount = amount, TransactionId = "SIM-TXN-123" }
            }
        };

        _orderRepository.CancelOrderWithStockRestorationAsync(orderId, customerId, null, request.Reason, Arg.Any<CancellationToken>())
            .Returns(cancelledOrder);

        _refundService.InitiateRefundForOrderCancellationAsync(orderId, paymentId, amount, request.Reason, Arg.Any<CancellationToken>())
            .Returns(new RefundResponse
            {
                Id = 1,
                OrderId = orderId,
                PaymentId = paymentId,
                Amount = amount,
                Status = "Success",
                RefundReference = "SIM-REF-123"
            });

        var result = await _orderService.CancelOrderAsync(customerId, orderId, request);

        Assert.Equal(orderId, result.OrderId);
        Assert.Equal("Cancelled", result.OrderStatus);
        Assert.True(result.IsStockRestored);
        Assert.True(result.RefundInitiated);
        Assert.Equal("Success", result.RefundStatus);
        Assert.Equal(amount, result.RefundAmount);
        Assert.Equal("SIM-REF-123", result.RefundReference);

        await _refundService.Received(1).InitiateRefundForOrderCancellationAsync(orderId, paymentId, amount, request.Reason, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsCOD_DoesNotTriggerRefund()
    {
        const int customerId = 5;
        const int orderId = 20;
        var request = new CancelOrderRequest { Reason = "Cash not available" };

        var cancelledOrder = new Order
        {
            Id = orderId,
            CustomerId = customerId,
            OrderNumber = "ORD-20261104-COD",
            OrderStatus = OrderStatus.Cancelled,
            PaymentStatus = PaymentStatus.Pending,
            IsStockRestored = true,
            StockRestoredAtUtc = DateTime.UtcNow,
            CancellationReason = request.Reason,
            Payments = new List<Payment>
            {
                new() { Id = 1, OrderId = orderId, PaymentMethod = PaymentMethod.COD, PaymentStatus = PaymentStatus.Pending, Amount = 4000m }
            }
        };

        _orderRepository.CancelOrderWithStockRestorationAsync(orderId, customerId, null, request.Reason, Arg.Any<CancellationToken>())
            .Returns(cancelledOrder);

        var result = await _orderService.CancelOrderAsync(customerId, orderId, request);

        Assert.False(result.RefundInitiated);
        await _refundService.DidNotReceiveWithAnyArgs().InitiateRefundForOrderCancellationAsync(default, default, default, default!);
    }
}
