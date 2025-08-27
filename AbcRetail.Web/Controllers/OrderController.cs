using AbcRetail.Core.Interfaces;
using AbcRetail.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Web.Controllers;

using Microsoft.AspNetCore.Authorization;

[Authorize]
public class OrderController : Controller
{
    private readonly IOrderRepository _orders;
    private readonly IOrderQueueService _orderQueue;
    private readonly IProductRepository _products;
    private readonly ICustomerRepository _customers;
    private readonly IAppLogger _appLogger;

    public OrderController(IOrderRepository orders, IOrderQueueService orderQueue, IProductRepository products, ICustomerRepository customers, IAppLogger appLogger)
    {
        _orders = orders;
        _orderQueue = orderQueue;
        _products = products;
        _customers = customers;
        _appLogger = appLogger;
    }

    [HttpGet]
    [Authorize(Roles = "Customer")] // only customers can create orders
    public async Task<IActionResult> Create()
    {
        ViewBag.Products = await _products.ListAsync();
        var order = new Order();
        if (User.IsInRole("Customer"))
        {
            order.CustomerId = User.Identity?.Name ?? string.Empty; // auto-bind for customer
        }
        return View(order);
    }

    [HttpPost]
    [Authorize(Roles = "Customer")] // only customers can submit
    public async Task<IActionResult> Create(string customerId, string[] productId, int[] quantity)
    {
    if (!User.IsInRole("Customer")) return Forbid(); // extra safety
    await _appLogger.LogInfoAsync($"OrderController.Create START userRole={(User.IsInRole("Admin")?"Admin":"Customer")} postedCustomerId={customerId}");
        // For Customers, ignore any posted customerId and enforce their own identity
        if (User.IsInRole("Customer"))
        {
            customerId = User.Identity?.Name ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(customerId))
        {
            ModelState.AddModelError("customerId", "Customer ID is required");
        }
        if (productId == null || quantity == null || productId.Length == 0)
        {
            ModelState.AddModelError("productId", "At least one product is required");
        }
        if (productId != null && quantity != null && productId.Length != quantity.Length)
        {
            ModelState.AddModelError("lines", "Product and quantity counts must match");
        }
        if (!ModelState.IsValid)
        {
            ViewBag.Products = await _products.ListAsync();
            return View(new Order { CustomerId = customerId });
        }

        // Create-if-not-exists for Customer
        var existingCustomer = await _customers.GetAsync(customerId);
        if (existingCustomer == null)
        {
            await _customers.UpsertAsync(new Customer
            {
                CustomerId = customerId,
                Name = $"Customer {customerId}",
                Email = $"{customerId}@example.local",
                ShippingAddress = "Unknown"
            });
        }
        var order = new Order { CustomerId = customerId };
    order.History.Add(new OrderStatusEvent { Status = "Queued", Notes = "Order created" });
        for (int i = 0; i < productId!.Length; i++)
        {
            var prod = await _products.GetAsync(productId[i]);
            if (prod == null) continue;
            var qty = quantity![i];
            if (qty <= 0)
            {
                ModelState.AddModelError("quantity", "Quantity must be positive");
                continue;
            }
            if (prod.Quantity < qty)
            {
                ModelState.AddModelError("quantity", $"Insufficient stock for product {prod.Name}. Available: {prod.Quantity}");
                continue;
            }
            order.Lines.Add(new OrderLine { ProductId = prod.ProductId, Quantity = qty, UnitPrice = prod.Price });
        }
        if (!ModelState.IsValid || order.Lines.Count == 0)
        {
            ViewBag.Products = await _products.ListAsync();
            return View(new Order { CustomerId = customerId });
        }
        // Persist order first
    await _orders.UpsertAsync(order);
    var total = order.Lines.Sum(l=> l.UnitPrice * l.Quantity);
    await _appLogger.LogInfoAsync($"OrderController.Create SAVED orderId={order.OrderId} customerId={order.CustomerId} lines={order.Lines.Count} total={total}");
        // Adjust inventory after saving each line
        foreach (var line in order.Lines)
        {
            await _products.AdjustQuantityAsync(line.ProductId, -line.Quantity);
        }
        await _orderQueue.EnqueueNewOrderAsync(order); // FR005
    await _appLogger.LogInfoAsync($"OrderController.Create QUEUED orderId={order.OrderId}");
        return RedirectToAction("Status", new { id = order.OrderId });
    }

