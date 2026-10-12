using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    public enum AnnouncementTone { Neutral, Fault, Success, ChangeEnds }

    public sealed class MatchAnnouncements : MonoBehaviour
    {
        public BotMatch match;
        public RectTransform panel;
        public TMP_Text headline, detail;
        public CanvasGroup opacity;
        public UnityEngine.UI.Image accent;
        public float enterSeconds = .18f, exitSeconds = .22f;
        // Training advances this alongside its lesson simulation, including pause and QA stepping.
        public bool externallyDriven;
        public bool Suspended { get; set; }
        sealed class Notice
        {
            public string title, body;
            public float duration;
            public AnnouncementTone tone;
            public Action started, finished;
        }
        readonly Queue<Notice> pending = new Queue<Notice>();
        Notice current;
        float elapsed;
        public bool Busy => current != null || pending.Count > 0;
        public string ActiveTitle => current?.title ?? "";
        public float Elapsed => elapsed;
        public int PendingCount => pending.Count;

        void Awake() { Clear(); }
        void Update()
        {
            if (externallyDriven) return;
            if (match && (match.Paused || match.Phase == MatchPhase.Cinematic))
            {
                if (panel) panel.gameObject.SetActive(false);
                return;
            }
            // Keep the text readable after a stalled frame or an Editor pause.
            Advance(Mathf.Min(Time.unscaledDeltaTime, .1f));
        }

        public void Enqueue(string title, string body, AnnouncementTone tone = AnnouncementTone.Neutral, float duration = 1.25f, Action started = null, Action finished = null)
        {
            pending.Enqueue(new Notice { title = title, body = body, tone = tone, duration = Mathf.Max(.6f, duration), started = started, finished = finished });
            if (current == null) StartNext();
        }

        void StartNext()
        {
            if (pending.Count == 0) return;
            current = pending.Dequeue(); elapsed = 0;
            headline.text = current.title; detail.text = current.body;
            Color color = current.tone == AnnouncementTone.Fault ? new Color(1,.55f,.40f) : current.tone == AnnouncementTone.Success ? new Color(.51f,.90f,.80f) : current.tone == AnnouncementTone.ChangeEnds ? new Color(.80f,.96f,.45f) : Color.white;
            headline.color = color; accent.color = color;
            panel.gameObject.SetActive(true); panel.SetAsLastSibling();
            current.started?.Invoke();
            Render();
        }

        public void Advance(float dt)
        {
            if (Suspended || match && match.Paused) return;
            dt = Mathf.Max(0, dt);
            if (current == null) StartNext();
            while (current != null)
            {
                float step = Mathf.Min(dt, current.duration - elapsed);
                elapsed += step; dt -= step; Render();
                if (elapsed < current.duration) break;
                var callback = current.finished;
                current = null;
                panel.gameObject.SetActive(false); opacity.alpha = 0;
                callback?.Invoke();
                StartNext();
                if (dt <= 0) break;
            }
        }

        void Render()
        {
            panel.gameObject.SetActive(true);
            var root = (RectTransform)panel.parent;
            panel.sizeDelta = new Vector2(Mathf.Min(900, Mathf.Max(240, root.rect.width - 64)), 120);
            float distance = root.rect.width * .5f + panel.rect.width * .5f + 40;
            float enter = Mathf.Min(enterSeconds, current.duration * .25f), exit = Mathf.Min(exitSeconds, current.duration * .25f);
            float x = elapsed < enter ? Mathf.Lerp(-distance, 0, Mathf.SmoothStep(0, 1, elapsed / enter)) : elapsed > current.duration - exit ? Mathf.Lerp(0, distance, Mathf.SmoothStep(0, 1, (elapsed - current.duration + exit) / exit)) : 0;
            panel.anchoredPosition = new Vector2(x, 0);
            opacity.alpha = elapsed < enter ? Mathf.Clamp01(elapsed / enter) : elapsed > current.duration - exit ? Mathf.Clamp01((current.duration - elapsed) / exit) : 1;
        }

        public void Clear()
        {
            pending.Clear(); current = null; elapsed = 0;
            if (panel) panel.gameObject.SetActive(false);
            if (opacity) opacity.alpha = 0;
        }
    }
}
