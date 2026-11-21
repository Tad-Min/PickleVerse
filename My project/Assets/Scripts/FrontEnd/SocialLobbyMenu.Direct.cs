#if !UNITY_SERVER
using System;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Services.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    internal sealed partial class SocialLobbyMenu
    {
        int messagesVersion;
        bool pendingDirectRefresh;
        public void OpenInbox()
        {
            if (!Open(ScreenView.Inbox, "Hộp thư")) return;
            Action("Refresh", editor, "Làm mới", OpenInbox, 0,0,.19f,1);
            Action("Whispers", editor, $"Thì thầm · {whispersReceived.Count}", OpenWhispers, .21f,0,.43f,1);
            _ = LoadInbox(true);
        }
        Task LoadInbox(bool reset) => Run(async token =>
        {
            int requestedOffset = reset ? 0 : offset;
            var items = await Get<InboxItem[]>("conversations?offset=" + requestedOffset, token);
            if (reset) Clear(rows);
            foreach (var item in items.Take(20))
            {
                var row = Row("Conversation", 92);
                Label("Name", row, item.playerName, 24, White, .02f,.48f,.64f,.96f);
                var preview = Label("Preview", row, item.lastMessage, 18, Muted, .02f,.04f,.65f,.48f);
                preview.overflowMode = TextOverflowModes.Ellipsis;
                Action("Read", row, item.unread == 0 ? "Mở" : $"Mở · {item.unread} chưa đọc",
                    () => OpenDirect(item.playerId, item.playerName, item.canSend, item.conversationId), .70f,.17f,.97f,.83f);
            }
            if (requestedOffset == 0 && items.Length == 0) Info("Chưa có tin nhắn bạn bè.");
            offset = requestedOffset + 20;
            if (items.Length > 20) More(() => _ = LoadInbox(false));
            if (reset) Top();
        });
        void OpenWhispers()
        {
            if (!Open(ScreenView.Whispers, "Thì thầm")) return;
            Action("Back", editor, "Hộp thư", OpenInbox, 0,0,.20f,1);
            foreach (var latest in whispersReceived.Where(message => message.senderId != Self).Reverse()
                .GroupBy(message => message.senderId).Select(group => group.First()))
            {
                var row = Row("WhisperThread", 76);
                Label("Sender", row, latest.sender + " · " + latest.body, 19, White, .02f,.05f,.77f,.95f).overflowMode = TextOverflowModes.Ellipsis;
                Action("Reply", row, "Mở", () => OpenDirect(latest.senderId, latest.sender, false), .81f,.12f,.97f,.9f);
            }
            if (whispersReceived.Count == 0) Info("Chưa có thì thầm trong phiên sảnh này.");
        }

        public void OpenDirect(Guid player, string name, bool friend, Guid existing = default)
        {
            if (!Open(ScreenView.Direct, name)) return;
            recipient = player;
            recipientName = name;
            conversation = existing;
            canSend = friend;
            whispers = !friend && existing == Guid.Empty;
            messages.Clear();
            messageId = Guid.Empty;
            messageBody = null;
            BuildChatComposer();
            if (whispers) RenderWhispers();
            else _ = LoadMessages(true);
        }
        void BuildChatComposer()
        {
            Clear(editor);
            if (canSend || whispers)
            {
                entry = RuntimeForms.Input(editor, whispers ? "Thì thầm trong sảnh" : "Nhắn tin cho " + recipientName,
                    new Vector2(0,0), new Vector2(.68f,1), 1000);
                Action("Send", editor, "Gửi", () => _ = SendDirect(), .70f,0,.83f,1,true);
            }
            else Label("ReadOnly", editor, "Bạn có thể đọc lại hội thoại này.", 18, Muted, 0,0,.82f,1);
            Action("Back", editor, "Quay lại", OpenInbox, .85f,0,1,1);
        }
        async Task LoadMessages(bool reset)
        {
            if (!opened) return;
            if (renderingMessages)
            {
                pendingDirectRefresh |= reset;
                return;
            }
            renderingMessages = true;
            var token = request.Token;
            int version = ++messagesVersion;
            try
            {
                if (conversation == Guid.Empty)
                {
                    var lookup = await Get<ConversationLookup>("conversation?player=" + recipient.ToString("D"), token);
                    token.ThrowIfCancellationRequested();
                    conversation = lookup.conversationId ?? Guid.Empty;
                }
                if (conversation != Guid.Empty)
                {
                    var page = await Get<Page<Message>>($"conversations/{conversation:D}/messages" + Cursor(reset ? null : next), token);
                    token.ThrowIfCancellationRequested();
                    if (reset) messages.Clear();
                    foreach (var message in page.items)
                        if (!messages.Any(previous => previous.messageId == message.messageId)) messages.Add(message);
                    next = page.nextBefore;
                    if (messages.Count > 0)
                        await Send<ApiMessage>("PUT", $"conversations/{conversation:D}/read", new { Through = messages.Max(message => message.messageId) }, token);
                }
                if (token.IsCancellationRequested) return;
                Clear(rows);
                if (next.HasValue) More(() => _ = LoadMessages(false));
                foreach (var message in messages.OrderBy(message => message.messageId))
                    AddMessage(message.senderId == Self ? "Bạn" : recipientName, message.body);
                if (messages.Count == 0) Info("Bắt đầu cuộc trò chuyện.");
                LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
                scroll.verticalNormalizedPosition = reset ? 0 : 1;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!token.IsCancellationRequested && this) feedback.text = error.Message; }
            finally
            {
                if (!token.IsCancellationRequested && version == messagesVersion)
                {
                    renderingMessages = false;
                    if (pendingDirectRefresh)
                    {
                        pendingDirectRefresh = false;
                        _ = LoadMessages(true);
                    }
                }
            }
        }
        Task SendDirect() => Run(async token =>
        {
            var text = entry.text.Trim();
            if (text.Length == 0) return;
            if (whispers)
            {
                if (live == null) throw new InvalidOperationException("Đang kết nối dịch vụ thì thầm.");
                await live.Invoke("Whisper", recipient, text);
                if (token.IsCancellationRequested) return;
                AddWhisper(new Whisper { senderId = Self, sender = "Bạn", body = text, recipientId = recipient });
                RenderWhispers();
            }
            else
            {
                if (messageId == Guid.Empty || text != messageBody)
                {
                    messageId = Guid.NewGuid();
                    messageBody = text;
                }
                var receipt = await Send<Message>("POST", "messages", new { PlayerId = recipient, ClientMessageId = messageId, Body = text }, token);
                conversation = receipt.conversationId;
                messageId = Guid.Empty;
                messageBody = null;
                await LoadMessages(true);
            }
            if (!token.IsCancellationRequested) entry.text = "";
        });
        void RenderWhispers()
        {
            Clear(rows);
            var thread = whispersReceived.Where(message => message.senderId == recipient || message.recipientId == recipient).ToArray();
            if (thread.Length == 0) Info("Thì thầm chỉ gửi khi người nhận đang ở sảnh.");
            foreach (var message in thread) AddMessage(message.sender, message.body);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
            scroll.verticalNormalizedPosition = 0;
        }
        void AddMessage(string sender, string body)
        {
            var row = Row("Message", BodyHeight(body, 110) + 57);
            Label("Sender", row, sender, 18, Mint, .02f,.60f,.98f,.98f);
            Label("Body", row, body, 19, White, .02f,.03f,.97f,.62f);
        }
    }
}
#endif
