using AbcRetail.Core.Interfaces;
using AbcRetail.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace AbcRetail.Web.Controllers;

[Authorize] // default: require login
public class ProductController : Controller
{
    private readonly IProductRepository _products;
    private readonly IImageStorageService _images;

    public ProductController(IProductRepository products, IImageStorageService images)
    {
        _products = products;
        _images = images;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var items = (await _products.ListAsync()).ToList();
        // Ensure both main image & thumbnail have SAS tokens (if shared key available)
        for (int i = 0; i < items.Count; i++)
        {
            var prod = items[i];
            if (!string.IsNullOrEmpty(prod.ImageUrl))
            {
                prod.ImageUrl = await _images.GetReadUrlAsync(prod.ImageUrl!);
            }
            if (!string.IsNullOrEmpty(prod.ThumbnailUrl))
            {
                // If already has a query part (likely signed), skip. Otherwise try explicit SAS generation by blob name.
                
                    try
                    {
                        var thumbUri = new Uri(prod.ThumbnailUrl);
                        var fileName = System.IO.Path.GetFileName(thumbUri.LocalPath);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            var (sasUrl, _) = await _images.GetReadSasForImageAsync(fileName, thumbnail: true);
                            prod.ThumbnailUrl = sasUrl;
                        }
                        else
                        {
                            // Fallback to generic signing of full URL
                            prod.ThumbnailUrl = await _images.GetReadUrlAsync(prod.ThumbnailUrl!);
                        }
                    }
                    catch
                    {
                        // Fallback to generic signing of full URL if parsing fails
                        prod.ThumbnailUrl = await _images.GetReadUrlAsync(prod.ThumbnailUrl!);
                    }
                
            }
        }
        return View(items);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")] // only admin can create catalogue items
    public IActionResult Create() => View(new Product());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")] // admin only
    public async Task<IActionResult> Create(Product model, IFormFile? image)
    {
        if (!ModelState.IsValid) return View(model);
        if (image != null)
        {
            using var stream = image.OpenReadStream();
            var blobName = Guid.NewGuid()+Path.GetExtension(image.FileName);
            model.ImageUrl = await _images.UploadImageAsync(blobName, stream, image.ContentType);
            if (!string.IsNullOrEmpty(model.ImageUrl) && model.ImageUrl.Contains("/product-images/"))
            {
                model.ThumbnailUrl = model.ImageUrl.Replace("/product-images/", "/product-thumbnails/");
            }
        }
        await _products.UpsertAsync(model);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var product = await _products.GetAsync(id);
        if (product == null) return NotFound();
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(string id, Product model, IFormFile? image)
    {
        if (id != model.ProductId) return BadRequest();
        var existing = await _products.GetAsync(id);
        if (existing == null) return NotFound();
        if (!ModelState.IsValid) return View(model);
        // preserve existing image if none uploaded
        if (image != null)
        {
            using var stream = image.OpenReadStream();
            var blobName = Guid.NewGuid()+Path.GetExtension(image.FileName);
            model.ImageUrl = await _images.UploadImageAsync(blobName, stream, image.ContentType);
            if (!string.IsNullOrEmpty(model.ImageUrl) && model.ImageUrl.Contains("/product-images/"))
            {
                model.ThumbnailUrl = model.ImageUrl.Replace("/product-images/", "/product-thumbnails/");
            }
        }
        else
        {
            model.ImageUrl = existing.ImageUrl;
            model.ThumbnailUrl = existing.ThumbnailUrl;
        }
        await _products.UpsertAsync(model);
        return RedirectToAction(nameof(Index));
    }
}
