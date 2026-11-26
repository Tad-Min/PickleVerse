namespace PickleVerse.VFX
{
    /// <summary>
    /// The blue phoenix of the Blue Phoenix paddle: it bursts out of the paddle with the shot, wings spread, and flies after
    /// the ball on a gentle swoop of the same sine flight as <see cref="SpineBallMotion"/>. PhoenixSpirit.shader keeps the
    /// body and wings on one rigid frame (they flap and unfold there) and bends the long tail along the ball's path; the
    /// blue fire ribbon and the black smoke ribbon (DragonRibbon.shader) follow the same wave.
    /// </summary>
    public sealed class PhoenixBallMotion : SpineBallMotion
    {
        /// <summary>The spread wings reach this far from the path, more than a dragon's body.</summary>
        protected override float Margin => 1.0f;
    }
}
