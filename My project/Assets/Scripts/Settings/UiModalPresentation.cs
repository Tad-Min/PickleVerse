using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    /// <summary>Finite reveal on an isolated canvas. Opaque panels suspend the hidden scene camera.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiModalPresentation : MonoBehaviour
    {
        public bool opaque;
        CanvasGroup group;Canvas surface;GraphicRaycaster raycaster;Camera suspended;bool cameraWasEnabled,visible;float elapsed;
        public bool Animating=>visible&&elapsed<.20f;
        void Bind(){if(!group)group=GetComponent<CanvasGroup>();if(!surface)surface=GetComponent<Canvas>();if(!raycaster)raycaster=GetComponent<GraphicRaycaster>();}
        public void Show()
        {
            if(visible)return;
            gameObject.SetActive(true);Bind();visible=true;surface.enabled=true;raycaster.enabled=true;
            group.interactable=group.blocksRaycasts=true;elapsed=0;group.alpha=0;
            if(opaque){suspended=Camera.main;if(suspended){cameraWasEnabled=suspended.enabled;suspended.enabled=false;}}
        }
        public void Hide()
        {
            Bind();visible=false;if(surface)surface.enabled=false;if(raycaster)raycaster.enabled=false;
            if(group){group.interactable=group.blocksRaycasts=false;group.alpha=0;}
            if(suspended)suspended.enabled=cameraWasEnabled;suspended=null;
        }
        void Update(){if(!visible||elapsed>=.20f)return;elapsed+=Time.unscaledDeltaTime;group.alpha=1-Mathf.Pow(1-Mathf.Clamp01(elapsed/.20f),3);}
        void OnDisable(){Hide();}
        public static UiModalPresentation Attach(RectTransform root,bool fullScreen)
        {
            // Add the component while inactive so it never acquires the scene camera during authoring.
            var canvas=root.GetComponent<Canvas>();if(!canvas)canvas=root.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=fullScreen?620:600;
            if(!root.GetComponent<GraphicRaycaster>())root.gameObject.AddComponent<GraphicRaycaster>();
            var fx=root.GetComponent<UiModalPresentation>();if(!fx)fx=root.gameObject.AddComponent<UiModalPresentation>();fx.opaque=fullScreen;return fx;
        }
    }
}
