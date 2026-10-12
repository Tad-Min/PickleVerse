using System;
using UnityEngine;

namespace PickleVerse.Audio
{
    [Serializable] public sealed class MusicTrack
    {
        public string id,title,artist,genre;
        public AudioClip clip;
        public Sprite disc;
        // Normalized visible artwork bounds; calibrated from alpha, without changing the PNG.
        public Rect discBounds=new Rect(0,0,1,1);
        public Color accent=Color.cyan;
    }
    public sealed class MusicLibrary : ScriptableObject
    {
        public MusicTrack[] tracks=Array.Empty<MusicTrack>();
        public GameObject uiPrefab;
        [Range(0,1)] public float gain=.45f;
        [Range(.1f,2)] public float crossfade=.65f;
    }
    public static class MusicScenePolicy
    {
        public static bool Allows(string scene,bool trainingExercise=false)=>scene=="Menu"||scene=="ModeSelection"||scene=="PlayMultiplayer"||scene=="Shop"||scene=="Collection"||scene=="Training"&&!trainingExercise;
        public static bool ShowsWidget(string scene)=>scene=="Menu";
        public static int Wrap(int index,int count)=>count<=0?-1:(index%count+count)%count;
    }
}
