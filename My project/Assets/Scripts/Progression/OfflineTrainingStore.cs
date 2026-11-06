using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PickleVerse.Progression
{
    [JsonConverter(typeof(PickleVerse.Services.Api.WireStatusConverter))]
    public enum TrainingSyncStatus
    {
        [EnumMember(Value = "APPLIED")] Applied,
        [EnumMember(Value = "DUPLICATE")] Duplicate,
        [EnumMember(Value = "REJECTED")] Rejected
    }
    [Serializable] public sealed class OnlineTrainingSyncReceipt
    {
        public string attemptId, message;
        public TrainingSyncStatus status;
        public bool retryable;
        public TrainingAttempt result;
    }
    [Serializable] public sealed class OnlineTrainingSyncResult
    {
        public OnlineGameSnapshot snapshot;
        public List<OnlineTrainingSyncReceipt> receipts;
    }
    [Serializable] public sealed class OfflineTrainingEntry
    {
        public TrainingAttempt attempt;
        public TrainingDrill[] lessons;
        public bool completed;
    }
    [Serializable] public sealed class OfflineTrainingFailure
    {
        public TrainingAttempt attempt;
        public OfflineBasicCommand command;
        public string message;
    }
    public enum OfflineCommandKind { TutorialCompleted, TutorialSkipped, OnboardingNew, OnboardingExperienced, TutorialCheckpoint }
    [Serializable] public sealed class OfflineBasicCommand { public string requestId; public OfflineCommandKind kind; public int step; }
    [Serializable] public sealed class OfflineTrainingData
    {
        public OnlineGameSnapshot confirmed;
        public List<OfflineTrainingEntry> pending = new List<OfflineTrainingEntry>();
        public List<OfflineTrainingFailure> rejected = new List<OfflineTrainingFailure>();
        public List<OfflineBasicCommand> commands = new List<OfflineBasicCommand>();
        public List<OfflineTrainingEntry> abandoned = new List<OfflineTrainingEntry>();
    }

    /// <summary>Per-account durable outbox. Contains no password, JWT or refresh token.</summary>
    public sealed class OfflineTrainingStore : IDisposable
    {
        [Serializable] sealed class Envelope { public int schema = 1; public string owner, payload, checksum; }
        readonly string path;
        readonly FileStream lease;
        OfflineTrainingData data = new OfflineTrainingData();
        public string Owner { get; private set; }
        public string RecoveryNotice { get; private set; }
        public bool HasConfirmed => data.confirmed != null;
        public int PendingCount => data.pending.Count;
        public int CompletedCount => data.pending.Count(e => e.completed);
        public int CommandCount => data.commands.Count;
        public IReadOnlyList<OfflineTrainingFailure> Rejected => data.rejected;
        public OfflineTrainingStore(string directory, string owner)
        {
            Guid parsed;
            if (!Guid.TryParseExact(owner, "N", out parsed)) throw new ArgumentException("Mã hồ sơ ngoại tuyến không hợp lệ.");
            Owner = parsed.ToString("N"); Directory.CreateDirectory(directory);
            path = Path.Combine(Path.GetFullPath(directory), Owner + ".json");
            lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            OfflineTrainingData loaded;
            if (TryRead(path, out loaded)) data = loaded;
            else if (TryRead(path + ".bak", out loaded)) { data = loaded; RecoveryNotice = "Đã khôi phục dữ liệu ngoại tuyến từ bản dự phòng."; }
            else if (File.Exists(path)) RecoveryNotice = "Chưa đọc được dữ liệu ngoại tuyến. Bản lỗi vẫn được giữ để kiểm tra.";
        }
        public static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        static string Checksum(string text)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
        bool TryRead(string file, out OfflineTrainingData value)
        {
            value = null;
            try
            {
                if (!File.Exists(file)) return false;
                var envelope = JsonConvert.DeserializeObject<Envelope>(File.ReadAllText(file));
                if (envelope == null || envelope.schema != 1 || envelope.owner != Owner || envelope.payload == null || Checksum(envelope.payload) != envelope.checksum) return false;
                value = JsonConvert.DeserializeObject<OfflineTrainingData>(envelope.payload);
                return value?.confirmed?.player?.id == Owner && value.confirmed.catalog?.lessons != null && value.confirmed.tutorial != null
                    && value.pending != null && value.rejected != null && value.commands != null && value.abandoned != null
                    && value.pending.All(e => e?.attempt?.id != null && e.attempt.playerId == Owner && e.lessons != null)
                    && value.commands.All(c => c != null && c.requestId != null && Enum.IsDefined(typeof(OfflineCommandKind), c.kind));
            }
            catch (Exception error) when (error is IOException || error is JsonException || error is UnauthorizedAccessException) { return false; }
        }
        void Persist()
        {
            string payload = JsonConvert.SerializeObject(data);
            var envelope = new Envelope { owner = Owner, payload = payload, checksum = Checksum(payload) };
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            { writer.Write(JsonConvert.SerializeObject(envelope)); writer.Flush(); stream.Flush(true); }
            if (File.Exists(path))
            {
                OfflineTrainingData previous;
                if (TryRead(path, out previous)) File.Replace(temp, path, path + ".bak");
                else { File.Move(path, path + ".corrupt-" + Guid.NewGuid().ToString("N")); File.Move(temp, path); }
            }
            else File.Move(temp, path);
        }
        public void Confirm(OnlineGameSnapshot snapshot)
        {
            if (snapshot == null || snapshot.player == null || snapshot.player.id != Owner || snapshot.catalog == null || snapshot.catalog.contentSchemaVersion < 2)
                throw new InvalidOperationException("Hồ sơ không hỗ trợ lưu luyện tập ngoại tuyến.");
            if (data.confirmed != null && snapshot.revision < data.confirmed.revision) return;
            data.confirmed = Copy(snapshot); Persist();
        }
        public bool Contains(string attempt) => data.pending.Any(e => e.attempt.id == attempt);
        public OnlineGameSnapshot Project()
        {
            if (!HasConfirmed) throw new InvalidOperationException("Hãy đăng nhập có mạng ít nhất một lần trước khi luyện tập ngoại tuyến.");
            var snapshot = Copy(data.confirmed); var training = TrainingEngine.Ensure(snapshot.player);
            foreach (var entry in data.pending)
            {
                var attempt = Copy(entry.attempt);
                if (attempt.progressEpoch != snapshot.learningEpoch || training.completedAttempts.Contains(attempt.id)) continue;
                var lesson = entry.lessons.FirstOrDefault(l => l.id == attempt.lessonId);
                if (lesson == null) continue;
                var record = TrainingEngine.Record(training, lesson.id);
                if (data.confirmed.player.training?.active?.id != attempt.id) record.attempts++;
                training.active = attempt;
                if (entry.completed) TrainingEngine.Complete(snapshot.player, attempt, entry.lessons);
                snapshot.catalog.lessons = Copy(entry.lessons);
            }
            // Pending money is never spendable. XP/level and stars are provisional.
            snapshot.player.coins = data.confirmed.player.coins; snapshot.player.gems = data.confirmed.player.gems;
            snapshot.player.online = false;
            foreach (var command in data.commands)
            {
                if (command.kind == OfflineCommandKind.TutorialCompleted) snapshot.tutorial.completed = true;
                if (command.kind == OfflineCommandKind.TutorialCheckpoint) snapshot.tutorial.checkpoint = Math.Max(snapshot.tutorial.checkpoint, command.step);
                if (command.kind == OfflineCommandKind.TutorialSkipped) snapshot.tutorial.skipped = true;
                if (command.kind == OfflineCommandKind.OnboardingNew) snapshot.experienceChoice = "NEW";
                if (command.kind == OfflineCommandKind.OnboardingExperienced) snapshot.experienceChoice = "EXPERIENCED";
            }
            return snapshot;
        }
        public OnlineGameSnapshot Start(string code, bool retry)
        {
            var projected = Project();
            var lesson = data.confirmed.catalog.lessons.FirstOrDefault(d => d.id == code);
            if (lesson == null || projected.player.level < lesson.requiredLevel) throw new InvalidOperationException("Bài tập chưa khả dụng trong danh mục đã tải.");
            var active = projected.player.training.active;
            if (active != null && !active.completed && active.lessonId == code && !retry) return projected;
            foreach (var interrupted in data.pending.Where(e => !e.completed).ToArray())
            { data.abandoned.Add(interrupted); data.pending.Remove(interrupted); }
            var attempt = TrainingEngine.Start(projected.player, code, retry, data.confirmed.catalog.lessons);
            attempt.definition = Copy(lesson);
            attempt.catalogVersion = data.confirmed.catalog.version; attempt.progressEpoch = data.confirmed.learningEpoch;
            data.pending.Add(new OfflineTrainingEntry { attempt = Copy(attempt), lessons = Copy(data.confirmed.catalog.lessons) });
            Persist(); return Project();
        }
        public OnlineGameSnapshot Save(TrainingAttempt attempt, bool completed)
        {
            if (attempt == null || attempt.playerId != Owner) throw new InvalidOperationException("Lượt tập không thuộc hồ sơ ngoại tuyến.");
            var entry = data.pending.FirstOrDefault(e => e.attempt.id == attempt.id);
            if (entry == null)
            {
                var confirmed = data.confirmed?.player?.training?.active;
                if (confirmed == null || confirmed.id != attempt.id) throw new InvalidOperationException("Không tìm thấy lượt tập đã bắt đầu.");
                entry = new OfflineTrainingEntry { attempt = Copy(confirmed), lessons = Copy(data.confirmed.catalog.lessons) }; data.pending.Add(entry);
            }
            if (entry.attempt.lessonId != attempt.lessonId || entry.attempt.catalogVersion != attempt.catalogVersion || entry.attempt.progressEpoch != attempt.progressEpoch)
                throw new InvalidOperationException("Phiên bản lượt tập đã thay đổi.");
            if (entry.completed) return Project();
            entry.attempt = Copy(attempt); entry.completed = completed; Persist(); return Project();
        }
        public List<TrainingAttempt> UploadBatch() => data.pending.Where(e => e.completed).Take(50).Select(e => Copy(e.attempt)).ToList();
        public void Accept(OnlineTrainingSyncResult result)
        {
            if (result?.snapshot?.player?.id != Owner || result.receipts == null) throw new InvalidOperationException("Kết quả đồng bộ không thuộc tài khoản.");
            foreach (var receipt in result.receipts)
            {
                var entry = data.pending.FirstOrDefault(e => e.attempt.id == receipt.attemptId && e.completed);
                if (entry == null || !Enum.IsDefined(typeof(TrainingSyncStatus), receipt.status)) continue;
                if (receipt.status == TrainingSyncStatus.Rejected && receipt.retryable) continue;
                if (receipt.status == TrainingSyncStatus.Rejected) data.rejected.Add(new OfflineTrainingFailure { attempt = entry.attempt, message = receipt.message });
                data.pending.Remove(entry);
            }
            if (result.snapshot.revision >= data.confirmed.revision) data.confirmed = Copy(result.snapshot);
            Persist();
        }
        public List<OfflineBasicCommand> Commands() => data.commands.Select(Copy).ToList();
        public OnlineGameSnapshot Queue(OfflineCommandKind kind, string requestId, int step = 0)
        {
            if (!Enum.IsDefined(typeof(OfflineCommandKind), kind) || requestId == null || requestId.Length < 8 || requestId.Length > 64) throw new ArgumentException("Lệnh ngoại tuyến không hợp lệ.");
            if (step < 0 || step > 17) throw new ArgumentException("Bước hướng dẫn không hợp lệ.");
            if (data.commands.Any(c => c.requestId == requestId && (c.kind != kind || c.step != step))) throw new InvalidOperationException("Mã yêu cầu đã được dùng cho nội dung khác.");
            if (!data.commands.Any(c => c.requestId == requestId)) data.commands.Add(new OfflineBasicCommand { kind = kind, requestId = requestId, step = step });
            Persist(); return Project();
        }
        public void AcceptCommand(string requestId, OnlineGameSnapshot snapshot, string rejection = null)
        {
            if (snapshot?.player?.id != Owner) throw new InvalidOperationException("Kết quả đồng bộ không thuộc tài khoản.");
            var command = data.commands.FirstOrDefault(c => c.requestId == requestId);
            if (command == null) return;
            if (rejection != null) data.rejected.Add(new OfflineTrainingFailure { command = command, message = rejection });
            data.commands.Remove(command);
            if (snapshot.revision >= data.confirmed.revision) data.confirmed = Copy(snapshot);
            Persist();
        }
        public void Dispose() => lease.Dispose();
    }
}
