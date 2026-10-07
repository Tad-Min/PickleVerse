using UnityEngine;
namespace PickleVerse.Multiplayer
{
    public sealed class MultiplayerBallContact : MonoBehaviour
    {
        public NetworkMatch match;
        void OnCollisionEnter(Collision collision) { if(match!=null) match.OnBallCollision(collision); }
    }
}
