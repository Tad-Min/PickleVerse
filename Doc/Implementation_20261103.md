# Bàn giao tài khoản, multiplayer, HLV và dedicated — 03/11/2026

Tài liệu lưu cấu hình và kết quả kiểm thử của bản bàn giao ngày 03/11/2026. Với repo hiện tại, xem [hướng dẫn CI/CD](CI_CD_DockerHub.md) để triển khai Ubuntu bằng `docker-compose.linux.yml`.

## Phiên bản và triển khai

Client và dedicated dùng protocol **18**, Unity **6000.6.2f1**. Các client cũ không được ghép chung phòng.
Dedicated mặc định **tắt**; chỉ bật sau khi cập nhật backend/schema và có worker Linux.
Chữ hệ thống mới trong game là tiếng Việt; định danh code/wire giữ nguyên.

Máy Ubuntu nghiệm thu: Pentium G6400, 8 GB RAM, toàn bộ stack Docker hiện có. Giới hạn ban đầu một trận đồng thời; CPU/RAM phải đo thực tế. Chưa nghiệm thu mục tiêu 500 trận.

1. Chép workspace/gói bàn giao sang thư mục project Docker đang dùng trên Ubuntu, giữ nguyên `.env` và Compose project name.
   Gói Ubuntu không chứa `.env`; credential trong bản `appsettings` đóng gói được làm trống. Compose lấy cấu hình thật từ `.env` hiện có. Không ghi đè `.env` bằng `.env.example` khi cập nhật.
2. Compose cần >= 2.24.4. Trong `.env` riêng thêm `PKV_RELEASE=20261103-p18`, `PKV_DEDICATED_ENABLED=false`, và `PKV_GAME_SERVER_KEY` sinh bằng `openssl rand -hex 32`. Không đưa key vào client hoặc Git.
3. Build image API/AI và kiểm tra Compose trước khi dừng dịch vụ:

```bash
docker compose -f docker-compose.yml -f docker-compose.dedicated.yml config --quiet
docker compose -f docker-compose.yml -f docker-compose.dedicated.yml build backend-api ai-service
```

Schema có `Users.AuthVersion`, `PasswordResetChallenges`, `GameMatches.AuthorityMode/ServerHeartbeatAt`. Database Ubuntu đã được xác nhận là development. Script dưới đây kiểm tra đúng instance/database và môi trường Development, sao lưu, dừng API/worker rồi tạo lại; chỉ xóa cache `pickleverse:*`, không chạy `down -v` hoặc xóa volume các dự án khác.

```bash
bash scripts/reset-development-database.sh --reset-pickleverse-development
docker compose -f docker-compose.yml -f docker-compose.dedicated.yml up -d backend-api ai-service
curl --fail http://127.0.0.1:8080/health/ready
```

4. Kiểm thử phase tài khoản/HLV với Editor mới. Lần đăng nhập cũ hết hiệu lực sau cập nhật schema. OTP reset dùng SMTP hiện có; không dùng mã reset để xác thực email.
5. Linux build đã có `PickleVerseServer`, `UnityPlayer.so` và thư mục dữ liệu (bao gồm `PickleVerseServer_Data/MonoBleedingEdge`) trong `artifacts/account-multiplayer-ai-20261103/linux-server`. Bật dedicated:

```bash
docker compose -f docker-compose.yml -f docker-compose.dedicated.yml --profile dedicated build game-server
# Đổi PKV_DEDICATED_ENABLED=true trong .env, rồi tạo lại API để nạp cấu hình.
docker compose -f docker-compose.yml -f docker-compose.dedicated.yml --profile dedicated up -d backend-api game-server
docker compose -f docker-compose.yml -f docker-compose.dedicated.yml logs --tail=100 game-server
docker stats --no-stream
```

UFW đã mở UDP 3669. Docker cũng publish 3669/udp; cần xác minh NAT/router bằng kết nối thực tế. Fusion giữ relay dự phòng. API dev tiếp tục dùng 8080; bản phát hành client vẫn yêu cầu HTTPS theo chính sách hiện có.

## Kiểm tra tự động và nghiệm thu

`scripts/check-development.sh` chạy các bộ API/schema/recovery/social bằng .NET 10; cấu hình `PKV_TEST_POSTGRES`/`PKV_TEST_REDIS` trỏ đúng development trước khi chạy. Mỗi bộ tạo/xóa database kiểm thử riêng. Không log connection string hoặc SMTP key.

