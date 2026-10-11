using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PickleVerse.Audio;
using PickleVerse.Settings;

public static class RecordAlignmentSetup
{
    sealed class BoundsRow {public string id{get;set;}public float[] bounds{get;set;}}
    public static object Run()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first");
        var data=Newtonsoft.Json.JsonConvert.DeserializeObject<BoundsRow[]>(File.ReadAllText("../UnityWork/RecordAlignment/bounds.json"));
        var library=Resources.Load<MusicLibrary>("Audio/Front End Music");
        foreach(var track in library.tracks)
        {var row=data.Single(r=>r.id==track.id);track.discBounds=new Rect(row.bounds[0],row.bounds[1],row.bounds[2],row.bounds[3]);}
        EditorUtility.SetDirty(library);AssetDatabase.SaveAssetIfDirty(library);
        const string path="Assets/Audio/MusicUI/Music Player.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var ui=root.GetComponent<MusicUI>();var frame=(RectTransform)ui.discTransform.parent.Find("Vinyl Orbit");
            frame.GetComponent<UiLineIcon>().SetGlyph(ArenaGlyph.RecordFrame);frame.anchorMin=new Vector2(-.085f,-.085f);frame.anchorMax=new Vector2(1.085f,1.085f);frame.offsetMin=frame.offsetMax=Vector2.zero;
            ui.trackIndex.rectTransform.anchorMin=new Vector2(.387f,.852f);ui.trackIndex.rectTransform.anchorMax=new Vector2(.70f,.882f);ui.trackIndex.rectTransform.offsetMin=ui.trackIndex.rectTransform.offsetMax=Vector2.zero;
            var track=library.tracks[0];var visible=track.discBounds;var source=track.disc.rect;float diameter=Mathf.Max(visible.width*source.width,visible.height*source.height);
            Vector2 span=source.size*(.92f/diameter);ui.discTransform.pivot=visible.center;ui.discTransform.anchorMin=new Vector2(.5f,.5f)-Vector2.Scale(span,visible.center);ui.discTransform.anchorMax=ui.discTransform.anchorMin+span;ui.discTransform.offsetMin=ui.discTransform.offsetMax=Vector2.zero;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        return new{tracks=library.tracks.Length,frame="Centered on visible artwork, rotation pivot follows artwork center"};
    }
}
