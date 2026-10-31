## Cập nhật mới nhất ngày 31/10/2026 — Backend phân tầng, UnitOfWork và ApiResponse

Theo góp ý tiếp theo của người dùng, phần bind cấu hình Redis và đăng ký singleton IConnectionMultiplexer đã chuyển trực tiếp về API/Program.cs, lấy section Redis qua builder.Configuration (appsettings/environment). RedisSettings chuyển thành model cấu hình thuần ở Entities/Configuration, không đọc IConfiguration; Repositories chỉ nhận settings/kết nối qua DI. Giữ nguyên Project Reference đã chốt.

Người dùng chốt kiến trúc Project Reference: Repositories → Entities; Services → Entities, Repositories; API → Entities, Services, lấy SmashClub ở D:/Repository/SmashHub/BESmashClub làm mẫu. Đã đưa DTO/exception về Entities, service vào Interfaces/Implementations, truy cập dữ liệu qua IUnitOfWork.Game/repository; API không còn dùng repository/EF trực tiếp. Redis nằm Repositories/Caching; AI worker nằm Services/Workers. Backend không còn link source GameRules từ Unity; rules/model backend do backend quản lý.

Đã bổ sung mapping EF cho các bảng/cột đã có, dùng EF cho truy vấn/update thông thường; PostgreSQL locks/sequence/upsert/atomic leasing nằm repository. Giữ transaction kết quả/Elo/thưởng và chống lặp. Không sửa schema hoặc reset database trong lượt này. Sửa default JSON Errors/Config cho khớp SQL; lỗi nội bộ được wrap giữ HTTP 500.

API JSON dùng ApiResponse<T>/ApiResponse qua filter và exception handler tập trung, giữ HTTP status, error code và validation errors; Swagger mô tả envelope. File replay download giữ nguyên nội dung file. Unity ApiClient và frontend Axios đã unwrap data; frontend giữ login khi gặp 403. Các client bên ngoài repo cần thích ứng với response envelope mới.

Đạt 613 assertions/19 bộ, backend Debug/Release 0 warning/0 error, frontend type-check/build đạt. Có 39 schema checks và 33 HTTP/architecture/Unity-client checks; test email không gửi SMTP thật. Đã build/recreate riêng pickleverse_api, đạt thêm 11 live HTTP checks, readiness/Swagger/OpenAPI 200; AI fixture đã dọn và không còn database test tạm. Chưa kiểm tra gameplay/UI Unity nhiều client. Chi tiết Doc/Backend_Layering_20261031.md, bằng chứng artifacts/backend-layering/summary.json và live-smoke.json.

## Đợt trước ngày 31/10/2026 — Bật AI service development

Theo yêu cầu người dùng, đã bật runtime AI ingest/dispatcher/coaching trong `.env` local được Git bỏ qua, tạo secret riêng dùng khớp FastAPI/backend. Model ML vẫn tắt, phân tích dùng rules-v1/curriculum. Hai container đã tạo lại, AI healthy, `/ready` và backend readiness/Swagger 200. Catalog thật truyền `hybridTelemetryEnabled=true`, `hybridCoachingEnabled=true` xuống Unity. Asset API vẫn là localhost:8080. Các dòng “AI runtime vẫn tắt” bên dưới là lịch sử trước lần bật này.

Sửa healthcheck AI từ `/health` sang `/ready` trong Dockerfile và Compose vì trước đó container healthy dù thiếu key, readiness 503. Bài tải phát hiện PostgreSQL 53300 vì pool 100 tranh ngân sách tổng 100 kết nối; Compose API và benchmark giờ dùng pool 40 (Compose đổi qua POSTGRES_MAX_POOL_SIZE). Bổ sung response body khi benchmark HTTP thất bại để chẩn đoán. Không sửa schema/reset DB. Image Python hiện có khớp cả năm file source sau chuẩn hóa xuống dòng; không cập nhật dependencies.

Kiểm tra mới: build backend/IntegrationChecks 0 warning/0 error, Python 11/11, Hybrid 33/33, HTTP/load 6/6 với 112 ACK/112 completed; steady ingest p95 41,30 ms, ready p95 1.031,75 ms; burst drain 2,88 giây. Live API→worker→FastAPI→SQL shadow 14/14, coaching bật 16/16, summary ready và recommendation đọc được. Fixture SQL/database tạm/process test đã dọn. Chưa chạy gameplay/UI Unity hoặc thiết bị thật; chưa có model production. Hướng dẫn thử trong game: `Doc/AI_Service_Activation_20261031.md`; bằng chứng: `artifacts/ai-service-activation-20261031/`.

## Đợt trước ngày 31/10/2026 — Chuẩn hóa GameMatches

Đã bỏ GameMatches.TeamAScore/TeamBScore vì là số game thắng đếm được từ MatchScore.Completed và điểm hai đội. GameMatches còn 15 cột; schema hiện tại 38 bảng/265 cột/53 FK. Điểm gốc vẫn ở MatchScore; DTO gamesWon/gamesLost và hai tổng ở lịch sử Player/Admin được tính khi đọc. Không thêm bảng/entity/view/JSON. Bot result tính lại số game hoàn tất; retry không tin tổng gamesWon/gamesLost dư của client. Kết quả bỏ trận/gián đoạn vẫn lấy Outcome, không suy ra từ số game.

Giữ Mode: code Custom Doubles hiện cho phép thiếu người nên không thể suy ra chỉ từ roster. Giữ RatingPolicyVersion vì cần trước khi có ledger; giữ các cột môi trường/kết nối và mốc vòng đời riêng. Xem Doc/GameMatches_Normalization_20261031.md và artifacts/normalized-game-matches-20261031/. Những số 267/276 bên dưới là lịch sử trước khi bỏ hai tổng của GameMatches.

Đã build backend, đạt 269 assertions trong 9 bộ kiểm tra và 15 HTTP checks trên image development đang chạy; database đã tạo lại từ SQL, readiness đạt, fixture được dọn. Sơ đồ giữ 38 entity/53 quan hệ/không attributes, source hash/XML/JS khớp. Không thay Unity DTO; chưa chạy lại gameplay nhiều client.

## Đợt trước ngày 31/10/2026 — Chuẩn hóa GameMatchPlayers

Người dùng yêu cầu không lưu các cột lấy/tính được từ bảng khác. Đã bỏ 9 cột GameMatchPlayers: PlayerName, IsMale, PaddleId, IsBot, Points, Accepted, RosterReady, ReceivedElo, Fingerprint. Bảng còn 15 cột; schema 38 bảng/267 cột/53 FK. Tên/giới tính/vợt đọc hồ sơ/inventory hiện tại; bot nhận diện từ profile UUID đã có; points cộng từ MatchScore; Elo delta từ ratingledger; ready từ Redis/StartedAt; đối chiếu kết quả tính lại từ dữ liệu chuẩn. Không thêm bảng/JSON thay thế. C# Ratingledger chỉ ánh xạ bảng SQL đã có sang EF, không thêm entity nghiệp vụ.

Giữ EloBefore vì là đầu vào cố định theo trận, gồm Elo bot được cấp theo đối thủ, không thể lấy từ Elo hiện tại và chưa có ledger khi đang chơi/PvE. Giữ các dữ kiện phân đội, party, thứ tự actor, quyền replay, thống kê cú đánh, lý do kết thúc và các mốc xử lý riêng của người tham gia. Đổi tên sẽ đổi tên hiển thị trong lịch sử; replay S3 giữ nội dung đã ghi. Giữ API/DTO Unity. Bot points bỏ qua tổng dư do client khai, tính từ scores trước khi cộng tiến trình; luật bỏ trận không cấp tiến trình/thưởng không đổi.

