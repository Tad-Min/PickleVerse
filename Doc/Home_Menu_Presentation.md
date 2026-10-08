# Home Menu với sân mới và nhân vật nữ

Cập nhật ngày 08/10/2026. Scene: `My project/Assets/Scenes/HomeScene/Menu.unity`.

## Bối cảnh và nhân vật

Menu sử dụng bản sao hierarchy **Arena của BotMatch**, gồm sân mới, lưới/vạch sân, nền bao quanh, khán đài, hàng rào, bảng tên sân và bốn cột đèn. Các mesh sân/lưới dùng tài nguyên hiện có trong `Assets/LocalMatchContent/Court`; vật liệu/model được dùng chung với BotMatch. Các instance sân cũ và vợt rời ở Menu đã được bỏ; asset nguồn cũ được giữ cho những scene khác.

Nhân vật nữ dùng model/rig và profile chuyển động tóc/váy của BotMatch. Clip đúng theo đường dẫn yêu cầu là **PKV_Dance_Win.anim**, dài **13,5 giây**. Clip được lấy mẫu theo thời gian, lặp liên tục và có đoạn hòa tư thế 0,35 giây cuối để quay về tư thế mở đầu nhẹ hơn. Vị trí gốc được giữ tại khu vực trình diễn; không có điều khiển đấu trận trong Home.

Nhân vật nhảy với tay không. Vợt được tháo khỏi socket tay và đặt nằm phẳng trên sân cùng một quả bóng trong nhóm `Home Court Props`. Vợt cách vị trí đứng nhân vật khoảng 1,4 m, bóng khoảng 1,0 m; cả hai nằm trong vùng trống giữa card UI và nhân vật, nhìn rõ ở camera 16:9/4:3. Đã kiểm tra 40 mẫu của Dance Win: đạo cụ không di chuyển theo tay. Ảnh kiểm tra lưu ở `UnityWork/HomeProps/Images`.

Camera đặt nhân vật ở phía phải và giữ vùng trái cho nút hành động chính. Một spotlight nhẹ hỗ trợ mặt/trang phục, còn hệ thống ánh sáng sân lấy từ BotMatch. Menu có một camera và một EventSystem.

## Chu kỳ ngày và đêm

Home tự chuyển ngày → hoàng hôn → đêm → bình minh → ngày. Mặc định một chu kỳ **90 giây**, có thể chỉnh `cycleSeconds` trong component `HomeLobbyPresentation`.

| Thời gian trong chu kỳ | Giai đoạn |
|---|---|
| 0–19,8 s | Ban ngày |
| 19,8–37,8 s | Chuyển sang hoàng hôn/đêm |
| 37,8–64,8 s | Ban đêm |
| 64,8–82,8 s | Chuyển sang bình minh/ngày |
| 82,8–90 s | Ban ngày, rồi lặp |

Chỉ dùng hai panorama đã có: `Epic_BlueSunset_EquiRect_flat.png` và `AllSky_Night_MoonBurst Equirect.png`. Shader `HomePanoramicBlend` hòa hai texture; intensity của sun/bốn spotlight, ambient, fog và reflection thay đổi theo cùng trọng số. Ban ngày sun sáng, bốn đèn sân tắt; ban đêm sun tắt, bốn đèn sân sáng. Nhãn góc phải cho biết giai đoạn hiện tại.

Chu kỳ này thuộc riêng Menu. Khi đi qua ModeSelection, lựa chọn ngày/đêm của trận BotMatch vẫn được áp dụng độc lập. Đã kiểm tra Home đang ban đêm nhưng chọn trận ban ngày: BotMatch dùng đúng sky ban ngày, sun và mức khó đã chọn.

## UI và UX

- Giữ CanvasHome cùng các nút/tham chiếu đang có, chỉnh bố cục tại chỗ.
- Header gọn với tên hiển thị prototype, thương hiệu và trạng thái bầu trời. Bỏ số tiền/level giả khỏi phần hiển thị Home.
- Welcome card bên trái có lời chào ngắn, mô tả và nút **Chơi ngay** nổi bật. Nút này giữ listener Play hiện có và tiếp tục mở ModeSelection.
- Sáu tab phụ nằm dưới; Trang chủ được nhấn màu. Những tab chưa có callback triển khai được đặt trạng thái chưa sẵn sàng, có thông báo chung để tránh bấm nhưng không nhận phản hồi.
- Dùng font Vietnamese SDF và palette teal/lime đồng bộ với ModeSelection, nền tối hỗ trợ đọc chữ cả ngày lẫn đêm.
- Canvas scale theo màn hình và có safe area. Hình 3D để trống phần phải, tránh phủ UI lên nhân vật.

Tham khảo: [Fortnite custom lobbies](https://www.fortnite.com/news/introducing-custom-lobbies-for-games-in-fortnite) sử dụng bối cảnh lobby riêng với điều hướng nhất quán; [Rocket League September Update](https://www.rocketleague.com/news/rocket-league-patch-notes-september-update/?lang=en) tổ chức lại Main Menu để giảm số nút và nhóm tính năng rõ hơn. Cách đặt nhân vật và ưu tiên Chơi ngay ở đây là lựa chọn thiết kế riêng cho PickleVerse.

## Thành phần và kiểm tra

`Assets/Scripts/FrontEnd/HomeLobbyPresentation.cs` điều khiển animation, chu kỳ trời/ánh sáng, safe area và focus Play. Material/shader mới nằm trong `Assets/FrontEndContent`; chúng tham chiếu hai ảnh sky hiện hữu. Các helper `Tools/HomeLobbySetup.cs`, `HomeLobbyValidation.cs` và `HomeLobbyMatchTransition.cs` chạy qua Unity CLI, ngoài runtime Assets.

**19/19 kiểm tra Editor Play Mode đạt**, gồm: sân cũ đã bỏ, đủ Arena, đúng clip, tóc/váy khởi tạo, pose thay đổi, 48 mẫu chuyển động không NaN/Infinity, các giai đoạn sky/light, chu kỳ lặp, cập nhật tự động, một camera/EventSystem, active hierarchy không Missing Script, glyph/chữ vừa khung, raycast vào Play và Home ↔ ModeSelection. Kiểm tra riêng Home đêm → BotMatch ngày cũng đạt.

Ảnh ngày/đêm được đối chiếu ở 1280×720 và 960×720. Console không có lỗi biên dịch/runtime mới; các prefab UI cũ còn ẩn có cảnh báo Missing Script từ trước, không được dùng trong Home view này. Chưa kiểm tra build hoặc hiệu năng trên điện thoại thật.

Backup scene trước thay đổi: `UnityWork/HomeLobby/Before/Menu.unity`. Kết quả và ảnh: `UnityWork/HomeLobby/validation.json`, `match-transition.json`, `Images/`.