- Authentication là scene đầu tiên. Thử đăng ký, OTP email, quên mật khẩu, retry tải dữ liệu, hết phiên và Settings → Đăng xuất.
- Menu chỉ có chọn Nam/Nữ tại Character Selection. ProgressionModal có icon copy ID và sửa tên Lưu/Hủy; không còn nút Tài khoản.
- Bạn bè có Chặn/Đã chặn/Bỏ chặn. Mời phòng đã nhận trước khi chặn phải bị từ chối khi chấp nhận. Hai người bị chặn vẫn có thể gặp qua matchmaking.
- HLV phản hồi ngắn sau điểm ở Training/BotMatch. Phân tích kết quả được đọc theo đúng sessionId; polling tối đa 10 giây. FastAPI hỏng không cản gameplay. ML vẫn tắt khi thiếu artifact đạt gate.
- Multiplayer: thử hai Editor khác mạng, đổi người tạo phòng, rồi hai Windows Development Build cùng revision. Bật `-pkvNetworkDiagnostics` trên build; Editor tự ghi JSONL dưới `Application.persistentDataPath`.
- So sánh region/authority, RTT, biến thiên RTT p95, input/ACK/backlog, snapshot age, correction p95/max và frame time p95 giữa hai máy. Biến thiên RTT là chênh lệch RTT giữa các mẫu 250 ms, không phải phép đo jitter một chiều hoặc tỷ lệ mất gói. Fusion RTT gồm thời gian xử lý peer. Dùng thêm thống kê Fusion/video khi thử mất gói. Chưa được kết luận nguyên nhân hoặc hết lag khi chưa có bằng chứng hai máy.
- Dedicated: hoàn thành 1v1 và 2v2; thử thiếu người, reconnect, vé sai/hết hạn/dùng lại, client tự báo kết quả, tắt worker giữa trận, chạy nhiều trận nối tiếp. SQL/Elo/thưởng chỉ được ghi một lần. Đối chiếu replay và telemetry theo từng người.

## Vận hành và quay lại bản trước

Worker giữ một lease Redis, server giữ state authority. Mất lease thì worker dừng; backend kết thúc trận gián đoạn khi hết thời hạn, không chuyển authority về client. Kết quả/Elo/thưởng dùng cùng transaction. Telemetry/replay dedicated chỉ nhận từ API service-key. Raw service key và OTP không được ghi log.

Giữ image theo tag, không ghi đè `latest`. Khi quay lại trước thay đổi schema, dừng API/game-server, khôi phục đúng bản dump development vừa sao lưu và khởi động image/client cùng phiên bản. Khi chỉ quay lại cấu hình Shared, đặt dedicated=false và tạo lại API; hoàn tất hoặc gián đoạn trận dedicated hiện tại trước khi đổi chế độ.

Không coi compile/unit test là nghiệm thu gameplay hai máy. Kiểm tra Ubuntu, SMTP thật, NAT, CPU/RAM và hai thiết bị phải ghi kết quả riêng khi bạn chạy gói triển khai.

## Kết quả kiểm tra tại máy phát triển

Đã khởi động Docker Desktop và chạy API, SMTP Mailpit STARTTLS, PostgreSQL/Redis cùng worker Unity Linux trong stack thử nghiệm cách ly. Database thử dùng tên `pkv_acceptance_*`, API thử chỉ bind `127.0.0.1:28080`; không tạo lại database `pickleverse` đang có. Không gửi email ra ngoài Docker. Bản client vẫn trỏ endpoint đã có trong `BackendApiSettings`; cập nhật backend/schema trên Ubuntu trước khi dùng client mới với endpoint này.

| Bộ kiểm tra | Kết quả |
|---|---:|
| Unity Editor, gồm input/ACK, prefab, heap và truyền kết quả | 481/481 |
| Hợp đồng HTTP/kiến trúc/Unity ApiClient | 33 |
| Codec vé backend đối chiếu verifier Unity và gate HLV | 18 |
| Schema development | 40 |
| Xác thực/game/reward/replay tích hợp | 132 |
| Quên mật khẩu, tranh chấp reset/thu hồi phiên | 20 |
| Bạn bè/chặn | 14 |
| Lời mời phòng, chặn và khóa hồ sơ theo thời hạn | 28 |
| Pipeline HLV/worker, đúng phiên và đúng người | 36 |
| Dedicated backend/Redis lease/kết quả chống lặp | 18 |
| HTTP/JWT/SMTP/hồ sơ qua container API thật | 16 |
| Python | 11 |