Đã tạo lại đúng DB development và triển khai backend, readiness 200. Đạt 387 assertions trong 14 bộ và 14 HTTP checks trên image đang chạy; backend và Unity C# build đạt. Sơ đồ vẫn 38 entity/53 quan hệ, không attribute; chưa chạy lại gameplay nhiều client. Xem Doc/GameMatchPlayers_Normalization_20261031.md và artifacts/normalized-match-players-20261031/summary.json. Các mô tả snapshot/24 cột/276 cột bên dưới là lịch sử. Chẩn đoán giật multiplayer trước đó chỉ audit source (Doc/Multiplayer_Jitter_Diagnosis_20261031.md); chưa chuyển Dedicated Server hoặc sửa runtime chuyển động.

## Đợt trước ngày 31/10/2026 — Xóa JSON Data/Report của trận

Người dùng yêu cầu tách hết dữ liệu thành cột rồi xóa GameMatches.Data/GameMatchPlayers.Report. Đã bỏ cả hai; hai bảng không còn JSON/JSONB/blob thay thế, không thêm entity. Schema hiện tại 38 bảng/276 cột/53 FK. GameMatches có Status PREPARING/PLAYING/FINISHED/CANCELLED, RatingPolicyVersion, Mode, PhotonSession, SearchId UUID, Region, Night, Venue. GameMatchPlayers có snapshot tên/Elo/giới tính/vợt/group/bot/roster-ready/thứ tự đội hình, IsReplayWriter, Points/ValidReturns/ValidServes/EndReason và ReportedAt. Không thêm lại OwnerId hoặc HostPlayerId; writer là quyền của người tham gia, có partial unique index.

RelationalMatch dựng DTO từ cột/quan hệ. Lịch sử/opponents dùng snapshot lúc tạo trận; điểm từ MatchScore, opponentElo từ đội đối diện. Kết quả gửi đầu tiên lưu một bộ điểm ứng viên theo đội A vào MatchScore và biên nhận scalar; các bên phải cùng fingerprint, chỉ công bố/cấp thưởng khi trận FINISHED. Kết quả cuối, Elo, thưởng và tiến trình cùng transaction; kiểm tra rollback/retry đạt. Cờ Completed của MatchScore chỉ nói game hoàn tất, không thay trạng thái xác nhận trận. Trạng thái PLAYING của PvE không giữ chỗ bot rank, giữ hành vi tái sử dụng virtual profile trước đây.

Đã tạo lại đúng DB dev và triển khai backend; 366 assertions trong 13 bộ, 14 HTTP checks trên image đang chạy, backend/Unity C# build đạt. Sơ đồ vẫn 38 entity/53 quan hệ, không attribute. API/DTO Unity giữ nguyên; chưa chạy lại gameplay nhiều client. Chi tiết Doc/Relational_Match_Columns_20261031.md và artifacts/relational-match-columns-20261031/. Các đoạn 257/263 cột và Data/Report còn giữ bên dưới là lịch sử. AI coaching runtime vẫn tắt.

## Đợt trước ngày 31/10/2026 — Phiên hoạt động trong Redis

Người dùng yêu cầu chuyển trạng thái phiên sang Redis, chỉ giữ tổng trong PlayerProfiles rồi xóa AppActivitySessions. Đã bỏ bảng/quan hệ khỏi src/Database.sql và sơ đồ; schema còn 38 bảng/257 cột/53 FK, không thêm bảng/cột. RedisAppActivity giữ opening/owner, bộ đếm/coverage theo người chơi và biên nhận tổng trước/sau; key TTL 8 ngày, phiên tối đa 7 ngày. OnlineGameService.Activity dùng khóa dòng/ExecuteUpdate Npgsql EF Core. Redis được ghi trước SQL commit để request sau khôi phục SQL rollback hoặc nhận diện commit thành công dù mất response.

Unity giữ nguyên hai API và DTO, bổ sung HTTP 410 để tạo UUID mới và đếm lại từ hiện tại khi key hết hạn/mất. Không cộng lại elapsed phiên cũ; generation ngăn phiên cũ dùng sau khi mất riêng key người chơi. Tổng SQL đã commit còn nguyên khi mất Redis; phần chưa commit/chưa gửi không được bảo đảm nếu mất trạng thái Redis. Chi tiết Doc/Redis_App_Activity_20261031.md và artifacts/redis-activity-checks-20261031/. Các đoạn 39 bảng bên dưới là lịch sử trước thay đổi này. AI coaching vẫn tắt; không có thay đổi nghiệp vụ trận/luyện tập.

Đã tạo lại đúng DB development từ SQL và triển khai image cuối, readiness 200. Đạt 206 assertions trong 6 bộ (31 activity Redis, 22 app/rank, 81 online game, 25 relational progress, 28 Redis, 19 unified matches) và 9 HTTP checks trên image đã triển khai; đã dọn fixture SQL/Redis. Backend và Unity FrontEnd cùng các assembly phụ thuộc build thành công. Sơ đồ 38 entity/53 quan hệ, không attribute; source hash/XML/JS/render đã kiểm tra, xem PNG CD-06 trực tiếp. Chưa chạy lại gameplay nhiều Unity client. Redis snapshot cũ lệch tổng SQL sẽ đổi generation và giữ tổng SQL; opening trước mốc mất trạng thái không được backfill khi không còn coverage. Bằng chứng artifacts/redis-activity-checks-20261031/summary.json.

## Đợt trước ngày 31/10/2026 — Gộp bảng trận, FK luyện tập, Npgsql EF Core

Đã gộp MatchHistory vào GameMatches và bỏ PlayerMatchHistory; GameMatchPlayers là bảng người tham gia duy nhất. GameMatches có StartedAt/EndedAt/Sequence và số game thắng hai đội; GameMatchPlayers có Outcome/ReceivedElo/LocalForfeit/FinalizedAt. Kết quả người và bot ghi cùng transaction với điểm/Elo/thưởng, history/replay chỉ đọc trận hoàn tất và participant đã finalized. FK replay/report/lỗi/rating chuyển sang GameMatches. Giữ MatchScore và MatchHistoryReplay theo thiết kế đã thống nhất. Không loại bỏ mọi metadata JSON trận trong lượt này.

Đã sửa PlayerLessonDrills: giữ PK ba cột và FK player; thay hai FK Lessons/Drills bằng FK ghép (LessonId,DrillId) tới LessonDrills. ORM navigation qua LessonDrill; không thêm entity. Người dùng yêu cầu backend dùng Npgsql EF Core: đã lưu vào AGENTS.md, dùng PickleVerseContext/Fluent API/EF queries/updates cho các luồng trận/lịch sử/điểm/replay đã sửa. SQL đặc thù PostgreSQL dùng cùng EF connection/transaction; các service cũ ngoài phạm vi này còn GameSql.

Đã tạo lại DB dev và triển khai backend đồng bộ: 39 bảng/263 cột/54 FK, 10 profile bot, 12 lesson/12 drill, readiness 200. Đạt 302 assertions trong 11 bộ kiểm tra, cộng 12 kiểm tra HTTP trên Docker API; đã dọn tài khoản/trận thử. Sơ đồ 39 entity/54 quan hệ, không attribute; sửa cách suy luận cardinality để partial unique index của TrainingSessions không bị coi là unique toàn bảng. Chưa chạy lại gameplay nhiều Unity client. Chi tiết Doc/Unified_Matches_20261031.md và artifacts/unified-match-checks-20261031/summary.json. Các đề xuất gộp bảng và FK trước đó nay đã thực hiện; AI coaching runtime vẫn chưa bật.

## Đợt trước ngày 31/10/2026 — MatchScore

