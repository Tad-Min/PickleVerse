using System.IO;
using System.Threading.Tasks;

namespace Services.Interfaces;

public interface IStorageService
{
    Task EnsureBucketExistsAsync();
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
    Task<Stream?> GetFileAsync(string fileName);
    Task<bool> DeleteFileAsync(string fileName);
    string GetFileUrl(string fileName);
}
