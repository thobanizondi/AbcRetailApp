using AbcRetail.Core.Interfaces;
using AbcRetail.Core.Models;
using Azure;
using Azure.Data.Tables;

namespace AbcRetail.Infrastructure;

internal class CustomerEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

internal class ProductEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int Quantity { get; set; }
}

internal class OrderEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string LinesJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string? HistoryJson { get; set; }
}

public class TableCustomerRepository : ICustomerRepository
{
    private readonly TableClient _table;
    public TableCustomerRepository(TableServiceClient service, StorageOptions options)
    {
        _table = service.GetTableClient(options.TableNameCustomers);
        _table.CreateIfNotExists();
    }
    public async Task<Customer?> GetAsync(string customerId)
    {
        try
        {
            var resp = await _table.GetEntityAsync<CustomerEntity>(Partition(customerId), customerId);
            var e = resp.Value;
            return new Customer { CustomerId = e.RowKey, Name = e.Name, Email = e.Email, ShippingAddress = e.ShippingAddress, PasswordHash = e.PasswordHash };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
    public async Task UpsertAsync(Customer customer)
    {
        var entity = new CustomerEntity
        {
            PartitionKey = Partition(customer.CustomerId),
            RowKey = customer.CustomerId,
            Name = customer.Name,
            Email = customer.Email,
            ShippingAddress = customer.ShippingAddress,
            PasswordHash = customer.PasswordHash
        };
        await _table.UpsertEntityAsync(entity);
    }
    public async Task<IEnumerable<Customer>> SearchByNameAsync(string nameStartsWith, int take = 10)
    {
        nameStartsWith = nameStartsWith?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(nameStartsWith)) return Enumerable.Empty<Customer>();
        var list = new List<Customer>();
        await foreach (var e in _table.QueryAsync<CustomerEntity>())
        {
            if (e.Name.StartsWith(nameStartsWith, StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new Customer { CustomerId = e.RowKey, Name = e.Name, Email = e.Email, ShippingAddress = e.ShippingAddress });
                if (list.Count >= take) break;
            }
        }
        return list;
    }
    public async Task<IEnumerable<Customer>> SearchByEmailAsync(string emailStartsWith, int take = 10)
    {
        emailStartsWith = emailStartsWith?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(emailStartsWith)) return Enumerable.Empty<Customer>();
        var list = new List<Customer>();
        await foreach (var e in _table.QueryAsync<CustomerEntity>())
        {
            if (e.Email.StartsWith(emailStartsWith, StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new Customer { CustomerId = e.RowKey, Name = e.Name, Email = e.Email, ShippingAddress = e.ShippingAddress });
                if (list.Count >= take) break;
            }
        }
        return list;
    }
    private static string Partition(string id) => id.Substring(0,1).ToUpperInvariant();
}

public class TableProductRepository : IProductRepository
{
    private readonly TableClient _table;
    public TableProductRepository(TableServiceClient service, StorageOptions options)
    {
        _table = service.GetTableClient(options.TableNameProducts);
        _table.CreateIfNotExists();
    }
    public async Task<Product?> GetAsync(string productId)
    {
        try
        {
            var resp = await _table.GetEntityAsync<ProductEntity>(Partition(productId), productId);
            var e = resp.Value;
            return Map(e);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
    public async Task UpsertAsync(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.ProductId))
            product.ProductId = Guid.NewGuid().ToString();
        var entity = new ProductEntity
        {
            PartitionKey = Partition(product.ProductId),
            RowKey = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            Price = (double)product.Price,
            Category = product.Category,
            ImageUrl = product.ImageUrl,
            ThumbnailUrl = product.ThumbnailUrl,
            Quantity = product.Quantity
        };
        await _table.UpsertEntityAsync(entity);
    }
    public async Task<IEnumerable<Product>> ListAsync(int take = 50)
    {
        var list = new List<Product>();
        int count = 0;
        await foreach (var e in _table.QueryAsync<ProductEntity>())
        {
            list.Add(Map(e));
            if (++count >= take) break;
        }
        return list;
    }
    public async Task<bool> AdjustQuantityAsync(string productId, int delta)
    {
        // Simple approach: fetch, modify, upsert (no concurrency token handling here)
        var existing = await GetAsync(productId);
        if (existing == null) return false;
        var newQty = existing.Quantity + delta;
        if (newQty < 0) return false; // insufficient stock
        existing.Quantity = newQty;
        await UpsertAsync(existing);
        return true;
    }
    private static string Partition(string id) => id.Substring(0,1).ToUpperInvariant();
    private static Product Map(ProductEntity e) => new() { ProductId = e.RowKey, Name = e.Name, Description = e.Description, Price = (decimal)e.Price, Category = e.Category, ImageUrl = e.ImageUrl, ThumbnailUrl = e.ThumbnailUrl, Quantity = e.Quantity };
}

