using TMPro;
using UnityEngine;

namespace PickleVerse.Shop
{
    public sealed class WalletBar : MonoBehaviour
    {
        public TMP_Text tokenpad,elite;
        IPlayerEconomy economy;
        async void OnEnable()
        {
            economy=PlayerEconomy.Service;economy.Changed+=Refresh;
            var state=await economy.ReadAsync();if(this&&isActiveAndEnabled)Refresh(state);
        }
        void Refresh(PlayerEconomyState wallet){tokenpad.text=wallet.tokenpad.ToString("N0");elite.text=wallet.elite.ToString("N0");}
        void OnDisable(){if(economy!=null)economy.Changed-=Refresh;}
    }
}
