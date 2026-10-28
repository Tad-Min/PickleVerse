# PickleVerse — Bàn giao cho Codex tiếp theo

Ngày lập: **27/10/2026, múi giờ Asia/Saigon**. Thư mục dự án: **D:/Repository/PickleVerse**.

Tài liệu này tổng hợp **142 lượt trao đổi trong 14 chat liên quan**, đọc hết các trang lịch sử mà công cụ cung cấp, rồi đối chiếu với source và báo cáo kiểm tra trong repository. Đây là bản ghi công việc và quyết định để tiếp tục dự án, không phải bản nghiệm thu hoàn thành sản phẩm.

Trong lượt lập tài liệu này, Codex chỉ đọc lịch sử, source, cấu hình công khai và kết quả kiểm tra có sẵn; **không chạy lại game/test, không thay đổi database, container hoặc mã ứng dụng**. Các kết quả chạy bên dưới được ghi rõ là kết quả từ các đợt trước. Không sao chép mật khẩu, token, OTP hoặc nội dung `.env` vào tài liệu.

## 1. Đọc nhanh: dự án đang ở đâu?

- PickleVerse đã qua giai đoạn prototype hardcode/local. Luồng chính hiện dùng **đăng nhập và dữ liệu backend** cho hồ sơ, tiến trình, training, shop, lịch sử, social và replay.
- Game là **Unity 6000.6.2f1**, URP, uGUI/TextMeshPro. Có BotMatch, Tutorial, Training, online 1v1/2v2, Custom, Profile, nhiệm vụ/thành tựu, Shop/Collection và Replay 3D.
- Multiplayer hiện dùng **Photon Fusion Shared**. Backend .NET quản lý tài khoản, matchmaking/ready, kết quả và Elo; một client giữ authority mô phỏng trận. Chưa có dedicated game server như mục tiêu trong proposal.
- Backend **ASP.NET Core/.NET 10**, PostgreSQL 16, Redis 7.4, S3; website admin React/TypeScript/Vite. Docker Compose là cấu hình phát triển.
- **Hybrid AI đã được viết**: bot chạy cục bộ, telemetry gửi sau phiên, worker .NET gọi FastAPI, lưu feedback/recommendation; pipeline classifier đã có nhưng **chưa có model production đã được đánh giá bằng nhãn chuyên gia**.
- Bốn cờ AI trong source mặc định `false`; đợt audit gần nhất cũng ghi nhận container có cả bốn cờ tắt. Không hiểu việc có source FastAPI là người chơi đã dùng coaching đầy đủ.
- Checklist ngày 27/10 có **183 thành phần: 83 đạt kiểm tra, 53 một phần, 16 chưa có/chưa bật, 20 chưa kiểm chứng, 11 lỗi**. Đây là số mục, **không phải phần trăm hoàn thành**.
- Lỗi đã biết tập trung ở admin login, trạng thái tài khoản, CMS/JSONB, analytics và một test còn kỳ vọng schema cũ. Phần app cần nghiệm thu gameplay/mạng/thiết bị và coaching tiếp.

### Mốc Git lúc bắt đầu bàn giao

| Mục | Giá trị đã đọc |
|---|---|
| Branch | `realtime-AI-response` |
| HEAD | `d3aa3ac` — `AI python + Unity Behavor` |
| Commit trước đó | `0877699` — `Merge branch 'Min'` |
| Working tree trước khi tạo file này | Chỉ có `?? Archify/source-snapshot/` |
| Thư mục Remember | Chưa có file bàn giao trước lượt này |

`Archify/source-snapshot/` là repository bằng chứng độc lập do chat vẽ kiến trúc tạo, có Git riêng. Nó không phải source ứng dụng đang chạy hoặc submodule đã đăng ký. Không vô tình stage cả thư mục bằng `git add -A`, không xóa chỉ vì thấy untracked. Đọc `Archify/source-notes.md` trước khi xử lý.

Kiểm tra lại cuối lượt bàn giao thấy thêm **`.gitignore` bị sửa** và **`src/NOREADDATABSE.sql` bị xóa**; đây là các thay đổi ngoài file bàn giao mà Codex đang tạo. Codex không sửa hoặc khôi phục hai file này. `Archify/source-snapshot/` không còn được liệt kê untracked ở lần kiểm tra sau, nhưng Git riêng của nó vẫn tồn tại. Codex tiếp theo phải đọc lại `git status` hiện tại và giữ những thay đổi ngoài phạm vi nhiệm vụ.

## 2. Những tài liệu nên đọc trước