Người dùng đồng ý thêm bảng điểm và đổi tên thành MatchScore (chính xác, không phải GameScores). Đã tạo MatchScore với PK (MatchId, GameNumber), FK tới GameMatches, điểm hai đội và Completed. Mỗi game một dòng chung cho cả 1v1/2v2, tối đa ba game. Backend ghi cùng transaction kết quả/thưởng; lịch sử đọc bảng và đổi góc nhìn theo GameMatchPlayers.Team. Điểm chính thức không còn trong GameMatches.Data.result/results; điểm tạm trong client Report được bỏ khi trận hoàn tất. Bot result receipt dùng SHA-256. Xem Doc/MatchScore_20261031.md.

Đã tạo lại đúng DB dev và triển khai backend đồng bộ: 41 bảng/265 cột/57 FK; 10 profile bot, 12 lesson/12 drill; API readiness 200. Đạt 235 assertions trong 8 bộ kiểm tra (20 MatchScore, 81 game, 25 relational progress, 22 app/rank, 33 Hybrid, 20 configuration, 23 replay, 11 HTTP/S3), cộng 9 kiểm tra HTTP trên API Docker. Tài khoản/trận thử đã được xóa. Sơ đồ 41 entity/57 quan hệ, không vẽ attribute. Chưa chạy lại gameplay nhiều Unity client. Các đề xuất gộp MatchHistory/GameMatches, gộp GameMatchPlayers/PlayerMatchHistory và sửa FK PlayerLessonDrills vẫn chưa được thực hiện; các lượt đó là trao đổi thiết kế. AI coaching runtime vẫn tắt, chưa kích hoạt trong lượt thêm MatchScore.

## Đợt trước ngày 31/10/2026 — Bỏ GameProgress

Đã bỏ hoàn toàn GameProgress và việc lưu cả trạng thái người chơi vào JSON. Backend lắp API state từ các bảng nghiệp vụ. Bổ sung cột có mục đích trong PlayerProfiles và PlayerLessonDrills; PlayerMissions dùng PK (PlayerId, MissionId, PeriodKey). TrainingSessions có IsCurrent/Sequence; MatchHistory có Sequence để giữ đúng thứ tự khi timestamp bằng nhau. GameCommands vẫn chống lặp và lưu biên nhận thưởng tổng kỳ. Hybrid coaching đọc cùng dữ liệu luyện tập từ bảng chuyên biệt. Không thêm bảng/entity mới; không chuyển full-progress JSON sang cột khác.

Đã tạo lại đúng database development pickleverse và triển khai backend đồng bộ: 40 bảng/260 cột/56 FK; 10 profile bot, 12 lesson/12 drill; API readiness 200. Đạt 263 assertions trong 9 bộ kiểm tra (25 relational progress, 81 game, 22 app/rank, 33 Hybrid, 23 replay, 20 configuration, 20 invitations, 28 Redis, 11 HTTP/S3), cộng 6 kiểm tra HTTP trên API Docker đang chạy. Tài khoản thử được xóa sau kiểm tra, không gửi email thật. Sơ đồ có 40 entity/56 quan hệ, không vẽ attribute. Chưa chạy lại gameplay nhiều Unity client trong lượt này. Xem Doc/Relational_Progress_20261031.md và artifacts/relational-progress-checks-20261031/summary.json. Các số 41/43 bảng và kiểm tra replay bên dưới là bằng chứng của các đợt trước thay đổi này.

## Đợt trước ngày 31/10/2026 — Replay dùng chung

Đã thay OnlineReplays/OnlineReplayChunks bằng bảng MatchHistoryReplay duy nhất (MatchId PK/FK tới MatchHistory, ObjectKey trỏ manifest S3). Mỗi trận một replay; manifest và chunk gzip/hash nằm trên S3. Chỉ máy điều phối ghi, người tham gia và Admin đọc cùng replay qua API có kiểm tra quyền. Backend dùng MatchReplayService, Unity chỉ ghi trên máy điều phối; không thêm entity mới. Xem Doc/Replay_Storage_20261031.md.

Đã tạo lại database development pickleverse và triển khai backend đồng bộ: 41 bảng/235 cột/57 FK, 10 profile bot, 12 lesson/12 drill, không còn GameMatches.OwnerId. API /health/ready trả 200. Lượt này đạt 219 assertions (gồm 11 HTTP/S3 thật); backend, Docker image và Unity Multiplayer C# build thành công. Chưa chạy lại gameplay nhiều Unity client. Sơ đồ Conceptual hiện có 41 entity/57 quan hệ, không vẽ attribute. Các số 43 bảng trong những đoạn lịch sử bên dưới là trước đợt hợp nhất replay này.

## Xác nhận của người dùng ngày 31/10/2026 — Database development

Người dùng xác nhận dự án hiện ở môi trường development và cho phép drop/recreate database development của dự án khi cần đồng bộ thay đổi schema, không cần hỏi lại. Dùng một file src/Database.sql cho schema và seed; bỏ qua, không tạo migration. Phải xác định đúng instance/database development trước khi reset, không áp dụng quyền này sang production hoặc dự án khác. Đồng bộ backend đang chạy với schema và chỉ dọn cache riêng của dự án nếu cần. Chính sách này cũng được lưu tại AGENTS.md ở gốc repository.

- Tỷ lệ theo Elo là **khả năng đánh dính bóng**, không phải tỷ lệ cố tình đánh ra ngoài. Từ lần sửa mới nhất, bot **không áp dụng xác suất hụt ngẫu nhiên lúc giao bóng**; xác suất hụt dùng cho cú trả bóng. Điều này không loại bỏ kiểm tra contact/luật sân.
- Lời mời bạn bè đã được nâng từ hộp chia sẻ Room Code thành danh sách có tìm kiếm/nút Mời và thông báo Chấp nhận/Từ chối. Cần quan hệ bạn bè và trạng thái lobby hợp lệ; TTL 60 giây, polling khoảng 3 giây, presence lease 20 giây.
- Chưa tạo bảng Rooms vì phòng đang hoạt động do Fusion quản lý, trạng thái lời mời/presence nằm Redis. Đề xuất tách mã join khỏi session để đổi mã trong chat không tự có nghĩa tính năng đổi mã đã hoàn thành.

### 4.3. Tiền tệ, thời gian, replay và dữ liệu nền

- **TOKENPAD** tương ứng coin; **ELITE TOKEN** tương ứng premium/gem. Đã yêu cầu bỏ số dư/thưởng premium miễn phí; chưa có hệ thống nạp tiền thật. Không ghi nhận IAP đã triển khai.
- Thời gian hoạt động tính **từ lúc mở app**, gồm đăng nhập/menu/tìm trận/training/trận đấu. Redis quản lý phiên/coverage/chống cộng trùng; PostgreSQL chỉ giữ tổng trong PlayerProfiles.TimePlayedSeconds. AppActivitySessions đã bỏ theo yêu cầu mới nhất.
- Replay đã chuyển từ local sang API/S3 trong luồng chạy chính. Local replay adapter còn phục vụ định dạng cũ/test. Telemetry spool lưu local là hàng đợi gửi AI, không phải profile hoặc offline progression.
- SQL phát triển thống nhất ở **một file `src/Database.sql`**, khởi tạo schema + seed trong một transaction. Người dùng đã yêu cầu bỏ Migrations và dùng database phát triển mới khi đổi schema; không tự tái tạo database đang dùng trong một nhiệm vụ không yêu cầu việc đó.
- `GameSettings` đã bỏ; cấu hình thưởng/EXP chuyển vào `appsettings.json`. `RankedSearches` và `AppActivitySessions` đã bỏ để chuyển trạng thái tạm sang Redis.
- Shared C# rules được backend compile trực tiếp từ một số file Unity. Khi sửa thuật toán thưởng, training, Elo hoặc model dùng chung, phải xem tác động cả Unity và .NET.

