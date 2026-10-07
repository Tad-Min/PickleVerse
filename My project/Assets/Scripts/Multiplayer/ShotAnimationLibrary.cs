using System;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    [CreateAssetMenu(menuName = "PickleVerse/Shot Animation Library")]
    public sealed class ShotAnimationLibrary : ScriptableObject
    {
        [Serializable] public class Entry
        {
            public string state, gender;
            public float contactNormalized = .45f, releaseNormalized = .28f;
            public float duration = .8f;
            public Vector3 contactLocal;
        }
        public Entry[] entries = Array.Empty<Entry>();
        public Entry Find(string gender, string state) => Array.Find(entries, e => e.gender == gender && e.state == state);
        public static string State(ShotKind kind, bool backhand)
        {
            string suffix = backhand ? "_BH" : "_FH";
            switch (kind)
            {
                case ShotKind.Serve: return "Serve_FH_Underhand";
                case ShotKind.Dink: return "Dink" + suffix;
                case ShotKind.Volley: case ShotKind.Punch: case ShotKind.ResetVolley: return "Volley" + suffix;
                case ShotKind.Drop: return "ThirdShotDrop" + suffix;
                case ShotKind.Smash: return "Overhead_Smash";
                default: return "Groundstroke" + suffix;
            }
        }
    }
}
