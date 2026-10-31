# Replay dùng chung — 31/10/2026

Cập nhật sau cùng: schema 38 bảng/267 cột/53 FK, GameMatchPlayers còn 15 cột. IsReplayWriter vẫn giữ vì phải xác thực máy ghi trước khi có manifest; các bản sao tên/giới tính/vợt/bot đã bỏ khỏi SQL. Xem [Rà soát GameMatchPlayers](GameMatchPlayers_Normalization_20261031.md). Các số 276 cột dưới đây là lịch sử.

Ghi chú các đợt tiếp theo cùng ngày: schema hiện tại 38 bảng/276 cột/53 FK, đã bỏ GameProgress, AppActivitySessions và JSON Data/Report của trận. Quyền ghi replay nằm ở GameMatchPlayers.IsReplayWriter, có partial unique index theo MatchId; không có chủ sở hữu trận. Các số lượng/kiểm tra dưới đây ghi riêng đợt hợp nhất replay. Xem [Relational Match Columns](Relational_Match_Columns_20261031.md).

Mỗi trận dùng chung một replay. Chỉ giữ bảng `MatchHistoryReplay`, gồm `MatchId` (PK/FK tới `GameMatches`) và `ObjectKey` (khóa manifest trên S3). Đã bỏ `OnlineReplays` và `OnlineReplayChunks`; không thêm entity mới.

## Dữ liệu và quyền

- S3 lưu `replays/{MatchId}/manifest.json` và các chunk gzip. Manifest chứa đội hình, thời lượng, trạng thái hoàn tất, danh sách chunk và SHA-256 của từng chunk. `MatchId` trong đường dẫn dùng UUID không dấu gạch.
- Một máy điều phối ghi replay online; người chơi thật ghi replay trận BotPractice. Backend kiểm tra máy ghi theo dữ liệu trận và đội hình `GameMatchPlayers`. Thông tin máy ghi trong manifest không phải FK sở hữu trận.
- Người tham gia và Admin đọc cùng manifest/chunk qua API có JWT. Quyền tham gia được kiểm tra bằng người tham gia đã hoàn tất trong `GameMatchPlayers`. Người ngoài không đọc được replay; người tham gia khác không được ghi đè.
- `ObjectKey` là khóa ổn định, không phải URL ký có hạn dùng. API trả dữ liệu S3 sau khi kiểm tra quyền.

## Lưu và công bố

Replay đang ghi được lưu trực tiếp trên S3. Chỉ tạo một tham chiếu `MatchHistoryReplay` khi `GameMatches` đã hoàn tất và có ít nhất một chunk; không cần bảng SQL lưu manifest hoặc từng chunk. Khi lưu replay xong trước kết quả trận, luồng hoàn tất trận sẽ công bố tham chiếu. Sau khi đã có lịch sử, bản ghi chưa đóng có thể được đọc trong lúc các chunk cuối đang tải lên.

Gửi lại cùng chunk trả về chunk đã lưu; nội dung khác ở cùng chỉ số bị từ chối. Backend khóa ghi theo trận, kiểm tra thứ tự, đội hình, kích thước và hash. Nếu S3 tạm lỗi khi công bố, kết quả/thưởng trận vẫn được lưu; lần ghi hoặc lần đọc hợp lệ sau có thể bổ sung tham chiếu từ manifest S3.

Không chuyển quyền ghi replay sang máy khác giữa trận trong thay đổi này. Nếu máy ghi mất kết nối, replay có thể chỉ có phần đã tải lên; trường `complete` cho biết đã đóng bản ghi hay chưa. Các object S3 cũ không bị xóa hàng loạt khi reset database.

## Kiểm tra trong lượt thay đổi này

- 219 assertions đạt: replay service (23), HTTP với S3 development thật (11), game (81), multiplayer backend (31), Hybrid (33), room invitations (20), configuration (20). Dùng database kiểm thử riêng, tự xóa sau khi chạy; object S3 kiểm thử cũng được dọn theo đúng khóa.
- Backend build, Docker backend image build và Unity Multiplayer C# build thành công. Chưa chạy lại gameplay nhiều Unity client trong lượt này.
- Đã drop/recreate đúng database development `pickleverse` từ `src/Database.sql`, không tạo migration: 41 bảng, 235 cột, 57 FK, 10 profile bot, 12 lesson/12 drill. Không còn hai bảng replay cũ hoặc `GameMatches.OwnerId`. Backend mới trả HTTP 200 ở `/health/ready`.
- Conceptual Diagram đã cập nhật cùng đường dẫn, 41 entity/57 quan hệ, không vẽ attribute. Bằng chứng nằm trong `artifacts/shared-replay-checks-20261031/` và các file validation của sơ đồ.