### 4.4. Cách làm việc cần kế thừa

- Người dùng làm việc bằng tiếng Việt, ưu tiên Windows/Unity Editor trước; hỗ trợ mobile vẫn cần đối chiếu proposal và nghiệm thu riêng.
- Giữ bố cục/style UI đã thống nhất; lịch sử có yêu cầu không tự thêm thành phần UI khi chưa được yêu cầu.
- Các chat từng yêu cầu duyệt phạm vi sửa backend và sau đó đã duyệt nhiều đợt cụ thể. Đọc đúng phạm vi yêu cầu mới và những chấp thuận đã có; không coi mọi câu hỏi hoặc đề xuất cũ là chỉ thị đang chờ thi hành.
- Trước khi chạy công cụ Unity, kiểm tra thay đổi scene chưa lưu. Đã từng có Menu chưa lưu và hộp thoại gây treo test; không tự Save, Don't Save hoặc buộc tắt Editor làm mất thay đổi.
- Không kết luận hoàn thiện bằng việc chỉ build thành công, số test lớn hoặc giao diện chạy mock. Ghi rõ môi trường, commit, test mới hay bằng chứng lịch sử.

## 5. Những đợt công việc đã làm được ghi trong chat

Đây là tóm tắt các đợt Codex được lịch sử ghi nhận, không quy toàn bộ commit của các thành viên dự án cho Codex.

| Giai đoạn/chủ đề | Công việc đã thực hiện | Trạng thái cần hiểu khi tiếp tục |
| --- | --- | --- |
| UI/icon ban đầu | Thiết kế bộ Home, Progression, Career, Collection, Store, Settings, Coin trên Figma | Đã có kết quả thiết kế; riêng yêu cầu đưa cả bộ vào Unity không có kết luận hoàn tất trong chat đó |
| Movement/animation | Chẩn đoán Rigidbody/Vector2 làm mất trục Z/phím phải; tách và đặt tên 83 clip từ 44 model; tạo controller nam/nữ | Đã có nhiều lần mở rộng và đồng bộ animation về sau; không chỉ dựa vào controller của bản đầu |
| Multiplayer prototype | UI 1v1/2v2/party/custom; tìm/hủy, ready, chia đội, reconnect, Scene Hash/Build Settings, HUD và đồng bộ sân/nhân vật | Bằng chứng UGS/Relay/protocol 3–7 là lịch sử, đã được Photon thay thế |
| Profile/progression local | Profile, XP/level, Elo/rank, history, achievement, nhiệm vụ ngày/tuần, dev player editor, ví tiền | Đã làm bản local và test/build; nguồn dữ liệu runtime nay đã chuyển sang server |
| Replay/streak/Entites/BhTree | Replay 3D snapshot/chunk, pause/seek/speed; daily streak UTC; gom entity/assembly; Unity Behavior 1.0.16 theo Elo | Đã triển khai, sau đó chuyển replay online và hợp nhất bot trong Hybrid |
| Rà soát tính năng/SQL | Đánh giá phần hardcode so với Core Features/proposal, tạo schema PostgreSQL và audit ban đầu | Schema từng là 71 bảng, rồi thay đổi theo backend; hiện phải dùng 38 bảng trong SQL mới |
| Kết nối backend | Tạo Services/API/Utilities, register/OTP/JWT/profile/logout; Docker/SMTP/Swagger, sửa quyền Admin | Luồng Unity account đã chạy Editor/Windows; không đồng nghĩa admin web login đã đúng |
| Chuyển toàn bộ game online | Seed từ Unity, training/shop/equip/claim/social/history online, Photon Fusion, replay S3, 1v1/2v2/ready/reconnect | Có smoke thật Editor/Windows và 4 client trên cùng máy; chưa thay thế ma trận thiết bị/mạng hiện tại |
| Activity/bot rank/cấu hình | Tính thời gian toàn app, 10 bot UUID, fallback trong Photon, tổng kết server, đưa thông số vào config | Đã thực hiện; timer hiện 180/10 theo thay đổi sau cùng |
| Redis | Queue Singles/Doubles/party, ready consent, presence/invites, leaderboard Sorted Set 15 giây; bỏ RankedSearches | PostgreSQL tiếp tục giữ activity/Elo/kết quả |
| Merge/Console/font | Resolve các conflict code/scene, khôi phục mesh giữ GUID; sửa Close trước Awake, font tiếng Việt/data bị '?', camera/audio, vòng đời Fusion | Có test và runtime evidence theo từng đợt; không bảo đảm mọi build sau đó không thể phát sinh lỗi mới |
| Room/replay fixes 24–25/10 | Chờ Fusion connect/shutdown đúng trước đổi scene; gắn nút Mời; sau đó thêm invite accept/decline; sửa raycast WatchReplay | Bản share-code cũ đã được invite picker thay thế; click replay đã kiểm tra bằng lịch sử thật |
| Hybrid 27/10 | Chung graph/adapters local-online, Elo chuẩn/ledger, telemetry spool/batch, worker/lease/retry, FastAPI/rules/recommendation, classifier pipeline, sửa bot serve | Code và kiểm tra cục bộ đã có; flags còn tắt, model production chưa có, UAT thiết bị còn thiếu |
| Kiến trúc/đánh giá | Tạo Archify runtime HTML và source snapshot; bộ tiêu chí; checklist 183 thành phần và audit evidence | Là tài liệu/kiểm tra; không phải đã sửa các lỗi được audit phát hiện |

Ngoài chat, source còn có hướng dẫn và tài nguyên cho training/tutorial, touch controls, âm nhạc/SFX, HUD, cinematic, nhiều sân, camera/loading, animation/secondary motion và tối ưu mobile. Xem các báo cáo theo chủ đề trong `Doc/Proccess/`. Sân/cây cỏ mới được ghi ở [Stadium Refinement](D:/Repository/PickleVerse/Doc/Stadium_Refinement_20261025.md) và [Natural Park](D:/Repository/PickleVerse/Doc/Natural_Park_20261026.md); nguồn Blender ở `ArtSource/Stadiums/`, runtime ở `My project/Assets/EnvironmentContent/`. Không coi dung lượng GLB hoặc fixture Editor là số đo FPS/APK trên điện thoại thật.

## 6. Bản đồ source để tìm đúng chỗ sửa

Các đường dẫn trong bảng này tính từ **D:/Repository/PickleVerse**; đây là quy ước tra cứu trong file bàn giao để dễ chuyển repository sang máy khác.

