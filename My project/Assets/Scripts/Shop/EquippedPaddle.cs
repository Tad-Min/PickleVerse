using System;
using UnityEngine;

namespace PickleVerse.Shop
{
    /// <summary>Local player/menu binding. Network replicas use the replicated skin id instead.</summary>
    public sealed class EquippedPaddle : MonoBehaviour
    {
        public PaddleAppearance appearance;
        IPlayerEconomy economy; int generation;
        void OnEnable()
        {
            economy = PlayerEconomy.Service;
            economy.Changed += Refresh;
            RefreshNow();
        }
        void Refresh(PlayerEconomyState state)
        {
            if (appearance)
                appearance.Apply(state.equippedPaddle);
        }
        void OnDisable()
        {
            generation++;
            if (economy != null)
                economy.Changed -= Refresh;
            economy = null;
        }
        public static EquippedPaddle Attach(GameObject paddle)
        {
            var binding = paddle.GetComponent<EquippedPaddle>();
            if (!binding)
            {
                binding = paddle.AddComponent<EquippedPaddle>();
                binding.appearance = PaddleAppearance.Attach(paddle);
            }
            if (!binding.appearance)
                binding.appearance = PaddleAppearance.Attach(paddle);
            // AddComponent invokes OnEnable before serialized references are assigned.
            binding.RefreshNow();
            return binding;
        }
        async void RefreshNow()
        {
            int run = ++generation;
            var provider = economy ?? PlayerEconomy.Service;
            // Lobby characters are enabled before login. The Changed subscription supplies
            // the real selection when the authenticated inventory has finished loading.
            if (provider is OnlinePlayerEconomy)
            {
                var runtime = PickleVerse.Progression.ProgressionRuntime.Instance;
                if (!runtime || !runtime.Ready)
                {
                    Refresh(new PlayerEconomyState());
                    return;
                }
            }
            try
            {
                var state = await provider.ReadAsync();
                if (this && isActiveAndEnabled && generation == run)
                    Refresh(state);
            }
            catch (Exception e)
            {
                if (this && isActiveAndEnabled && generation == run)
                    Debug.LogWarning("Paddle selection unavailable: " + e.Message);
            }
        }
    }
}
