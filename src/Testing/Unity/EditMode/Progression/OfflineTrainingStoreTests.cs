using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using PickleVerse.Progression;
using UnityEngine;

namespace PickleVerse.Tests
{
    public sealed class OfflineTrainingStoreTests
    {
        string directory, owner;
        OfflineTrainingStore store;
        [SetUp] public void Setup()
        {
            owner=Guid.NewGuid().ToString("N"); directory=Path.Combine(Application.temporaryCachePath,"functional-offline-tests",Guid.NewGuid().ToString("N"));
            store=new OfflineTrainingStore(directory,owner); store.Confirm(Snapshot());
        }
        [TearDown] public void Cleanup()
        {
            store?.Dispose();
            string parent=Path.GetFullPath(Path.Combine(Application.temporaryCachePath,"functional-offline-tests"));
            if (Directory.Exists(directory) && Path.GetDirectoryName(Path.GetFullPath(directory))==parent) Directory.Delete(directory,true);
        }
        OnlineGameSnapshot Snapshot(long revision=1,long epoch=0) => new OnlineGameSnapshot {
            revision=revision,learningEpoch=epoch,experienceChoice="EXPERIENCED",player=new PlayerProfile { id=owner,playerName="Test",level=1,elo=2000 },
            inventory=new OnlineInventory { owned=new List<string>{"paddle.default"},equippedPaddle="paddle.default" },tutorial=new OnlineTutorialState(),
            catalog=new OnlineGameCatalog { contentSchemaVersion=2,version="cached-v1",lessons=OfflineTrainingStore.Copy(TrainingCurriculum.Lessons),
                definitions=new List<ProgressDefinition>(),cosmetics=new OnlineCosmeticOffer[0] } };
        TrainingAttempt Completed()
        {
            var state=store.Start("serve",false);var a=state.player.training.active;
            a.trials=a.successes=a.contacts=TrainingCurriculum.Find("serve").trials;a.seconds=30;a.timingTotal=a.contacts*.1;
            store.Save(a,true);return a;
        }
        [Test] public void RestartRestoresTheLatestCheckpoint()
        {
            var active=store.Start("serve",false).player.training.active;active.trials=active.successes=active.contacts=3;active.seconds=9;
            store.Save(active,false);store.Dispose();store=new OfflineTrainingStore(directory,owner);
            var restored=store.Project().player.training.active;
            Assert.AreEqual(active.id,restored.id);Assert.AreEqual(3,restored.trials);Assert.AreEqual(9,restored.seconds);
        }
        [Test] public void ProvisionalCompletionDoesNotCreateSpendableMoney()
        {
            var attempt=Completed();var projected=store.Project();
            Assert.AreEqual(0,projected.player.coins);Assert.AreEqual(5,projected.player.levelXp);Assert.AreEqual(3,projected.player.training.active.stars);
            store.Save(attempt,true);Assert.AreEqual(5,store.Project().player.levelXp);Assert.AreEqual(1,store.CompletedCount);
        }
        [Test] public void CorruptPrimaryFallsBackToTheLastValidBackup()
        {
            store.Start("serve",false);store.Dispose();File.WriteAllText(Path.Combine(directory,owner+".json"),"{broken");
            store=new OfflineTrainingStore(directory,owner);Assert.IsTrue(store.HasConfirmed);Assert.IsNotEmpty(store.RecoveryNotice);Assert.AreEqual(owner,store.Project().player.id);
        }
        [Test] public void OwnerMismatchCannotRemoveQueuedProgress()
        {
            var attempt=Completed();var other=Snapshot(2);other.player.id=Guid.NewGuid().ToString("N");
            Assert.Throws<InvalidOperationException>(()=>store.Accept(new OnlineTrainingSyncResult { snapshot=other,receipts=new List<OnlineTrainingSyncReceipt>{
                new OnlineTrainingSyncReceipt { attemptId=attempt.id,status=TrainingSyncStatus.Applied } } }));
            Assert.AreEqual(1,store.CompletedCount);
        }
        [Test] public void AcceptedOrDuplicateReceiptsRemoveTheOutboxOnce()
        {
            var attempt=Completed();var confirmed=Snapshot(2);confirmed.player.coins=150;
            var response=new OnlineTrainingSyncResult { snapshot=confirmed,receipts=new List<OnlineTrainingSyncReceipt>{new OnlineTrainingSyncReceipt { attemptId=attempt.id,status=TrainingSyncStatus.Duplicate }} };
            store.Accept(response);store.Accept(response);Assert.AreEqual(0,store.CompletedCount);Assert.AreEqual(150,store.Project().player.coins);
        }
        [Test] public void RetryablePrerequisiteFailuresKeepTheAttempt()
        {
            var attempt=Completed();store.Accept(new OnlineTrainingSyncResult { snapshot=Snapshot(2),receipts=new List<OnlineTrainingSyncReceipt>{new OnlineTrainingSyncReceipt {
                attemptId=attempt.id,status=TrainingSyncStatus.Rejected,retryable=true,message="Chờ bài điều kiện" }} });
            Assert.AreEqual(1,store.CompletedCount);Assert.AreEqual(0,store.Rejected.Count);
        }
        [Test] public void TerminalRejectionKeepsEvidenceAndReason()
        {
            var attempt=Completed();store.Accept(new OnlineTrainingSyncResult { snapshot=Snapshot(2),receipts=new List<OnlineTrainingSyncReceipt>{new OnlineTrainingSyncReceipt {
                attemptId=attempt.id,status=TrainingSyncStatus.Rejected,message="Tiến trình đã được đặt lại" }} });
            Assert.AreEqual(0,store.CompletedCount);Assert.AreEqual(attempt.id,store.Rejected[0].attempt.id);Assert.IsNotEmpty(store.Rejected[0].message);
        }
        [Test] public void ANewResetEpochDoesNotOverlayOldProgress()
        {
            Completed();store.Confirm(Snapshot(10,1));var view=store.Project();
            Assert.AreEqual(0,view.player.levelXp);Assert.IsNull(view.player.training.active);Assert.AreEqual(1,store.UploadBatch().Count);
        }
        [Test] public void DelayedSyncResponseCannotOverwriteANewerSnapshot()
        {
            var attempt=Completed();var newer=Snapshot(20);newer.player.coins=900;store.Confirm(newer);
            store.Accept(new OnlineTrainingSyncResult { snapshot=Snapshot(2),receipts=new List<OnlineTrainingSyncReceipt>{new OnlineTrainingSyncReceipt {attemptId=attempt.id,status=TrainingSyncStatus.Applied}} });
            Assert.AreEqual(20,store.Project().revision);Assert.AreEqual(900,store.Project().player.coins);
        }
        [Test] public void ActiveAttemptRetainsItsOwnDefinitionAfterCatalogChanges()
        {
            var attempt=store.Start("serve",false).player.training.active;var updated=Snapshot(2);
            updated.catalog.version="cached-v2";Array.Find(updated.catalog.lessons,d=>d.id=="serve").trials=3;store.Confirm(updated);
            attempt.trials=attempt.successes=attempt.contacts=8;attempt.seconds=30;attempt.timingTotal=.8;
            var view=store.Save(attempt,true);Assert.IsTrue(view.player.training.active.passed);Assert.AreEqual(8,Array.Find(view.catalog.lessons,d=>d.id=="serve").trials);
        }
        [Test] public void TwoWritersCannotCorruptOneAccountFile()
        {
            Assert.Throws<IOException>(()=>new OfflineTrainingStore(directory,owner));
        }
        [Test] public void TutorialAndOnboardingChangesSurviveRestart()
        {
            store.Queue(OfflineCommandKind.OnboardingNew,Guid.NewGuid().ToString("N"));store.Queue(OfflineCommandKind.TutorialCompleted,Guid.NewGuid().ToString("N"));
            store.Dispose();store=new OfflineTrainingStore(directory,owner);Assert.AreEqual("NEW",store.Project().experienceChoice);
            Assert.IsTrue(store.Project().tutorial.completed);Assert.AreEqual(2,store.CommandCount);
        }
        [Test] public void ReceiptEnumUsesCanonicalServerCodes()
        {
            var receipt=JsonConvert.DeserializeObject<OnlineTrainingSyncReceipt>("{\"attemptId\":\"a\",\"status\":\"APPLIED\",\"retryable\":false}");
            Assert.AreEqual(TrainingSyncStatus.Applied,receipt.status);StringAssert.Contains("APPLIED",JsonConvert.SerializeObject(receipt));
        }
        [Test] public void TutorialCheckpointSurvivesRestartAndNeverMovesBackwards()
        {
            store.Queue(OfflineCommandKind.TutorialCheckpoint,Guid.NewGuid().ToString("N"),7);
            store.Queue(OfflineCommandKind.TutorialCheckpoint,Guid.NewGuid().ToString("N"),3);
            store.Dispose();store=new OfflineTrainingStore(directory,owner);Assert.AreEqual(7,store.Project().tutorial.checkpoint);
        }
        [TestCase("0")][TestCase("999")][TestCase("\"invalid\"")]
        public void ReceiptRejectsInvalidEnumCodes(string code)
        { Assert.Throws<JsonSerializationException>(()=>JsonConvert.DeserializeObject<OnlineTrainingSyncReceipt>("{\"status\":"+code+"}")); }
    }
}
