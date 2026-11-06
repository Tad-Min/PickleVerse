using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using UnityEngine;

namespace PickleVerse.Progression
{
    public sealed partial class ProgressionRuntime
    {
        OfflineTrainingStore offlineStore;
        bool syncing;
        float nextSync;
        public bool IsOffline { get; private set; }
        public int PendingTraining => offlineStore?.CompletedCount ?? 0;
        public int PendingOperations => (offlineStore?.CompletedCount ?? 0) + (offlineStore?.CommandCount ?? 0);
        public string SyncMessage { get; private set; }
        public System.Collections.Generic.IReadOnlyList<OfflineTrainingFailure> SyncFailures => offlineStore != null ? offlineStore.Rejected : Array.Empty<OfflineTrainingFailure>();
        string OfflineRoot
        {
            get
            {
                string address = AccountRuntime.Ensure().Settings.baseUrl.TrimEnd('/').ToLowerInvariant();
                string key;
                using (var hash = SHA256.Create()) key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(address))).Replace("-", "").Substring(0, 16);
                return Path.Combine(Application.persistentDataPath, "offline-training-v1", key);
            }
        }
        public bool HasOfflineProfile
        {
            get
            {
                try { string owner = LastOfflineOwner();
                    return owner != null && !File.Exists(Path.Combine(OfflineRoot,owner+".restricted")); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { return false; }
            }
        }
        string LastOfflineOwner()
        {
            foreach(var suffix in new[]{"",".bak"})
            {
                string path=Path.Combine(OfflineRoot,"last-player.txt"+suffix);if(!File.Exists(path))continue;
                string owner=File.ReadAllText(path).Trim();
                if(Guid.TryParseExact(owner,"N",out _) && (File.Exists(Path.Combine(OfflineRoot,owner+".json")) || File.Exists(Path.Combine(OfflineRoot,owner+".json.bak"))))return owner;
            }
            return null;
        }
        void RememberOfflineRestriction()
        {
            if(account?.IsSignedIn!=true)return;
            try
            {
                Directory.CreateDirectory(OfflineRoot);string path=Path.Combine(OfflineRoot,account.Profile.UserId.ToString("N")+".restricted");
                if(account.IsRestricted)File.WriteAllText(path,"appeals");else if(File.Exists(path))File.Delete(path);
            }
            catch(Exception error)when(error is IOException || error is UnauthorizedAccessException)
            { Debug.LogWarning("[Training] Could not persist account restriction ("+error.GetType().Name+")."); }
        }
        static bool CanUseOffline(Exception error) => error is ApiException api && (api.StatusCode == 0 || api.StatusCode >= 500);
        void CloseOfflineStore() { offlineStore?.Dispose(); offlineStore = null; SyncMessage = null; }
        void OpenOfflineStore(string owner)
        {
            if (offlineStore?.Owner == owner) return;
            CloseOfflineStore(); offlineStore = new OfflineTrainingStore(OfflineRoot, owner);
        }
        void CacheConfirmed(OnlineGameSnapshot state)
        {
            if (state.catalog.contentSchemaVersion < 2) return;
            try
            {
                OpenOfflineStore(state.player.id); offlineStore.Confirm(state);
                string path = Path.Combine(OfflineRoot, "last-player.txt"), temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(temp, state.player.id);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { SyncMessage = "Chưa lưu được bản ngoại tuyến. Hãy kiểm tra dung lượng hoặc phiên game khác."; }
        }
        public bool TryEnterOffline()
        {
            try
            {
                string owner = account.IsSignedIn ? account.Profile.UserId.ToString("N") : LastOfflineOwner();
                if(owner==null)throw new InvalidOperationException("Hãy đăng nhập có mạng ít nhất một lần trước.");
                if(account.IsRestricted || File.Exists(Path.Combine(OfflineRoot,owner+".restricted")))throw new InvalidOperationException("Tài khoản đã được thông báo hạn chế. Hãy đăng nhập để xem quyết định hoặc khiếu nại.");
                OpenOfflineStore(owner);
                if (!offlineStore.HasConfirmed) throw new InvalidOperationException("Hãy đăng nhập có mạng ít nhất một lần trước khi luyện tập ngoại tuyến.");
                IsOffline = true; ApplyOffline(offlineStore.Project());
                SyncMessage = offlineStore.RecoveryNotice ?? "Luyện tập ngoại tuyến. Tiến trình và thưởng sẽ được xác nhận khi đồng bộ.";
                Debug.Log("[Training] Offline profile opened; pending=" + offlineStore.CompletedCount);
                Changed?.Invoke(); return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidOperationException)
            { Error = error.Message; Changed?.Invoke(); return false; }
        }
        public void ExitOffline()
        {
            CloseOfflineStore(); IsOffline = false; Snapshot = null; player = Empty(); loadedSession = -1; Changed?.Invoke();
        }
        void ApplyOffline(OnlineGameSnapshot state)
        {
            if (state?.player == null || state.catalog?.lessons == null || state.catalog.definitions == null)
                throw new InvalidOperationException("Dữ liệu ngoại tuyến chưa đầy đủ.");
            TrainingCurriculum.Configure(state.catalog.lessons);
            rules.winXp = state.catalog.winXp; rules.lossXp = state.catalog.lossXp; rules.dailyBonus = state.catalog.dailyBonus;
            rules.weeklyBonus = state.catalog.weeklyBonus; rules.definitions = state.catalog.definitions;
            state.player.online = false; player = state.player; Snapshot = state;
            loadedSession = account.SessionVersion; Error = null; Changed?.Invoke();
        }
        OnlineGameSnapshot PrepareActiveLesson(OnlineGameSnapshot state)
        {
            var active = state.player.training?.active;
            if (active?.definition == null || active.completed || active.catalogVersion == state.catalog.version) return state;
            var definition = OfflineTrainingStore.Copy(active.definition);
            var available = new System.Collections.Generic.HashSet<string>();
            foreach (var lesson in state.catalog.lessons) available.Add(lesson.id);
            // This is a display-only resume entry. The server validates the original immutable graph.
            definition.prerequisites = Array.FindAll(definition.prerequisites, available.Contains);
            var lessons = new System.Collections.Generic.List<TrainingDrill>();
            foreach (var lesson in state.catalog.lessons) if (lesson.id != definition.id) lessons.Add(lesson);
            lessons.Add(definition);state.catalog.lessons=lessons.ToArray();return state;
        }
        async Task<OnlineGameSnapshot> StartTrainingResilient(string id, bool retry)
        {
            await EnsureReadyAsync();
            if (IsOffline || offlineStore?.PendingCount > 0)
            { var state = offlineStore.Start(id, retry); ApplyOffline(state); return state; }
            try { return await CommandAsync("training/start", new { RequestId = Guid.NewGuid().ToString("N"), LessonCode = id, Retry = retry }); }
            catch (Exception error) when (CanUseOffline(error) && offlineStore?.HasConfirmed == true)
            { IsOffline = true; var state = offlineStore.Start(id, retry); ApplyOffline(state); SyncMessage = "Mất mạng. Lượt tập đang được lưu trên máy."; return state; }
        }
        async Task<OnlineGameSnapshot> SaveTrainingResilient(TrainingAttempt attempt, bool completed, string requestId)
        {
            if (IsOffline || offlineStore?.Contains(attempt.id) == true)
            { var local = offlineStore.Save(attempt, completed); ApplyOffline(local); return local; }
            try { return await CommandAsync(completed ? "training/complete" : "training/checkpoint", new { RequestId = requestId, Attempt = attempt }); }
            catch (Exception error) when (CanUseOffline(error) && offlineStore?.HasConfirmed == true)
            { IsOffline = true; var local = offlineStore.Save(attempt, completed); ApplyOffline(local); SyncMessage = "Mất mạng. Kết quả đang chờ đồng bộ."; return local; }
        }
        public Task<OnlineGameSnapshot> SetExperienceAsync(bool newcomer) => SaveBasicResilient(newcomer ? OfflineCommandKind.OnboardingNew : OfflineCommandKind.OnboardingExperienced, Guid.NewGuid().ToString("N"));
        public Task<OnlineGameSnapshot> SetTutorialCheckpointAsync(int step) => SaveBasicResilient(OfflineCommandKind.TutorialCheckpoint, Guid.NewGuid().ToString("N"), step);
        static bool IsTutorial(OfflineCommandKind kind) => kind == OfflineCommandKind.TutorialCompleted || kind == OfflineCommandKind.TutorialSkipped || kind == OfflineCommandKind.TutorialCheckpoint;
        static object BasicPayload(OfflineBasicCommand command) => IsTutorial(command.kind)
            ? (object)new { RequestId=command.requestId, Key=command.kind==OfflineCommandKind.TutorialCompleted?"completed":command.kind==OfflineCommandKind.TutorialSkipped?"skipped":"checkpoint", Step=command.step }
            : new { RequestId=command.requestId, Choice=command.kind==OfflineCommandKind.OnboardingNew?"NEW":"EXPERIENCED" };
        async Task<OnlineGameSnapshot> SaveBasicResilient(OfflineCommandKind kind, string requestId, int step = 0)
        {
            var command = new OfflineBasicCommand { kind=kind, requestId=requestId, step=step };
            var store=offlineStore;store?.Queue(kind,requestId,step);
            if (IsOffline) { var local = store.Project(); ApplyOffline(local); return local; }
            try
            {
                var state=await CommandAsync(IsTutorial(kind)?"tutorial":"onboarding",BasicPayload(command));
                if(store!=null && offlineStore==store && state.player.id==store.Owner){store.AcceptCommand(requestId,state);ApplySnapshot(state);}
                return state;
            }
            catch (Exception error) when (CanUseOffline(error) && offlineStore?.HasConfirmed == true)
            { IsOffline = true; var local = offlineStore.Project(); ApplyOffline(local); return local; }
        }
        public async Task SyncOfflineAsync()
        {
            if (syncing || offlineStore == null) return;
            if (!account.IsSignedIn || account.IsRestricted || offlineStore.Owner != account.Profile.UserId.ToString("N"))
                throw new InvalidOperationException("Hãy đăng nhập đúng tài khoản để đồng bộ tiến trình.");
            syncing = true; string owner = offlineStore.Owner;
            try
            {
                foreach (var command in offlineStore.Commands())
                {
                    bool tutorial = IsTutorial(command.kind);
                    object payload = BasicPayload(command);
                    try
                    {
                        var response = await account.SendAuthorizedAsync<OnlineGameSnapshot>("POST", tutorial ? "api/Game/tutorial" : "api/Game/onboarding", payload);
                        if (!this || offlineStore?.Owner != owner) return;
                        offlineStore.AcceptCommand(command.requestId, response); ApplySnapshot(response);
                    }
                    catch (ApiException error) when (error.StatusCode == 400 || error.StatusCode == 409)
                    {
                        var current = await account.SendAuthorizedAsync<OnlineGameSnapshot>("GET", "api/Game/state");
                        if (!this || offlineStore?.Owner != owner) return;
                        offlineStore.AcceptCommand(command.requestId, current, error.Message); ApplySnapshot(current);
                    }
                }
                while (offlineStore != null && offlineStore.Owner == owner && offlineStore.CompletedCount > 0)
                {
                    int before = offlineStore.CompletedCount;
                    var response = await account.SendAuthorizedAsync<OnlineTrainingSyncResult>("POST", "api/Game/training/sync", new { Attempts = offlineStore.UploadBatch() });
                    if (!this || offlineStore?.Owner != owner || !account.IsSignedIn || account.Profile.UserId.ToString("N") != owner) return;
                    offlineStore.Accept(response); ApplySnapshot(response.snapshot);
                    if (offlineStore.CompletedCount >= before) break;
                }
                if (offlineStore == null) return;
                if (offlineStore.CompletedCount == 0)
                {
                    IsOffline = false;
                    await ReloadAsync();
                    SyncMessage = offlineStore.Rejected.Count > 0 ? "Đã đồng bộ. Có lượt không được áp dụng: " + offlineStore.Rejected[offlineStore.Rejected.Count - 1].message : "Đã đồng bộ tiến trình và phần thưởng.";
                }
                else SyncMessage = "Một số lượt đang chờ bài điều kiện. Dữ liệu vẫn được giữ trên máy.";
                Debug.Log("[Training] Sync completed; pending=" + PendingTraining); Changed?.Invoke();
            }
            finally { syncing = false; }
        }
        void TickOfflineSync()
        {
            if (syncing || Time.unscaledTime < nextSync || PendingOperations < 1 || !account.IsSignedIn || offlineStore == null) return;
            nextSync = Time.unscaledTime + 15; _ = TrySyncOffline();
        }
        async Task TrySyncOffline()
        {
            try { await SyncOfflineAsync(); }
            catch (Exception error) when (error is ApiException || error is IOException || error is InvalidOperationException || error is OperationCanceledException)
            { if (this) { SyncMessage = error is ApiException ? "Chưa thể đồng bộ. Kết quả vẫn được lưu trên máy." : error.Message; Changed?.Invoke(); } }
        }
    }
}