Frontend đã qua type-check; Compose đã qua `config --quiet`. Các con số backend không phải số trận gameplay đã nghiệm thu. Bằng chứng nằm trong `artifacts/account-multiplayer-ai-20261103`; `game-*-attempt*.json` là các lượt lỗi đã lưu để chẩn đoán, không phải kết quả đạt.

Đã chạy hai client Windows → worker Linux cho 1v1 và bốn client cho 2v2, qua đúng luồng đăng nhập/Custom/xác nhận phòng. Cả sáu client ghi nhận vào sân, di chuyển, input được ACK, nhận kết quả chính thức và không có exception runtime. SQL có đủ 2/4 người được finalized. Hai replay đều complete, có 4 chunk, lần lượt 231/230 frame; đã tải lại, đối chiếu hash và chuyển động của đủ 2/4 actor. Các lượt này kết thúc bằng thao tác rời trận có chủ ý, **chưa thay thế một trận đánh đủ điểm để nghiệm thu contact/luật sân**.

Reconnect native cũng đã qua: khách quay lại cùng trận, nhận lại điều khiển và di chuyển được; hai client nhận kết quả, không có exception runtime. Một worker đã phục vụ nhiều lượt liên tiếp. Kết nối client đo được là **Relayed**; chưa chứng minh Direct/NAT. Khi dừng worker giữa trận, lượt kiểm tra cuối kết thúc gián đoạn cho cả hai trong 46,05 giây, giữ nguyên coins/gems/XP/Elo và tổng thắng/thua; không suy diễn người thắng.

Client retry dedicated trong cửa sổ reconnect; khi không khôi phục được thì trở về phòng chờ và tải lại trạng thái kết quả theo phiên tài khoản. Tác vụ này nằm ngoài simulation, dừng khi tài khoản thay đổi, không chuyển sang authority client.

Đã kiểm tra lifecycle này bằng cách dừng worker khi hai client đang ở sân: cả hai tự về phòng chờ, tự xóa pending sau khi backend xác nhận gián đoạn và ghi nhận 0 exception runtime. Shared 1v1 và Shared 2v2 cũng qua luồng di chuyển/input-ACK/kết quả với 0 exception ở đủ 2/4 client. Tổng hợp máy đọc được: `artifacts/account-multiplayer-ai-20261103/acceptance-summary.json`.

Trong 10 mẫu Docker thu khi chạy 1v1/reconnect (giới hạn worker 2 CPU/2 GiB), CPU theo cách tính của Docker nằm trong 10,40–41,83%, RAM 140,9–224,1 MiB. Log server 2v2 ghi tick 15,625 ms (64 Hz), frame-time p95 cao nhất 16,746 ms. Đây là smoke ngắn trên Docker Desktop của máy Windows, không phải phép đo trên Pentium G6400/Ubuntu hoặc bảo đảm tải dài hạn.

Log Unity còn có `Curl error 35 / UnityTls error code: 7` ở các yêu cầu nền chưa định danh được đích kết nối. Không tắt kiểm tra chứng chỉ TLS. Các luồng API/Fusion nêu trên vẫn đạt; không gọi những log này là nguyên nhân giật của hai thiết bị.

Đã dọn bốn container có nhãn acceptance, database `pkv_acceptance_*`, Redis prefix, bucket S3 thử và các file credential tạm. Dịch vụ S3 local trả 405 cho DeleteBucket; chỉ xóa thư mục bucket đã xác minh rỗng bằng `rmdir`, không xóa đệ quy volume. Stack và database `pickleverse` chính được giữ nguyên. Editor để tại Authentication, dừng Play, target Windows Player, HTTP DevelopmentOnly.

Build Windows đầu tiên sau khi thêm prefab mới mất khoảng 25 phút biên dịch lại shader và vượt timeout 600 giây của CLI; Unity tiếp tục chạy và báo `Succeeded`. Bản build cuối tiếp theo dùng cache, không có lỗi. Không chạy thêm build chồng trong lúc build cũ còn hoạt động.

## Phạm vi mã đã bàn giao

