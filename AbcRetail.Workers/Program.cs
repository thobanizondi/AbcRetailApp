using AbcRetail.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

var host = new HostBuilder()
    .ConfigureAppConfiguration(c =>
    {
        c.SetBasePath(Directory.GetCurrentDirectory())
         .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
         .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
         .AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        Console.WriteLine("--- Config Providers ---");
        // enumerate a few keys
        string[] keys = new[]{"Storage:StorageConnectionString","Values:StorageConnectionString","AzureWebJobsStorage","Values:AzureWebJobsStorage"};
        foreach(var k in keys){
            var v = ctx.Configuration[k];
            Console.WriteLine($"{k} => {(string.IsNullOrEmpty(v)?"<null>":$"len={v.Length}")}");
        }
        Console.WriteLine("------------------------");
        services.AddStorageServices(ctx.Configuration);
    })
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();
