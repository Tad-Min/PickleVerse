# Thời gian trong app và đối thủ dự phòng Rank Online

Phạm vi người dùng duyệt ngày 22/10/2026: thời gian từ mở app, chờ đối thủ thật đủ 360 giây, 10 tài khoản ảo, giữ phòng Photon và tổng kết Elo online.

## Thời gian hoạt động

`AppActivityRuntime` khởi động trước scene đầu tiên và dùng đồng hồ thời gian thực, độc lập với `Time.timeScale`. Menu, đăng nhập, tìm trận, training và trận đấu đều thuộc cùng phiên. Phiên được mở trước đăng nhập; khi đăng nhập mới gắn với tài khoản, bao gồm khoảng chờ đăng nhập. Đồng bộ mỗi 15 giây, trước đăng xuất/đổi tài khoản và khi thoát Windows bình thường. UI hồ sơ hiển thị thời gian tăng giữa các checkpoint.

- `POST /api/Game/activity/open`: không cần JWT, mở phiên bằng UUID ngẫu nhiên của lần chạy app.
- `POST /api/Game/activity/checkpoint`: cần JWT; `{ sessionId, elapsedSeconds, close }`. UUID trong JSON dùng định dạng có dấu gạch nối. Server trả `{ totalSeconds, elapsedSeconds }`.
- Server gắn phiên với một tài khoản, kiểm tra thời gian tăng theo đồng hồ server và tính phần thời gian chưa được ghi nhận. Retry và các cửa sổ app chồng nhau không cộng trùng. Đóng phiên không tiếp tục tăng giờ.
- Không còn cộng thời lượng khi hoàn tất trận. Endpoint cũ `/api/Profiles/sync-time` trả 410 để tránh cộng thêm lần nữa.
- Giữ số thời gian đã lưu trước thay đổi này; không có log mở/đóng app cũ để tính lại lịch sử chính xác. Nếu process bị kill/crash trước checkpoint cuối, phần chưa gửi chưa được bảo đảm lưu.

## Rank Online sau sáu phút

Unity tạo lượt tìm trận tại `POST /api/Game/ranked/search`. API giữ mốc bắt đầu, không đặt lại khi gửi lại cùng mã yêu cầu. Trong 360 giây đầu vẫn tìm người thật trên Photon theo cơ chế Elo hiện có.

Khi đủ hạn mà phòng vẫn chỉ có một người, peer điều phối khóa nhận thêm người trong lúc xin đối thủ tại `POST /api/Game/ranked/bot`. API kiểm tra tài khoản, lượt tìm chưa hủy, đủ thời gian, mã trận và tên phòng; cấp một tài khoản ảo còn trống. Unity thêm actor bot vào chính phòng đó, không tải `BotMatch`.

Luồng tiếp tục là **xác nhận → MultiplayerCourt → bắt đầu**. Ghế ảo được hệ thống chấp nhận; người chơi thật vẫn phải tự bấm xác nhận. Hủy/từ chối tìm trận làm lượt tìm mất hiệu lực, kể cả khi phản hồi cấp bot về muộn. Bot được điều khiển bằng `MultiplayerBot` trên state authority của phòng Fusion Shared.

Tên, giới tính và vợt lấy từ hồ sơ database. Elo của đối thủ được snapshot theo `EloPolicy.BotElo` hiện có, với sàn 100. Trận ghi loại `RankedSingles`, nhận phần thưởng online 150/75 TOKENPAD theo cấu hình gốc và tính Elo trên backend. API PvE chỉ cho `BotPractice`, không còn cho tạo `RankedBot` trực tiếp.

## Tài khoản và dữ liệu

Seed nằm trong block `BEGIN APP ACTIVITY AND RANKED FALLBACK` của `src/Database.sql`. Chạy toàn bộ file chỉ cho database mới. Với volume đã có, chỉ áp dụng block bổ sung này; không xóa volume.

10 tài khoản có role **Player**, hồ sơ riêng và UUID cố định. Danh sách chuẩn cho Unity/API nằm trong `My project/Assets/Scripts/Entites/Multiplayer/RankedBotAccounts.cs`; backend compile cùng file, kiểm thử đối chiếu với SQL. Các tài khoản này không đăng nhập/refresh được và không gửi OTP; không có mật khẩu đăng nhập chung. Không tạo xác thực email giả để đưa chúng vào danh sách người thật.

Thay đổi schema trong đợt này: **thêm 2 bảng, tổng cộng 15 cột mới; không thêm/bớt cột của bảng cũ**.

| Bảng | Số cột | Vai trò |
| --- | ---: | --- |
| `appactivitysessions` | 6 | Lần mở app, tài khoản gắn với phiên, thời gian đã báo/đã ghi nhận và trạng thái đóng. Tổng hiện ở `playerprofiles.timeplayedseconds`, Unity đọc vào mục Thời gian hoạt động. |
| `rankedsearches` | 9 | Mốc 360 giây, hủy tìm, UUID bot được cấp, Elo snapshot và ràng buộc phòng Photon/mã trận. |

