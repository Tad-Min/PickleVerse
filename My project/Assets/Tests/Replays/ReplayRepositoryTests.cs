using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PickleVerse.Replays;
using PickleVerse.Progression;
using UnityEngine;
namespace PickleVerse.Tests
{
    public sealed class ReplayRepositoryTests
    {
        string dir;LocalReplayRepository repo;ReplayMetadata meta;
        [SetUp]public void Setup(){dir=Path.Combine(Path.GetTempPath(),"Replay"+Guid.NewGuid().ToString("N"));repo=new LocalReplayRepository(dir);meta=new ReplayMetadata{playerId="Alpha",matchId="Match1"};repo.Save(meta);}
        [TearDown]public void Cleanup(){if(Directory.Exists(dir))Directory.Delete(dir,true);}
        ReplayChunk Chunk(int count=2,double start=0)=>new ReplayChunk{frames=Enumerable.Range(0,count).Select(i=>new ReplayFrame{time=start+i/30d,ball=new float[]{1,2,3},scoreA=i,actors=new[]{new ReplayActorPose{root=new float[10],bones=new float[20],paddleVisible=true},new ReplayActorPose{root=new float[10],bones=new float[20]}}}).ToList(),events=new System.Collections.Generic.List<ReplayEvent>{new ReplayEvent{kind="Serve",time=start}}};
        [Test]public void CompressedRoundTripPreservesSnapshotsAndEvents(){repo.WriteChunk(meta,Chunk());meta.complete=true;repo.Save(meta);var m=repo.Read("Alpha","Match1");var c=repo.ReadChunk(m,m.chunks[0]);Assert.That(m.complete,Is.True);Assert.That(c.frames[1].scoreA,Is.EqualTo(1));Assert.That(c.frames[1].ball,Is.EqualTo(new float[]{1,2,3}));Assert.That(c.events[0].kind,Is.EqualTo("Serve"));}
        [Test]public void PartialRecordIsReadable(){repo.WriteChunk(meta,Chunk());Assert.That(repo.Read("Alpha","Match1").complete,Is.False);}
        [Test]public void RecoverChunkCommittedBeforeMetadata(){string initial=File.ReadAllText(Path.Combine(dir,"Alpha","Match1","metadata.json"));repo.WriteChunk(meta,Chunk());File.WriteAllText(Path.Combine(dir,"Alpha","Match1","metadata.json"),initial);repo.RecoverAndPrune("Alpha",new[]{"Match1"});Assert.That(repo.Read("Alpha","Match1").chunks.Count,Is.EqualTo(1));}
        [Test]public void MissingMetadataDoesNotCreateHistory(){Assert.That(repo.Read("Alpha","Legacy"),Is.Null);}
        [Test]public void UnsupportedVersionIsExplicit(){meta.version=42;repo.Save(meta);Assert.Throws<NotSupportedException>(()=>repo.Read("Alpha","Match1"));}
        [Test]public void CorruptChunkDoesNotMutateProfile(){var p=new PlayerProfile{id="Alpha"};p.history.Add(new MatchResult{id="Match1"});string before=JsonUtility.ToJson(p);repo.WriteChunk(meta,Chunk());File.WriteAllText(Path.Combine(dir,"Alpha","Match1",meta.chunks[0].file),"broken");Assert.Catch<IOException>(()=>repo.ReadChunk(meta,meta.chunks[0]));Assert.That(JsonUtility.ToJson(p),Is.EqualTo(before));}
        [Test]public void MissingChunkHasClearFailure(){repo.WriteChunk(meta,Chunk());File.Delete(Path.Combine(dir,"Alpha","Match1",meta.chunks[0].file));Assert.Throws<FileNotFoundException>(()=>repo.ReadChunk(meta,meta.chunks[0]));}
        [Test]public void MetadataBackupRecoversCorruptMain(){repo.WriteChunk(meta,Chunk());meta.complete=true;repo.Save(meta);File.WriteAllText(Path.Combine(dir,"Alpha","Match1","metadata.json"),"broken");Assert.That(repo.Read("Alpha","Match1").chunks.Count,Is.EqualTo(1));}
        [Test]public void RecoveryKeepsValidChunkWhenLaterChunkIsBroken(){string initial=File.ReadAllText(Path.Combine(dir,"Alpha","Match1","metadata.json"));repo.WriteChunk(meta,Chunk());File.WriteAllText(Path.Combine(dir,"Alpha","Match1","000001.json.gz"),"broken");File.WriteAllText(Path.Combine(dir,"Alpha","Match1","metadata.json"),initial);repo.RecoverAndPrune("Alpha",new[]{"Match1"});Assert.That(repo.Read("Alpha","Match1").chunks.Count,Is.EqualTo(1));}
        [Test]public void RecorderPreservesBallRotationAndScale(){var go=new GameObject("Ball pose");try{go.transform.rotation=Quaternion.Euler(30,60,90);go.transform.localScale=Vector3.one*.07f;var recorder=go.AddComponent<ReplayRecorder>();recorder.Begin(meta,new[]{new ReplayActorSource{root=go.transform}},()=>go.transform.position,()=>new ReplayFrame(),repo,go.transform);recorder.Capture();recorder.Finish(true);var saved=repo.ReadChunk(meta,meta.chunks[0]).frames[0];Assert.That(saved.ballRotation.Length,Is.EqualTo(4));Assert.That(saved.ballScale[0],Is.EqualTo(.07f).Within(.00001f));}finally{UnityEngine.Object.DestroyImmediate(go);}}
        [Test]public void BackgroundWriterDrainsBeforeFinishing(){var go=new GameObject("Async recorder fixture");try{var recorder=go.AddComponent<ReplayRecorder>();recorder.Begin(meta,new[]{new ReplayActorSource{root=go.transform}},()=>Vector3.zero,()=>new ReplayFrame(),repo);for(int i=0;i<64;i++)recorder.Capture();recorder.Finish(true);var m=repo.Read("Alpha","Match1");Assert.That(m.complete,Is.True);Assert.That(m.chunks.Count,Is.EqualTo(2));Assert.That(m.chunks.Sum(c=>repo.ReadChunk(m,c).frames.Count),Is.EqualTo(65));}finally{UnityEngine.Object.DestroyImmediate(go);}}
        [Test]public void CleanupRetainsTwentyHistoryAndPendingAndOtherProfile(){for(int i=0;i<22;i++)repo.Save(new ReplayMetadata{playerId="Alpha",matchId="M"+i});repo.Save(new ReplayMetadata{playerId="Beta",matchId="M0"});var keep=Enumerable.Range(1,20).Select(i=>"M"+i).Concat(new[]{"M21"});repo.RecoverAndPrune("Alpha",keep);Assert.That(repo.Read("Alpha","M0"),Is.Null);Assert.That(repo.Read("Alpha","M21"),Is.Not.Null);Assert.That(repo.Read("Beta","M0"),Is.Not.Null);}
        [Test]public void PathsCannotEscapeStorage(){Assert.Throws<InvalidDataException>(()=>repo.Read("../Other","Match1"));Assert.Throws<InvalidDataException>(()=>repo.Read("Alpha","../Other"));Assert.Throws<InvalidDataException>(()=>repo.ReadChunk(meta,new ReplayChunkInfo{file="../escape.json.gz"}));}
        [TestCase(2)][TestCase(4)]public void RecorderCapturesAllPlayersAndBoundsBuffer(int actors)
        {
            var go=new GameObject("Recorder fixture");var sources=Enumerable.Range(0,actors).Select(i=>{var root=new GameObject("Actor"+i).transform;root.SetParent(go.transform);var paddle=new GameObject("Paddle / fixture").transform;paddle.SetParent(root);return new ReplayActorSource{root=root,paddle=paddle,id="P"+i,visualId="Male"};}).ToArray();
            try{var recorder=go.AddComponent<ReplayRecorder>();recorder.BackgroundWrites=false;recorder.Begin(meta,sources,()=>Vector3.one,()=>new ReplayFrame{scoreA=9},repo);for(int i=0;i<130;i++)recorder.Capture();Assert.That(recorder.BufferedFrames,Is.LessThan(64));recorder.Event("EndChange");recorder.Finish(false);var saved=repo.Read("Alpha","Match1");Assert.That(saved.actors.Count,Is.EqualTo(actors));Assert.That(repo.ReadChunk(saved,saved.chunks.Last()).frames.Last().actors.Length,Is.EqualTo(actors));Assert.That(repo.ReadChunk(saved,saved.chunks.Last()).frames.Last().discontinuity,Is.True);}
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test]public void BonePathHandlesNamesWithSlash(){var root=new GameObject("Root").transform;try{var child=new GameObject("Equipped Paddle / skin").transform;child.SetParent(root);Assert.That(ReplayRecorder.Find(root,ReplayRecorder.Path(root,child)),Is.SameAs(child));}finally{UnityEngine.Object.DestroyImmediate(root.gameObject);}}
    }
}
