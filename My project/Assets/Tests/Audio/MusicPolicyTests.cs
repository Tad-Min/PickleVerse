using NUnit.Framework;
using PickleVerse.Audio;
using PickleVerse.Settings;
using UnityEngine;
using UnityEditor;

namespace PickleVerse.Tests
{
    public sealed class MusicPolicyTests
    {
        [TestCase("Menu",true)] [TestCase("ModeSelection",true)] [TestCase("PlayMultiplayer",true)] [TestCase("Shop",true)] [TestCase("Collection",true)]
        [TestCase("BotMatch",false)] [TestCase("MultiplayerCourt",false)] [TestCase("Training",false)] [TestCase("SampleScene",false)] [TestCase("Settings",false)] [TestCase(null,false)]
        public void OnlyFrontEndPagesHaveMusic(string scene,bool expected){Assert.That(MusicScenePolicy.Allows(scene),Is.EqualTo(expected));}
        [Test] public void PlaylistCyclesInBothDirections()
        {Assert.That(MusicScenePolicy.Wrap(4,4),Is.EqualTo(0));Assert.That(MusicScenePolicy.Wrap(-1,4),Is.EqualTo(3));Assert.That(MusicScenePolicy.Wrap(0,0),Is.EqualTo(-1));}
        [Test] public void AnotherOwnerCannotStealOrReleaseMusicModal()
        {
            var music=new object();var other=new object();
            try{Assert.That(FrontEndModalInput.TryAcquire(music));Assert.That(FrontEndModalInput.TryAcquire(other),Is.False);FrontEndModalInput.Release(other);Assert.That(FrontEndModalInput.IsOpen);}
            finally{FrontEndModalInput.Release(music);}
            Assert.That(FrontEndModalInput.IsOpen,Is.False);
        }
        [Test] public void CatalogContainsFourMatchingDiscsAndAudio()
        {
            var library=Resources.Load<MusicLibrary>("Audio/Front End Music");Assert.That(library);Assert.That(library.uiPrefab);Assert.That(library.tracks.Length,Is.EqualTo(4));
            foreach(var track in library.tracks){Assert.That(track.clip);Assert.That(track.disc);Assert.That(track.clip.channels,Is.EqualTo(2));Assert.That(track.clip.loadType,Is.EqualTo(AudioClipLoadType.Streaming));Assert.That(track.clip.length,Is.GreaterThan(140));var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(track.clip));Assert.That(importer.loadInBackground);Assert.That(importer.defaultSampleSettings.preloadAudioData,Is.False);}
        }
    }
}
