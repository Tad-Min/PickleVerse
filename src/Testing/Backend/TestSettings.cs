using Microsoft.Extensions.Configuration;
using Services.Configuration;

static class TestSettings
{
    public static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
    public static IConfigurationRoot Configuration() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(Root, "src/Backend/API/appsettings.json"))
        .AddInMemoryCollection(new Dictionary<string, string?>
        { ["Jwt:Key"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)),
          // Stable timing fixture, independent of a developer's local gameplay settings.
          ["Matchmaking:BotFallbackSeconds"] = "360", ["Matchmaking:ReadyConfirmationSeconds"] = "120" }).Build();
    public static ServerSettings Load() => new(Configuration());

    public static IConfigurationRoot StorageConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:ServiceUrl"] = Environment.GetEnvironmentVariable("PKV_TEST_S3_URL") ?? "http://127.0.0.1:19000",
            ["Storage:AccessKey"] = Environment.GetEnvironmentVariable("S3_ACCESS_KEY"),
            ["Storage:SecretKey"] = Environment.GetEnvironmentVariable("S3_SECRET_KEY"),
            ["Storage:BucketName"] = "pickleverse"
        }).Build();
}
