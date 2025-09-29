using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace FunctionApp.Queue;

public class QueueFunction
{
    private readonly ILogger _logger;
    private readonly QueueClient _imageUploadQueue;
    private readonly QueueClient _orderQueue;

    public QueueFunction(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<QueueFunction>();
        var conn = config["Storage:StorageConnectionString"] ?? config["AzureWebJobsStorage"] ?? throw new InvalidOperationException("Storage connection not configured.");
        var imageQueueName = config["Storage:ImageUploadQueue"] ?? "image-uploads";
        var orderQueueName = config["Storage:NewOrdersQueue"] ?? "new-orders";
        _imageUploadQueue = new QueueClient(conn, imageQueueName, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
        _orderQueue = new QueueClient(conn, orderQueueName);
        _imageUploadQueue.CreateIfNotExists();
        _orderQueue.CreateIfNotExists();
    }

    private sealed record EnqueueImageRequest(string FileName, string ContentType, string Base64Data);
    private sealed record OrderLineDto(string ProductId, int Quantity, decimal UnitPrice);
    private sealed record EnqueueOrderRequest(string OrderId, string CustomerId, List<OrderLineDto> Lines);

    [Function("EnqueueImageUpload")] 
    public async Task<HttpResponseData> EnqueueImageAsync([HttpTrigger(AuthorizationLevel.Function, "post", Route = "enqueue-image")] HttpRequestData req)
    {
        try
        {
            using var reader = new StreamReader(req.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var payload = JsonSerializer.Deserialize<EnqueueImageRequest>(body);
            if (payload is null || string.IsNullOrWhiteSpace(payload.FileName) || string.IsNullOrWhiteSpace(payload.Base64Data))
            {
                var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid payload");
                return bad;
            }
            try { Convert.FromBase64String(payload.Base64Data); } catch {
                var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Base64Data not valid");
                return bad; }

            var msgJson = JsonSerializer.Serialize(payload);
            await _imageUploadQueue.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(msgJson)));
            var ok = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
            await ok.WriteStringAsync("Image Enqueued");
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing image upload");
            var fail = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await fail.WriteStringAsync("Error");
            return fail;
        }
    }

    [Function("EnqueueOrder")] 
    public async Task<HttpResponseData> EnqueueOrderAsync([HttpTrigger(AuthorizationLevel.Function, "post", Route = "enqueue-order")] HttpRequestData req)
    {
        try
        {
            using var reader = new StreamReader(req.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var payload = JsonSerializer.Deserialize<EnqueueOrderRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload is null || string.IsNullOrWhiteSpace(payload.OrderId) || string.IsNullOrWhiteSpace(payload.CustomerId) || payload.Lines is null || payload.Lines.Count == 0)
            {
                var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid order payload");
                return bad;
            }
            
            var processorMessage = new { payload.OrderId, payload.CustomerId, Lines = payload.Lines };
            var json = JsonSerializer.Serialize(processorMessage);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            await _orderQueue.SendMessageAsync(base64);
            var ok = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
            await ok.WriteStringAsync("Order Enqueued");
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing order");
            var fail = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await fail.WriteStringAsync("Error");
            return fail;
        }
    }
}