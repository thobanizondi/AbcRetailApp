namespace AbcRetail.Core.Models;

public class Order
{
    public string OrderId { get; set; } = Guid.NewGuid().ToString();
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderLine> Lines { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Queued"; // Queued, Processing, Shipped, Completed
    public List<OrderStatusEvent> History { get; set; } = new();
}

public class OrderLine
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrderStatusEvent
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
