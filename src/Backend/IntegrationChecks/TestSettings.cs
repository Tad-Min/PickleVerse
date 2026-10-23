using Microsoft.Extensions.Configuration;
using Services.Configuration;

static class TestSettings
{
    public static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
    public static IConfigurationRoot Configuration() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(Root, "src/Backend/API/appsettings.json"))
        .AddInMemoryCollection(new Dictionary<string,string?>
        { ["Jwt:Key"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)) }).Build();
    public static ServerSettings Load() => new(Configuration());
}
