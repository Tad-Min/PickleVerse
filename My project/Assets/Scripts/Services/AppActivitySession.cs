using System;
using System.Threading.Tasks;
using PickleVerse.Services.Api;

namespace PickleVerse.Services
{
    // Run on the caller's main thread, including continuations after the API calls.
    public sealed class AppActivitySession : IDisposable
    {
        [Serializable]
        sealed class Receipt { public long totalSeconds, elapsedSeconds; }

        readonly AccountService account;
        readonly ApiClient api;
        readonly Func<double> now;
        Guid sessionId = Guid.NewGuid();
        Guid? owner;
        double began, next;
        int ownerVersion;
        bool opened, closing, disposed;
        Receipt receipt;
        Task pending = Task.CompletedTask;

        public AppActivitySession(AccountService account, ApiClient api, Func<double> now)
        {
            this.account = account;
            this.api = api;
            this.now = now;
            owner = account.Profile?.UserId;
            account.Changed += AccountChanged;
        }

        public string Error { get; private set; }
        public double Seconds => receipt == null ? 0 : receipt.totalSeconds + Math.Max(0, Elapsed - receipt.elapsedSeconds);
        double Elapsed => Math.Max(0, now() - began);

        void AccountChanged()
        {
            var currentOwner = account.Profile?.UserId;
            if (owner.HasValue && owner != currentOwner)
            {
                ownerVersion++;
                RenewSession();
                receipt = null;
                closing = false;
                Error = null;
            }
            owner = currentOwner;
            next = 0;
        }

        void RenewSession()
        {
            sessionId = Guid.NewGuid();
            began = now();
            opened = false;
        }

        public void Tick()
        {
            if (disposed || closing || !pending.IsCompleted || now() < next)
                return;
            next = now() + 15;
            pending = SendAsync(false);
        }

        async Task SendAsync(bool close, bool renewExpired = true)
        {
            var key = sessionId;
            bool IsCurrent() => !disposed && key == sessionId;
            object Request(bool final) => new { SessionId = key, ElapsedSeconds = (long)Elapsed, Close = final };
            try
            {
                if (!opened)
                {
                    await api.SendAsync<Receipt>("POST", "api/Game/activity/open", Request(false));
                    if (!IsCurrent()) return;
                    opened = true;
                }
                if (account.IsSignedIn)
                {
                    var saved = await account.SendAuthorizedAsync<Receipt>("POST", "api/Game/activity/checkpoint", Request(close));
                    if (!IsCurrent()) return;
                    receipt = saved;
                }
                Error = null;
            }
            catch (ApiException error) when (error.StatusCode == 410 && renewExpired && IsCurrent())
            {
                // Preserve the confirmed total when Redis requires a fresh session.
                RenewSession();
                if (receipt != null)
                    receipt.elapsedSeconds = 0;
                await SendAsync(close, false);
            }
            catch (Exception error)
            {
                if (IsCurrent())
                    Error = error.Message;
            }
        }

        public Task FlushAsync(bool close = false)
        {
            if (disposed) return Task.CompletedTask;
            if (close) closing = true;
            pending = FlushAfterAsync(pending, ownerVersion, close);
            return pending;
        }

        async Task FlushAfterAsync(Task previous, int version, bool close)
        {
            await previous;
            if (!disposed && version == ownerVersion)
                await SendAsync(close);
        }

        public void Dispose()
        {
            disposed = true;
            account.Changed -= AccountChanged;
        }
    }
}
