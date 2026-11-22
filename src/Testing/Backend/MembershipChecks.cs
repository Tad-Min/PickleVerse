using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using API.Controllers;
using API.Filters;
using API.Middlewares;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PickleVerse.Multiplayer;
using Repositories.Context;
using Services.Configuration;
using Services.Implementations;

static class MembershipChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        var fixture = new FunctionalFixture(options);
        Guid buyer = await fixture.Player(), free = await fixture.Player();
        var clock = new TestClock();
        var settings = new PayOsSettings
        {
            Enabled = true, ClientId = "local-test-client", ApiKey = "local-test-key",
            ChecksumKey = "local-checksum-for-simulated-payments",
            ReturnUrl = "https://example.invalid/membership/payment", CancelUrl = "https://example.invalid/membership/payment"
        };
        check(new PayOsSettings().CheckoutLifetimeMinutes == 15, "PayOS checkout defaults to 15 minutes without an override");
        foreach (int invalid in new[] { 0, -1, int.MaxValue })
        {
            settings.CheckoutLifetimeMinutes = invalid;
            bool rejected = false;
            try { settings.Validate(); }
            catch (InvalidOperationException error) when (error.Message.Contains(nameof(settings.CheckoutLifetimeMinutes))) { rejected = true; }
            check(rejected, $"startup rejects QR expiry {invalid} outside the supported range");
        }
        settings.CheckoutLifetimeMinutes = 5;
        settings.Validate();
        using var provider = new Provider(settings, clock, check);
        var payos = new PayOsGateway(provider, settings);
        CheckPublishedSignature(provider, check);
        async Task<T> Call<T>(Func<IMembershipService, Task<T>> work)
        {
            await using var db = new PickleVerseContext(options);
            using var unit = new UnitOfWork(db);
            return await work(new MembershipService(unit, payos, settings, clock));
        }
        Task Notify(JsonElement json) => Call(async service => { await service.ReceiveWebhook(json, default); return true; });
        async Task Reject(Func<Task> work, HttpStatusCode status, string name)
        {
            try { await work(); }
            catch (AppException error) when (error.StatusCode == status) { check(true, name); return; }
            check(false, name);
        }
        var initial = await Call(service => service.Read(buyer));
        check(!initial.Active && initial.PriceVnd == 49000 && initial.EliteTokenBonus == 3000 && initial.PendingCheckout == null,
            "the advertised lifetime offer costs 49,000 VND and grants 3,000 Elite Token");
        var duplicates = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => Call(service => service.Checkout(buyer, default))));
        var order = duplicates[0].PendingCheckout!;
        check(duplicates.All(offer => offer.PendingCheckout?.OrderCode == order.OrderCode) && provider.Created == 1,
            "concurrent clicks reuse one PayOS checkout for the account");
        check(new DateTimeOffset(order.ExpiresAt).ToUnixTimeSeconds() == clock.GetUtcNow().AddMinutes(5).ToUnixTimeSeconds()
            && order.ExpiresAt.Ticks % TimeSpan.TicksPerSecond == 0,
            "the configured five-minute expiration is stored and returned to the game at PayOS precision");
        settings.CheckoutLifetimeMinutes = 10;
        var existing = (await Call(service => service.Checkout(buyer, default))).PendingCheckout!;
        check(existing.OrderCode == order.OrderCode && existing.ExpiresAt == order.ExpiresAt && provider.Created == 1,
            "changing the configured lifetime preserves the deadline of an existing checkout");
        check((await Call(service => service.Read(free))).PendingCheckout == null,
            "pending checkout is scoped to the authenticated player's account");
        int providerReads = provider.Reads;
        await Reject(() => Notify(provider.Webhook(order.OrderCode, signature: new string('0', 64))),
            HttpStatusCode.Forbidden, "a forged webhook cannot query payment or grant membership");
        await Notify(provider.Webhook(order.OrderCode, linkId: Guid.NewGuid().ToString("N")));
        check(provider.Reads == providerReads, "a signed callback for another link cannot affect this order");
        await Notify(provider.Webhook(order.OrderCode));
        check(!(await Call(service => service.Read(buyer))).Active, "a valid callback cannot activate an unpaid PayOS order");
        provider.Set(order.OrderCode, payment => payment with { Status = "PAID", AmountPaid = 1000, AmountRemaining = 48000 });
        await Reject(() => Notify(provider.Webhook(order.OrderCode)), HttpStatusCode.BadGateway, "partial payment never activates membership");
        provider.Set(order.OrderCode, payment => payment with { Amount = 1000, AmountRemaining = 0 });
        await Reject(() => Notify(provider.Webhook(order.OrderCode)), HttpStatusCode.BadGateway, "a mismatched amount never activates membership");
        provider.Set(order.OrderCode, payment => payment with { Amount = 49000, AmountPaid = 49000, AmountRemaining = 0 });
        await Task.WhenAll(
            Notify(provider.Webhook(order.OrderCode)),
            Notify(provider.Webhook(order.OrderCode)),
            Call(service => service.Refresh(buyer, default)));
        var paid = await Call(service => service.Read(buyer));
        var state = await fixture.Game(game => game.Read(buyer));
        check(paid.Active && paid.PendingCheckout == null && state.hasMembership && state.inventory.elite == 3000,
            "payment and the full wallet credit are committed together");
        await using (var db = new PickleVerseContext(options))
        {
            var credits = await db.CurrencyTransactions.Where(t => t.PlayerId == buyer && t.Reason == "lifetime-membership").ToListAsync();
            check(credits.Count == 1 && credits[0].Delta == 3000 && credits[0].BalanceAfter == 3000,
                "simultaneous webhook delivery and game reconciliation write exactly one currency credit");
        }
        check((await Call(service => service.Checkout(buyer, default))).Active && provider.Created == 1,
            "a paid account cannot create a second lifetime checkout");

        var old = (await Call(service => service.Checkout(free, default))).PendingCheckout!;
        check(new DateTimeOffset(old.ExpiresAt).ToUnixTimeSeconds() == clock.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds(),
            "new checkouts use the updated ten-minute lifetime");
        provider.Set(old.OrderCode, payment => payment with { Status = "EXPIRED" });
        clock.Now = clock.Now.AddMinutes(16);
        var replacement = (await Call(service => service.Checkout(free, default))).PendingCheckout!;
        check(replacement.OrderCode != old.OrderCode && !((await Call(s => s.Read(free))).Active),
            "an expired unpaid order can be replaced without granting any rights");
        await Http(options, provider, settings, clock, buyer, free, order.OrderCode, check);

        var missedWebhook = await fixture.Player();
        var recoveredOrder = (await Call(service => service.Checkout(missedWebhook, default))).PendingCheckout!;
        provider.Set(recoveredOrder.OrderCode, payment => payment with { Status = "PAID", AmountPaid = 49000, AmountRemaining = 0 });
        clock.Now = clock.Now.AddMinutes(16);
        var recovered = await Call(service => service.Refresh(missedWebhook, default));
        check(recovered.Active && (await fixture.Game(game => game.Read(missedWebhook))).inventory.elite == 3000,
            "the game recovers a completed PayOS purchase even after the checkout deadline when the webhook was missed");
    }

    static void CheckPublishedSignature(Provider provider, Action<bool, string> check)
    {
        // Public test vector from https://payos.vn/docs/tich-hop-webhook/kiem-tra-du-lieu-voi-signature/
        var settings = new PayOsSettings
        {
            Enabled = true,
            ChecksumKey = "1a54716c8f0efb2744fb28b6e38b25da7f67a925d98bc1c18bd8faaecadd7675"
        };
        using var fixture = JsonDocument.Parse("""
            {
                "code":"00","success":true,
                "data":{
                    "orderCode":123,"amount":3000,"description":"VQRIO123","accountNumber":"12345678",
                    "reference":"TF230204212323","transactionDateTime":"2023-02-04 18:25:00","currency":"VND",
                    "paymentLinkId":"124c33293c43417ab7879e14c8d9eb18","code":"00","desc":"Thành công",
                    "counterAccountBankId":"","counterAccountBankName":"","counterAccountName":"",
                    "counterAccountNumber":"","virtualAccountName":"","virtualAccountNumber":""
                },
                "signature":"412e915d2871504ed31be63c8f62a149a4410d34c4c42affc9006ef9917eaa03"
            }
            """);
        check(new PayOsGateway(provider, settings).VerifyWebhook(fixture.RootElement)?.OrderCode == 123,
            "PayOS's published webhook signature verifies with Vietnamese text");
    }

    static async Task Http(DbContextOptions<PickleVerseContext> options, Provider provider, PayOsSettings settings,
        TimeProvider clock, Guid buyer, Guid free, long paidOrder, Action<bool, string> check)
    {
        var gameSettings = TestSettings.Load();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<IPayOsGateway>(new PayOsGateway(provider, settings));
        builder.Services.AddSingleton(clock);
        builder.Services.AddScoped(_ => new PickleVerseContext(options));
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IMembershipService, MembershipService>();
        builder.Services.AddScoped<IGameMatchService>(services =>
        {
            var unit = services.GetRequiredService<IUnitOfWork>();
            return new OnlineGameService(unit, new GameCatalogService(unit, gameSettings), clock, gameSettings,
                TestRedis.State, TestRedis.Leaderboard, new MatchReplayService(unit, new TestReplayStorage(), NullLogger<MatchReplayService>.Instance));
        });
        builder.Services.AddControllers(config => config.Filters.Add<ApiResponseFilter>()).AddApplicationPart(typeof(MembershipController).Assembly)
            .AddJsonOptions(json =>
            {
                json.JsonSerializerOptions.IncludeFields = true;
                json.JsonSerializerOptions.Converters.Add(new GameplayEnumConverter());
            });
        builder.Services.AddAuthentication("membership-test").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("membership-test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("auth", limiter =>
        { limiter.PermitLimit = 20; limiter.Window = TimeSpan.FromMinutes(1); }));
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, Guid? user, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (user.HasValue) request.Headers.Add("X-Membership-Fixture", user.Value.ToString());
            if (body != null) request.Content = JsonContent.Create(body);
            return await http.SendAsync(request);
        }
        using (var anonymous = await Send(HttpMethod.Post, "/api/Game/membership/checkout", null))
            check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "HTTP checkout requires login");
        using (var webhook = await Send(HttpMethod.Post, "/api/payments/payos/webhook", null, provider.Webhook(paidOrder)))
            check(webhook.IsSuccessStatusCode, "the public PayOS webhook acknowledges signed notifications without a game session");
        using (var offer = await Send(HttpMethod.Get, "/api/Game/membership", buyer))
        {
            var result = await offer.Content.ReadFromJsonAsync<JsonElement>();
            check(offer.IsSuccessStatusCode && result.GetProperty("success").GetBoolean()
                && result.GetProperty("data").GetProperty("active").GetBoolean(), "HTTP offer exposes paid status through ApiResponse");
        }
        Guid search = Guid.NewGuid();
        using (var rejected = await Send(HttpMethod.Post, "/api/Game/ranked/search", free, new { searchId = search, region = "asia" }))
            check(rejected.StatusCode == HttpStatusCode.Forbidden, "free player cannot start online matchmaking through HTTP");
        using (var accepted = await Send(HttpMethod.Post, "/api/Game/ranked/search", buyer, new { searchId = search, region = "asia" }))
            check(accepted.IsSuccessStatusCode, "paid player can start online matchmaking through HTTP");
        using (var cancel = await Send(HttpMethod.Post, "/api/Game/ranked/cancel", buyer, new { searchId = search }))
            check(cancel.IsSuccessStatusCode, "the paid player's search can be cancelled normally");
        var room = new { hostPlayerId = buyer.ToString("N"), session = "membership-check-room",
            roster = new { matchId = Guid.NewGuid().ToString("N"), custom = true, mode = 1,
                members = new[] { new { id = buyer.ToString("N"), team = 0 }, new { id = free.ToString("N"), team = 1 } } } };
        using (var denied = await Send(HttpMethod.Post, "/api/Game/match/online/prepare", buyer, room))
            check(denied.StatusCode == HttpStatusCode.Forbidden, "a paid host cannot start a private match with an unpaid player");
        using (var denied = await Send(HttpMethod.Post, "/api/Game/match/bot/start", free, new { requestId = Guid.NewGuid(), difficulty = 3 }))
            check(denied.StatusCode == HttpStatusCode.Forbidden, "adaptive bot HTTP start requires membership");
        for (int level = 0; level < 3; level++)
        {
            using var allowed = await Send(HttpMethod.Post, "/api/Game/match/bot/start", free, new { requestId = Guid.NewGuid(), difficulty = level });
            check(allowed.IsSuccessStatusCode, $"fixed bot difficulty {level} remains free through HTTP");
        }
        using (var allowed = await Send(HttpMethod.Post, "/api/Game/match/bot/start", buyer, new { requestId = Guid.NewGuid(), difficulty = 3 }))
            check(allowed.IsSuccessStatusCode, "membership permits adaptive bot HTTP start");
    }

    sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Membership-Fixture"], out var player))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, player.ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    sealed class Provider(PayOsSettings settings, TimeProvider clock, Action<bool, string> check) : HttpMessageHandler, IHttpClientFactory
    {
        readonly ConcurrentDictionary<long, PayOsPayment> orders = new();
        int created, reads;
        public int Created => created;
        public int Reads => reads;
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false)
            { BaseAddress = new Uri("https://api-merchant.payos.vn/") };
        public void Set(long order, Func<PayOsPayment, PayOsPayment> change) =>
            orders.AddOrUpdate(order, _ => throw new InvalidOperationException(), (_, value) => change(value));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri?.Host != "api-merchant.payos.vn" ||
                request.Headers.GetValues("x-client-id").Single() != settings.ClientId ||
                request.Headers.GetValues("x-api-key").Single() != settings.ApiKey)
                throw new InvalidOperationException("Unexpected merchant request.");
            if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == "/v2/payment-requests")
            {
                var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
                int amount = body.GetProperty("amount").GetInt32();
                long code = body.GetProperty("orderCode").GetInt64();
                string description = body.GetProperty("description").GetString()!;
                var expected = FormattableString.Invariant($"amount=49000&cancelUrl={settings.CancelUrl}&description={description}&orderCode={code}&returnUrl={settings.ReturnUrl}");
                check(body.GetProperty("signature").GetString() == Sign(expected) && amount == 49000 &&
                    body.GetProperty("expiredAt").GetInt64() == clock.GetUtcNow().AddMinutes(settings.CheckoutLifetimeMinutes).ToUnixTimeSeconds(),
                    "PayOS request contains the server price, the configured expiry and the documented HMAC");
                string id = Guid.NewGuid().ToString("N");
                if (!orders.TryAdd(code, new(code, id, amount, 0, amount, "PENDING"))) throw new InvalidOperationException("Duplicate provider order.");
                Interlocked.Increment(ref created);
                return Response(new { amount, orderCode = code, paymentLinkId = id, checkoutUrl = "https://pay.payos.vn/web/" + id, currency = "VND", status = "PENDING" });
            }
            if (request.Method == HttpMethod.Get &&
                long.TryParse(request.RequestUri.Segments.Last(), CultureInfo.InvariantCulture, out long order))
            {
                Interlocked.Increment(ref reads);
                var payment = orders[order];
                string transactions = payment.AmountPaid == 0 ? "[]" :
                    FormattableString.Invariant($$"""[{"amount":{{payment.AmountPaid}},"description":"Thanh toán gói","reference":"FIXTURE"}]""");
                using var rows = JsonDocument.Parse(transactions);
                return Response(new { id = payment.PaymentLinkId, orderCode = order, amount = payment.Amount,
                    amountPaid = payment.AmountPaid, amountRemaining = payment.AmountRemaining, status = payment.Status,
                    cancellationReason = (string?)null, transactions = rows.RootElement }, transactions);
            }
            throw new InvalidOperationException("Unexpected PayOS request path.");
        }

        public JsonElement Webhook(long order, string? signature = null, string? linkId = null)
        {
            var data = JsonSerializer.SerializeToElement(new
            { orderCode = order, paymentLinkId = linkId ?? orders[order].PaymentLinkId, code = "00", currency = "VND", description = "Thanh toán gói" });
            return JsonSerializer.SerializeToElement(new { code = "00", success = true, data, signature = signature ?? Signature(data) });
        }
        HttpResponseMessage Response(object value, string? transactions = null)
        {
            var data = JsonSerializer.SerializeToElement(value);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { code = "00", data, signature = Signature(data, transactions) }) };
        }
        string Signature(JsonElement data, string? transactions = null) => Sign(string.Join("&",
            data.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => $"{p.Name}={(p.Name == "transactions" ? transactions : p.Value.ToString())}")));
        string Sign(string text) => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.ChecksumKey), Encoding.UTF8.GetBytes(text)));
    }
}
