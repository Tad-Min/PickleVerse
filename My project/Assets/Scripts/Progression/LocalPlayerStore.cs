using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace PickleVerse.Progression
{
    public sealed class LocalPlayerStore : IDisposable
    {
        readonly string root;
        FileStream lease;
        public PlayerProfile Current { get; private set; }
        public string Root => root;
        public LocalPlayerStore(string directory)
        {
            root = directory;
            Directory.CreateDirectory(root);
        }
        public static bool ValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 24 && id.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9');
        public static string RandomName()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            return new string(bytes.Take(4).Select(b => (char)('A' + b % 26)).ToArray());
        }
        public static string CleanName(string name)
        {
            return new string((name ?? "").Trim().Where(c => !char.IsControl(c) && c != '\u200b').Take(18).ToArray());
        }
        string PathFor(string id) => Path.Combine(root, id + ".json");
        public void Open(string requestedId = null)
        {
            if (Current != null)
                throw new InvalidOperationException("Player đã được mở.");
            using (SelectionLock())
            {
                if (requestedId != null)
                {
                    Select(requestedId, false);
                    return;
                }
                string preferredPath = Path.Combine(root, "preferred.txt");
                string preferred = File.Exists(preferredPath) ? File.ReadAllText(preferredPath).Trim() : "";
                var candidates = Directory.GetFiles(root, "*.json").Select(Path.GetFileNameWithoutExtension).Where(ValidId).OrderBy(s => s).ToList();
                if (ValidId(preferred))
                {
                    candidates.Remove(preferred);
                    candidates.Insert(0, preferred);
                }
                foreach (string candidate in candidates)
                {
                    if (TryLease(candidate, out var acquired))
                    {
                        try
                        {
                            Current = Load(candidate);
                            lease = acquired;
                            return;
                        }
                        catch
                        {
                            acquired.Dispose();
                            throw;
                        }
                    }
                }
                string fresh = "P" + Guid.NewGuid().ToString("N").Substring(0, 23);
                Select(fresh, false);
                if (!ValidId(preferred))
                    WriteAtomic(preferredPath, fresh);
            }
        }
        public void Switch(string id, string newName = null)
        {
            using (SelectionLock())
                Select(id, true);
            if (!string.IsNullOrWhiteSpace(newName))
                Transaction(p => p.playerName = CleanName(newName));
        }
        void Select(string id, bool preferred)
        {
            if (!ValidId(id))
                throw new ArgumentException("ID gồm 1–24 chữ cái tiếng Anh hoặc chữ số.");
            if (Current?.id == id)
                return;
            if (!TryLease(id, out var acquired))
                throw new IOException("Player này đang được sử dụng ở một phiên khác.");
            try
            {
                var next = File.Exists(PathFor(id)) ? Load(id) : new PlayerProfile { id = id, playerName = RandomName(), createdUtcTicks = DateTime.UtcNow.Ticks };
                WriteAtomic(PathFor(id), JsonUtility.ToJson(next, true));
                if (preferred)
                    WriteAtomic(Path.Combine(root, "preferred.txt"), id);
                Save();
                lease?.Dispose();
                lease = acquired;
                Current = next;
            }
            catch
            {
                acquired.Dispose();
                throw;
            }
        }
        bool TryLease(string id, out FileStream stream)
        {
            try
            {
                stream = new FileStream(Path.Combine(root, id + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                stream = null;
                return false;
            }
        }
        FileStream SelectionLock()
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return new FileStream(Path.Combine(root, "selection.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    if (attempt >= 100)
                        throw;
                    Thread.Sleep(20);
                }
            }
        }
        PlayerProfile Load(string id)
        {
            string path = PathFor(id);
            Exception first;
            try
            {
                return Parse(File.ReadAllText(path), id);
            }
            catch (Exception e)
            {
                first = e;
            }
            if (File.Exists(path + ".bak"))
            {
                try
                {
                    var recovered = Parse(File.ReadAllText(path + ".bak"), id);
                    File.Move(path, path + ".corrupt." + Guid.NewGuid().ToString("N"));
                    WriteAtomic(path, JsonUtility.ToJson(recovered, true));
                    return recovered;
                }
                catch (Exception) { }
            }
            throw new IOException("Không đọc được dữ liệu player hoặc bản dự phòng: " + id, first);
        }
        static PlayerProfile Parse(string json, string id)
        {
            var p = JsonUtility.FromJson<PlayerProfile>(json);
            if (p == null || p.id != id || p.level < 1 || p.elo < 100 || p.lifetime == null || p.daily == null || p.weekly == null || p.history == null || p.processedMatches == null || p.achievementClaims == null)
                throw new InvalidDataException("Dữ liệu player không hợp lệ.");
            if (p.previousDays == null)
                p.previousDays = new System.Collections.Generic.List<MissionPeriod>();
            if (p.previousWeeks == null)
                p.previousWeeks = new System.Collections.Generic.List<MissionPeriod>();
            // JsonUtility writes a null nested object as its empty default shape.
            // An empty/already-applied match is not an interrupted match on the next launch.
            if (p.pendingMatch != null && (string.IsNullOrEmpty(p.pendingMatch.id) || p.processedMatches.Contains(p.pendingMatch.id)))
                p.pendingMatch = null;
            return p;
        }
        public void Transaction(Action<PlayerProfile> action)
        {
            string before = JsonUtility.ToJson(Current);
            try
            {
                action(Current);
                Save();
            }
            catch
            {
                Current = Parse(before, Current.id);
                throw;
            }
        }
        public void Save()
        {
            if (Current != null)
                WriteAtomic(PathFor(Current.id), JsonUtility.ToJson(Current, true));
        }
        public static void WriteAtomic(string path, string value)
        {
            string temp = path + ".tmp";
            var bytes = new UTF8Encoding(false).GetBytes(value);
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes, 0, bytes.Length);
                file.Flush(true);
            }
            if (File.Exists(path))
                File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        public void Dispose()
        {
            try
            {
                Save();
            }
            finally
            {
                lease?.Dispose();
                lease = null;
                Current = null;
            }
        }
    }
}
