using System;
using Fusion;
using Fusion.Editor;
using PickleVerse.Multiplayer;
using UnityEditor;
using UnityEngine;

public static class DedicatedPrefabSetup
{
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring prefabs.");
        var settings = Resources.Load<MultiplayerSettings>("MultiplayerSettings");
        GameObject Copy(GameObject source, string target)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(target) && !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), target))
                throw new InvalidOperationException("Could not copy dedicated prefab.");
            var root = PrefabUtility.LoadPrefabContents(target);
            try
            {
                root.GetComponent<NetworkObject>().Flags &= ~(NetworkObjectFlags.MasterClientObject | NetworkObjectFlags.AllowStateAuthorityOverride | NetworkObjectFlags.DestroyWhenStateAuthorityLeaves);
                PrefabUtility.SaveAsPrefabAsset(root, target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(target);
        }
        settings.dedicatedMatchPrefab = Copy(settings.matchPrefab, "Assets/Prefabs/Multiplayer/DedicatedMatch.prefab");
        settings.dedicatedPlayerPrefab = Copy(settings.playerPrefab, "Assets/Prefabs/Multiplayer/DedicatedPlayer.prefab");
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets(); NetworkProjectConfigUtilities.RebuildPrefabTable();
        return "Dedicated prefabs authored without Shared authority flags; original prefabs preserved.";
    }
}
