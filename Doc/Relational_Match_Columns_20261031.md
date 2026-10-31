# Trận đấu bằng cột và quan hệ — 31/10/2026

Đợt tiếp theo đã bỏ 9 cột dư ở GameMatchPlayers và hai tổng game thắng ở GameMatches. Schema mới nhất 38 bảng/265 cột/53 FK, hai bảng trận đều có 15 cột. Số game thắng và điểm lấy MatchScore, Elo delta lấy ratingledger; dữ liệu hồ sơ dùng JOIN. Các mô tả snapshot và 276 cột ở dưới ghi lại đợt trước. Xem [Rà soát GameMatches](GameMatches_Normalization_20261031.md) và [Rà soát GameMatchPlayers](GameMatchPlayers_Normalization_20261031.md).

Theo yêu cầu, đã xóa hoàn toàn `GameMatches.Data` và `GameMatchPlayers.Report`. Hai bảng không còn cột JSON/JSONB hoặc cột blob thay thế. Không thêm entity/bảng; schema hiện có **38 bảng / 276 cột / 53 FK**. Nguồn khởi tạo là `src/Database.sql`; không tạo migration.

## Ánh xạ dữ liệu

| Dữ liệu trước đây | Nguồn hiện tại |
|---|---|
| `phase` và trạng thái trận | `GameMatches.Status`: PREPARING, PLAYING, FINISHED, CANCELLED; bỏ trạng thái STARTED chung chung |
| Loại trận, thời điểm, số game thắng, thứ tự lịch sử | Các cột Kind, StartedAt, EndedAt, TeamAScore, TeamBScore, Sequence hiện có |
| Phiên bản quy tắc Elo | `GameMatches.RatingPolicyVersion` |
| 1v1/2v2, Photon session, lượt tìm trận, khu vực | `GameMatches.Mode`, PhotonSession, SearchId (UUID nullable), Region |
| Môi trường của đội hình online | `GameMatches.Night`, Venue |
| Danh sách `roster.members` | Các dòng `GameMatchPlayers`, không lưu thêm một bản danh sách |
| Thứ tự phần tử đội hình | `GameMatchPlayers.RosterIndex`; giữ nguyên vị trí actor 2v2 khi đọc lại |
| Thông tin người chơi lúc tạo trận | PlayerName, EloBefore, IsMale, PaddleId, GroupId, IsBot trong `GameMatchPlayers` |
| Cờ ready của snapshot đội hình | RosterReady; xác nhận hiện hành vẫn do Redis quản lý, Accepted ghi trạng thái đã được chấp nhận khi bắt đầu |
| `host` và người ghi trận bot | `GameMatchPlayers.IsReplayWriter`; partial unique index chỉ cho tối đa một bên ghi trên mỗi MatchId |
| Kết quả và thay đổi Elo của mỗi người | Outcome, ReceivedElo, LocalForfeit, FinalizedAt hiện có |
| Thống kê và lý do kết thúc | `GameMatchPlayers.Points`, ValidReturns, ValidServes, EndReason |
| Nhận báo cáo và chống gửi lại kết quả khác | `GameMatchPlayers.ReportedAt`, Fingerprint, LastSeenAt |
| Điểm từng game | `MatchScore`, chuẩn theo hai đội |
| `result/results`, opponents, gamesWon/gamesLost, opponentElo | Dựng DTO từ các quan hệ/cột trên; Elo đối thủ tính từ EloBefore của thành viên đội đối diện |

`GameMatches` không có OwnerId/PlayerId/HostPlayerId và không sở hữu phòng Photon. PhotonSession là tham chiếu dùng để kiểm tra đúng phiên mạng; SearchId tham chiếu lượt Redis, không phải FK tới một bảng mới. IsReplayWriter là quyền ghi replay của một người tham gia. Thời điểm API đọc từ TIMESTAMPTZ PostgreSQL có độ chính xác microsecond.

