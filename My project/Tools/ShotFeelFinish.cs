using System;
using System.IO;
using PickleVerse.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShotFeelFinish
{
    public static object Run()
    {
        if(!Application.isPlaying||SceneManager.GetActiveScene().name!="Menu")throw new Exception("Play Menu without a match first");
        var runtime=ProgressionRuntime.Instance;
        string original=File.ReadAllText("../UnityWork/ShotFeel/original-profile.txt").Trim();
        if(original=="ShotFeelQA20261015"||string.IsNullOrEmpty(original))throw new Exception("Original profile missing");
        if(runtime.Player.id!="ShotFeelQA20261015"&&runtime.Player.id!=original)throw new Exception("Unexpected active player; leave untouched");
        runtime.SwitchPlayer(original,null);
        string root=Path.GetFullPath(Path.Combine(Application.persistentDataPath,"LocalPlayers"));
        foreach(string suffix in new[]{".json",".json.bak",".lock"})
        {
            string path=Path.GetFullPath(Path.Combine(root,"ShotFeelQA20261015"+suffix));
            if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("QA path outside profile directory");
            if(File.Exists(path))File.Delete(path);
        }
        return new{restored=runtime.Player.id==original,qaProfileRemoved=true};
    }
}
