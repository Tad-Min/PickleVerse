using System;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    /// <summary>
    /// Keeps a bottom HUD panel (the shot cue) off the local player's body. While the player's on-screen silhouette
    /// overlaps the panel, the panel slides sideways to whichever side has room, and slides back afterwards.
    /// The panel's home position stays owned by the HUD layout.
    /// </summary>
    public sealed class HudActorClearance : MonoBehaviour
    {
        const float Margin = 14, ShoulderHalfWidth = .45f, BodyHeight = 1.85f, SlideTime = .12f;
        Func<Transform> actor;
        RectTransform rect;
        Canvas canvas;
        Vector2 home, applied;
        float offset, velocity;
        bool started;
        readonly Vector3[] corners = new Vector3[4];

        public static HudActorClearance Attach(GameObject panel, Func<Transform> actor)
        {
            if (!panel)
                return null;
            // Unity's fake-null objects defeat ??, so test the component explicitly.
            var clearance = panel.GetComponent<HudActorClearance>();
            if (!clearance)
                clearance = panel.AddComponent<HudActorClearance>();
            clearance.actor = actor;
            return clearance;
        }

        void OnEnable()
        {
            rect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            started = false;
        }

        void OnDisable()
        {
            // Hide at home so the next cue starts from the layout position.
            if (rect && started)
                rect.anchoredPosition = home;
            offset = velocity = 0;
            started = false;
        }

        void LateUpdate()
        {
            // Anything else that moved the panel (the HUD layout) defines a new home position.
            if (!started || rect.anchoredPosition != applied)
                home = rect.anchoredPosition;
            started = true;
            float target = Target();
            offset = Mathf.SmoothDamp(offset, target, ref velocity, SlideTime, Mathf.Infinity, Time.unscaledDeltaTime);
            applied = home + new Vector2(offset, 0);
            rect.anchoredPosition = applied;
        }

        float Target()
        {
            var body = actor?.Invoke();
            var view = Camera.main;
            if (!body || !view || !canvas)
                return 0;
            if (!Silhouette(view, body.position, out var person))
                return 0;
            var root = canvas.rootCanvas;
            var uiCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            float scale = Mathf.Max(.01f, root.scaleFactor);
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]), max = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
            float shift = offset * scale;
            var panel = Rect.MinMaxRect(min.x - shift, min.y, max.x - shift, max.y);
            if (!panel.Overlaps(person))
                return 0;
            float left = person.xMin - Margin - panel.xMax, right = person.xMax + Margin - panel.xMin;
            var area = Screen.safeArea;
            bool leftFits = panel.xMin + left >= area.xMin + Margin, rightFits = panel.xMax + right <= area.xMax - Margin;
            float chosen = leftFits && rightFits ? (-left < right ? left : right) : leftFits ? left : rightFits ? right : 0;
            return chosen / scale;
        }

        static bool Silhouette(Camera view, Vector3 feet, out Rect area)
        {
            area = default;
            var side = view.transform.right * ShoulderHalfWidth;
            var points = new[] { feet, feet + Vector3.up * BodyHeight, feet + Vector3.up * (BodyHeight * .55f) + side, feet + Vector3.up * (BodyHeight * .55f) - side };
            bool any = false;
            foreach (var point in points)
            {
                var screen = view.WorldToScreenPoint(point);
                if (screen.z <= 0)
                    continue;
                area = any ? Rect.MinMaxRect(Mathf.Min(area.xMin, screen.x), Mathf.Min(area.yMin, screen.y), Mathf.Max(area.xMax, screen.x), Mathf.Max(area.yMax, screen.y)) : new Rect(screen.x, screen.y, 0, 0);
                any = true;
            }
            return any;
        }
    }
}
