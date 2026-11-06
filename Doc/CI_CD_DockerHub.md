# CI/CD PickleVerse trên GitHub Actions

## Luồng phát hành

Workflow `Build, publish and deploy PickleVerse` chạy khi push vào `main`. Nó kiểm tra backend, FastAPI và web admin, đồng thời dùng GameCI để build Unity Dedicated Server Linux x86-64 trực tiếp từ commit đó. Job publish xác minh ELF, phiên bản Unity, protocol và checksum trước khi build bốn image `api`, `ai`, `web`, `server` trong `tadminm/pickelverse`.

Mỗi lượt publish dùng tag `<component>-<commit>-<run_id>-<attempt>`. Sau khi smoke test với PostgreSQL và Redis tạm, workflow tạo artifact `release-<commit>-<run_id>-<attempt>` gồm Compose, `Database.sql`, digest của các image và checksum SHA-256. Job deploy dùng đúng artifact của lượt build đã qua kiểm tra. Các lần deploy nối tiếp nhau qua GitHub concurrency và khóa trên Linux. Lượt build của commit cũ bỏ qua deploy nếu `main` đã tiến lên.

Pull request chạy validation. **Run workflow** từ `main` có thể build và publish lại. Chọn `deploy` nếu muốn lượt chạy thủ công cũng deploy. Thiết lập GitHub Environment `development` cho nhánh `main` và để job chạy tự động theo lựa chọn đã chốt.

## GitHub Secrets và Variables

Thêm giá trị trong **Settings → Secrets and variables → Actions**. Dùng repository secrets cho Unity và token publish. Có thể đặt SSH và token pull trong environment `development` để chỉ job deploy nhận chúng.

| Secret | Giá trị |
| --- | --- |
| `UNITY_EMAIL` | Email tài khoản Unity dùng để kích hoạt CI. |
| `UNITY_PASSWORD` | Mật khẩu tài khoản Unity. |
| `UNITY_LICENSE` | Nội dung đầy đủ file `.ulf` cho Unity Personal theo hướng dẫn GameCI. Chọn đúng một trong secret này và `UNITY_SERIAL`. |
| `UNITY_SERIAL` | Serial Unity Pro hoặc Enterprise. Dùng cho cơ chế cấp phép bằng serial. |
| `DOCKERHUB_TOKEN` | Docker Hub PAT có quyền đọc và ghi `tadminm/pickelverse`. |
| `DOCKERHUB_PULL_TOKEN` | Docker Hub PAT chỉ đọc image trên Linux. Docker CLI nhận qua stdin và dùng thư mục cấu hình tạm. |
| `SSH_HOST` | DNS name hoặc IPv4 mà GitHub-hosted runner có thể truy cập. |
| `SSH_PORT` | Cổng SSH thực tế. |
| `SSH_PASSWORD` | Mật khẩu SSH của tài khoản triển khai. |
| `SSH_KNOWN_HOSTS` | Dòng public host key lấy từ máy Linux qua kênh tin cậy. Với cổng 22 dùng `HOST <key-type> <public-key>`. Với cổng khác dùng `[HOST]:PORT <key-type> <public-key>`. |

Variables tùy chọn gồm `DOCKERHUB_USERNAME` mặc định `tadminm` và `SSH_USER` mặc định `min`. Tài khoản Docker Hub phải có quyền với repository `tadminm/pickelverse`. Workflow dùng `github.token` tự cấp để đối chiếu HEAD của `main`.

Lấy public key qua console đáng tin cậy của Linux, ví dụ `awk '{print $1, $2}' /etc/ssh/ssh_host_ed25519_key.pub`. Giữ đúng kiểu key và hostname/cổng của endpoint. Có thể đối chiếu fingerprint bằng `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub`. OpenSSH trên runner kiểm tra host key trước khi gửi mật khẩu.

## Chuẩn bị Linux một lần

