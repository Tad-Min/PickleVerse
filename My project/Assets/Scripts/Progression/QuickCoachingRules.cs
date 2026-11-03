#nullable disable
using System.Collections.Generic;

namespace PickleVerse.Progression
{
    // Brief feedback uses observable events only; assisted aim/timing never implies human error.
    public static class QuickCoachingRules
    {
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
