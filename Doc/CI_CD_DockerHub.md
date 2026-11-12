# CI/CD PickleVerse trên GitHub Actions

## Luồng phát hành

Workflow `Build, publish and deploy PickleVerse` chạy khi push vào `main`. Nó kiểm tra backend, FastAPI và web admin, đồng thời dùng GameCI để build Unity Dedicated Server Linux x86-64 trực tiếp từ commit đó. Job publish xác minh ELF, phiên bản Unity, protocol và checksum trước khi build bốn image `api`, `ai`, `web`, `server` trong `tadminm/pickelverse`.

Mỗi lượt publish dùng tag `<component>-<commit>-<run_id>-<attempt>`. API image chứa protocol của Dedicated Server cùng lượt build. Sau smoke test, phần Summary trên GitHub Actions hiển thị `DOCKER_REPOSITORY` và `DOCKER_TAG_RELEASE`. Job deploy gửi `docker-compose.linux.yml` và `src/Database.sql` từ commit của lượt build qua SSH. Sau khi kiểm tra cấu hình, job cài các file còn thiếu, thay file nếu nội dung khác, cập nhật hai giá trị trong `.env` rồi chạy Docker Compose. GitHub chạy nối tiếp các job deploy. Lượt build của commit cũ bỏ qua deploy nếu `main` đã tiến lên.

Pull request chạy validation. **Run workflow** từ `main` có thể build và publish lại. Chọn `deploy` nếu muốn lượt chạy thủ công cũng deploy. Thiết lập GitHub Environment `development` cho nhánh `main` và để job chạy tự động theo lựa chọn đã chốt.

## GitHub Secrets và Variables

Thêm giá trị trong **Settings → Secrets and variables → Actions**. Dùng repository secrets cho Unity và token publish. Có thể đặt thông tin SSH trong environment `development` để chỉ job deploy nhận chúng.

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
3. Điền các khóa `POSTGRES_PASSWORD`, `S3_ACCESS_KEY`, `S3_SECRET_KEY`, `JWT_KEY`, `AI_SERVICE_KEY` và `PKV_GAME_SERVER_KEY`. Khi giữ dữ liệu hiện có, dùng thông tin đăng nhập và thư mục dữ liệu của stack cũ. Ba key JWT, AI và Dedicated cần tối thiểu 32 ký tự. Dùng mật khẩu PostgreSQL không chứa dấu nháy kép hoặc xuống dòng để dùng được trong connection string.
4. Đặt `PKV_DATA_ROOT` trong `.env` và tạo sẵn các thư mục dưới đây. Với thư mục `GameServerData` mới, cấp quyền ghi cho UID/GID `10001:10001` của Dedicated Server.

| Dữ liệu | Thư mục host | Điểm mount trong container |
| --- | --- | --- |
| PostgreSQL | `PostgresData` | `/var/lib/postgresql/data` |
| Redis | `RedisData` | `/data` |
| S3 | `S3Data` | `/data` |
| AI artifact | `AiService/models` | `/models`, chỉ đọc |
| Dedicated | `GameServerData` | `/home/unity/.config/unity3d` |

## Cập nhật phiên bản

Khi cập nhật thủ công, chép `docker-compose.linux.yml` và `src/Database.sql` từ commit cần chạy, điền hai giá trị trong Summary của lượt build đã qua smoke test vào `.env`, rồi chạy lệnh sau trong thư mục deploy.

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
