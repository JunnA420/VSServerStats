using Microsoft.AspNetCore.Hosting;

namespace VSServerStats.Web.Services;

public interface IFileService
{
    Task<string> LoadEstablishmentsJson();
}

public class FileService : IFileService
{
    private readonly IWebHostEnvironment _env;

    public FileService(IWebHostEnvironment env) => _env = env;

    public Task<string> LoadEstablishmentsJson()
    {
        var path = Path.Combine(_env.WebRootPath, "establishments", "establishments.json");
        return File.ReadAllTextAsync(path);
    }
}
