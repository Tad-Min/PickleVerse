# Cài đặt dùng chung của PickleVerse

Ngày triển khai và kiểm tra: 09/10/2026.

## Scene và cách mở

- Scene mới: `My project/Assets/Scenes/UI/Settings.unity`.
- Đại sảnh: mở từ nút **Setting** trong `Assets/Scenes/UI/Menu.unity`.
- Đấu bot: nút **Esc / Cài đặt** góc phải trên và phím **Esc** mở cùng scene. Nút Dừng cũ được thay bằng nút này.
- Settings được tải **additive** trên scene hiện tại. Trận đang chơi vẫn tồn tại; đóng Settings không tạo lại trận hoặc reset điểm.
- Trong đấu bot, mở Settings tự tạm dừng: giữ bóng, đồng hồ giao bóng, điều khiển và tiến trình cinematic mở đầu. Tab Tổng quan có **Tiếp tục trận** và **Rời trận về đại sảnh**.
- Tại Menu, Tổng quan có **Đăng xuất**; không hiện các thao tác của trận đấu.
- Khi đóng bình thường, hệ thống khôi phục trạng thái tạm dừng trước đó. Nút Tiếp tục trận chủ động tiếp tục trận.

Settings đã được thêm vào danh sách scene build chung và các Build Profile đang dùng danh sách scene riêng.

## Những chức năng đã hoạt động

| Nhóm | Tùy chọn | Hành vi thực tế |
|---|---|---|
| Tổng quan | Tiếp tục / rời trận | Giữ nguyên trận khi tiếp tục; xác nhận trước khi rời về Menu |
| Tổng quan | Đăng xuất | Gọi luồng đăng xuất Unity Authentication hiện có, trở về trạng thái khách ở đại sảnh |
| Âm thanh | Tổng / nhạc / hiệu ứng | Tổng áp dụng qua `AudioListener.volume`; Hiệu ứng điều chỉnh SFX ngay; Nhạc lưu mức để dùng sau |
| Đồ họa | Thấp / Cân bằng / Cao | Đổi render scale, MSAA và ngân sách shadow của URP |
| Đồ họa | Độ nét | Render scale từ 50% đến 125% |
| Đồ họa | FPS | Giới hạn 30 / 60 / 120; kết quả còn phụ thuộc thiết bị |
| Đồ họa | Khử răng cưa | Tắt / MSAA 2x / 4x |
| Đồ họa | Bóng đổ / VSync | Bật/tắt shadow; VSync dùng trên PC |
| Máy tính | Đổi phím | Đổi các hành động di chuyển, chạy, đánh, modifier và các phím phụ |
| Điện thoại | Bố trí HUD | Kéo vị trí joystick và tám nút đánh/modifier trên bản xem trước |
| Điện thoại | Kích thước / độ rõ | Điều chỉnh kích thước và độ trong suốt nút cảm ứng |
| Điện thoại | Joystick | Điều chỉnh vùng chết và ngưỡng kích hoạt chạy |
| Điện thoại | Đảo tay thuận | Đối xứng bố trí nút để dùng tay trái |

Các mức đồ họa mặc định:

| Mức | Render scale | MSAA | Khoảng cách shadow | Shadow chính / bổ sung |
|---|---:|---:|---:|---:|
| Thấp | 70% | Tắt | 12 m | 1024 / 512 |
| Cân bằng | 85% | 2x | 25 m | 2048 / 1024 |
| Cao | 100% | 4x | 45 m | 4096 / 2048 |

Khi tắt bóng đổ, khoảng cách shadow về 0. Hệ thống dùng một bản URP asset riêng trong runtime, không ghi các lựa chọn người dùng vào asset đồ họa gốc của dự án.

VSync trên PC có thể quyết định tốc độ khung hình thực tế theo màn hình. Giới hạn FPS không bảo đảm thiết bị đạt đúng mức đã chọn.

## Điều khiển và combo

Các phím mặc định vẫn giữ cách chơi hiện có: WASD di chuyển, Shift chạy, J Drive/giao bóng, K Dink, L Volley, U tăng lực, I đánh chéo. Drop = Drive + Dink; Smash = Drive + Volley; Reset = Dink + Volley.

