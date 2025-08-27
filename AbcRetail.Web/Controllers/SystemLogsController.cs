using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Files.Shares;
using AbcRetail.Infrastructure;
using System.Text;

namespace AbcRetail.Web.Controllers;

[Authorize(Roles = "Admin")]
public class SystemLogsController : Controller
{
    private readonly ShareServiceClient _shareService;
    private readonly StorageOptions _options;
    public SystemLogsController(ShareServiceClient shareService, StorageOptions options)
    {
        _shareService = shareService;
        _options = options;
    }

    public async Task<IActionResult> Index()
    {
        var share = _shareService.GetShareClient(_options.FileShareLogs);
        await share.CreateIfNotExistsAsync();
        var root = share.GetRootDirectoryClient();
        var files = new List<(string name,long length, DateTimeOffset? modified)>();
        await foreach (var item in root.GetFilesAndDirectoriesAsync())
        {
            if (!item.IsDirectory && item.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            {
                long size = 0;
                DateTimeOffset? modified = item.Properties.LastModified;
                try
                {
                    var fc = root.GetFileClient(item.Name);
                    var p = (await fc.GetPropertiesAsync()).Value;
                    size = p.ContentLength;
                    modified = p.LastModified;
                }
                catch { }
                files.Add((item.Name, size, modified));
            }
        }
        ViewBag.Files = files
            .OrderByDescending(f => f.modified)
            .ThenByDescending(f => f.name)
            .ToList();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> File(string name, bool tail = false)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return BadRequest("Invalid file name");
        if (!name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Not a log file");
        var share = _shareService.GetShareClient(_options.FileShareLogs);
        var fileClient = share.GetRootDirectoryClient().GetFileClient(name);
        try
        {
            var dl = await fileClient.DownloadAsync();
            using var ms = new MemoryStream();
            await dl.Value.Content.CopyToAsync(ms);
            var all = ms.ToArray();
            // If tail requested and file > 256KB, only return last 256KB
            const int max = 256 * 1024;
            byte[] slice = all;
            if (tail && all.Length > max)
            {
                slice = new byte[max];
                Buffer.BlockCopy(all, all.Length - max, slice, 0, max);
            }
            var text = Encoding.UTF8.GetString(slice);
            return Content(text, "text/plain", Encoding.UTF8);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return NotFound();
        }
    }
}
