# Tiến trình từ các bảng chuyên biệt — 31/10/2026

Cập nhật sau cùng: GameMatchPlayers bỏ 9 cột trùng, schema 38 bảng/267 cột/53 FK. Points lấy từ MatchScore, ReceivedElo từ ratingledger và hồ sơ đọc trực tiếp. Xem [Rà soát GameMatchPlayers](GameMatchPlayers_Normalization_20261031.md); các số 276 cột phía dưới là lịch sử.

Các đợt tiếp theo cùng ngày đã thêm MatchScore, gộp bảng trận/lịch sử, sửa FK PlayerLessonDrills bằng Npgsql EF Core, chuyển AppActivitySessions sang Redis và bỏ JSON Data/Report của trận. Schema hiện tại là 38 bảng/276 cột/53 FK. Các số 40 bảng bên dưới ghi riêng đợt bỏ GameProgress. Xem [Relational Match Columns](Relational_Match_Columns_20261031.md) và [Redis App Activity](Redis_App_Activity_20261031.md).

Theo yêu cầu, đã bỏ hoàn toàn bảng `GameProgress` và việc serialize cả trạng thái người chơi để lưu vào database. Không chuyển bản JSON đó sang một cột khác và không thêm bảng mới. API vẫn trả một đối tượng tiến trình để Unity sử dụng; backend lắp đối tượng này từ dữ liệu trong các bảng nghiệp vụ.

## Nguồn dữ liệu

| Dữ liệu | Nguồn |
|---|---|
| Hồ sơ, level/EXP, Elo, số trận thắng/thua, bộ đếm tích lũy, chuỗi rank, tutorial và revision | `PlayerProfiles` với các cột cụ thể; ngày tạo lấy từ `Users` |
| Số dư | `PlayerCurrencies` |
| Vật phẩm và trang bị | `PlayerCosmetics`, `Cosmetics` |
| Trạng thái thành tựu đã nhận | `PlayerAchievements`; tiến độ được cập nhật từ bộ đếm tương ứng |
| Nhiệm vụ ngày/tuần, tiến độ và đã nhận thưởng | `PlayerMissions`, `Missions` |
| Thưởng hoàn thành cả kỳ | Biên nhận `period-bonus` trong `GameCommands`, dùng cùng transaction với cập nhật số dư/EXP |
| Tổng hợp bài tập, sao, độ chính xác tốt nhất, số lần tập/lỗi | `PlayerLessonDrills` |
| Lượt tập hiện tại và checkpoint | `TrainingSessions` với `IsCurrent`; chỉ một lượt hiện tại cho mỗi người |
| Kết quả luyện tập gần nhất phục vụ biểu đồ có sẵn | Đọc tối đa 8 lượt hoàn tất từ `TrainingSessions`; không lưu thêm bản lịch sử trong JSON tiến trình |
| Trận đang chơi | `GameMatches`, `GameMatchPlayers` |
| Lịch sử và nhận diện trận đã xử lý | `GameMatches` đã hoàn tất và `GameMatchPlayers` đã FinalizedAt; điểm lấy từ `MatchScore` |
| Chuỗi ngày hoạt động | Tính từ ngày hoàn tất các trận hợp lệ và mốc bắt đầu theo dõi trong `PlayerProfiles` |
| AI coaching/lộ trình mở khóa | Đọc cùng dữ liệu luyện tập từ các bảng chuyên biệt |

## Các thay đổi cần thiết

- `PlayerProfiles` bổ sung cột tutorial, revision và các bộ đếm cụ thể còn thiếu. Không có cột JSON chứa toàn bộ tiến trình. Tổng XP được suy ra từ level/EXP theo quy tắc online hiện tại.
- Khóa chính `PlayerMissions` gồm `(PlayerId, MissionId, PeriodKey)`, giữ riêng từng kỳ. `CurrentStreak` duy trì chuỗi thắng của nhiệm vụ theo kỳ; trạng thái thưởng tổng được đọc từ biên nhận để tránh trả lại thưởng sau reload.
- `PlayerLessonDrills` bổ sung số lần tập/tập lại/hoàn thành, tổng lượt đánh/thành công/thời gian, độ chính xác tốt nhất và danh sách tổng hợp lỗi của bài. JSON lỗi chỉ chứa các loại lỗi và số lượng, không chứa bản tiến trình người chơi.
- `TrainingSessions.IsCurrent` xác định lượt hiện tại, với unique partial index theo người chơi. `Sequence` cho thứ tự các lượt, kể cả khi thời gian bằng nhau.
- `MatchHistory.Sequence` bảo đảm thứ tự lịch sử khi nhiều trận có cùng timestamp.
- `GameCommands` tiếp tục chống xử lý lặp. Không dọn biên nhận thưởng kỳ như thể đó chỉ là cache.

Code chính: `OnlineGameService.State.cs`, `RelationalTraining.cs`, `OnlineGameService.cs`, `OnlineGameService.Training.cs` và `HybridCoachingService.cs`. Giữ transaction và khóa profile để đồng bộ số dư, thưởng, nhiệm vụ và kết quả. Khi chỉ tải dữ liệu, không ghi lại các dòng nhiệm vụ/bài tập không đổi.

## Kiểm tra và giới hạn

Bộ kiểm tra riêng bao phủ reload qua context mới, tutorial/revision, checkpoint, tập lại kém hơn, nguồn dữ liệu SQL, coaching, chống thưởng trùng, trận dang dở/lịch sử, chuỗi thắng và đổi kỳ ngày/tuần. Các bộ game, rank, Hybrid, replay, configuration, invitations và Redis được chạy lại. Kết quả cụ thể nằm trong `artifacts/relational-progress-checks-20261031/summary.json`.

Conceptual Diagram cùng đường dẫn đã đồng bộ theo SQL: 40 entity, 56 FK, không vẽ attribute. Đây là thay đổi lưu trữ/backend; không tuyên bố đã chạy lại gameplay nhiều Unity client hoặc đo hiệu năng thiết bị trong lượt này.

Môi trường development được drop/recreate từ `src/Database.sql` theo quyền người dùng đã cấp; không tạo migration. Dữ liệu và các bảng khác thuộc các dự án khác không nằm trong phạm vi reset.
