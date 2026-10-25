using UnityEngine;

namespace PickleVerse.Shop
{
    public sealed class ShopLauncher : MonoBehaviour
    {
        public void OpenShop()
        {
            LobbyNavigation.OpenShop();
        }
        public void OpenCollection()
        {
            LobbyNavigation.OpenCollection();
        }
    }
}
