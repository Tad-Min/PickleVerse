using UnityEngine;

namespace PickleVerse.LocalMatch
{
    public sealed class TutorialBillboard : MonoBehaviour
    {
        public Camera view;
        void LateUpdate()
        {
            if (view)
                transform.rotation = Quaternion.LookRotation(transform.position - view.transform.position);
        }
    }
}
