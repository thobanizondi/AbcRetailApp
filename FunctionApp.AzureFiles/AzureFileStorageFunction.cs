using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Files.Shares;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FunctionApp.AzureFiles;

public class AzureFileStorageFunction
{
    private readonly ILogger _logger;
    private readonly string _connectionString;
    private readonly string _shareName;

    public AzureFileStorageFunction(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _logger = loggerFactory.CreateLogger<AzureFileStorageFunction>();
        _connectionString = configuration["Storage:StorageConnectionString"] ?? throw new InvalidOperationException("Storage:StorageConnectionString not configured.");
        _shareName = configuration["Storage:FileShareLogs"] ?? "logs";
    }

    [Function("AzureFileLogWriter")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
    {
        var now = DateTime.UtcNow;
        _logger.LogInformation("AzureFileLogWriter executed at: {Time}", now);
        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next schedule: {Next}", myTimer.ScheduleStatus.Next);
        }

        var logContent = new StringBuilder()
            .AppendLine($"ExecutionUtc: {now:o}")
            .AppendLine($"NextUtc: {myTimer.ScheduleStatus?.Next:o}")
            .AppendLine($"Host: {Environment.MachineName}")
            .ToString();

        try
        {
            await WriteToAzureFilesAsync(logContent);
            _logger.LogInformation("Log file written to Azure File Share '{Share}'", _shareName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write log file to Azure File Share {Share}", _shareName);
            throw;
        }
    }

    private async Task WriteToAzureFilesAsync(string content)
    {
        var shareClient = new ShareClient(_connectionString, _shareName);
        await shareClient.CreateIfNotExistsAsync();

        // Directory per day (UTC)
        var dateDirectory = DateTime.UtcNow.ToString("yyyyMMdd");
        var directoryClient = shareClient.GetDirectoryClient(dateDirectory);
        await directoryClient.CreateIfNotExistsAsync();

        // Unique file name per execution (guid)
        var fileName = $"{Guid.NewGuid()}.txt";
        var fileClient = directoryClient.GetFileClient(fileName);
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
        {
            await fileClient.CreateAsync(stream.Length);
            await fileClient.UploadAsync(stream);
        }
    }
}