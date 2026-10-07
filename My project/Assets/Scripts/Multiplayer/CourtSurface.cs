using UnityEngine;
namespace PickleVerse.Multiplayer
{
    public sealed class CourtSurface : MonoBehaviour
    {
        public enum Kind { Ground, Net }
        public Kind kind;
    }
}
