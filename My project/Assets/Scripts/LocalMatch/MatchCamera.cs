using UnityEngine;
namespace PickleVerse.LocalMatch
{
    public sealed class MatchCamera : MonoBehaviour
    {
        public MatchActor player;
        public float height = 5.3f, followDistance = 4.21f, pitch = 31f, fieldOfView = 60f;
        public Vector3 DesiredPosition => new Vector3(player.transform.position.x + .05f, height, player.transform.position.z + player.end * followDistance);
        public Quaternion DesiredRotation => Quaternion.Euler(pitch, player.end < 0 ? 0 : 180, 0);
        void OnEnable()
        {
            if (player)
                SnapToPlayer();
        }
        public void SnapToPlayer()
        {
            if (!player)
                return;
            transform.SetPositionAndRotation(DesiredPosition, DesiredRotation);
            GetComponent<Camera>().fieldOfView = fieldOfView;
        }
        void LateUpdate()
        {
            if (!player)
                return;
            Vector3 position = DesiredPosition;
            transform.position = Vector3.Lerp(transform.position, position, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
            transform.rotation = DesiredRotation;
            GetComponent<Camera>().fieldOfView = fieldOfView;
        }
    }
}
