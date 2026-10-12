using System;
using System.Collections.Generic;
using System.Linq;

namespace PickleVerse.Progression
{
    public enum TrainingSkill { Serve, Drive, Dink, Volley, Modifiers, Drop, Reset, Smash, Footwork, Approach, DoublesCover, DoublesTransition }
    public enum TrainingError { WrongInput, Early, Late, Kitchen, Momentum, Net, Out, Target, ServeFeet, Movement, Spacing }
    [Serializable] public sealed class TrainingDrill
    {
        public string id, title, explanation, coaching;
        public TrainingSkill skill;
        public int tier, trials, required, reward, xp;
        public int[] masks;
        public string[] prerequisites;
        public bool Doubles => skill == TrainingSkill.DoublesCover || skill == TrainingSkill.DoublesTransition;
        public string TierName => new[]{"Cơ bản","Kiểm soát","Nâng cao","Đánh đôi"}[tier];
        public int Mask(int trial) => masks[trial % masks.Length];
    }
    public static class TrainingCurriculum
    {
        static TrainingDrill D(string id,string title,TrainingSkill skill,int tier,int[] masks,string explanation,string coaching,params string[] prerequisites)
        {return new TrainingDrill{id=id,title=title,skill=skill,tier=tier,masks=masks,explanation=explanation,coaching=coaching,prerequisites=prerequisites,trials=tier<1?8:tier==1?10:12,required=tier<1?6:tier==1?7:9,reward=new[]{150,225,300,450}[tier],xp=new[]{5,7,10,15}[tier]};}
        public static readonly TrainingDrill[] Lessons = {
            D("serve","Giao bóng đúng ô",TrainingSkill.Serve,0,new[]{1},"Giao dưới tay từ sau baseline. Bóng phải sang ô chéo và vượt cả vạch Kitchen.","Đứng trong vùng sáng phía sau baseline. Giữ rồi thả Drive; không bước vào sân trước khi chạm bóng."),
            D("drive","Drive về cuối sân",TrainingSkill.Drive,0,new[]{1},"Đón bóng sau một lần nảy, đánh Drive vào vùng sâu đã đánh dấu.","Chuẩn bị vợt phía trước. Thả phím sớm hơn lúc bóng tới để animation đưa vợt đến điểm tiếp xúc."),
            D("dink","Dink kiểm soát Kitchen",TrainingSkill.Dink,0,new[]{2},"Đánh nhẹ sau nảy, đưa bóng vào Kitchen đối phương. Được đứng trong Kitchen khi đánh bóng đã nảy.","Đón bóng trước người. Không dùng lực mạnh; ưu tiên kiểm soát và cung bóng thấp."),
            D("volley","Volley ngoài Kitchen",TrainingSkill.Volley,0,new[]{4},"Chặn bóng trước khi nảy. Cả hai chân và đà sau cú đánh phải ở ngoài Kitchen.","Giữ vị trí sau vạch sáng. Chọn Volley khi bóng còn trên không; không lao vào Kitchen sau cú đánh."),
            D("modifiers","Lực và hướng chéo",TrainingSkill.Modifiers,1,new[]{9,17,10,18,12,20},"Luân phiên Drive, Dink, Volley với tăng lực hoặc đánh chéo theo chỉ dẫn từng bóng.","Giữ modifier cho đến khi thả nút đánh. Quan sát vùng đích đổi bên khi cần đánh chéo.","drive","dink","volley"),
            D("drop","Third-shot drop",TrainingSkill.Drop,1,new[]{3},"Sau giao và trả giao đã nảy, đánh cú thứ ba mềm vào Kitchen để lấy thời gian lên lưới.","Ghép Drive + Dink. Đây là cú đánh sau nảy; bóng phải rơi trong vùng sáng gần lưới.","drive","dink"),
            D("reset","Reset bóng nhanh",TrainingSkill.Reset,1,new[]{6},"Dùng Reset để giảm nhịp bóng nhanh, đưa bóng về Kitchen thay vì cố đánh mạnh.","Ghép Dink + Volley, tiếp bóng trên không và giữ cả hai chân ngoài Kitchen.","dink","volley"),
            D("smash","Smash bóng bổng",TrainingSkill.Smash,2,new[]{5},"Đợi bóng bổng đủ cao rồi đánh Smash vào vùng sâu. Không volley trong Kitchen.","Ghép Drive + Volley. Quan sát vòng điểm tiếp bóng trên sân và thả khi chỉ báo timing vào vùng xanh.","volley","modifiers"),
            D("footwork","Di chuyển và hồi vị",TrainingSkill.Footwork,2,new[]{1,17},"Đón bóng luân phiên hai bên sân, đánh Drive rồi trở về vị trí hồi phục.","Di chuyển A/D hoặc joystick tới vùng đón bóng. Có thể dùng chạy; sau cú đánh quay về vòng hồi vị.","drive","modifiers"),
            D("approach","Drop rồi tiến lên",TrainingSkill.Approach,2,new[]{3},"Đánh drop từ cuối sân, tiến lên vùng sẵn sàng trước Kitchen. Dừng có kiểm soát trước lượt tiếp theo.","Ghép Drive + Dink rồi di chuyển W lên vùng sáng. Đừng chạy qua vạch Kitchen để đón volley.","drop","footwork"),
            D("doubles-cover","Đánh đôi: giữ khoảng cách",TrainingSkill.DoublesCover,3,new[]{4,20},"Hai người giữ hàng ngang, dịch chuyển cùng phía bóng. Đánh bóng phần sân của bạn và giữ khe giữa đội nhỏ.","Đồng đội AI đi cùng bạn. Dịch chuyển ngang, đánh Volley theo chỉ dẫn, rồi hồi vị để cả đội giữ hàng ngang.","volley","footwork"),
            D("doubles-transition","Đánh đôi: drop và phối hợp",TrainingSkill.DoublesTransition,3,new[]{3,2,4},"Đội giao chờ trả giao nảy, dùng drop tiến lên cùng nhau rồi chuyển sang dink và volley. Có lượt bóng của đồng đội AI.","Theo dấu vị trí của bạn. Sau drop tiến lên cùng đồng đội; khi hiện ‘Bóng của đồng đội’, giữ hàng ngang và để AI xử lý.","approach","doubles-cover","reset")
        };
        public static TrainingDrill Find(string id)=>Lessons.FirstOrDefault(d=>d.id==id);
        public static string ErrorName(TrainingError e)=>new[]{"Sai phím hoặc kỹ thuật","Đánh quá sớm","Đánh hụt / quá muộn","Volley trong Kitchen","Đà volley vào Kitchen","Bóng chạm lưới","Bóng ngoài sân","Chưa trúng vùng đích","Chân giao bóng sai","Chưa hồi vị / tiến lên","Khoảng cách đội hình"}[(int)e];
        public static string Tip(TrainingError e)=>new[]{"Giữ đúng tổ hợp trên HUD; giữ modifier đến lúc thả nút đánh.","Đợi bóng nảy nếu bài yêu cầu. Thả nút khi vợt có đủ thời gian tới bóng.","Di chuyển vào vòng đón bóng và thả sớm hơn thời điểm tiếp xúc.","Lùi cả hai chân ra ngoài vạch Kitchen trước khi volley.","Dừng lại sau volley; đừng để đà chạy đưa chân vào Kitchen.","Tiếp bóng phía trước người và đúng độ cao của kỹ thuật.","Giữ bóng trong đường biên; ưu tiên kiểm soát hơn lực.","Đón bóng đúng vị trí và chọn đúng hướng thẳng / chéo của vùng sáng.","Giữ chân sau baseline, giữa đường giữa sân và biên dọc khi giao.","Sau cú đánh, di chuyển về vùng hồi vị được đánh dấu.","Giữ cùng hàng với đồng đội, khoảng cách 1–3 mét và không để khe giữa quá lớn."}[(int)e];
    }
    [Serializable] public sealed class TrainingErrorCount {public TrainingError error;public int count;}
    [Serializable] public sealed class TrainingAttempt
    {
        public string id,lessonId,playerId;
        public int trials,successes,contacts;
        public double seconds,timingTotal;
        public bool completed,passed,review;
        public int stars,rewardTokenpad,rewardXp;
        public List<TrainingErrorCount> errors=new List<TrainingErrorCount>();
        public double Accuracy=>trials==0?0:(double)successes/trials;
        public double Timing=>contacts==0?1:timingTotal/contacts;
        public int Errors=>errors.Sum(e=>e.count);
        public void Error(TrainingError error){var row=errors.Find(e=>e.error==error);if(row==null){row=new TrainingErrorCount{error=error};errors.Add(row);}row.count++;}
    }
    [Serializable] public sealed class TrainingLessonRecord
    {
        public string id;
        public int attempts,retries,completions,bestStars,trials,successes;
        public double seconds,bestAccuracy;
        public List<TrainingErrorCount> errors=new List<TrainingErrorCount>();
        public List<float> accuracyHistory=new List<float>();
    }
    [Serializable] public sealed class TrainingProfile
    {
        public int version=1;
        public string lastLesson="serve";
        public TrainingAttempt active;
        public List<TrainingLessonRecord> lessons=new List<TrainingLessonRecord>();
        public List<string> completedAttempts=new List<string>();
    }
    public static class TrainingEngine
    {
        public static TrainingProfile Ensure(PlayerProfile p)
        {
            if(p.training==null)p.training=new TrainingProfile();var t=p.training;
            if(t.lessons==null)t.lessons=new List<TrainingLessonRecord>();if(t.completedAttempts==null)t.completedAttempts=new List<string>();
            if(t.active!=null&&string.IsNullOrEmpty(t.active.id))t.active=null;
            return t;
        }
        public static TrainingLessonRecord Record(TrainingProfile p,string id)
        {var r=p.lessons.Find(x=>x.id==id);if(r==null){r=new TrainingLessonRecord{id=id};p.lessons.Add(r);}return r;}
        public static bool Unlocked(TrainingProfile p,TrainingDrill d)=>d.prerequisites.All(id=>p.lessons.Any(r=>r.id==id&&r.bestStars>0));
        public static bool Completed(TrainingProfile p,string id)=>p.lessons.Any(r=>r.id==id&&(r.bestStars>0||r.completions>0));
        public static string Recommended(TrainingProfile p)
        {
            if(p.active!=null&&!p.active.completed&&TrainingCurriculum.Find(p.active.lessonId)!=null)return p.active.lessonId;
            var next=TrainingCurriculum.Lessons.FirstOrDefault(d=>Unlocked(p,d)&&!p.lessons.Any(r=>r.id==d.id&&r.bestStars>0));
            if(next!=null)return next.id;
            return p.lessons.OrderBy(r=>r.bestAccuracy).ThenByDescending(r=>r.retries).Select(r=>r.id).FirstOrDefault()??"serve";
        }
        public static TrainingAttempt Start(PlayerProfile p,string id,bool retry=false)
        {
            var data=Ensure(p);var lesson=TrainingCurriculum.Find(id);
            if(lesson==null||!Unlocked(data,lesson))throw new InvalidOperationException("Bài tập chưa mở khóa.");
            var record=Record(data,id);if(retry||record.attempts>0)record.retries++;record.attempts++;
            data.lastLesson=id;data.active=new TrainingAttempt{id=Guid.NewGuid().ToString("N"),lessonId=id,playerId=p.id,review=Completed(data,id)};return data.active;
        }
        public static bool Complete(PlayerProfile p,TrainingAttempt a)
        {
            var data=Ensure(p);var lesson=TrainingCurriculum.Find(a.lessonId);
            if(lesson==null||a.playerId!=p.id||data.active?.id!=a.id||a.trials!=lesson.trials||a.successes<0||a.successes>a.trials||data.completedAttempts.Contains(a.id))return false;
            // Check saved mastery too: old checkpoints predate the review flag.
            a.review|=Completed(data,a.lessonId);a.rewardTokenpad=a.rewardXp=0;
            a.completed=true;a.passed=a.successes>=lesson.required;
            a.stars=!a.passed?0:a.Accuracy>=.90&&a.Timing<=.16?3:a.Accuracy>=.80?2:1;
            var r=Record(data,a.lessonId);r.completions+=a.passed?1:0;r.bestStars=Math.Max(r.bestStars,a.stars);r.bestAccuracy=Math.Max(r.bestAccuracy,a.Accuracy);
            r.trials+=a.trials;r.successes+=a.successes;r.seconds+=a.seconds;r.accuracyHistory.Add((float)a.Accuracy);if(r.accuracyHistory.Count>8)r.accuracyHistory.RemoveAt(0);
            foreach(var e in a.errors){var existing=r.errors.Find(x=>x.error==e.error);if(existing==null){existing=new TrainingErrorCount{error=e.error};r.errors.Add(existing);}existing.count+=e.count;}
            if(a.passed&&!a.review){_ = checked(p.coins+lesson.reward);ProgressionEngine.Grant(p,new RewardBundle(lesson.reward,0,lesson.xp));a.rewardTokenpad=lesson.reward;a.rewardXp=lesson.xp;}
            data.completedAttempts.Add(a.id);data.active=a;return true;
        }
    }
}
