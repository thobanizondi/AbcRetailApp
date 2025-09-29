using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FunctionApp.TableStorage;

public class TableStorageFunction
{
    private readonly ILogger _logger;
    private readonly TableClient _customerTable;

    public TableStorageFunction(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<TableStorageFunction>();
        var conn = config["Storage:StorageConnectionString"] ?? config["AzureWebJobsStorage"] ?? throw new InvalidOperationException("Storage connection not configured.");
        var tableService = new TableServiceClient(conn);
        var tableName = config["Storage:TableNameCustomers"] ?? "Customers";
        _customerTable = tableService.GetTableClient(tableName);
        _customerTable.CreateIfNotExists();
    }

    private sealed record RegisterRequest(string CustomerId, string Name, string Password, string? ShippingAddress);

    [Function("RegisterCustomer")]
    public async Task<HttpResponseData> RegisterAsync([HttpTrigger(AuthorizationLevel.Function, "post", Route = "register-customer")] HttpRequestData req)
    {
        try
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            var payload = JsonSerializer.Deserialize<RegisterRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload is null || string.IsNullOrWhiteSpace(payload.CustomerId) || string.IsNullOrWhiteSpace(payload.Name) || string.IsNullOrWhiteSpace(payload.Password))
            {
                var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid payload");
                return bad;
            }
            var customerId = payload.CustomerId.Trim();
            // check if exists
            try
            {
                var existing = await _customerTable.GetEntityAsync<TableEntity>(Partition(customerId), customerId);
                if (existing != null)
                {
                    var conflict = req.CreateResponse(System.Net.HttpStatusCode.Conflict);
                    await conflict.WriteStringAsync("Customer already exists");
                    return conflict;
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // not found => ok to create
            }

            var entity = new TableEntity(Partition(customerId), customerId)
            {
                { "Name", payload.Name.Trim() },
                { "Email", customerId },
                { "ShippingAddress", payload.ShippingAddress?.Trim() ?? string.Empty },
                { "PasswordHash", HashPassword(payload.Password) },
                { "IsDisabled", false }
            };
            await _customerTable.AddEntityAsync(entity);
            var okResp = req.CreateResponse(System.Net.HttpStatusCode.Created);
            await okResp.WriteStringAsync("Registered");
            return okResp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failure");
            var fail = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await fail.WriteStringAsync("Error");
            return fail;
        }
    }

    private static string Partition(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return "_";
        var first = char.ToUpperInvariant(customerId[0]);
        if (!char.IsLetterOrDigit(first)) return "_";
        return first.ToString();
    }

    private static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return string.Empty;
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}