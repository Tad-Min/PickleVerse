using System.Text;
using Entities.Configuration;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Services;
using API.Filters;
using API.OpenApi;
using Entities.DTOs.Common;
using Services.Interfaces;
using API.Middlewares;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// The API composition root owns configuration binding and Redis connection setup.
var redisSettings = builder.Configuration.GetSection("Redis").Get<RedisSettings>()
    ?? throw new InvalidOperationException("Redis configuration is required.");
redisSettings.Validate();
builder.Services.AddSingleton(redisSettings);
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisSettings.ConnectionString));

builder.Services.AddPickleVerseServices(builder.Configuration);

// 1. Add Controllers
builder.Services.AddControllers(options => options.Filters.Add<ApiResponseFilter>()).AddJsonOptions(options =>
{
    // The shared Unity game models use serializable fields and named enums.
    options.JsonSerializerOptions.IncludeFields = true;
    options.JsonSerializerOptions.IgnoreReadOnlyProperties = true;
    options.JsonSerializerOptions.Converters.Add(new Entities.Enums.GameplayEnumConverter());
});
builder.Services.AddRequestDecompression();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminWebPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://127.0.0.1:5173")
              .WithExposedHeaders("Content-Disposition", "X-Dataset-Sha256")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3. Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "PickleVerse_Super_Secret_Key_2026_DotNet10_Advanced_Capstone!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PickleVerseAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PickleVerseClient";
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must contain at least 32 characters.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (!Guid.TryParse(principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ||
                    !int.TryParse(principal?.FindFirst("auth_version")?.Value, out int version) ||
                    !await context.HttpContext.RequestServices.GetRequiredService<IAuthService>().ValidateSessionAsync(id, version,
                        principal?.FindFirst("access_scope")?.Value ?? "game"))
                    context.Fail("Phiên đăng nhập đã hết hiệu lực.");
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(ApiResponse.ErrorResponse("Quá nhiều yêu cầu. Vui lòng thử lại sau 60 giây.", "rate_limited"), cancellationToken);
    };
});
// 5. OpenAPI
builder.Services.AddOpenApi(options => options.AddOperationTransformer<ApiResponseOperationTransformer>());

// 6. Global Error Handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// 6. Auto-create S3 Bucket on Startup
using (var scope = app.Services.CreateScope())
{
    var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
    await storageService.EnsureBucketExistsAsync();
}

// 7. Global Exception Handler Pipeline
app.UseExceptionHandler();
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    if (context.Request.Path.StartsWithSegments("/api"))
        await context.Response.WriteAsJsonAsync(ApiResponse.ErrorResponse(ApiResponseFilter.DefaultMessage(context.Response.StatusCode)));
});
app.UseRequestDecompression();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "PickleVerse API v1");
        options.DocumentTitle = "PickleVerse API";
    });
}

// Docker dev exposes HTTP; deployed environments must terminate TLS.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("AdminWebPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<RestrictedSessionMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health/ready", async (IReadinessService readiness, CancellationToken ct) =>
    await readiness.Check(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));

app.Run();
