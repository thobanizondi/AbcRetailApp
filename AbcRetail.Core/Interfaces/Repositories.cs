using AbcRetail.Core.Models;

namespace AbcRetail.Core.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetAsync(string customerId);
    Task UpsertAsync(Customer customer);
    Task<IEnumerable<Customer>> SearchByNameAsync(string nameStartsWith, int take = 10);
    Task<IEnumerable<Customer>> SearchByEmailAsync(string emailStartsWith, int take = 10);
}

public interface IProductRepository
{
    Task<Product?> GetAsync(string productId);
    Task UpsertAsync(Product product);
    Task<IEnumerable<Product>> ListAsync(int take = 50);
    Task<bool> AdjustQuantityAsync(string productId, int delta); // negative delta to reduce
}

public interface IOrderRepository
{
    Task<Order?> GetAsync(string orderId);
    Task UpsertAsync(Order order);
    Task<IEnumerable<Order>> ListAsync(int take = 100);
    Task<IEnumerable<Order>> ListByCustomerAsync(string customerId, int take = 100);
}

public interface IOrderQueueService
{
    Task EnqueueNewOrderAsync(Order order);
}

public interface IInventoryQueueService
{
    Task EnqueueInventoryUpdateAsync(string productId, int quantityDelta);
}

public interface IImageStorageService
{
    Task<string> UploadImageAsync(string fileName, Stream content, string contentType);
    Task<string> GetReadUrlAsync(string storedUrl, TimeSpan? ttl = null); // returns SAS-signed URL if container is private
    Task<(string url, DateTimeOffset expiresOn)> GetReadSasForImageAsync(string fileName, bool thumbnail = false, TimeSpan? ttl = null);
}

public interface IAppLogger
{
    Task LogInfoAsync(string message);
    Task LogErrorAsync(string message, Exception? ex = null);
}
