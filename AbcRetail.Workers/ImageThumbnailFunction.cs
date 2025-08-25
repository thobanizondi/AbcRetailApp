using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AbcRetail.Workers;

public class ImageThumbnailFunction
{
    private readonly BlobServiceClient _blobService;
    public ImageThumbnailFunction(BlobServiceClient blobService) => _blobService = blobService;

    [Function("ImageThumbnailFunction")]
    public async Task Run([BlobTrigger("product-images/{name}")] Stream image, string name, FunctionContext context)
    {
        var log = context.GetLogger("Thumb");
        try
        {
            using var original = Image.FromStream(image);
            int size = 150;
            var thumb = new Bitmap(size, size);
            using (var g = Graphics.FromImage(thumb))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(original, 0,0,size,size);
            }
            var thumbContainer = _blobService.GetBlobContainerClient("product-thumbnails");
            await thumbContainer.CreateIfNotExistsAsync();
            var blob = thumbContainer.GetBlobClient(name);
            using var ms = new MemoryStream();
            thumb.Save(ms, ImageFormat.Jpeg);
            ms.Position = 0;
            await blob.UploadAsync(ms, overwrite:true);
            log.LogInformation("Thumbnail generated for {name}", name);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error generating thumbnail for {name}", name);
        }
    }
}
