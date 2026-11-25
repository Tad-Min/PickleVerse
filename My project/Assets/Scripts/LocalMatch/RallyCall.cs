namespace PickleVerse.LocalMatch
{
    /// <summary>
    /// Wording of the referee call after a fault, from the point of view of the player who reads it:
    /// a red "lost the rally" call for the side that faulted, a green call for the side that benefits.
    /// Scoring is side-out: only the serving side scores; a fault by the server passes the serve.
    /// </summary>
    public static class RallyCall
    {
        // The box slides in (.18 s) and out (.22 s); this leaves the text standing still for about 1.5 s.
        public const float Seconds = 1.9f;

        public enum Serve { Point, SideOut, SecondServer }

        public static (string title, string detail, AnnouncementTone tone) For(bool ownFault, Serve serve, string reason, string rival, bool team = false)
        {
            string you = team ? "Đội bạn" : "Bạn", YOU = team ? "ĐỘI BẠN" : "BẠN";
            if (ownFault)
                return ("MẤT LƯỢT", "Lỗi: " + reason + "\n" + (serve == Serve.Point ? rival + " ghi điểm"
                    : serve == Serve.SideOut ? you + " mất quyền giao bóng" : "Chuyển sang người giao thứ hai của đội bạn"), AnnouncementTone.Fault);
            string why = rival + " phạm lỗi: " + reason;
            return serve == Serve.Point ? (YOU + " GHI ĐIỂM", why + "\n" + you + " nhận 1 điểm và tiếp tục giao bóng", AnnouncementTone.Success)
                : serve == Serve.SideOut ? ("ĐẾN LƯỢT " + YOU + " GIAO BÓNG", why + "\n" + you + " giành quyền giao bóng", AnnouncementTone.Success)
                : ("ĐỐI THỦ MẤT LƯỢT", why + "\nĐối thủ chuyển sang người giao thứ hai", AnnouncementTone.Success);
        }
    }
}
