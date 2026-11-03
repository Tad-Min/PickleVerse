using System;
using PickleVerse.Multiplayer;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    public sealed partial class PlayerProgressionMenu
    {
        Button copyIdentity, renameIdentity, saveIdentity, cancelIdentity;
        RectTransform renameDialog;
        TMP_InputField renameInput;
        TMP_Text identityNotice, renameError;
        bool savingIdentity;
        float identityNoticeUntil;

        void ConfigureIdentityActions()
        {
            copyIdentity = IdentityIcon("CopyPlayerId", ArenaGlyph.Copy, () =>
            {
                var id = AccountRuntime.Ensure().Account.Profile?.UserId.ToString("N");
                if (string.IsNullOrEmpty(id)) return;
                GUIUtility.systemCopyBuffer = id;
                identityNotice.text = "Đã sao chép mã người chơi";
                identityNoticeUntil = Time.unscaledTime + 3;
            });
            renameIdentity = IdentityIcon("EditPlayerName", ArenaGlyph.Edit, OpenRename);
            identityNotice = Text("IdentityNotice", left, "", 11, Mint, new Vector2(.05f, .50f), new Vector2(.95f, .56f), Vector2.zero, Vector2.zero);
            identityNotice.raycastTarget = false;
            renameDialog = Panel("RenamePlayer", modal, Ink, new Vector2(.27f, .28f), new Vector2(.73f, .72f));
            renameDialog.SetAsLastSibling();
            Text("Title", renameDialog, "ĐỔI TÊN NGƯỜI CHƠI", 23, White, new Vector2(.06f, .75f), new Vector2(.94f, .93f), Vector2.zero, Vector2.zero);
            renameInput = Input("PlayerName", renameDialog, new Vector2(.06f, .49f), new Vector2(.94f, .69f), 18);
            renameInput.textComponent.richText = false;
            renameInput.lineType = TMP_InputField.LineType.SingleLine;
            renameError = Text("Error", renameDialog, "", 13, Mint, new Vector2(.06f, .26f), new Vector2(.94f, .46f), Vector2.zero, Vector2.zero);
            renameError.richText = false;
            saveIdentity = MakeButton("Save", renameDialog, "Lưu", SaveRename, new Vector2(.52f, .07f), new Vector2(.94f, .23f), Mint, Ink, 17);
            cancelIdentity = MakeButton("Cancel", renameDialog, "Hủy", () => renameDialog.gameObject.SetActive(false), new Vector2(.06f, .07f), new Vector2(.48f, .23f), Card, White, 17);
            renameDialog.gameObject.SetActive(false);
            presentationSize = Vector2.zero; LayoutPresentation();
        }
        Button IdentityIcon(string name, ArenaGlyph glyph, UnityEngine.Events.UnityAction action)
        {
            var button = MakeButton(name, left, "", action, new Vector2(.83f, .6f), new Vector2(.96f, .68f), Card, Mint, 12);
            var iconRect = Rect("Icon", button.transform, new Vector2(.2f, .2f), new Vector2(.8f, .8f));
            var icon = iconRect.gameObject.AddComponent<UiLineIcon>(); icon.glyph = glyph; icon.color = Mint; icon.raycastTarget = false;
            return button;
        }
        bool CanRename => AccountRuntime.Ensure().Account.IsSignedIn && (!MultiplayerApp.Instance || MultiplayerApp.Instance.CanChangeLocalProfile);
        void RefreshIdentityActions()
        {
            if (renameIdentity) renameIdentity.interactable = !savingIdentity && CanRename;
            if (identityNotice && Time.unscaledTime >= identityNoticeUntil) identityNotice.text = "";
        }
        void OpenRename()
        {
            if (!CanRename || savingIdentity) return;
            renameInput.SetTextWithoutNotify(AccountRuntime.Ensure().Account.Profile.PlayerName);
            renameError.text = ""; renameDialog.gameObject.SetActive(true); renameDialog.SetAsLastSibling(); renameInput.Select();
        }
        async void SaveRename()
        {
            if (savingIdentity || !CanRename) return;
            string name = renameInput.text.Trim();
            if (name.Length < 3 || name.Length > 18) { renameError.text = "Tên người chơi cần từ 3 đến 18 ký tự."; return; }
            savingIdentity = true; saveIdentity.interactable = cancelIdentity.interactable = renameInput.interactable = false;
            try
            {
                await AccountRuntime.Ensure().Account.UpdateProfileAsync(new UpdateProfileRequest { PlayerName = name });
                await PickleVerse.Progression.ProgressionRuntime.Instance.ReloadAsync();
                if (!this) return;
                renameDialog.gameObject.SetActive(false); Refresh();
                identityNotice.text = "Đã lưu tên người chơi"; identityNoticeUntil = Time.unscaledTime + 3;
            }
            catch (Exception error) { if (this) renameError.text = error is ApiException ? error.Message : "Chưa lưu được tên. Vui lòng thử lại."; }
            finally
            {
                savingIdentity = false;
                if (this) saveIdentity.interactable = cancelIdentity.interactable = renameInput.interactable = true;
            }
        }
    }
}
