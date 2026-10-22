using UnityEngine;

namespace PickleVerse.LocalMatch
{
    public interface IMatchTouchShotReceiver : IMatchTouchInput
    {
        bool TouchShotsEnabled { get; }
        bool TouchServing { get; }
        void CommitTouchShot(int mask);
    }

    public enum TouchShotDirection { Center, Up, Left, Right, Cancel }

    // A touch is a transaction. Preview changes never enter the keyboard release parser.
    public sealed class TouchShotGesture
    {
        public const string HelpText="<size=28><color=#83E6CD>ĐIỀU KHIỂN CẢM ỨNG</color></size>\n\nKéo cần bên trái để đi bộ; giữ nút CHẠY bên phải để chạy nhanh.\nGiữ rồi thả DRIVE / DINK / VOLLEY để đánh.\nKéo lên: DRIVE → SMASH • DINK → DROP • VOLLEY → RESET\nKéo trái: CHÉO • Kéo phải: LỰC • Kéo xuống: HỦY\nTrở về giữa để đánh thường. Có thể vừa di chuyển vừa đánh.\n\nGiao bóng bằng DRIVE; kéo trái/phải để giao chéo/mạnh.\nHai cú đầu phải nảy. Volley và đà volley vào Kitchen là lỗi.\nChỉnh kích thước, độ mờ, tay trái và bố trí trong Cài đặt.";
        public const int NoPointer = int.MinValue;
        public int Pointer { get; private set; } = NoPointer;
        public int BaseMask { get; private set; }
        public TouchShotDirection Direction { get; private set; }
        public float Progress { get; private set; }
        public bool Active => Pointer != NoPointer;
        public int Mask => Direction == TouchShotDirection.Cancel ? 0 : Direction == TouchShotDirection.Up ?
            (BaseMask == 1 ? 5 : BaseMask == 2 ? 3 : 6) : BaseMask |
            (Direction == TouchShotDirection.Left ? 16 : Direction == TouchShotDirection.Right ? 8 : 0);

        public bool Begin(int pointer, int baseMask)
        {
            if (Active || (baseMask != 1 && baseMask != 2 && baseMask != 4)) return false;
            Pointer = pointer; BaseMask = baseMask; Direction = TouchShotDirection.Center; Progress = 0;
            return true;
        }
        public void Move(int pointer, Vector2 delta, bool serving = false)
        {
            if (pointer != Pointer || !Active) return;
            float distance = delta.magnitude;
            Progress = Mathf.Clamp01(distance / 72);
            if (distance < (Direction == TouchShotDirection.Center ? 30 : 22))
            { Direction = TouchShotDirection.Center; return; }
            var next = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ?
                (delta.x < 0 ? TouchShotDirection.Left : TouchShotDirection.Right) :
                (delta.y < 0 ? TouchShotDirection.Cancel : TouchShotDirection.Up);
            if (next == TouchShotDirection.Cancel && distance < 40) return;
            // During a serve an upward flick remains a serve, never an illegal smash.
            Direction = serving && next == TouchShotDirection.Up ? TouchShotDirection.Center : next;
        }
        public bool Release(int pointer, out int mask)
        {
            mask = 0;
            if (!Active || pointer != Pointer) return false;
            mask = Mask; Cancel(); return mask != 0;
        }
        public void Cancel() { Pointer = NoPointer; BaseMask = 0; Direction = TouchShotDirection.Center; Progress = 0; }
        public static string Hint(int mask)
        {
            switch(mask)
            {
                case 2:return "DINK";case 4:return "VOLLEY";
                case 3:return "DINK ↑ DROP";case 5:return "DRIVE ↑ SMASH";case 6:return "VOLLEY ↑ RESET";
                case 9:return "DRIVE → LỰC";case 10:return "DINK → LỰC";case 12:return "VOLLEY → LỰC";
                case 17:return "DRIVE ← CHÉO";case 18:return "DINK ← CHÉO";case 20:return "VOLLEY ← CHÉO";
                default:return "DRIVE";
            }
        }
    }
}
