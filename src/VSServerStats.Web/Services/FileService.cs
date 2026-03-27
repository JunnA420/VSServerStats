using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace VSServerStats.Web.Services
{
    public interface IFileService
    {
        Task<string> LoadEstablishmentsJson();
        Task<string> ReadFileAsync(string filePath);
        Task WriteFileAsync(string filePath, string content);
        Task<bool> FileExistsAsync(string filePath);
        Task DeleteFileAsync(string filePath);
    }

    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _env;

        public FileService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> LoadEstablishmentsJson()
        {
            var path = Path.Combine(_env.WebRootPath, "establishments", "establishments.json");
            return await File.ReadAllTextAsync(path);
        }
        public async Task<string> ReadFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            return await File.ReadAllTextAsync(filePath);
        }

        public async Task WriteFileAsync(string filePath, string content)
        {
            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
              Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
            await File.WriteAllTextAsync(filePath, content);              
        }

        public Task<bool> FileExistsAsync(string filePath)
        {
            return Task.FromResult(File.Exists(filePath));
        }

        public async Task DeleteFileAsync(string filePath)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);

            await Task.CompletedTask;
        }
    }

}