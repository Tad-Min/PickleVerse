using UnityEngine;
namespace PickleVerse.Settings
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiSafeArea : MonoBehaviour
    {
        Rect last;Vector2 size;RectTransform target;
        void OnEnable(){target=(RectTransform)transform;Apply();}
        void Update(){Apply();}
        void Apply(){var area=Screen.safeArea;var current=new Vector2(Screen.width,Screen.height);if(current.x<1||current.y<1||area==last&&current==size)return;last=area;size=current;target.anchorMin=new Vector2(area.xMin/current.x,area.yMin/current.y);target.anchorMax=new Vector2(area.xMax/current.x,area.yMax/current.y);target.offsetMin=target.offsetMax=Vector2.zero;}
    }
}
