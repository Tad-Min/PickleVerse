# PickleVerse playtest log — 10/11/2026

Môi trường: Unity Editor 6000.6.2f1 (Windows, Game view 1280×720), project `D:/Github/PickleVerse/My project`, main @ 82f6a7a, git sạch. API client `http://tad-min.io.vn:8080` (`/health/ready` 200, ~70 ms). Điều khiển bằng Unity CLI 1.0.0-beta.17 (Pipeline). Ảnh trong thư mục `shots/` cạnh file này.

## Phiên 1 — offline (không đăng nhập)
| Bước | Kết quả | Ảnh |
|---|---|---|
| Mở Login | Form email/mật khẩu, quên mật khẩu, đăng ký, nút "Luyện tập ngoại tuyến" (do có profile cache) | 01 |
| Offline → Menu | Profile Huy, cấp 1, Đồng 100 Elo; CHƠI NGAY bị khóa offline | 02 |
| Tiến trình | 0/15 bài đạt; góp ý sau buổi chơi; thời gian luyện | 03 |
| Training "Giao bóng đúng ô" | Lời HLV, tiêu chí 6/8, thưởng 150 Tokenpad/5 XP | 04 |
| Giao bóng (touch commit J) | "THÀNH CÔNG"; lượt không đánh trong thời hạn → lỗi Late | 05, 06 |
| Kết thúc drill | 4/8 → 0 sao, "CHƯA ĐẠT MỤC TIÊU", tip theo lỗi, 1 mục chờ đồng bộ | 07 |
| Danh sách bài | Tab Tất cả/Cơ bản/Kiểm soát/Nâng cao/Đánh đôi | 08 |
Sau phiên: khôi phục profile offline (SHA-256 khớp), chuyển telemetry spool sang `quarantine-telemetry/`.

## Phiên 2 — online (người dùng tự đăng nhập)
| Bước | Kết quả | Ảnh |
|---|---|---|
| Menu online | Server: dedicated=true, hybridTelemetry=true, hybridCoaching=true, catalog schema 2, 15 bài, 3 cosmetic, bot fallback 180 s | 11 |
| Lịch sử | 2 trận Xếp hạng 1v1 vs "dat" 09/11 21:47 & 21:55 — "Gián đoạn", 0:0, +0 Elo, không replay | 12 |
| Bạn bè / BXH | 0 bạn; BXH 2 người (Huy, dat) cùng 100 Elo | 13, 14 |
| Nhiệm vụ | 7 nhiệm vụ ngày, daily streak đổi ngày 07:00 giờ VN | 15 |
| Chọn chế độ | 4 thẻ: Đấu với bot, Đấu online, Luyện tập, Tạo phòng + Tutorial | 16 |
| Chuẩn bị trận bot | Độ khó Dễ/Vừa/Khó/"BhTree"; ngày/đêm; sân Chuyên nghiệp/Ngoài trời | 17 |
| BotMatch | Intro cinematic letterbox + Bỏ qua; HUD điểm góc trái; gợi ý tình huống; autoPlayer (runtime) để quan sát trận | 18, 19 |
| Kết thúc trận (10:42–10:48) | Thắng 2–0 (ván 2: 11–3), 41 rally, rally dài nhất 6 chạm, FPS Editor 51–79. Server ghi BotPractice Won 2-0, +100 Tokenpad, +10 XP, Elo không đổi | 20 |
| Phân tích AI (Hybrid, server thật) | "86 lượt đánh, 71 tiếp xúc, 28 giao; Đánh hụt 5/86; không trả trước nảy 2 2/86"; gợi ý "Đánh bạt về cuối sân" (1/16 quan sát gần đây). Spool telemetry đã ACK và xóa | 21 |
| Shop | GEMSTONE 650 Elite; THOT 900 Tokenpad / 250 Elite; số dư 100 Tokenpad / 0 Elite | 22 |
| Bộ sưu tập | 3 skin vợt: PICKLEVERSE (đang dùng), GEMSTONE, THOT (chưa sở hữu, xem 3D) | 23 |
| Cài đặt | Tổng quan (Đăng xuất), Âm thanh, Đồ họa, Điều khiển (Máy tính / Điện thoại, đổi phím) | 24–26 |

