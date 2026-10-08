using System.Linq;
using PickleVerse.Multiplayer.Services;

namespace PickleVerse.Multiplayer.UI
{
    /// <summary>Pure UI policy shared by the view and tests.</summary>
    public static class RoomViewModel
    {
        public static bool CanStartCustom(IMultiplayerState state)
        {
            var members = state.Members();
            return state.Custom && state.IsCaptain && !state.Busy && state.State == FlowState.WaitingRoom &&
                members.All(m => m.team >= 0 && m.team <= 1) &&
                members.Count(m => m.team == 0) >= 1 && members.Count(m => m.team == 1) >= 1 &&
                members.Count(m => m.team == 0) <= (int)state.Mode && members.Count(m => m.team == 1) <= (int)state.Mode;
        }
        public static string PlayLabel(IMultiplayerState state)
        {
            if (state.Searching) return "Hủy";
            if (state.Busy || state.State == FlowState.Menu) return "Đang kết nối…";
            if (!state.Custom) return "Tìm trận";
            return state.IsCaptain ? "Bắt đầu" : "Chờ chủ phòng";
        }
    }
}