Các bảng có sẵn tiếp tục dùng: `users`/`roles`/`playerprofiles` chứa 10 danh tính; `gamematches` giữ phòng/đội hình/kết quả; `gamematchplayers` có hai participant; `matchhistory` và `playermatchhistory` lưu tỉ số, bên thắng/thua và Elo từng UUID. Backend chỉ tự hoàn tất ghế ảo đã được cấp; trận giữa hai người thật vẫn cần hai JWT đồng ý và báo cáo tỉ số khớp.

## Kết thúc trận

Giữ bảng tỉ số và phần trình bày kết thúc hiện có. Nút **THOÁT TRẬN** mở màn tổng kết; nút về phòng khi trận đã kết thúc cũng đi qua màn này. Tổng kết đợi kết quả đã lưu, hiển thị Elo cộng/trừ và Elo trước/sau từ backend. Retry không cấp thưởng hoặc đổi Elo thêm lần nữa. Sau đó người chơi chọn về phòng hoặc menu.

Kiểm thử tự động mới: `IntegrationChecks --check-app-ranked` kiểm tra dữ liệu seed, đồng hồ app, chồng phiên, mốc 359/360, hủy sát hạn, cấm PvE rank, lịch sử và Elo chống lặp. `RankedFallbackSmokeRunner` là runner development gọi Photon thật và chờ đủ sáu phút; phần kiểm tra kết thúc trận chủ động tạo lỗi rally qua luật điểm thật để hoàn tất trận trong thời gian kiểm thử, không giả lập response API.

## Kết quả kiểm chứng 22/10/2026

- 122 kiểm tra backend hiện có đã qua sau khi cập nhật fixture cho 10 tài khoản ảo; thêm 22 kiểm tra app/rank đã qua trên PostgreSQL tạm. Log: `artifacts/verification/app-ranked-auth-regression.log`, `app-ranked-backend-checks.log`.
- 18 kiểm tra Unity Services đã qua, gồm chốt phiên trước đăng xuất và đổi tài khoản: `app-ranked-unity-services-tests.json`.
- Photon thật trong Editor: chờ 360,80 giây, xác nhận, sân SceneRef #5 (`MultiplayerCourt`), đủ hai actor với một bot, có cú đánh/trả bóng, tổng kết +60 Elo. `ranked-fallback-editor.json`; ảnh `ranked-fallback-editor-summary.png`. Công cụ kiểm thử đã được sửa để kiểm tra SceneRef của Fusion, vì multi-peer gộp asset sân vào scene runtime có tên khác.
- Photon thật trong Windows: chờ 360,77 giây, cùng luồng sân online, tổng kết -60 Elo. `ranked-fallback-windows.json`. Khi app đóng bình thường, database ghi phiên đã đóng và 463 giây: `app-ranked-windows-close.json`.
- UI hồ sơ Editor: thời gian đổi từ `00:10:46` sang `00:10:52`; tạm dừng `Time.timeScale=0` vẫn tăng 3,21 giây; checkpoint và đăng xuất thành công. `app-activity-profile-check.json`, ảnh `app-activity-profile.png`.
- Lịch sử database trước dọn fixture có đủ hai participant/UUID và Elo từng bên cho mỗi trận: `ranked-fallback-database-results.json`.
- Sửa thêm lỗi dọn phần trình bày: `ActorPose.Restore` không đọc `CourtEnd` từ actor mạng đã despawn. Đã chạy lại tìm trận công khai hai người thật bằng Editor + Windows, cả hai xác nhận/vào sân/di chuyển/ghi replay/rời trận đều đạt; `ranked-human-regression-editor.json`, `ranked-human-regression-windows.json`. Console Editor sau rời sân và dừng Play có 0 lỗi: `app-ranked-final-console.json`.
- Windows build cuối thành công, 0 lỗi; còn một cảnh báo API `SimulationMessagePtr` đã obsolete của SDK Fusion: `app-ranked-windows-build.json`. Player log Windows còn có thông báo native `Curl error 35` về xác thực chứng chỉ; chưa xác định request phát sinh log này. Các luồng API dev, Photon và replay trong ca kiểm thử vẫn hoàn tất; không tắt kiểm tra chứng chỉ để che log.
- Đã dọn hai tài khoản tạm, trận/replay của các UUID thử, và trả hai hồ sơ bot đã dùng về trạng thái seed ban đầu. Giữ tài khoản và dữ liệu người dùng thật. `app-ranked-cleanup.log`.
- Database dev sau thay đổi có 39 bảng/219 cột; riêng đợt này thêm 2 bảng/15 cột. `app-ranked-schema-summary.json`.
