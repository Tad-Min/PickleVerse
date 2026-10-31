using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;
using API.Filters;
using API.Middlewares;
using API.OpenApi;
using Entities.DTOs.Common;
using Entities.Exceptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PickleVerse.Services.Api;

static class ApiContractChecks
{
    public static async Task Run()
    {
        int checks = 0;
        void Check(bool passed, string message)
        { if (!passed) throw new Exception("FAILED: " + message); checks++; Console.WriteLine("PASS: " + message); }
        var root = TestSettings.Root;
        foreach (var (project, expected) in new[] { ("API", new[] { "Entities", "Services" }),
                     ("Services", new[] { "Entities", "Repositories" }), ("Repositories", new[] { "Entities" }) })
        {
            var document = XDocument.Load(Path.Combine(root, "src/Backend", project, project + ".csproj"));
            var references = document.Descendants("ProjectReference").Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/'))).Order().ToArray();
            Check(references.SequenceEqual(expected.Order()), project + " has exactly the agreed project references");
            Check(!document.Descendants("Compile").Any(e => e.Attribute("Include")?.Value.Contains("My project") == true), project + " builds independently of Unity source");
        }
        var serviceReferences = typeof(IOnlineGameService).Assembly.GetReferencedAssemblies();
        Check(!serviceReferences.Any(a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore") || a.Name.StartsWith("Npgsql") || a.Name == "StackExchange.Redis"), "service assembly has no direct EF, PostgreSQL or Redis SDK dependency");
        Check(!typeof(API.Controllers.GameController).Assembly.GetReferencedAssemblies().Any(a => a.Name == "Repositories"), "API assembly does not use repository types");
        Check(new AppException("database failed", new Exception()).StatusCode == HttpStatusCode.InternalServerError, "unexpected service failures retain HTTP 500");

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers(o => o.Filters.Add<ApiResponseFilter>()).AddApplicationPart(typeof(ContractController).Assembly);
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddAuthentication("contract").AddScheme<AuthenticationSchemeOptions, ContractAuthentication>("contract", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddOpenApi(o => o.AddOperationTransformer<ApiResponseOperationTransformer>());
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages(async context => await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse.ErrorResponse(ApiResponseFilter.DefaultMessage(context.HttpContext.Response.StatusCode))));
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); app.MapOpenApi();
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        foreach (var (path, status) in new[] { ("success", 200), ("already-wrapped", 200), ("missing", 404), ("forbidden", 403),
                     ("conflict", 409), ("failure", 500), ("authorized", 401), ("no-route", 404) })
        {
            using var response = await http.GetAsync("api/contract/" + path);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Check((int)response.StatusCode == status && body.RootElement.GetProperty("success").GetBoolean() == (status < 400)
                && body.RootElement.TryGetProperty("message", out _), path + " returns the correct HTTP status and ApiResponse");
            Check(!body.RootElement.TryGetProperty("detail", out _) && !body.RootElement.GetRawText().Contains("private diagnostic"), path + " does not expose internal exception details");
            if (status == 200) Check(body.RootElement.GetProperty("data").GetProperty("name").GetString() == "fixture", path + " wraps the DTO exactly once");
        }
        using (var response = await http.PostAsync("api/contract/validate", new StringContent("{}", Encoding.UTF8, "application/json")))
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Check(response.StatusCode == HttpStatusCode.BadRequest && body.RootElement.GetProperty("code").GetString() == "validation_failed"
                && body.RootElement.TryGetProperty("errors", out _), "automatic model validation uses ApiResponse and field errors");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "api/contract/admin"))
        {
            request.Headers.Add("X-Contract-User", "member");
            using var response = await http.SendAsync(request);
            Check(response.StatusCode == HttpStatusCode.Forbidden && (await response.Content.ReadAsStringAsync()).Contains("\"success\":false"), "authenticated authorization denial returns 403 with a JSON envelope");
        }
        using (var response = await http.GetAsync("api/contract/file"))
            Check(await response.Content.ReadAsStringAsync() == "{\"recording\":true}" && response.Content.Headers.ContentDisposition?.FileName == "fixture.json", "JSON file downloads retain file content and disposition");
        using (var response = await http.GetAsync("openapi/v1.json"))
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var schema = document.RootElement.GetProperty("paths").GetProperty("/api/contract/typed").GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
            var schemaText = schema.GetRawText();
            if (schema.TryGetProperty("$ref", out var reference))
                schemaText = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1]).GetRawText();
            Check(schemaText.Contains("success") && schemaText.Contains("data"), "OpenAPI documents the actual ApiResponse contract");
        }
        var client = new ApiClient(new ContractTransport(http));
        var dto = await client.SendAsync<ContractDto>("GET", "api/contract/success");
        Check(dto.Name == "fixture", "the actual Unity ApiClient unwraps server DTOs");
        bool missing = false;
        try { await client.SendAsync<ContractDto>("GET", "api/contract/missing"); }
        catch (ApiException error) { missing = error.StatusCode == 404 && error.Message == "fixture missing"; }
        Check(missing, "the actual Unity ApiClient preserves HTTP status and service messages");
        await app.StopAsync();
        Console.WriteLine($"SUCCESS: {checks} API, architecture and Unity client contract checks; no database used.");
    }
    sealed class ContractTransport(HttpClient client) : IApiTransport
    {
        public async Task<ApiHttpResponse> SendAsync(string method, string path, string json, string bearerToken, CancellationToken cancellation)
        {
            using var response = await client.GetAsync(path, cancellation);
            return new ApiHttpResponse((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellation));
        }
    }
}

public sealed class ContractDto { public string Name { get; set; } = "fixture"; }
public sealed class ContractInput { [Required] public string? Name { get; set; } }

[ApiController]
[Route("api/contract")]
public sealed class ContractController : ControllerBase
{
    [HttpGet("success")] public IActionResult Success() => Ok(new ContractDto());
    [HttpGet("typed")] public ContractDto Typed() => new();
    [HttpGet("already-wrapped")] public IActionResult Wrapped() => Ok(ApiResponse<ContractDto>.SuccessResponse(new()));
    [HttpGet("missing")] public IActionResult Missing() => throw new KeyNotFoundException("fixture missing");
    [HttpGet("forbidden")] public IActionResult Forbidden() => throw new UnauthorizedAccessException("fixture denied");
    [HttpGet("conflict")] public IActionResult ConflictFixture() => throw new AppException("fixture conflict", HttpStatusCode.Conflict);
    [HttpGet("failure")] public IActionResult Failure() => throw new InvalidOperationException("private diagnostic");
    [Authorize, HttpGet("authorized")] public IActionResult Authorized() => Ok(new ContractDto());
    [Authorize(Roles = "Admin"), HttpGet("admin")] public IActionResult Admin() => Ok(new ContractDto());
    [HttpPost("validate")] public IActionResult Validate(ContractInput input) => Ok(input);
    [FileResponse, HttpGet("file")] public IActionResult Download() => File(Encoding.UTF8.GetBytes("{\"recording\":true}"), "application/json", "fixture.json");
}

public sealed class ContractAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(Request.Headers.ContainsKey("X-Contract-User")
        ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "fixture"), new Claim(ClaimTypes.Role, "Player")], "contract")), "contract"))
        : AuthenticateResult.NoResult());
}