Combo dựa trên **hành động**, không dựa vào ký tự cố định. Ví dụ đổi Drive từ J sang F thì Drop thành F + K, Smash thành F + L. Hướng dẫn và thông báo giao bóng dùng tên phím đang được lưu.

Danh sách 15 hàng trong trang máy tính có thanh cuộn: 14 hành động được gán lại, riêng Esc mở/đóng Settings được giữ cố định để luôn có đường thoát. Phím trùng với hành động khác bị từ chối và có giải thích. Esc cũng hủy thao tác đang chờ gán phím.

Trang điện thoại sửa bố trí nút của đấu bot. Nút combo cảm ứng vẫn phát cùng các hành động Drive/Dink/Volley như bàn phím; không tạo bộ luật đánh riêng. Bản xem trước dùng tọa độ chuẩn hóa để lưu vị trí và áp dụng lại theo màn hình.

## Lưu, hoàn tác và xác nhận

- **Áp dụng và lưu** ghi cài đặt vào PlayerPrefs JSON `PKV.Settings.v1` trên thiết bị.
- Đồ họa và âm lượng tổng có xem trước ngay khi chỉnh; **Hoàn tác** trả về giá trị đã lưu.
- Đổi phím/bố trí cảm ứng áp dụng vào gameplay sau khi lưu.
- Đóng khi có thay đổi chưa lưu sẽ hỏi trước khi bỏ thay đổi.
- Tiếp tục trận khi có thay đổi chưa lưu sẽ hỏi trước khi áp dụng.
- Có khôi phục toàn bộ mặc định và đặt lại riêng nhóm điều khiển.
- Các nút rời trận, đăng xuất và khôi phục toàn bộ có hộp xác nhận.
- HUD của scene bên dưới được che trong lúc mở Settings; EventSystem và camera gameplay được dùng lại để tránh nhận input vào hai giao diện.
- Layout dùng CanvasScaler và vùng an toàn của màn hình. Đã xem giao diện 16:9 và 4:3 trong Editor.

## Đăng xuất và phần âm thanh còn chờ

Đăng xuất nối vào `MultiplayerApp.SignOutFromLobbyAsync` và `PlayerSignOut.FromLobbyAsync`, không tạo một hệ thống tài khoản thứ hai. Luồng dọn room/match còn tồn tại trước khi gọi `AuthenticationService.Instance.SignOut()` và đưa header đại sảnh về **Khách**.

Theo cơ chế Unity Authentication, `SignOut()` mặc định giữ session token trên thiết bị. Điều này bảo toàn khả năng quay lại danh tính khách chưa liên kết. Không xóa tài khoản, token khách, dữ liệu tiến trình hoặc cài đặt. Phiên đăng nhập lại vẫn theo luồng Authentication hiện có của dự án; đây chưa phải tính năng chọn/chuyển tài khoản của backend tương lai.

SFX trận đấu đã được bổ sung ngày 09/10/2026. Thanh Hiệu ứng điều chỉnh nguồn SFX ngay cả khi xem trước, hoàn tác hoặc lưu; âm lượng Tổng tiếp tục điều khiển AudioListener. Nhạc nền chưa được triển khai, thanh Nhạc vẫn lưu mức để nối sau. Chi tiết tại `Match_SFX_Integration.md`.

## Nền móng cho các chế độ tiếp theo

Assembly `PickleVerse.Settings` độc lập với FrontEnd/LocalMatch/Multiplayer. Scene dùng chung giao tiếp `ISettingsHost` để biết ngữ cảnh và gọi tiếp tục/rời/đăng xuất. `SettingsLauncher` có thể gắn vào nút mở Settings của các scene mới.

Hiện đã nối đầy đủ tại **Menu và BotMatch** theo phạm vi ưu tiên. Online/Training chưa được tích hợp input rebinding và pause vào gameplay. Khi mở rộng cần:

