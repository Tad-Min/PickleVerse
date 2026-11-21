# Kiểm thử PickleVerse

Các ca dùng S3 thật và dọn fixture Unity đọc `S3_ACCESS_KEY` và `S3_SECRET_KEY` từ môi trường tiến trình. Dùng credential của stack dev đang chạy. `PKV_TEST_S3_URL` có thể ghi đè địa chỉ mặc định `http://127.0.0.1:19000`.

Mã kiểm thử hiện được nhóm theo môi trường chạy. Các assembly, dependency và nhóm ca hiện có vẫn được dùng để kiểm tra hồi quy.

```text
src/Testing/
  Backend/                 IntegrationChecks của .NET
  AiService/               pytest cho FastAPI, coaching và model
  Frontend/                Kiểm tra phiên đăng nhập qua Axios adapter
  Unity/EditMode/          Các assembly NUnit và file .meta của Unity
  Sync-UnityTests.ps1      Đồng bộ test Unity trước khi chạy
```

## Cách chạy hiện tại

Từ thư mục repository, chạy ca hợp đồng ticket và coaching cục bộ hoặc dùng script tích hợp với PostgreSQL development đã xác minh.

```powershell
dotnet run --project src/Testing/Backend -- --check-client-security
./scripts/Invoke-FunctionalChecks.ps1 -Build -Check schema,api-contract,training-sync,dedicated,expert-review
```

Các ca tích hợp tạo database tạm từ `src/Database.sql`. Các cờ cấp tài khoản fixture như `-ProvisionCoachingPilot` dùng riêng khi cần chuẩn bị kiểm thử thủ công. Không đưa các cờ đó vào lượt chạy hồi quy thông thường.

Chạy `python -m pytest -q` từ `src/AiService` như trước. `pyproject.toml` trỏ pytest sang test ở `src/Testing/AiService`. Fixture hợp đồng dùng chung vẫn nằm trong `contracts/` để Unity, API và FastAPI đối chiếu cùng định dạng.

Chạy `./src/Testing/Sync-UnityTests.ps1` sau khi clone và sau khi sửa test Unity. Script đồng bộ mã và GUID `.meta` sang `My project/Assets/PickleVerse.Testing.Generated`. Thư mục này là bản sinh tự động, đã loại khỏi Git. Mở **Window → General → Test Runner → EditMode** trong Unity Editor để chạy. Thay đổi test ở `src/Testing/Unity/EditMode` rồi đồng bộ lại. Các script smoke và probe Editor hiện có vẫn dùng các điểm chạy cũ.

Web admin hiện có `npm run type-check` và `npm run build` trong `src/Frontend`. Có thể bổ sung bộ test trình duyệt sau khi thống nhất phạm vi E2E.

`npm run test:session` trong `src/Frontend` kiểm tra interceptor Axios thật: lỗi 401 đến muộn từ phiên cũ giữ nguyên phiên mới; lỗi 401 của phiên hiện tại xóa credential và chuyển về đăng nhập. Không gọi mạng.

`--check-single-session` kiểm tra đăng nhập đồng thời, thu hồi JWT/refresh token, logout đến muộn, cạnh tranh login/refresh và endpoint HTTP `GET /api/Auth/session`. Endpoint cần JWT và được miễn hạn mức đăng nhập vì client gọi định kỳ. Các ca `--check-dedicated` và `--check-client-security` kiểm tra epoch trong vé HMAC, admission, heartbeat và callback cũ sau takeover. CI chạy các bộ kiểm tra này.

Tính năng đăng nhập một nơi dùng `users.authversion` hiện có, không đổi schema. Client Unity kiểm tra phiên mỗi 3 giây và khi ứng dụng trở lại; web kiểm tra mỗi 5 giây. Dedicated kiểm tra phiên trong heartbeat mỗi 5 giây và ngay khi admission. Thiết bị mới tự resume `pendingMatch`; phiên cũ ngắt kết nối mà không gửi lệnh bỏ cuộc. Trận giữ thời hạn reconnect 30 giây hiện tại. Khi triển khai, cập nhật đồng bộ API, Unity client và Dedicated **protocol 22** để đồng bộ cả vé vào trận và sự kiện cú đánh.

Online giữ cơ chế chọn tổ hợp và nhả phím để gửi cú đánh. Client phát animation ngay khi nhả; mỗi yêu cầu có mã riêng để bỏ qua animation xác nhận đã phát. Server kiểm tra cú đánh, xác nhận chạm bóng hoặc từ chối; client không tự ghi nhận hit hay đổi tỷ số. Sự kiện từ lượt cũ được chặn theo `motionEpoch`. `ShotRequestHistoryTests` kiểm tra nhận trùng và phản hồi cũ; các ca `SinglesMatchTests`/`TouchControlsTests` bảo vệ cơ chế nhả phím hiện tại.

Trong lúc `MatchActor.Swinging`, input online bị bỏ qua cho đến hết cả động tác và phần hồi vợt; phím giữ từ lúc đang đánh phải được nhả trước khi chọn cú mới. Mobile không bắt đầu gesture đánh trong thời gian này. Prediction bị server từ chối đóng cửa sổ chạm bóng và chạy hết động tác như đánh trượt. `NetworkShotLockTests` kiểm tra luồng `NetworkPlayer.TickShot` thực khi spam J, tổ hợp phím và giao bóng, bảo đảm không phát thêm prediction/RPC hoặc mở lại cửa sổ chạm bóng.

