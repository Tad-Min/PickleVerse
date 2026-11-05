#nullable disable
using System.Collections.Generic;

namespace PickleVerse.Progression
{
    // Brief feedback uses observable events only; assisted aim/timing never implies human error.
    public static class QuickCoachingRules
    {
        public static string RallyTip(bool playerFault, string fault)
        {
            if (!playerFault) return "Bạn thắng lượt bóng. Hãy giữ vị trí sẵn sàng cho lượt tiếp theo.";
            switch (fault)
            {
                case "KitchenVolley": return "Khi vô lê, hãy giữ cả hai chân ngoài vùng cấm vô lê.";
                case "KitchenMomentum": return "Sau cú vô lê, giữ thăng bằng để không bước vào vùng cấm.";
                case "NetPlane": return "Bóng chưa qua lưới. Hãy tạo thêm độ cao an toàn khi qua lưới.";
                case "Out": return "Bóng vừa ra ngoài. Hãy ưu tiên khoảng an toàn bên trong đường biên.";
                case "ServeTimeout": return "Bạn đã hết 10 giây giao bóng. Hãy chuẩn bị và giao bóng trước khi hết thời gian.";
                case "ServeMiss": return "Cú giao bóng vừa đánh hụt. Hãy chuẩn bị vợt trong tầm bóng trước khi đánh.";
                case "ServeFoot": return "Khi giao bóng, giữ chân sau đường biên cuối sân và trong ô giao.";
                case "ServeShort": return "Giao bóng vượt vùng cấm vô lê và vào đúng ô chéo sân.";
                case "ServeWrongCourt": return "Hướng cú giao bóng vào ô giao chéo sân.";
                case "DoubleBounce": return "Di chuyển đón bóng sớm để trả bóng trước lần nảy thứ hai.";
                case "TwoBounce": return "Sau giao bóng và trả giao, hãy để bóng nảy bên mình trước khi đánh.";
                case "NetTouch": return "Giữ khoảng cách với lưới khi di chuyển và vung vợt.";
                case "BodyHit": return "Di chuyển vào tầm vợt để tránh bóng chạm người.";
                case "OwnCourt": return "Hướng bóng qua lưới sang phần sân đối phương.";
                case "DoubleHit": return "Chỉ tiếp xúc bóng một lần trong mỗi lượt đánh.";
                default: return "Bạn vừa mất lượt bóng. Hãy chuẩn bị vị trí đón bóng cho lượt tiếp theo.";
            }
        }
        public static string Tip(IReadOnlyList<ShotTelemetry> shots, int start = 0)
        {
            if (shots == null || start < 0 || start >= shots.Count) return "";
            for (int i = shots.Count - 1; i >= start; i--)
            {
                var shot = shots[i];
                if (shot == null) continue;
                if (shot.kitchen == true && shot.volley == true || shot.outcome == "Kitchen")
                    return "Khi vô lê, hãy giữ cả hai chân ngoài vùng cấm vô lê.";
                if (shot.momentum == true || shot.outcome == "Momentum")
                    return "Sau cú vô lê, giữ thăng bằng để không bước vào vùng cấm.";
                if (shot.outcome == "Net") return "Bóng vừa chạm lưới. Hãy tạo thêm độ cao an toàn khi qua lưới.";
                if (shot.outcome == "Out") return "Bóng vừa ra ngoài. Hãy ưu tiên khoảng an toàn bên trong đường biên.";
                if (!shot.assisted && shot.timingError.HasValue && shot.outcome == "LateServe")
                    return "Cú giao bóng vừa bị trễ. Hãy chuẩn bị sớm hơn trước khi tiếp xúc.";
                if (shot.recovered == false) return "Sau cú đánh, hãy trở về vị trí sẵn sàng cho bóng tiếp theo.";
            }
            return "";
        }
    }
}
