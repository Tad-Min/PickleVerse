using UnityEngine;

namespace PickleVerse.Shop
{
    /// <summary>Use the main camera for a full-viewport studio while keeping the HUD on that camera.</summary>
    public sealed class ShopDetailPresentation : MonoBehaviour
    {
        public ShopScreen screen;
        public Camera uiCamera, studioCamera;
        public GameObject fullScreenInput;
        public float detailFov = 34;
        Transform originalParent;
        Vector3 originalPosition;
        Quaternion originalRotation;
        float originalFov;
        int originalMask;
        Color originalBackground;
        bool initialized, showing;
        public bool Showing => showing;
        void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            originalParent = uiCamera.transform.parent;
            originalPosition = uiCamera.transform.localPosition;
            originalRotation = uiCamera.transform.localRotation;
            originalFov = uiCamera.fieldOfView;
            originalMask = uiCamera.cullingMask;
            originalBackground = uiCamera.backgroundColor;
        }
        public void Show()
        {
            Initialize();
            fullScreenInput.SetActive(true);
            studioCamera.enabled = false;
            uiCamera.transform.SetParent(studioCamera.transform.parent, false);
            uiCamera.transform.localPosition = studioCamera.transform.localPosition;
            uiCamera.transform.localRotation = studioCamera.transform.localRotation;
            uiCamera.fieldOfView = detailFov;
            uiCamera.cullingMask = (1 << 30) | (1 << 5);
            uiCamera.backgroundColor = new Color(.018f, .032f, .055f);
            uiCamera.enabled = true;
            screen.preview.UseCamera(uiCamera);
            screen.preview.ResetView();
            showing = true;
        }
        public void Hide()
        {
            Initialize();
            fullScreenInput.SetActive(false);
            screen.preview.ResetView();
            screen.preview.UseCamera(studioCamera);
            studioCamera.enabled = false;
            uiCamera.transform.SetParent(originalParent, false);
            uiCamera.transform.localPosition = originalPosition;
            uiCamera.transform.localRotation = originalRotation;
            uiCamera.fieldOfView = originalFov;
            uiCamera.cullingMask = originalMask;
            uiCamera.backgroundColor = originalBackground;
            uiCamera.enabled = true;
            showing = false;
        }
    }
}