Tên/Elo/giới tính/vợt/group được chốt khi tạo đội hình. Việc đổi hồ sơ hoặc Elo sau đó không sửa lịch sử hoặc đội hình của trận cũ. Trận BotPractice lưu hai profile hiện có và cờ bot; không tạo loại tài khoản hay entity bot mới. Profile bot có thể dùng trong luyện tập PvE; chỉ các trận online đang hoạt động giữ chỗ bot cho rank như trước.

## Đối chiếu kết quả không dùng Report JSON

Request HTTP vẫn gửi DTO MatchResult có danh sách scores, nhưng backend không serialize DTO đó vào SQL. Báo cáo hợp lệ đầu tiên ghi một bộ điểm ứng viên chung vào MatchScore theo hướng đội A; ghi thống kê, thời điểm nhận và SHA-256 vào dòng người gửi. Báo cáo khác phải có cùng fingerprint tỷ số; thống kê riêng của từng người giữ ở dòng tương ứng. Mỗi bên thật vẫn tự gửi bằng JWT; bot được mirror từ đối thủ thật theo quy tắc hiện có.

Điểm ứng viên chỉ trở thành kết quả chính thức khi GameMatches.Status=FINISHED. `MatchScore.Completed` diễn tả game đã xong, không có nghĩa cả trận đã được xác nhận. Khi chưa đủ báo cáo, không có lịch sử, replay được công bố hoặc thưởng từ bộ điểm đó. Không lưu thêm bảng/cột JSON làm hàng đợi. Khi rời trận hoặc hết hạn, backend xử lý kết quả theo chính sách hiện có và thay bộ điểm ứng viên bằng điểm của kết quả được chốt.

Match advisory lock và khóa profile theo UUID giữ thứ tự cập nhật. Báo cáo cuối, điểm, finalization, Elo, phần thưởng và tiến trình nằm trong cùng transaction; rollback không để lại thưởng một phần hoặc một người đã finalized. Retry dùng Fingerprint và FinalizedAt để không trả thưởng thêm.

## Code và tương thích

- Entities/Fluent API ánh xạ cột cụ thể bằng Npgsql EF Core.
- `RelationalMatch.cs` dựng đội hình, kết quả, lịch sử và trận đang chơi từ quan hệ. `OnlineMatch`/`MatchResult` còn là đối tượng trong bộ nhớ/API, không phải tài liệu SQL.
- Các service trận bot/online, khôi phục trận, chọn bot, lời mời và quyền ghi replay không đọc JSON Data/Report nữa.
- Giữ endpoint và cấu trúc DTO Unity hiện có. Unity tiếp tục nhận history/scores qua API, không đọc bảng SQL.
- GameProgress và AppActivitySessions vẫn đã bỏ; phiên hoạt động vẫn nằm Redis, tổng thời gian ở PlayerProfiles. Các JSON ở hệ thống luyện tập/AI khác nằm ngoài phạm vi yêu cầu này.

## Kiểm chứng

Bộ `--check-match-columns` có 33 kiểm tra: schema/EF không có JSON hai bảng, không thêm chủ sở hữu trận, giữ thứ tự actor và snapshot, quyền replay, đội B gửi trước, tỷ số mâu thuẫn, rollback lúc chốt, retry, bot/forfeit và tái sử dụng profile bot giữa PvE/rank. Các bộ online game, MatchScore, unified matches, relational progress, Hybrid, replay/HTTP/S3, cấu hình, rank, invitations, Redis và activity đã được chạy lại. Tổng 366 assertions trong 13 bộ; backend và Unity C# build thành công.

Database development được tạo lại từ SQL, backend được triển khai và kiểm tra HTTP; log và summary nằm trong `artifacts/relational-match-columns-20261031/`. Sơ đồ được tạo lại từ SQL với 38 entity/53 quan hệ, không attribute. Chưa chạy lại gameplay nhiều Unity client; không suy ra AI coaching runtime đã được bật.
