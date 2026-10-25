using UnityEngine;
using System;
using System.Threading.Tasks;
using PickleVerse.Progression;

namespace PickleVerse.LocalMatch
{
    public interface ITutorialProgressStore { int Read(string key); void Write(string key, int value); }
    public sealed class LocalTutorialProgressStore : ITutorialProgressStore
    {
        public int Read(string key) => PlayerPrefs.GetInt(key, 0); public void Write(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }
    public static class TutorialProgress
    {
        public const string CompletedKey = "PKV.Tutorial.Basic.Completed.v1", SkippedKey = "PKV.Tutorial.Basic.Skipped.v1";
        static ITutorialProgressStore storage;
        static bool Read(bool completed)
        {
            if (storage != null)
                return storage.Read(completed ? CompletedKey : SkippedKey) != 0;
            var runtime = ProgressionRuntime.Instance;
            if (!runtime || !runtime.Ready)
                throw new InvalidOperationException("Vui lòng tải hồ sơ online trước.");
            return completed ? runtime.Snapshot.tutorial.completed : runtime.Snapshot.tutorial.skipped;
        }
        public static bool NeedsIntroduction => !Read(true) && !Read(false);
        public static bool Completed => Read(true);
        static Task WriteAsync(bool completed)
        {
            if (storage != null)
            {
                storage.Write(completed ? CompletedKey : SkippedKey, 1);
                return Task.CompletedTask;
            }
            return ProgressionRuntime.Instance.SetTutorialAsync(completed ? "completed" : "skipped", Guid.NewGuid().ToString("N"));
        }
        public static Task CompleteAsync() => WriteAsync(true);
        public static Task SkipAsync() => WriteAsync(false);
        // Kept for explicitly configured test adapters; runtime callers await the online command.
        public static void Complete()
        {
            if (storage == null)
                throw new InvalidOperationException("Use CompleteAsync for online progress.");
            storage.Write(CompletedKey, 1);
        }
        public static void Skip()
        {
            if (storage == null)
                throw new InvalidOperationException("Use SkipAsync for online progress.");
            storage.Write(SkippedKey, 1);
        }
        public static void Configure(ITutorialProgressStore adapter)
        {
            storage = adapter;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            storage = null;
        }
    }
}
