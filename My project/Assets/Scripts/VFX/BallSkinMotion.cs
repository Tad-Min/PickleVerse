using UnityEngine;

namespace PickleVerse.VFX
{
    /// <summary>
    /// A part of a paddle-skin ball effect that moves by itself (for example the dragon of the Dragon Ruby paddle).
    /// <see cref="BallSkinVfx"/> restarts it on every contact by that skin and tells it how strongly it is shown.
    /// </summary>
    public abstract class BallSkinMotion : MonoBehaviour
    {
        /// <summary>A new shot with this skin left the paddle at <paramref name="contact"/> (world space).</summary>
        public abstract void Restart(Vector3 contact);

        /// <summary>Called every frame: is this skin the one that owns the shot in flight?</summary>
        public abstract void SetShown(bool shown);
    }
}
