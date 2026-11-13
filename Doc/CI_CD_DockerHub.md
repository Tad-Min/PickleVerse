# CI/CD PickleVerse trên GitHub Actions

## Luồng phát hành

Workflow `Build, publish and deploy PickleVerse` giữ một file Compose trên Ubuntu. Trên PR, workflow so với commit của nhánh đích để chọn các bài kiểm tra backend, FastAPI và TypeScript. Khi push vào `main`, workflow đọc commit đã triển khai thành công gần nhất trên Ubuntu để quyết định build lại image nào. Ví dụ, sửa giao diện admin chỉ build image web và giữ nguyên image API, AI và Dedicated. Thay đổi protocol build cả API và Dedicated. Các thay đổi chỉ có tài liệu không build hay deploy khi đã có mốc phát hành.

Mỗi image mới có tag `<component>-<commit>-<run_id>-<attempt>` trong `tadminm/pickelverse`. Job publish ghim bốn image bằng digest, chạy smoke test với đúng tổ hợp đó và đối chiếu protocol giữa API và Dedicated. Phần Summary hiển thị `PKV_API_IMAGE`, `PKV_AI_IMAGE`, `PKV_WEB_IMAGE` và `PKV_GAME_SERVER_IMAGE`.

Job deploy gửi `docker-compose.linux.yml` và `src/Database.sql` qua SSH, pull image cần thiết, xác minh bản backup PostgreSQL rồi thay bốn tham chiếu image trong `.env`. Các mật khẩu và cấu hình do bạn đặt vẫn giữ nguyên. Sau khi xác minh API, web và các container, job mới ghi commit, protocol và bốn digest vào `.deploy/release.json`. Nếu lỗi giữa chừng, lượt sau build lại đầy đủ. Nếu một lượt khác đã cập nhật mốc deploy trong lúc đang build, chạy lại workflow để lập kế hoạch từ mốc mới.

Lần đầu chạy cơ chế này sẽ build cả bốn image để tạo mốc. Dùng **Run workflow** từ `main` và chọn `full_rebuild` khi cần build lại toàn bộ. Chọn thêm `deploy` để cập nhật Ubuntu. Lượt build đầy đủ không deploy có thể chạy mà không cần kết nối SSH.

## GitHub Secrets và Variables

Thêm giá trị trong **Settings → Secrets and variables → Actions**. Dùng repository secrets cho Unity và token publish. Các job đọc mốc deploy và cập nhật máy chủ dùng thông tin SSH trong environment `development`. Job đọc mốc không tạo bản ghi deployment trên GitHub.

| Secret | Giá trị |
| --- | --- |
| `UNITY_EMAIL` | Email tài khoản Unity dùng để kích hoạt CI. |
| `UNITY_PASSWORD` | Mật khẩu tài khoản Unity. |
| `UNITY_LICENSE` | Nội dung đầy đủ file `.ulf` cho Unity Personal theo hướng dẫn GameCI. Chọn đúng một trong secret này và `UNITY_SERIAL`. |
| `UNITY_SERIAL` | Serial Unity Pro hoặc Enterprise. Dùng cho cơ chế cấp phép bằng serial. |
| `DOCKERHUB_TOKEN` | Docker Hub PAT có quyền đọc và ghi `tadminm/pickelverse`. |
| `SSH_HOST` | DNS name hoặc IPv4 mà GitHub-hosted runner có thể truy cập. |
| `SSH_PORT` | Cổng SSH thực tế. |
| `SSH_PASSWORD` | Mật khẩu SSH của tài khoản triển khai. |
| `SSH_KNOWN_HOSTS` | Dòng public host key lấy từ máy Linux qua kênh tin cậy. Với cổng 22 dùng `HOST <key-type> <public-key>`. Với cổng khác dùng `[HOST]:PORT <key-type> <public-key>`. |

Variables tùy chọn gồm `DOCKERHUB_USERNAME` mặc định `tadminm` và `SSH_USER` mặc định `min`. Tài khoản Docker Hub phải có quyền với repository `tadminm/pickelverse`. Workflow dùng `github.token` tự cấp để đối chiếu HEAD của `main`.

Đặt GitHub Actions variable `PKV_DEPLOY_ROOT` thành thư mục deploy trên Linux, ví dụ `/mnt/data/pickleverse`. Dùng đường dẫn tuyệt đối đã tồn tại và không có khoảng trắng. Workflow cũng chấp nhận tên biến GitHub cũ `PKV_DATA_ROOT` khi chưa đặt `PKV_DEPLOY_ROOT`. Đường dẫn mặc định là `/run/media/min/Data/pickleverse`.

Lấy public key qua console đáng tin cậy của Linux, ví dụ `awk '{print $1, $2}' /etc/ssh/ssh_host_ed25519_key.pub`. Giữ đúng kiểu key và hostname/cổng của endpoint. Có thể đối chiếu fingerprint bằng `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub`. OpenSSH trên runner kiểm tra host key trước khi gửi mật khẩu.

