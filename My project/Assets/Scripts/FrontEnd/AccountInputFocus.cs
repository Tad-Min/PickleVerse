using UnityEngine;
using UnityEngine.EventSystems;

namespace PickleVerse.FrontEnd
{
    public sealed class AccountInputFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public GameObject indicator;
        public void OnSelect(BaseEventData data) { if (indicator) indicator.SetActive(true); }
        public void OnDeselect(BaseEventData data) { if (indicator) indicator.SetActive(false); }
        void OnDisable() { if (indicator) indicator.SetActive(false); }
    }
}
