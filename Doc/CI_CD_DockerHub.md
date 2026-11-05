# Phát hành Docker Hub với Unity build cục bộ

Theo quyết định ngày 05/11/2026: máy có license build **Linux x86_64 Dedicated Server**;
GitHub Actions chỉ build/push Docker. Binary được gửi bằng **GitHub Release asset**,
không commit vào Git. Cache `Library` trên máy được tái sử dụng; CI không tải Unity Editor,
không chạy GameCI và không cần Unity Secrets.

## Luồng phát hành

1. Review và commit mã nguồn. Script từ chối working tree còn thay đổi, kể cả file mới.
2. Build dedicated trong Editor có license, đóng gói `.tar.gz` và SHA-256 theo full commit.
3. Khi gọi `-Publish`, script push `main` không force, tạo draft release
   `server-build-<full-commit>`, tải lên binary/checksum rồi mới publish draft.
4. Sự kiện `release.published` chạy workflow. Workflow kiểm tra tag trỏ đúng commit thuộc
   `main`, chạy kiểm thử, tải asset và kiểm tra checksum, metadata Unity/protocol cùng ELF x86_64.
5. Build/push bốn image candidate với cùng mã `<commit>-<run-id>-<attempt>`.
6. Khởi động stack CI dùng dữ liệu tạm, kiểm tra API, AI, Nginx và heartbeat worker.
7. Chỉ khi đạt, xuất artifact `deployment-<mã>` chứa bộ Compose ghim digest và checksum.
   Các alias `api`, `ai`, `web`, `server` chỉ được cập nhật nếu source vẫn là HEAD `main`.

Push/PR `main` chỉ chạy kiểm thử, không publish image. Một commit chưa có native release
không tự dùng binary cũ. Run lỗi có thể để lại tag candidate trên Docker Hub; chỉ dùng bộ
deployment của run thành công. Các alias không cập nhật nguyên tử, vì vậy Ubuntu dùng digest
trong bộ deployment, không ghép các alias. Các run publish dùng chung concurrency lock;
GitHub chỉ giữ run đang chạy và run chờ mới nhất, không bảo đảm hàng đợi FIFO cho mọi run.

## Chuẩn bị một lần

Máy Windows build:

- Unity đúng version trong `My project/ProjectSettings/ProjectVersion.txt` (hiện `6000.6.2f1`),
  license hợp lệ, module **Linux Dedicated Server Build Support** và backend Mono.
- Unity CLI beta.16 trở lên; project đã có package `com.unity.pipeline`.
- Git, Windows `tar.exe`, PowerShell và GitHub CLI `gh`.
- Đăng nhập GitHub CLI bằng tài khoản có quyền push code và tạo release cho repository:

```powershell
gh auth login --hostname github.com
gh auth setup-git
```

GitHub repository:

- Có Docker Hub repository **`tadminm/pickelverse`**.
- Tạo Docker Hub access token có quyền Read/Write, thêm vào
  **Settings → Secrets and variables → Actions → Repository secrets** với tên **`DOCKERHUB_TOKEN`**.
- Không cần `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_LICENSE` hoặc `UNITY_SERIAL` cho workflow này.
  Có thể xóa các Unity Secrets trước đây nếu không workflow nào khác sử dụng.
- Đưa workflow và script vào `main` trước lần phát hành đầu. Actions dùng `contents: read`;
  việc tạo/publish native release do `gh` trên máy build thực hiện.

## Build và push từ Windows

Trong `D:\Repository\PickleVerse`, review/commit các thay đổi cần phát hành trước.
`git status --short` phải không có dòng nào. Script không tự stage, commit hay stash.
Không chạy Play Mode hoặc sửa code trong khi build.

Chỉ build/đóng gói để xem trước, chưa push:

```powershell
.\scripts\Publish-DockerHubRelease.ps1
```

Sau đó phát hành đúng gói vừa build, không build lại:

```powershell
.\scripts\Publish-DockerHubRelease.ps1 -Publish -SkipBuild
```

Hoặc build và phát hành trong một lệnh:

```powershell
.\scripts\Publish-DockerHubRelease.ps1 -Publish
```

`-Publish` yêu cầu branch `main` và `origin` thuộc github.com. Nếu main có branch protection,
merge PR trước, cập nhật main cục bộ rồi build commit đã merge; script không vượt bảo vệ nhánh.
Unity CLI tái sử dụng Editor đang mở và giữ Editor đó chạy; nếu chưa mở, CLI khởi động Editor
headless. Helper chọn hai scene server, loại App UI native plugin, chỉ bật HTTP nội bộ trong
build server và khôi phục thiết lập build sau khi xong. Đầu ra mới dùng thư mục riêng mỗi lần,
không trộn với lần build trước.

