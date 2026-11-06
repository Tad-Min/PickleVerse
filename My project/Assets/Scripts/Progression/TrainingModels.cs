using System;
using System.Collections.Generic;
using System.Linq;

namespace PickleVerse.Progression
{
    public static class TrainingCurriculum
    {
        static TrainingDrill D(string id, string title, TrainingSkill skill, int tier, int[] masks, string explanation, string coaching, params string[] prerequisites)
        {
            return new TrainingDrill { alternateTargets = id == "serve-alternating", requireRecovery = id == "volley-recovery", targetRadius = tier < 2 ? .9f : .7f, id = id, title = title, skill = skill, tier = tier, masks = masks, explanation = explanation, coaching = coaching, prerequisites = prerequisites, trials = tier < 1 ? 8 : tier == 1 ? 10 : 12, required = tier < 1 ? 6 : tier == 1 ? 7 : 9, reward = new[] { 150, 225, 300, 450 }[tier], xp = new[] { 5, 7, 10, 15 }[tier] };
        }
        public static TrainingDrill[] Lessons { get; private set; } = new TrainingDrill[] {
            D("serve", "Giao bóng đúng ô", TrainingSkill.Serve, 0, new[]{1}, "Giao dưới tay từ sau đường biên cuối sân. Bóng phải sang ô chéo và vượt cả vạch vùng cấm vô lê.", "Đứng trong vùng sáng phía sau đường biên cuối sân. Giữ rồi thả Đánh bạt; không bước vào sân trước khi chạm bóng."),
            D("drive", "Đánh bạt về cuối sân", TrainingSkill.Drive, 0, new[]{1}, "Đón bóng sau một lần nảy, đánh Đánh bạt vào vùng sâu đã đánh dấu.", "Chuẩn bị vợt phía trước. Thả phím sớm hơn lúc bóng tới để hoạt ảnh đưa vợt đến điểm tiếp xúc."),
            D("dink", "Bóng ngắn kiểm soát vùng cấm vô lê", TrainingSkill.Dink, 0, new[]{2}, "Đánh nhẹ sau nảy, đưa bóng vào vùng cấm vô lê đối phương. Được đứng trong vùng cấm vô lê khi đánh bóng đã nảy.", "Đón bóng trước người. Không dùng lực mạnh; ưu tiên kiểm soát và cung bóng thấp."),
            D("volley", "Vô lê ngoài vùng cấm vô lê", TrainingSkill.Volley, 0, new[]{4}, "Chặn bóng trước khi nảy. Cả hai chân và đà sau cú đánh phải ở ngoài vùng cấm vô lê.", "Giữ vị trí sau vạch sáng. Chọn Vô lê khi bóng còn trên không; không lao vào vùng cấm vô lê sau cú đánh."),
            D("modifiers", "Lực và hướng chéo", TrainingSkill.Modifiers, 1, new[]{9, 17, 10, 18, 12, 20}, "Luân phiên Đánh bạt, Bóng ngắn, Vô lê với tăng lực hoặc đánh chéo theo chỉ dẫn từng bóng.", "Giữ modifier cho đến khi thả nút đánh. Quan sát vùng đích đổi bên khi cần đánh chéo.", "drive", "dink", "volley"),
            D("drop", "Third-shot drop", TrainingSkill.Drop, 1, new[]{3}, "Sau giao và trả giao đã nảy, đánh cú thứ ba mềm vào vùng cấm vô lê để lấy thời gian lên lưới.", "Ghép Đánh bạt + Bóng ngắn. Đây là cú đánh sau nảy; bóng phải rơi trong vùng sáng gần lưới.", "drive", "dink"),
            D("reset", "Hãm bóng bóng nhanh", TrainingSkill.Reset, 1, new[]{6}, "Dùng Hãm bóng để giảm nhịp bóng nhanh, đưa bóng về vùng cấm vô lê thay vì cố đánh mạnh.", "Ghép Bóng ngắn + Vô lê, tiếp bóng trên không và giữ cả hai chân ngoài vùng cấm vô lê.", "dink", "volley"),
            D("smash", "Đập bóng bóng bổng", TrainingSkill.Smash, 2, new[]{5}, "Đợi bóng bổng đủ cao rồi đánh Đập bóng vào vùng sâu. Không vô lê trong vùng cấm vô lê.", "Ghép Đánh bạt + Vô lê. Quan sát vòng điểm tiếp bóng trên sân và thả khi chỉ báo thời điểm vào vùng xanh.", "volley", "modifiers"),
            D("footwork", "Di chuyển và hồi vị", TrainingSkill.Footwork, 2, new[]{1, 17}, "Đón bóng luân phiên hai bên sân, đánh Đánh bạt rồi trở về vị trí hồi phục.", "Di chuyển A/D hoặc cần điều khiển tới vùng đón bóng. Có thể dùng chạy; sau cú đánh quay về vòng hồi vị.", "drive", "modifiers"),
            D("approach", "Bỏ nhỏ rồi tiến lên", TrainingSkill.Approach, 2, new[]{3}, "Đánh bỏ nhỏ từ cuối sân, tiến lên vùng sẵn sàng trước vùng cấm vô lê. Dừng có kiểm soát trước lượt tiếp theo.", "Ghép Đánh bạt + Bóng ngắn rồi di chuyển W lên vùng sáng. Đừng chạy qua vạch vùng cấm vô lê để đón vô lê.", "drop", "footwork"),
            D("doubles-cover", "Đánh đôi: giữ khoảng cách", TrainingSkill.DoublesCover, 3, new[]{4, 20}, "Hai người giữ hàng ngang, dịch chuyển cùng phía bóng. Đánh bóng phần sân của bạn và giữ khe giữa đội nhỏ.", "Đồng đội AI đi cùng bạn. Dịch chuyển ngang, đánh Vô lê theo chỉ dẫn, rồi hồi vị để cả đội giữ hàng ngang.", "volley", "footwork"),
            D("doubles-transition", "Đánh đôi: bỏ nhỏ và phối hợp", TrainingSkill.DoublesTransition, 3, new[]{3, 2, 4}, "Đội giao chờ trả giao nảy, dùng bỏ nhỏ tiến lên cùng nhau rồi chuyển sang bóng ngắn và vô lê. Có lượt bóng của đồng đội AI.", "Theo dấu vị trí của bạn. Sau bỏ nhỏ tiến lên cùng đồng đội; khi hiện ‘Bóng của đồng đội’, giữ hàng ngang và để AI xử lý.", "approach", "doubles-cover", "reset"),
            D("serve-alternating", "Giao bóng luân phiên hai ô", TrainingSkill.Serve, 1, new[]{1}, "Luân phiên giao từ hai bên đường cuối sân. Đưa bóng vào đúng ô chéo và ngoài Kitchen.", "Quan sát vị trí bắt đầu đổi bên. Giữ chân sau đường cuối sân và giao vào vùng đích.", "serve"),
            D("dink-cross", "Dink chéo sân", TrainingSkill.Dink, 1, new[]{18}, "Đánh Dink chéo sân sau một lần nảy, đưa bóng vào vùng Kitchen đã đánh dấu.", "Giữ hướng chéo đến khi thả nút. Ưu tiên kiểm soát bóng ngắn và đứng cân bằng.", "dink", "modifiers"),
            D("volley-recovery", "Volley rồi hồi vị", TrainingSkill.Volley, 2, new[]{4, 20}, "Đón Volley ngoài Kitchen rồi trở về vòng hồi vị trước lượt tiếp theo.", "Dừng đà sau cú đánh, giữ chân ngoài Kitchen và quay về vòng hồi vị.", "volley", "footwork")
        };
        public static void Configure(TrainingDrill[] lessons)
        {
            if (lessons == null || lessons.Length == 0 || lessons.Any(d => d == null || string.IsNullOrWhiteSpace(d.id) || d.masks == null || d.masks.Length == 0 || d.trials < 1 || d.required < 1 || d.required > d.trials || d.tier < 0 || d.tier > 3) || lessons.Select(d => d.id).Distinct().Count() != lessons.Length)
                throw new ArgumentException("Danh mục bài học máy chủ không hợp lệ.");
            if (lessons.Any(d => (d.prerequisites ?? Array.Empty<string>()).Any(id => !lessons.Any(other => other.id == id))))
                throw new ArgumentException("Thiếu bài học điều kiện.");
            Lessons = lessons;
        }
        public static TrainingDrill Find(string id) => Lessons.FirstOrDefault(d => d.id == id);
        public static string ErrorName(TrainingError e) => new[] { "Sai phím hoặc kỹ thuật", "Đánh quá sớm", "Đánh hụt / quá muộn", "Volley trong Kitchen", "Đà vô lê vào vùng cấm vô lê", "Bóng chạm lưới", "Bóng ngoài sân", "Chưa trúng vùng đích", "Chân giao bóng sai", "Chưa hồi vị / tiến lên", "Khoảng cách đội hình" }[(int)e];
        public static string Tip(TrainingError e) => new[] { "Giữ đúng tổ hợp trên HUD; giữ modifier đến lúc thả nút đánh.", "Đợi bóng nảy nếu bài yêu cầu. Thả nút khi vợt có đủ thời gian tới bóng.", "Di chuyển vào vòng đón bóng và thả sớm hơn thời điểm tiếp xúc.", "Lùi cả hai chân ra ngoài vạch vùng cấm vô lê trước khi vô lê.", "Dừng lại sau vô lê; đừng để đà chạy đưa chân vào vùng cấm vô lê.", "Tiếp bóng phía trước người và đúng độ cao của kỹ thuật.", "Giữ bóng trong đường biên; ưu tiên kiểm soát hơn lực.", "Đón bóng đúng vị trí và chọn đúng hướng thẳng / chéo của vùng sáng.", "Giữ chân sau đường biên cuối sân, giữa đường giữa sân và biên dọc khi giao.", "Sau cú đánh, di chuyển về vùng hồi vị được đánh dấu.", "Giữ cùng hàng với đồng đội, khoảng cách 1–3 mét và không để khe giữa quá lớn." }[(int)e];
    }
    public static class TrainingEngine
    {
        public static TrainingProfile Ensure(PlayerProfile p)
        {
            if (p.training == null)
                p.training = new TrainingProfile();
            var t = p.training;
            if (t.lessons == null)
                t.lessons = new List<TrainingLessonRecord>();
            if (t.completedAttempts == null)
                t.completedAttempts = new List<string>();
            if (t.active != null && string.IsNullOrEmpty(t.active.id))
                t.active = null;
            return t;
        }
        public static TrainingLessonRecord Record(TrainingProfile p, string id)
        {
            var r = p.lessons.Find(x => x.id == id);
            if (r == null)
            {
                r = new TrainingLessonRecord { id = id };
                p.lessons.Add(r);
            }
            return r;
        }
        public static bool Unlocked(TrainingProfile p, TrainingDrill d) => d.prerequisites.All(id => p.lessons.Any(r => r.id == id && r.bestStars > 0));
        public static bool Completed(TrainingProfile p, string id) => p.lessons.Any(r => r.id == id && (r.bestStars > 0 || r.completions > 0));
        public static string Recommended(TrainingProfile p, TrainingDrill[] catalog = null)
        {
            var lessons = catalog ?? TrainingCurriculum.Lessons;
            if (p.active != null && !p.active.completed && lessons.Any(d => d.id == p.active.lessonId))
                return p.active.lessonId;
            var next = lessons.FirstOrDefault(d => Unlocked(p, d) && !p.lessons.Any(r => r.id == d.id && r.bestStars > 0));
            if (next != null)
                return next.id;
            return p.lessons.OrderBy(r => r.bestAccuracy).ThenByDescending(r => r.retries).Select(r => r.id).FirstOrDefault() ?? "serve";
        }
        public static TrainingAttempt Start(PlayerProfile p, string id, bool retry = false, TrainingDrill[] catalog = null)
        {
            var data = Ensure(p);
            var lesson = (catalog ?? TrainingCurriculum.Lessons).FirstOrDefault(d => d.id == id);
            if (lesson == null || !Unlocked(data, lesson))
                throw new InvalidOperationException("Bài tập chưa mở khóa.");
            var record = Record(data, id);
            if (retry || record.attempts > 0)
                record.retries++;
            record.attempts++;
            data.lastLesson = id;
            data.active = new TrainingAttempt { id = Guid.NewGuid().ToString("N"), lessonId = id, playerId = p.id, review = Completed(data, id) };
            return data.active;
        }
        public static bool Complete(PlayerProfile p, TrainingAttempt a, TrainingDrill[] catalog = null)
        {
            var data = Ensure(p);
            var lesson = (catalog ?? TrainingCurriculum.Lessons).FirstOrDefault(d => d.id == a.lessonId);
            if (lesson == null || a.playerId != p.id || data.active?.id != a.id || a.trials != lesson.trials || a.successes < 0 || a.successes > a.trials || data.completedAttempts.Contains(a.id))
                return false;
            // Check saved mastery too: old checkpoints predate the review flag.
            a.review |= Completed(data, a.lessonId);
            a.rewardTokenpad = a.rewardXp = 0;
            a.completed = true;
            a.passed = a.successes >= lesson.required;
            a.stars = !a.passed ? 0 : a.Accuracy >= .90 && a.Timing <= .16 ? 3 : a.Accuracy >= .80 ? 2 : 1;
            var r = Record(data, a.lessonId);
            r.completions += a.passed ? 1 : 0;
            r.bestStars = Math.Max(r.bestStars, a.stars);
            r.bestAccuracy = Math.Max(r.bestAccuracy, a.Accuracy);
            r.trials += a.trials;
            r.successes += a.successes;
            r.seconds += a.seconds;
            r.accuracyHistory.Add((float)a.Accuracy);
            if (r.accuracyHistory.Count > 8)
                r.accuracyHistory.RemoveAt(0);
            foreach (var e in a.errors)
            {
                var existing = r.errors.Find(x => x.error == e.error);
                if (existing == null)
                {
                    existing = new TrainingErrorCount { error = e.error };
                    r.errors.Add(existing);
                }
                existing.count += e.count;
            }
            if (a.passed && !a.review)
            {
                _ = checked(p.coins + lesson.reward);
                ProgressionEngine.Grant(p, new RewardBundle(lesson.reward, 0, lesson.xp));
                a.rewardTokenpad = lesson.reward;
                a.rewardXp = lesson.xp;
            }
            data.completedAttempts.Add(a.id);
            data.active = a;
            return true;
        }
    }

}
