# Chạy thử PickleVerse trên Ubuntu bằng Docker Hub

Tài liệu lưu quy trình bàn giao gói ngày 03/11/2026. Quy trình hiện hành dùng GitHub Actions và bind mount bền vững. [CI/CD PickleVerse](CI_CD_DockerHub.md)

Repository: `tadminm/pickelverse`. Phiên bản: `20261103-p18-53871c6-hub1`, protocol 18, Linux amd64. Ba tag là `api-20261103-p18-53871c6-hub1`, `ai-20261103-p18-53871c6-hub1`, `server-20261103-p18-53871c6-hub1`. Dùng cùng bộ phiên bản; không đổi các tag `latest`/`ai` cũ.

Ubuntu chỉ cần Docker/Compose >=2.24.4 và gói cấu hình này. Mã backend/AI và Linux dedicated nằm trong image; không cài Unity Editor, Python hoặc .NET SDK trên Ubuntu. Windows client vẫn chạy riêng trên máy người chơi.

## 1. Xóa dữ liệu development cũ, không backup

Người dùng đã yêu cầu xóa toàn bộ dữ liệu development của riêng PickleVerse, không backup và không migration. Lệnh này xóa tài khoản/tiến trình/replay/cache trong volume Docker của stack cũ; giữ source và `.env` để tái sử dụng thông tin SMTP. Không xóa dữ liệu Docker của dự án khác.

Chạy trên Ubuntu khi container `pickleverse_api` và `pickleverse_postgres` cũ còn tồn tại:

```bash
PKV_DIR="$(docker inspect pickleverse_api --format '{{ index .Config.Labels "com.docker.compose.project.working_dir" }}')"
export COMPOSE_PROJECT_NAME="$(docker inspect pickleverse_api --format '{{ index .Config.Labels "com.docker.compose.project" }}')"
printf 'Thư mục: %s\nProject: %s\n' "$PKV_DIR" "$COMPOSE_PROJECT_NAME"

(
  set -euo pipefail
  test -n "$PKV_DIR"
  test -n "$COMPOSE_PROJECT_NAME"
  cd "$PKV_DIR"
  test -f docker-compose.yml
  test "$(docker inspect pickleverse_postgres --format '{{ index .Config.Labels "com.docker.compose.project" }}')" = "$COMPOSE_PROJECT_NAME"
  test "$(docker inspect pickleverse_api --format '{{range .Config.Env}}{{if eq . "ASPNETCORE_ENVIRONMENT=Development"}}development{{end}}{{end}}')" = development
  test "$(docker exec pickleverse_postgres psql -U sa -d pickleverse -Atqc 'SELECT current_database()')" = pickleverse
  files=(-f docker-compose.yml)
  if [ -f docker-compose.dedicated.yml ]; then files+=(-f docker-compose.dedicated.yml); fi
  if [ -f docker-compose.hub.yml ]; then files+=(-f docker-compose.hub.yml); fi
  docker compose -p "$COMPOSE_PROJECT_NAME" "${files[@]}" --profile '*' down --volumes --remove-orphans
)
```

Chỉ tiếp tục khi khối lệnh hoàn tất thành công. Nếu stack cũ đã được xóa, dùng đúng thư mục/project đã xác định ở lượt trước; không chạy lại phần đọc label container đã mất. Giữ cùng terminal để giữ biến môi trường.

## 2. Chép và giải nén gói cấu hình

Chép file `PickleVerse-DockerHub-20261103-p18-53871c6-hub1.tar.gz` và `.sha256` vào `~/pickleverse-upload` bằng SCP/WinSCP. Đây là gói cấu hình nhỏ, không phải gói Unity Linux cũ.

```bash
cd "$HOME/pickleverse-upload"
sha256sum -c PickleVerse-DockerHub-20261103-p18-53871c6-hub1.tar.gz.sha256
tar -xzf PickleVerse-DockerHub-20261103-p18-53871c6-hub1.tar.gz -C "$PKV_DIR"
cd "$PKV_DIR"
```

Nếu không còn `.env`, chạy `cp .env.example .env`. Gói không chứa credential hoặc `.env` thật.

## 3. Cấu hình môi trường

Mở `nano .env`, giữ/cấu hình SMTP và JWT; sửa các biến (không để trùng key):

```dotenv
PKV_DOCKER_REPOSITORY=tadminm/pickelverse
PKV_RELEASE=20261103-p18-53871c6-hub1
PKV_DEDICATED_ENABLED=false
API_PORT=8080
PKV_API_BIND=0.0.0.0
AI_INGEST_ENABLED=true
AI_DISPATCHER_ENABLED=true
AI_COACHING_ENABLED=true
AI_ENABLE_MODEL=false
PKV_GAME_CPUS=2
PKV_GAME_MEMORY=2g
```