1. Dùng Linux amd64, Docker Engine local, Docker Compose có `up --wait-timeout`, `bash`, `curl`, `tar`, `sha256sum`, `mountpoint`, `realpath` và `flock`. Tài khoản SSH cần chạy Docker và ghi được vào `/run/media/min/Data/pickleverse`.
2. Mount ổ dữ liệu tại `/run/media/min/Data` trước khi deploy. Ổ phải hỗ trợ quyền POSIX và ownership mà PostgreSQL và Unity cần. Script kiểm tra mount mỗi lượt chạy để tránh ghi nhầm xuống ổ hệ thống.
3. Dùng `deploy/linux/runtime.env.example` làm mẫu cho `/run/media/min/Data/pickleverse/.env`. Giữ cấu hình này trên host với quyền đọc giới hạn cho tài khoản deploy. Các khóa bắt buộc gồm `POSTGRES_PASSWORD`, `S3_ACCESS_KEY`, `S3_SECRET_KEY`, `JWT_KEY`, `AI_SERVICE_KEY` và `PKV_GAME_SERVER_KEY`. Ba key JWT, AI và Dedicated cần tối thiểu 32 ký tự. Dùng mật khẩu PostgreSQL không chứa dấu nháy kép hoặc xuống dòng để dùng được trong connection string.
4. Khi giữ database và S3 hiện có, điền đúng thông tin đăng nhập đang dùng trên stack cũ. PostgreSQL chỉ áp dụng `POSTGRES_PASSWORD` khi khởi tạo cluster rỗng. Giữ JWT key hiện tại nếu muốn duy trì các phiên đang dùng. Điền `SMTP_USERNAME`, `SMTP_APP_PASSWORD`, `SMTP_FROM_EMAIL` khi cần gửi OTP và khôi phục mật khẩu. Giữ các bind và cổng hiện dùng nếu Linux có reverse proxy.

Các thư mục sau nằm trong `/run/media/min/Data/pickleverse`.

| Dữ liệu | Thư mục host | Điểm mount trong container |
| --- | --- | --- |
| PostgreSQL | `PostgresData` | `/var/lib/postgresql/data` |
| Redis | `RedisData` | `/data` |
| S3 | `S3Data` | `/data` |
| AI artifact | `AiService/models` | `/models`, chỉ đọc |
| Dedicated | `GameServerData` | `/home/unity/.config/unity3d` |

## Chuyển dữ liệu và kiểm tra deploy

- Nếu phát hiện container PickleVerse cũ cùng Compose project dùng named volume hoặc bind mount khác trong cây dự án, deploy kiểm tra PostgreSQL 16, schema và trận đang hoạt động. Sau đó nó tạo `pg_dump` vào `.deploy/backup.*`, dừng các dịch vụ liên quan rồi sao chép dữ liệu sang thư mục mới. Nguồn cũ được giữ để phục hồi. Khi đã có dữ liệu ở cả hai nơi hoặc phát hiện volume cũ chưa gắn container, job dừng để tránh chọn nhầm bản dữ liệu.
- Schema được đối chiếu với PostgreSQL khởi tạo từ `src/Database.sql` trong CI. Database mới dùng file này trong lần khởi tạo đầu. Nếu schema cũ khác, workflow báo lỗi và giữ dữ liệu. Cần xử lý riêng trên development với backup và quy trình đã chốt cho database trước khi chạy lại.
- Deploy dùng digest đã smoke test, đợi PostgreSQL, Redis, AI, web và Dedicated báo sẵn sàng, rồi xác minh HTTP API và web. `.deploy/current` chỉ cập nhật sau khi tất cả kiểm tra đạt. Khi có release trước do workflow quản lý và schema tương thích, lỗi khởi động sẽ thử khôi phục bộ image trước. Lần chuyển đầu giữ nguyên nguồn cũ và backup để phục hồi bằng lệnh Compose cũ đã biết.
- `AI_ENABLE_MODEL` và `HybridAi__ModelEnabled` bật ở development. `AiService/models/tier.joblib` và `tier.json` phải qua cổng model mới được phục vụ. Trong lúc thiếu artifact đạt cổng, FastAPI báo `model=unavailable` và tiếp tục coaching theo luật. Đặt thư mục và file artifact đủ quyền đọc cho user của container AI.

Xem workflow run và artifact phát hành ở tab **Actions**. Các script phát hành native cục bộ cũ có thể dùng để kiểm tra binary. Workflow này nhận trực tiếp commit `main` và tự build native.

## Tài liệu tham chiếu

- [GameCI Unity Builder](https://game.ci/docs/github/builder/) và [kích hoạt Unity trên CI](https://game.ci/docs/github/activation/)
- [GitHub Actions workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax) và [GitHub deployment environments](https://docs.github.com/en/actions/concepts/workflows-and-actions/deployment-environments)
- [Docker Compose bind mounts và service config](https://docs.docker.com/reference/compose-file/services/) và [Docker login qua stdin](https://docs.docker.com/reference/cli/docker/login/)
- [Docker Official Image PostgreSQL và init database](https://hub.docker.com/_/postgres)