`PhotonSceneManager` dọn scene Menu, chọn chế độ và phòng chờ trong quá trình tải sân, trước khi `IsBusy` kết thúc. Cách này xử lý cả tiếp quản trận từ Menu và vào sân từ phòng chờ ở Multiple Peer mode; scene sân thực của runner được giữ và kích hoạt. Không dùng callback `async void SceneDone` để dọn riêng `PlayMultiplayer` nữa. Kiểm tra quyền sở hữu runner trước mỗi lần unload để callback cũ không dọn sảnh của kết nối mới. `PhotonSceneIdentityTests` bảo vệ danh sách scene được dọn và các scene mạng/loading cần giữ.

Heartbeat Dedicated dùng UUID dạng D (36 ký tự) khi gửi sessions[].playerId; ID trong roster vẫn giữ dạng N (32 ký tự). API đọc cả D và N cho trường này để tương thích các worker protocol 22 đã triển khai. --check-api-contract kiểm tra HTTP model binding bằng payload Unity thực và payload worker cũ; --check-dedicated kiểm tra gia hạn lặp sau admission/spawn. Không cần đổi schema hoặc build lại client cho bản sửa GUID này.

## Kiểm thử nhiều trận Fusion

Chạy `--check-dedicated` để kiểm tra bốn người tìm 2v2 riêng, hủy và tìm lại, cùng 500 reservation trên Redis. Dùng build Windows Development cùng phiên bản protocol với Linux Dedicated để kiểm thử Fusion thật. `--provision-photon-fixtures <thư-mục> <số-người>` tạo tài khoản test trên PostgreSQL PickleVerse cục bộ và lưu thông tin đăng nhập trong `artifacts`.

Dùng `docker-compose.yml` cùng `docker-compose.functional-local.yml` và profile `dedicated` để chạy nhiều worker trên máy dev. Đặt `PKV_GAME_SERVER_IMAGE` theo image Linux Dedicated đã build và `PKV_GAME_REPLICAS` theo số worker đang nghiệm thu.

`Start-AcceptanceClients.ps1` nhận `-BuildPath`, `-FixturePath`, `-OutputPath` và `-BaseUrl` loopback. `-PublicSearch` dùng ghép trận thường. Thêm `-Doubles` cho bốn client tìm riêng hoặc thêm cả `-PartyPairs` để kiểm tra hai party. Chạy hai lượt với `-FixtureOffset` khác nhau để chọn tài khoản riêng cho từng trận. `-HoldSeconds 1800` giữ sân trong 30 phút và ghi thời gian vào sân, thời lượng giữ sân và nhịp khung hình vào JSON. Đối chiếu số slot, log worker, CPU, RAM và CCU trước khi tăng số client.

## Các kiến trúc có thể áp dụng

| Kiến trúc | Bố trí | Phù hợp khi | Chi phí vận hành |
| --- | --- | --- | --- |
| Theo runtime | Mỗi thư mục Unity, Backend, AiService và Frontend dùng runner riêng. | Cần chạy nhanh phần vừa sửa, bám công cụ hiện có. | Cần một nơi ghi rõ các ca đi qua nhiều dịch vụ. |
| Theo cấp độ | Chia `Unit`, `Contract`, `Integration` và `E2E` ở cấp đầu tiên. | CI phân quyền và chạy theo từng cổng kiểm thử. | Mỗi cấp chứa nhiều ngôn ngữ và phải điều phối nhiều runner. |
| Kết hợp | Giữ mã theo runtime, định nghĩa các cổng và kịch bản xuyên hệ thống ở cấp repository. | Game có Unity, API, PostgreSQL, Redis, web admin, FastAPI và Photon Fusion. | Cần thống nhất ai sở hữu fixture và kịch bản chung. |

## Đề xuất cho PickleVerse

Áp dụng **kiến trúc kết hợp**. Giữ bố trí mã hiện tại trong `src/Testing` và tách việc chạy thành các cổng theo rủi ro.

1. **Kiểm tra nhanh khi sửa code.** Unity EditMode cho luật điểm, training và offline store. .NET cho ticket và hợp đồng API. pytest cho rules, telemetry, ngưỡng và kiểm tra artifact. TypeScript cho kiểu dữ liệu và build admin.
2. **Tích hợp với hạ tầng thật trước khi hợp nhất.** Dùng PostgreSQL tạm từ `src/Database.sql`, namespace Redis riêng và FastAPI test local. Ưu tiên sync lặp, reset theo thế hệ, phần thưởng, publish CMS, ban/appeal và tranh chấp kết thúc trận. Đối chiếu enum và JSON giữa API, Unity và web admin.
3. **E2E trước bản phát hành.** Giữ một tập nhỏ các hành trình có giá trị cao. Ví dụ Unity vào phòng Shared rồi chuyển sang Dedicated 1v1/2v2, mất kết nối và quay lại, admin dừng trận, hoàn thành bài offline rồi đồng bộ sau khi mở game lại. Dùng stack và tài khoản test riêng, giữ log API, Dedicated và Unity theo cùng mã trận hoặc mã lượt.
4. **Nghiệm thu AI độc lập.** Dữ liệu mô phỏng kiểm tra đường ống và cổng phục vụ. Đánh giá chất lượng phân loại và coaching bằng tập người thật có nhãn chuyên gia theo các ngưỡng đã chốt. Lưu báo cáo và checksum cùng artifact.

Khi cần thêm công cụ, ưu tiên PlayMode cho vòng đời scene và UI Unity, cùng một vài ca E2E trình duyệt cho CMS và moderation. Giữ hạ tầng có chi phí cao như Photon Dedicated cho cổng phát hành hoặc lượt chạy chủ động.
