# Rà soát cột GameMatches — 31/10/2026

`GameMatches` giảm từ **17 xuống 15 cột**. Bỏ hai tổng số game thắng lưu trùng với `MatchScore`; không thêm bảng/entity, view, cột tổng khác hoặc JSON thay thế. Schema hiện tại có **38 bảng / 265 cột / 53 FK**. `src/Database.sql` là nguồn khởi tạo database development, không dùng migration.

## Hai cột đã bỏ

| Cột đã bỏ khỏi GameMatches | Nguồn thay thế |
|---|---|
| TeamAScore | Đếm dòng MatchScore của trận có Completed = true và TeamAScore > TeamBScore |
| TeamBScore | Đếm dòng MatchScore của trận có Completed = true và TeamBScore > TeamAScore |

Hai cột cũ là **số game thắng** (ví dụ 2–1), khác với điểm từng game (ví dụ 11–5). Hai cột cùng tên **vẫn cần ở MatchScore** vì đó là điểm gốc, không phải bản sao cần xóa.

Backend lắp `gamesWon/gamesLost` theo đội của người xem. API lịch sử cũ của Player và Admin vẫn trả `TeamAScore/TeamBScore` theo hướng đội A/đội B nhưng tính từ quan hệ MatchScore. Không đổi endpoint hoặc DTO mà Unity đang dùng. Các truy vấn repository liên quan nạp MatchScore bằng Npgsql Entity Framework Core.

Game chưa hoàn tất (`Completed = false`) đóng góp điểm đang có nhưng không tính vào số game thắng. Bỏ trận có thể thua dù đang dẫn 1–0; kết quả vẫn lấy từ `GameMatchPlayers.Outcome` và `LocalForfeit`, không suy ra thắng/thua chỉ từ số game. Đối với trận bot bỏ dở/gián đoạn, backend tính lại số game hoàn tất từ scores thay vì tin tổng gamesWon/gamesLost client khai; retry so dữ liệu gốc và không xử lý kết quả/thưởng lần hai.

## Mười lăm cột giữ lại

| Cột | Lý do giữ, không có nguồn tương đương ở bảng khác |
|---|---|
| MatchId | Khóa định danh của trận, được participant/score/replay/report/ledger tham chiếu |
| Kind | Loại nghiệp vụ BotPractice, RankedSingles, MatchedDoubles hoặc Custom; chỉ nhìn profile bot, số người hay tỷ số không phân biệt đủ |
| Status | Vòng đời PREPARING/PLAYING/FINISHED/CANCELLED. MatchScore có thể chứa ứng viên khi trận vẫn PLAYING nên không thay trạng thái xác nhận trận |
| RatingPolicyVersion | Chính sách Elo cố định tại lúc tạo trận. Ledger chỉ có sau khi chốt trận được tính Elo; chưa có khi đang chơi và không có ở trận practice/gián đoạn |
| Mode | Chế độ Singles/Doubles đã chọn. Code hiện cho phép phòng Custom Doubles có ít hơn bốn người, nên không thể suy ra chắc chắn từ số participant |
| PhotonSession | Tên phiên Photon thực tế để đối chiếu request và reconnect; không phải lúc nào cũng là phép chuyển đổi cố định của MatchId |
| SearchId | Mã lượt tìm trận Redis dùng đối chiếu cấp bot/đội hình. Không có bảng tìm trận SQL tương ứng để JOIN |
| Region | Khu vực của trận, không lấy từ hồ sơ từng người hoặc trang bị |
| Night | Môi trường ngày/đêm đã chọn cho trận |
| Venue | Sân đã chọn cho trận; không có bảng sân tương ứng trong schema hiện tại |
| CreatedAt | Mốc tạo trận, dùng giới hạn thời gian xác nhận; khác lúc người chơi được tạo hoặc báo cáo kết quả |
| UpdatedAt | Mốc cập nhật vòng đời bản ghi trận; heartbeat/báo cáo của participant có ý nghĩa khác và không thay thế được |
| StartedAt | Mốc cả trận thực sự bắt đầu, sau khi xác nhận; không phải thời điểm tạo hay heartbeat |
| EndedAt | Mốc chốt cả trận; các mốc ReportedAt/FinalizedAt của từng người có thể khác và phục vụ việc xử lý riêng |
| Sequence | Thứ tự chốt lịch sử, dùng phân định khi timestamp trùng; không có cùng giá trị ở bảng khác |

Không xóa Mode bằng cách đổi quy tắc phòng Custom trong lượt này. Không lấy chính sách Elo từ cấu hình hiện tại hoặc metadata replay vì chúng không thay thế được dữ kiện lúc tạo trận. Tất cả dữ liệu còn lại mô tả chính trận này hoặc bảo đảm vòng đời của nó.

## Phạm vi đồng bộ

- SQL, entity Gamematch, Fluent API, finalization và truy vấn lịch sử Player/Admin.
- Fixture kiểm tra điểm/replay/AI chuyển sang ghi điểm gốc ở MatchScore.
- Database development và backend được cập nhật đồng bộ sau khi kiểm tra, theo quyền reset development đã được xác nhận.
- Conceptual Diagram giữ 38 entity/53 quan hệ và **không vẽ attributes**; chỉ cập nhật bằng chứng nguồn SQL.

Bằng chứng kiểm tra của lượt này được lưu trong `artifacts/normalized-game-matches-20261031/`. Không suy diễn việc build/kiểm tra API thành bằng chứng đã chạy lại gameplay nhiều máy hoặc đã sửa hiện tượng giật mạng.

## Kết quả kiểm chứng

Backend build thành công. Chín bộ kiểm tra đạt **269 assertions**, gồm 28 kiểm tra MatchScore (8 kiểm tra bổ sung cho lượt này), 33 relational match columns, 19 unified matches, 21 normalized participants, 81 online game, 20 configuration, 23 shared replay, 11 HTTP/S3 và 33 Hybrid. Bao gồm hướng điểm theo đội trong 2v2, lịch sử Player/Admin, ứng viên chưa công bố, rollback, retry, bỏ trận/gián đoạn và Custom Doubles thiếu người.

Đã xác minh đúng container/project development, tạo lại database từ SQL hiện tại và triển khai backend đồng bộ, readiness đạt. **15 kiểm tra HTTP trên image đang chạy** đạt; tài khoản/trận thử được dọn và không gửi email thật. Source hash/schema/diagram XML và JavaScript của preview được kiểm tra, không thêm entity hoặc attribute. Unity không cần thay DTO; chưa chạy lại gameplay nhiều client. Xem `artifacts/normalized-game-matches-20261031/summary.json`.
