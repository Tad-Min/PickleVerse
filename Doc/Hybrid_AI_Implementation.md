# Hybrid AI — triển khai và nghiệm thu

## Phân chia trách nhiệm

- Unity/Fusion chạy Behavior Tree và contact; adaptive practice và bot online dùng chung graph qua hai adapter. Profile bot được chụp khi bắt đầu trận. Các mức khó cố định được giữ lại.
- Bot giao bóng không áp dụng xác suất đánh hụt và không rút số ngẫu nhiên của cú trả bóng. Tỷ lệ đánh hụt theo Elo chỉ áp dụng cho các cú trả bóng trong rally, cùng policy ở local và online; contact/luật sân vẫn được kiểm tra bởi game.
- ASP.NET xác thực, quản lý trận, Elo chuẩn, telemetry và toàn bộ dữ liệu bền vững.
- FastAPI phân tích một phiên mỗi yêu cầu, trả findings, metrics, tối đa ba drill và tier có độ tin cậy. Python không truy cập database.
- PostgreSQL giữ parts, job/lease/retry, kết quả từng phiên, profile coaching và rating ledger. Redis giữ matchmaking/cache.

## Bật dịch vụ theo giai đoạn

1. Khởi tạo database phát triển mới trực tiếp từ `src/Database.sql`. Docker Compose dùng volume `pickleverse_dev_postgres_data` (đổi qua `POSTGRES_VOLUME` trong `.env`) và tự chạy toàn bộ SQL khi volume trống. Schema game, sáu bảng Hybrid và seed được tạo chung trong một transaction. Khi thay đổi schema trong giai đoạn phát triển, sửa SQL khởi tạo và dùng một volume mới.
2. Tạo secret riêng `AI_SERVICE_KEY` tối thiểu 32 ký tự trong môi trường; giữ các cờ `AI_INGEST_ENABLED`, `AI_DISPATCHER_ENABLED`, `AI_COACHING_ENABLED`, `AI_ENABLE_MODEL` ở `false`.
3. Build/start `ai-service` và `backend-api` bằng Docker Compose. FastAPI không publish cổng ra máy host.
4. Bật ingest và dispatcher để kiểm tra shadow; chỉ bật coaching sau nghiệm thu. Catalog truyền cờ xuống client nên client cũ hoặc cấu hình tắt không gửi thêm telemetry.
5. Bật classifier riêng khi có artifact đáp ứng gate. `AI_ENABLE_MODEL` điều khiển cả việc gửi dữ liệu onboarding từ backend và việc load model Python. Volume model chỉ đọc.

Elo mới dùng K=32, hệ số 400, làm tròn midpoint away from zero, sàn 100. Điểm/ngưỡng hạng/lịch sử giữ nguyên. Phiên bản được backend chụp lúc tạo trận; bản ghi đang mở không có phiên bản tiếp tục dùng legacy-v1. Client không chọn công thức, điểm đối thủ hoặc delta. Bot dự phòng vẫn được tính Elo theo rating đã chụp lúc cấp trận. Ledger và progression được ghi cùng transaction.

## Request và độ bền dữ liệu

- Không gọi AI trong simulation hoặc theo từng cú đánh. Client lưu bản ghi kể cả cú hụt, checkpoint spool ngoài bước simulation và upload sau phiên đã được backend xác nhận.
- Gói tối đa 1.000 records hoặc 1 MiB/phần. Gói lớn có nhiều phần, cùng batch ID; backend chỉ tạo job khi đủ phần. Payload lớn được gzip; giới hạn 1 MiB cũng áp dụng sau giải nén.
- Client chỉ xóa spool sau ACK của tất cả phần, và chỉ upload bằng đúng tài khoản. Retry giữ IDs và nội dung; payload khác với phần đã ACK nhận 409.
- Online: host thu các quan sát từ physics/contact vào record trong bộ nhớ. Sau rally, `LateUpdate` gửi record qua kênh Fusion tin cậy về đúng tài khoản; client checkpoint spool rồi gửi HTTP sau phiên. Tối đa hai record/frame và nhường kênh khi có từ 12 lệnh chờ để giữ chỗ cho gameplay. Marker cuối chỉ đóng spool sau toàn bộ record; authority và batch/session/account được kiểm tra khi nhận. Không quy lỗi đánh hụt cho một đồng đội tùy ý trong doubles.
- Training checkpoint được gom ở 30 giây khi có thay đổi, cộng thêm pause/rời bài/hoàn thành. Telemetry không có quyền cấp XP, thưởng hay Elo.
- Worker có hai lane, HTTP timeout 15 giây, lease 60 giây, tối đa 10 lần thử với backoff. Request phân tích được đóng băng trong job. Commit kết quả/profile/log coaching và hoàn tất job là một transaction.
- Raw parts và frozen request được dọn sau 30 ngày; metrics, recommendation và lịch sử aggregate được giữ. Findings chi tiết cũ được rút gọn. Profile quá 30 ngày dùng curriculum fallback.
- Khi bật Hybrid, API client tự khai báo `log-error` trả 410. Mastery luyện tập vẫn chỉ lấy từ training được backend chấp nhận; match analytics không ghi đè mastery.

