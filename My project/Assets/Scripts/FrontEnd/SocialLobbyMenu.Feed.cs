#if !UNITY_SERVER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Replays;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using PickleVerse.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PickleVerse.FrontEnd
{
    internal sealed partial class SocialLobbyMenu
    {
        static readonly string[] Audiences = { "community", "private", "friends", "friends_except" };
        static readonly string[] AudienceNames = { "Cộng đồng", "Chỉ mình tôi", "Bạn bè", "Bạn bè ngoại trừ" };
        readonly HashSet<Guid> exclusions = new HashSet<Guid>();
        TMP_InputField caption;
        TMP_Text mediaLabel, replayLabel;
        Guid? mediaId, replayId;
        int audience;

        public void OpenFeed()
        {
            if (!Open(ScreenView.Feed, "Bảng tin")) return;
            Action("CreatePost", editor, "Viết bài", () => Compose(null), 0,0,.19f,1, true);
            Action("RefreshFeed", editor, "Làm mới", OpenFeed, .21f,0,.39f,1);
            _ = LoadFeed(true);
        }
        Task LoadFeed(bool reset) => Run(async token =>
        {
            if (reset) { Clear(rows); next = null; }
            var page = await Get<Page<Post>>("posts" + Cursor(next), token);
            foreach (var post in page.items) AddPost(post);
            if (reset && page.items.Length == 0) Info("Bảng tin chưa có bài đăng.");
            next = page.nextBefore;
            if (next.HasValue) More(() => _ = LoadFeed(false));
            if (reset) Top();
        });
        void AddPost(Post post)
        {
            string visibility = AudienceNames[Math.Max(0, Array.IndexOf(Audiences, post.audience))];
            var row = Row("Post" + post.postId, BodyHeight(post.body, 120) + 132);
            Label("Author", row, post.author, 23, White, .02f,.80f,.63f,.99f);
            Label("Audience", row, visibility + "  ·  " + post.createdAt.ToUniversalTime().AddHours(7).ToString("dd/MM HH:mm"), 16, Muted,
                .60f,.80f,.98f,.99f).alignment = TextAlignmentOptions.MidlineRight;
            Label("Body", row, post.body, 19, White, .02f,.29f,.98f,.80f);
            var like = Action("Like", row, LikeLabel(post), () => _ = Run(async token =>
            {
                var updated = await Send<Post>("PUT", $"posts/{post.postId}/like", new { Liked = !post.liked }, token);
                post.liked = updated.liked;
                post.likes = updated.likes;
                row.Find("Like").GetComponentInChildren<TMP_Text>().text = LikeLabel(post);
            }), .02f,.04f,.16f,.26f);
            Action("Comments", row, $"Bình luận · {post.comments}", () => OpenComments(post), .17f,.04f,.35f,.26f);
            if (post.hasVideo)
                Action("Video", row, "Xem video", () => _ = Run(token => PlayVideo(post, token)), .36f,.04f,.49f,.26f);
            if (post.replayMatchId.HasValue)
                Action("Replay", row, "Xem replay", () => PlayReplay(post), .50f,.04f,.64f,.26f);
            if (post.authorId == Self)
            {
                Action("Edit", row, "Sửa", () => Compose(post), .72f,.04f,.83f,.26f);
                Action("Delete", row, "Xóa", () => ConfirmDelete(post), .84f,.04f,.98f,.26f);
            }
        }
        static string LikeLabel(Post post) => (post.liked ? "Đã thích" : "Thích") + " · " + post.likes;
        static float BodyHeight(string value, int columns) =>
            23 * Math.Max(1, (value ?? "").Split('\n').Sum(line => Math.Max(1, (line.Length + columns - 1) / columns)));
        void ConfirmDelete(Post post)
        {
            Clear(editor);
            Label("ConfirmDelete", editor, "Xóa bài đăng này?", 20, Gold, 0,0,.49f,1);
            Action("Delete", editor, "Xóa bài", () => _ = Run(async token =>
            {
                await Send<ApiMessage>("DELETE", $"posts/{post.postId}", null, token);
                OpenFeed();
            }), .56f,0,.76f,1, true);
            Action("Keep", editor, "Giữ bài", OpenFeed, .78f,0,.98f,1);
        }

        void Compose(Post post)
        {
            if (!Open(ScreenView.Compose, post == null ? "Viết bài" : "Sửa bài")) return;
            selected = post;
            mediaId = post?.mediaId;
            replayId = post?.replayMatchId;
            audience = post == null ? 0 : Math.Max(0, Array.IndexOf(Audiences, post.audience));
            exclusions.Clear();
            exclusions.UnionWith(post?.exclude ?? Array.Empty<Guid>());
            var writing = Row("Writing", 230);
            caption = RuntimeForms.Input(writing, "Nội dung bài đăng", new Vector2(.02f,.08f), new Vector2(.98f,.93f), 2000);
            var visibility = Row("Visibility", 82);
            Label("Label", visibility, "Ai có thể xem", 20, White, .02f,0,.22f,1);
            var audienceButton = Action("Audience", visibility, AudienceNames[audience], () => { }, .23f,.12f,.52f,.88f);
            var exceptButton = Action("Exclusions", visibility, "Chọn người ngoại trừ", () => _ = Run(ChooseExclusions), .54f,.12f,.84f,.88f);
            exceptButton.gameObject.SetActive(audience == 3);
            audienceButton.onClick.AddListener(() =>
            {
                audience = (audience + 1) % Audiences.Length;
                audienceButton.GetComponentInChildren<TMP_Text>().text = AudienceNames[audience];
                exceptButton.gameObject.SetActive(audience == 3);
            });
            if (post != null) caption.text = post.body;
            var media = Row("Media", 83);
            mediaLabel = Label("MediaStatus", media, mediaId.HasValue ? "Đã đính kèm MP4" : "MP4 tối đa 250 MB · 15 phút", 19, Muted, .02f,.07f,.47f,.93f);
            Action("PickVideo", media, "Chọn MP4", PickVideo, .48f,.12f,.72f,.88f);
            Action("RemoveVideo", media, "Bỏ video", () => { mediaId = null; mediaLabel.text = "MP4 tối đa 250 MB · 15 phút"; }, .74f,.12f,.98f,.88f);
            var replay = Row("Replay", 83);
            replayLabel = Label("ReplayStatus", replay, replayId.HasValue ? "Đã đính kèm replay" : "Replay trận đã hoàn tất", 19, Muted, .02f,.07f,.47f,.93f);
            Action("PickReplay", replay, "Chọn replay", () => _ = Run(ChooseReplay), .48f,.12f,.72f,.88f);
            Action("RemoveReplay", replay, "Bỏ replay", () => { replayId = null; replayLabel.text = "Replay trận đã hoàn tất"; }, .74f,.12f,.98f,.88f);
            Action("SavePost", editor, post == null ? "Đăng bài" : "Lưu bài", () => _ = Run(SavePost), 0,0,.20f,1, true);
            Action("Back", editor, "Quay lại", OpenFeed, .22f,0,.40f,1);
            Top();
        }

        Task ChooseReplay(CancellationToken token) => ShowPicker(async (list, done) =>
        {
            var replays = await Account.SendAuthorizedAsync<ReplayMetadata[]>("GET", "api/Game/replays", cancellation: token);
            foreach (var replay in replays.Where(replay => replay.complete).OrderByDescending(replay => replay.startedUtcTicks).Take(50))
            {
                string date = new DateTime(replay.startedUtcTicks, DateTimeKind.Utc).AddHours(7).ToString("dd/MM/yyyy HH:mm");
                string names = string.Join(" · ", replay.actors.Select(actor => actor.name));
                PickerItem(list, date + "  •  " + names, () =>
                {
                    replayId = Guid.Parse(replay.matchId);
                    replayLabel.text = "Replay · " + date;
                    done();
                });
            }
            if (!replays.Any(replay => replay.complete)) PickerItem(list, "Chưa có replay hoàn tất", () => { });
        }, "Chọn replay của bạn");

        Task ChooseExclusions(CancellationToken token) => ShowPicker(async (list, _) =>
        {
            int skip = 0;
            bool more;
            do
            {
                var page = await Account.SendAuthorizedAsync<FriendPage>("GET", $"api/Game/social?view=friends&offset={skip}", cancellation: token);
                foreach (var friend in page.players.Where(friend => friend.relationship == "friend"))
                    PickerItem(list, friend.name, () => { }, friend.id);
                skip += page.players.Length;
                more = page.hasMore;
            } while (more && skip > 0);
            if (list.childCount == 0) PickerItem(list, "Danh sách bạn bè trống", () => { });
        }, "Ẩn bài với những người đã chọn");

        async Task ShowPicker(Func<RectTransform, Action, Task> fill, string title)
        {
            var backdrop = ui.Panel("Picker", window, new Color(0,0,0,.65f));
            backdrop.GetComponent<Image>().raycastTarget = true;
            var overlay = ui.Panel("PickerPanel", backdrop, Ink, new Vector2(.16f,.17f), new Vector2(.84f,.83f));
            Label("Title", overlay, title, 25, White, .04f,.87f,.80f,.98f);
            void Done() { if (backdrop) Destroy(backdrop.gameObject); }
            Action("Done", overlay, "Xong", Done, .82f,.87f,.97f,.98f, true);
            var viewport = ui.Panel("Viewport", overlay, Surface, new Vector2(.035f,.045f), new Vector2(.965f,.85f));
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var list = ui.Rect("Content", viewport, new Vector2(0,1), Vector2.one);
            list.pivot = new Vector2(.5f,1);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var control = overlay.gameObject.AddComponent<ScrollRect>();
            control.content = list;
            control.viewport = viewport;
            control.horizontal = false;
            control.movementType = ScrollRect.MovementType.Clamped;
            try { await fill(list, Done); }
            catch { Done(); throw; }
        }
        void PickerItem(RectTransform list, string name, Action select, Guid? friend = null)
        {
            if (!list) return;
            var button = Action("Choice" + list.childCount, list, friend.HasValue && exclusions.Contains(friend.Value) ? "✓ " + name : name, () => select(), 0,0,1,1);
            button.GetComponentInChildren<TMP_Text>().richText = false;
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 57;
            if (friend.HasValue)
            {
                button.onClick.AddListener(() =>
                {
                    if (!exclusions.Remove(friend.Value))
                    {
                        if (exclusions.Count >= 200) return;
                        exclusions.Add(friend.Value);
                    }
                    button.GetComponentInChildren<TMP_Text>().text = exclusions.Contains(friend.Value) ? "✓ " + name : name;
                });
            }
        }

        bool pickingVideo;
        void PickVideo()
        {
            if (submitting || pickingVideo) return;
#if UNITY_EDITOR
            UploadPickedVideo(UnityEditor.EditorUtility.OpenFilePanel("Chọn video MP4", "", "mp4"));
#elif UNITY_STANDALONE_WIN
            _ = PickWindowsVideo();
#else
            feedback.text = "Tải video hiện có trên bản Windows. Bản Android chưa hỗ trợ chọn tệp video.";
#endif
        }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        async Task PickWindowsVideo()
        {
            pickingVideo = true;
            try
            {
                string path = await WindowsFilePicker.PickAsync("Chọn video MP4", "Video MP4", "mp4");
                if (this) UploadPickedVideo(path);
            }
            catch (Exception)
            {
                if (this) feedback.text = "Không mở được hộp chọn tệp video.";
            }
            finally
            {
                pickingVideo = false;
            }
        }
#endif
        void UploadPickedVideo(string path)
        {
            if (string.IsNullOrEmpty(path) || submitting) return;
            _ = Run(async token =>
            {
                if (new FileInfo(path).Length > 250_000_000)
                    throw new InvalidOperationException("Video vượt quá 250 MB.");
                string name = Path.GetFileName(path);
                var progress = new Progress<float>(value =>
                {
                    if (!token.IsCancellationRequested && mediaLabel)
                        mediaLabel.text = value >= .999f ? "Đang kiểm tra video…" : $"Đang tải {value:P0}";
                });
                mediaLabel.text = "Đang tải 0%";
                mediaId = await SocialTransport.UploadVideo(path, progress, token);
                mediaLabel.text = name;
            });
        }
        async Task SavePost(CancellationToken token)
        {
            var body = new { Body = caption.text, Audience = Audiences[audience], MediaId = mediaId, ReplayMatchId = replayId,
                Exclude = audience == 3 ? exclusions.ToArray() : Array.Empty<Guid>() };
            await Send<Post>(selected == null ? "POST" : "PUT", selected == null ? "posts" : "posts/" + selected.postId, body, token);
            OpenFeed();
        }
        void OpenComments(Post post)
        {
            if (!Open(ScreenView.Comments, "Bình luận")) return;
            selected = post;
            entry = RuntimeForms.Input(editor, "Viết bình luận", new Vector2(0,0), new Vector2(.68f,1), 1000);
            Action("Send", editor, "Gửi", () => _ = Run(async token =>
            {
                await Send<Comment>("POST", $"posts/{post.postId}/comments", new { Body = entry.text }, token);
                OpenComments(post);
            }), .70f,0,.83f,1,true);
            Action("Back", editor, "Quay lại", OpenFeed, .85f,0,1,1);
            _ = LoadComments(true);
        }
        Task LoadComments(bool reset) => Run(async token =>
        {
            var post = selected;
            var page = await Get<Page<Comment>>($"posts/{post.postId}/comments" + Cursor(next), token);
            foreach (var item in page.items)
            {
                var row = Row("Comment", BodyHeight(item.body, 110) + 63);
                Label("Author", row, item.author, 20, Mint, .02f,.55f,.78f,.99f);
                Label("Body", row, item.body, 17, White, .02f,.05f,.93f,.56f);
                if (item.authorId == Self || post.authorId == Self)
                    Action("Delete", row, "Xóa", () => _ = Run(async ct =>
                    {
                        await Send<ApiMessage>("DELETE", $"posts/{post.postId}/comments/{item.commentId}", null, ct);
                        OpenComments(post);
                    }), .86f,.62f,.98f,.94f);
            }
            next = page.nextBefore;
            if (next.HasValue) More(() => _ = LoadComments(false));
            if (reset && page.items.Length == 0) Info("Chưa có bình luận.");
            if (reset) Top();
        });

        void PlayReplay(Post post)
        {
            Close();
            ReplayLaunch.Select(Self.ToString("N"), post.replayMatchId.Value.ToString("N"), () =>
            {
                LobbyNavigation.ReturnToLobby();
                if (this) StartCoroutine(RestoreFeed());
            }, post.postId);
            LobbyNavigation.OpenReplay();
        }
        System.Collections.IEnumerator RestoreFeed()
        {
            while (LobbyNavigation.IsBusy) yield return null;
            OpenFeed();
        }

        async Task PlayVideo(Post post, CancellationToken token)
        {
            var grant = await Send<Playback>("POST", $"posts/{post.postId}/video-playback", null, token);
            StopVideo();
            mediaPanel = ui.Panel("Video", window, new Color(0,0,0,.76f));
            mediaPanel.GetComponent<Image>().raycastTarget = true;
            var view = ui.Panel("Player", mediaPanel, Ink, new Vector2(.14f,.15f), new Vector2(.86f,.86f));
            var surface = ui.Rect("Surface", view, new Vector2(.03f,.12f), new Vector2(.97f,.93f));
            surface.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            videoSurface = new RenderTexture(1280, 720, 0);
            surface.gameObject.AddComponent<RawImage>().texture = videoSurface;
            player = mediaPanel.gameObject.AddComponent<VideoPlayer>();
            player.source = VideoSource.Url;
            player.url = SocialTransport.Url(grant.path);
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = videoSurface;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.aspectRatio = VideoAspectRatio.FitInside;
            player.playOnAwake = false;
            player.errorReceived += (_, __) => { StopVideo(); if (feedback) feedback.text = "Không phát được video. Bài có thể đã đổi quyền xem."; };
            player.prepareCompleted += source => source.Play();
            Action("PlayPause", view, "Phát / Dừng", () => { if (player.isPlaying) player.Pause(); else player.Play(); }, .04f,.015f,.28f,.09f);
            Action("CloseVideo", view, "Đóng video", StopVideo, .72f,.015f,.96f,.09f);
            player.Prepare();
        }
        void StopVideo()
        {
            if (player) player.Stop();
            if (mediaPanel)
            {
                mediaPanel.gameObject.SetActive(false);
                mediaPanel.SetParent(null, false);
                Destroy(mediaPanel.gameObject);
            }
            if (videoSurface) { videoSurface.Release(); Destroy(videoSurface); }
            player = null;
            mediaPanel = null;
            videoSurface = null;
        }
    }
}
#endif