- Authentication riêng, guard khi mở thẳng scene, quên mật khẩu OTP với challenge riêng và phiên bản JWT; Settings đăng xuất, chọn nhân vật qua hồ sơ, copy ID/sửa tên tại Tiến trình, bỏ UI Tài khoản cũ.
- Danh sách người do mình chặn; block/unblock dùng nghiệp vụ hiện có, chặn gửi/chấp nhận lời mời với khóa người chơi trong transaction. Matchmaking không lọc block.
- Shared giữ lịch sử 32 input và chỉ ACK sau đủ thời gian mô phỏng từng input; tích phân di chuyển 120 Hz thống nhất. Correction làm mượt trên phần hiển thị; proxy/bóng đọc snapshot Fusion, epoch phân biệt teleport/reset. Protocol tăng 18 vì network layout thay đổi.
- HLV ngắn cục bộ sau lượt/điểm; tổng kết đọc đúng sessionId, chờ tối đa 10 giây; tái sử dụng worker/FastAPI và gate cũ. ML vẫn tắt. Catalog SQL/JSON và các nhãn kỹ thuật đã Việt hóa; enum/ID giữ nguyên.
- Worker Linux native Fusion Server/Client, roster/vé HMAC dùng một lần, lease Redis một slot, xác thực service-key cho kết quả/telemetry/replay, transaction kết quả dùng lại luồng hiện có. Heartbeat worker lưu riêng để client không che server chết; không chuyển authority về client.
- Build server thêm `APPUI_PLATFORM_DISABLED` (cờ có trong package App UI đang cài) để không khởi tạo plugin GTK/clipboard của UI. Bootstrap server không có UI/camera/audio; actor vẫn giữ animation/contact dùng cho simulation.
- Builder loại native App UI plugin khỏi build server, cho phép HTTP nội bộ tới API Docker trong server build, rồi khôi phục cấu hình client. Cấu hình client vẫn chỉ cho HTTP trong Development Build; bản client phát hành yêu cầu HTTPS như trước.
- Vé admission v1 dài 105 byte: một byte phiên bản, bốn UUID theo `Guid.ToByteArray` (trận/người chơi/lease/nonce), thời hạn Unix 8 byte big-endian và HMAC-SHA256 32 byte. API trả Base64; client giải mã trước khi đưa vào Fusion. Giới hạn transport 128 byte đã đối chiếu trực tiếp hằng `NetCommandConnect.TOKEN_MAX_LENGTH_BYTES` trong SDK Fusion 2.1.3 của dự án. Verifier Unity được kiểm thử với codec backend thật; không chấp nhận vé bị sửa, bị cắt ngắn hoặc dùng ngoài trận/lease.
- Hai prefab `DedicatedMatch`/`DedicatedPlayer` không mang `MasterClientObject`; hai prefab Shared gốc vẫn giữ cờ này. Cờ này chỉ dành cho Shared theo [API NetworkObjectFlags của Photon](https://doc-api.photonengine.com/en/fusion/current/namespace_fusion.html). Giới hạn token và cờ prefab đều đã gây lỗi trong lượt kiểm thử Docker thật, không được phát hiện chỉ bằng unit test.
- API rời trận từ client không được hoàn tất kết quả dedicated. Yêu cầu rời trận đi qua simulation server; chỉ service credential của server mới ghi kết quả chính thức.
- Ticket online trả `authorityMode` từ bản ghi trận; client dùng giá trị này thay cho feature flag hiện tại. Guard hồ sơ/lời mời bỏ qua bản ghi `PREPARING` đã quá hạn xác nhận, nhưng vẫn chặn trận đang `PLAYING`.
- Đã bỏ JSON kết quả khỏi state snapshot, giảm `NetworkMatch` từ 78.144 xuống 12.600 byte. Kết quả được gửi riêng qua kênh tin cậy, chia phần Base64 tối đa 1.000 ký tự, giới hạn tổng 64 KiB, kiểm tra nguồn authority và ghép theo mã/lần gửi. Worker chờ ACK có giới hạn trước khi giải phóng session; client vào muộn có thể yêu cầu gửi lại. Giữ heap ban đầu 256 trang 32 KiB (8 MiB), có kiểm tra Shared/dedicated và Unicode/đảo thứ tự/trùng gói.

Ảnh UI trong `artifacts/account-multiplayer-ai-20261103/` dùng tài khoản fixture trong bộ nhớ, không phải bằng chứng SMTP/API thật. Script QA nằm `My project/AgentScripts/AccountUiSmoke.cs`; dừng Play Mode sẽ xóa fixture. Ba font đã có chỉnh sửa từ trước được giữ nguyên.

Chưa nghiệm thu offline training, self-assessment hoặc 2.000 người chơi/500 trận. Chưa có dữ liệu hai thiết bị để tuyên bố hết giật. Linux binary build thành công không thay thế kiểm thử trận trên Ubuntu.
