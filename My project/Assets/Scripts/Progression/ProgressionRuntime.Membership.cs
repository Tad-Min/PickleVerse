using System;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Services.Api;
using UnityEngine;

namespace PickleVerse.Progression
{
    public sealed partial class ProgressionRuntime
    {
        public const string MembershipRequiredMessage = "Hãy mua gói thành viên để mở khóa tính năng này.";
        const string MembershipRoute = "api/Game/membership";
        readonly SemaphoreSlim membershipRequests = new SemaphoreSlim(1, 1);
        MembershipOffer membershipOffer;
        int membershipSession = -1;
        bool checkingMembership, discoverMembership;
        double nextMembershipCheck, nextProviderCheck;

        public static event Action MembershipChanged;
        public bool HasMembership => IsAuthenticated && Snapshot.hasMembership;
        public MembershipOffer CurrentMembership => IsAuthenticated && membershipSession == account.SessionVersion ? membershipOffer : null;

        public void DiscoverMembership() => discoverMembership = true;
        public Task<MembershipOffer> ReadMembershipAsync(CancellationToken cancellation = default) =>
            RequestMembership("GET", MembershipRoute, cancellation);
        public Task<MembershipOffer> CheckoutMembershipAsync(CancellationToken cancellation = default) =>
            RequestMembership("POST", MembershipRoute + "/checkout", cancellation);

        async Task<MembershipOffer> RequestMembership(string method, string route, CancellationToken cancellation)
        {
            if (!IsAuthenticated) throw new InvalidOperationException("Đăng nhập để xem gói thành viên.");
            int session = account.SessionVersion;
            await membershipRequests.WaitAsync(cancellation);
            try
            {
                CheckMembershipSession(session);
                var result = await account.SendAuthorizedAsync<MembershipOffer>(method, route, cancellation: cancellation);
                CheckMembershipSession(session);
                if (result == null || result.priceVnd <= 0 || result.eliteTokenBonus < 0)
                    throw new InvalidOperationException("Thông tin gói thành viên không đầy đủ.");
                membershipOffer = result;
                membershipSession = session;
                nextMembershipCheck = Time.realtimeSinceStartupAsDouble + 3;
                discoverMembership = false;
                if (result.active && !HasMembership)
                {
                    // Refresh the server snapshot so the wallet and all mode gates update together.
                    try { await ReloadAsync(); }
                    catch (ApiException) when (IsAuthenticated && session == account.SessionVersion) { }
                    CheckMembershipSession(session);
                }
                MembershipChanged?.Invoke();
                return result;
            }
            finally { membershipRequests.Release(); }
        }

        void CheckMembershipSession(int session)
        {
            if (!this || !IsAuthenticated || session != account.SessionVersion)
                throw new OperationCanceledException("Phiên đăng nhập đã thay đổi.");
        }

        void TickMembership()
        {
            if (checkingMembership || !IsAuthenticated || !Application.isFocused) return;
            var offer = CurrentMembership;
            if (!discoverMembership && offer?.pendingCheckout == null && !(offer?.active == true && !HasMembership)) return;
            if (HasMembership) { discoverMembership = false; return; }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextMembershipCheck) return;
            bool reconcile = offer?.checkoutEnabled == true && offer.pendingCheckout != null && now >= nextProviderCheck;
            if (reconcile) nextProviderCheck = now + 30;
            _ = PollMembership(reconcile);
        }

        async Task PollMembership(bool reconcile)
        {
            checkingMembership = true;
            try
            {
                await RequestMembership(reconcile ? "POST" : "GET",
                    reconcile ? MembershipRoute + "/refresh" : MembershipRoute, default);
            }
            catch (Exception error) when (error is ApiException || error is OperationCanceledException || error is InvalidOperationException)
            { nextMembershipCheck = Time.realtimeSinceStartupAsDouble + 6; }
            finally { checkingMembership = false; }
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) return;
            nextMembershipCheck = 0;
            nextProviderCheck = 0;
        }
    }
}
