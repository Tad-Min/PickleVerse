using UnityEngine;

namespace PickleVerse.LocalMatch
{
    public interface ITutorialProgressStore {int Read(string key);void Write(string key,int value);}
    public sealed class LocalTutorialProgressStore : ITutorialProgressStore
    {public int Read(string key)=>PlayerPrefs.GetInt(key,0);public void Write(string key,int value){PlayerPrefs.SetInt(key,value);PlayerPrefs.Save();}}
    public static class TutorialProgress
    {
        public const string CompletedKey="PKV.Tutorial.Basic.Completed.v1",SkippedKey="PKV.Tutorial.Basic.Skipped.v1";
        static ITutorialProgressStore storage;
        static ITutorialProgressStore Store=>storage??(storage=new LocalTutorialProgressStore());
        public static bool NeedsIntroduction=>Store.Read(CompletedKey)==0&&Store.Read(SkippedKey)==0;
        public static bool Completed=>Store.Read(CompletedKey)!=0;
        public static void Complete(){Store.Write(CompletedKey,1);}
        public static void Skip(){Store.Write(SkippedKey,1);}
        public static void Configure(ITutorialProgressStore adapter){storage=adapter;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){storage=null;}
    }
}
