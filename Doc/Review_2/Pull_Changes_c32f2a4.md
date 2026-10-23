# Cập nhật backend sau lần pull ngày 23/10/2026

Baseline trước: `d44e8c063d837442dab9bc3ecc29eed7cc929cfa`. HEAD: `c32f2a4c0a546d0ed4f0df1b2c97e185ed831fb6`.

Phương pháp: chỉ đọc diff, source, schema, package manifest và tài liệu. Không chạy Unity, API, SQL, npm, dotnet, cloud hay test game/backend. Mọi trạng thái “có trong source” đều chưa đồng nghĩa đã nghiệm thu end-to-end.

## Những thay đổi chính

- Unity đã có AccountRuntime/AccountService, REST transport và UI đăng ký, OTP, đăng nhập. Token giữ trong bộ nhớ; có refresh và chống phản hồi của tài khoản cũ ghi đè phiên mới.
- ProgressionRuntime chuyển sang ảnh chụp trạng thái do server sở hữu. Luyện tập, tutorial, XP, thưởng, ví, mua/trang bị đi qua API và sổ lệnh idempotent. Luồng offline cũ không còn chứng minh hoạt động trong runtime hiện tại.
- Backend thêm Game service dùng transaction/row lock, GameCommands chống trùng, CurrencyTransactions theo dõi thay đổi ví, JSONB cho trạng thái tổng hợp, Redis cho hàng chờ/đặt chỗ/cache BXH.
- Multiplayer hiện dùng Photon Fusion Shared. Đây là peer StateAuthority, không phải dedicated server đáng tin cậy như proposal. Kết quả được server kiểm tra roster/score, yêu cầu báo cáo người thật đồng thuận và áp dụng thưởng một lần; điều đó chưa thay thế mô phỏng vật lý do server độc lập kiểm chứng.
- Bạn bè/BXH đã dùng dữ liệu API thật trong source. Replay có API, metadata/chunk giới hạn kích thước và lưu đối tượng S3. Các kết luận cũ “chỉ demo local” cần được thay bằng kết luận mới có giới hạn kiểm chứng.
- Web quản trị React đã có trang login, dashboard, users, CMS, matches, moderation, analytics. Tuy nhiên hợp đồng đăng nhập mặc định chưa khớp API.

Đếm tĩnh: **37 bảng SQL, 208 cột, 54 FK khai báo; 14 controller và 81 khai báo endpoint**. Có 27 lớp EF; 10 bảng mới được thao tác qua SQL trực tiếp nên không tự xem chênh lệch này là lỗi EF.

## Những kết luận cũ đã thay đổi

| Kết luận trước pull | Hiện trạng ở HEAD mới |
|---|---|
| Game chưa nối backend | Đã có REST adapters và luồng server snapshot; cần test triển khai thật. |
| Web admin chưa có | Đã có React source; login contract đang lệch. |
| Admin thiếu role guard | AdminController đã có `[Authorize(Roles="Admin")]`; chưa chạy kiểm thử từ chối quyền. |
| Luồng finish/submit-drill cũ cho phép cộng tùy ý | Endpoint cũ trả 410; Game service mới dùng kiểm tra và transaction. |
| Verify email/refresh chỉ là khung cũ | Có OTP hạn dùng/giới hạn lần thử/SMTP và rotating refresh; chưa kiểm chứng email thật. |
| Replay storage key bị cắt prefix | Source StorageService mới dùng full key; upload/download thực tế chưa chạy. |

## Các vấn đề và khoảng thiếu cần theo dõi

