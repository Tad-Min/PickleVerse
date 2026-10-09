using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickleVerse.Settings
{
    public enum GameAction { Forward, Backward, Left, Right, Run, Drive, Dink, Volley, Power, Cross, CancelShot, Timeout, Help, Continue, Settings }

    [Serializable]
    public sealed class SettingsData
    {
        public int version = 1;
        public float master = 1, music = .8f, effects = 1;
        public int graphics = 2, fps = 60, msaa = 4;
        public float renderScale = 1;
        public bool shadows = true, vsync;
        public float touchScale = 1, touchOpacity = .85f, deadzone = .08f, sprintThreshold = .88f;
        public bool leftHanded;
        public int[] keys;
        public Vector2[] touchPositions;

        public static SettingsData Defaults()
        {
            var d = new SettingsData();
            d.keys = new[] { Key.W, Key.S, Key.A, Key.D, Key.LeftShift, Key.J, Key.K, Key.L, Key.U, Key.I, Key.Backspace, Key.T, Key.F1, Key.Enter, Key.Escape }.Select(k => (int)k).ToArray();
            d.touchPositions = new[] { new Vector2(.08f,.15f), new Vector2(.944f,.085f), new Vector2(.858f,.085f), new Vector2(.772f,.085f), new Vector2(.944f,.165f), new Vector2(.858f,.165f), new Vector2(.772f,.165f), new Vector2(.944f,.245f), new Vector2(.858f,.245f) };
            return d;
        }
        public SettingsData Copy() => JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(this));
        public Key KeyFor(GameAction action) => (Key)keys[(int)action];
        public void SetPreset(int preset)
        {
            graphics = Mathf.Clamp(preset,0,2);
            renderScale = new[] { .7f,.85f,1f }[graphics];
            msaa = new[] { 1,2,4 }[graphics];
        }
        public void Validate()
        {
            var defaults = Defaults();
            if (keys == null || keys.Length != defaults.keys.Length) keys = defaults.keys;
            if (touchPositions == null || touchPositions.Length != 9) touchPositions = defaults.touchPositions;
            if (keys.Distinct().Count() != keys.Length || keys.Where((key,index) => !Enum.IsDefined(typeof(Key),key) || key==(int)Key.None || (index!=14 && key==(int)Key.Escape)).Any()) keys=defaults.keys;
            keys[14] = (int)Key.Escape;
            master = Mathf.Clamp01(master); music = Mathf.Clamp01(music); effects = Mathf.Clamp01(effects);
            graphics = Mathf.Clamp(graphics,0,2); renderScale = Mathf.Clamp(renderScale,.5f,1.25f);
            if (msaa != 1 && msaa != 2 && msaa != 4) msaa = 4;
            if (fps != 30 && fps != 60 && fps != 120) fps = 60;
            touchScale = Mathf.Clamp(touchScale,.75f,1.4f); touchOpacity = Mathf.Clamp(touchOpacity,.35f,1);
            deadzone = Mathf.Clamp(deadzone,.02f,.25f); sprintThreshold = Mathf.Clamp(sprintThreshold,.7f,.98f);
            for (int i = 0; i < touchPositions.Length; i++) touchPositions[i] = new Vector2(Mathf.Clamp(touchPositions[i].x,.04f,.96f),Mathf.Clamp(touchPositions[i].y,.05f,.8f));
        }
    }

    public static class GameSettings
    {
        public const string PreferenceKey = "PKV.Settings.v1";
        static SettingsData current;
        public static event Action Changed;
        public static SettingsData Current { get { if(current==null) Reload(); return current; } }
        public static void Reload()
        {
            try { current = JsonUtility.FromJson<SettingsData>(PlayerPrefs.GetString(PreferenceKey,"")); } catch { current = null; }
            if(current==null)current=SettingsData.Defaults(); current.Validate();
        }
        public static void Commit(SettingsData settings)
        {
            current = settings.Copy(); current.Validate();
            PlayerPrefs.SetString(PreferenceKey,JsonUtility.ToJson(current)); PlayerPrefs.Save();
            SettingsRuntime.Apply(current); Changed?.Invoke();
        }
        public static string ActionName(GameAction action) => new[] { "Tiến lên", "Lùi", "Sang trái", "Sang phải", "Chạy", "Drive / Giao bóng", "Dink", "Volley", "Tăng lực", "Đánh chéo", "Hủy chọn cú", "Time-out", "Hướng dẫn", "Tiếp tục / Bỏ qua intro", "Mở cài đặt (cố định)" }[(int)action];
        public static string KeyName(Key key) => key == Key.LeftShift ? "Shift trái" : key == Key.RightShift ? "Shift phải" : key == Key.Space ? "Space" : key.ToString();
        public static bool TryBind(SettingsData data,GameAction action,Key key,out string error)
        {
            error="";
            if(action==GameAction.Settings || key==Key.None || key==Key.Escape) { error="Esc được giữ để mở/đóng cài đặt.";return false; }
            int conflict=Array.IndexOf(data.keys,(int)key);
            if(conflict>=0&&conflict!=(int)action) { error="Phím này đang dùng cho: "+ActionName((GameAction)conflict);return false; }
            data.keys[(int)action]=(int)key;return true;
        }
    }

    public static class GameControls
    {
        public static bool Held(GameAction action)
        {
            var keyboard=Keyboard.current;if(keyboard==null)return false;
            var key=GameSettings.Current.KeyFor(action);
            return keyboard[key].isPressed || (action==GameAction.Run&&key==Key.LeftShift&&!GameSettings.Current.keys.Contains((int)Key.RightShift)&&keyboard.rightShiftKey.isPressed);
        }
        public static bool Pressed(GameAction action) => Keyboard.current!=null&&Keyboard.current[GameSettings.Current.KeyFor(action)].wasPressedThisFrame;
        public static int ShotMask => (Held(GameAction.Drive)?1:0)|(Held(GameAction.Dink)?2:0)|(Held(GameAction.Volley)?4:0)|(Held(GameAction.Power)?8:0)|(Held(GameAction.Cross)?16:0);
        public static int TapMask => (Pressed(GameAction.Drive)&&!Held(GameAction.Drive)?1:0)|(Pressed(GameAction.Dink)&&!Held(GameAction.Dink)?2:0)|(Pressed(GameAction.Volley)&&!Held(GameAction.Volley)?4:0);
        public static string ShotKeys(int mask)
        {
            GameAction[] actions={GameAction.Drive,GameAction.Dink,GameAction.Volley,GameAction.Power,GameAction.Cross};
            return string.Join(" + ",Enumerable.Range(0,5).Where(i=>(mask&(1<<i))!=0).Select(i=>GameSettings.KeyName(GameSettings.Current.KeyFor(actions[i]))));
        }
    }
}
