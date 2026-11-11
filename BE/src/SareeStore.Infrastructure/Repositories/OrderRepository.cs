using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.Data;

namespace SareeStore.Infrastructure.Repositories;

/// <summary>
/// EF Core repository implementation for Order persistence, transactional checkout, inventory deduction, and queries.
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly ApplicationDbContext _context;

    public OrderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Order> CreateOrderWithCartClearingAsync(
        Order order,
        int cartId,
        CancellationToken cancellationToken = default)
    {
        return CreateOrderWithCartClearingAsync(order, cartId, null, cancellationToken);
    }

    public async Task<Order> CreateOrderWithCartClearingAsync(
        Order order,
        int cartId,
        Func<string>? orderNumberGenerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        orderNumberGenerator ??= () => $"ORD-{DateTime.UtcNow:yyyyMMdd}-{RandomNumberGenerator.GetHexString(8).ToUpperInvariant()}";

        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                // 1. Claim customer's cart using rowlock/updlock to prevent duplicate checkout race conditions
                var cart = await _context.Carts
                    .FromSql($"SELECT * FROM Carts WITH (UPDLOCK, ROWLOCK) WHERE Id = {cartId}")
                    .FirstOrDefaultAsync(cancellationToken);

                if (cart == null)
                {
                    throw new ValidationException("Cannot checkout with an empty cart.");
                }

                // 2. Fetch current cart items inside the lock
                var cartItems = await _context.CartItems
                    .Where(ci => ci.CartId == cartId)
                    .ToListAsync(cancellationToken);

                if (cartItems.Count == 0)
                {
                    throw new ConflictException("Your cart has already been checked out or is empty.");
                }

                // 3. Load inventories for all ordered products sorted by Id to eliminate deadlock risk
                var productIds = order.OrderItems.Select(oi => oi.ProductId).Distinct().ToList();
                var inventories = await _context.Inventories
                    .Where(i => productIds.Contains(i.ProductId))
                    .OrderBy(i => i.Id)
                    .ToListAsync(cancellationToken);

                // 4. Validate and deduct stock atomically
                foreach (var orderItem in order.OrderItems)
                {
                    var inventory = inventories.FirstOrDefault(i => i.ProductId == orderItem.ProductId);
                    if (inventory == null || inventory.StockQuantity < orderItem.Quantity)
                    {
                        var available = inventory?.StockQuantity ?? 0;
                        throw new ValidationException(
                            $"Insufficient stock for product '{orderItem.ProductTitleSnapshot}'. Requested: {orderItem.Quantity}, Available: {available}.");
                    }

                    // Deduct stock atomically
                    inventory.StockQuantity -= orderItem.Quantity;
                }

                // 5. Add order entity graph and remove cart items
                await _context.Orders.AddAsync(order, cancellationToken);
                _context.CartItems.RemoveRange(cartItems);

                // 6. Save changes and commit
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return order;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                throw new ConflictException("The inventory for one or more items in your cart was updated by another customer. Please review your cart and try again.");
            }
            catch (DbUpdateException ex) when (IsOrderNumberUniqueConstraintViolation(ex) && attempt < maxAttempts)
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                order.OrderNumber = orderNumberGenerator();
                continue;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique order number after maximum retries.");
    }

    public async Task<Order?> GetByIdAndCustomerIdAsync(
        int orderId,
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Images)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerIdAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Images)
            .Include(o => o.Payments)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> OrderNumberExistsAsync(
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AnyAsync(o => o.OrderNumber == orderNumber, cancellationToken);
    }

    public async Task<Address?> GetCustomerAddressAsync(
        int addressId,
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Addresses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == customerId, cancellationToken);
    }

    public async Task<Order> CancelOrderWithStockRestorationAsync(
        int orderId,
        int? customerId,
        int? adminUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        _context.ChangeTracker.Clear();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var order = await _context.Orders
                .FromSql($"SELECT * FROM Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = {orderId}")
                .FirstOrDefaultAsync(cancellationToken);

            if (order == null)
            {
                throw new NotFoundException($"Order with ID {orderId} was not found.");
            }

            if (customerId.HasValue && order.CustomerId != customerId.Value)
            {
                throw new ForbiddenException("You do not have permission to cancel this order.");
            }

            if (order.OrderStatus == OrderStatus.Cancelled)
            {
                throw new ConflictException("This order has already been cancelled.");
            }

            // Customer cancellation: Placed or Confirmed only
            if (customerId.HasValue && order.OrderStatus != OrderStatus.Placed && order.OrderStatus != OrderStatus.Confirmed)
            {
                throw new ValidationException($"Orders in '{order.OrderStatus}' status cannot be cancelled.");
            }

            // SuperAdmin cancellation: Placed, Confirmed, Processing, or Packed only. Dispatched, Delivered, Returned strictly disallowed.
            if (adminUserId.HasValue)
            {
                if (order.OrderStatus == OrderStatus.Dispatched ||
                    order.OrderStatus == OrderStatus.Delivered ||
                    order.OrderStatus == OrderStatus.Returned)
                {
                    throw new ValidationException($"Orders in '{order.OrderStatus}' status cannot be administratively cancelled.");
                }

                if (order.OrderStatus != OrderStatus.Placed &&
                    order.OrderStatus != OrderStatus.Confirmed &&
                    order.OrderStatus != OrderStatus.Processing &&
                    order.OrderStatus != OrderStatus.Packed)
                {
                    throw new ValidationException($"Orders in '{order.OrderStatus}' status cannot be administratively cancelled.");
                }
            }

            var orderItems = await _context.OrderItems
                .Where(oi => oi.OrderId == order.Id)
                .ToListAsync(cancellationToken);

            // Restore stock exactly once
            if (!order.IsStockRestored)
            {
                var productIds = orderItems.Select(oi => oi.ProductId).Distinct().ToList();
                var inventories = await _context.Inventories
                    .Where(i => productIds.Contains(i.ProductId))
                    .OrderBy(i => i.Id)
                    .ToListAsync(cancellationToken);

                foreach (var item in orderItems)
                {
                    var inventory = inventories.FirstOrDefault(i => i.ProductId == item.ProductId);
                    if (inventory != null)
                    {
                        inventory.StockQuantity += item.Quantity;
                    }
                }

                order.IsStockRestored = true;
                order.StockRestoredAtUtc = DateTime.UtcNow;
            }

            var previousStatus = order.OrderStatus;
            order.OrderStatus = OrderStatus.Cancelled;
            order.CancellationReason = reason;
            order.CancelledDateUtc = DateTime.UtcNow;

            var history = new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = previousStatus,
                ToStatus = OrderStatus.Cancelled,
                Remarks = customerId.HasValue
                    ? $"Order cancelled by customer. Reason: {reason}"
                    : $"Order cancelled by administrator. Reason: {reason}",
                ProcessedByUserId = customerId ?? adminUserId,
                ChangedAtUtc = DateTime.UtcNow
            };

            await _context.OrderStatusHistories.AddAsync(history, cancellationToken);

            var payments = await _context.Payments
                .FromSql($"SELECT * FROM Payments WITH (UPDLOCK, ROWLOCK) WHERE OrderId = {order.Id}")
                .ToListAsync(cancellationToken);
            order.Payments = payments;
            order.OrderItems = orderItems;

            // Durable Refund Intent: If order has paid payment, create durable Pending Refund record inside transaction
            var paidPayment = payments.FirstOrDefault(p => p.PaymentStatus == PaymentStatus.Paid && p.Amount > 0);
            if (paidPayment != null)
            {
                var idempotencyKey = $"REF-ORD{order.Id}-PAY{paidPayment.Id}-CANCEL";
                var existingRefunds = await _context.Refunds
                    .Where(r => r.PaymentId == paidPayment.Id)
                    .ToListAsync(cancellationToken);

                var existingRefund = existingRefunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
                if (existingRefund == null)
                {
                    var successfulSum = existingRefunds.Where(r => r.Status == RefundStatus.Success).Sum(r => r.Amount);
                    var inFlightSum = existingRefunds.Where(r => r.Status == RefundStatus.Pending || r.Status == RefundStatus.Processing).Sum(r => r.Amount);
                    var remainingBalance = paidPayment.Amount - (successfulSum + inFlightSum);

                    if (remainingBalance > 0)
                    {
                        var refund = new Refund
                        {
                            OrderId = order.Id,
                            PaymentId = paidPayment.Id,
                            Amount = Math.Min(paidPayment.Amount, remainingBalance),
                            Currency = "INR",
                            Status = RefundStatus.Pending,
                            Reason = reason,
                            IdempotencyKey = idempotencyKey,
                            CreatedAtUtc = DateTime.UtcNow
                        };

                        await _context.Refunds.AddAsync(refund, cancellationToken);
                        order.Refunds.Add(refund);
                    }
                }
                else
                {
                    order.Refunds.Add(existingRefund);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return order;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    private static bool IsOrderNumberUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            return (sqlEx.Number == 2601 || sqlEx.Number == 2627) &&
                   sqlEx.Message.Contains("IX_Orders_OrderNumber", StringComparison.OrdinalIgnoreCase);
        }

        return ex.InnerException?.Message.Contains("IX_Orders_OrderNumber", StringComparison.OrdinalIgnoreCase) == true
            || ex.Message.Contains("IX_Orders_OrderNumber", StringComparison.OrdinalIgnoreCase);
    }
}
