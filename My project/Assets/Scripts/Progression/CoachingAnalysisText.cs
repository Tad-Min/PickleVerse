using System.Linq;

namespace PickleVerse.Progression
{
    public static class CoachingAnalysisText
    {
        public static string Describe(CoachingAnalysis analysis)
        {
            if (analysis == null) return "Phân tích đang được xử lý. Bạn có thể tiếp tục chơi và xem lại trong Tiến trình luyện tập.";
            if (!string.IsNullOrWhiteSpace(analysis.summary)) return analysis.summary;
            // Older analyses remain readable after an API/AI upgrade.
            var errors = analysis.metrics?.Where(m => m.opportunities > 0 && m.errors > 0)
                .OrderByDescending(m => m.errors).Take(2).Select(m => Name(m.code) + ": " + m.errors + "/" + m.opportunities + " lần quan sát");
            string metrics = errors == null ? "" : string.Join("; ", errors);
            string tips = analysis.findings == null ? "" : string.Join("\n", analysis.findings.Select(f => f.tip).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().Take(2));
            if (metrics.Length > 0 || tips.Length > 0) return string.Join("\n", new[] {metrics, tips}.Where(t => t.Length > 0));
            return analysis.metrics?.Any(m => m.opportunities > 0) == true
                ? "Chưa ghi nhận lỗi trong các tiêu chí đã đo ở phiên này. Hãy tiếp tục luyện giao, trả bóng và kiểm soát điểm rơi."
                : "Phiên này chưa ghi nhận được số liệu cú đánh. Hãy hoàn thành một lượt có giao và trả bóng để HLV thống kê.";
        }
        static string Name(string code) => code == "Net" ? "Bóng chạm lưới" : code == "Out" ? "Bóng ngoài sân" :
            code == "Miss" ? "Đánh hụt" : code == "DoubleBounce" ? "Bóng nảy hai lần" :
            code == "Kitchen" ? "Vô lê trong vùng cấm" : code == "Momentum" ? "Đà vô lê vào vùng cấm" :
            code == "LateServe" ? "Giao bóng trễ" : code == "ServeFeet" ? "Lỗi chân giao bóng" :
            code == "Early" ? "Đánh sớm" : code == "Late" ? "Đánh muộn" :
            code == "Movement" ? "Chưa hồi vị" : code == "Target" ? "Chưa trúng vùng đích" : "Tiêu chí kỹ thuật";
    }
}
