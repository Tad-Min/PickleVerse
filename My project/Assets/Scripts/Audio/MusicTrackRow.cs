using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Audio
{
    public sealed class MusicTrackRow : MonoBehaviour
    {
        public Image disc,background,line;
        public TMP_Text title,artist,state;
        public Button button;
        Color accent;
        public int Index {get;private set;}
        public void Bind(MusicTrack track,int index,Action select)
        {Index=index;disc.sprite=track.disc;title.text=track.title;artist.text=track.artist;accent=track.accent;button.onClick.AddListener(()=>select());}
        public void Select(bool selected,bool playing)
        {background.color=selected?new Color(.085f,.20f,.23f):new Color(.03f,.065f,.085f);line.color=accent;line.enabled=selected;state.text=selected?(playing?"ĐANG PHÁT":"ĐÃ CHỌN"):"PHÁT  →";state.color=selected?accent:new Color(.49f,.66f,.69f);}
    }
}
