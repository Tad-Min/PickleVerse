# AI service development — 31/10/2026

Đã bật ingest, dispatcher và coaching trên Docker development của PickleVerse. Unity gọi backend tại `http://localhost:8080`; FastAPI chỉ phục vụ nội bộ qua `http://ai-service:8000`. Phân tích dùng **rules-v1** và gợi ý bài tập theo curriculum. Model ML vẫn tắt vì chưa có artifact được nghiệm thu.

## Thử trong Unity

1. Dừng rồi chạy lại Play Mode, hoặc khởi động lại client để tải catalog mới. Asset `My project/Assets/Resources/BackendApiSettings.asset` hiện trỏ tới `http://localhost:8080`.
2. Đăng nhập bằng tài khoản của bạn.
3. Hoàn thành một bài luyện tập hoặc một trận và chờ backend xác nhận hoàn tất. Telemetry chỉ được nhận cho phiên hoàn tất, thuộc đúng tài khoản.
4. Về menu **Luyện tập**, tìm mục **GÓP Ý SAU BUỔI CHƠI**, bấm **CẬP NHẬT**. Nếu còn đang tổng hợp, đợi vài giây rồi bấm lại.
5. Kiểm tra góp ý và bài tập gợi ý. Khi chưa có dữ liệu, hệ thống vẫn trả lộ trình luyện tập hiện tại. Số liệu được hỗ trợ/tạo bởi engine và các quan sát bị thiếu không tự trở thành lỗi kỹ thuật của người chơi; không phải phiên nào cũng có findings.

Phản hồi được tổng hợp **sau phiên**, không phải sau mỗi cú đánh. Phân loại mức kỹ năng bằng ML chưa hoạt động.

## Kiểm tra API và dịch vụ

- Swagger: `http://localhost:8080/swagger/index.html`.
- Backend readiness: `http://localhost:8080/health/ready`.
- Sau khi Authorize bằng access token của tài khoản: `GET /api/Game/catalog` phải có `hybridTelemetryEnabled=true`, `hybridCoachingEnabled=true`; `GET /api/AiCoaching/summary` trả trạng thái và phân tích; `GET /api/AiCoaching/recommendation` trả bài tập gợi ý.
- Summary thường chuyển từ `empty` sang `pending`/`processing` rồi `ready`. Khi chưa có kết quả vẫn có gợi ý curriculum.

Chạy tại thư mục repository:

```powershell
docker compose ps
docker compose exec -T ai-service python -c "import urllib.request; print(urllib.request.urlopen('http://localhost:8000/ready').read().decode())"
docker compose logs --tail 80 ai-service backend-api
```

FastAPI `/ready` hiện trả `status=ready`, `ruleVersion=rules-v1`, `model=disabled`. Không cần publish cổng FastAPI để test bằng game hoặc Swagger. Không gửi fixture `contracts/hybrid-ai/session-v1.json` trực tiếp vào endpoint telemetry của backend: contract phân tích và contract upload khác nhau, và backend còn kiểm tra phiên đã hoàn tất.

## Cấu hình đã áp dụng

Các giá trị local lưu trong `.env` được Git bỏ qua; secret AI riêng được tạo ngẫu nhiên và dùng khớp ở hai service. Không đưa secret vào tài liệu hoặc kết quả kiểm tra.

```dotenv
AI_INGEST_ENABLED=true
AI_DISPATCHER_ENABLED=true
AI_COACHING_ENABLED=true
AI_ENABLE_MODEL=false
```

Compose và Dockerfile dùng `/ready` cho healthcheck AI. Trước đây `/health` vẫn báo healthy dù `/ready` trả 503 do thiếu key. Compose giới hạn pool Npgsql của API ở 40 kết nối, đổi qua `POSTGRES_MAX_POOL_SIZE` khi có ngân sách kết nối phù hợp. PostgreSQL development hiện có tổng giới hạn 100; pool mặc định 100 của bài tải đã gây lỗi `53300: too many clients already` khi có kết nối khác.

Hai container được tạo lại bằng image đã có; đã so sánh cả năm file Python trong image với source, khớp sau chuẩn hóa CRLF/LF. Không đổi dependency hay schema, không reset database. Nếu cần khởi động lại với cấu hình hiện tại:

```powershell
docker compose up -d --no-build --no-deps ai-service backend-api
```

Sau khi sửa `.env`, dùng `up -d` để tạo lại container; `restart` không nạp môi trường mới. Để tạm ẩn coaching mà vẫn chạy phân tích nền, đặt `AI_COACHING_ENABLED=false` rồi tạo lại `backend-api`. Để tắt toàn bộ pipeline, đặt cả ingest/dispatcher/coaching thành false rồi tạo lại backend. Giữ `AI_ENABLE_MODEL=false` cho tới khi có model đáp ứng gate trong README FastAPI.

## Kiểm tra đã chạy

| Kiểm tra mới | Kết quả |
|---|---|
| Build backend và IntegrationChecks | Thành công, 0 warning/0 error |
| Python | 11/11 đạt; một cảnh báo deprecation của FastAPI TestClient |
| Hybrid backend, FastAPI fixture riêng | 33/33 đạt; ownership, idempotency, retry, lease recovery, rating |
| HTTP/load sau giới hạn pool | 6/6 đạt; 112 ACK/112 completed |
| Tải steady, 10 phiên × 1.000 records | Ingest p95 41,30 ms; coaching ready p95 1.031,75 ms |
| Burst 100 phiên × 200 records | Ingest p95 190,38 ms; hết backlog 2,88 giây |
| FastAPI container thật | Readiness và phân tích 200; thiếu/sai key 401; rule không hợp lệ 422; fixture trả LateServe/Kitchen |
| API/worker/container thật, bước shadow | 14/14 đạt; job hoàn tất và có phân tích, chưa công bố coaching |
| API/worker/container thật, coaching bật | 16/16 đạt; summary `ready`, findings và recommendation đọc được qua API |
| Swagger/backend readiness | HTTP 200; AI healthy |

Bài tải dùng database tạm và FastAPI fixture riêng tại 18081; số đo loopback không bao gồm UI/Internet. Live smoke dùng tài khoản/trận fixture riêng trên đúng development database `pickleverse` trong container `pickleverse_postgres` và dọn ngay trong `finally`. Database tạm, fixture SQL và tiến trình FastAPI kiểm thử đã được dọn. Kết quả nằm trong `artifacts/ai-service-activation-20261031/`.

Chưa thực hiện UAT gameplay/UI Unity, app kill/reconnect hoặc Android ARM64 trong lượt này. Chưa có kết luận về độ chính xác trên dữ liệu người chơi thật hoặc mục tiêu phản hồi sau point/drill trong một giây. Các bước trong game ở trên là phần để người dùng thử tiếp.