`JWT_KEY`, `AI_SERVICE_KEY`, `PKV_GAME_SERVER_KEY` phải có khóa riêng ít nhất 32 ký tự. Sinh mỗi khóa bằng `openssl rand -hex 32`. `SMTP_USERNAME`, `SMTP_APP_PASSWORD`, `SMTP_FROM_EMAIL` phải đúng để đăng ký/reset nhận OTP. SMTP mặc định Gmail/587, dùng App Password. Image API không chứa credential phát triển; Compose truyền chúng từ `.env`.

Nếu có reverse proxy trên host, giữ endpoint/proxy hiện tại và dùng `PKV_API_BIND=127.0.0.1`. Nếu client truy cập trực tiếp cổng 8080, dùng `0.0.0.0` như trên.

## 4. Pull image và khởi tạo database mới

```bash
dc() {
  docker compose -p "$COMPOSE_PROJECT_NAME" -f docker-compose.yml -f docker-compose.dedicated.yml -f docker-compose.hub.yml "$@"
}

dc --profile dedicated config --quiet
dc --profile dedicated pull postgres redis s3-storage backend-api ai-service game-server
dc up -d --no-build --wait postgres redis s3-storage
dc logs --tail=100 postgres
```

Nếu repository private, chạy `docker login --username tadminm` bằng tài khoản có quyền pull trước bước `pull`. Không đưa token vào chat.

Volume PostgreSQL mới sẽ tự chạy `src/Database.sql` đã bind vào `/docker-entrypoint-initdb.d/01_init.sql`. Không chạy migration hoặc script reset có backup. Kiểm tra sau khi khởi tạo xong:

```bash
docker exec pickleverse_postgres psql -U sa -d pickleverse -c "SELECT to_regclass('public.users') AS users, to_regclass('public.passwordresetchallenges') AS password_reset;"
```

Cả hai tên bảng phải hiện, không rỗng. API readiness chỉ kiểm tra kết nối PostgreSQL/Redis; kiểm tra schema này là bước riêng.

## 5. API/AI, rồi dedicated

```bash
dc up -d --no-build backend-api ai-service
curl --fail http://127.0.0.1:8080/health/ready
dc exec -T ai-service python -c "import urllib.request; print(urllib.request.urlopen('http://localhost:8000/ready').read().decode())"
```

Khi API/AI ready, đổi `PKV_DEDICATED_ENABLED=true` trong `.env`:

```bash
dc up -d --no-build --force-recreate backend-api
curl --fail http://127.0.0.1:8080/health/ready
dc --profile dedicated up -d --no-build game-server
dc --profile dedicated ps
dc logs --tail=100 game-server
docker inspect pickleverse_game_server --format '{{.State.Health.Status}}'
docker exec pickleverse_redis redis-cli TTL 'pickleverse:{dedicated}:worker'
```

Worker cần healthy và TTL dương. Không cần log `Session ready` khi chưa có trận.

## 6. Kiểm tra từ Windows

Client dùng protocol 18, bản sau merge Login mới cần build lại bằng `My project/AgentScripts/ClientBuild.cs`. Cấu hình `BackendApiSettings` trỏ đúng Ubuntu/domain. Development Build hiện cho phép HTTP; bản release yêu cầu HTTPS.

Thử đăng ký/email OTP/quên mật khẩu, sau đó một trận 1v1 và một trận 2v2 nối tiếp. Mốc đầu chỉ một trận đồng thời. Kiểm tra `dc logs -f --tail=100 game-server backend-api` và `docker stats --no-stream`.

Compose publish UDP 3669 và API 8080 theo cấu hình. Nếu test qua Internet, kiểm tra firewall/router/NAT thực tế. Relay vẫn có thể hoạt động; không coi publish port hoặc UFW là bằng chứng Direct. Giữ database/Redis/S3 bind loopback như Compose.

## Phạm vi xác minh image

Đã build lại native Linux bằng Unity 6000.6.2f1 (0 lỗi), API image được xóa credential trong cấu hình trước khi tạo runtime layer và quét cả 8 layer. Smoke Docker Desktop dùng PostgreSQL/Redis/S3/network/volume cách ly: schema mới, API ready, AI ready và worker healthy/heartbeat. AI đạt 11/11 Python tests; HTTP thật kiểm tra thiếu key (401), phân tích hợp lệ (200), payload sai (422). AI dùng lại dependency layer đã đối chiếu requirements/lock và phiên bản, chép toàn bộ app hiện tại; các file Python trong image khớp byte với source. Các secret kiểm thử chỉ truyền lúc chạy container. Đây chưa phải nghiệm thu gameplay trên Ubuntu/NAT; log worker vẫn có cảnh báo Unity TLS ở yêu cầu nền như đợt trước.

## Tham chiếu

- [Docker Compose push](https://docs.docker.com/reference/cli/docker/compose/push/)
- [Docker Compose down/volumes](https://docs.docker.com/reference/cli/docker/compose/down/)
- [Docker Compose merge/override >=2.24.4](https://docs.docker.com/reference/compose-file/merge/)
- [PostgreSQL initialization](https://hub.docker.com/_/postgres)
