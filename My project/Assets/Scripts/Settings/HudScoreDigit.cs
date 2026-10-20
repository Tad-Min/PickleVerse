using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    // An event-driven, short roll. There is no Update or string formatting at rest.
    public sealed class HudScoreDigit : MonoBehaviour
    {
        public TMP_Text current,previous;
        public CanvasGroup currentOpacity,previousOpacity;
        public Image flash;
        public float baseFontSize=30;
        public int Value { get; private set; } = -1;
        public int TransitionCount { get; private set; }
        public bool IsAnimating => rolling;
        float elapsed,direction;
        bool rolling;
        // Six UI units stay inside the number cell at every canvas scale.
        // This avoids nested TMP clipping and its material rebuild on resize.
        const float Duration=.26f,Travel=6;
        public void SetValue(int value)
        {
            value=Mathf.Max(0,value);
            if(Value==value)return;
            int old=Value;Value=value;current.fontSize=baseFontSize*(value>=100?.75f:1);current.SetText("{0:0}",value);
            if(old<0||!gameObject.activeInHierarchy){Snap();return;}
            previous.fontSize=baseFontSize*(old>=100?.75f:1);previous.SetText("{0:0}",old);direction=value>=old?1:-1;
            elapsed=0;rolling=true;TransitionCount++;enabled=true;Advance(0);
        }
        void Update(){Advance(Time.unscaledDeltaTime);}
        public void Advance(float seconds)
        {
            if(!rolling)return;
            elapsed+=Mathf.Max(0,seconds);float t=Mathf.Clamp01(elapsed/Duration),ease=1-Mathf.Pow(1-t,3);
            current.rectTransform.anchoredPosition=new Vector2(0,-Travel*direction*(1-ease));
            previous.rectTransform.anchoredPosition=new Vector2(0,Travel*direction*ease);
            currentOpacity.alpha=ease;previousOpacity.alpha=1-ease;
            if(flash){var c=flash.color;c.a=.22f*(1-t);flash.color=c;}
            if(t>=1)Snap();
        }
        void Snap()
        {
            rolling=false;current.rectTransform.anchoredPosition=Vector2.zero;previous.rectTransform.anchoredPosition=Vector2.zero;
            currentOpacity.alpha=1;previousOpacity.alpha=0;if(flash){var c=flash.color;c.a=0;flash.color=c;}enabled=false;
        }
        void OnDisable(){if(rolling)Snap();}
    }
}