public class TableOrderRepository : IOrderRepository
{
    private readonly TableClient _table;
    public TableOrderRepository(TableServiceClient service, StorageOptions options)
    {
        _table = service.GetTableClient(options.TableNameOrders);
        _table.CreateIfNotExists();
    }
    public async Task<Order?> GetAsync(string orderId)
    {
        try
        {
            var resp = await _table.GetEntityAsync<OrderEntity>(Partition(orderId), orderId);
            var e = resp.Value;
            return new Order
            {
                OrderId = e.RowKey,
                CustomerId = e.CustomerId,
                Lines = System.Text.Json.JsonSerializer.Deserialize<List<OrderLine>>(e.LinesJson) ?? new(),
                Status = e.Status,
                CreatedUtc = e.CreatedUtc,
                History = string.IsNullOrEmpty(e.HistoryJson) ? new() : (System.Text.Json.JsonSerializer.Deserialize<List<OrderStatusEvent>>(e.HistoryJson) ?? new())
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
    public async Task UpsertAsync(Order order)
    {
        var entity = new OrderEntity
        {
            PartitionKey = Partition(order.OrderId),
            RowKey = order.OrderId,
            CustomerId = order.CustomerId,
            LinesJson = System.Text.Json.JsonSerializer.Serialize(order.Lines),
            Status = order.Status,
            CreatedUtc = order.CreatedUtc,
            HistoryJson = System.Text.Json.JsonSerializer.Serialize(order.History)
        };
        await _table.UpsertEntityAsync(entity);
    }
    public async Task<IEnumerable<Order>> ListAsync(int take = 100)
    {
        var list = new List<Order>();
        int count = 0;
        await foreach (var e in _table.QueryAsync<OrderEntity>())
        {
            list.Add(new Order
            {
                OrderId = e.RowKey,
                CustomerId = e.CustomerId,
                Lines = System.Text.Json.JsonSerializer.Deserialize<List<OrderLine>>(e.LinesJson) ?? new(),
                Status = e.Status,
                CreatedUtc = e.CreatedUtc,
                History = string.IsNullOrEmpty(e.HistoryJson) ? new() : (System.Text.Json.JsonSerializer.Deserialize<List<OrderStatusEvent>>(e.HistoryJson) ?? new())
            });
            if (++count >= take) break;
        }
        return list.OrderByDescending(o => o.CreatedUtc);
    }
    public async Task<IEnumerable<Order>> ListByCustomerAsync(string customerId, int take = 100)
    {
        customerId = customerId?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(customerId)) return Enumerable.Empty<Order>();
        var list = new List<Order>();
        int count = 0;
        await foreach (var e in _table.QueryAsync<OrderEntity>())
        {
            if (!string.Equals(e.CustomerId, customerId, StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(new Order
            {
                OrderId = e.RowKey,
                CustomerId = e.CustomerId,
                Lines = System.Text.Json.JsonSerializer.Deserialize<List<OrderLine>>(e.LinesJson) ?? new(),
                Status = e.Status,
                CreatedUtc = e.CreatedUtc,
                History = string.IsNullOrEmpty(e.HistoryJson) ? new() : (System.Text.Json.JsonSerializer.Deserialize<List<OrderStatusEvent>>(e.HistoryJson) ?? new())
            });
            if (++count >= take) break;
        }
        return list.OrderByDescending(o => o.CreatedUtc);
    }
    private static string Partition(string id) => id.Substring(0,1).ToUpperInvariant();
}
