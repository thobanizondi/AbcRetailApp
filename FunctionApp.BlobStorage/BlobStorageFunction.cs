using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FunctionApp.BlobStorage;

public class BlobStorageFunction
{
    private readonly ILogger _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _imagesContainer;

    public BlobStorageFunction(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<BlobStorageFunction>();
        var conn = config["Storage:StorageConnectionString"] ?? config["AzureWebJobsStorage"] ?? throw new InvalidOperationException("Storage connection not configured.");
        _blobServiceClient = new BlobServiceClient(conn);
        _imagesContainer = config["Storage:BlobContainerProductImages"] ?? "product-images";
    }

    // Queue message contract
    private sealed record ImageUploadMessage(string FileName, string ContentType, string Base64Data);

    [Function("ProcessImageUploadMessage")]
    public async Task RunAsync([QueueTrigger("image-uploads")] string rawMessage)
    {
        _logger.LogInformation("Received image upload message length={len}", rawMessage?.Length);
        ImageUploadMessage? msg = null;
        try
        {
            msg = JsonSerializer.Deserialize<ImageUploadMessage>(rawMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed deserializing queue message");
            return;
        }
        if (msg is null || string.IsNullOrWhiteSpace(msg.FileName) || string.IsNullOrWhiteSpace(msg.Base64Data))
        {
            _logger.LogWarning("Invalid message payload");
            return;
        }
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(_imagesContainer);
            await container.CreateIfNotExistsAsync(PublicAccessType.None);
            var blob = container.GetBlobClient(msg.FileName);
            byte[] data;
            try { data = Convert.FromBase64String(msg.Base64Data); }
            catch (FormatException)
            {
                _logger.LogWarning("Invalid base64 for {file}", msg.FileName);
                return;
            }
            await using var ms = new MemoryStream(data);
            var headers = new BlobHttpHeaders { ContentType = string.IsNullOrWhiteSpace(msg.ContentType)? "application/octet-stream" : msg.ContentType };
            await blob.UploadAsync(ms, overwrite:true);
            if (headers.ContentType != null)
            {
                try { await blob.SetHttpHeadersAsync(headers); } catch { }
            }
            _logger.LogInformation("Uploaded {file} ({bytes} bytes)", msg.FileName, data.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failure for {file}", msg.FileName);
            throw;
        }
    }
}