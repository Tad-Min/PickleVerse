# Nâng cấp giao diện đại sảnh và âm nhạc — 11/10/2026

## Luồng chơi đã sửa

**Đại sảnh → Chơi ngay → Chọn chế độ** cho mọi người chơi. Nút Play không kiểm tra cờ tutorial để chuyển scene nữa.

Màn chọn chế độ có một hàng **TUTORIAL / HƯỚNG DẪN CƠ BẢN** với icon sách, mô tả và mũi tên. Chọn hàng này mới mở tutorial. Hàng hướng dẫn ẩn khi mở trang cấu hình đấu bot và hiện lại khi quay về chọn chế độ.

Luồng tutorial tự xuất hiện khi bắt đầu mở app hoặc khi cài lần đầu vẫn dành cho phần khởi động/onboarding về sau, chưa được làm trong thay đổi này.

## Quyết định UI/UX

Các ảnh tham khảo được dùng để nghiên cứu bố cục và thứ bậc thông tin; icon, shader và bố cục triển khai là của PickleVerse.

| Tham khảo | Quan sát thiết kế | Áp dụng vào PickleVerse |
|---|---|---|
| [Honkai: Star Rail — Phonograph](https://www.gameleap.com/articles/hope-is-the-thing-with-feathers-location-how-to-get-in-honkai-star-rail) | Khung tối, điểm nhấn sáng, artwork trung tâm và danh sách nhạc rõ trạng thái chọn | Phòng nghe với đĩa lớn, vòng trang trí, danh sách có số thứ tự/thời lượng, thông tin tách khỏi artwork đang xoay |
| [Haikyu!! FLY HIGH — giao diện đại sảnh](https://gamingonphone.com/reviews/haikyu-fly-high-game-review/) | Nhân vật là trọng tâm, tiền tệ ở trên, các chức năng quanh rìa; [trang game chính thức](https://haikyu-flyhigh.garena.com/en/) cung cấp bối cảnh phong cách anime/thể thao | Giữ nữ 3D và sân nhìn rõ; hero card gọn bên trái; nav có icon ở mép dưới; tiền tệ tách hai khung ở trên |
| [Palworld — ảnh UI inventory](https://gamesdb.launchbox-app.com/games/images/389908-palworld), [trang phát hành](https://store.steampowered.com/app/1623730/Palworld/) | Các tab dùng nhãn/icon rõ, dữ liệu số có vị trí ổn định, panel tối tách chức năng | Nav có trạng thái chọn, phản hồi hover/focus; số dư rõ; ký hiệu tiền tệ ở cuối khung |

Đây là đánh giá thiết kế từ ảnh tham khảo, không phải cam kết sao chép hay đánh giá chất lượng tương đương các game đó.

## Giao diện mới

- **Thanh trên:** nền chuyển sắc xanh navy, profile có vòng trang trí, brand ở giữa; Tokenpad và Elite có sắc nhận diện riêng, nhãn nhỏ và số dư nổi bật. Logo ở bên phải vẫn có sheen mỗi giây.
- **Thanh dưới:** sáu mục hiện có giữ liên kết, thêm icon vector, viền chọn và phản hồi hover/focus nhẹ. Hit area không co giãn theo hiệu ứng. Các mục chưa hoạt động giữ trạng thái hiện có.
- **Đại sảnh:** thẻ “Vào sân. Tỏa sáng.” và nút Chơi ngay màu champagne; vẫn thấy rõ nhân vật, vợt/bóng trên sân và nhãn bối cảnh ngày/đêm.
- **Widget nhạc:** chỉ ở Menu, gọn bên trái phía trên nav. Hiện đĩa, trạng thái, tên bài/nghệ sĩ và mũi tên mở. Chữ chỉ chạy khi dài hơn khung.
- **Phòng âm nhạc:** danh sách có artwork, số thứ tự và thời lượng; đĩa lớn xoay ở trung tâm; tên bài/nghệ sĩ đứng yên; thể loại và cài đặt âm lượng ở bên phải; thanh tiến độ và nút trước/phát/tạm dừng/sau ở dưới.
- **Shop/Collection/ModeSelection:** widget, canvas nhạc và raycaster nhạc đều ẩn. Nhạc tiếp tục cùng player; không tạo player mới hoặc khởi động lại bài khi đổi trang. Trận đấu vẫn không có nhạc nền.

## Nền móng và phạm vi

Giao diện dùng uGUI/TMP hiện có. Scene, prefab và material được sửa qua Unity Editor API, giữ button/listener và GUID của prefab. `ArenaGlass.shader` tạo nền chuyển sắc và đường viền cắt góc. `UiLineIcon` tạo icon vector có CanvasRenderer; `UiButtonMotion` chỉ làm phản hồi trên phần trang trí, không thay vùng bấm.

Cập nhật cùng ngày: viền đĩa được căn theo vùng artwork thực sự nhìn thấy, và luồng Menu ↔ ModeSelection dùng lại scene đại sảnh đã tải. Chi tiết và số đo trong [Menu_Mode_Return_Optimization.md](Menu_Mode_Return_Optimization.md).

Các material dùng chung nằm trong `Assets/FrontEndContent/AnimeUI`. Không thêm camera hoặc RenderTexture chạy nền để tạo hiệu ứng UI. Tiền tệ vẫn lấy từ `WalletBar` và hệ thống economy hiện có; không reset ví hoặc thêm nạp tiền. `MusicScenePolicy.Allows` quản lý phát nhạc, `ShowsWidget` quản lý UI riêng.

Helper `My project/Tools/AnimeUiRefresh.cs` có thể áp dụng lại style, tạo bản sao scene trước lần chạy đầu trong `UnityWork/AnimeUI/Before`. Không chạy helper MusicSetup/HomeLobbySetup cũ để ghi đè style mới.

## Kiểm chứng

- **128/128 EditMode tests đạt**, gồm 7 ca mới kiểm tra chỉ Menu hiện widget.
- **21/21 kiểm tra UI runtime:** Play cho người mới đi đúng chọn chế độ; tutorial được chọn chủ động và ẩn trong cấu hình bot; raycast chạm đúng Play; modal nhạc chặn nút phía sau; playlist không nhân đôi; artwork/index đúng; widget ẩn không bắt input; tiền tệ và progress thật không đổi.
- **34/34 kiểm tra audio runtime:** đủ 4 bài có âm thanh đầu ra; pause/resume, seek, chuyển/lặp bài, âm lượng, Settings và âm thanh qua chuyển scene.
- **16/16 kiểm tra tutorial tương tác** và **5/5 kiểm tra Esc/ẩn widget/SFX** đạt.
- Kiểm tra ảnh đại sảnh/phòng nhạc ở **16:9 và 4:3**; xem lại hàng Tutorial và tiền tệ ở Shop. Shader không có lỗi biên dịch.

Ảnh và báo cáo đặt ở `UnityWork/AnimeUI`, ngoài Assets để không tăng dung lượng tài nguyên game. Kiểm chứng thực hiện trong Unity Editor trên PC; chưa phải kiểm thử bản build Android/iOS thật hoặc đo FPS trên thiết bị đó.
