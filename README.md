# PickleVerse

## CI/CD Docker Hub

Build Unity Dedicated Server trên máy đã có license bằng
`scripts/Publish-DockerHubRelease.ps1`. Script đóng gói binary theo commit rồi,
khi có `-Publish`, push `main` và phát hành asset GitHub Release.
GitHub Actions tải binary đó, build/push bốn image `api`, `ai`, `web`, `server`
vào `tadminm/pickelverse`, kiểm tra cả stack và xuất gói Compose ghim digest.
Push/PR chỉ kiểm thử; CI chỉ cần secret `DOCKERHUB_TOKEN`, không cần license Unity.
Xem [hướng dẫn CI/CD và triển khai Ubuntu](Doc/CI_CD_DockerHub.md).

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

Docker dev dùng volume `pickleverse_dev_postgres_data`, có thể đổi tên bằng `POSTGRES_VOLUME`
trong `.env`. Khi cần database trắng để phát triển, chọn một tên volume mới rồi chạy lại
`docker compose up -d postgres backend-api`. PostgreSQL nạp trực tiếp schema và seed từ
`src/Database.sql` khi volume trống; volume đã khởi tạo không chạy lại SQL.

Nếu chữ tiếng Việt trong danh mục đã bị lưu thành `?`, chạy
`./scripts/Repair-CatalogText.ps1` để xem trước, rồi thêm `-Apply` để phục hồi từ
`src/Database.sql`. Script sao lưu sáu bảng danh mục trước khi sửa, chỉ cập nhật
văn bản bị hỏng; giữ nguyên cấu hình gameplay và dữ liệu người chơi.
File SQL được chuyển bằng `docker cp` và đọc qua `psql --file`, tránh mất dấu do
pipe `Get-Content ... | docker exec -i ... psql` trên Windows. Với database mới,
dùng init script của Compose hoặc `psql -f` như trên.

Hướng dẫn chạy Docker dev, cấu trúc service Unity và kết quả kiểm thử:
[Unity – Backend Integration](Doc/PickleVerse_Project_Documentation.md#doc-unity-backend-integration).

Database cũng chứa 10 tài khoản ảo role Player dùng cho đối thủ dự phòng Rank Online
sau 6 phút tìm người thật. Chúng không có quyền đăng nhập. Luồng vẫn chạy trong phòng
Photon; thời gian hoạt động tính từ lúc mở app. Xem thay đổi schema và cách đồng bộ tại
[Thời gian app và Rank Online](Doc/PickleVerse_Project_Documentation.md#doc-app-activity-ranked-fallback).

## Cấu hình API và gameplay

Unity đọc địa chỉ API từ asset `My project/Assets/Resources/BackendApiSettings.asset` (chọn asset trong Project và sửa **Base Url** ở Inspector). Đổi giá trị mặc định trong `BackendApiSettings.cs` không cập nhật asset đã tồn tại. Để thử server HTTP ngoài localhost trong Editor hoặc Development Build, bật **Allow Remote Http In Development** trên asset; bản phát hành vẫn yêu cầu URL HTTPS.

`src/Backend/API/appsettings.json` quản lý SMTP (Host/Port/FromName), OTP (10 phút / gửi lại sau 60 giây / 5 lần sai), token (2 giờ / 14 ngày), Matchmaking (bot sau 360 giây / xác nhận 120 giây), OnlineMatch (mất kết nối 35 giây / heartbeat 8 giây) và toàn bộ EXP/phần thưởng progression. API kiểm tra cấu hình khi khởi động; sau khi sửa chạy `docker compose up -d --build backend-api`.

Khi chạy Docker, SMTP Username/Password/FromEmail lấy từ `SMTP_USERNAME`, `SMTP_APP_PASSWORD`, `SMTP_FROM_EMAIL` trong `.env` và ghi đè các giá trị tương ứng trong `appsettings.json`. Sau khi đổi Google App Password, cập nhật `SMTP_APP_PASSWORD` trong `.env` rồi chạy `docker compose up -d backend-api` để tạo lại container với mật khẩu mới; `docker compose restart` không nạp lại `.env`. Không commit giá trị bí mật. Unity chỉ nhận cấu hình gameplay công khai qua catalog API.

SQL khởi tạo bao gồm toàn bộ schema game, hàng đợi Hybrid AI và Elo ledger. Trong giai đoạn phát triển, cập nhật trực tiếp `src/Database.sql` và khởi tạo database mới khi schema thay đổi. Danh mục game và dữ liệu gốc được seed trong cùng transaction.

## Redis và Photon

`docker compose up -d --build` khởi động cả Redis. API trong Docker kết nối `redis:6379`; khi chạy API trực tiếp, mặc định kết nối `localhost:16379`. Có thể đổi cổng host bằng `REDIS_PORT` trong `.env` và đổi `Redis:ConnectionString` tương ứng nếu chạy API ngoài Docker.

Unity đăng ký/poll hàng chờ qua API; API trả phòng Photon được ghép, từng người xác nhận rồi mới tải sân. Rank Singles giữ bot dự phòng sau 360 giây, xác nhận trong 120 giây. Nhóm Doubles cần mỗi thành viên đăng ký và luôn được giữ cùng đội. Protocol Unity hiện là 14; cần dùng cùng bản client mới.

Leaderboard dùng Redis Sorted Set, cache 15 giây; Elo/kết quả/thời gian app vẫn lưu PostgreSQL. Redis có AOF và volume riêng, không thay thế PostgreSQL. Schema khởi tạo hiện tại không chứa các bảng cấu hình cũ `GameSettings` và `RankedSearches`.

Xem [chi tiết Redis](Doc/PickleVerse_Project_Documentation.md#doc-redis-matchmaking).

## Mời bạn bè vào phòng

Trong phòng Doubles hoặc Custom, chọn **Mời bạn bè**, tìm tên/ID rồi bấm **Mời**.
Người nhận phải có quan hệ bạn bè và đang ở lobby; thông báo có **Chấp nhận/Từ chối**,
chấp nhận sẽ vào đúng phòng Fusion. Lời mời có hiệu lực 60 giây, cập nhật khoảng 3 giây.
Presence và lời mời dùng Redis, không cần thêm bảng Rooms.
Xem [triển khai và kết quả kiểm tra](Doc/Proccess/Friend_Room_Invitations_20261025.md).

## Hybrid AI

Kiến trúc Unity-local bot + FastAPI analytics, khởi tạo database, cờ triển khai và hướng dẫn kiểm thử nằm tại [Hybrid AI](Doc/Hybrid_AI_Implementation.md). Các bảng Hybrid và Elo ledger được tạo trực tiếp bởi `src/Database.sql`.

Hướng dẫn thử coaching trên môi trường development đã bật ngày 31/10/2026: [AI service và cách test trong game](Doc/AI_Service_Activation_20261031.md).