| Chức năng | File/thư mục chính |
| --- | --- |
| Unity account/API/URL | `My project/Assets/Scripts/Services/AccountRuntime.cs`, `AccountService.cs`, `Api/ApiClient.cs`, `Api/UnityApiTransport.cs`, `Api/BackendApiSettings.cs`, `Utilities/ApiUrl.cs` |
| Giá trị API Unity thực dùng | `My project/Assets/Resources/BackendApiSettings.asset` |
| Login và dashboard Unity | `My project/Assets/Scripts/FrontEnd/AccountScreen.cs`, `PlayerProgressionMenu.cs`, `PlayerProgressionMenu.Training.cs` |
| Tiến trình online và catalog model | `My project/Assets/Scripts/Progression/ProgressionRuntime.cs`, `ProgressionEngine.cs`, `OnlineGameModels.cs`, `TrainingModels.cs` |
| Thời gian app | `My project/Assets/Scripts/Services/AppActivityRuntime.cs`; `src/Backend/Services/Game/OnlineGameService.Activity.cs` |
| Entities/assembly dùng chung | `My project/Assets/Scripts/Entites/`; danh sách `<Compile Include>` trong `src/Backend/Services/Services.csproj` |
| Bot/gameplay/training | `My project/Assets/Scripts/LocalMatch/BotMatch.cs`, `LocalBot.cs`, `BhTreeBot.cs`, `AdaptiveBotContext.cs`, `TrainingDirector.cs`, `TrainingStars.cs`, `PaddleContact.cs` |
| Behavior graph | `My project/Assets/Resources/BhTree/Bot Behavior.asset` |
| Elo/contact chance | `My project/Assets/Scripts/Progression/EloPolicy.cs`, `BotContactChance.cs`; `My project/Assets/Scripts/Entites/Multiplayer/RankedBotAccounts.cs` |
| Photon/online Unity | `My project/Assets/Scripts/Multiplayer/PhotonSession.cs`, `MultiplayerApp.Online.cs`, `NetworkMatch.Photon.cs`, `NetworkMatch.Bots.cs`, `Services/MultiplayerSceneConfiguration.cs` |
| Phòng và mời bạn | `My project/Assets/Scripts/Multiplayer/Services/RoomService.cs`, `Services/RoomInvitationClient.cs`, `RoomInvitationsRuntime.cs`; `src/Backend/Services/Game/RoomInvitationService.cs` |
| Social/leaderboard | `My project/Assets/Scripts/FrontEnd/OfflineSocialMenu.cs` và `.Presentation.cs`; `src/Backend/Services/Game/OnlineSocialService.cs`, `LeaderboardCache.cs` |
| Replay | `My project/Assets/Scripts/Replays/ReplayRecorder.cs`, `OnlineReplayRepository.cs`, `ReplayView.cs`; `src/Backend/Services/Game/MatchReplayService.cs` |
| Shop/equip | `My project/Assets/Scripts/Shop/OnlinePlayerEconomy.cs`; `src/Backend/Services/Game/OnlineGameService.cs` và các partial liên quan |
| Telemetry/coaching Unity | `My project/Assets/Scripts/Progression/CoachingRuntime.cs`, `HybridAiModels.cs`; `My project/Assets/Scripts/Multiplayer/NetworkMatch.Coaching.cs` |
| API/game backend | `src/Backend/API/Program.cs`, `Controllers/GameController.cs`; `src/Backend/Services/Game/OnlineGameService*.cs`, `GameCatalogService.cs`, `GameSql.cs`, `RedisGameState.cs` |
| Hybrid backend | `src/Backend/Services/Game/HybridCoachingService.cs`, `HybridAiMetrics.cs`; `src/Backend/API/Workers/HybridAiWorker.cs`; `src/Backend/Services/Configuration/HybridAiSettings.cs` |
| FastAPI/ML | `src/AiService/app/main.py`, `engine.py`, `tier.py`, `models.py`, `train.py`, `tests/`; `contracts/hybrid-ai/session-v1.json` |
| Auth/SMTP/admin backend | `src/Backend/Services/Implementations/AuthService.cs`, `SmtpEmailSender.cs`, `AdminService.cs`; `src/Backend/Services/DTOs/AllDTOs.cs` |
| Admin browser | `src/Frontend/src/`, đặc biệt auth, API client/admin API và mock mode; `src/Frontend/package.json` |
| Schema/seed/config | `src/Database.sql`, `src/Seed/UnityCatalog.json`, `src/Backend/API/appsettings.json`, `docker-compose.yml` |
| Kiểm thử backend | `src/Backend/IntegrationChecks/`; là chương trình kiểm thử riêng, không phải dependency runtime của game |
| Kiểm thử Unity | `My project/Assets/Tests/`; các helper Editor và kết quả riêng ở `My project/Tools/`, `artifacts/`, `UnityWork/` |

Tên `OfflineSocialMenu` còn sót từ prototype nhưng UI hiện gọi dữ liệu online. Không kết luận tính năng đang offline chỉ từ tên class. Backend có một số endpoint legacy đã trả **410**; đối chiếu controller và lời gọi runtime trước khi sửa service cũ.

Scene chính ở `My project/Assets/Scenes/`: `UI/Menu.unity`, `UI/ModeSelection.unity`, `UI/PlayMultiplayer.unity`, `UI/Loading.unity`, `PhotonLobby.unity`, `BotMatch.unity`, `MultiplayerCourt.unity`, `Tutorial.unity`, `Training.unity`, `Shop.unity`, `Collection.unity`, `Replay.unity`. Kiểm tra Build Profile và danh sách scene chung trước khi build; đây từng là nguyên nhân lỗi Scene Hash trên máy mới pull.

## 7. Cấu hình thực tế đã đối chiếu ở lượt bàn giao

### Unity và backend

| Cấu hình | Giá trị trong source hiện tại |
| --- | --- |
| Unity Editor | `6000.6.2f1` |
| Unity Behavior | `com.unity.behavior` `1.0.16` |
| URP | `17.6.0` |
| Base URL trong BackendApiSettings.asset | `http://localhost:8080` |
| allowRemoteHttpInDevelopment | `1` / bật |
| Timeout Unity API | 30 giây |
| OTP | Hiệu lực 10 phút; gửi lại sau 60 giây; tối đa 5 lần sai |
| Access/refresh token | 2 giờ / 14 ngày |
| Matchmaking | `BotFallbackSeconds: 180`, `ReadyConfirmationSeconds: 10` |
| Online heartbeat/disconnect | 8 giây / 35 giây |
| Leaderboard Redis cache | 15 giây |
| Hybrid | Ingest/Dispatcher/Coaching/Model đều `false` trong appsettings |
| Rating policy | `elo-v2` |

Đã từng sửa Unity gọi `http://tad-min.io.vn:8080` ở một chat khác, nhưng **asset trong checkout hiện tại đã là localhost**. Không tự đổi lại domain khi chưa biết mục tiêu triển khai. Sửa giá trị mặc định ở `BackendApiSettings.cs` **không cập nhật asset có sẵn**; đổi URL trong Inspector của asset rồi lưu. HTTP ngoài localhost chỉ được cho trong Editor/Development Build khi bật cờ; release vẫn yêu cầu HTTPS.

Các giá trị trên là **source**, có thể bị biến môi trường/container hoặc catalog đang cache ghi đè. Lượt bàn giao không đọc `.env` và không kiểm tra lại container đang chạy. Đợt audit trước đó ghi nhận bốn cờ Hybrid runtime cũng đều `false`.

### Docker và thay đổi cấu hình

Compose có `postgres`, `pgadmin`, `s3-storage`, `redis`, `ai-service`, `backend-api`. Mặc định các cổng host bind loopback:

| Dịch vụ | Từ máy Windows | Từ container cùng network |
| --- | --- | --- |
| API/Swagger dev | `http://localhost:8080`, `/swagger`, `/health/ready` | `backend-api:8080` |
| PostgreSQL | `localhost:15432` | `postgres:5432` |
| pgAdmin web | `http://localhost:15050` | pgAdmin phải kết nối DB bằng host `postgres` |
| Redis | `localhost:16379` | `redis:6379` |
| S3 dev | `localhost:19000` | `s3-storage:9000` |
| FastAPI | Compose không publish cổng ra host | `ai-service:8000` |

Các cổng có thể đổi qua `.env`; không suy ra server domain public dùng được chỉ vì Docker local sẵn sàng. Máy này từng có PostgreSQL khác tên `my_postgres` ở cổng 5432, khác `pickleverse_postgres` ở 15432; xác định đúng instance trước thao tác dữ liệu.

Quy trình chạy dev đã có trong README: cấu hình `.env` theo `.env.example`, rồi tại repository chạy:

~~~powershell
Set-Location 'D:/Repository/PickleVerse'
docker compose up -d --build
~~~

