using UnityEngine;
namespace PickleVerse.FrontEnd
{
    /// <summary>Sprites baked in Blender; motion keys exported from the same source.</summary>
    public sealed class LobbyUiTheme : ScriptableObject
    {
        public Sprite panel,play,paddle,ball,impact,sweep,avatarRing,social,arrow;
        public Sprite[] navigation;
        public AnimationCurve paddleAngle,paddleX,ballX,ballY;
        public float strikeDuration=.4f,impactTime=.15f;
    }
}