| Thứ tự | Tài liệu | Mục đích |
|---|---|---|
| 1 | [README](D:/Repository/PickleVerse/README.md) | Khởi tạo database, Docker, API asset, Redis và SMTP |
| 2 | [Checklist hệ thống 27/10](D:/Repository/PickleVerse/Doc/Proposal/PickleVerse_System_Checklist_20261027.md) | Trạng thái chi tiết, lỗi, bằng chứng và việc còn thiếu mới nhất |
| 3 | [Hybrid AI Implementation](D:/Repository/PickleVerse/Doc/Hybrid_AI_Implementation.md) | Thiết kế đã triển khai, cờ bật/tắt, hợp đồng, test và giới hạn |
| 4 | [Tài liệu tổng hợp](D:/Repository/PickleVerse/Doc/PickleVerse_Project_Documentation.md) | Lịch sử triển khai và vận hành theo chủ đề; có nội dung cũ cần đọc đúng thời điểm |
| 5 | [Core Features](D:/Repository/PickleVerse/Doc/Proposal/PickleVerse_Core_Features.md), [Capstone Proposal](D:/Repository/PickleVerse/Doc/Proposal/PickleVerse_Capstone_Proposal.md) | Yêu cầu gốc, không tự coi là tính năng đã hoàn thành |
| 6 | [Tiêu chí đánh giá tiến độ](D:/Repository/PickleVerse/Doc/Proposal/PickleVerse_Progress_Evaluation_Criteria.md) | Trọng số, mức độ hoàn thành và bằng chứng nghiệm thu |
| 7 | [Sơ đồ runtime](D:/Repository/PickleVerse/Archify/pickleverse-runtime.html), [ghi chú nguồn](D:/Repository/PickleVerse/Archify/source-notes.md) | Ranh giới Unity/Photon/API/Redis/PostgreSQL/S3/AI/admin |

Tài liệu tổng hợp từng gộp 48 Markdown ngày 22/10. Sau đó nhiều báo cáo được thêm hoặc xuất hiện trở lại ở `Doc/Proccess/`, `Doc/Proposal/`, `Doc/Review_2/`. Không dựa vào câu lịch sử “Doc chỉ còn 5 file” để xóa các file hiện tại. Tên thư mục **Proccess** và **Entites** đang được dùng thật; tránh tự đổi chính tả khi chưa kiểm tra references.

## 3. Kiến trúc hiện tại và những ranh giới cần giữ

| Thành phần | Trách nhiệm hiện tại |
|---|---|
| Unity | Input, luật/vật lý, animation, UI, bot/Behavior Tree, replay playback, thu telemetry |
| Photon Fusion Shared | Phiên phòng và đồng bộ gameplay giữa các client; client giữ StateAuthority |
| ASP.NET API | Auth/JWT/OTP, kiểm tra quyền, catalog, tiến trình/thưởng, kết quả trận/Elo, replay, social và ingest AI |
| PostgreSQL | Nguồn dữ liệu bền vững: tài khoản, hồ sơ, tiến trình, trận đấu, Elo ledger, metadata replay, telemetry/job/coaching |
| Redis | Matchmaking, trạng thái tìm/xác nhận, presence, lời mời có TTL, leaderboard Sorted Set/cache |
| S3 | Nội dung object như replay chunks; metadata/quyền truy cập được API quản lý |
| Worker Hybrid trong backend | Lease/retry job PostgreSQL, gọi FastAPI rồi ghi kết quả bằng transaction |
| Python FastAPI | Phân tích telemetry theo phiên, feedback, đề xuất drill, tier nếu model đủ điều kiện; không ghi database/Elo |
| Browser admin | Gọi API quản trị; còn lỗi hợp đồng và nghiệp vụ, có mock mode nên cần kiểm tra với API thật |

Luồng chính: **Unity → API → PostgreSQL/Redis/S3**; đồng bộ trận đi qua **Photon**. Luồng AI: **Unity thu dữ liệu → API nhận sau phiên → PostgreSQL job → worker gọi FastAPI → PostgreSQL lưu kết quả → API trả Unity**.

Fallback SQL được triển khai cho **đọc leaderboard khi Redis lỗi**. Không diễn giải thành mọi chức năng Redis đều có PostgreSQL fallback; matchmaking không có fallback SQL tương đương. Redis AOF không thay vai trò bền vững của PostgreSQL.

## 4. Các quyết định đã chốt trong lịch sử

### 4.1. Dữ liệu người chơi và tài khoản

