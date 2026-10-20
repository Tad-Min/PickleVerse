using UnityEngine;
namespace PickleVerse.Multiplayer
{
    public sealed class CourtCamera : MonoBehaviour
    {
        public Transform target;
        int previousEnd;
        public void SnapToPlayer()
        {
            if (!target || !MultiplayerApp.Instance) return;
            var settings=MultiplayerApp.Instance.Settings;
            int end=target.GetComponent<NetworkPlayer>()?.CourtEnd ?? (target.forward.z>0?-1:1);
            transform.SetPositionAndRotation(target.position+new Vector3(.05f,settings.cameraHeight-target.position.y,end*settings.cameraDistance),Quaternion.Euler(settings.cameraPitch,end<0?0:180,0));
            GetComponent<Camera>().fieldOfView=settings.cameraFieldOfView;
            previousEnd=end;
        }
        void LateUpdate()
        {
            if(target==null || !MultiplayerApp.Instance) return;
            var settings=MultiplayerApp.Instance.Settings;
            int end=target.GetComponent<NetworkPlayer>()?.CourtEnd ?? (target.forward.z>0?-1:1);
            if(previousEnd!=end)SnapToPlayer();
            var desired=target.position+new Vector3(.05f,0,end*settings.cameraDistance); desired.y=settings.cameraHeight;
            transform.position=Vector3.Lerp(transform.position,desired,1-Mathf.Exp(-5*Time.unscaledDeltaTime));
            transform.rotation=Quaternion.Euler(settings.cameraPitch,end<0?0:180,0);
            GetComponent<Camera>().fieldOfView=settings.cameraFieldOfView;
        }
    }
}
