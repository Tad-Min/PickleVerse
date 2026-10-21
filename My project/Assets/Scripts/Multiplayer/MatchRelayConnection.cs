#if PICKLEVERSE_LEGACY_NGO
using System;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using Unity.Services.Relay;

namespace PickleVerse.Multiplayer
{
    public static class MatchRelayConnection
    {
        // Matchmaker's hosting region is not necessarily a Relay region. Let Relay
        // select automatically if a measured region has disappeared from its catalog.
        public static async Task StartAsync(string region,Func<RelayNetworkOptions,Task> start,Func<bool> current)
        {
            if(!current())throw new OperationCanceledException();
            try {await start(new RelayNetworkOptions(string.IsNullOrWhiteSpace(region)?null:region,true));}
            catch(Exception e) when(!string.IsNullOrWhiteSpace(region) && RegionUnavailable(e) && current())
            {await start(new RelayNetworkOptions(null,true));}
            if(!current())throw new OperationCanceledException();
        }
        public static bool RegionUnavailable(Exception exception)
        {
            for(var e=exception;e!=null;e=e.InnerException)
                if(e is RelayServiceException relay && (relay.Reason==RelayExceptionReason.RegionNotFound ||
                    relay.Reason==RelayExceptionReason.InvalidArgument || relay.Reason==RelayExceptionReason.InvalidRequest))return true;
            return false;
        }
        public static string Describe(Exception exception)
        {
            var e=exception.GetBaseException();
            return e is RelayServiceException relay?$"{relay.Reason} ({relay.ErrorCode}): {relay.Message}":e.Message;
        }
    }
}
#endif
