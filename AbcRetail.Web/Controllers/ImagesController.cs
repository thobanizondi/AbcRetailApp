using AbcRetail.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Web.Controllers;

[Authorize] // require login to request SAS
[Route("images")] 
public class ImagesController : Controller
{
    private readonly IImageStorageService _images;
    public ImagesController(IImageStorageService images){ _images = images; }

    // GET /images/sas?name=<fileName>&thumb=true
    [HttpGet("sas")]
    public async Task<IActionResult> GetSas([FromQuery] string name, [FromQuery] bool thumb = false)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("name required");
        var (url, expires) = await _images.GetReadSasForImageAsync(name, thumb);
        return Json(new { url, expires });
    }
}
