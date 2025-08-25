using AbcRetail.Core.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Azure;
using Azure.Storage.Sas;
using Azure.Storage;

namespace AbcRetail.Infrastructure;

public class BlobImageStorageService : IImageStorageService
{
    private readonly BlobServiceClient _service;
    private readonly StorageOptions _options;
    private readonly BlobContainerClient _images;
    private readonly BlobContainerClient _thumbs;
    private readonly string _accountName;
    private readonly string _accountKey;

    public BlobImageStorageService(BlobServiceClient service, StorageOptions options)
    {
        _service = service;
        _options = options;
        _images = service.GetBlobContainerClient(options.BlobContainerProductImages);
        _thumbs = service.GetBlobContainerClient(options.BlobContainerThumbnails);
        _images.CreateIfNotExists();
        _thumbs.CreateIfNotExists();
        // Parse account credentials from connection string for SAS generation
        // Expected keys: AccountName=...;AccountKey=...
        var parts = options.StorageConnectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        _accountName = parts.FirstOrDefault(p => p.StartsWith("AccountName=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1] ?? string.Empty;
        _accountKey = parts.FirstOrDefault(p => p.StartsWith("AccountKey=", StringComparison.OrdinalIgnoreCase))?.Split(';')[0].Split("AccountKey=")[1] ?? string.Empty;
    }
    public async Task<string> UploadImageAsync(string fileName, Stream content, string contentType)
    {
        var blob = _images.GetBlobClient(fileName);
        await blob.UploadAsync(content, overwrite: true);
        try { await blob.SetHttpHeadersAsync(new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }); } catch { }
        return blob.Uri.ToString();
    }

    public Task<string> GetReadUrlAsync(string storedUrl, TimeSpan? ttl = null)
    {
        if (string.IsNullOrWhiteSpace(storedUrl)) return Task.FromResult(storedUrl);
        // If connection string does not contain a usable shared key (e.g., UseDevelopmentStorage=true or AAD), return original URL.
        if (string.IsNullOrEmpty(_accountName) || string.IsNullOrEmpty(_accountKey))
            return Task.FromResult(storedUrl);


        try
        {
            // Attempt decode to be certain
            _ = Convert.FromBase64String(_accountKey);
            var builder = new Azure.Storage.Blobs.BlobUriBuilder(new Uri(storedUrl));
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = builder.BlobContainerName,
                BlobName = builder.BlobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromHours(1))
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);
            var credential = new StorageSharedKeyCredential(_accountName, _accountKey);
            builder.Sas = sasBuilder.ToSasQueryParameters(credential);
            return Task.FromResult(builder.ToUri().ToString());
        }
        catch
        {
            // Fallback: return original unsuffixed URL if signing fails
            return Task.FromResult(storedUrl);
        }
    }

    public Task<(string url, DateTimeOffset expiresOn)> GetReadSasForImageAsync(string fileName, bool thumbnail = false, TimeSpan? ttl = null)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return Task.FromResult((string.Empty, DateTimeOffset.MinValue));
        if (string.IsNullOrEmpty(_accountName) || string.IsNullOrEmpty(_accountKey))
        {
            var client = (thumbnail ? _thumbs : _images).GetBlobClient(fileName);
            return Task.FromResult((client.Uri.ToString(), DateTimeOffset.MaxValue));
        }
       
        try
        {
            //_ = Convert.FromBase64String(_accountKey); // validate
            var container = thumbnail ? _options.BlobContainerThumbnails : _options.BlobContainerProductImages;
            var blobClient = (thumbnail ? _thumbs : _images).GetBlobClient(fileName);
            var builder = new Azure.Storage.Blobs.BlobUriBuilder(blobClient.Uri);
            var expires = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(30));
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = container,
                BlobName = fileName,
                Resource = "b",
                ExpiresOn = expires
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);
            var credential = new StorageSharedKeyCredential(_accountName, _accountKey);
            builder.Sas = sasBuilder.ToSasQueryParameters(credential);
            return Task.FromResult((builder.ToUri().ToString(), expires));
        }
        catch
        {
            var clientFallback = (thumbnail ? _thumbs : _images).GetBlobClient(fileName);
            return Task.FromResult((clientFallback.Uri.ToString(), DateTimeOffset.MaxValue));
        }
    }
}

public class FileShareLogger : IAppLogger
{
    private readonly ShareDirectoryClient _dir;
    public FileShareLogger(ShareServiceClient shareService, StorageOptions options)
    {
        var share = shareService.GetShareClient(options.FileShareLogs);
        share.CreateIfNotExists();
        _dir = share.GetRootDirectoryClient();
    }
    public async Task LogInfoAsync(string message) => await AppendAsync("info", message);
    public async Task LogErrorAsync(string message, Exception? ex = null) => await AppendAsync("error", message + (ex!=null?" :: "+ex: string.Empty));

    private async Task AppendAsync(string category, string message)
    {
        var day = DateTime.UtcNow.ToString("yyyyMMdd");
        var fileName = $"{category}-{day}.log";
        var file = _dir.GetFileClient(fileName);
        var bytes = System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o") + " | " + message + "\n");
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                long existing = 0;
                bool exists = true;
                try
                {
                    var props = (await file.GetPropertiesAsync()).Value;
                    existing = props.ContentLength;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    exists = false;
                }
                // Grow file exactly as needed (cap at 5 MB per file to avoid runaway growth)
                const long cap = 5 * 1024 * 1024; // 5MB
                var newLength = existing + bytes.Length;
                if (newLength > cap) return; // silently drop once cap reached
                if (!exists)
                {
                    await file.CreateAsync(newLength);
                    await file.SetHttpHeadersAsync(newLength, new ShareFileHttpHeaders { ContentType = "text/plain" });
                    existing = 0;
                }
                else if (newLength > existing)
                {
                    // Resize keeping content type
                    await file.SetHttpHeadersAsync(newLength, new ShareFileHttpHeaders { ContentType = "text/plain" });
                }
                using var ms = new MemoryStream(bytes);
                await file.UploadRangeAsync(ShareFileRangeWriteType.Update, new HttpRange(existing, bytes.Length), ms);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // Simple backoff for race
                await Task.Delay(40 * (attempt + 1));
            }
            catch
            {
                return; // swallow logging errors
            }
        }
    }
}
