namespace PickleVerse.VFX
{
    /// <summary>
    /// The red dragon of the Dragon Ruby paddle: it leaves the paddle with the shot and chases the ball along the ball's own
    /// path, weaving on a sine wave (see <see cref="SpineBallMotion"/>). DragonSpirit.shader bends the straight dragon mesh
    /// along the path; DragonRibbon.shader lays its burning ribbons on the same wave.
    /// </summary>
    public sealed class DragonBallMotion : SpineBallMotion
    {
    }
}
