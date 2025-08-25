using AbcRetail.Core.Interfaces;
using AbcRetail.Core.Models;
using Azure.Storage.Queues;
using System.Text.Json;

namespace AbcRetail.Infrastructure;

public class QueueOrderService : IOrderQueueService
{
    private readonly QueueClient _queue;
    public QueueOrderService(QueueServiceClient service, StorageOptions options)
    {
        _queue = service.GetQueueClient(options.QueueNewOrders);
        _queue.CreateIfNotExists();
    }
    public async Task EnqueueNewOrderAsync(Order order)
    {
        var payload = JsonSerializer.Serialize(new { order.OrderId, order.CustomerId, Lines = order.Lines });
        await _queue.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload)));
    }
}

public class QueueInventoryService : IInventoryQueueService
{
    private readonly QueueClient _queue;
    public QueueInventoryService(QueueServiceClient service, StorageOptions options)
    {
        _queue = service.GetQueueClient(options.QueueInventoryUpdates);
        _queue.CreateIfNotExists();
    }
    public async Task EnqueueInventoryUpdateAsync(string productId, int quantityDelta)
    {
        var payload = JsonSerializer.Serialize(new { ProductId = productId, QuantityDelta = quantityDelta });
        await _queue.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload)));
    }
}
