using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.DTOs.Orders;
using SareeStore.Application.DTOs.Payment;
using SareeStore.Application.Services.Interfaces;

namespace SareeStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json", "application/problem+json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly IRefundService _refundService;

    public OrdersController(
        IOrderService orderService,
        IPaymentService paymentService,
        IRefundService refundService)
    {
        _orderService = orderService;
        _paymentService = paymentService;
        _refundService = refundService;
    }

    /// <summary>
    /// Creates a new order from the customer's active shopping cart.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Customer")]
    [EndpointSummary("Checkout and create order")]
    [EndpointDescription("Creates a new customer order from items currently in their shopping cart. Validates available inventory and selected address, freezes item and address snapshots, and clears the cart.")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var order = await _orderService.CreateOrderAsync(customerId, request, cancellationToken);
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    /// <summary>
    /// Retrieves all orders placed by the authenticated customer.
    /// </summary>
    [HttpGet("my-orders")]
    [Authorize(Roles = "Customer")]
    [EndpointSummary("List customer orders")]
    [EndpointDescription("Returns the complete order history for the authenticated customer ordered by date descending.")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> GetMyOrders(CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var orders = await _orderService.GetMyOrdersAsync(customerId, cancellationToken);
        return Ok(orders);
    }

    /// <summary>
    /// Retrieves details of a specific order belonging to the authenticated customer.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Customer")]
    [EndpointSummary("Get customer order by ID")]
    [EndpointDescription("Retrieves an order by its ID. Access is strictly isolated to the customer who placed the order.")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetOrderById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var order = await _orderService.GetOrderByIdAsync(id, customerId, cancellationToken);
        return Ok(order);
    }

    /// <summary>
    /// Initiates an online payment attempt for an existing order.
    /// </summary>
    [HttpPost("{id:int}/payments")]
    [Authorize(Roles = "Customer")]
    [EndpointSummary("Initiate online payment")]
    [EndpointDescription("Creates a new in-flight payment attempt for an order in Placed status and returns provider reference.")]
    [ProducesResponseType(typeof(PaymentInitiateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentInitiateResponse>> InitiatePayment(
        int id,
        [FromBody] InitiatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var response = await _paymentService.InitiatePaymentAsync(customerId, id, request, cancellationToken);
        return Created($"/api/orders/{id}/payments", response);
    }

    /// <summary>
    /// Retrieves all payment attempts and audit history for an order.
    /// </summary>
    [HttpGet("{id:int}/payments")]
    [Authorize(Roles = "Customer,Employee,SuperAdmin")]
    [EndpointSummary("Get order payment attempts")]
    [EndpointDescription("Returns list of all payment attempts for the specified order. Customers can only view their own orders.")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PaymentHistoryResponse>>> GetOrderPayments(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var isEmployeeOrAdmin = User.IsInRole("Employee") || User.IsInRole("SuperAdmin");
        var payments = await _paymentService.GetOrderPaymentsAsync(userId, id, isEmployeeOrAdmin, cancellationToken);
        return Ok(payments);
    }

    /// <summary>
    /// Cancels an eligible customer order and restores inventory stock.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "Customer")]
    [EndpointSummary("Cancel customer order")]
    [EndpointDescription("Cancels an order in Placed or Confirmed status, restores inventory stock, and automatically initiates a refund if paid online.")]
    [ProducesResponseType(typeof(CancelOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancelOrderResponse>> CancelOrder(
        int id,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var response = await _orderService.CancelOrderAsync(customerId, id, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Administrative cancellation operation for exceptional cases.
    /// </summary>
    [HttpPost("{id:int}/admin-cancel")]
    [Authorize(Roles = "SuperAdmin")]
    [EndpointSummary("Administrative cancel order")]
    [EndpointDescription("Exceptional administrative cancellation of an order. Restores inventory stock and records the acting administrator.")]
    [ProducesResponseType(typeof(CancelOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancelOrderResponse>> AdminCancelOrder(
        int id,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = GetCurrentUserId();
        var response = await _orderService.AdminCancelOrderAsync(adminUserId, id, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Returns all refund records for the specified order.
    /// </summary>
    [HttpGet("{id:int}/refunds")]
    [Authorize(Roles = "Customer,Employee,SuperAdmin")]
    [EndpointSummary("Get order refunds")]
    [EndpointDescription("Returns list of all refund operations for the specified order.")]
    [ProducesResponseType(typeof(IReadOnlyList<RefundResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RefundResponse>>> GetOrderRefunds(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var isEmployeeOrAdmin = User.IsInRole("Employee") || User.IsInRole("SuperAdmin");
        var refunds = await _refundService.GetOrderRefundsAsync(userId, id, isEmployeeOrAdmin, cancellationToken);
        return Ok(refunds);
    }

    /// <summary>
    /// Retries or reconciles an in-flight, timed-out, or failed refund operation.
    /// </summary>
    [HttpPost("{id:int}/refunds/{refundId:int}/reconcile")]
    [Authorize(Roles = "Customer,SuperAdmin")]
    [EndpointSummary("Retry or reconcile refund")]
    [EndpointDescription("Reconciles an in-flight refund or retries an interrupted refund operation. Customers may only reconcile their own orders.")]
    [ProducesResponseType(typeof(RefundResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RefundResponse>> ReconcileRefund(
        int id,
        int refundId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var response = await _refundService.RetryOrReconcileRefundAsync(
            isSuperAdmin ? null : userId,
            refundId,
            cancellationToken);
        return Ok(response);
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
