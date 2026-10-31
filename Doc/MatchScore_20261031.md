# MatchScore — 31/10/2026

Cập nhật sau cùng: schema 38 bảng/265 cột/53 FK. Đã bỏ thêm GameMatches.TeamAScore/TeamBScore; số game thắng tính từ MatchScore có Completed = true. Điểm gốc TeamAScore/TeamBScore vẫn giữ trong MatchScore. GameMatchPlayers.Points đã bỏ, API và tiến trình tính tổng điểm từ MatchScore theo đội. Xem [Rà soát GameMatches](GameMatches_Normalization_20261031.md) và [Rà soát GameMatchPlayers](GameMatchPlayers_Normalization_20261031.md). Những số 276 cột phía dưới là đợt trước.

Cập nhật mới nhất: schema 38 bảng/276 cột/53 FK; đã bỏ Data/Report của trận. Báo cáo đầu tiên có thể ghi điểm ứng viên vào MatchScore khi Status vẫn PLAYING; điểm chỉ chính thức khi trận FINISHED, đủ báo cáo cùng fingerprint. Các câu bên dưới nói chưa có dòng trước xác nhận thuộc thiết kế cũ. Xem [Relational Match Columns](Relational_Match_Columns_20261031.md).

Đợt tiếp theo cùng ngày đã gộp các bảng trận/lịch sử và sửa FK PlayerLessonDrills bằng Npgsql EF Core. Schema hiện tại là 39 bảng/263 cột/54 FK; những dòng "chưa triển khai" và số lượng bên dưới ghi lại riêng đợt thêm MatchScore. Xem [Unified Matches](Unified_Matches_20261031.md).

Người dùng đồng ý thêm bảng điểm và chọn tên chính xác `MatchScore`. Bảng lưu điểm của hai đội theo từng game trong trận, dùng chung cho 1v1 và 2v2.

| Cột | Ý nghĩa |
|---|---|
| `MatchId` | FK tới `GameMatches`, xóa theo trận |
| `GameNumber` | Số thứ tự game từ 1 đến 3 theo luật hiện tại |
| `TeamAScore` | Điểm đội 0 trong game |
| `TeamBScore` | Điểm đội 1 trong game |
| `Completed` | Game đã hoàn tất hay đang dang dở khi trận kết thúc |

Khóa chính ghép `(MatchId, GameNumber)` ngăn hai bản ghi cho cùng một game. Các cột điểm là INT, không âm và không vượt giới hạn 100.000 hiện dùng trong API. Trận chưa có điểm được xác nhận chưa có dòng trong bảng. Trận thắng 2–0 có hai dòng; trận 2–1 có ba dòng. Một game dang dở khi bỏ trận vẫn được giữ với `Completed=false` nếu kết quả đó được backend chấp nhận.

## Luồng đọc và ghi

- Backend ghi điểm cùng transaction với kết quả trận, Elo và thưởng. Với online, phải có kết quả được xác nhận theo luồng hiện tại; một báo cáo riêng lẻ chưa tạo điểm chính thức.
- Đội hình 2v2 vẫn chỉ có một dòng điểm cho mỗi game, không cộng hoặc nhân đôi theo số thành viên.
- Khi tải lịch sử, backend đọc MatchScore theo số thứ tự và dùng `GameMatchPlayers.Team` để trả đúng `own`/`opponent` cho người xem. Hợp đồng `MatchResult.scores` trả cho Unity được giữ nguyên.
- `GameMatches.Data.result/results` không còn lưu mảng điểm chính thức. Báo cáo client có thể giữ điểm tạm trong lúc chờ xác nhận; khi hoàn tất, mảng điểm trong `GameMatchPlayers.Report` được loại bỏ. Biên nhận kết quả BotPractice lưu SHA-256 thay cho nguyên payload chứa điểm.
- Không tạo bảng `GameScores`. Các đề xuất gộp `MatchHistory`/`GameMatches`, gộp hai bảng người tham gia và sửa FK `PlayerLessonDrills` chưa được triển khai trong lượt thêm MatchScore này.

Code chính: `src/Backend/Services/Game/MatchScoreStore.cs`, các luồng `OnlineGameService.Matches.cs`, `OnlineGameService.OnlineMatches.cs` và `OnlineGameService.State.cs`.

## Kiểm tra

Đạt 235 assertions trong 8 bộ kiểm tra, gồm 20 kiểm tra riêng cho MatchScore: 2v2 ba game, góc nhìn bốn người chơi, retry, dữ liệu chưa xác nhận, điểm dang dở khi bỏ trận, ràng buộc khóa/điểm và cascade. Đạt thêm 9 kiểm tra HTTP trên backend Docker, gồm ghi điểm trận bot, tải lại lịch sử từ bảng và chống kết quả/thưởng trùng. Tài khoản/trận thử đã được xóa; không gửi email thật.

Backend build và Docker image build thành công. Đã tạo lại đúng database development từ `src/Database.sql`, không migration: 41 bảng/265 cột/57 FK. API `/health/ready` trả 200; giữ 10 profile bot và 12 lesson/12 drill. Chưa chạy lại gameplay nhiều Unity client trong lượt này.

Conceptual Diagram đã đồng bộ với 41 entity/57 quan hệ, không vẽ attribute. Bằng chứng chi tiết: `artifacts/match-score-checks-20261031/summary.json`.
