using System;
using UnityEngine;
using PickleVerse.Rules;

namespace PickleVerse.LocalMatch
{
    public enum TutorialKind { Court, Kitchen, Serve, Net, Out, DoubleBounce, KitchenVolley, Movement, Shot, Complete }
    public sealed class TutorialLesson
    {
        public TutorialKind kind; public string title, body; public int mask; public float minimum;
        public TutorialLesson(TutorialKind kind, string title, string body, float minimum = 3, int mask = 0)
        {
            this.kind = kind;
            this.title = title;
            this.body = body;
            this.minimum = minimum;
            this.mask = mask;
        }
    }
    public static class TutorialCurriculum
    {
        public const int FirstShot = 8, ShotCount = 9;
        public static readonly TutorialLesson[] Lessons = {
            new TutorialLesson(TutorialKind.Court, "Làm quen sân đấu", "Sân dài 13,41 m và rộng 6,10 m. Lưới chia sân thành hai nửa. Bóng chạm vạch biên vẫn ở trong sân.", 4),
            new TutorialLesson(TutorialKind.Kitchen, "Kitchen • Vùng không volley", "Mỗi bên lưới có vùng sâu 2,13 m, gồm cả vạch kitchen. Bạn được vào đây để đánh bóng đã nảy; không được volley khi chạm vùng này.", 4),
            new TutorialLesson(TutorialKind.Serve, "Giao bóng và hai lần nảy", "Giao dưới tay từ sau đường cuối sân, vào ô chéo và vượt kitchen. Bóng phải nảy bên nhận, rồi nảy bên giao. Sau đó mới được volley.", 6),
            new TutorialLesson(TutorialKind.Net, "Lỗi • Bóng không qua lưới", "Bóng mắc lưới và rơi lại phía bạn sẽ kết thúc pha bóng. Chạm lưới rồi vẫn qua sân hợp lệ không tự động là lỗi.", 3),
            new TutorialLesson(TutorialKind.Out, "Lỗi • Bóng ra ngoài", "Bóng chạm đất ngoài đường biên là out. Bóng chạm vạch biên vẫn trong sân. Riêng khi giao bóng, vạch kitchen thuộc vùng không được rơi vào.", 3),
            new TutorialLesson(TutorialKind.DoubleBounce, "Lỗi • Nảy hai lần", "Đỡ bóng trước khi nó nảy lần thứ hai trên phía sân của bạn. Hãy nhìn bóng đang tới và chuẩn bị vợt sớm.", 3),
            new TutorialLesson(TutorialKind.KitchenVolley, "Lỗi • Volley trong kitchen", "Không volley khi chân chạm kitchen hoặc vạch kitchen. Sau volley, đà lao vào vùng này cũng là lỗi. Đánh bóng đã nảy trong kitchen thì được phép.", 4),
            new TutorialLesson(TutorialKind.Movement, "Di chuyển vào vị trí", "Di chuyển một đoạn ngắn bằng các phím hướng, rồi thử chạy. Trên điện thoại, kéo cần để đi; giữ nút CHẠY bên phải để chạy nhanh.", 0),
            new TutorialLesson(TutorialKind.Shot, "Drive • Đánh sau nảy", "Đối phương sẽ đưa bóng tới. Drive đẩy bóng sâu qua sân. Bấm rồi thả nút đánh; bài tập sẽ giúp bạn canh lúc bóng đến.", 0, 1),
            new TutorialLesson(TutorialKind.Shot, "Dink • Đánh nhẹ", "Chờ bóng nảy rồi đánh nhẹ qua lưới vào kitchen bên kia. Bạn có thể bước vào kitchen để đánh bóng đã nảy.", 0, 2),
            new TutorialLesson(TutorialKind.Shot, "Volley • Đánh trên không", "Đánh bóng trước khi nó nảy và đứng ngoài kitchen. Pha luyện này bắt đầu sau hai lần nảy bắt buộc ở đầu rally.", 0, 4),
            new TutorialLesson(TutorialKind.Shot, "Drive + Lực", "Giữ nút Lực, bấm rồi thả nút Drive. Bóng đi nhanh hơn. Chỉ cần thực hiện một lần để làm quen.", 0, 9),
            new TutorialLesson(TutorialKind.Shot, "Tăng tốc", "Giữ nút Lực, bấm rồi thả nút Dink. Tổ hợp này chuyển sang cú tăng tốc sau nảy.", 0, 10),
            new TutorialLesson(TutorialKind.Shot, "Volley + Lực", "Giữ nút Lực, bấm rồi thả nút Volley. Đây là cú đẩy volley mạnh hơn; chân vẫn phải ngoài kitchen.", 0, 12),
            new TutorialLesson(TutorialKind.Shot, "Drive chéo", "Giữ nút Chéo, bấm rồi thả nút Drive. Đưa bóng chéo về nửa sân đối diện để thay đổi hướng đánh.", 0, 17),
            new TutorialLesson(TutorialKind.Shot, "Dink chéo", "Giữ nút Chéo, bấm rồi thả nút Dink. Bóng nảy trước khi đánh, rồi được đưa nhẹ theo hướng chéo.", 0, 18),
            new TutorialLesson(TutorialKind.Shot, "Volley chéo", "Giữ nút Chéo, bấm rồi thả nút Volley. Đánh trên không và hướng bóng chéo, từ vị trí ngoài kitchen.", 0, 20),
            new TutorialLesson(TutorialKind.Complete, "Sẵn sàng vào sân", "Bạn đã biết sân, kitchen, hai lần nảy và 9 thao tác đánh cơ bản. Đấu đơn thường chơi đến 11 điểm, cách 2; chỉ bên giao bóng ghi điểm. Những bài nâng cao sẽ ở phần luyện tập khác.", 0)
        };
        public static ShotIntent Intent(int mask)
        {
            int b = mask & 7;
            return new ShotIntent(b == 4 ? ShotKind.Volley : b == 2 && (mask & 8) == 0 ? ShotKind.Dink : ShotKind.Drive, (mask & 8) != 0, (mask & 16) != 0);
        }
        public static bool Matches(int expected, int committed, ShotIntent shot)
        {
            var correct = Intent(expected);
            return expected == committed && shot.kind == correct.kind && shot.power == correct.power && shot.cross == correct.cross;
        }
        public static SinglesRules RallyReady()
        {
            var rules = new SinglesRules(1);
            rules.PrepareServe(1);
            rules.Serve(1, new CourtPoint(-1.2f, 0, 7.3f), new CourtPoint(-1.4f, 0, 7.3f), new CourtPoint(-1.3f, .7f, 7.1f), 1);
            rules.Bounce(new CourtPoint(1.3f, Court.BallRadius, -4));
            rules.Hit(0, new CourtPoint(1.3f, .9f, -4), false);
            rules.Bounce(new CourtPoint(-1.3f, Court.BallRadius, 4));
            rules.Hit(1, new CourtPoint(-1.3f, .9f, 4), false);
            return rules;
        }
    }
    /// <summary>Same gravity/rebound as gameplay; solve a coach feed through the authored contact pose.</summary>
    public struct TutorialFeed
    {
        public Vector3 origin, velocity, bouncePoint, rebound;
        public float bounceTime, duration;
        public bool hasBounce;
        public Vector3 At(float time)
        {
            if (hasBounce && time >= bounceTime)
            {
                float t = time - bounceTime;
                return bouncePoint + rebound * t + Vector3.down * (.5f * BallFlight.Gravity * t * t);
            }
            return origin + velocity * time + Vector3.down * (.5f * BallFlight.Gravity * time * time);
        }
        public static TutorialFeed Create(Vector3 from, Vector3 contact, bool bounce)
        {
            float g = BallFlight.Gravity, apex = Mathf.Max(2.6f, from.y + .5f), up = Mathf.Sqrt(2 * g * (apex - from.y));
            var plan = new TutorialFeed { origin = from, hasBounce = bounce };
            if (!bounce)
            {
                plan.duration = (up + Mathf.Sqrt(up * up + 2 * g * (from.y - contact.y))) / g;
                plan.velocity = (contact - from) / plan.duration;
                plan.velocity.y = up;
                return plan;
            }
            float fall = Mathf.Sqrt(up * up + 2 * g * (from.y - Court.BallRadius)), rise = fall * BallFlight.Restitution;
            float second = (rise - Mathf.Sqrt(Mathf.Max(.01f, rise * rise - 2 * g * (contact.y - Court.BallRadius)))) / g;
            plan.bounceTime = (up + fall) / g;
            plan.duration = plan.bounceTime + second;
            float horizontalTime = plan.bounceTime + .84f * second;
            plan.velocity = new Vector3((contact.x - from.x) / horizontalTime, up, (contact.z - from.z) / horizontalTime);
            plan.bouncePoint = plan.origin + plan.velocity * plan.bounceTime + Vector3.down * (.5f * g * plan.bounceTime * plan.bounceTime);
            plan.bouncePoint.y = Court.BallRadius;
            plan.rebound = new Vector3(plan.velocity.x * .84f, rise, plan.velocity.z * .84f);
            return plan;
        }
    }
}
