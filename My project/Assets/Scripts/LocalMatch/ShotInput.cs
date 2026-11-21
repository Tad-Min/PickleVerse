using PickleVerse.Rules;

namespace PickleVerse.LocalMatch
{
    public enum ShotKind { Drive, Dink, Volley, Drop, Smash, Reset, Serve }
    public struct ShotIntent
    {
        public ShotKind kind;
        public bool power, cross;
        public ShotIntent(ShotKind kind, bool power = false, bool cross = false)
        {
            this.kind = kind;
            this.power = power;
            this.cross = cross;
        }
        public bool AfterBounce => kind == ShotKind.Drive || kind == ShotKind.Dink || kind == ShotKind.Drop;
        public bool BeforeBounce => kind == ShotKind.Volley || kind == ShotKind.Reset;
        public float ContactDelay => kind == ShotKind.Serve ? .8f : kind == ShotKind.Dink ? .367f : kind == ShotKind.Drop ? .533f : kind == ShotKind.Smash ? .5f : BeforeBounce ? .233f : .433f;
        public float Duration => kind == ShotKind.Serve ? 1.5f : kind == ShotKind.Dink ? .8f : kind == ShotKind.Drop ? 1.2f : BeforeBounce ? .6f : 1f;
        static readonly string[] Names = { "Đánh bạt", "Bóng ngắn", "Vô lê", "Bỏ nhỏ", "Đập bóng", "Hãm bóng", "Giao bóng" };
        public static string Name(ShotKind kind) => Names[(int)kind];
        public override string ToString() => Name(kind) + (power ? " + lực" : "") + (cross ? " chéo" : "");
    }
    // Bits: J=1 K=2 L=4 U=8 I=16. Commit once, on first base-key release.
    public sealed class ShotInput
    {
        int previous, bases;
        bool locked, invalid;
        public string Feedback { get; private set; } = "";
        public string Selection { get; private set; } = "";
        public string SelectionKeys { get; private set; } = "";
        public string CommittedKeys { get; private set; } = "";
        public int SelectionMask { get; private set; }
        public int CommittedMask { get; private set; }
        public double? PressedAt { get; private set; }
        public double? ReleasedAt { get; private set; }
        public int ObservedMask { get; private set; }
        public void Cancel()
        {
            bases = 0;
            PressedAt = ReleasedAt = null;
            ObservedMask = 0;
            invalid = true;
            locked = true;
            Selection = SelectionKeys = "";
            SelectionMask = 0;
        }
        public bool Tick(int mask, bool cancel, bool serve, out ShotIntent intent, double? observedTime = null)
        {
            intent = default;
            if (cancel)
                Cancel();
            if (mask == 0 && (locked || (previous & 7) == 0))
            {
                previous = bases = 0;
                locked = invalid = false;
                Selection = SelectionKeys = "";
                SelectionMask = 0;
                return false;
            }
            if (locked)
            {
                previous = mask;
                return false;
            }
            int currentBases = mask & 7;
            int released = (previous & 7) & ~currentBases;
            if (bases == 0)
            {
                bases = currentBases;
                if (bases != 0) { PressedAt = observedTime; ReleasedAt = null; }
            }
            else bases |= currentBases;
            int modifiers = mask & 24;
            ObservedMask = bases | modifiers;
            int count = Count(bases) + Count(modifiers);
            if (count > 2)
            {
                invalid = true;
                locked = true;
                Feedback = "Chỉ ghép tối đa hai nút";
                Selection = SelectionKeys = "";
                previous = mask;
                return false;
            }
            bool valid = Resolve(bases, modifiers, serve, out intent);
            Selection = valid && bases != 0 ? intent.ToString() : "";
            SelectionKeys = valid && bases != 0 ? Keys(bases | modifiers) : "";
            SelectionMask = valid && bases != 0 ? bases | modifiers : 0;
            previous = mask;
            if (released == 0 || bases == 0 || invalid)
                return false;
            ReleasedAt = observedTime;
            CommittedKeys = SelectionKeys;
            CommittedMask = SelectionMask;
            locked = true;
            Selection = SelectionKeys = "";
            SelectionMask = 0;
            if (!valid)
            {
                Feedback = serve ? "Giao bóng bằng J, J+U hoặc J+I" : "Tổ hợp không hợp lệ";
                return false;
            }
            Feedback = "";
            return true;
        }
        static int Count(int v)
        {
            int n = 0;
            while (v != 0)
            {
                n += v & 1;
                v >>= 1;
            }
            return n;
        }
        public static string Keys(int mask)
        {
            string text = "";
            string[] names = { "J", "K", "L", "U", "I" };
            for (int i = 0; i < names.Length; i++)
                if ((mask & (1 << i)) != 0)
                    text += (text.Length == 0 ? "" : " + ") + names[i];
            return text;
        }
        static bool Resolve(int b, int m, bool serve, out ShotIntent intent)
        {
            intent = default;
            if (b == 0 || m == 24)
                return false;
            if (serve)
            {
                intent = new ShotIntent(ShotKind.Serve, m == 8, m == 16);
                return b == 1;
            }
            ShotKind kind;
            switch (b) { case 1: kind = ShotKind.Drive; break; case 2: kind = m == 8 ? ShotKind.Drive : ShotKind.Dink; break; case 4: kind = ShotKind.Volley; break; case 3: kind = ShotKind.Drop; break; case 5: kind = ShotKind.Smash; break; case 6: kind = ShotKind.Reset; break; default: return false; }
            intent = new ShotIntent(kind, m == 8, m == 16);
            return true;
        }
    }
}
