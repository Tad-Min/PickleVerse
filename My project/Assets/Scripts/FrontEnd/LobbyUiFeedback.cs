using UnityEngine;
using UnityEngine.EventSystems;
namespace PickleVerse.FrontEnd
{
    /// <summary>Only decorative children move. The button and its touch target stay still.</summary>
    public sealed class LobbyUiFeedback : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler
    {
        public RectTransform symbol,sweep;
        public UnityEngine.UI.Image accent,sweepImage;
        public bool keepFocus;
        UnityEngine.UI.Selectable button;
        bool hover,selected,pressed;
        float amount,pulse=1,symbolScale=1;
        void Awake(){button=GetComponent<UnityEngine.UI.Selectable>();}
        public void OnPointerEnter(PointerEventData e){hover=true;pulse=0;}
        public void OnPointerExit(PointerEventData e){hover=pressed=false;}
        public void OnPointerDown(PointerEventData e){pressed=true;pulse=0;}
        public void OnPointerUp(PointerEventData e){pressed=false;}
        public void OnSelect(BaseEventData e){selected=true;pulse=0;}
        public void OnDeselect(BaseEventData e){selected=pressed=false;}
        void Update()
        {
            bool active=button&&button.IsInteractable()&&(hover||selected||keepFocus);
            float next=Mathf.MoveTowards(amount,active?1:0,Time.unscaledDeltaTime*9);
            if(!Mathf.Approximately(next,amount))
            {
                amount=next;
                if(accent){var c=accent.color;c.a=.85f*amount;accent.color=c;}
            }
            float scale=pressed?.93f:1+.045f*amount;
            if(symbol&&!Mathf.Approximately(symbolScale,scale)){symbolScale=scale;symbol.localScale=Vector3.one*scale;}
            if(pulse<1)
            {
                pulse=Mathf.Min(1,pulse+Time.unscaledDeltaTime/.32f);
                if(sweep){float w=((RectTransform)transform).rect.width;sweep.anchoredPosition=new Vector2(Mathf.Lerp(-w,w,pulse),0);}
                if(sweepImage){var c=sweepImage.color;c.a=Mathf.Sin(pulse*Mathf.PI)*.22f;sweepImage.color=c;}
            }
        }
        void OnDisable()
        {
            hover=selected=pressed=false;amount=0;pulse=1;symbolScale=1;
            if(symbol)symbol.localScale=Vector3.one;
            if(accent){var c=accent.color;c.a=0;accent.color=c;}
            if(sweepImage){var c=sweepImage.color;c.a=0;sweepImage.color=c;}
        }
    }
}

