using UnityEngine;
using System;
namespace PickleVerse.Multiplayer.UI
{
    public enum RoomLaunchAction { None,CreateSingles,CreateDoubles,Join }
    /// <summary>One-shot navigation intent, consumed by the room presenter after identity initialization.</summary>
    public static class RoomLaunchRequest
    {
        public static RoomLaunchAction Action {get;private set;}
        public static string Code {get;private set;}
        public static void Create(PlayMode mode){Action=mode==PlayMode.Singles?RoomLaunchAction.CreateSingles:RoomLaunchAction.CreateDoubles;Code=null;}
        public static void Join(string code){if(string.IsNullOrWhiteSpace(code))throw new ArgumentException("Nhập mã phòng trước.",nameof(code));Action=RoomLaunchAction.Join;Code=code.Trim().ToUpperInvariant();}
        public static RoomLaunchAction Consume(out string code){var action=Action;code=Code;Clear();return action;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] public static void Clear(){Action=RoomLaunchAction.None;Code=null;}
    }
}
