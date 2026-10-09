using System;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Multiplayer.Services;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace PickleVerse.Multiplayer.UI
{
    [Serializable] public sealed class RoomPlayerSlot
    {
        public TMP_Text name;
        public UnityEngine.UI.Button kick;
        [NonSerialized] public string playerId;
    }

    /// <summary>Serialized bindings to the user's Canvas. All room operations use services.</summary>
    public sealed class PlayMultiplayerPresenter : MonoBehaviour
    {
        public TMP_InputField playerName, joinCode;
        public TMP_Text roomType, matchTime, roomCode;
        public UnityEngine.UI.Button join, singles, doubles, custom, copy, play, swap;
        public GameObject singlesPanel, doublesPanel, customPanel;
        public RoomPlayerSlot singleSlot;
        public RoomPlayerSlot[] duoSlots, customSlots;
        public PlayMode customMode = PlayMode.Doubles;
        IRoomService rooms;
        IMatchmakingService matchmaking;
        IMultiplayerState state;
        IPlayerIdentityService identity;
        readonly System.Collections.Generic.List<(UnityEngine.UI.Button button, UnityAction action)> listeners = new System.Collections.Generic.List<(UnityEngine.UI.Button, UnityAction)>();

        async void Start()
        {
            var app = MultiplayerApp.Ensure(); app.RoomSceneName = gameObject.scene.name;
            rooms = app.RoomService; matchmaking = app.MatchmakingService; state = rooms.State; identity = app.IdentityService;
            state.Changed += Refresh;
            playerName.characterLimit = 18;
            playerName.onEndEdit.AddListener(NameChanged);
            Bind(singles, () => Run(rooms.SelectSinglesAsync));
            Bind(doubles, () => Run(rooms.SelectDoublesAsync));
            Bind(custom, () => Run(() => rooms.SelectCustomAsync(customMode)));
            Bind(join, () => Run(() => rooms.JoinAsync(joinCode.text)));
            Bind(copy, () => { GUIUtility.systemCopyBuffer = state.RoomCode; app.SetNotice("Đã sao chép mã phòng."); });
            Bind(swap, () => Run(rooms.SwapTeamAsync));
            Bind(play, () => Run(PlayAsync));
            foreach (var slot in customSlots) Bind(slot.kick, () => { if (!string.IsNullOrEmpty(slot.playerId)) Run(() => rooms.KickAsync(slot.playerId)); });
            string profile = app.ActiveProfile ?? "P" + System.Diagnostics.Process.GetCurrentProcess().Id;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-pkvProfile") profile = args[i + 1];
            bool openDefault = MultiplayerApp.OpenDefaultRoomOnLoad;
            MultiplayerApp.OpenDefaultRoomOnLoad = false;
            if (!args.Contains("-pkvCase") && (state.State == FlowState.Menu || openDefault))
                await rooms.OpenAsync(profile, string.IsNullOrWhiteSpace(playerName.text) ? app.DisplayName : playerName.text);
            playerName.SetTextWithoutNotify(state.DisplayName);
            Refresh();
        }
        void Bind(UnityEngine.UI.Button button, UnityAction action)
        {
            if (!button) return;
            button.onClick.AddListener(action); listeners.Add((button, action));
        }
        async void Run(Func<Task> action)
        {
            try { await action(); } catch (Exception e) { MultiplayerApp.Instance.SetNotice(e.Message); }
            if (this) Refresh();
        }
        void NameChanged(string value)
        {
            if (state == null || state.Busy || state.State != FlowState.WaitingRoom) return;
            Run(() => rooms.SetNameAsync(value));
        }
        async Task PlayAsync()
        {
            if (state.Searching) { await matchmaking.CancelAsync(); return; }
            if (!state.Custom) await matchmaking.FindAsync();
            else if (RoomViewModel.CanStartCustom(state)) await rooms.StartCustomAsync();
        }
        void Update() { if (state != null) matchTime.text = state.Elapsed; }
        void Refresh()
        {
            if (state == null || !this) return;
            bool idle = state.State == FlowState.WaitingRoom && !state.Busy;
            bool isDoubles = !state.Custom && state.Mode == PlayMode.Doubles;
            singlesPanel.SetActive(!state.Custom && state.Mode == PlayMode.Singles);
            doublesPanel.SetActive(isDoubles); customPanel.SetActive(state.Custom);
            roomType.text = state.Custom ? "Custom" : isDoubles ? "2Vs2" : "1Vs1";
            SelectMode(singles, !state.Custom && !isDoubles);
            SelectMode(doubles, isDoubles);
            SelectMode(custom, state.Custom);
            bool showCode = isDoubles || state.Custom;
            roomCode.gameObject.SetActive(showCode); copy.gameObject.SetActive(showCode);
            roomCode.text = "MÃ PHÒNG ID:\n" + state.RoomCode; copy.interactable = state.RoomCode.Length > 0;
            singles.interactable = doubles.interactable = custom.interactable = join.interactable = idle;
            joinCode.interactable = playerName.interactable = idle;
            if (!playerName.isFocused) playerName.SetTextWithoutNotify(state.DisplayName);
            if (swap) { swap.gameObject.SetActive(state.Custom); swap.interactable = idle; }
            play.GetComponentInChildren<TMP_Text>(true).text = RoomViewModel.PlayLabel(state);
            play.interactable = state.Searching || (idle && (state.Custom ? RoomViewModel.CanStartCustom(state) : state.IsCaptain));
            matchTime.gameObject.SetActive(state.Searching); matchTime.text = state.Elapsed;
            var members = state.Members();
            Fill(singleSlot, members.FirstOrDefault(m => m.id == state.PlayerId));
            var duo = members.OrderBy(m => m.id == state.PlayerId ? 0 : 1).ToArray();
            for (int i = 0; i < duoSlots.Length; i++) Fill(duoSlots[i], i < duo.Length ? duo[i] : null);
            for (int team = 0; team < 2; team++)
            {
                var teamMembers = members.Where(m => m.team == team).ToArray();
                for (int seat = 0; seat < 2; seat++) Fill(customSlots[team * 2 + seat], seat < teamMembers.Length ? teamMembers[seat] : null);
            }
        }
        void Fill(RoomPlayerSlot slot, RoomMember member)
        {
            if (slot == null) return;
            slot.playerId = member?.id;
            if (slot.name) { slot.name.richText = false; slot.name.text = member?.name ?? "Chờ người chơi"; }
            var portrait = slot.name ? slot.name.transform.parent.Find("ImgIcon/LocalPortrait") : null;
            if (portrait)
            {
                bool local = member != null && member.id == state.PlayerId;
                portrait.gameObject.SetActive(local);
                var silhouette = portrait.parent.GetComponent<UnityEngine.UI.Image>();
                if (silhouette) silhouette.color = local ? Color.clear : Color.white;
            }
            // The future Invite placeholder fills an empty custom seat. Hide it
            // when occupied so its white background cannot cover the player's name.
            var invite=slot.name ? slot.name.transform.parent.Find("BtnInvite") : null;
            if(invite) invite.gameObject.SetActive(member==null);
            if (slot.kick) { slot.kick.gameObject.SetActive(state.Custom && state.IsCaptain && member != null && member.id != state.PlayerId); slot.kick.interactable = !state.Busy && state.State == FlowState.WaitingRoom; }
        }
        static void SelectMode(UnityEngine.UI.Button button, bool selected)
        {
            if (!button) return;
            var border = button.transform.Find("SelectedBorder");
            if (border) border.gameObject.SetActive(selected);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label) label.color = selected ? new Color32(131, 213, 190, 255) : new Color32(174, 190, 197, 255);
        }
        void OnDestroy()
        {
            if (state != null) state.Changed -= Refresh;
            if (playerName) playerName.onEndEdit.RemoveListener(NameChanged);
            foreach (var listener in listeners) if (listener.button) listener.button.onClick.RemoveListener(listener.action);
        }
    }
}
