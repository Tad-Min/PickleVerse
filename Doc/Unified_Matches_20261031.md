# Gộp trận, người tham gia và sửa FK luyện tập — 31/10/2026

Cập nhật mới nhất: GameMatches và GameMatchPlayers đều còn 15 cột; schema 38 bảng/265 cột/53 FK. Hai tổng game thắng của GameMatches đã bỏ, lấy MatchScore; ReceivedElo lấy ratingledger. Xem [Rà soát GameMatches](GameMatches_Normalization_20261031.md) và [Rà soát GameMatchPlayers](GameMatchPlayers_Normalization_20261031.md). Các cập nhật 276/263 cột dưới đây là lịch sử.

Đợt tiếp theo đã bỏ AppActivitySessions và xóa hai JSONB GameMatches.Data/GameMatchPlayers.Report, thay bằng cột/quan hệ. Schema mới nhất 38 bảng/276 cột/53 FK; xem [Relational Match Columns](Relational_Match_Columns_20261031.md). Các mô tả còn giữ JSON ở dưới ghi lại riêng đợt gộp bảng.

Đã thực hiện ba yêu cầu: gộp MatchHistory vào GameMatches; dùng GameMatchPlayers làm bảng người tham gia duy nhất; thay hai FK riêng của PlayerLessonDrills bằng FK ghép tới LessonDrills. Backend dùng Npgsql Entity Framework Core qua PickleVerseContext.

## Trận và kết quả người tham gia

- Không còn bảng/model MatchHistory hoặc PlayerMatchHistory. GameMatches giữ Kind, trạng thái, thời điểm tạo/bắt đầu/kết thúc và số game thắng của hai đội. Sequence được cấp lúc hoàn tất để giữ đúng thứ tự lịch sử khi nhiều kết quả cùng timestamp.
- GameMatchPlayers giữ một dòng cho mỗi cặp MatchId/PlayerId, từ lúc tham gia tới lúc hoàn tất. Team thay cho IsTeamA. Outcome, ReceivedElo, LocalForfeit và FinalizedAt lưu kết quả cuối; các trường kết quả còn trống khi chưa được xử lý.
- Kết quả cuối, điểm từng game trong MatchScore, Elo, thưởng và tiến trình được ghi trong cùng transaction. Cập nhật FinalizedAt có điều kiện để không xử lý một dòng người tham gia lần thứ hai. BotPractice lưu kết quả của cả người thật và profile bot hiện có; không phát thưởng cho profile bot trong trận luyện tập.
- Report giữ dữ liệu báo cáo trong lúc chờ xác nhận. Khi kết quả được backend chấp nhận, nó chứa bản kết quả do server xác nhận, không kèm mảng điểm. Các trường Outcome/Elo/finalization là nguồn cho lịch sử; điểm đọc từ MatchScore theo đội của người xem.
- Truy vấn lịch sử yêu cầu GameMatches.Status=FINISHED và người tham gia đã FinalizedAt. Trận đang chuẩn bị/đang chơi hoặc bị hủy trước khi chơi không tạo mục lịch sử.
- Các FK của MatchHistoryReplay, ReportPlayerInMatch, PlayerTechniquesErrors và ratingledger chuyển sang GameMatches. Replay chỉ được công bố sau khi trận hoàn tất; quyền xem theo GameMatchPlayers. API báo cáo kiểm tra cả người báo cáo và người bị báo cáo thuộc trận đã hoàn tất.
- Giữ các endpoint và DTO lịch sử hiện có. Endpoint cũ cho client tự tạo kết quả/Elo vẫn trả 410; không duy trì một đường ghi kết quả khác.

GameMatches.Data vẫn phục vụ trạng thái phiên/đội hình và metadata hiện có. Lượt này gộp các bảng trận/lịch sử và chuyển các trường kết quả chính vào bảng tương ứng; không phải đợt loại bỏ mọi JSON metadata của trận.

## PlayerLessonDrills

PK vẫn là (PlayerId, LessonId, DrillId). Chỉ giữ hai FK:

- PlayerId tới PlayerProfiles.UserId.
- (LessonId, DrillId) tới LessonDrills.(LessonId, DrillId).

Hai FK riêng tới Lessons và Drills đã bỏ. Xóa liên kết LessonDrills sẽ cascade tiến trình phụ thuộc như các quan hệ trước đây. Entity Playerlessondrill đi qua navigation LessonDrill; Fluent API ánh xạ FK ghép, không tạo shadow FK hoặc entity mới.

## Npgsql EF Core

Entity/model mới: Gamematch, Gamematchplayer và Matchscore. Các entity phụ thuộc trỏ tới Gamematch. Các luồng tạo trận, ghi kết quả, lịch sử, điểm và quyền đọc replay sử dụng EF Core queries/updates. Advisory lock, nextval và upsert dùng SQL tham số hóa trên kết nối/transaction EF Core. Một số luồng cũ khác của backend vẫn sử dụng GameSql; không diễn giải thay đổi này là toàn bộ backend đã chuyển sang LINQ.

Các package Npgsql.EntityFrameworkCore.PostgreSQL và Microsoft.EntityFrameworkCore hiện có được giữ nguyên. Dùng Database.sql tạo schema/seed trong môi trường development, không thêm migration.

## Kiểm tra và triển khai

Đạt 302 assertions trong 11 bộ kiểm tra: 19 kiểm tra riêng cho gộp bảng/FK/EF, cùng game, MatchScore, relational progress, Hybrid, replay, HTTP/S3, configuration, app/rank, invitations và Redis. Thêm 12 kiểm tra HTTP trên backend Docker, bao gồm API lịch sử cũ, kết quả người/bot và báo cáo liên kết trận mới. Tài khoản/trận thử được xóa sau kiểm tra; không gửi email thật.

Backend và Docker image build thành công. Đã tạo lại đúng database development pickleverse: 39 bảng/263 cột/54 FK; giữ 10 profile bot, 12 lesson/12 drill. API readiness trả 200. Chưa chạy lại gameplay nhiều Unity client hoặc đo NFR thiết bị trong lượt này.

Conceptual Diagram đã đồng bộ theo SQL: 39 entity/54 quan hệ, không vẽ attribute. Partial unique index giới hạn một TrainingSessions đang hiện hành không được diễn giải thành chỉ một lượt tập cho toàn bộ lịch sử. Xem artifacts/unified-match-checks-20261031/summary.json để tra bằng chứng.
