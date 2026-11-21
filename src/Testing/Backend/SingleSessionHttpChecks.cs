using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using API.Controllers;
using API.Filters;
using Entities.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Repositories.Context;
using Repositories.UnitOfWork;
using Services.Configuration;
using Services.Implementations;
using Services.Interfaces;

static class SingleSessionHttpChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        var player = await new FunctionalFixture(options).Player();
        var config = TestSettings.Configuration();
        var settings = new ServerSettings(config);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped(_ => new PickleVerseContext(options));
        builder.Services.AddScoped<IUnitOfWork>(sp => new UnitOfWork(sp.GetRequiredService<PickleVerseContext>()));
        builder.Services.AddScoped<IAuthService>(sp =>
        {
            var unit = sp.GetRequiredService<IUnitOfWork>();
            return new AuthService(unit, new ProfileService(unit, NullLogger<ProfileService>.Instance, settings, TimeProvider.System),
                config, new CapturedMail(), TimeProvider.System, settings);
        });
        builder.Services.AddControllers(o => o.Filters.Add<ApiResponseFilter>()).AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidIssuer = config["Jwt:Issuer"] ?? "PickleVerseAPI", ValidAudience = config["Jwt:Audience"] ?? "PickleVerseClient",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!))
            };
            o.Events = new JwtBearerEvents { OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (!Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
                    !int.TryParse(principal?.FindFirstValue("auth_version"), out int version) ||
                    !await context.HttpContext.RequestServices.GetRequiredService<IAuthService>().ValidateSessionAsync(id, version,
                        principal?.FindFirstValue("access_scope") ?? "game")) context.Fail("Expired login");
            }};
        });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("auth", limiter =>
        { limiter.PermitLimit = 20; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; }));
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        async Task<AuthResponse> Login()
        {
            using var response = await http.PostAsJsonAsync("api/Auth/login", new { Email = player.ToString("N") + "@example.invalid", Password = "TestPass!234" });
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("data").Deserialize<AuthResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
        async Task<HttpStatusCode> Session(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/Auth/session");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request);
            return response.StatusCode;
        }
        var a = await Login();
        check(await Session(a.AccessToken!) == HttpStatusCode.OK, "authenticated HTTP heartbeat accepts the first device");
        var b = await Login();
        check(await Session(a.AccessToken!) == HttpStatusCode.Unauthorized && await Session(b.AccessToken!) == HttpStatusCode.OK,
            "real JWT HTTP requests reject device A immediately after B signs in");
        using (var response = await http.PostAsJsonAsync("api/Auth/refresh-token", new { RefreshToken = a.RefreshToken }))
            check(response.StatusCode == HttpStatusCode.Unauthorized, "HTTP refresh cannot revive the replaced device");
        bool allHealthy = true;
        for (int i = 0; i < 25; i++) allHealthy &= await Session(b.AccessToken!) == HttpStatusCode.OK;
        check(allHealthy, "frequent session heartbeats do not exhaust the authentication rate limit");
        using (var response = await http.GetAsync("api/Auth/session"))
            check(response.StatusCode == HttpStatusCode.Unauthorized, "session heartbeat requires authentication");
        await app.StopAsync();
    }
}