## Chuẩn bị Linux một lần

1. Dùng Linux amd64 và Docker Compose có `up --wait-timeout`. Tài khoản SSH cần chạy được Docker và ghi được vào thư mục deploy. Với repository Docker Hub riêng tư, đăng nhập Docker Hub một lần bằng chính tài khoản SSH.
2. Chuẩn bị `.env` tại thư mục deploy đã chọn, ví dụ `/mnt/data/pickleverse`. Dùng `deploy/linux/runtime.env.example` làm mẫu. CD cài `docker-compose.linux.yml` và `src/Database.sql` ở lần chạy đầu nếu các file chưa có.
3. Điền các khóa `POSTGRES_PASSWORD`, `PGADMIN_PASSWORD`, `S3_ACCESS_KEY`, `S3_SECRET_KEY`, `JWT_KEY`, `AI_SERVICE_KEY` và `PKV_GAME_SERVER_KEY`. Khi giữ dữ liệu hiện có, dùng thông tin đăng nhập và thư mục dữ liệu của stack cũ. Ba key JWT, AI và Dedicated cần tối thiểu 32 ký tự. Dùng mật khẩu PostgreSQL không chứa dấu nháy kép hoặc xuống dòng để dùng được trong connection string.
4. Đặt `PKV_DATA_ROOT` trong `.env`. CD tạo các thư mục còn thiếu dưới đường dẫn này và cấp quyền ghi cho UID/GID `10001:10001` khi tạo mới `GameServerData`. Nếu tự chạy Compose lần đầu, chuẩn bị các thư mục và quyền ghi tương ứng.

| Dữ liệu | Thư mục host | Điểm mount trong container |
| --- | --- | --- |
| PostgreSQL | `PostgresData` | `/var/lib/postgresql/data` |
| Redis | `RedisData` | `/data` |
| S3 | `S3Data` | `/data` |
| AI artifact | `AiService/models` | `/models`, chỉ đọc |
| Dedicated | `GameServerData` | `/home/unity/.config/unity3d` |

## HTTPS và sao lưu

`deploy/linux/Caddyfile` phục vụ admin tại `https://tad-min.io.vn/login` và chuyển tiếp `/api/*` cùng `/health/ready` tới API. Caddy chạy trên host hoặc dùng mạng host để truy cập API và web ở `127.0.0.1:8080` và `127.0.0.1:3000`. Caddy giữ chứng chỉ trong vùng lưu trữ riêng. Cài và nạp cấu hình Caddy một lần trên host. Nếu đổi `API_PORT` hoặc `WEB_PORT`, cập nhật địa chỉ đích trong Caddyfile tương ứng. Unity client dùng `https://tad-min.io.vn` làm Base Url.

CD lưu bản dump PostgreSQL dạng custom tại `<PKV_DEPLOY_ROOT>/.deploy/backups/` với quyền truy cập giới hạn cho tài khoản deploy. Bước kiểm tra đọc toàn bộ archive bằng `pg_restore` mà không ghi vào database. Lần cài mới với thư mục PostgreSQL trống được phép chạy khi chưa có container PostgreSQL. Nếu đã có dữ liệu mà PostgreSQL không chạy, khởi động đúng service trước khi deploy. Quản lý dung lượng và thời hạn giữ bản sao lưu trên host.

## Cập nhật phiên bản

Ưu tiên cập nhật qua workflow để mốc so sánh phản ánh bản đang chạy. Nếu cập nhật thủ công, chép `docker-compose.linux.yml` và `src/Database.sql` từ commit cần chạy, điền bốn tham chiếu image đã qua smoke test trong Summary vào `.env`, rồi chạy lệnh sau trong thư mục deploy. Sau lần cập nhật thủ công, chọn `full_rebuild` và `deploy` cho lượt CD tiếp theo để ghi lại mốc.

```bash
docker compose -f docker-compose.linux.yml up -d --wait
```

Cờ `-f` chọn file Compose, `-d` chạy container nền và `--wait` chờ dịch vụ chạy hoặc đạt kiểm tra sức khỏe. Khi job deploy báo `Development release active`, Compose đã hoàn thành bước này.

PostgreSQL dùng `src/Database.sql` khi khởi tạo thư mục dữ liệu rỗng. Với database đã có dữ liệu, xử lý thay đổi schema theo quy trình database development trước khi deploy bản cần schema mới.

## Tài liệu tham chiếu

- [GameCI Unity Builder](https://game.ci/docs/github/builder/) và [kích hoạt Unity trên CI](https://game.ci/docs/github/activation/)
- [GitHub Actions workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax) và [GitHub deployment environments](https://docs.github.com/en/actions/concepts/workflows-and-actions/deployment-environments)
- [Docker Compose bind mounts và service config](https://docs.docker.com/reference/compose-file/services/) và [Docker login qua stdin](https://docs.docker.com/reference/cli/docker/login/)
- [Docker Official Image PostgreSQL và init database](https://hub.docker.com/_/postgres)
