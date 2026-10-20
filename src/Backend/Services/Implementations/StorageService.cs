using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Implementations;

public class StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _serviceUrl;
    private readonly ILogger<StorageService> _logger;

    public StorageService(IConfiguration configuration, ILogger<StorageService> logger)
    {
        _logger = logger;
        _serviceUrl = configuration["Storage:ServiceUrl"] ?? "http://localhost:9000";
        var accessKey = configuration["Storage:AccessKey"] ?? "minioadmin";
        var secretKey = configuration["Storage:SecretKey"] ?? "minioadmin";
        _bucketName = configuration["Storage:BucketName"] ?? "pickleverse";

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = _serviceUrl,
            ForcePathStyle = true, // Bắt buộc cho S3-compatible cục bộ như local-s3 / MinIO
            UseHttp = _serviceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        };

        _s3Client = new AmazonS3Client(credentials, config);
    }

    public async Task EnsureBucketExistsAsync()
    {
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName);
            if (!exists)
            {
                _logger.LogInformation("Bucket '{BucketName}' chưa tồn tại. Đang tự động tạo bucket...", _bucketName);
                var putBucketRequest = new PutBucketRequest
                {
                    BucketName = _bucketName,
                    UseClientRegion = true
                };

                await _s3Client.PutBucketAsync(putBucketRequest);
                _logger.LogInformation("Tạo tự động Bucket '{BucketName}' thành công!", _bucketName);
            }
            else
            {
                _logger.LogInformation("Bucket '{BucketName}' đã tồn tại sẵn sàng sử dụng.", _bucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi kiểm tra hoặc tự động tạo Bucket '{BucketName}'", _bucketName);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        await EnsureBucketExistsAsync();

        var key = $"{Guid.NewGuid()}_{fileName}";
        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(putRequest);
        return key;
    }

    public async Task<Stream?> GetFileAsync(string fileName)
    {
        try
        {
            var getRequest = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = fileName
            };

            var response = await _s3Client.GetObjectAsync(getRequest);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteFileAsync(string fileName)
    {
        try
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = fileName
            };

            await _s3Client.DeleteObjectAsync(deleteRequest);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa file '{FileName}' từ Bucket '{BucketName}'", fileName, _bucketName);
            return false;
        }
    }

    public string GetFileUrl(string fileName)
    {
        return $"{_serviceUrl.TrimEnd('/')}/{_bucketName}/{fileName}";
    }
}
