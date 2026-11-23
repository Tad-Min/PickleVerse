using System;
using System.Collections.Generic;
using System.Linq;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Ui = PickleVerse.Multiplayer.MatchPresentationUi;

namespace PickleVerse.Multiplayer.UI
{
    public sealed partial class PlayMultiplayerPresenter
    {
        enum PositionDialogKind { None, Confirm, Incoming, Feedback }

        PhotonSession positionRoom;
        RoomMember positionDraft;
        int positionDraftEpoch;
        string positionRequestId, positionTargetName, positionResponseId, positionFeedback;
        double positionAckDeadline;
        bool positionDialogDeferred;
        GameObject positionDialog, previousSelection;
        RectTransform positionCard, positionActions;
        TMP_Text positionMessage;
        Button positionPrimary, positionSecondary;
        PositionDialogKind positionDialogKind;
        string positionDialogId;

        string PositionSwapNotice => positionRequestId != null ? "Đang chờ phản hồi đổi vị trí…" :
            state.IsCaptain && positionRoom?.Data.positionSwap != null ? "Đang có yêu cầu đổi vị trí trong phòng." : null;

        void AddPositionButton(RoomPlayerSlot slot)
        {
            var card = slot.name.transform.parent;
            var image = Ui.Image("Xin đổi vị trí", card, new Vector2(.04f, .85f), new Vector2(.22f, .98f), LobbyPanelStyle.Card, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ScreenUiTheme.ButtonStyle(button);
            var icon = Ui.Rect("Vị trí", button.transform, new Vector2(.12f, .12f), new Vector2(.88f, .88f)).gameObject.AddComponent<UiLineIcon>();
            icon.SetGlyph(ArenaGlyph.Position);
            icon.color = ScreenUiTheme.Mint;
            icon.stroke = .10f;
            icon.raycastTarget = false;
            slot.positionSwap = button;
            button.gameObject.SetActive(false);
            Bind(button, () =>
            {
                if (positionRoom == null || positionRoom.Data.positionSwap != null || positionRequestId != null || !CanShowPositionDialog)
                    return;
                positionDraft = state.Members().FirstOrDefault(member => member.id == slot.playerId && member.id != state.PlayerId);
                positionDraftEpoch = positionRoom.Data.positionEpoch;
                Refresh();
            });
        }

        bool CanShowPositionDialog => state != null && !state.Busy && !(inviteDialog && inviteDialog.activeSelf) &&
            !SettingsOverlay.IsOpen && !SettingsOverlay.Loading && !SceneLoading.IsBlocking &&
            RoomInvitationsRuntime.Instance?.Responding != true &&
            (!FrontEndModalInput.IsOpen || FrontEndModalInput.IsOwnedBy(this));

        void RefreshPositionDialog(List<RoomMember> members)
        {
            var room = MultiplayerApp.Instance?.Room;
            if (!isActiveAndEnabled || initializing || returningToModes || !state.Custom || state.State != FlowState.WaitingRoom ||
                room == null || room.Closing || !room.Runner || !room.Runner.IsRunning || room.Data.phase != "idle" || SceneLoading.IsBlocking)
            {
                ResetPositionDialog();
                return;
            }
            if (positionRoom != room)
            {
                ResetPositionDialog();
                positionRoom = room;
            }

            var data = room.Data;
            var offer = data.positionSwap;
            if (positionRequestId != null)
            {
                if (data.positionSwapResults?.TryGetValue(state.PlayerId, out var result) == true && result.id == positionRequestId)
                {
                    positionFeedback = result.status switch
                    {
                        RoomPositionSwapStatus.Accepted => null,
                        RoomPositionSwapStatus.Declined => $"Người chơi {positionTargetName} không đồng ý việc đổi vị trí",
                        RoomPositionSwapStatus.Expired => "Yêu cầu đổi vị trí đã hết hạn.",
                        RoomPositionSwapStatus.Busy => "Đang có yêu cầu đổi vị trí khác trong phòng.",
                        RoomPositionSwapStatus.Unavailable => "Người chơi được chọn đã rời phòng.",
                        _ => "Yêu cầu đổi vị trí đã hủy vì phòng có thay đổi."
                    };
                    positionRequestId = null;
                    positionAckDeadline = 0;
                }
                else if (offer?.id == positionRequestId && offer.requesterId == state.PlayerId)
                    positionAckDeadline = 0;
                else if (positionAckDeadline == 0 || positionDraftEpoch != data.positionEpoch ||
                    Time.realtimeSinceStartupAsDouble >= positionAckDeadline)
                {
                    positionFeedback = positionAckDeadline == 0 || positionDraftEpoch != data.positionEpoch
                        ? "Yêu cầu đổi vị trí đã hủy vì phòng có thay đổi."
                        : "Phòng chưa nhận được yêu cầu đổi vị trí. Vui lòng thử lại.";
                    positionRequestId = null;
                    positionAckDeadline = 0;
                }
            }

            if (positionDraft != null && (offer != null || positionDraftEpoch != data.positionEpoch ||
                members.All(member => member.id != positionDraft.id)))
                positionDraft = null;
            if (positionResponseId != offer?.id)
                positionResponseId = null;

            if (offer != null && offer.targetId == state.PlayerId)
            {
                string sender = members.FirstOrDefault(member => member.id == offer.requesterId)?.name ?? "Người chơi";
                ShowPositionDialog(PositionDialogKind.Incoming, offer.id,
                    $"Người chơi {sender} muốn đổi vị trí với bạn, bạn có đồng ý đổi không", "Đồng ý", "Không");
            }
            else if (positionDraft != null)
                ShowPositionDialog(PositionDialogKind.Confirm, positionDraft.id,
                    $"Bạn xác nhận muốn đổi vị trí với người chơi {positionDraft.name}", "Xác nhận", "Hủy");
            else if (positionFeedback != null)
                ShowPositionDialog(PositionDialogKind.Feedback, null, positionFeedback, "Đóng", null);
            else
            {
                positionDialogDeferred = false;
                HidePositionDialog();
            }
        }

        void UpdatePositionDialog()
        {
            bool visible = positionDialog && positionDialog.activeSelf;
            if ((positionRoom != null && positionRoom != MultiplayerApp.Instance?.Room) ||
                (positionDialogDeferred && CanShowPositionDialog) || (visible && !CanShowPositionDialog) ||
                (positionAckDeadline > 0 && Time.realtimeSinceStartupAsDouble >= positionAckDeadline))
            {
                Refresh();
                return;
            }
            if (visible && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ActOnPositionDialog(false);
        }

        void ActOnPositionDialog(bool primary)
        {
            if (!FrontEndModalInput.IsOwnedBy(this) || !CanShowPositionDialog || positionRoom == null || positionRoom != MultiplayerApp.Instance?.Room)
                return;
            if (positionDialogKind == PositionDialogKind.Confirm)
            {
                var target = positionDraft;
                positionDraft = null;
                if (primary && target != null)
                {
                    positionRequestId = Guid.NewGuid().ToString("N");
                    positionTargetName = target.name;
                    positionAckDeadline = Time.realtimeSinceStartupAsDouble + PhotonRoomState.PositionRequestArrivalSeconds + 2;
                    try { rooms.RequestPositionSwap(positionRequestId, target.id, positionDraftEpoch); }
                    catch (InvalidOperationException error)
                    {
                        positionRequestId = null;
                        positionAckDeadline = 0;
                        positionFeedback = error.Message;
                    }
                }
            }
            else if (positionDialogKind == PositionDialogKind.Incoming)
            {
                if (positionResponseId != null)
                    return;
                positionResponseId = positionDialogId;
                try { rooms.RespondToPositionSwap(positionResponseId, primary); }
                catch (InvalidOperationException error)
                {
                    positionResponseId = null;
                    MultiplayerApp.Instance.SetNotice(error.Message);
                }
            }
            else if (positionDialogKind == PositionDialogKind.Feedback)
                positionFeedback = null;
            Refresh();
        }

        void BuildPositionDialog()
        {
            var overlay = Ui.Rect("Yêu cầu đổi vị trí", transform, Vector2.zero, Vector2.one);
            positionDialog = overlay.gameObject;
            positionDialog.SetActive(false);
            var canvas = positionDialog.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 600;
            positionDialog.AddComponent<GraphicRaycaster>();
            Ui.Image("Nền hộp thoại", overlay, Vector2.zero, Vector2.one, new Color(0, 0, 0, .65f), true);
            var safe = Ui.Rect("Safe Area", overlay, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<UiSafeArea>();
            positionCard = Ui.Image("Hộp thoại", safe, new Vector2(.22f, .32f), new Vector2(.78f, .68f), LobbyPanelStyle.Surface, true).rectTransform;
            LobbyPanelStyle.SurfaceStyle(positionCard);
            var font = roomType.font;
            positionMessage = Ui.Label("Thông báo", positionCard, font, "", 27, new Vector2(.06f, .39f), new Vector2(.94f, .90f), ScreenUiTheme.Text);
            positionMessage.richText = false;
            positionMessage.enableAutoSizing = true;
            positionMessage.fontSizeMin = 16;
            positionMessage.fontSizeMax = 27;
        }

        void BuildPositionActions(string primary, string secondary)
        {
            // A pointer held on the previous dialog must not click a newly arrived request.
            if (positionActions)
            {
                positionActions.gameObject.SetActive(false);
                Destroy(positionActions.gameObject);
            }
            var actions = positionActions = Ui.Rect("Thao tác", positionCard, Vector2.zero, Vector2.one);
            var font = positionMessage.font;
            bool hasSecondary = !string.IsNullOrEmpty(secondary);
            positionPrimary = Ui.Button(primary, actions, font, primary, new Vector2(hasSecondary ? .52f : .31f, .10f),
                new Vector2(hasSecondary ? .92f : .69f, .32f), true);
            ScreenUiTheme.ButtonStyle(positionPrimary, true);
            positionPrimary.onClick.AddListener(() => { if (positionActions == actions) ActOnPositionDialog(true); });
            positionSecondary = null;
            if (hasSecondary)
            {
                positionSecondary = Ui.Button(secondary, actions, font, secondary, new Vector2(.08f, .10f), new Vector2(.48f, .32f), false);
                ScreenUiTheme.ButtonStyle(positionSecondary);
                positionSecondary.onClick.AddListener(() => { if (positionActions == actions) ActOnPositionDialog(false); });
                positionSecondary.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = positionPrimary };
            }
            positionPrimary.navigation = new Navigation { mode = hasSecondary ? Navigation.Mode.Explicit : Navigation.Mode.None, selectOnLeft = positionSecondary };
        }

        void ShowPositionDialog(PositionDialogKind kind, string id, string message, string primary, string secondary)
        {
            positionDialogDeferred = !CanShowPositionDialog || !FrontEndModalInput.TryAcquire(this);
            if (positionDialogDeferred)
            {
                HidePositionDialog();
                return;
            }
            if (!positionDialog)
                BuildPositionDialog();
            bool opening = !positionDialog.activeSelf;
            bool changed = opening || kind != positionDialogKind || id != positionDialogId;
            if (opening && EventSystem.current)
                previousSelection = EventSystem.current.currentSelectedGameObject;
            positionDialogKind = kind;
            positionDialogId = id;
            positionDialog.SetActive(true);
            positionMessage.text = message;
            if (changed)
                BuildPositionActions(primary, secondary);
            positionPrimary.interactable = positionResponseId == null;
            if (positionSecondary)
                positionSecondary.interactable = positionResponseId == null;
            if (changed && EventSystem.current)
                EventSystem.current.SetSelectedGameObject((positionSecondary ? positionSecondary : positionPrimary).gameObject);
        }

        void HidePositionDialog()
        {
            FrontEndModalInput.Release(this);
            if (positionDialog && positionDialog.activeSelf)
            {
                positionDialog.SetActive(false);
                if (EventSystem.current)
                    EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
            }
            previousSelection = null;
            positionDialogKind = PositionDialogKind.None;
            positionDialogId = null;
        }

        void ResetPositionDialog()
        {
            HidePositionDialog();
            positionRoom = null;
            positionDraft = null;
            positionRequestId = positionTargetName = positionResponseId = positionFeedback = null;
            positionAckDeadline = 0;
            positionDialogDeferred = false;
        }
    }
}
