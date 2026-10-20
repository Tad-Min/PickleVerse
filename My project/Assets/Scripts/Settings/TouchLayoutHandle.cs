using UnityEngine;
using UnityEngine.EventSystems;

namespace PickleVerse.Settings
{
    public sealed class TouchLayoutHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public SettingsScreen screen;
        public int index;
        public void Refresh()
        {
            if(screen.Draft==null)return;
            if(index>=4){gameObject.SetActive(false);return;}
            var rect=(RectTransform)transform;var p=screen.Draft.touchPositions[index];if(screen.Draft.leftHanded)p.x=1-p.x;
            rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.Scale(p,screen.touchBoard.rect.size);
            var size=screen.touchBoard.rect.size;
            rect.sizeDelta=Vector2.one*Mathf.Max(index==0?70:44,(index==0?146:90)*Mathf.Min(size.x/700,size.y/390)*screen.Draft.touchScale);
            var group=GetComponent<CanvasGroup>();if(group)group.alpha=screen.Draft.touchOpacity;
        }
        public void OnBeginDrag(PointerEventData e){OnDrag(e);}
        public void OnDrag(PointerEventData e)
        {
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(screen.touchBoard,e.position,e.pressEventCamera,out var p))
                screen.MoveTouch(index,new Vector2((p.x-screen.touchBoard.rect.xMin)/screen.touchBoard.rect.width,(p.y-screen.touchBoard.rect.yMin)/screen.touchBoard.rect.height));
        }
    }
}
