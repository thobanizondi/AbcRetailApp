namespace AbcRetail.Infrastructure;

public class StorageOptions
{
    public string StorageConnectionString { get; set; } = string.Empty;
    public string TableNameCustomers { get; set; } = "Customers";
    public string TableNameProducts { get; set; } = "Products";
    public string TableNameOrders { get; set; } = "Orders";
    public string BlobContainerProductImages { get; set; } = "product-images";
    public string BlobContainerThumbnails { get; set; } = "product-thumbnails";
    public string QueueNewOrders { get; set; } = "new-orders";
    public string QueueInventoryUpdates { get; set; } = "inventory-updates";
    public string FileShareLogs { get; set; } = "logs";
}
