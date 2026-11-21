using System.Collections;
using System.Linq;
using System.Reflection;
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

    [TestCase("Menu", true)]
    [TestCase("ModeSelection", true)]
    [TestCase("PlayMultiplayer", true)]
    [TestCase("Loading", false)]
    [TestCase("Login", false)]
    [TestCase("MultiplayerCourt", false)]
    [TestCase("PhotonLobby", false)]
    [TestCase("Settings", false)]
    [TestCase("Replay", false)]
    [TestCase("Dedicated Match_[Player:2]", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void EnteringCourtRemovesFrontendScenesAndPreservesNetworkAndLoadingScenes(string name, bool remove)
    {
        Assert.That(PhotonSceneManager.IsFrontEndScene(name, "PlayMultiplayer"), Is.EqualTo(remove));
    }

    [Test] public void CustomRoomCleanupAlsoRecognizesTheNormalMenuAndRoom()
    {
        foreach (string name in new[] { "Menu", "ModeSelection", "PlayMultiplayer", "Custom Room" })
            Assert.That(PhotonSceneManager.IsFrontEndScene(name, "Custom Room"), Is.True);
        Assert.That(PhotonSceneManager.IsFrontEndScene("MultiplayerCourt", "Custom Room"), Is.False);
    }

    [Test] public void MissingCourtCannotStartFrontendCleanup()
    {
        Assert.That(PhotonSceneManager.PrepareCourtScene(default, "PlayMultiplayer", () => true).MoveNext(), Is.False);
    }

    [TestCase(MultiplayerSceneConfiguration.CourtPath, true)]
    [TestCase(MultiplayerSceneConfiguration.LobbyPath, false)]
    public void CourtRemainsBusyDuringPostLoadAndReleasesTheGateOnCancellation(string path, bool busy)
    {
        var loaded = typeof(PhotonSceneManager).GetMethod("OnSceneLoaded", BindingFlags.Instance | BindingFlags.NonPublic);
        var flow = (IEnumerator)loaded.Invoke(manager, new object[] { SceneRef.FromPath(path), default(UnityEngine.SceneManagement.Scene), default(NetworkLoadSceneParameters) });
        try
        {
            Assert.That(flow.MoveNext(), Is.True);
            Assert.That(manager.IsBusy, Is.EqualTo(busy), "Court readiness must wait before processing the base scene callback.");
        }
        finally { ((System.IDisposable)flow).Dispose(); }
        Assert.That(manager.IsBusy, Is.False, "Cancelling the scene load must not leave a stuck loading gate.");
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
