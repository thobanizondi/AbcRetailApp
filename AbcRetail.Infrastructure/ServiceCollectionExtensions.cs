using AbcRetail.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Files.Shares;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AbcRetail.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStorageServices(this IServiceCollection services, IConfiguration config)
    {
        var options = new StorageOptions();
        config.GetSection("Storage").Bind(options);
        // Fallback: allow env var AZURE_STORAGE_CONNECTION_STRING to populate if not present in config
        if (string.IsNullOrWhiteSpace(options.StorageConnectionString))
        {
            var envConn = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(envConn))
            {
                options.StorageConnectionString = envConn;
            }
        }
        // Additional fallbacks: root-level StorageConnectionString, Values:StorageConnectionString (Azure Functions local.settings.json), AzureWebJobsStorage
        if (string.IsNullOrWhiteSpace(options.StorageConnectionString))
        {
            var direct = config["StorageConnectionString"];
            if (!string.IsNullOrWhiteSpace(direct)) options.StorageConnectionString = direct;
        }
        if (string.IsNullOrWhiteSpace(options.StorageConnectionString))
        {
            var fromValues = config["Values:StorageConnectionString"]; // local.settings.json pattern
            if (!string.IsNullOrWhiteSpace(fromValues)) options.StorageConnectionString = fromValues;
        }
        if (string.IsNullOrWhiteSpace(options.StorageConnectionString))
        {
            var azureJobs = config["AzureWebJobsStorage"] ?? config["Values:AzureWebJobsStorage"]; // final fallback
            if (!string.IsNullOrWhiteSpace(azureJobs)) options.StorageConnectionString = azureJobs;
        }
        if (string.IsNullOrWhiteSpace(options.StorageConnectionString))
        {
            throw new ArgumentException("Storage connection string is not configured. Set Storage:StorageConnectionString in appsettings or AZURE_STORAGE_CONNECTION_STRING env var.");
        }
        services.AddSingleton(options);

        services.AddSingleton(new TableServiceClient(options.StorageConnectionString));
        services.AddSingleton(new BlobServiceClient(options.StorageConnectionString));
        services.AddSingleton(new QueueServiceClient(options.StorageConnectionString));
        services.AddSingleton(new ShareServiceClient(options.StorageConnectionString));

        services.AddScoped<ICustomerRepository, TableCustomerRepository>();
        services.AddScoped<IProductRepository, TableProductRepository>();
        services.AddScoped<IOrderRepository, TableOrderRepository>();
        services.AddScoped<IOrderQueueService, QueueOrderService>();
        services.AddScoped<IInventoryQueueService, QueueInventoryService>();
        services.AddScoped<IImageStorageService, BlobImageStorageService>();
        services.AddScoped<IAppLogger, FileShareLogger>();

        // Add initializer hosted service to ensure storage artifacts exist
        services.AddHostedService<StorageInitializerHostedService>();

        return services;
    }
}

internal class StorageInitializerHostedService : IHostedService
{
    private readonly TableServiceClient _tableService;
    private readonly BlobServiceClient _blobService;
    private readonly QueueServiceClient _queueService;
    private readonly ShareServiceClient _shareService;
    private readonly StorageOptions _options;
    private readonly ILogger<StorageInitializerHostedService> _logger;

    public StorageInitializerHostedService(TableServiceClient tableService,
        BlobServiceClient blobService,
        QueueServiceClient queueService,
        ShareServiceClient shareService,
        StorageOptions options,
        ILogger<StorageInitializerHostedService> logger)
    {
        _tableService = tableService;
        _blobService = blobService;
        _queueService = queueService;
        _shareService = shareService;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring Azure Storage resources exist...");
        // Tables
        await _tableService.GetTableClient(_options.TableNameCustomers).CreateIfNotExistsAsync(cancellationToken);
        await _tableService.GetTableClient(_options.TableNameProducts).CreateIfNotExistsAsync(cancellationToken);
        await _tableService.GetTableClient(_options.TableNameOrders).CreateIfNotExistsAsync(cancellationToken);
        // Blob containers
    // Create containers (private by default; SAS links will be generated for access)
    await _blobService.GetBlobContainerClient(_options.BlobContainerProductImages).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    await _blobService.GetBlobContainerClient(_options.BlobContainerThumbnails).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        // Queues
    await _queueService.GetQueueClient(_options.QueueNewOrders).CreateIfNotExistsAsync();
    await _queueService.GetQueueClient(_options.QueueInventoryUpdates).CreateIfNotExistsAsync();
        // File share
        await _shareService.GetShareClient(_options.FileShareLogs).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Azure Storage resources ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
