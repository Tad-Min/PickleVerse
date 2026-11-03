# Merge main vào realtime-AI-response — 03/11/2026

Merge `origin/main` tại `61504d43` vào nhánh `realtime-AI-response`, sau `bcbb47d1`.

Theo yêu cầu bổ sung của người dùng trong đợt merge này, dùng **scene Login được đưa về từ main** làm scene đầu client, thay thế và xóa scene Authentication cũ. Giữ GUID Login `48bfcab7c33d01146a6711a22d2f275b`, phong cách uGUI và nền động của main; bổ sung hai nút Quên mật khẩu/Về đăng nhập vào hierarchy có sẵn. Đồng bộ scene khởi động trong global build settings, build profile client, navigation và các script build/setup.

- Giữ đăng ký, email OTP và recovery OTP qua các API hiện có; recovery không dùng endpoint xác thực email. Mật khẩu được che mặc định và xóa khỏi form sau thao tác thành công. Reset thành công xóa cả phiên cục bộ đã bị thu hồi nếu có.
- Giữ loading chuẩn bị tài khoản và Menu song song, rollback khi API lỗi và retry phiên đã đăng nhập. Navigation cho phép transition này hoàn tất trước khi kiểm tra quyền vào scene; phiên mất trong lúc warm-up không được lộ Menu.
- Giữ hồ sơ trong ProgressionModal, chọn Nam/Nữ tại Character Selection, đăng xuất tại Settings. Không khôi phục nút/trang Tài khoản của phiên bản main cũ. Gộp lại một luồng lưu nhân vật có kiểm tra phòng/tìm trận/trận đấu và chỉ đổi hiển thị sau API xác nhận.
- Nhận khán giả, animation, sân và nền động/loading từ main. Giữ đường đồng bộ multiplayer/dedicated của nhánh hiện tại và kiểm tra authority trước khi nhận thông báo khán giả.
- Hợp nhất lịch sử bàn giao của cả hai nhánh; font Bold dùng phiên bản main. Không thay đổi backend/schema hay cấu hình endpoint API.

## Xác minh

Unity 6000.6.2f1, CLI 1.0.0-beta.15, Windows Editor trên máy hiện tại:

| Kiểm tra | Kết quả |
|---|---|
| Compile | Thành công, 0 lỗi |
| Unity Editor tests | 481/481 |
| Asset Login | 8 kiểm tra đạt |
| UI đăng ký/email OTP/recovery/hồ sơ/Settings | 30 kiểm tra đạt |
| Loading song song, lỗi đăng nhập, retry và hết phiên | 46 kiểm tra chức năng đạt |
| Khán giả: asset, animation, phản ứng và scene | 40 kiểm tra đạt |

UI/Loading dùng HTTP fixture loopback với transport/service thật trong Unity; không dùng database hoặc gửi email thật. Fixture xóa phiên trước khi khôi phục transport. Đã xem ảnh màn Quên mật khẩu và Đặt lại mật khẩu ở 1920×1080.

Hiệu năng Editor được ghi riêng, không tính là nghiệm thu bản build: lượt đầu p95 26,46 ms; lượt kiểm tra chức năng cuối p50 7,16 ms, p95 10,14 ms, tối đa 29,81 ms/600 frame. Cờ `performanceAccepted` vẫn false vì có spike vượt ngưỡng 20 ms. Harness hỗ trợ bật lại việc bắt buộc frame budget khi chạy nghiệm thu hiệu năng. Chưa chạy lại Windows/Linux build hoặc trận multiplayer thực tế trong riêng đợt merge này.

Bằng chứng cục bộ, không đưa vào Git: `artifacts/login-20261101/play-checks.json`, `artifacts/merge-main-20261103/loading/e2e.json`, `artifacts/audience-20261031/asset-animation-checks.json`, hai ảnh `artifacts/merge-main-20261103/login-*-password.png`.
