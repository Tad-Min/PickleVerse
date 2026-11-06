using System.Linq;
using Fusion;
using NUnit.Framework;
using PickleVerse.Multiplayer;
using PickleVerse.Multiplayer.Services;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

public sealed class PhotonSceneIdentityTests
{
    GameObject owner;
    PhotonSceneManager manager;

    [SetUp] public void SetUp()
    {
        owner = new GameObject("Scene identity test");
        manager = owner.AddComponent<PhotonSceneManager>();
    }
    [TearDown] public void TearDown() => Object.DestroyImmediate(owner);

    [TestCase(MultiplayerSceneConfiguration.LobbyPath, "PhotonLobby")]
    [TestCase(MultiplayerSceneConfiguration.CourtPath, "MultiplayerCourt")]
    public void NetworkScenesUseCanonicalPaths(string path, string name)
    {
        var expected = SceneRef.FromPath(path);
        Assert.That(manager.GetSceneRef(path), Is.EqualTo(expected));
        Assert.That(manager.GetSceneRef(name), Is.EqualTo(expected));
        Assert.That(expected.IsIndex, Is.False);
    }

    [Test] public void MissingSceneIsNotMappedToAnUnrelatedScene()
    {
        Assert.That(manager.GetSceneRef("Assets/Scenes/DoesNotExist.unity"), Is.EqualTo(SceneRef.None));
    }

    [Test] public void WindowsAndGlobalLayoutsResolveTheSameNetworkSceneIdentity()
    {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/BuildProfiles/MultiplayerWindows.asset");
        Assert.That(profile, Is.Not.Null);
        var layouts = new[] { EditorBuildSettings.globalScenes, profile.scenes };
        foreach (var path in new[] { MultiplayerSceneConfiguration.LobbyPath, MultiplayerSceneConfiguration.CourtPath })
        {
            foreach (var layout in layouts)
            {
                var paths = layout.Where(s => s.enabled).Select(s => s.path).ToArray();
                Assert.That(paths, Does.Contain(path));
                // Sender encodes the canonical path, receiver searches its own list.
                var wire = manager.GetSceneRef(path);
                var matches = paths.Where(p => wire.IsPath(p)).ToArray();
                Assert.That(matches, Is.EqualTo(new[] { path }));
            }
        }
    }

    [Test] public void CourtAndLobbyCannotResolveToSettingsOrReplay()
    {
        foreach (var path in new[] { MultiplayerSceneConfiguration.LobbyPath, MultiplayerSceneConfiguration.CourtPath })
        {
            var scene = manager.GetSceneRef(path);
            Assert.That(scene.IsPath("Assets/Scenes/UI/Settings.unity"), Is.False);
            Assert.That(scene.IsPath("Assets/Scenes/Replay.unity"), Is.False);
        }
    }

    [Test] public void NetworkVersionSeparatesLegacyClientsWithoutMutatingSavedSettings()
    {
        var saved = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.AppVersion;
        var first = PhotonSession.Settings("asia");
        var second = PhotonSession.Settings("asia");
        Assert.That(first.AppVersion, Is.EqualTo(saved + ".protocol-" + MultiplayerSceneConfiguration.ProtocolVersion));
        Assert.That(second.AppVersion, Is.EqualTo(first.AppVersion));
        Assert.That(first.AppVersion, Is.Not.EqualTo(saved));
        Assert.That(Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.AppVersion, Is.EqualTo(saved));
    }
}