    public async Task<IActionResult> Status(string id)
    {
        var order = await _orders.GetAsync(id);
        if (order == null) return NotFound();
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")] // only admin can change status
    public async Task<IActionResult> UpdateStatus(string id, string newStatus, string? notes)
    {
        var order = await _orders.GetAsync(id);
        if (order == null) return NotFound();
        var previous = order.Status;
        var allowed = new[] { "Queued", "Processing", "Completed", "Canceled" };
        if (!allowed.Contains(newStatus))
        {
            TempData["StatusError"] = "Invalid status";
            return RedirectToAction("Status", new { id });
        }
        if (!string.Equals(order.Status, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            order.Status = newStatus;
            order.History.Add(new OrderStatusEvent { Status = newStatus, Notes = string.IsNullOrWhiteSpace(notes)?null:notes.Trim() });
            await _orders.UpsertAsync(order);
            await _appLogger.LogInfoAsync($"OrderController.UpdateStatus orderId={order.OrderId} prev={previous} new={newStatus} notes={(notes??string.Empty).Replace('\n',' ').Replace('\r',' ')}");
        }
        return RedirectToAction("Status", new { id });
    }

    [HttpGet]
    public async Task<IActionResult> StatusData(string id)
    {
        var order = await _orders.GetAsync(id);
        if (order == null) return NotFound();
        return Json(new { order.Status, history = order.History });
    }

    [Authorize(Roles = "Customer,Admin")] // must login to track list
    [HttpGet]
    public async Task<IActionResult> Track()
    {
        ViewBag.OrderId = string.Empty;
        ViewBag.CustomerEmail = string.Empty;
        IEnumerable<Order> orders;
        if (User.IsInRole("Admin"))
        {
            orders = await _orders.ListAsync(50);
        }
        else
        {
            // Customer sees only their orders; username assumed customerId
            var customerId = User.Identity?.Name ?? string.Empty;
            orders = await _orders.ListByCustomerAsync(customerId, 50);
        }
        return View(orders.ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Track(string orderId, string customerQuery, bool viewList = false)
    {
        ViewBag.OrderId = orderId;
        ViewBag.CustomerQuery = User.IsInRole("Admin") ? customerQuery : string.Empty;
        // If user is performing a customer search only, ensure any spurious required error for orderId is cleared
        if (string.IsNullOrWhiteSpace(orderId))
        {
            ModelState.Remove("orderId");
        }
        // Shortcut: if specific order ID filled, redirect
        if (!string.IsNullOrWhiteSpace(orderId))
        {
            var o = await _orders.GetAsync(orderId.Trim());
            if (o != null) return RedirectToAction("Status", new { id = orderId.Trim() });
            ModelState.AddModelError("orderId", "Order not found");
        }
        IEnumerable<Order> orders;
        if (User.IsInRole("Admin"))
        {
            orders = await _orders.ListAsync(100);
            if (!string.IsNullOrWhiteSpace(customerQuery))
            {
                customerQuery = customerQuery.Trim();
                // Search by email and name; union the resulting customer IDs
                var byEmail = await _customers.SearchByEmailAsync(customerQuery, 50);
                var byName = await _customers.SearchByNameAsync(customerQuery, 50);
                var idSet = byEmail.Concat(byName).Select(c => c.CustomerId).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Also allow direct prefix match on CustomerId (which for registered users is email; for system-created may be an ID)
                orders = orders.Where(o =>
                    idSet.Contains(o.CustomerId) ||
                    o.CustomerId.StartsWith(customerQuery, StringComparison.OrdinalIgnoreCase) ||
                    // If query looks like an email local-part, allow match before '@'
                    (!customerQuery.Contains('@') && o.CustomerId.Contains('@') && o.CustomerId.Split('@')[0].StartsWith(customerQuery, StringComparison.OrdinalIgnoreCase))
                );
            }
        }
        else
        {
            var customerId = User.Identity?.Name ?? string.Empty;
            orders = await _orders.ListByCustomerAsync(customerId, 100);
        }
        return View(orders.ToList());
    }
}