- Người dùng đã yêu cầu chuyển từ local sang online. `ProgressionRuntime` hiện là view trong bộ nhớ của trạng thái server, cần signed-in và server snapshot; `DeveloperControls` đang `false`.
- Những hướng dẫn cũ về chỉnh Elo/tiền bằng “Chỉnh player”, JSON profile local hoặc danh sách bạn demo là **giai đoạn prototype**. Không bật lại chúng như cách sửa lỗi đồng bộ online.
- Đăng ký gửi `email`, `password`, `confirmPassword`, `isMale`. Backend kiểm tra hai mật khẩu và tự sinh tên dạng `player` + chuỗi ngẫu nhiên; đổi tên thực hiện trong hồ sơ.
- Email cần OTP thật qua Gmail SMTP trước đăng nhập. Đã sửa resend feedback bị UI xóa và trường hợp email đã verified: hướng về đăng nhập thay vì chờ OTP mới.
- `FromName` là tên hiển thị; `FromEmail` là địa chỉ gửi; `Username` là tài khoản SMTP. Chat có trao đổi khả năng gộp, nhưng **không phải bằng chứng code đã gộp**: Compose hiện vẫn truyền ba biến SMTP riêng.
- Không seed tài khoản Admin mặc định. Tài khoản bot không có quyền đăng nhập.

### 4.2. Multiplayer, bot dự phòng và kết thúc trận

- UGS/NGO/Relay là transport lịch sử. Đã chuyển sang Photon; mã cũ còn được giữ có điều kiện `PICKLEVERSE_LEGACY_NGO`, không phải luồng chính.
- Luồng online đã chốt: **tìm trận → người thật xác nhận → tải sân → thi đấu**. Trong trận có bot, bot được hệ thống ready sẵn.
- Bot dự phòng Rank Singles vẫn tham gia **phòng Photon/MultiplayerCourt**, có UUID và tên hợp lệ; không chuyển sang BotMatch PvE khi hết thời gian chờ.
- Đã seed **10 tài khoản bot role Player**, danh sách UUID tương ứng trong Unity. Giữ đồng bộ SQL và `RankedBotAccounts.cs` khi sửa roster.
- Thời gian chờ ban đầu là 6 phút, sau đó người dùng đổi cấu hình. **Source hiện tại là 180 giây chờ bot, 10 giây xác nhận**; không hardcode lại 360/120 từ tài liệu cũ.
- Kết thúc trận hiện bảng tỷ số; khi thoát về ngoài thì hiện tổng kết và Elo do server chốt. Các số +60/−60 trong test cũ không phải công thức Elo hiện tại.
- Tỷ lệ theo Elo là **khả năng đánh dính bóng**, không phải tỷ lệ cố tình đánh ra ngoài. Từ lần sửa mới nhất, bot **không áp dụng xác suất hụt ngẫu nhiên lúc giao bóng**; xác suất hụt dùng cho cú trả bóng. Điều này không loại bỏ kiểm tra contact/luật sân.
- Lời mời bạn bè đã được nâng từ hộp chia sẻ Room Code thành danh sách có tìm kiếm/nút Mời và thông báo Chấp nhận/Từ chối. Cần quan hệ bạn bè và trạng thái lobby hợp lệ; TTL 60 giây, polling khoảng 3 giây, presence lease 20 giây.
- Chưa tạo bảng Rooms vì phòng đang hoạt động do Fusion quản lý, trạng thái lời mời/presence nằm Redis. Đề xuất tách mã join khỏi session để đổi mã trong chat không tự có nghĩa tính năng đổi mã đã hoàn thành.

### 4.3. Tiền tệ, thời gian, replay và dữ liệu nền

