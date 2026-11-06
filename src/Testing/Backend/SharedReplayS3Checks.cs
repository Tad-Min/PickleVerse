using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using PickleVerse.Replays;
using Repositories.Context;
using Services.Implementations;
using Services.Interfaces;
using static Repositories.Data.PostgresCommands;

static class SharedReplayS3Checks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options,Action<bool,string> check)
    {
        var storage=new StorageService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["Storage:ServiceUrl"]=Environment.GetEnvironmentVariable("PKV_TEST_S3_URL")??"http://127.0.0.1:19000",
            ["Storage:AccessKey"]="minioadmin",["Storage:SecretKey"]="minioadmin",["Storage:BucketName"]="pickleverse"}).Build(),NullLogger<StorageService>.Instance);
        var key=new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped(_=>new PickleVerseContext(options));builder.Services.AddScoped<PostgresCommands>();
        builder.Services.AddScoped<GameRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddSingleton<IStorageService>(storage);builder.Services.AddScoped<MatchReplayService>();
        builder.Services.AddScoped<IMatchReplayService>(sp => sp.GetRequiredService<MatchReplayService>());
        builder.Services.AddControllers(o=>o.Filters.Add<API.Filters.ApiResponseFilter>()).AddApplicationPart(typeof(API.Controllers.OnlineReplaysController).Assembly).AddJsonOptions(o=>{
            o.JsonSerializerOptions.IncludeFields=true;o.JsonSerializerOptions.IgnoreReadOnlyProperties=true;
            o.JsonSerializerOptions.Converters.Add(new Entities.Enums.GameplayEnumConverter());});
        builder.Services.AddExceptionHandler<API.Middlewares.GlobalExceptionHandler>();builder.Services.AddProblemDetails();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>o.TokenValidationParameters=new(){
            ValidateIssuerSigningKey=true,IssuerSigningKey=key,ValidateIssuer=true,ValidIssuer="replay-check",
            ValidateAudience=true,ValidAudience="replay-check",ValidateLifetime=true,ClockSkew=TimeSpan.Zero});
        builder.Services.AddAuthorization();
        await using var app=builder.Build();app.UseExceptionHandler();app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();
        var a=Guid.NewGuid();var b=Guid.NewGuid();var outsider=Guid.NewGuid();var match=Guid.NewGuid();
        await using var db=new PickleVerseContext(options);var sql=new PostgresCommands(db);var sqlData = new UnitOfWork(db);
        foreach(var id in new[]{a,b,outsider})
        {
            await sql.Run("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'",P("id",id),P("email",id+"@example.invalid"));
            await sql.Run("INSERT INTO playerprofiles(userid,playername) VALUES(@id,@name)",P("id",id),P("name","S3"+id.ToString("N")[..10]));
        }
        await sql.Run("INSERT INTO gamematches(matchid,kind,status) VALUES(@match,'Custom','PLAYING')",P("match",match));
        await sql.Run("INSERT INTO gamematchplayers(matchid,playerid,team,isreplaywriter,rosterindex) VALUES(@match,@a,0,true,0),(@match,@b,1,false,1)",P("match",match),P("a",a),P("b",b));
        var metadata=new ReplayMetadata{playerId=a.ToString("N"),matchId=match.ToString("N"),actors=[
            new(){id=a.ToString("N"),name="A",team=0,visualId="Male",bones=["Root"]},new(){id=b.ToString("N"),name="B",team=1,visualId="Female",bones=["Root"]}]};
        var chunk=new ReplayChunk{frames=[new(){time=0,ball=[0,1,0],actors=[new(){root=new float[10],bones=new float[10]},new(){root=new float[10],bones=new float[10]}]}]};
        using var compressed=new MemoryStream();
        using(var zip=new GZipStream(compressed,CompressionLevel.Fastest,true))zip.Write(Encoding.UTF8.GetBytes(GameJson.Write(chunk)));
        var bytes=compressed.ToArray();var hash=Convert.ToHexString(SHA256.HashData(bytes));
        var chunkKey=$"replays/{match:N}/000000-{hash}.json.gz";
        string route=$"api/Game/replays/{match:N}";
        HttpClient Client(Guid? id,string role="Player")
        {
            var client=new HttpClient{BaseAddress=new Uri(app.Urls.Single())};
            if(id!=null)
            {
                var token=new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("replay-check","replay-check",
                    [new Claim(ClaimTypes.NameIdentifier,id.ToString()!),new Claim(ClaimTypes.Role,role)],expires:DateTime.UtcNow.AddMinutes(10),signingCredentials:new(key,SecurityAlgorithms.HmacSha256)));
                client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
            }
            return client;
        }
        using var writer=Client(a);using var viewer=Client(b);using var stranger=Client(outsider);using var admin=Client(outsider,"Admin");using var anonymous=Client(null);
        StringContent Body<T>(T value)=>new(GameJson.Write(value),Encoding.UTF8,"application/json");
        try
        {
            using(var response=await anonymous.GetAsync(route))check(response.StatusCode==HttpStatusCode.Unauthorized,"shared replay HTTP requires authentication");
            using(var response=await writer.PutAsync(route,Body(metadata)))check(response.IsSuccessStatusCode,"designated recorder initializes manifest through HTTP and S3");
            var guest=GameJson.Read<ReplayMetadata>(GameJson.Write(metadata));guest.playerId=b.ToString("N");
            using(var response=await viewer.PutAsync(route,Body(guest)))check(response.StatusCode==HttpStatusCode.Forbidden,"participant JWT does not authorize overwriting the common recording");
            var responses=await Task.WhenAll(writer.PutAsync(route+"/chunks/0",Body(new ReplayChunkUpload{Data=Convert.ToBase64String(bytes)})),writer.PutAsync(route+"/chunks/0",Body(new ReplayChunkUpload{Data=Convert.ToBase64String(bytes)})));
            try { check(responses.All(r=>r.IsSuccessStatusCode),"concurrent HTTP upload retries succeed against real S3"); }
            finally {foreach(var response in responses)response.Dispose();}
            await sql.Run("UPDATE gamematches SET status='FINISHED',endedat=CURRENT_TIMESTAMP,sequence=nextval('gamematches_history_seq') WHERE matchid=@match",P("match",match));
            await MatchScoreStore.Save(db,match,[new(11,5,true),new(11,8,true)]);
            await sql.Run("UPDATE gamematchplayers SET finalizedat=CURRENT_TIMESTAMP,outcome=CASE WHEN team=0 THEN 'WIN' ELSE 'LOSE' END WHERE matchid=@match",P("match",match));
            metadata.complete=true;
            using(var response=await writer.PutAsync(route,Body(metadata)))check(response.IsSuccessStatusCode,"one match-history reference is published after recording completion");
            using(var response=await viewer.GetAsync(route))
            {
                response.EnsureSuccessStatusCode();var shared=GameJson.Read<Entities.DTOs.Common.ApiResponse<ReplayMetadata>>(await response.Content.ReadAsStringAsync()).Data!;
                check(shared.playerId==a.ToString("N") && shared.chunks.Count==1 && shared.chunks[0].contentHash==hash,"other participant receives the same S3 manifest and recorder identity");
            }
            using(var response=await viewer.GetAsync(route+"/chunks/0"))
            {response.EnsureSuccessStatusCode();check(GameJson.Read<Entities.DTOs.Common.ApiResponse<ReplayChunkUpload>>(await response.Content.ReadAsStringAsync()).Data!.Data==Convert.ToBase64String(bytes),"authenticated viewer downloads the shared S3 chunk");}
            using(var response=await stranger.GetAsync(route))check(response.StatusCode==HttpStatusCode.NotFound,"outsider HTTP request cannot read shared recording");
            using(var response=await admin.GetAsync($"api/Replays/{match:N}"))
            {response.EnsureSuccessStatusCode();check(response.Content.Headers.ContentType?.MediaType=="application/json" && GameJson.Read<ReplayMetadata>(await response.Content.ReadAsStringAsync()).matchId==match.ToString("N"),"admin evidence route serves the same manifest through authenticated API");}
            await using(var manifest=await storage.GetFileAsync(MatchReplayService.ManifestKey(match)))check(manifest!=null,"manifest object actually exists in development S3");
            check(await sql.One("SELECT objectkey FROM matchhistoryreplay WHERE matchid=@match",r=>r.GetString(0),P("match",match))==MatchReplayService.ManifestKey(match),"database stores a stable S3 key rather than a per-user copy or expiring URL");
        }
        finally
        {
            await app.StopAsync();
            if(!await storage.DeleteFileAsync(chunkKey) || !await storage.DeleteFileAsync(MatchReplayService.ManifestKey(match)))throw new IOException("Could not clean up owned S3 test objects.");
        }
    }
}