Lưu ý dữ liệu: phiên BotMatch 10/11 10:42–10:48 trên tài khoản Huy do autoPlayer đánh, không phải người thật — loại khỏi mọi dataset gán nhãn chuyên gia / nghiệm thu AI.

## Lỗi / điểm cần sửa phát hiện khi chơi
1. Banner "Đang luyện tập ngoại tuyến · N mục chờ đồng bộ" đè tiêu đề ở Tiến trình, HUD Training, Danh sách bài.
2. Offline: ô "Đang phân tích…" không bao giờ đổi (CoachingSessionView.Poll return sớm khi chưa đăng nhập).
3. Hộp gợi ý phím HUD Training che nhân vật người chơi.
4. Nút "Quyết định & Khiếu nại" đè lên nút CHƠI NGAY ở Menu và lên panel hồ sơ.
5. Độ khó thứ 4 hiển thị tên kỹ thuật "BhTree" cho người chơi.
6. Thẻ trận bot ghi "Không cần kết nối mạng" nhưng offline thì CHƠI NGAY bị khóa.
7. Lựa chọn giới tính nhân vật (isMale) không áp dụng trong BotMatch/Training/Tutorial — luôn là nhân vật nữ.
8. Elo khởi đầu 100 (= sàn) trong khi admin reset-elo mặc định 2000 — mâu thuẫn quy tắc.
9. Trận dedicated thật trên server (09/11) đều kết thúc "Gián đoạn" — chưa có trận online trọn vẹn.
10. Màn kết quả BotMatch: ô "Xem phân tích" quá lớn, che giữa màn hình và nhân vật.
11. Số liệu phân tích lệch cách tính: tóm tắt "2/86" (toàn phiên) cạnh gợi ý "1/16" (theo kỹ thuật) — dễ gây hiểu nhầm.
12. Elite Token không có nguồn kiếm (không IAP, không thưởng) → GEMSTONE chỉ có qua mốc cấp 25; cần ghi rõ trong SRS/kinh tế game.
13. Cân bằng: autoPlayer thắng bot "Khó" 2–0 với rally ngắn (≤6 chạm) — cần phiên cân bằng bot/độ khó với người thật.

## Phiên 3 — Xếp hạng 1v1 trên dedicated server (10/11, 11:00–11:07)
| Thời điểm | Sự kiện |
|---|---|
| 11:00:44 | Bắt đầu tìm trận (Singles, Elo 100) |
| 11:03:23 | Sau ~160 s: ghép bot dự phòng `player09da8e832ab6` (Elo 220); ready-check; đã chấp nhận |
| 11:03:30 | Chuyển Shared → `conn=dedicated`, "Đang chuẩn bị máy chủ thi đấu…" |
| 11:03:36–46 | Vào scene Dedicated Match, tải sân, "Trận đã bắt đầu" (từ chấp nhận đến vào trận ≈ 23 s) |
| 11:06:04 | Kết quả: Đội B (bot) thắng 0:2, ván cuối 0:11, lỗi cuối "Bóng chạm người" (người chơi không điều khiển) |
| 11:06:47 | Về phòng chờ với thông báo "Mất kết nối Photon." (thông báo gây hiểu nhầm sau khi trận kết thúc bình thường) |
Kết luận: luồng tìm trận → bot dự phòng → vé dedicated → trận đủ 2 ván → kết quả chạy trọn vẹn trên server thật. Chưa kiểm tra lại Lịch sử/Elo/replay sau trận (Play Mode đã dừng). Trận này người chơi đứng yên → loại khỏi dataset AI.
