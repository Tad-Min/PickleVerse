# Rà soát cột GameMatchPlayers — 31/10/2026

Đợt tiếp theo đã bỏ thêm GameMatches.TeamAScore/TeamBScore, đưa schema về 38 bảng/265 cột/53 FK; hai bảng trận đều còn 15 cột. Nội dung dưới đây ghi lại riêng đợt chuẩn hóa người tham gia. Xem [Rà soát GameMatches](GameMatches_Normalization_20261031.md).

Theo yêu cầu, bỏ các bản sao hồ sơ, dữ liệu đã có ở bảng khác và giá trị có thể tính lại. `GameMatchPlayers` giảm từ 24 xuống **15 cột**. Toàn bộ schema còn **38 bảng / 267 cột / 53 FK**; không thêm bảng, không tạo migration và không khôi phục JSON Data/Report.

## Chín cột đã bỏ

| Cột | Nguồn thay thế |
|---|---|
| PlayerName | JOIN PlayerProfiles qua PlayerId = UserId |
| IsMale | PlayerProfiles.IsMale |
| PaddleId | PlayerCosmetics có Equipped, JOIN Cosmetics lấy ItemCode; cùng thứ tự chọn vợt của inventory hiện có |
| IsBot | Nhận diện UserId của các profile bot đã seed, dùng cùng RankedBotAccounts với Unity và API |
| Points | SUM điểm trong MatchScore theo Team của người tham gia, gồm cả điểm game dang dở nếu được giữ |
| Accepted | Xác nhận từng người ở Redis trước khi bắt đầu; khi trận bắt đầu thì suy ra từ GameMatches.StartedAt |
| RosterReady | Dựng DTO ready từ xác nhận Redis hoặc trạng thái bắt đầu; không giữ thêm một bản cờ |
| ReceivedElo | ratingledger.Delta theo (PlayerId, MatchId); trận không tính Elo hoặc bị gián đoạn trả 0 khi không có ledger |
| Fingerprint | Tính lại phép đối chiếu từ MatchScore và dữ kiện kết quả chuẩn khi cần; không lưu thêm hash vào người tham gia |

`Ratingledger.cs`/Fluent API bổ sung ánh xạ EF cho **bảng ratingledger đã có**, không tạo entity/bảng nghiệp vụ mới. Endpoint và các trường DTO Unity như name, gender, paddleId, bot, ready, points và eloDelta vẫn có; backend dựng chúng bằng JOIN/tính toán.

## Mười lăm cột giữ lại

| Cột | Vì sao giữ |
|---|---|
| MatchId, PlayerId | PK ghép và hai FK xác định người tham gia trận |
| Team | Phân đội tại trận này; không có trong hồ sơ người chơi |
| EloBefore | Đầu vào cố định khi tính Elo của trận. Elo hiện tại có thể đã đổi; bot được cấp Elo theo đối thủ, không nhất thiết bằng Elo trong profile bot. Ledger chỉ có sau khi chốt trận rank, còn trận đang chơi/PvE chưa có nguồn tương đương |
| GroupId | Nhóm/party trong đội hình của trận; không có bảng nhóm tương ứng |
| RosterIndex | Vị trí trong đội hình để giữ thứ tự actor/ô người chơi 2v2 khi đọc lại |
| IsReplayWriter | Quyền ghi replay cho trận, cần kiểm tra trước khi manifest S3 được tạo |
| ValidReturns, ValidServes | Số cú trả/giao hợp lệ của từng người trong trận; không suy ra từ tỷ số hoặc tổng mastery/lỗi luyện tập |
| EndReason | Nguyên nhân kết thúc tương ứng kết quả; trạng thái/điểm không mô tả đủ nguyên nhân |
| ReportedAt | Mốc nhận kết quả của từng người, hỗ trợ chờ đủ báo cáo |
| LastSeenAt | Heartbeat của người tham gia trận, dùng xử lý mất kết nối |
| Outcome | Kết quả riêng đã chấp nhận. Khi bỏ trận, tỷ số có thể còn 0–0 nên không suy ra chính xác chỉ bằng điểm |
| LocalForfeit | Đánh dấu người thực sự rời trận; đồng đội có thể cùng thua nhưng không phải người bỏ trận |
| FinalizedAt | Mốc xử lý kết quả của từng người và điều kiện chống xử lý lần hai; khác với thời điểm kết thúc của cả trận |

Tên/giới tính/vợt đọc từ dữ liệu hiện tại. Đổi tên có thể làm mục lịch sử hiển thị tên mới, đúng theo yêu cầu không giữ bản sao hồ sơ. Replay S3 vẫn giữ metadata/pose đã ghi tại thời điểm chơi. EloBefore giữ ý nghĩa đầu vào tính trận; không thay bằng PlayerProfiles.Elo hiện tại khi tính lại hoặc nhận kết quả.

## Luồng cập nhật

- `RelationalMatch.Query` JOIN hồ sơ và vật phẩm đang trang bị; nạp MatchScore và ratingledger theo trận. Không đọc full profile JSON hoặc user/password để dựng roster.
- Ready được dựng trong bộ nhớ từ Redis consent; sau bắt đầu dựa vào StartedAt. Điều kiện mọi người đồng ý trước khi vào sân vẫn giữ nguyên.
- Đối chiếu online tính hash trong bộ nhớ từ điểm chuẩn theo hai đội; retry kiểm tra thêm thống kê cú đánh của chính người gửi. Name/appearance thay đổi không làm một kết quả hợp lệ trở thành kết quả khác.
- Trận bot so nội dung chuẩn từ các cột/quan hệ; tổng points được tính từ scores trước khi áp dụng nhiệm vụ/tiến trình. Không tin một tổng points riêng do client cung cấp. Bỏ trận vẫn không được cộng điểm tiến trình/thưởng theo luật hiện có.
- Legacy Matches/my-history cũng lấy ReceivedElo từ ratingledger. Kết quả, điểm, Elo ledger và thưởng vẫn cùng transaction, giữ khóa trận/profile và chống trả thưởng trùng.

## Kiểm chứng

Bộ `--check-normalized-participants` có 21 kiểm tra với PostgreSQL/Redis thật: không còn chín cột ở SQL/EF, ready, đổi tên/giới tính/vợt, nguồn Elo ledger, retry, thay đổi thống kê và client cố gửi points=999 khi tỷ số chỉ cộng được 22 hoặc 4. Tổng 387 assertions trong 14 bộ và 14 kiểm tra HTTP trên image đã triển khai. Backend và Unity C# build đạt; fixture SQL/Redis được dọn.

Database development được tạo lại từ `src/Database.sql`, backend mới đã triển khai. Sơ đồ vẫn 38 entity/53 quan hệ, không attribute. Chưa chạy lại gameplay nhiều Unity client. Bằng chứng: `artifacts/normalized-match-players-20261031/summary.json`.
