namespace AbcRetail.Core.Models;

public class Customer
{
    public string CustomerId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    // Stored password hash (e.g., SHA256). Empty if not set.
    public string PasswordHash { get; set; } = string.Empty;
}