| ID | Mức | Phát hiện | Bằng chứng |
|---|---|---|---|
|SR01|High|Photon Fusion Shared gives a peer state authority. This is not the proposed trusted dedicated simulation and leaves host/peer trust risk.|`My project/Assets/Scripts/Multiplayer/PhotonSession.cs:116`|
|SR02|High|The active progression runtime requires server state. Offline drill execution and exactly-once later synchronization are not demonstrated.|`My project/Assets/Scripts/Progression/ProgressionRuntime.cs:8`|
|SR03|High|Initial-placement ML, telemetry technique classifier, labelled evaluation and validated recommendations are not evidenced. Rule-based feedback does not establish those commitments.|`My project/Assets/Scripts/LocalMatch/BotMatch.cs:11`|
|SR04|High|The React login sends emailOrUsername/passwordHash and reads role/token, while the API requires Email/Password and returns AccessToken/Profile. The default contracts are incompatible.|`src/Frontend/src/context/AuthContext.tsx:41`|
|SR05|High|Admin mutations serialize enum status as Banned/Active while the SQL constraint uses ACTIVE/BANNED/SUSPENDED; these values can fail the database constraint.|`src/Backend/Services/Implementations/AdminService.cs:82`|
|SR06|High|Admin lesson creation omits catalog Code/Config, while the game catalog selects coded configured lessons. CRUD does not yet demonstrate publishing a playable drill.|`src/Backend/Services/Implementations/AdminService.cs:159`|
|SR07|High|Complete live-match oversight, appeals and operational analytics are not evidenced. ActiveUsersToday currently counts all users rather than dated activity.|`src/Backend/Services/Implementations/AdminService.cs:353`|
|SR08|High|Current-revision physical-device, network/load, security, availability, offline and ML acceptance results are unavailable. Test source is not execution evidence.|`src/Database.sql`|
|SR09|High|The local review-2 tag and CI execution evidence are absent; the required official tagged self-review and automated-in-CI target cannot be verified.|`src/Database.sql`|
|SR10|Medium|The approved registration revision, official project code and decisions accepting stack/authority deviations are unconfirmed. No scope reduction has been assumed.|`src/Database.sql`|
|SR11|Medium|Server-owned transactions and matching human reports improve result integrity, but peers still supply simulation outcomes and training measurements. Agreement does not independently prove legal physics.|`src/Backend/Services/Game/OnlineGameService.OnlineMatches.cs:182`|
|SR12|Medium|Real friendship APIs and room-code play exist and cover the proposal room-code requirement. Account-targeted invitations are an unverified desired UX extension; confirm whether they should become a separate commitment.|`src/Backend/Services/Game/OnlineSocialService.cs:27`|
|SR13|Medium|Fallback authentication configuration, deployed TLS/AES-256 controls, production rollback and iOS build acceptance require deployment evidence; configuration values are intentionally redacted.|`My project/Assets/Scripts/Services/Api/UnityApiTransport.cs:17`|

## Các bảng mới

CosmeticPrices, GameProgress, GameCommands, TrainingSessions, AppActivitySessions, GameMatches, GameMatchPlayers, CurrencyTransactions, OnlineReplays, OnlineReplayChunks.

Chỉ `src/Database.sql` là schema hiện hành dùng để lập ERD. `src/NOREADDATABSE.sql` gồm 71 bảng là phương án khác trong repo, không phải bằng chứng DB hiện đang deploy. Schema nguồn không chứng minh migration đã được áp dụng vào môi trường chạy.

## Tác động tới Review 2

SRS giữ đầy đủ scope proposal, đánh dấu phần source/partial/gap. SDD tách kiến trúc hiện có và target; không vẽ UGS/NGO legacy thành luồng chính. BM03 đề nghị xác nhận thay đổi stack, authority và chính sách bot fallback, không tự phê duyệt giảm scope. BM02 chỉ là rà soát sơ bộ vì chưa có tag review-2. Tên/thông tin hành chính để sau theo yêu cầu của bạn.

Cảnh báo Git: lệnh status mặc định gặp lỗi metadata worktree ở submodule `.agents/plugins/sdd-hybrid`. Đã dùng `--ignore-submodules=all` để kiểm tra cây mã chính; không sửa submodule hay Git. Điều này giới hạn xác nhận phần nội bộ submodule, không làm thay đổi kết quả đọc backend.

Tệp audit cũ trong `_work` và `Diagrams/Source` giữ như bằng chứng baseline cũ, không dùng làm kết luận hiện hành. Bộ hiện hành được chỉ rõ trong README và thư mục `Diagrams/Current_c32f2a4`.