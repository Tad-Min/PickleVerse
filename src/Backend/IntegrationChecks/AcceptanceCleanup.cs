using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

static class AcceptanceCleanup
{
    public static async Task RemoveBucket()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
        string directory = Path.Combine(root, "artifacts/account-multiplayer-ai-20261103/acceptance-private");
        using var state = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "state.json")));
        string database = state.RootElement.GetProperty("database").GetString()!;
        string bucket = state.RootElement.GetProperty("bucket").GetString()!;
        if (!Regex.IsMatch(database, "^pkv_acceptance_[a-f0-9]{32}$") || bucket != "pkv-acceptance-" + database[15..27])
            throw new InvalidOperationException("Unexpected isolated bucket identity.");
        var env = (await File.ReadAllLinesAsync(Path.Combine(directory, "api.env"))).Select(s => s.Split('=', 2)).Where(s => s.Length == 2).ToDictionary(s => s[0], s => s[1]);
        if (env["Storage__BucketName"] != bucket || env["Storage__ServiceUrl"] != "http://pickleverse_s3:9000")
            throw new InvalidOperationException("Unexpected acceptance storage configuration.");
        using var s3 = new AmazonS3Client(new BasicAWSCredentials(env["Storage__AccessKey"], env["Storage__SecretKey"]),
            new AmazonS3Config { ServiceURL = "http://127.0.0.1:19000", ForcePathStyle = true, UseHttp = true });
        int removed = 0;
        bool bucketDeleted = true;
        try
        {
            // Fetch the first page again after deletion to avoid skipping keys.
            while (true)
            {
                var page = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket });
                if (page.S3Objects == null || page.S3Objects.Count == 0) break;
                foreach (var item in page.S3Objects)
                {
                    await s3.DeleteObjectAsync(bucket, item.Key);
                    removed++;
                }
            }
            await s3.DeleteBucketAsync(bucket);
        }
        catch (AmazonS3Exception error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound) { }
        // local-s3 implements DeleteObject but not DeleteBucket. The wrapper may
        // remove its verified empty directory with rmdir, never a recursive delete.
        catch (AmazonS3Exception error) when (error.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed) { bucketDeleted = false; }
        await File.WriteAllTextAsync(Path.Combine(root, "artifacts/account-multiplayer-ai-20261103/s3-cleanup.json"), JsonSerializer.Serialize(new { bucket, empty = true, bucketDeleted, removedObjects = removed }));
        Console.WriteLine($"Cleared isolated acceptance S3 objects ({removed}); bucket removed through API: {bucketDeleted}.");
    }
}