## Phản hồi và model

Timing có dấu, contact/feet, volley/kitchen, landing và net clearance được ghi khi quan sát được. Thiếu số liệu không đồng nghĩa mắc lỗi. Aim do engine sinh hoặc timing được hỗ trợ không được dùng làm bằng chứng độc lập. Trong game hiện tại, các metric này có thể thiếu biến thiên để đánh giá chính xác một số kỹ thuật; không tạo số liệu giả để đủ feature.

Rule engine hỗ trợ early/late, late serve, kitchen/momentum, net/out, vùng đích và hồi vị. Dink quá cao có gate hiệu chỉnh bằng fixture. Recommendation dùng tối đa 10 phiên/30 ngày, tỷ lệ lỗi trên cơ hội quan sát, severity và recency; cần tối thiểu năm cơ hội và tôn trọng prerequisite.

Không có model production hoặc nhãn chuyên gia trong repo. Pipeline huấn luyện và gate đã được bổ sung; xem `src/AiService/README.md`. Tier chỉ hỗ trợ bài tập/độ khó adaptive practice, không ghi đè Elo.

## Kiểm thử

Dùng PostgreSQL/Redis riêng và database tạm của IntegrationChecks. Không dùng dữ liệu production. Biến môi trường:

```powershell
$env:PKV_TEST_POSTGRES = '<connection string tới PostgreSQL kiểm thử>'
$env:PKV_TEST_REDIS = '<Redis kiểm thử>'
$env:PKV_TEST_AI_URL = 'http://127.0.0.1:18080'
dotnet run --project src/Testing/Backend -- --check-hybrid
dotnet run --project src/Testing/Backend -- --check-hybrid-http
```

Fixture HTTP/worker dùng service key gồm 32 ký tự `k`, chỉ dành cho test cục bộ. FastAPI kiểm thử phải dùng cùng key; không dùng key này khi triển khai.

`--check-hybrid` tạo database tạm bằng chính `src/Database.sql`, kiểm tra đủ các bảng Hybrid, ingest, ownership, replay/conflict, retry, lease recovery, ledger chuẩn/legacy, bỏ qua rating client giả và ticket ngoài trang 512. `--check-hybrid-http` mở Kestrel tạm, kiểm tra JWT/controller thật và tải đều/burst. Benchmark được ghi tại `artifacts/verification/hybrid-http-benchmark.json`; kết quả là số đo máy chạy test, không đại diện Android.

Unity: gọi `HybridAiEditorVerification.RunDirect("HybridAiTests,BotContactChanceTests,AdaptiveBotGraphTests,NetworkCoachingTests")` cho assertion Elo, contact chance, graph/seed/luật, p99 desktop và telemetry online. Helper chạy assertion trực tiếp nhằm giữ scene đang mở và các thay đổi chưa lưu. Chỉ dùng Test Runner chuẩn khi scene đã được người dùng chủ động xử lý; không tự lưu hoặc bỏ thay đổi Menu.

