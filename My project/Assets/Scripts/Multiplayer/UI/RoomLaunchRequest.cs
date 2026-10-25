using UnityEngine;
using System;
namespace PickleVerse.Multiplayer.UI
{
    public enum RoomLaunchAction { None, CreateSingles, CreateDoubles, Join }
    /// <summary>One-shot navigation intent, consumed by the room presenter after identity initialization.</summary>
    public static class RoomLaunchRequest
    {
        public static RoomLaunchAction Action { get; private set; }
        public static string Code { get; private set; }
        public static string Region { get; private set; }
        public static void Create(PlayMode mode)
        {
            Clear();
            Action = mode == PlayMode.Singles ? RoomLaunchAction.CreateSingles : RoomLaunchAction.CreateDoubles;
        }
        public static void Join(string code, string region = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Nhập mã phòng trước.", nameof(code));
            Action = RoomLaunchAction.Join;
            Code = code.Trim().ToUpperInvariant();
            Region = region;
        }
        public static RoomLaunchAction Consume(out string code) => Consume(out code, out _);
        public static RoomLaunchAction Consume(out string code, out string region)
        {
            var action = Action;
            code = Code;
            region = Region;
            Clear();
            return action;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            Action = RoomLaunchAction.None;
            Code = Region = null;
        }
    }
}