Sau khi đổi `appsettings.json`, rebuild image API rồi bắt đầu phiên Unity/lượt tìm trận mới để tải lại catalog:

~~~powershell
docker compose up -d --build --no-deps backend-api
~~~

Khi đổi SMTP trong `.env`, cần tạo lại container để nạp môi trường mới; `docker compose restart` không nạp lại `.env`. Compose truyền `SMTP_USERNAME`, `SMTP_APP_PASSWORD`, `SMTP_FROM_EMAIL`; chúng ghi đè cấu hình SMTP tương ứng. Lỗi Gmail 535 từng do App Password cũ trong `.env` ghi đè giá trị khác trong JSON, không phải lỗi UI OTP.

Lệnh và URL trên là hướng dẫn có sẵn, **chưa được chạy lại trong lượt viết bàn giao**.

### Database hiện tại

- `src/Database.sql` hiện có **39 lệnh CREATE TABLE**; kiểm tra ngày 31/10 sau gộp bảng xác nhận fresh DB và DB dev **39 bảng/263 cột/54 FK**.
- Seed đã ghi nhận: 10 achievement, 17 mission, 3 cosmetic, 12 lesson/12 drill, 10 tài khoản bot; không seed admin đăng nhập sẵn. Dữ liệu seed không phải toàn bộ dữ liệu người chơi đang có.
- Sáu bảng Hybrid bổ sung: `ratingledger`, `aitelemetrysessions`, `aitelemetryparts`, `aianalysisjobs`, `aicoachingsessions`, `aicoachingprofiles`.
- Không còn `AppActivitySessions`, `GameSettings` hoặc `RankedSearches` trong schema hiện tại; trạng thái phiên hoạt động/ghép trận nằm Redis.
- SQL khởi tạo chạy khi volume PostgreSQL **trống lần đầu**, không tự chạy lại khi volume đã có dữ liệu. Đổi schema không tự áp dụng chỉ bằng restart API.
- Volume PostgreSQL mặc định `pickleverse_dev_postgres_data`, đổi qua `POSTGRES_VOLUME`; S3 `pickleverse_dev_s3_data`. Giữ dữ liệu đang có nếu nhiệm vụ không yêu cầu reset.
- Khi cần DB dev mới, README hướng dẫn dùng tên volume mới. Không đưa thao tác xóa volume hoặc DROP DATABASE vào bước setup mặc định.
- SQL được viết nhiều tên theo PascalCase không quote, nên PostgreSQL lưu identifier chữ thường. Không thêm quote tùy tiện khiến code raw SQL/EF không tìm đúng bảng.
- Dữ liệu danh mục tiếng Việt từng bị biến thành `?` do pipe PowerShell; đã có `scripts/Repair-CatalogText.ps1` xem trước, `-Apply` mới sửa và sao lưu. Dùng init mount/`psql -f` hoặc copy file UTF-8, tránh `Get-Content ... | docker exec -i ... psql`.

## 8. Hybrid AI và Elo: những điều Codex mới cần biết

- Local adaptive practice và bot online dùng chung graph qua adapter; profile bot chụp ở đầu trận. Không chuyển quyết định bot theo từng tick sang REST.
- Elo mới: **K=32**, thang 400, làm tròn midpoint away from zero, sàn 100. Backend chụp policy khi tạo trận; trận cũ chưa có version tiếp tục `legacy-v1`. Client không tự chọn delta/rating đối thủ. Ledger và progression ghi trong cùng transaction.
- Telemetry upload sau phiên backend đã xác nhận; tối đa **1.000 records hoặc 1 MiB/phần** kể cả sau giải nén. Batch ID/part IDs ổn định, retry giữ payload, conflict dữ liệu trả 409; chỉ xóa spool sau đủ ACK.
- Online: authority thu dữ liệu, gửi qua Fusion cho đúng tài khoản sau rally; upload HTTP ngoài bước simulation. Telemetry không có quyền cấp XP, tiền hoặc Elo.
- Worker có hai lane, HTTP timeout 15 giây, lease 60 giây, tối đa 10 lần thử có backoff. Raw parts/frozen requests giữ 30 ngày; aggregate/coaching profile có chính sách riêng.
- Findings gồm timing early/late/late serve, kitchen/momentum, net/out, vùng đích/hồi vị khi có quan sát. Thiếu metric không được tự suy ra lỗi; timing được hỗ trợ hoặc aim do engine sinh không phải bằng chứng kỹ thuật người chơi độc lập.
- `DinkTooHigh` chưa được bật đầy đủ nếu chưa có ngưỡng hiệu chỉnh và fixture phù hợp.
- Recommendation xét tối đa 10 phiên trong 30 ngày, tỷ lệ lỗi trên số cơ hội, severity/recency; ít nhất 5 cơ hội, kiểm tra prerequisite, tối đa 3 drill. Thiếu dữ liệu dùng curriculum fallback.
- Classifier dùng scikit-learn; chia train/test theo người chơi. Gate: holdout tối thiểu 10 người/tier, macro-F1 ≥0,70 và hơn majority baseline ≥0,05. Inference cần ≥20 cơ hội, confidence ≥0,65, context phù hợp.
- **Không có production model hoặc expert dataset trong repo.** Fixture tổng hợp chỉ kiểm tra pipeline. Tier dùng gợi ý practice/drill, không ghi đè competitive Elo.
- Bật theo giai đoạn: secret riêng `AI_SERVICE_KEY` tối thiểu 32 ký tự → ingest/dispatcher shadow → nghiệm thu → coaching → model riêng khi đạt gate. Không bật toàn bộ chỉ vì test unit pass.

Hướng dẫn đầy đủ và hợp đồng nằm ở [Hybrid AI](D:/Repository/PickleVerse/Doc/Hybrid_AI_Implementation.md), [README FastAPI](D:/Repository/PickleVerse/src/AiService/README.md) và [session-v1.json](D:/Repository/PickleVerse/contracts/hybrid-ai/session-v1.json).

## 9. Bằng chứng kiểm tra đã có và giới hạn

### Đợt audit gần nhất ngày 27/10/2026

| Kiểm tra đã chạy trong chat trước | Kết quả ghi nhận |
| --- | --- |
| Unity EditMode | **463/463 đạt**, 0 failed/skipped |
| Python AI | **11/11 đạt**, fixture tổng hợp |
| Build backend/API/IntegrationChecks | Thành công; còn cảnh báo feed/nullable theo đợt |
| Build TypeScript/admin production | Thành công; warning bundle >500 kB |
| Auth backend | 124 assertions đạt, email adapter nội bộ |
| Online game | 76 assertions đạt |
| Online multiplayer/social | 31 assertions đạt |
| Activity/ranked | 22 assertions đạt |
| Redis | 28 assertions đạt |
| Room invitations | 20 assertions đạt |
| Hybrid backend | 32 assertions đạt |
| Hybrid HTTP/load | 6 assertions đạt |
| Configuration | 4 assertions đạt rồi dừng vì kỳ vọng schema 37/208 trong khi thực tế 43/247 |

Không cộng các suite thành số yêu cầu độc lập hoặc độ bao phủ; có ca trùng. `IntegrationChecks` là test harness ngoài runtime, nên giữ để phát hiện hồi quy khi phát triển.

Benchmark mới nhất: steady 10 phiên ×1.000 records: ingest p95 **59,00 ms**, coaching ready p95 **1.014,68 ms**. Burst 100 phiên ×200 records: ingest p95 **417,80 ms**, backlog hết **3,30 giây**; tổng **112 ACK/112 completed** gồm warm-up. Đây là loopback local, không gồm Internet/UI và không chứng minh yêu cầu phản hồi ≤1 giây sau từng point/drill. Số 854,73 ms ở báo cáo Hybrid là lần đo trước, không phải cùng lần audit.

