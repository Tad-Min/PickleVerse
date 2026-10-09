# Đề xuất đồng bộ HUD từ BotMatch

Người dùng đã duyệt **đồng bộ toàn bộ UI** ngày 10/10/2026 và yêu cầu bỏ các giới hạn cũ về tỉ số/TechniqueBar. Đề xuất bên dưới ghi lại phạm vi cũ; triển khai cuối đã mở rộng sang toàn bộ HUD BotMatch. Xem `MultiplayerHudSync-20261010.md`.

Bản vá trước đó từng bị bộ duyệt tự động từ chối vì yêu cầu giữ UI. Yêu cầu mới của người dùng đã cho phép thay đổi HUD.

## Thay đổi đề xuất để duyệt

| Thành phần | Nguồn có sẵn trong BotMatch | Cách dùng ở multiplayer |
|---|---|---|
| Scoreboard, RefereeNotice, ServeClock | Match HUD | Cập nhật style từ nguồn vào các object hiện có. Tỉ số vẫn chỉ `0 : 0`. |
| RallyAnnouncement | Match HUD/RallyAnnouncement | Sao chép banner thông báo lỗi, kết thúc game, đổi sân và kết quả trận; dữ liệu do host gửi. |
| Settings | Match HUD/Settings | Sao chép nút Esc/cài đặt, dùng màn hình Settings có sẵn. Chặn input của người mở cài đặt trong khi trận online tiếp tục. |
| Leave, ReturnToRoom | Nút multiplayer hiện có | Giữ chức năng rời trận/về phòng; bố trí để tránh chồng nút Settings. |

TechniqueBar tiếp tục được bỏ theo yêu cầu trước đó. Các widget huấn luyện bot, độ khó bot và chơi lại với bot không được đưa vào sân online.

Phần sân, nhân vật, bóng, camera và gameplay được đồng bộ độc lập với đề xuất này.
