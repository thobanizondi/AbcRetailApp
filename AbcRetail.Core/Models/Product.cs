namespace AbcRetail.Core.Models;

using System.ComponentModel.DataAnnotations;

public class Product
{
    public string ProductId { get; set; } = Guid.NewGuid().ToString();

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 1000000)]
    public decimal Price { get; set; }

    [Required, StringLength(80)]
    public string Category { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }

    [Range(0, 1000000)]
    public int Quantity { get; set; } // Inventory quantity
}
