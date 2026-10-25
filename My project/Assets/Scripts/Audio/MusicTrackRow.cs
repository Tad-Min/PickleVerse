using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Audio
{
    public sealed class MusicTrackRow : MonoBehaviour
    {
        public Image disc, background, line;
        public TMP_Text title, artist, state, number, duration;
        public Button button;
        Color accent;
        public int Index { get; private set; }
        public void Bind(MusicTrack track, int index, Action select)
        {
            Index = index;
            disc.sprite = track.disc;
            title.text = track.title;
            artist.text = track.artist;
            accent = track.accent;
            button.onClick.AddListener(() => select());
            if (number)
                number.text = (index + 1).ToString("00");
            if (duration && track.clip)
            {
                int seconds = (int)track.clip.length;
                duration.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            }
        }
        public void Select(bool selected, bool playing)
        {
            background.color = selected ? Color.white : new Color(.63f, .72f, .86f);
            line.color = accent;
            line.enabled = selected;
            state.text = selected ? (playing ? "ĐANG PHÁT" : "ĐÃ CHỌN") : "CHẠM ĐỂ PHÁT";
            state.color = selected ? accent : new Color(.55f, .64f, .76f);
        }
    }
}