- **TOKENPAD** tương ứng coin; **ELITE TOKEN** tương ứng premium/gem. Đã yêu cầu bỏ số dư/thưởng premium miễn phí; chưa có hệ thống nạp tiền thật. Không ghi nhận IAP đã triển khai.
- Thời gian hoạt động tính **từ lúc mở app**, gồm đăng nhập/menu/tìm trận/training/trận đấu. Không chỉ cộng duration trận; `AppActivitySessions` hỗ trợ đồng bộ/chống cộng trùng cùng PostgreSQL.
- Replay đã chuyển từ local sang API/S3 trong luồng chạy chính. Local replay adapter còn phục vụ định dạng cũ/test. Telemetry spool lưu local là hàng đợi gửi AI, không phải profile hoặc offline progression.
- SQL phát triển thống nhất ở **một file `src/Database.sql`**, khởi tạo schema + seed trong một transaction. Người dùng đã yêu cầu bỏ Migrations và dùng database phát triển mới khi đổi schema; không tự tái tạo database đang dùng trong một nhiệm vụ không yêu cầu việc đó.
- `GameSettings` đã bỏ; cấu hình thưởng/EXP chuyển vào `appsettings.json`. `RankedSearches` đã bỏ để chuyển sang Redis. **Giữ `AppActivitySessions`**.
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
|---|---|---|
| UI/icon ban đầu | Thiết kế bộ Home, Progression, Career, Collection, Store, Settings, Coin trên Figma | Đã có kết quả thiết kế; riêng yêu cầu đưa cả bộ vào Unity không có kết luận hoàn tất trong chat đó |
| Movement/animation | Chẩn đoán Rigidbody/Vector2 làm mất trục Z/phím phải; tách và đặt tên 83 clip từ 44 model; tạo controller nam/nữ | Đã có nhiều lần mở rộng và đồng bộ animation về sau; không chỉ dựa vào controller của bản đầu |
| Multiplayer prototype | UI 1v1/2v2/party/custom; tìm/hủy, ready, chia đội, reconnect, Scene Hash/Build Settings, HUD và đồng bộ sân/nhân vật | Bằng chứng UGS/Relay/protocol 3–7 là lịch sử, đã được Photon thay thế |
| Profile/progression local | Profile, XP/level, Elo/rank, history, achievement, nhiệm vụ ngày/tuần, dev player editor, ví tiền | Đã làm bản local và test/build; nguồn dữ liệu runtime nay đã chuyển sang server |
| Replay/streak/Entites/BhTree | Replay 3D snapshot/chunk, pause/seek/speed; daily streak UTC; gom entity/assembly; Unity Behavior 1.0.16 theo Elo | Đã triển khai, sau đó chuyển replay online và hợp nhất bot trong Hybrid |
| Rà soát tính năng/SQL | Đánh giá phần hardcode so với Core Features/proposal, tạo schema PostgreSQL và audit ban đầu | Schema từng là 71 bảng, rồi thay đổi theo backend; hiện phải dùng 43 bảng trong SQL mới |
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
|---|---|
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
| Replay | `My project/Assets/Scripts/Replays/ReplayRecorder.cs`, `OnlineReplayRepository.cs`, `ReplayView.cs`; `src/Backend/Services/Game/OnlineReplayService.cs` |
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
|---|---|
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
|---|---|---|
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

- `src/Database.sql` có **43 lệnh CREATE TABLE**; audit gần nhất xác nhận fresh DB và DB dev **43 bảng/247 cột**.
- Seed đã ghi nhận: 10 achievement, 17 mission, 3 cosmetic, 12 lesson/12 drill, 10 tài khoản bot; không seed admin đăng nhập sẵn. Dữ liệu seed không phải toàn bộ dữ liệu người chơi đang có.
- Sáu bảng Hybrid bổ sung: `ratingledger`, `aitelemetrysessions`, `aitelemetryparts`, `aianalysisjobs`, `aicoachingsessions`, `aicoachingprofiles`.
- Còn `AppActivitySessions`; không còn `GameSettings` hoặc `RankedSearches` trong schema hiện tại.
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
|---|---|
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
|---|---|---|
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
|---|---|
| Chỉ hardcode/local, profile JSON và bạn bè demo | Luồng chính đã online; local còn adapter/test/telemetry spool theo chức năng |
| UGS/Relay/NGO, protocol 3/6/7 | Photon Fusion Shared; README hiện ghi protocol 14, kiểm tra source khi build |
| Timer 360/120 hoặc Unity hiện 300 | appsettings hiện 180/10, runtime cần rebuild/reload catalog |
| API URL đã chuyển domain nên mọi checkout dùng domain | Asset trong checkout hiện là localhost:8080 |
| Chưa có FastAPI, bot local và online chưa chung | Là audit trước Hybrid; nay đã có FastAPI, pipeline và adapters chung |
| Elo tùy biến/streak +60/−60 là policy hiện tại | Trận mới `elo-v2`, K32; chỉ trận cũ giữ legacy theo version |
| Invite chỉ sao chép mã phòng | Đã có danh sách bạn và accept/decline |
| WatchReplay click mở Detail | Đã sửa raycast và kiểm tra trực tiếp ngày 25/10 |
| Cần chạy migration Hybrid | Migration đã bỏ theo yêu cầu; fresh DB dùng Database.sql |
| Schema 71, 39 hoặc 37 bảng | Source hiện 43 bảng; audit 43/247 |
| Review_2 là hồ sơ đã ký/approved | Đây là draft ở snapshot 23/10, nhiều nội dung/table count cũ; có các gate formal còn mở |
| Có build Windows/APK tức đã đạt thiết bị/NFR | Chưa có đủ đo đạc và nghiệm thu hiện tại trên thiết bị thật |

`Doc/Review_2/` có SRS/SDD/TestPlan, diagram, traceability và 184 test case **dự kiến** theo snapshot cũ `c32f2a4`. Không biến trạng thái NOT RUN thành PASS chỉ vì có test harness khác, không coi draft/BM chưa ký là tài liệu được phê duyệt.

## 12. Chỉ mục các chat đã đọc

Giữ nguyên tiêu đề chat để tra lại bằng công cụ `read_thread`. Mỗi dòng là một chat; đã đọc cả các trang cũ được trả về. Không đưa secret từ chat vào bản bàn giao này.

| Chat | Thread ID | Số lượt đọc |
|---|---|---:|
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
