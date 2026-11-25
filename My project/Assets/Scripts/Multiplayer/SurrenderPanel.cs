using System;
using PickleVerse.Audio;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PickleVerse.Multiplayer
{
    /// <summary>What the box needs to know about the running match.</summary>
    public interface ISurrenderCourt
    {
        bool CanSurrender { get; }
        string LocalPlayer { get; }
        MatchRoster Roster { get; }
        /// <summary>The human who has to agree as well, or null when the local player decides alone.</summary>
        RoomMember SurrenderPartner { get; }
        SurrenderRequest Surrender { get; }
        void AskToSurrender();
        void AgreeToSurrender();
        void RefuseToSurrender();
    }

    /// <summary>
    /// The confirmation box of "Đầu hàng". The player who pressed the button gets it in the middle of the court; the
    /// doubles partner gets it as a card on the right, because the rally goes on while they decide. Nothing here locks
    /// the controls or takes the keyboard focus, so a shot key can never answer the question.
    /// </summary>
    public sealed class SurrenderPanel : MonoBehaviour
    {
        public enum View { Hidden, Ask, Partner, Waiting, Notice, Leave }

        /// <summary>Seconds without an answer of the server before the box says so.</summary>
        public const float AnswerSeconds = 6, NoticeSeconds = 3.5f;
        static readonly Vector2 AskSize = new Vector2(500, 252), CardSize = new Vector2(392, 216), NoticeSize = new Vector2(392, 92);
        static readonly Color Danger = new Color32(255, 160, 148, 255);
        const float Bevel = 14;

        public View Current { get; private set; }
        /// <summary>Opened by this player and not answered yet: Esc closes it instead of opening the settings.</summary>
        public bool CanCancel => Current == View.Ask || Current == View.Leave;

        RectTransform card, anchor;
        TMP_Text title, body, countdown, acceptLabel, refuseLabel;
        Button accept, refuse;
        RectTransform timer;
        ISurrenderCourt match;
        Action leave;
        bool answered;
        int seen;
        double sentAt = -1, noticeUntil, dismissedUntil;
        int shownSecond = -1;
        /// <summary>The clock of <see cref="SurrenderRequest.ClosesAt"/>; tests replace it.</summary>
        public Func<double> Clock = () => Time.realtimeSinceStartupAsDouble;

        public Button AcceptButton => accept;
        public Button RefuseButton => refuse;
        public string TitleText => title.text;
        public string BodyText => body.text;
        public string CountdownText => countdown.text;
        public RectTransform Card => card;

        /// <param name="button">The HUD button the card hangs under; it already respects the safe area.</param>
        public static SurrenderPanel Create(Transform canvas, RectTransform button, TMP_FontAsset font)
        {
            var rect = MatchPresentationUi.Rect("Surrender", canvas, Vector2.zero, Vector2.one);
            var panel = rect.gameObject.AddComponent<SurrenderPanel>();
            panel.Build(button, font);
            return panel;
        }

        // ------------------------------------------------------------------------------------------ actions
        /// <summary>The player pressed "Đầu hàng".</summary>
        public void Ask(ISurrenderCourt current)
        {
            if (current == null || !current.CanSurrender || Current != View.Hidden && Current != View.Notice)
                return;
            match = current;
            answered = false;
            sentAt = -1;
            dismissedUntil = 0;
            seen = current.Surrender.Revision;
            Show(View.Ask);
            if (current.SurrenderPartner != null)
                current.AskToSurrender();
        }

        /// <summary>The match server cannot be asked (reconnecting): leaving still needs a confirmation.</summary>
        public void AskLeave(Action confirmed)
        {
            if (Current != View.Hidden && Current != View.Notice)
                return;
            leave = confirmed;
            Show(View.Leave);
        }

        public void Cancel()
        {
            if (Current == View.Leave)
                Hide();
            else if (Current != View.Hidden && Current != View.Notice)
                Refuse();
        }

        void Accept()
        {
            ReleaseFocus();
            if (Current == View.Leave)
            {
                var action = leave;
                Hide();
                action?.Invoke();
                return;
            }
            if (Current != View.Ask && Current != View.Partner || match == null || !match.CanSurrender)
                return;
            answered = true;
            sentAt = Clock();
            match.AgreeToSurrender();
            Show(View.Waiting);
        }

        void Refuse()
        {
            ReleaseFocus();
            bool tell = Current != View.Leave && match != null && match.CanSurrender && match.SurrenderPartner != null;
            Hide();
            if (!tell)
                return;
            // Messages already on the way must not bring the box back before the server confirms.
            dismissedUntil = Clock() + 2;
            match.RefuseToSurrender();
        }

        static void ReleaseFocus()
        {
            // A button that keeps the selection would be pressed again by Enter or Space during the rally.
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
        }

        // ------------------------------------------------------------------------------------------ state
        /// <summary>Called every frame by the court UI.</summary>
        public void Refresh(ISurrenderCourt current, bool hidden)
        {
            if (Current == View.Leave)
            {
                var app = MultiplayerApp.Instance;
                if (!app || app.State != FlowState.Reconnecting)
                    Hide();
            }
            else if (current == null || !current.CanSurrender)
            {
                if (Current != View.Hidden)
                    Hide();
                seen = 0;
                match = null;
            }
            else
            {
                if (match != current)
                {
                    match = current;
                    seen = 0;
                    answered = false;
                    dismissedUntil = 0;
                }
                ReadRequest(current.Surrender);
                Step(current.Surrender);
            }
            bool visible = Current != View.Hidden && !hidden;
            if (card.gameObject.activeSelf != visible)
                card.gameObject.SetActive(visible);
            if (visible && transform.GetSiblingIndex() != transform.parent.childCount - 1)
                transform.SetAsLastSibling();
        }

        void ReadRequest(SurrenderRequest request)
        {
            if (request.Revision == seen)
                return;
            seen = request.Revision;
            string self = match.LocalPlayer;
            if (request.Open)
            {
                if (Clock() < dismissedUntil)
                    return;
                sentAt = -1;
                answered |= request.HasAgreed(self);
                if (answered)
                    Show(View.Waiting);
                else if (Current != View.Ask && Current != View.Partner)
                {
                    Show(request.Proposer == self ? View.Ask : View.Partner);
                    if (request.Proposer != self && Application.isPlaying)
                        SfxPlayer.Play(GameSfx.Countdown);
                }
                return;
            }
            answered = false;
            sentAt = -1;
            dismissedUntil = 0;
            var notice = SurrenderText.EndNotice(request.End, request.EndActor == self, Name(request.EndActor), request.EndSeconds);
            if (notice == null)
                Hide();
            else
                ShowNotice(notice);
        }

        void Step(SurrenderRequest request)
        {
            double now = Clock(), left = request.ClosesAt - now;
            if (Current == View.Notice && now >= noticeUntil)
                Hide();
            bool lost = request.Open && left < -3 && Current != View.Hidden && Current != View.Notice;
            if (lost || Current == View.Waiting && sentAt >= 0 && now >= sentAt + AnswerSeconds)
            {
                answered = false;
                ShowNotice(SurrenderText.NoAnswer);
            }
            bool counting = request.Open && Current != View.Hidden && Current != View.Notice;
            if (timer.parent.gameObject.activeSelf != counting)
                timer.parent.gameObject.SetActive(counting);
            if (!counting)
            {
                if (shownSecond != -1)
                {
                    shownSecond = -1;
                    countdown.text = "";
                }
                return;
            }
            timer.anchorMax = new Vector2(Mathf.Clamp01((float)(left / SurrenderDesk.Seconds)), 1);
            int second = Mathf.Max(0, Mathf.CeilToInt((float)left));
            if (second != shownSecond)
            {
                shownSecond = second;
                countdown.text = SurrenderText.Countdown(left);
            }
        }

        string Name(string id)
        {
            string name = match?.Roster?.Find(id)?.name;
            return string.IsNullOrEmpty(name) ? "Đồng đội" : name;
        }

        // ------------------------------------------------------------------------------------------ view
        void Hide()
        {
            Current = View.Hidden;
            leave = null;
            card.gameObject.SetActive(false);
        }

        void ShowNotice(string text)
        {
            noticeUntil = Clock() + NoticeSeconds;
            Show(View.Notice);
            body.text = text;
        }

        void Show(View view)
        {
            Current = view;
            var partner = match?.SurrenderPartner;
            string partnerName = partner == null ? null : Name(partner.id);
            bool rated = match?.Roster != null && !match.Roster.custom;
            bool team = match?.Roster != null && match.Roster.mode == PlayMode.Doubles;
            bool centred = view == View.Ask || view == View.Leave || view == View.Waiting && partner == null;
            bool buttons = view == View.Ask || view == View.Partner || view == View.Leave || view == View.Waiting && partner != null;
            switch (view)
            {
                case View.Ask:
                    Set(SurrenderText.AskTitle, SurrenderText.AskBody(partnerName, team, rated), SurrenderText.Accept, SurrenderText.KeepPlaying);
                    break;
                case View.Partner:
                    Set(SurrenderText.PartnerTitle, SurrenderText.PartnerBody(Name(match.Surrender.Proposer), rated), SurrenderText.Accept, SurrenderText.Refuse);
                    break;
                case View.Waiting:
                    Set(partner == null ? SurrenderText.AskTitle : SurrenderText.WaitTitle,
                        partner == null ? SurrenderText.Sending : SurrenderText.WaitBody(partnerName), "", SurrenderText.Withdraw);
                    break;
                case View.Leave:
                    Set(SurrenderText.LeaveTitle, SurrenderText.LeaveBody, "RỜI TRẬN", "Ở LẠI");
                    break;
                case View.Notice:
                    Set("", body.text, "", "");
                    break;
            }
            bool both = buttons && acceptLabel.text.Length > 0;
            accept.gameObject.SetActive(both);
            refuse.gameObject.SetActive(buttons);
            ActionRect(refuse, both ? .5f : .25f, both ? 1 : .75f, both ? 5 : 0, both ? 16 : 0);
            if (view == View.Notice)
                Fill(body.rectTransform, 22, 10, 22, 10);
            else
                Fill(body.rectTransform, 22, buttons ? 92 : 40, 22, 62);
            body.alignment = view == View.Notice ? TextAlignmentOptions.Center : TextAlignmentOptions.Top;
            Place(centred, view == View.Notice ? NoticeSize : centred ? AskSize : CardSize);
            card.gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        void Set(string heading, string text, string yes, string no)
        {
            title.text = heading;
            body.text = text;
            acceptLabel.text = yes;
            refuseLabel.text = no;
        }

        void Place(bool centred, Vector2 size)
        {
            card.sizeDelta = size;
            if (centred)
            {
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
                card.anchoredPosition = Vector2.zero;
                return;
            }
            // Right under the button of the court HUD, which already respects the safe area of the device.
            card.anchorMin = card.anchorMax = card.pivot = Vector2.one;
            card.anchoredPosition = anchor ? anchor.anchoredPosition + new Vector2(0, -anchor.rect.height - 10) : new Vector2(-18, -170);
        }

        void Build(RectTransform button, TMP_FontAsset font)
        {
            anchor = button;
            if (!font)
                font = ScreenUiTheme.Current && ScreenUiTheme.Current.font ? ScreenUiTheme.Current.font : TMP_Settings.defaultFontAsset;
            card = MatchPresentationUi.Image("Card", transform, Vector2.one, Vector2.one, LobbyPanelStyle.Surface, true).rectTransform;
            LobbyPanelStyle.SurfaceStyle(card);
            card.gameObject.AddComponent<CanvasGroup>();
            card.gameObject.AddComponent<UiPanelReveal>();
            // The glass surface has bevelled corners, so the lines stop before them.
            var accent = MatchPresentationUi.Image("Accent", card, new Vector2(0, 1), Vector2.one, Danger).rectTransform;
            accent.offsetMin = new Vector2(Bevel, -3);
            accent.offsetMax = new Vector2(-Bevel, 0);

            title = Text("Title", font, 21, ScreenUiTheme.Text, FontStyles.Bold);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(18, -56);
            title.rectTransform.offsetMax = new Vector2(-18, -14);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 15;
            title.fontSizeMax = 21;
            body = Text("Body", font, 17, ScreenUiTheme.Text, FontStyles.Normal);
            body.enableAutoSizing = true;
            body.fontSizeMin = 13;
            body.fontSizeMax = 17;
            countdown = Text("Countdown", font, 14, ScreenUiTheme.Muted, FontStyles.Normal);
            countdown.rectTransform.anchorMin = Vector2.zero;
            countdown.rectTransform.anchorMax = new Vector2(1, 0);
            countdown.rectTransform.offsetMin = new Vector2(18, 66);
            countdown.rectTransform.offsetMax = new Vector2(-18, 88);
            countdown.text = "";

            accept = Action("Accept", font, false, Accept, out acceptLabel);
            acceptLabel.color = Danger;
            refuse = Action("Refuse", font, true, Refuse, out refuseLabel);
            ActionRect(accept, 0, .5f, 16, 5);
            ActionRect(refuse, .5f, 1, 5, 16);

            var track = MatchPresentationUi.Image("Timer", card, Vector2.zero, new Vector2(1, 0), new Color(1, 1, 1, .08f));
            track.rectTransform.offsetMin = new Vector2(Bevel, 0);
            track.rectTransform.offsetMax = new Vector2(-Bevel, 4);
            timer = MatchPresentationUi.Image("Fill", track.transform, Vector2.zero, Vector2.one, Danger).rectTransform;
            track.gameObject.SetActive(false);
            Hide();
        }

        TMP_Text Text(string name, TMP_FontAsset font, float size, Color colour, FontStyles style)
        {
            var text = MatchPresentationUi.Label(name, card, font, "", size, Vector2.zero, Vector2.one, colour);
            text.fontStyle = style;
            text.richText = false;
            return text;
        }

        Button Action(string name, TMP_FontAsset font, bool primary, UnityEngine.Events.UnityAction clicked, out TMP_Text label)
        {
            var button = MatchPresentationUi.Button(name, card, font, "", Vector2.zero, Vector2.zero, primary);
            LobbyPanelStyle.Button(button, primary);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(clicked);
            label = button.GetComponentInChildren<TMP_Text>(true);
            label.fontSize = 16;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        static void ActionRect(Button button, float left, float right, float insetLeft, float insetRight)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(left, 0);
            rect.anchorMax = new Vector2(right, 0);
            rect.offsetMin = new Vector2(insetLeft, 14);
            rect.offsetMax = new Vector2(-insetRight, 60);
        }

        static void Fill(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
