using UnityEditor;
public static class EditorClientMode
{
    public static string Restore()
    {
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        return "Windows client target restored: " + switched;
    }
}
