# Các thay đổi backend cần duyệt trước khi áp dụng

Ngày: 20/10/2026. Phạm vi đợt đầu: Docker dev, đăng ký, OTP Gmail, đăng nhập/JWT và hồ sơ Unity.

Tài liệu ghi lại đề xuất đã được người dùng duyệt ngày 20/10/2026. Source backend, script nâng cấp và Docker Compose đã được cập nhật theo phạm vi này.

## 1. Các lỗi đã đối chiếu với source

| Vị trí | Hiện trạng | Thay đổi đề xuất |
|---|---|---|
| `docker-compose.yml` | Mount `src/MyDatabaseShorter.sql`, nhưng file không tồn tại. | Mount `src/Database.sql` dưới dạng chỉ đọc; từ chối tạo thư mục khi source thiếu. |
| `Repositories/Context/PickleVerseContext.cs` | `OnConfiguring` luôn gọi `UseNpgsql` với host `localhost`, ghi đè cấu hình DI dùng host `postgres`. | Chỉ cấu hình fallback khi `optionsBuilder.IsConfigured == false`; lấy chuỗi kết nối từ cấu hình thay vì hardcode. |
| `src/Database.sql` và `AuthService` | Schema không seed role; đăng ký fallback về role ID 1 dù role có thể không tồn tại. | Seed idempotent role Player/Admin bằng script dev; đăng ký tra role Player và báo lỗi cấu hình nếu thiếu. Không tạo tài khoản admin hay mật khẩu mặc định. |
| `AuthService.RegisterAsync` | `Isverified = true`; không gửi thư. | Tạo trạng thái chưa xác thực, sinh OTP, gửi bằng SMTP Gmail từ backend. |
| `AuthService.VerifyEmailAsync` | Tra token với `Guid.Empty`, không dùng email được gửi lên. | Tìm đúng tài khoản theo email; kiểm tra OTP còn hạn, chưa dùng, đúng tài khoản; xác thực thành công mới kích hoạt đăng nhập. |
| `AuthService.LoginAsync/RefreshTokenAsync` | Đăng nhập không kiểm tra email; refresh không kiểm tra trạng thái cấm/tạm khóa. | Chỉ cấp/gia hạn phiên cho tài khoản hoạt động và đã xác thực email. |

## 2. Phạm vi patch xin duyệt

1. Sửa Compose và cấu hình kết nối DB; thêm kiểm tra sẵn sàng PostgreSQL trước khi khởi động API. Giữ volume hiện có, không chạy `down -v`.
2. Thêm seed role dev, `.dockerignore` và cấu hình biến môi trường phù hợp Docker dev.
3. Thêm dịch vụ gửi SMTP Gmail phía backend và cấu hình qua biến môi trường. Không đưa Gmail App Password vào Git hoặc Unity.
4. Sửa đăng ký/xác thực/đăng nhập/refresh theo bảng trên; thêm API gửi lại OTP với thời gian chờ.
5. Bảo vệ luồng OTP bằng thời hạn, giới hạn thử/gửi lại và xử lý mã đã dùng. Nếu lưu hash OTP, cập nhật độ dài cột `verificationemail.token` cùng script nâng cấp cho DB hiện có; không xóa DB.
6. Thêm kiểm thử backend cho các trường hợp xác thực và tài khoản bị khóa; chạy Docker và kiểm tra API sau khi patch được duyệt.

Endpoint đã có được giữ: `POST /api/Auth/register`, `verify-email`, `login`, `refresh-token`, `logout`; `GET/PUT /api/Profiles/me`.

Endpoint bổ sung dự kiến: `POST /api/Auth/resend-otp`, body `{ "email": "..." }`. Unity có thể hoàn thành luồng đăng ký/xác thực bằng endpoint hiện có trước khi endpoint bổ sung được duyệt.

## 3. Cấu hình Gmail cần người dùng cung cấp tại máy

- Gmail gửi thư và địa chỉ From phù hợp tài khoản SMTP.
- Gmail App Password (qua file môi trường không commit).
- SMTP Gmail: `smtp.gmail.com:587`, STARTTLS.
- Không thể xác nhận gửi email thật thành công trước khi có cấu hình này và địa chỉ nhận thử do người dùng chỉ định.

## 4. Phần ngoài patch này

- Chuyển Netcode/Unity Services sang Photon chưa nằm trong patch backend/auth.
- API replay/S3 sẽ được xử lý theo đợt multiplayer. Hiện URL trả về dùng hostname nội bộ `s3-storage`, và cách đọc key bằng `Path.GetFileName` làm mất prefix thư mục; cần sửa trước khi dùng replay thực tế.
- Các service training/shop/nhiệm vụ và đồng bộ toàn bộ tiến trình cần phạm vi tích hợp riêng.

## Nguồn đối chiếu

- Source trực tiếp trong repository: Compose, Dockerfile, `PickleVerseContext`, `AuthService`, `UserRepository`, `Database.sql`, `ReplayService`, `StorageService`.
- Docker: https://docs.docker.com/compose/how-tos/startup-order/
- local-s3: https://github.com/shyim/local-s3
- Gmail App Password: https://support.google.com/accounts/answer/185833

## Quyết định

Người dùng đã đồng ý toàn bộ phạm vi backend/Compose/OTP. Lỗi phân quyền Admin được báo riêng và cũng đã được người dùng đồng ý sửa; xem `Backend_Followup_Findings.md`.