### Bằng chứng gameplay có từ các đợt trước

- Editor + Windows vào phòng Photon thật, ready, tải sân, di chuyển và ghi replay.
- Có smoke Doubles bằng **1 Editor + 3 Windows client trên cùng máy**. Không gọi đó là bốn thiết bị/mạng độc lập đã nghiệm thu.
- Có reconnect và đọc replay S3 sau nối lại; có thử public/custom và bot dự phòng.
- Invite accept/decline đã kiểm tra bằng hai tài khoản/hai runner Fusion trong cùng Editor, gồm click qua EventSystem.
- WatchReplay đã sửa raycast; kiểm tra replay hoàn tất/chưa hoàn tất, pause/seek/2×/trở về history bằng dữ liệu thật.
- Font/Console/fusion lifecycle đã có các đợt fix với báo cáo riêng. Warning IDE Visual Studio/UDP hoặc công cụ Pipeline có thể thuộc môi trường, không tự coi là lỗi gameplay chưa sửa.

Bằng chứng chi tiết gần nhất ở **`artifacts/verification/progress-audit-20261027/`**: `backend-results.json`, `unity-tests.json`, `python-tests.xml`, `admin-observations.json`, `runtime-probes.json`, `database-readonly.json`, `hybrid-http-benchmark.json`, `live-hybrid-flags.json`, `historical-smoke-inventory.json`.

Các smoke Photon cũ ở `artifacts/verification/photon-*.json`; invite và click replay ở `UnityWork/RoomInvitations/`, `UnityWork/ReplayClick/`. **artifacts, UnityWork và Tools bị Git ignore**: có trên máy này không bảo đảm Codex trên máy khác pull về sẽ có. Tài liệu và source vẫn là điểm vào; khi chuyển máy cần chuyển log/build cần dùng hoặc chạy lại kiểm tra đúng phạm vi.

### Lệnh tham khảo để tiếp tục kiểm chứng

Chạy tại root repository khi task tiếp theo cần test, không phải xác nhận rằng các lệnh đã được chạy trong lượt bàn giao:

~~~powershell
Set-Location 'D:/Repository/PickleVerse'
dotnet build src/Backend/PickleVerse.slnx

# Dùng Python 3.12 và venv phát triển có requirements-dev.txt.
# Máy hiện tại có artifacts/hybrid-ai-venv; venv này không được Git chuyển máy.
./artifacts/hybrid-ai-venv/Scripts/python.exe -m pytest src/AiService/tests -q

Push-Location src/Frontend
npm ci
npm run type-check
npm run build
Pop-Location
~~~

Backend IntegrationChecks cần `PKV_TEST_POSTGRES`, `PKV_TEST_REDIS`; bài HTTP cần `PKV_TEST_AI_URL` và FastAPI test có key khớp fixture. Chỉ dùng PostgreSQL/Redis thử riêng và database tạm, không dùng production. Các flag sẵn có: `--check-online-game`, `--check-online-multiplayer`, `--check-online-social`, `--check-app-ranked`, `--check-redis`, `--check-room-invitations`, `--check-hybrid`, `--check-hybrid-http`, `--check-configuration`; không truyền flag chạy bộ auth mặc định. Ví dụ sau khi đã cấu hình môi trường test:

~~~powershell
dotnet run --project src/Backend/IntegrationChecks -- --check-hybrid
~~~

Đối với Unity, đọc skill unity-cli hiện có trước khi gọi công cụ. Helper `HybridAiEditorVerification.RunDirect("HybridAiTests,BotContactChanceTests,AdaptiveBotGraphTests,NetworkCoachingTests")` đã được dùng để kiểm tra assertion trực tiếp và giữ scene; nó chỉ bao phủ nhóm được chọn, không thay toàn bộ 463 tests. Muốn chạy Test Runner chuẩn cần xử lý scene chưa lưu theo quyết định của người dùng.

## 10. Việc còn lại và điểm bắt đầu hợp lý

### App Unity/gameplay/AI

| Việc | Trạng thái hiện biết | Điều kiện tiếp tục/đóng việc |
| --- | --- | --- |
| Nghiệm thu online trên bản hiện tại | Có code và smoke lịch sử, chưa đủ UAT đa thiết bị | Chạy 1v1/2v2/custom, party/ready/cancel, nhiều rally, reconnect/app kill/authority rời; kiểm kết quả/thưởng/Elo/replay |
| Hybrid coaching trong UI | Code có, cờ đang tắt; chưa đủ manual UAT | Kiểm thu dữ liệu đúng người/phiên, upload/retry/spool, UI feedback/recommendation rồi quyết định bật |
| Model/tier/onboarding | Có pipeline/gate; chưa có expert labels/model production và onboarding tự đánh giá đầy đủ | Thu dữ liệu/nhãn độc lập, đạt gate và hoàn thiện luồng onboarding; không dùng fixture làm model thật |
| DinkTooHigh | Có rule gate, chưa hiệu chỉnh đầy đủ | Fixture/threshold được đánh giá và UI feedback đúng |
| Offline training/progression | Luồng hiện tại chủ ý online; khác cam kết proposal | Chốt phạm vi với người dùng trước khi thêm offline/reconciliation hoặc sửa yêu cầu đề tài |
| Multiplayer authority | Fusion Shared theo triển khai đã chọn, khác dedicated trong proposal | Thống nhất phạm vi/độ tin cậy hoặc thiết kế dedicated riêng; không tự chuyển transport |
| Training content | 12 lesson/12 drill, proposal dự kiến khoảng 15 | Nghiệm thu từng drill và chốt danh mục cuối, không coi số mục tự chứng minh chất lượng |
| Report người chơi | Backend có endpoint; UI game chưa được nghiệm thu | Nối luồng gửi báo cáo và kiểm quyền/dữ liệu từ UI tới API |
| Hiệu năng/nền tảng | Chưa đủ bằng chứng 60 FPS điện thoại, RTT <100 ms, 2.000 người/500 trận | Profile build thật, Android ARM64/iOS, mạng đại diện và load theo workload đã chốt |
| iOS 14 | Target hiện 15.0, khác proposal 14+ | Quyết định nâng phạm vi hỗ trợ hoặc cập nhật yêu cầu có xác nhận |

Thứ tự đề xuất nếu chỉ làm app: **nghiệm thu online/reconnect → coaching + dữ liệu AI → phạm vi offline/iOS → hiệu năng thiết bị**. Không khẳng định đang có collision/xuyên bóng chỉ vì chưa nghiệm thu; cần tái hiện mới ghi lỗi.

### Lỗi và thiếu hụt backend/admin đã được audit xác nhận

1. **Admin login sai contract**: frontend gửi `emailOrUsername/passwordHash`; backend cần `email/password`. Frontend đọc `token/role/userId` ở top-level trong khi response có `success/accessToken/refreshToken/profile`. Đã tái hiện HTTP 400; cần sửa cả request và response, tắt mock để test.
2. **Ban/unban/manual verify**: service dùng `Active/Banned` khác enum chữ hoa trong SQL, gây `23514:users_status_check`. Manual verify còn chưa cập nhật cờ verified của dữ liệu xác thực email; chỉ đổi status chưa làm login thành công.
3. **CMS tạo lesson**: gán Reward dạng `50 EXP` vào JSONB gây PostgreSQL `22P02`. Sau khi sửa insert còn phải thống nhất code/config/catalog để Unity đọc và chơi được; edit/publish/unpublish chưa hoàn chỉnh.
4. **Analytics**: `ActiveUsersToday` lấy tổng users; `TotalMatchesPlayed` đếm danh sách recent tối đa 1.000 thay vì tổng thực. Cần fixture độc lập, không chỉ thay nhãn UI.
5. **ConfigurationChecks.cs còn schema 37 bảng/208 cột**: cần cập nhật kiểm tra theo schema hiện tại và chạy tiếp những ca đang bị chặn phía sau; không tắt test để né lỗi.
6. Admin còn thiếu hoặc một phần ở reset toàn bộ progression, ban có hạn/lý do, live monitoring, appeals, retention/lesson analytics, matchmaking health và một số CMS/runtime contracts.
7. Chưa có bằng chứng đủ cho CI toàn stack, staging/TLS/backup-restore/giám sát production, UAT và traceability hoàn chỉnh.

