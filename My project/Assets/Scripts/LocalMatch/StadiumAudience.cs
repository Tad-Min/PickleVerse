using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.LocalMatch
{
    /// <summary>Local presentation of authoritative rally events; never affects match simulation.</summary>
    public sealed class StadiumAudience : MonoBehaviour
    {
        static readonly List<StadiumAudience> active = new List<StadiumAudience>();
        AudienceSpectator[] spectators;
        float elapsed;
        public int SpectatorCount => spectators == null ? GetComponentsInChildren<AudienceSpectator>(true).Length : spectators.Length;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        public static void Ensure(StadiumEnvironment stadium, CourtVenue venue)
        {
            if (!stadium || stadium.GetComponentInChildren<StadiumAudience>(true)) return;
            var prefab = Resources.Load<GameObject>("Audience/" + (venue == CourtVenue.LowPoly ? "LowPoly" : "Professional"));
            if (prefab) Instantiate(prefab, stadium.transform, false);
        }
        void OnEnable()
        {
            if (!Application.isPlaying) return;
            active.Add(this); Initialize();
        }
        public void Initialize()
        {
            spectators = GetComponentsInChildren<AudienceSpectator>(true);
            foreach (var spectator in spectators)
            {
                bool enabled = !Application.isMobilePlatform || spectator.showOnMobile;
                spectator.gameObject.SetActive(enabled);
                if (enabled) spectator.Initialize();
            }
        }
        void Update()
        {
            elapsed += Time.deltaTime;
            float interval = Application.isMobilePlatform ? 1f / 20 : 1f / 30;
            if (elapsed < interval || spectators == null) return;
            Advance(elapsed); elapsed = 0;
        }
        public void Advance(float dt, bool forceEvaluate = false)
        {
            if (spectators == null) return;
            foreach (var spectator in spectators)
                if (spectator.isActiveAndEnabled) spectator.Step(dt, forceEvaluate || spectator.Visible);
        }
        public void React(int winningTeam, bool pointAwarded, bool matchFinished)
        {
            if (spectators == null) Initialize();
            foreach (var spectator in spectators) if (spectator.isActiveAndEnabled)
                spectator.React(winningTeam, pointAwarded, matchFinished);
        }
        public static void Rally(Scene scene, int winningTeam, bool pointAwarded, bool matchFinished)
        {
            foreach (var audience in active) if (audience && audience.gameObject.scene == scene)
                audience.React(winningTeam, pointAwarded, matchFinished);
        }
        public static void Restart(Scene scene)
        {
            foreach (var audience in active) if (audience && audience.gameObject.scene == scene) audience.Initialize();
        }
        void OnDisable()
        {
            active.Remove(this);
            if (spectators != null) foreach (var spectator in spectators) if (spectator) spectator.DisposeGraph();
        }
    }
}
