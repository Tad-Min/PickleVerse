using UnityEngine;

namespace PickleVerse.Audio
{
    public enum GameSfx { HitNormal, Serve, HitStrong, ShoeSkid, Footstep, Countdown, FaultWhistle, GameWhistle }

    public sealed class SfxLibrary : ScriptableObject
    {
        public AudioClip[] clips = new AudioClip[8];
        public AudioClip Clip(GameSfx sound) => clips[(int)sound];
    }
}
