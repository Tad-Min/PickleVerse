# PickleVerse

## Khởi tạo database mới

Chỉ dùng **[`src/Database.sql`](src/Database.sql)**. File này chứa toàn bộ schema,
cấu hình OTP, role Player/Admin và dữ liệu gốc từ Unity (achievement, nhiệm vụ,
cosmetic, lesson/drill). Không tạo tài khoản Admin mặc định.

- **Docker dev:** cấu hình `.env` theo `.env.example`, rồi chạy `docker compose up -d --build`
  tại thư mục repository. PostgreSQL tự chạy file SQL khi khởi tạo volume database lần đầu.
- **PostgreSQL có sẵn:** tạo một database trống, mở kết nối vào database đó và chạy toàn bộ
  `src/Database.sql` một lần bằng pgAdmin hoặc `psql -v ON_ERROR_STOP=1 -d <database> -f src/Database.sql`.

Volume database đã tồn tại được giữ nguyên; Compose không chạy lại init script.
Không chạy toàn bộ file khởi tạo lên database đang có dữ liệu, không cần xóa volume hiện tại.

Hướng dẫn chạy Docker dev, cấu trúc service Unity và kết quả kiểm thử:
[Unity – Backend Integration](Doc/PickleVerse_Project_Documentation.md#doc-unity-backend-integration).

Database cũng chứa 10 tài khoản ảo role Player dùng cho đối thủ dự phòng Rank Online
sau 6 phút tìm người thật. Chúng không có quyền đăng nhập. Luồng vẫn chạy trong phòng
Photon; thời gian hoạt động tính từ lúc mở app. Xem thay đổi schema và cách đồng bộ tại
[Thời gian app và Rank Online](Doc/PickleVerse_Project_Documentation.md#doc-app-activity-ranked-fallback).