Gói nằm tại `artifacts/native-server/packages/<commit>/`; thư mục artifacts được Git bỏ qua.
Script kiểm tra lại HEAD, working tree, metadata và checksum trước khi push. Nếu Unity làm
thay đổi source/asset trong build, script dừng để bạn kiểm tra; không tự xóa thay đổi đó.
`-SkipBuild` chỉ dùng gói đúng commit hiện tại, không dùng build cũ cho commit mới.

Nếu upload gián đoạn khi release còn draft, chạy lại `-Publish -SkipBuild`: script tải lại
hai asset trong draft rồi publish. Release đã publish không bị thay binary. Để thử lại CI,
vào **Actions → Build and publish Docker Hub images → Run workflow**, chọn **main** và nhập
`server_release = server-build-<full-commit>`. Mỗi lần thử có mã image mới theo run/attempt.

## Lấy bộ deployment xuống Ubuntu

Sau khi workflow thành công, tải artifact `deployment-<mã>` ở cuối trang run. Giải nén ZIP
artifact để lấy `PickleVerse-deployment-<mã>.tar.gz` và `.sha256`, chuyển hai file đó sang Ubuntu.
Bộ này nhỏ: binary nằm trên Docker Hub. Đây là gói đã vượt smoke test, khác native release
đầu vào chỉ chứa binary.

Trong thư mục đã nhận hai file, thay `<mã>` bằng mã thực của run:

```bash
sha256sum -c PickleVerse-deployment-<mã>.tar.gz.sha256
mkdir -p release
tar -xzf PickleVerse-deployment-<mã>.tar.gz -C release
cd release
cp .env.example .env
```

Chỉnh `.env` theo máy Ubuntu: JWT, SMTP, AI service key, dedicated service key và các biến
cấu hình cần thiết. Đặt `PKV_DEDICATED_ENABLED=true` và một `PKV_GAME_SERVER_KEY` riêng.
`release.env` chỉ chứa repository/mã phát hành, không chứa secret. Nếu thay stack đang có,
dùng đúng thư mục/project Compose của stack đó và chuyển `.env` của bạn vào thư mục release;
việc reset dữ liệu development thực hiện riêng theo hướng dẫn vận hành hiện có.

```bash
docker compose --env-file .env --env-file release.env \
  -f docker-compose.yml -f docker-compose.dedicated.yml \
  -f docker-compose.hub.yml -f docker-compose.release.yml \
  --profile dedicated pull

docker compose --env-file .env --env-file release.env \
  -f docker-compose.yml -f docker-compose.dedicated.yml \
  -f docker-compose.hub.yml -f docker-compose.release.yml \
  --profile dedicated up -d --no-build

docker compose --env-file .env --env-file release.env \
  -f docker-compose.yml -f docker-compose.dedicated.yml \
  -f docker-compose.hub.yml -f docker-compose.release.yml \
  --profile dedicated ps
```

Yêu cầu Compose ≥ 2.24.4. Thêm `--profile admin` cạnh `--profile dedicated` để bật web admin.
Web mặc định chỉ mở `127.0.0.1:3000`; đặt reverse proxy/TLS hoặc SSH tunnel khi dùng từ xa.
Nginx dùng `API_UPSTREAM=http://backend-api:8080`, hỗ trợ route SPA `/login` và proxy `/api`.
Không sửa `image` thành alias chung. Rollback image bằng bộ deployment cũ tương thích với
schema hiện tại; workflow không tự thay database hoặc tự deploy lên Ubuntu.

## Phạm vi kiểm thử

Workflow kiểm tra API/client contract không cần database, Python rules, frontend build,
độ nhất quán gói phát hành và stack Docker mới khởi tạo từ `src/Database.sql`.
Smoke test kiểm tra readiness API, SPA/auth proxy, AI có service authentication và heartbeat
worker. Stack CI có project riêng `pkv-ci-*`, không dùng volume/database development.
Đây chưa phải kiểm thử trận Photon 1v1/2v2, animation, reconnect hay đo hiệu năng gameplay.

Kiểm chứng cục bộ 05/11/2026: xem phần bàn giao CI cùng ngày trong
`Remember/PickleVerse_Codex_Handover.md`. Chưa coi việc kiểm tra local là một run GitHub
hoặc một lần publish Docker Hub thành công.

## Nguồn chính thức

- [Docker Build với GitHub Actions](https://docs.docker.com/build/ci/github-actions/)
- [GitHub sự kiện release.published](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#release)
- [GitHub CLI tạo release và tải asset](https://cli.github.com/manual/gh_release_create)
- [GitHub CLI publish draft](https://cli.github.com/manual/gh_release_edit)
- [Unity 6000.6 Dedicated Server build](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/dedicated-server/build)
