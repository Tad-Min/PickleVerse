using UnityEngine;
namespace PickleVerse.Multiplayer
{
    public sealed class CourtCamera : MonoBehaviour
    {
        public Transform target;
        void LateUpdate()
        {
            if(target==null) return;
            var settings=MultiplayerApp.Instance.Settings;
            var desired=target.position-target.forward*settings.cameraDistance; desired.y=settings.cameraHeight;
            transform.position=Vector3.Lerp(transform.position,desired,1-Mathf.Exp(-5*Time.unscaledDeltaTime));
            transform.rotation=Quaternion.Euler(settings.cameraPitch,target.eulerAngles.y,0);
            GetComponent<Camera>().fieldOfView=settings.cameraFieldOfView;
        }
    }
}
