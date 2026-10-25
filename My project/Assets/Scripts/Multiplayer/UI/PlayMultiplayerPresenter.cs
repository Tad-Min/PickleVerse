using System;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Multiplayer.Services;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PickleVerse.Multiplayer.UI
{
    [Serializable]
    public sealed class RoomPlayerSlot
    {
        public TMP_Text name;
        public UnityEngine.UI.Button kick;
        [NonSerialized]
        public string playerId;
    }

    /// <summary>Serialized bindings to the user's Canvas. All room operations use services.</summary>
    public sealed class PlayMultiplayerPresenter : MonoBehaviour
    {
        public TMP_InputField joinCode;
        public UnityEngine.UI.Button back;
        public TMP_Text navigationError;
        bool returningToModes;
        public TMP_Text roomType, matchTime, roomCode;
        public UnityEngine.UI.Button join, singles, doubles, custom, copy, play, swap;
        public GameObject singlesPanel, doublesPanel, customPanel;
        public RoomPlayerSlot singleSlot;
        public RoomPlayerSlot[] duoSlots, customSlots;
        public PlayMode customMode = PlayMode.Doubles;
        public TMP_Text roomHint, memberCount, inviteCode;
        public GameObject inviteDialog;
        public UnityEngine.UI.Button invite, inviteCopy, inviteClose, retry;
        RoomLaunchAction launchAction;
        string launchCode;
        string launchRegion;
        RoomFriendInvitePanel friendInvites;
        bool initializing;
        public bool IsInitializing => initializing;
        float nextClockUpdate;
        int arrangedSeats;
        IRoomService rooms;
        IMatchmakingService matchmaking;
        IMultiplayerState state;
        IPlayerIdentityService identity;
        readonly System.Collections.Generic.List<(UnityEngine.UI.Button button, UnityAction action)> listeners = new System.Collections.Generic.List<(UnityEngine.UI.Button, UnityAction)>();

        async void Start()
        {
            var app = MultiplayerApp.Ensure();
            app.RoomSceneName = gameObject.scene.name;
            rooms = app.RoomService;
            matchmaking = app.MatchmakingService;
            state = rooms.State;
            identity = app.IdentityService;
            state.Changed += Refresh;
            friendInvites = RoomFriendInvitePanel.Attach(this);
            Bind(back, Back);
            Bind(singles, () => Run(rooms.SelectSinglesAsync));
            Bind(doubles, () => Run(rooms.SelectDoublesAsync));
            Bind(custom, () => Run(() => rooms.SelectCustomAsync(customMode)));
            Bind(join, () => Run(() => rooms.JoinAsync(joinCode.text)));
            Bind(copy, () =>
            {
                GUIUtility.systemCopyBuffer = state.RoomCode;
                app.SetNotice("Đã sao chép mã phòng.");
            });
            Bind(swap, () => Run(rooms.SwapTeamAsync));
            Bind(play, () => Run(PlayAsync));
            Bind(invite, ShowInvite);
            Bind(inviteClose, CloseInvite);
            Bind(retry, () => Run(InitializeRoom));
            foreach (var slot in customSlots)
                Bind(slot.kick, () =>
                {
                    if (!string.IsNullOrEmpty(slot.playerId))
                        Run(() => rooms.KickAsync(slot.playerId));
                });
            foreach (var slot in duoSlots.Concat(customSlots).Where(slot => slot != null))
            {
                var button = slot.name ? slot.name.transform.parent.Find("BtnInvite")?.GetComponent<UnityEngine.UI.Button>() : null;
                Bind(button, ShowInvite);
            }
            launchAction = RoomLaunchRequest.Consume(out launchCode, out launchRegion);
            if (!Environment.GetCommandLineArgs().Contains("-pkvCase"))
                await InitializeRoom();
            if (this)
                Refresh();
        }
        async Task InitializeRoom()
        {
            if (initializing || returningToModes)
                return;
            initializing = true;
            Refresh();
            try
            {
                var localPlayer = PickleVerse.Progression.ProgressionRuntime.Instance.Player;
                string profile = localPlayer.id;
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-pkvProfile")
                        profile = args[i + 1];
                bool openDefault = MultiplayerApp.OpenDefaultRoomOnLoad;
                MultiplayerApp.OpenDefaultRoomOnLoad = false;
                if (!args.Contains("-pkvCase") && (state.State == FlowState.Menu || openDefault))
                    await rooms.OpenAsync(profile, localPlayer.playerName);
                if (!this || returningToModes || state.State != FlowState.WaitingRoom)
                    return;
                if (launchAction == RoomLaunchAction.CreateSingles || launchAction == RoomLaunchAction.CreateDoubles)
                {
                    var mode = launchAction == RoomLaunchAction.CreateSingles ? PlayMode.Singles : PlayMode.Doubles;
                    await rooms.SelectCustomAsync(mode);
                    if (state.Custom && state.Mode == mode && !string.IsNullOrEmpty(state.RoomCode))
                        launchAction = RoomLaunchAction.None;
                }
                else if (launchAction == RoomLaunchAction.Join)
                {
                    await MultiplayerApp.Instance.ExecuteAsync(() => MultiplayerApp.Instance.JoinRoomAsync(launchCode, launchRegion));
                    if (!string.IsNullOrEmpty(state.RoomCode))
                        launchAction = RoomLaunchAction.None;
                }
            }
            finally
            {
                initializing = false;
                if (this)
                    Refresh();
            }
        }
        public void ShowInvite()
        {
            if (!inviteDialog || string.IsNullOrEmpty(state?.RoomCode))
                return;
            friendInvites.Open();
            Refresh();
        }
        public void CloseInvite()
        {
            if (!inviteDialog)
                return;
            inviteDialog.SetActive(false);
            Refresh();
            if (EventSystem.current && invite && invite.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(invite.gameObject);
        }
        void Bind(UnityEngine.UI.Button button, UnityAction action)
        {
            if (!button)
                return;
            button.onClick.AddListener(action);
            listeners.Add((button, action));
        }
        async void Run(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception e)
            {
                MultiplayerApp.Instance.SetNotice(e.Message);
            }
            if (this)
                Refresh();
        }
        async void Back()
        {
            if (returningToModes)
                return;
            returningToModes = true;
            Refresh();
            if (navigationError)
                navigationError.text = "";
            try
            {
                await MultiplayerApp.Instance.ReturnToModeSelectionAsync();
            }
            catch (Exception e)
            {
                if (navigationError)
                    navigationError.text = "Không thể rời phòng: " + e.Message;
                MultiplayerApp.Instance.SetNotice(e.Message);
            }
            finally
            {
                if (this)
                {
                    returningToModes = false;
                    Refresh();
                }
            }
        }
        async Task PlayAsync()
        {
            if (state.Searching)
            {
                await matchmaking.CancelAsync();
                return;
            }
            if (!state.Custom)
                await matchmaking.FindAsync();
            else if (RoomViewModel.CanStartCustom(state))
                await rooms.StartCustomAsync();
        }
        void Update()
        {
            if (state != null && matchTime.gameObject.activeSelf && Time.unscaledTime >= nextClockUpdate)
            {
                nextClockUpdate = Time.unscaledTime + .2f;
                var elapsed = state.Elapsed;
                if (matchTime.text != elapsed)
                    matchTime.text = elapsed;
            }
            if (inviteDialog && inviteDialog.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CloseInvite();
        }
        void Refresh()
        {
            if (state == null || !this)
                return;
            bool modal = inviteDialog && inviteDialog.activeSelf;
            bool idle = state.State == FlowState.WaitingRoom && !state.Busy && !initializing && !modal;
            bool isDoubles = !state.Custom && state.Mode == PlayMode.Doubles;
            singlesPanel.SetActive(!state.Custom && state.Mode == PlayMode.Singles);
            doublesPanel.SetActive(isDoubles);
            customPanel.SetActive(state.Custom);
            roomType.text = state.Custom ? "PHÒNG RIÊNG  /  " + (state.Mode == PlayMode.Singles ? "1v1" : "2v2") : isDoubles ? "ĐẤU ĐÔI  /  2v2" : "ĐẤU ĐƠN  /  1v1";
            SelectMode(singles, !state.Custom && !isDoubles);
            SelectMode(doubles, isDoubles);
            SelectMode(custom, state.Custom);
            bool showCode = isDoubles || state.Custom;
            roomCode.gameObject.SetActive(showCode);
            copy.gameObject.SetActive(showCode);
            roomCode.text = "MÃ PHÒNG ID:\n" + state.RoomCode;
            copy.interactable = !modal && state.RoomCode.Length > 0;
            idle = idle && !returningToModes;
            singles.interactable = doubles.interactable = join.interactable = idle;
            singles.gameObject.SetActive(!state.Custom);
            doubles.gameObject.SetActive(!state.Custom);
            if (custom)
                custom.gameObject.SetActive(false);
            joinCode.interactable = idle && !returningToModes;
            if (back)
                back.interactable = !returningToModes && !modal;
            if (swap)
            {
                swap.gameObject.SetActive(state.Custom);
                swap.interactable = idle;
            }
            play.GetComponentInChildren<TMP_Text>(true).text = RoomViewModel.PlayLabel(state);
            play.interactable = !returningToModes && !modal && (state.Searching || (idle && (state.Custom ? RoomViewModel.CanStartCustom(state) : state.IsCaptain)));
            matchTime.gameObject.SetActive(state.Searching);
            matchTime.text = state.Elapsed;
            var members = state.Members();
            if (roomHint)
                roomHint.text = initializing ? "Đang kết nối dịch vụ phòng…" : state.Notice;
            if (memberCount)
                memberCount.text = state.Custom ? members.Count + " / " + (2 * (int)state.Mode) + " NGƯỜI CHƠI" : "GHÉP TRẬN THEO ELO  •  " + (isDoubles ? "TỔ ĐỘI 2 NGƯỜI" : "ĐẤU ĐƠN");
            if (invite)
            {
                invite.gameObject.SetActive(showCode);
                invite.interactable = idle && !string.IsNullOrEmpty(state.RoomCode);
            }
            if (retry)
                retry.gameObject.SetActive(!modal && !initializing && !state.Busy && (state.State == FlowState.Menu || launchAction != RoomLaunchAction.None));
            if (state.Custom)
                ArrangeCustom((int)state.Mode);
            Fill(singleSlot, members.FirstOrDefault(m => m.id == state.PlayerId));
            var duo = members.OrderBy(m => m.id == state.PlayerId ? 0 : 1).ToArray();
            for (int i = 0; i < duoSlots.Length; i++)
                Fill(duoSlots[i], i < duo.Length ? duo[i] : null);
            for (int team = 0; team < 2; team++)
            {
                var teamMembers = members.Where(m => m.team == team).ToArray();
                for (int seat = 0; seat < 2; seat++)
                    Fill(customSlots[team * 2 + seat], seat < teamMembers.Length ? teamMembers[seat] : null);
            }
        }
        void ArrangeCustom(int seats)
        {
            if (arrangedSeats == seats)
                return;
            arrangedSeats = seats;
            for (int team = 0; team < 2; team++)
                for (int seat = 0; seat < 2; seat++)
                {
                    var root = customSlots[team * 2 + seat].name.transform.parent as RectTransform;
                    root.gameObject.SetActive(seat < seats);
                    root.anchorMin = new Vector2(seats == 1 ? .16f : seat == 0 ? .02f : .52f, .13f);
                    root.anchorMax = new Vector2(seats == 1 ? .84f : seat == 0 ? .48f : .98f, .86f);
                    root.offsetMin = root.offsetMax = Vector2.zero;
                }
        }
        void Fill(RoomPlayerSlot slot, RoomMember member)
        {
            if (slot == null)
                return;
            slot.playerId = member?.id;
            if (slot.name)
            {
                slot.name.richText = false;
                slot.name.text = member?.name ?? "Chờ người chơi";
            }
            var portrait = slot.name ? slot.name.transform.parent.Find("ImgIcon/LocalPortrait") : null;
            if (portrait)
            {
                bool local = member != null && member.id == state.PlayerId;
                portrait.gameObject.SetActive(local);
                var silhouette = portrait.parent.GetComponent<UnityEngine.UI.Image>();
                if (silhouette)
                    silhouette.color = local ? Color.clear : Color.white;
            }
            // Both doubles and custom empty seats open the room invitation dialog.
            var invite = slot.name ? slot.name.transform.parent.Find("BtnInvite") : null;
            if (invite)
            {
                invite.gameObject.SetActive(member == null);
                var inviteButton = invite.GetComponent<UnityEngine.UI.Button>();
                if (inviteButton)
                    inviteButton.interactable = !state.Busy && !initializing && !returningToModes && state.State == FlowState.WaitingRoom && !string.IsNullOrEmpty(state.RoomCode) && !(inviteDialog && inviteDialog.activeSelf);
            }
            if (slot.kick)
            {
                slot.kick.gameObject.SetActive(state.Custom && state.IsCaptain && member != null && member.id != state.PlayerId);
                slot.kick.interactable = !state.Busy && state.State == FlowState.WaitingRoom && !(inviteDialog && inviteDialog.activeSelf);
            }
        }
        static void SelectMode(UnityEngine.UI.Button button, bool selected)
        {
            if (!button)
                return;
            var border = button.transform.Find("SelectedBorder");
            if (border)
                border.gameObject.SetActive(selected);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label)
                label.color = selected ? new Color32(131, 213, 190, 255) : new Color32(174, 190, 197, 255);
        }
        void OnDestroy()
        {
            if (state != null)
                state.Changed -= Refresh;
            foreach (var listener in listeners)
                if (listener.button)
                    listener.button.onClick.RemoveListener(listener.action);
        }
    }
}