1. Gắn launcher vào UI của chế độ mới và triển khai host cho ngữ cảnh đó.
2. Cho bộ điều khiển của chế độ đó đọc `GameControls` và cài đặt touch, thay vì hardcode phím.
3. Với online, thiết kế host để mở cài đặt chỉ chặn input cục bộ; không dừng đồng hồ hay simulation của các người chơi khác.
4. Nối nhạc nền vào mức âm lượng Nhạc đã lưu; các SFX trận hiện dùng nhóm nguồn riêng trong SfxPlayer.

Không thêm dịch vụ cloud, package mới hoặc thay đổi luật trận đấu trong tính năng này.

Các phần mã chính: `Assets/Scripts/Settings`, các tích hợp ở BotMatch/MatchHud/ShotInput/TouchControl/BotMatchIntro, HomeLobbyPresentation và MultiplayerApp.SignOut. `Tools/SettingsSetup.cs` dựng scene bằng API Unity; không chỉnh YAML scene thủ công.

## Kiểm chứng

- **28/28 kiểm tra tích hợp**: tải scene additive, pause bóng/đồng hồ, ngữ cảnh nút, camera/EventSystem, đồ họa, hoàn tác, lưu/đọc lại, remap combo, bố trí cảm ứng, tiếp tục/rời trận và mở từ Menu.
- **3/3 kiểm tra tương tác**: bắt phím F qua input thật của Settings, kéo bằng PointerEventData, đăng xuất trong trạng thái khách mà không khởi tạo phiên Authentication mới.
- Kiểm tra mở Settings giữa cinematic: thời gian intro đứng yên và tiếp tục sau khi đóng.
- **29/29 EditMode tests của LocalMatch** đạt: luật giao bóng, nảy hai lần, Kitchen, tính điểm/đổi sân và các combo.
- Kiểm tra hình ảnh các tab và tỷ lệ 16:9/4:3; scene Settings không có Missing Script.
- Kiểm thử không giữ lại cài đặt giả lập: profile ban đầu được khôi phục sau các bài kiểm tra.

Chưa kiểm tra trên thiết bị Android/iOS thật hoặc tài khoản UGS đang đăng nhập thật. Đã kiểm tra luồng đăng xuất khách; mã signed-in sử dụng API Unity Authentication hiện có.

Trong một lượt mở Play cuối, Editor gặp lỗi cấp phát bộ nhớ native và bị crash. Project được mở lại; tiếp tục vào BotMatch, mở Settings và chụp giao diện thành công. Đây là sự cố môi trường kiểm thử đã được ghi nhận, không phải kết quả test logic thất bại.

Kết quả máy đọc: `UnityWork/Settings/validation.json`, `interaction-validation.json`. Ảnh tham khảo: `UnityWork/Settings/Images`.

## Cơ sở thiết kế và tài liệu tham khảo

Từ `PickleVerse_Controls_Combo_Research.md` và proposal: giữ ba hành động đánh gốc, modifier và combo, có tùy biến cho PC/mobile; chuẩn bị cài đặt theo thiết bị trước khi hệ thống tài khoản/backend đầy đủ.

Thiết kế dùng nhóm cài đặt bên trái và nội dung một nhóm bên phải để dễ tìm; tách máy tính/điện thoại; luôn thấy nút đóng và lưu; thao tác rời trận có màu/câu chữ riêng. Đây là cách áp dụng cho PickleVerse, không sao chép giao diện của game khác.

- [Fortnite: chỉnh điều khiển PC](https://www.epicgames.com/help/c-34254770/c-38015632/a10326284): tách phần điều khiển khỏi âm thanh/đồ họa và cho gán phím theo hành động.
- [Fortnite mobile HUD layout](https://www.fortnite.com/news/getting-started---fortnite-for-mobile): tùy biến vị trí/kích thước nút cảm ứng, làm cơ sở cho bản xem trước kéo nút.
- [Epic: sidebar và game menu](https://dev.epicgames.com/documentation/fortnite/exploring-the-sidebar-and-game-menu-in-fortnite-creative?lang=en-US): truy cập cài đặt và thao tác phiên chơi theo ngữ cảnh.
- [Unity Authentication: session management](https://docs.unity.com/en-us/authentication/session-management): hành vi SignOut và session token của tài khoản khách.