Nghiệm thu thiết bị thật vẫn cần kiểm tra màn hình coaching, reconnect/app kill, gameplay nhiều rally và p99 trên Android ARM64. Mục tiêu đầu: ingest p95 <250 ms, coaching p95 <5 giây ở tải một phiên/giây, 1.000 records/phiên; burst 100 phiên phải xử lý hết dữ liệu đã ACK.

## Kết quả kiểm chứng cục bộ — 27/10/2026

| Kiểm tra | Kết quả |
|---|---|
| Build backend và IntegrationChecks | Thành công, 0 lỗi; còn cảnh báo nullable trong mã model dùng chung |
| Python rule engine, HTTP contract và classifier pipeline | 11/11 đạt; dữ liệu ML trong test là fixture tổng hợp |
| Hybrid ingest, worker, khởi tạo database, Elo ledger và Redis | 32/32 đạt trên PostgreSQL/Redis kiểm thử riêng |
| Hồi quy game online, training, replay và social | 76/76 đạt |
| Hồi quy activity/ranked/bot dự phòng | 22/22 đạt |
| HTTP JWT/controller và tải steady/burst | 6/6 đạt |
| Unity biên dịch và assertion trực tiếp | 24/24 đạt; Menu giữ nguyên trạng thái chưa lưu |
| Container Python 3.12 Linux | Build thành công; readiness và phân tích LateServe qua HTTP đạt |

Đo trên máy phát triển Windows, backend .NET 10.0.12, PostgreSQL 16 và Redis 7.4 trong Docker; FastAPI chạy cục bộ. Benchmark dùng connection pooling như production, loopback và gzip cho telemetry. Không bao gồm độ trễ Internet:

| Kịch bản | Số đo |
|---|---|
| 10 phiên steady, 1 phiên/giây, 1.000 records/phiên | Ingest p95 **58,34 ms**, coaching ready p95 **854,73 ms** |
| Burst 100 phiên, 200 records/phiên | Ingest p95 **425,54 ms**, giải phóng backlog **3,64 giây** |
| Toàn bộ lượt tải gồm hai warm-up | **112 ACK / 112 completed**, không mất phiên đã ACK |
| 1.000 bước bot, graph thật và `BotMatch.PredictContact` thật trên state cố định | p99 **0,118 ms**, Windows Editor |

Artifact số đo nằm trong `artifacts/verification/hybrid-http-benchmark.json`, `hybrid-bot-desktop.json`, `hybrid-unity-direct.json` và `hybrid-python-tests.xml`. Đây là baseline cục bộ, chưa chứng minh SLO production hay ngân sách Android.

Collector local/training và host online đã gắn input, contact, miss, landing, net clearance và fault. Phần online có assertion cho idempotency, account/session, first bounce và giới hạn thông điệp; đối chiếu hai adapter qua nhiều rally trên mạng thật vẫn cần nghiệm thu gameplay. UI coaching, hàng đợi sau app kill và mạng thiết bị thật cũng chưa được nghiệm thu thủ công. Các cờ ingest/dispatcher/coaching/model vì vậy vẫn mặc định tắt.

Pha classifier đã có pipeline, manifest/hash, player split, gate đánh giá và cơ chế từ chối. Việc huấn luyện và bật model production còn chờ nhãn kỹ thuật độc lập cùng holdout tối thiểu mười người mỗi tier; không dùng model fixture để thay thế gate này.

## Vận hành

Meter `PickleVerse.HybridAi` cung cấp thời gian từng stage ingest, thời gian phân tích, số job hoàn tất và retry. Log chỉ chứa batch ID, attempt và loại lỗi/SQLSTATE, không chứa telemetry, token hay service key.

Theo dõi job theo `state`, `availableat`, `leaseuntil`, `attempts`, `lasterror`; cảnh báo backlog tăng, failed jobs, model unavailable hoặc latency vượt mục tiêu. Failed job có thể được vận hành đưa lại về pending, reset attempts/lease sau khi sửa nguyên nhân, với điều kiện raw parts còn đủ trong thời hạn lưu trữ.

Rollback bằng cách tắt các cờ ingest/dispatcher/coaching/model. Không xóa dữ liệu đã ACK; không đổi policy của trận đang mở. Trận mới có thể chọn lại `HybridAi:RatingPolicyVersion=legacy-v1` nếu cần rollback công thức.
