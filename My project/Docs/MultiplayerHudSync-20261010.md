# HUD MultiplayerCourt từ BotMatch — 10/10/2026

Người dùng đã yêu cầu đồng bộ toàn bộ UI và bỏ giới hạn cũ về bảng điểm chỉ `0 : 0` / bỏ TechniqueBar.

## Thành phần đã đồng bộ

Toàn bộ 15 nhóm UI của BotMatch hiện có trong `MultiplayerCourt`: Scoreboard, RefereeNotice, ServeClock, TechniqueBar, Settings, Help, Touch, Timeout, Difficulty, TouchControls, ControlsHelp, MatchDialog, CinematicIntro, RallyAnnouncement, MatchResultActions. Giữ thêm Leave và ReturnToRoom phục vụ phiên online.

| Thành phần | Hành vi multiplayer |
|---|---|
| Scoreboard | Tên người chơi trong đội một người, ĐỘI A/B trong đội hai người; điểm, GAME hiện tại, số game thắng, Đấu đơn/Đấu đôi/Custom và số người hai đội. Giữ màu và bố cục từ BotMatch. |
| RallyAnnouncement | Banner có hiệu ứng vào/ra và màu theo loại thông báo: FAULT/OUT, hết game, đổi bên, time-out và kết quả. Dữ liệu lỗi/điểm do host gửi. |
| TechniqueBar | Hiện phím/combo đang giữ và tên cú đánh; sau nhả phím, hiển thị combo gắn với animation host đã chấp nhận. Combo bị bỏ qua khi animation trước chưa hồi phục không thay nhãn đang đánh. |
| Settings / Help | Dùng màn hình cài đặt có sẵn và hướng dẫn cùng style BotMatch. Chỉ chặn input người mở; toàn bộ trận online tiếp tục. Hướng dẫn cập nhật khi đổi bindings. |
| Touch / TouchControls | Bật/tắt joystick và tám nút kỹ thuật, dùng cùng TouchControl qua IMatchTouchInput. Cả nhân vật bot/local và multiplayer tiếp tục dùng chung xử lý cảm ứng. |
| Timeout / MatchDialog | Nút xin time-out gọi host; dialog hiện đội xin, thời gian còn lại, điểm và nút tiếp tục. Chỉ đội xin được kết thúc sớm. Dialog cũng hiện khi chờ kết nối lại. |
| Difficulty | Giữ vị trí/style, thay nội dung bot thành tên chế độ trận online. |
| CinematicIntro | Giữ overlay nguồn cho giai đoạn tải sân/chờ mọi người kết nối. Tự ẩn khi host bắt đầu trận; không thêm đếm ngược hoặc bước xác nhận, không chạy cinematic bot riêng. |
| MatchResultActions | Hiện số game thắng; VỀ PHÒNG giữ nhóm hiện có; CHỌN CHẾ ĐỘ rời phòng hiện tại và mở phòng 1vs1 để chọn lại. |

Nút rời trận trong Settings dùng lời xác nhận multiplayer; rời giữa trận có xử thua, host rời kết thúc phiên.

## Đồng bộ lại sau khi sửa BotMatch

Dừng Play, chọn **PickleVerse → Multiplayer → Sync all HUD from BotMatch**. Công cụ cập nhật object đã có, thêm object còn thiếu, remap reference và callback sang MultiplayerCourtUI, lưu scene và bảo đảm scene Settings nằm trong Build Profile. Không sửa YAML.

Bản sao scene trước khi đồng bộ nằm trong `Temp/MultiplayerHudSync/`. Tên các nút cảm ứng có dấu `/` được tìm theo tên trực tiếp, tránh Transform.Find hiểu thành đường dẫn. Công cụ đã được chạy hai lần liên tiếp và không sinh thêm object trùng.

## Xác minh

- **13 kiểm tra runtime đạt** trên Editor host + một client Windows qua Unity Services/Relay: scoreboard, giữ phím, combo bị bỏ qua, chọn/nhả combo, Help, touch, Settings, time-out/tiếp tục, banner lỗi host, game tiếp, kết quả trận và về phòng. Báo cáo: `MultiplayerHudRuntime-20261010.json`.
- Kiểm tra cấu trúc sau đồng bộ hai lần: **72 object, 9 control cảm ứng, 0 tên sibling trùng, 0 Missing Script**, reference banner đủ và scene Settings bật trong build. Báo cáo: `MultiplayerHudStructure-20261010.json`.
- Ảnh banner thực tế: `MultiplayerHud-Fault-20261010.png`.
- Build Windows cuối `build_4d33cf5fdfaf` hoàn tất lúc **02:16 ngày 10/10/2026**, **0 lỗi**, 542 cảnh báo. Kết quả: `MultiplayerHudBuild-20261010.json`; bản chạy tại `Builds/Multiplayer/PickleVerse.exe`.

Phép thử dùng một máy vật lý với hai phiên. Để kiểm tra HUD chuyển game/kết quả, bộ thử đặt điểm/game và kích hoạt lỗi trên host; không mô phỏng một trận thi đấu thủ công đầy đủ. Host được tạo trước khi công bố roster trong bộ thử để Editor kiểm tra dữ liệu host; luồng tạo/nhập phòng của ứng dụng giữ nguyên.

Client Windows trong phép thử dùng build HUD trước sửa nhãn combo/chống trùng; Editor chạy source cuối và đã kiểm tra hai sửa này. Scene cuối được kiểm tra cấu trúc trước build cuối. Tất cả máy nên thay nguyên thư mục Windows mới để thấy đầy đủ HUD.

Các tiến trình và phòng test do tôi tạo đã đóng. Chưa commit hoặc push.
