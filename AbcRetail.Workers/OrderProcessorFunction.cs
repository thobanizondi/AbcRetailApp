using AbcRetail.Core.Interfaces;
using AbcRetail.Core.Models;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AbcRetail.Workers;

public class OrderProcessorFunction
{
    private readonly IOrderRepository _orders;
    private readonly IInventoryQueueService _inventoryQueue;
    private readonly IAppLogger _appLogger;
    private readonly ILogger<OrderProcessorFunction> _logger;

    public OrderProcessorFunction(IOrderRepository orders, IInventoryQueueService inventoryQueue, IAppLogger appLogger, ILogger<OrderProcessorFunction> logger)
    {
        _orders = orders;
        _inventoryQueue = inventoryQueue;
        _appLogger = appLogger;
        _logger = logger;
    }

    [Function("OrderProcessorFunction")]
    public async Task Run([QueueTrigger("new-orders")] string message, FunctionContext context)
    {
        var log = context.GetLogger("OrderProcessor"); // functions logger (console)
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(message));
            var payload = JsonSerializer.Deserialize<OrderPayload>(json);
            if (payload == null)
            {
                await _appLogger.LogErrorAsync("OrderProcessor: Payload deserialization returned null");
                return;
            }
            await _appLogger.LogInfoAsync($"OrderProcessor: Received order message OrderId={payload.OrderId}");
            var order = await _orders.GetAsync(payload.OrderId);
            if (order == null)
            {
                await _appLogger.LogErrorAsync($"OrderProcessor: Order not found OrderId={payload.OrderId}");
                return;
            }
            order.Status = "Processing";
            order.History.Add(new OrderStatusEvent { Status = "Processing", Notes = "Processing started" });
            await _orders.UpsertAsync(order);
            await _appLogger.LogInfoAsync($"OrderProcessor: Order {order.OrderId} set to Processing");
            // Simulate processing
            await Task.Delay(500);
            order.Status = "Shipped";
            order.History.Add(new OrderStatusEvent { Status = "Shipped", Notes = "Order shipped" });
            await _orders.UpsertAsync(order);
            await _appLogger.LogInfoAsync($"OrderProcessor: Order {order.OrderId} set to Shipped; enqueuing inventory updates");
            foreach (var l in order.Lines)
            {
                await _inventoryQueue.EnqueueInventoryUpdateAsync(l.ProductId, -l.Quantity);
                await _appLogger.LogInfoAsync($"OrderProcessor: Inventory update queued ProductId={l.ProductId} Delta={-l.Quantity}");
            }
            log.LogInformation("Order {id} processed", order.OrderId);
            _logger.LogInformation("Order {id} fully processed and shipped", order.OrderId);
            await _appLogger.LogInfoAsync($"OrderProcessor: Order {order.OrderId} processing complete");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed processing order message");
            _logger.LogError(ex, "Order processing failure");
            await _appLogger.LogErrorAsync("OrderProcessor: Exception during processing", ex);
            throw; // Let Functions retry
        }
    }
    private record OrderPayload(string OrderId, string CustomerId);
}
