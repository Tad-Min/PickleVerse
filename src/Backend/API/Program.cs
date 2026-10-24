using StackExchange.Redis;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Repositories.Context;
using Repositories.Implementations;
using Repositories.Interfaces;
using Repositories.UnitOfWork;
using Services.Implementations;
using Services.Interfaces;
using API.Middlewares;
using System.Threading.RateLimiting;
using Services.Game;
using Services.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new ServerSettings(builder.Configuration));
builder.Services.AddSingleton(new RedisSettings(builder.Configuration));
builder.Services.AddSingleton<IConnectionMultiplexer>(sp=>ConnectionMultiplexer.Connect(sp.GetRequiredService<RedisSettings>().ConnectionString));
builder.Services.AddSingleton<RedisGameState>();
builder.Services.AddSingleton<LeaderboardCache>();

// 1. Add Controllers
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // The shared Unity game models use serializable fields and named enums.
    options.JsonSerializerOptions.IncludeFields = true;
    options.JsonSerializerOptions.IgnoreReadOnlyProperties = true;
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminWebPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 2. Configure PostgreSQL DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<PickleVerseContext>(options =>
    options.UseNpgsql(connectionString));

// 3. Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "PickleVerse_Super_Secret_Key_2026_DotNet10_Advanced_Capstone!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PickleVerseAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PickleVerseClient";
if (jwtKey.Length < 32) throw new InvalidOperationException("Jwt:Key must contain at least 32 characters.");

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
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Quá nhiều yêu cầu. Vui lòng thử lại sau 60 giây." }, cancellationToken);
    };
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Services.Game.OnlineSocialService>();
builder.Services.AddScoped<RoomInvitationService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// 4. Register Repositories & Unit of Work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPlayerProfileRepository, PlayerProfileRepository>();
builder.Services.AddScoped<IEconomyRepository, EconomyRepository>();
builder.Services.AddScoped<ITrainingRepository, TrainingRepository>();
builder.Services.AddScoped<IAiCoachingRepository, AiCoachingRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IFriendRepository, FriendRepository>();
builder.Services.AddScoped<IMissionRepository, MissionRepository>();
builder.Services.AddScoped<IAchievementRepository, AchievementRepository>();

// 5. Register Application Services
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ITrainingService, TrainingService>();
builder.Services.AddScoped<IAiCoachingService, AiCoachingService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<IEconomyService, EconomyService>();
builder.Services.AddScoped<IMissionService, MissionService>();
builder.Services.AddScoped<IAchievementService, AchievementService>();
builder.Services.AddScoped<IReplayService, ReplayService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<GameSql>();
builder.Services.AddScoped<GameCatalogService>();
builder.Services.AddScoped<OnlineGameService>();
builder.Services.AddScoped<OnlineReplayService>();

// 5. OpenAPI
builder.Services.AddOpenApi();

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
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseCors("AdminWebPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health/ready", async (PickleVerseContext db,IConnectionMultiplexer redis,CancellationToken ct) =>
{
    try
    {
        if(!await db.Database.CanConnectAsync(ct))return Results.StatusCode(503);
        await redis.GetDatabase().PingAsync();return Results.Ok(new { status="ready" });
    }
    catch(RedisException){return Results.StatusCode(503);}
});

app.Run();
