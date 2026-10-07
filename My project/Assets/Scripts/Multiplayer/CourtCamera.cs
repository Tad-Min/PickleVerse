using UnityEngine;
namespace PickleVerse.Multiplayer
{
    public sealed class CourtCamera : MonoBehaviour
    {
        public Transform target;
        void LateUpdate()
        {
            if(target==null) return;
            var desired=target.position-target.forward*6.5f+Vector3.up*5.5f;
            transform.position=Vector3.Lerp(transform.position,desired,1-Mathf.Exp(-8*Time.deltaTime));
            transform.LookAt(target.position+target.forward*3+Vector3.up*.6f);
        }
    }
}
