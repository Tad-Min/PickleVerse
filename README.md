# PickleVerse

## Khởi tạo database mới

Chỉ dùng **[`src/Database.sql`](src/Database.sql)**. File này chứa toàn bộ schema,
dữ liệu xác thực email, role Player/Admin và dữ liệu gốc từ Unity (achievement, nhiệm vụ,
cosmetic, lesson/drill). Không tạo tài khoản Admin mặc định.

Toàn bộ schema và dữ liệu gốc được khởi tạo chung trong một transaction `BEGIN … COMMIT`.
Bảng `AppActivitySessions` khởi tạo rỗng; 10 tài khoản bot kèm hồ sơ, role Player nằm cùng phần seed. Hàng chờ ghép trận, tìm trận và xác nhận dùng Redis.

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

## Cấu hình API và gameplay

`src/Backend/API/appsettings.json` quản lý SMTP (Host/Port/FromName), OTP (10 phút / gửi lại sau 60 giây / 5 lần sai), token (2 giờ / 14 ngày), Matchmaking (bot sau 360 giây / xác nhận 120 giây), OnlineMatch (mất kết nối 35 giây / heartbeat 8 giây) và toàn bộ EXP/phần thưởng progression. API kiểm tra cấu hình khi khởi động; sau khi sửa chạy `docker compose up -d --build backend-api`.

Khi chạy Docker, SMTP Username/Password/FromEmail lấy từ `SMTP_USERNAME`, `SMTP_APP_PASSWORD`, `SMTP_FROM_EMAIL` trong `.env` và ghi đè các giá trị tương ứng trong `appsettings.json`. Sau khi đổi Google App Password, cập nhật `SMTP_APP_PASSWORD` trong `.env` rồi chạy `docker compose up -d backend-api` để tạo lại container với mật khẩu mới; `docker compose restart` không nạp lại `.env`. Không commit giá trị bí mật. Unity chỉ nhận cấu hình gameplay công khai qua catalog API.

Schema mới có **37 bảng / 208 cột**, đã bỏ `GameSettings` và `RankedSearches`; danh mục game và dữ liệu người chơi tiếp tục lưu database. Với database cũ, triển khai API mới rồi sao lưu và bỏ riêng bảng `GameSettings`; giữ nguyên volume và dữ liệu khác. Chi tiết ở [mục cấu hình API trong tài liệu tổng hợp](Doc/PickleVerse_Project_Documentation.md#doc-server-configuration).

## Redis và Photon

`docker compose up -d --build` khởi động cả Redis. API trong Docker kết nối `redis:6379`; khi chạy API trực tiếp, mặc định kết nối `localhost:16379`. Có thể đổi cổng host bằng `REDIS_PORT` trong `.env` và đổi `Redis:ConnectionString` tương ứng nếu chạy API ngoài Docker.

Unity đăng ký/poll hàng chờ qua API; API trả phòng Photon được ghép, từng người xác nhận rồi mới tải sân. Rank Singles giữ bot dự phòng sau 360 giây, xác nhận trong 120 giây. Nhóm Doubles cần mỗi thành viên đăng ký và luôn được giữ cùng đội. Protocol Unity hiện là 14; cần dùng cùng bản client mới.

Leaderboard dùng Redis Sorted Set, cache 15 giây; Elo/kết quả/thời gian app vẫn lưu PostgreSQL. Redis có AOF và volume riêng, không thay thế PostgreSQL. Database cũ cần backup rồi bỏ riêng `RankedSearches` sau khi triển khai API mới; giữ `AppActivitySessions` và các volume. Không chạy lại `Database.sql` trên database có dữ liệu.

Xem [chi tiết Redis](Doc/PickleVerse_Project_Documentation.md#doc-redis-matchmaking).
