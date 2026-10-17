using UnityEngine;
namespace PickleVerse.Settings
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiPanelReveal : MonoBehaviour
    {
        CanvasGroup group;float elapsed;
        void OnEnable(){group=GetComponent<CanvasGroup>();elapsed=0;group.alpha=0;}
        void Update(){if(elapsed>=.24f)return;elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/.24f);group.alpha=1-Mathf.Pow(1-t,3);}
        void OnDisable(){if(group)group.alpha=1;}
    }
}
