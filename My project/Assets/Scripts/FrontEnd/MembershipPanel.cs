#if !UNITY_SERVER
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Progression;
using PickleVerse.Services.Api;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PickleVerse.FrontEnd
{
    public sealed class MembershipPanel : MonoBehaviour
    {
        static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_ANDROID
        const bool Supported = true;
#else
        const bool Supported = false;
#endif
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        ProgressionRuntime runtime;
        TMP_Text price, bonus, notice, actionLabel;
        UnityEngine.UI.Button action;
        RectTransform safeArea, card;
        GameObject previousFocus;
        bool busy, activated;
        string error;

        public static void Show(bool locked = false)
        {
            if (FrontEndModalInput.IsOpen || SceneLoading.IsBlocking || SettingsOverlay.IsOpen) return;
            var canvas = ArenaForms.Canvas("Membership Offer", 2000);
            var panel = canvas.AddComponent<MembershipPanel>();
            if (!FrontEndModalInput.TryAcquire(panel)) { Destroy(canvas); return; }
            panel.Build(locked);
        }

        void Build(bool locked)
        {
            runtime = ProgressionRuntime.Instance;
            previousFocus = EventSystem.current?.currentSelectedGameObject;
            var cover = ArenaForms.Cover("Backdrop", transform);
            safeArea = ArenaForms.Rect("Safe Area", cover);
            safeArea.gameObject.AddComponent<UiSafeArea>();
            card = ArenaForms.Surface("Membership Card", safeArea);
            ArenaForms.Pin(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(760, 620));
            Fit();

            var heading = ArenaForms.Label("Heading", card, "GÓI THÀNH VIÊN", 30, ScreenUiTheme.Gold);
            ArenaForms.Fill(heading.rectTransform, new Vector2(0, .88f), Vector2.one, 34, 0, 96, 10);
            var close = ArenaForms.IconButton("Close", card, ArenaGlyph.Close, () => Destroy(gameObject));
            ArenaForms.Pin((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-18, -16), new Vector2(58, 58));

            if (locked)
            {
                var explanation = ArenaForms.Label("Explanation", card, ProgressionRuntime.MembershipRequiredMessage,
                    22, ScreenUiTheme.Text, TextAlignmentOptions.Left, true);
                ArenaForms.Fill(explanation.rectTransform, new Vector2(0, .77f), new Vector2(1, .88f), 34, 0, 34);
            }
            price = ArenaForms.Label("Price", card, "Đang tải giá…", 52, ScreenUiTheme.Gold);
            ArenaForms.Fill(price.rectTransform, new Vector2(0, .61f), new Vector2(1, locked ? .77f : .86f), 34, 0, 34);
            var duration = ArenaForms.Label("Duration", card, "Thanh toán một lần · Vĩnh viễn", 20, ScreenUiTheme.Muted);
            ArenaForms.Fill(duration.rectTransform, new Vector2(0, .54f), new Vector2(1, .62f), 34, 0, 34);

            var benefits = ArenaForms.Inset("Benefits", card);
            ArenaForms.Fill(benefits, new Vector2(0, .23f), new Vector2(1, .53f), 34, 3, 34, 3);
            Benefit(benefits, "Mở độ khó Tự động khi đấu với bot", 2);
            Benefit(benefits, "Đấu online và tạo hoặc tham gia phòng riêng", 1);
            bonus = Benefit(benefits, "Nhận Elite Token khi kích hoạt", 0);

            notice = ArenaForms.Label("Status", card, "", 20, ScreenUiTheme.Muted, TextAlignmentOptions.Center, true);
            ArenaForms.Fill(notice.rectTransform, new Vector2(0, .13f), new Vector2(1, .23f), 34, 1, 34, 1);
            action = ArenaForms.Action("Buy Now", card, "MUA NGAY", true, () => _ = Buy(), ArenaGlyph.Arrow, 24);
            ArenaForms.Fill((RectTransform)action.transform, Vector2.zero, new Vector2(1, .13f), 34, 18, 34, 6);
            actionLabel = action.GetComponentInChildren<TMP_Text>();
            var closeNav = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnDown = action };
            var buyNav = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = close };
            close.navigation = closeNav;
            action.navigation = buyNav;
            ProgressionRuntime.MembershipChanged += Render;
            ProgressionRuntime.Changed += Render;
            EventSystem.current?.SetSelectedGameObject(close.gameObject);
            Render();
            if (runtime && runtime.IsAuthenticated) _ = Read();
        }

        static TMP_Text Benefit(RectTransform parent, string text, int row)
        {
            float bottom = row / 3f, top = (row + 1) / 3f;
            var icon = ArenaForms.Icon("Included", parent, ArenaGlyph.Check, ScreenUiTheme.Mint);
            ArenaForms.Pin(icon.rectTransform, new Vector2(0, (bottom + top) * .5f),
                new Vector2(0, .5f), new Vector2(18, 0), new Vector2(23, 23));
            var label = ArenaForms.Label("Benefit", parent, text, 22, ScreenUiTheme.Text, TextAlignmentOptions.Left, true);
            ArenaForms.Fill(label.rectTransform, new Vector2(0, bottom), new Vector2(1, top), 52, 0, 12);
            return label;
        }

        void Render()
        {
            if (!this || !notice || !action) return;
            var offer = runtime ? runtime.CurrentMembership : null;
            bool signedIn = runtime && runtime.IsAuthenticated;
            bool member = runtime && runtime.HasMembership;
            if (offer != null)
            {
                price.text = offer.priceVnd.ToString("N0", Vietnamese) + " đ";
                bonus.text = "+" + offer.eliteTokenBonus.ToString("N0", Vietnamese) + " Elite Token khi kích hoạt";
            }
            else
            {
                price.text = signedIn && error == null ? "Đang tải giá…" : "—";
                bonus.text = "Nhận Elite Token khi kích hoạt";
            }
            bool pending = offer?.pendingCheckout?.IsOpen == true;
            actionLabel.text = member ? "ĐÃ KÍCH HOẠT" : busy ? "ĐANG XỬ LÝ…" : offer == null && error != null ? "TẢI LẠI" :
                pending ? "MỞ THANH TOÁN" : "MUA NGAY";
            action.interactable = signedIn && Supported && !busy && !member && (offer?.checkoutEnabled == true || offer == null && error != null);
            notice.color = error != null ? ArenaForms.Error : member ? ArenaForms.Success : ScreenUiTheme.Muted;
            notice.text = member ? (activated ? "Thanh toán thành công. Gói thành viên đã được kích hoạt." : "Gói thành viên đang hoạt động.") :
                error ?? (!signedIn ? "Đăng nhập để mua gói thành viên." :
                !Supported ? "Thanh toán trên Windows hoặc bản Android cài APK trực tiếp." :
                offer?.active == true ? "Đang cập nhật quyền thành viên và ví…" :
                offer == null ? "Đang lấy thông tin gói thành viên…" :
                !offer.checkoutEnabled ? "Thanh toán PayOS hiện chưa khả dụng." :
                pending ? "Hoàn tất tại PayOS rồi quay lại game để cập nhật." : "");
        }

        async Task Read()
        {
            busy = true;
            error = null;
            Render();
            try { await runtime.ReadMembershipAsync(lifetime.Token); }
            catch (OperationCanceledException) { }
            catch (Exception failure) { error = failure.Message; }
            finally { busy = false; Render(); }
        }

        async Task Buy()
        {
            if (busy || !Supported || !runtime || !runtime.IsAuthenticated || runtime.HasMembership) return;
            if (runtime.CurrentMembership == null) { await Read(); return; }
            busy = true;
            error = null;
            Render();
            try
            {
                var offer = await runtime.CheckoutMembershipAsync(lifetime.Token);
                if (!this) return;
                if (offer.active) return;
                var checkout = offer.pendingCheckout;
                if (checkout == null || !checkout.IsOpen || !checkout.HasSafeUrl)
                    throw new InvalidOperationException("Liên kết thanh toán PayOS chưa sẵn sàng. Hãy thử lại.");
                activated = true;
                Application.OpenURL(checkout.checkoutUrl);
            }
            catch (OperationCanceledException) { }
            catch (Exception failure) { error = failure.Message; }
            finally { busy = false; Render(); }
        }

        void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Destroy(gameObject);
            else Fit();
        }

        void Fit()
        {
            if (!safeArea || safeArea.rect.width < 1 || safeArea.rect.height < 1) return;
            float scale = Mathf.Clamp01(Mathf.Min((safeArea.rect.width - 32) / 760, (safeArea.rect.height - 32) / 620));
            if (!Mathf.Approximately(card.localScale.x, scale)) card.localScale = Vector3.one * scale;
        }

        void OnDestroy()
        {
            ProgressionRuntime.MembershipChanged -= Render;
            ProgressionRuntime.Changed -= Render;
            lifetime.Cancel();
            lifetime.Dispose();
            if (!FrontEndModalInput.IsOwnedBy(this)) return;
            FrontEndModalInput.Release(this);
            if (previousFocus && previousFocus.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(previousFocus);
        }
    }

}
#endif
