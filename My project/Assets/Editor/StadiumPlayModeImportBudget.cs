using UnityEditor;

/// <summary>Reserve memory for Play mode instead of retaining several large background import workers.</summary>
[InitializeOnLoad]
public static class StadiumPlayModeImportBudget
{
    const string Previous="PickleVerse.Stadium.PreviousImportWorkers";
    static StadiumPlayModeImportBudget()
    {
        EditorApplication.playModeStateChanged+=Changed;
        AssemblyReloadEvents.beforeAssemblyReload+=Restore;
        EditorApplication.quitting+=Restore;
        if(EditorApplication.isPlaying)EditorApplication.delayCall+=Limit;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode)Limit();
        else if(state==PlayModeStateChange.EnteredEditMode)Restore();
    }
    static void Limit()
    {
        if(!EditorApplication.isPlaying)return;
        if(SessionState.GetInt(Previous,-1)<0)SessionState.SetInt(Previous,AssetDatabase.DesiredWorkerCount);
        if(AssetDatabase.DesiredWorkerCount<=1)return;
        AssetDatabase.DesiredWorkerCount=1;
        AssetDatabase.ForceToDesiredWorkerCount();
    }
    static void Restore()
    {
        int count=SessionState.GetInt(Previous,-1);if(count<0)return;
        AssetDatabase.DesiredWorkerCount=count;SessionState.EraseInt(Previous);
    }
}
