using NUnit.Framework;
using PickleVerse.Shop;
using UnityEngine;
using UnityEngine.TestTools;

namespace PickleVerse.Tests
{
    public sealed class EquippedPaddleLifecycleTests
    {
        [Test]
        public void LobbyPaddleCanEnableBeforeAuthenticatedInventoryIsReady()
        {
            var previous=PlayerEconomy.Service;
            var online=new OnlinePlayerEconomy();
            var host=new GameObject("Paddle before login");
            host.SetActive(false);
            try
            {
                PlayerEconomy.Configure(online);
                host.AddComponent<EquippedPaddle>();
                host.SetActive(true);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(host);
                PlayerEconomy.Configure(previous);
                online.Dispose();
            }
        }
    }
}
