using System;
using UnityEngine;

namespace PickleVerse.Shop
{
    /// <summary>Local player/menu binding. Network replicas use the replicated skin id instead.</summary>
    public sealed class EquippedPaddle : MonoBehaviour
    {
        public PaddleAppearance appearance;
        IPlayerEconomy economy;int generation;
        async void OnEnable()
        {
            int run=++generation;economy=PlayerEconomy.Service;economy.Changed+=Refresh;
            try{var state=await economy.ReadAsync();if(this&&isActiveAndEnabled&&generation==run)Refresh(state);}
            catch(Exception e){Debug.LogWarning("Paddle selection unavailable: "+e.Message);}
        }
        void Refresh(PlayerEconomyState state){if(appearance)appearance.Apply(state.equippedPaddle);}
        void OnDisable(){generation++;if(economy!=null)economy.Changed-=Refresh;economy=null;}
        public static EquippedPaddle Attach(GameObject paddle)
        {
            var binding=paddle.GetComponent<EquippedPaddle>();
            if(!binding){binding=paddle.AddComponent<EquippedPaddle>();binding.appearance=PaddleAppearance.Attach(paddle);}
            if(!binding.appearance)binding.appearance=PaddleAppearance.Attach(paddle);
            // AddComponent invokes OnEnable before serialized references are assigned.
            binding.RefreshNow();return binding;
        }
        async void RefreshNow(){try{var state=await PlayerEconomy.Service.ReadAsync();if(this&&isActiveAndEnabled)Refresh(state);}catch(Exception e){Debug.LogWarning(e.Message);}}
    }
}
