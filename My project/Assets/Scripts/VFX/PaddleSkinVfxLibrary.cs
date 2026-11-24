using System;
using UnityEngine;

namespace PickleVerse.VFX
{
    /// <summary>All paddle-skin effects, loaded from Resources/VFX/PaddleSkinVfxLibrary.</summary>
    [CreateAssetMenu(menuName = "PickleVerse/VFX/Paddle skin VFX library")]
    public sealed class PaddleSkinVfxLibrary : ScriptableObject
    {
        public const string ResourcePath = "VFX/PaddleSkinVfxLibrary";
        public PaddleSkinVfxProfile[] profiles = Array.Empty<PaddleSkinVfxProfile>();

        public PaddleSkinVfxProfile Find(string skinId)
        {
            if (string.IsNullOrEmpty(skinId))
                return null;
            foreach (var p in profiles)
                if (p && p.skinId == skinId)
                    return p;
            return null;
        }
    }
}