Các lỗi này **chưa được sửa trong lượt bàn giao**, không nhầm việc audit tái hiện thành việc đã khắc phục. Danh sách đầy đủ và nguồn từng dòng nằm trong checklist 183 mục.

## 11. Những kết luận cũ không nên dùng làm hiện trạng

| Kết luận trong chat/tài liệu cũ | Hiện trạng cần dùng |
| --- | --- |
| Chỉ hardcode/local, profile JSON và bạn bè demo | Luồng chính đã online; local còn adapter/test/telemetry spool theo chức năng |
| UGS/Relay/NGO, protocol 3/6/7 | Photon Fusion Shared; README hiện ghi protocol 14, kiểm tra source khi build |
| Timer 360/120 hoặc Unity hiện 300 | appsettings hiện 180/10, runtime cần rebuild/reload catalog |
| API URL đã chuyển domain nên mọi checkout dùng domain | Asset trong checkout hiện là localhost:8080 |
| Chưa có FastAPI, bot local và online chưa chung | Là audit trước Hybrid; nay đã có FastAPI, pipeline và adapters chung |
| Elo tùy biến/streak +60/−60 là policy hiện tại | Trận mới `elo-v2`, K32; chỉ trận cũ giữ legacy theo version |
| Invite chỉ sao chép mã phòng | Đã có danh sách bạn và accept/decline |
| WatchReplay click mở Detail | Đã sửa raycast và kiểm tra trực tiếp ngày 25/10 |
| Cần chạy migration Hybrid | Migration đã bỏ theo yêu cầu; fresh DB dùng Database.sql |
| Schema 71, 43, 41, 40 hoặc 37 bảng | Source hiện 39 bảng; kiểm tra 31/10 sau gộp bảng: 39/263/54 FK |
| Review_2 là hồ sơ đã ký/approved | Đây là draft ở snapshot 23/10, nhiều nội dung/table count cũ; có các gate formal còn mở |
| Có build Windows/APK tức đã đạt thiết bị/NFR | Chưa có đủ đo đạc và nghiệm thu hiện tại trên thiết bị thật |

`Doc/Review_2/` có SRS/SDD/TestPlan, diagram, traceability và 184 test case **dự kiến** theo snapshot cũ `c32f2a4`. Không biến trạng thái NOT RUN thành PASS chỉ vì có test harness khác, không coi draft/BM chưa ký là tài liệu được phê duyệt.

## 12. Chỉ mục các chat đã đọc

Giữ nguyên tiêu đề chat để tra lại bằng công cụ `read_thread`. Mỗi dòng là một chat; đã đọc cả các trang cũ được trả về. Không đưa secret từ chat vào bản bàn giao này.

| Chat | Thread ID | Số lượt đọc |
| --- | --- | ---: |
| Đề xuất tiêu chí đánh giá tiến độ | `01a17c3a-9eb4-7482-8062-de33deed70a6` | 3 |
| Draw runtime architecture | `01a17c13-3c25-7be2-b11b-7bbcc9add28f` | 1 |
| Cài đặt Codex toàn cục | `01a17c0f-276e-7003-b2e4-c9d5cce78af4` | 2 |
| Đánh giá kiến trúc AI PickleVerse | `01a178b4-a6e2-7fa1-ba0e-bcb78c91d661` | 8 |
| Clarify SMTP email credentials | `01a1699d-4401-7e73-9de9-8bb4d53b15f4` | 12 |
| Lập câu hỏi kỹ thuật cho PickleVerse | `01a16e5a-9f7e-71d1-bf27-7f7fdc682938` | 6 |
| Fix matchmaking timer config | `01a16c84-3082-7ad0-b5b1-12f1ec45338e` | 1 |
| Tích hợp API backend vào Unity | `01a1580c-2d37-7ee0-b8f6-76dd833aafae` | 44 |
| Kiểm tra tiến độ Unity hardcode | `01a12463-3fb1-7b12-8d50-fcd2ea6dcb74` | 5 |
| Kiểm tra lỗi console Unity | `01a110cb-9b39-7253-a180-544002a1f554` | 43 |
| Thiết kế hệ thống tiến trình Unity | `01a12a3d-3a30-79a1-ab54-7c017edd0ddb` | 12 |
| Plan Unity features and behavior | `01a13449-c80b-7c93-b536-f946688039a3` | 2 |
| Thiết kế icon UI PickleBall | `01a0c337-d81e-7633-8035-78d26ef63b0b` | 2 |
| Continue task | `01a0c296-66cd-7320-81fa-acb70471e12a` | 1 |

Chat “Continue task” xác định tracking/webcam thuộc workspace khác `D:/Repository/My project`, không phải phần đã hoàn tất trong PickleVerse. Không ghép nhầm tiến độ đó vào game này. Chat “Cài đặt Codex toàn cục” có kết quả cài skill Archify; không đủ kết luận lệnh cài Codex global đã thành công.

Danh sách chat đang thấy không phải cam kết đã truy xuất toàn bộ lịch sử tài khoản ChatGPT/Codex. Công cụ liệt kê archive Codex tại thời điểm này trả rỗng; các chat không hiện trong danh sách hoặc trên host khác chưa được xác minh. Source/tài liệu hiện tại được dùng để bổ sung và kiểm tra chéo, không tự tạo nội dung chat bị thiếu.

## 13. Cách bắt đầu một phiên Codex mới

Có thể đưa cho Codex tiếp theo lời nhắn sau, kèm yêu cầu cụ thể bạn muốn làm tiếp:

> Đọc D:/Repository/PickleVerse/Remember/PickleVerse_Codex_Handover.md, README.md, Doc/Hybrid_AI_Implementation.md và checklist Doc/Proposal/PickleVerse_System_Checklist_20261027.md trước. Kiểm tra branch/HEAD/git status cùng các instruction hiện tại; đối chiếu source vì tài liệu là snapshot ngày 27/10/2026. Game đã chuyển online, dùng Photon Fusion Shared + backend .NET/PostgreSQL/Redis/S3; Hybrid AI đã viết nhưng cờ mặc định tắt và chưa có model production. Kế thừa phần đã làm, phân biệt bằng chứng cũ với kiểm tra mới, giữ dữ liệu/thay đổi của người dùng. Sau đó thực hiện yêu cầu mới của tôi.

Khi hoàn thành việc mới, cập nhật file này với ngày/commit, thay đổi, kiểm tra thật đã chạy, giới hạn và việc còn lại. Không đánh dấu một việc hoàn thành chỉ vì có kế hoạch hoặc đề xuất trong chat.


### Đồng bộ database dev ngày 31/10/2026

Đã drop/recreate đúng database pickleverse trong container pickleverse_postgres từ src/Database.sql, rebuild/recreate backend-api để dùng schema mới. Kiểm tra: 43 bảng, GameMatches không còn OwnerId, 10 Users/PlayerProfiles bot đã seed, 12 lessons/12 drills, 0 trận sau reset. Các migration vừa tạo và bộ test migration đã được bỏ theo yêu cầu. Các sửa source về người chơi/bot qua GameMatchPlayers vẫn được giữ.
