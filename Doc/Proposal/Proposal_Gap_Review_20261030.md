# Đối chiếu mã nguồn hiện tại với Proposal — 30/10/2026

## Cập nhật ngày 31/10/2026

Đợt mới nhất đã bỏ hoàn toàn GameProgress theo yêu cầu: tiến trình đọc/ghi từ các bảng chuyên biệt, không thêm entity. Đã đồng bộ schema development/backend/sơ đồ với 40 bảng/260 cột/56 FK; API readiness 200 và kiểm tra HTTP tiến trình trên backend Docker đạt. Chi tiết tại [Relational Progress](../Relational_Progress_20261031.md). Những số lượng bảng bên dưới thuộc các đợt trước.

Đã hợp nhất replay theo yêu cầu tiếp theo: bỏ OnlineReplays/OnlineReplayChunks, chỉ giữ MatchHistoryReplay trỏ manifest S3 cho một replay chung mỗi trận. Backend/Unity và Conceptual Diagram đã đồng bộ; database development đã tạo lại với 41 bảng/235 cột/57 FK, backend readiness 200. Chi tiết và kiểm tra trong [Replay Storage](../Replay_Storage_20261031.md). Thay đổi này không xác nhận các khoảng cách Proposal khác đã được xử lý.

Đã sửa source/schema để bỏ GameMatches.OwnerId và dùng GameMatchPlayers cho người chơi/bot, gồm cả BotPractice. Người dùng xác nhận đây là môi trường development và cho phép drop/recreate từ src/Database.sql, không cần migration; các migration vừa tạo đã được loại bỏ. Database development pickleverse đã được tạo lại từ Database.sql và backend đã được build/khởi động đồng bộ; xác nhận GameMatches không còn cột OwnerId. Những nhận xét OwnerId bên dưới ghi lại hiện trạng ngày 30/10 trước thay đổi này. Các chênh lệch Proposal khác chưa được coi là đã sửa.

## Phạm vi và cách kết luận

Đối chiếu Proposal DOCX gốc với mã Unity, ASP.NET, React, Python và Database.sql đang có trong workspace. Đây là rà soát tĩnh, không phải nghiệm thu toàn hệ thống: không chạy lại game nhiều máy, API, container, tải, thiết bị hay đo hiệu năng trong lượt này. Cấu hình trong repository có thể bị ghi đè khi triển khai. Các kết luận về môi trường chạy thực tế cần kiểm tra riêng.

Nguồn yêu cầu chính: [Proposal DOCX](PickleVerse_Capstone_Proposal.docx). [Bản Markdown](PickleVerse_Capstone_Proposal.md) giúp tra cứu theo dòng. Checklist ngày 27/10 được đọc để định hướng, nhưng không coi trạng thái chạy hoặc số test cũ là kết quả kiểm tra mới.

Phân loại: **khác yêu cầu** = có hành vi/kiến trúc không khớp cam kết; **lỗi tích hợp** = mã hiện tại có mâu thuẫn cụ thể; **còn thiếu/một phần** = chưa thấy luồng đáp ứng đầy đủ trong phạm vi đã đọc; **chưa kiểm chứng** = cần đo/chạy để kết luận. Một bảng, một FK hay một cột không được nhắc tên trong Proposal không tự động là sai hoặc cần xóa.

## Những chênh lệch chính

### P-01. Multiplayer chưa theo mô hình dedicated server đã đăng ký

- Proposal yêu cầu máy chủ gameplay riêng có quyền quyết định trạng thái trận; client gửi input, server xác thực và tính kết quả. Cho phép dùng Photon Fusion thay SDK, nhưng vẫn ghi mô hình server-authoritative.
- Cả tạo và join ở [PhotonSession.cs](<D:/Repository/PickleVerse/My project/Assets/Scripts/Multiplayer/PhotonSession.cs:163>) dùng `GameMode.Shared`; `IsHost` là `IsSharedModeMasterClient`. Đây không phải phiên chạy ở Dedicated Server Mode.
- Backend có kiểm tra đội hình, kết quả và cập nhật Elo/thưởng, nhưng việc đó không thay thế một máy chủ mô phỏng gameplay độc lập.
- Kết luận: **khác yêu cầu kiến trúc**. Dùng Photon và có phòng tạo/join không phải lỗi; phần cần xử lý là vị trí giữ quyền mô phỏng. Không thể khắc phục bằng cách đổi tên hoặc xóa OwnerId.

### P-02. Luyện tập offline và đồng bộ tiến trình chưa được đáp ứng bởi luồng hiện tại

- Proposal yêu cầu training/drill chơi offline, đồng bộ khi kết nối lại.
- [ProgressionRuntime.cs](<D:/Repository/PickleVerse/My project/Assets/Scripts/Progression/ProgressionRuntime.cs:138>) gửi start/checkpoint/complete qua API có xác thực. [TrainingDirector.cs](<D:/Repository/PickleVerse/My project/Assets/Scripts/LocalMatch/TrainingDirector.cs:190>) đợi máy chủ cấp lượt tập trước khi vào Practice.
- Hàng đợi telemetry gửi lại sau mất mạng trong CoachingRuntime chỉ phục vụ dữ liệu phân tích; không phải cơ chế chơi offline và hợp nhất tiến trình/thưởng.
- Kết luận: **còn thiếu trong luồng training đang dùng**. Cần luồng offline và chính sách đồng bộ/xung đột, hoặc thay đổi phạm vi được chấp thuận.

### P-03. Thiếu bước self-assessment trong onboarding

- Proposal yêu cầu người mới tự đánh giá khi onboarding, kết hợp các phiên đầu để ước lượng tier.
- Đã tìm trong mã game/backend/AI: thấy tài khoản, tutorial và `onboardingShots`, nhưng chưa thấy questionnaire, câu trả lời hoặc luồng tự đánh giá. `onboardingShots` là dữ liệu cú đánh từ các phiên đầu, không phải câu trả lời tự đánh giá.
- Kết luận: **chưa thấy triển khai self-assessment**. Không suy ra rằng phải thêm bảng riêng; dữ liệu có thể lưu trong cấu trúc hiện có nếu thiết kế phù hợp.

### P-04. Classifier tier chưa có model triển khai được chứng minh

- Có pipeline huấn luyện và gate kiểm tra model, nhưng [AiService README](<D:/Repository/PickleVerse/src/AiService/README.md:20>) ghi chưa kèm model production/dataset được gán nhãn chuyên gia; mặc định dùng quy tắc/lộ trình dự phòng. Không thấy artifact model trong đường dẫn models của source đang kiểm tra.
- Cùng tài liệu nêu tier dùng để gợi ý độ khó/drill, không thay Elo thi đấu. Nếu giữ yêu cầu ước lượng hạng khởi đầu cho người mới, cần chốt rõ tier được dùng vào đâu trong onboarding/matchmaking.
- Kết luận: **mới có hạ tầng/pipeline, chưa đủ bằng chứng hoàn thành classifier trong sản phẩm**. Không kết luận toàn bộ AI chưa có: lỗi kỹ thuật và gợi ý drill đã có logic rule-based.

### P-05. Luồng Hybrid AI mặc định đang tắt trong cấu hình repository

- [appsettings.json](<D:/Repository/PickleVerse/src/Backend/API/appsettings.json:2>) để IngestEnabled, DispatcherEnabled, CoachingEnabled, ModelEnabled bằng false.
- [docker-compose.yml](<D:/Repository/PickleVerse/docker-compose.yml:99>) cũng mặc định các cờ này false, nhưng cho phép ghi đè qua biến môi trường.
- Kết luận: **chưa được bật theo cấu hình mặc định**, không phải chứng minh môi trường triển khai hiện tại đang tắt. Cần kiểm tra cờ thực tế và đi hết luồng từ game tới UI trước nghiệm thu.

### P-06. Phản hồi AI sau từng point và thời hạn một giây chưa được bảo đảm

- Proposal có mốc một giây sau drill hoặc point.
- [CoachingRuntime.cs](<D:/Repository/PickleVerse/My project/Assets/Scripts/Progression/CoachingRuntime.cs:142>) chỉ gửi session đã đóng; backend [HybridCoachingService.cs](<D:/Repository/PickleVerse/src/Backend/Services/Game/HybridCoachingService.cs:51>) kiểm tra phiên đã hoàn thành trước khi nhận phân tích. Đây là luồng Hybrid phân tích sau phiên hoàn tất, chưa phải phân tích từng point khi trận đang tiếp tục.
- Không đo lại độ trễ đầu-cuối đến UI trong lượt này. Không sử dụng việc có code bất đồng bộ như bằng chứng đạt dưới một giây.
- Kết luận: **chênh lệch mức phản hồi của luồng Hybrid; chỉ tiêu thời gian chưa kiểm chứng**. Phản hồi tức thời bằng luật trong gameplay là luồng khác, không tự chứng minh tốc độ của dịch vụ Hybrid.
- Riêng DinkTooHigh trong [models.py](<D:/Repository/PickleVerse/src/AiService/app/models.py:63>) cần ngưỡng đã hiệu chỉnh, mặc định None; request backend hiện không truyền ngưỡng này. Không coi nhánh điều kiện đã có là tính năng được bật và hiệu chỉnh hoàn chỉnh.

### P-07. Hợp đồng đăng nhập Admin giữa React và API không khớp

- Web [AuthContext.tsx](<D:/Repository/PickleVerse/src/Frontend/src/context/AuthContext.tsx:49>) gửi emailOrUsername/passwordHash, trong khi [LoginRequest](<D:/Repository/PickleVerse/src/Backend/Services/DTOs/AllDTOs.cs:23>) bắt buộc email/password.
- Web đọc token/role/userId ở cấp ngoài; AuthResponse của backend trả accessToken/refreshToken/profile và không có cấu trúc giống giả định của web. adminApi chuyển trực tiếp request/response, chưa có adapter sửa sai khác này.
- Mock mode có thể cho xem UI nhưng không chứng minh đăng nhập Admin qua API thật hoạt động.
- Kết luận: **lỗi tích hợp ở chức năng Admin bắt buộc**. Cần thống nhất request, response và cách xác định quyền từ thông tin được backend xác thực.

### P-08. Ban/unban không khớp ràng buộc trạng thái SQL

- [AdminService.cs](<D:/Repository/PickleVerse/src/Backend/Services/Implementations/AdminService.cs:80>) dùng `UserStatus.Banned.ToString()` / `Active.ToString()`; enum có tên Banned/Active.
- [Database.sql](<D:/Repository/PickleVerse/src/Database.sql:13>) chỉ cho ACTIVE/BANNED/SUSPENDED; PostgreSQL so sánh chuỗi này phân biệt chữ hoa/thường. Theo schema này, ghi Banned/Active vi phạm CHECK.
- Reason/BannedUntil được nhận nhưng nhánh đang đọc chưa lưu/thi hành đầy đủ chính sách cấm có thời hạn. Lệnh ban từ xử lý report cũng dùng cách gán tương tự.
- Kết luận: **lỗi tích hợp**, khác với việc chỉ thiếu một entity trong sơ đồ. Cần thống nhất giá trị trạng thái và đặc tả moderation.

### P-09. Reset tiến trình mới có reset Elo

- Proposal yêu cầu Admin reset progress.
- [AdminController.cs](<D:/Repository/PickleVerse/src/Backend/API/Controllers/AdminController.cs:57>) có reset-elo; service tương ứng chỉ đổi Elo hồ sơ. Chưa thấy hành động reset tiến trình bài học/XP và các dữ liệu tiến trình liên quan theo một chính sách thống nhất.
- Kết luận: **chức năng chưa đầy đủ**. Cần định nghĩa phạm vi reset và cập nhật nhất quán các nguồn tiến trình, thay vì xem reset Elo là reset toàn bộ progress.

### P-10. Tạo nội dung CMS có lỗi dữ liệu và chưa đi hết vào game

- [CreateLessonAsync](<D:/Repository/PickleVerse/src/Backend/Services/Implementations/AdminService.cs:176>) gán Reward = "50 EXP", trong khi cột được map kiểu JSONB. Đây không phải JSON hợp lệ để ghi vào cột theo định dạng hiện tại.
- Lesson mới cũng chưa được điền Code/Config theo hợp đồng runtime. [GameCatalogService.cs](<D:/Repository/PickleVerse/src/Backend/Services/Game/GameCatalogService.cs:17>) chỉ nạp lesson có code và đọc config thành TrainingDrill.
- Kết luận: **lỗi tích hợp và thiếu kết nối CMS–runtime**; có form tạo bản ghi chưa đủ chứng minh Admin tạo được bài học chơi được.

### P-11. CMS chưa đủ edit/publish/unpublish và quản lý tham số

- Proposal yêu cầu create/edit lesson, drill, difficulty/reference technique data và publish/unpublish.
- AdminController/adminApi hiện thấy các endpoint tạo lesson/drill/cosmetic/mission, chưa thấy luồng sửa/xuất bản/gỡ lesson–drill tương ứng. Các thao tác publish phòng mạng không phải publish nội dung.
- Kết luận: **còn thiếu workflow quản trị nội dung**. Chưa quyết định cần entity mới; nên đặc tả trạng thái và hợp đồng nội dung trước.

### P-12. Giám sát trận live chưa có nguồn dữ liệu phù hợp

- Proposal yêu cầu xem trận đang hoạt động và hồ sơ trận đã kết thúc, xử lý disconnect.
- [AdminController.cs](<D:/Repository/PickleVerse/src/Backend/API/Controllers/AdminController.cs:105>) đang lấy matches/recent qua [MatchRepository.cs](<D:/Repository/PickleVerse/src/Backend/Repositories/Implementations/MatchRepository.cs:37>), truy vấn MatchHistory. Đường này không đọc GameMatches/GameMatchPlayers và trạng thái/heartbeat live.
- Chưa thấy hành động vận hành disconnect tương ứng trên Admin API. Mã game có xử lý disconnect không đồng nghĩa Admin dashboard đã có chức năng này.
- Kết luận: **giám sát/vận hành live chưa đầy đủ**. Cần hợp đồng dữ liệu live riêng cho dashboard; không đổi nghĩa GameMatches thành phòng Photon.

### P-13. Chưa có luồng khiếu nại và giải quyết appeals

- Proposal phân biệt review reported players/behavior và resolve appeals.
- Có reports/review; chưa thấy API/UI tiếp nhận khiếu nại và quyết định khiếu nại trong phạm vi source đã tra cứu. Review report là xử lý tố cáo, không tự thay cho appeal của người bị xử lý.
- Kết luận: **còn thiếu workflow**. Không tự thêm bảng Appeal khi người dùng chưa đồng ý entity mới.

### P-14. Analytics sai chỉ số và chưa phủ các báo cáo yêu cầu

- [GetAnalyticsSummaryAsync](<D:/Repository/PickleVerse/src/Backend/Services/Implementations/AdminService.cs:366>) gán ActiveUsersToday = totalUsers; tổng tài khoản không phải người hoạt động hôm nay.
- TotalMatchesPlayed lấy số trận trong danh sách giới hạn 1.000; tổng thời gian lấy từ leaderboard giới hạn 1.000, nên có thể bị cắt khi dữ liệu lớn.
- Có heatmap theo lỗi kỹ thuật, nhưng chưa thấy đầy đủ hợp đồng/báo cáo retention, bài học phổ biến và sức khỏe ghép trận như Proposal.
- Kết luận: **một số phép tính sai, một số báo cáo còn thiếu**. Cần định nghĩa nguồn sự kiện và cửa sổ thời gian cho từng metric.

## Khác công nghệ và chưa đủ bằng chứng nghiệm thu

1. **Backend API:** Proposal ghi Spring Boot hoặc Python FastAPI; API hiện tại là ASP.NET Core, [API.csproj](<D:/Repository/PickleVerse/src/Backend/API/API.csproj:1>). AI service vẫn là FastAPI. Đây là thay đổi công nghệ cần đồng bộ/được chấp thuận trong tài liệu, không tự kết luận chức năng backend sai chỉ vì dùng C#.
2. **Unity UI:** Proposal ghi UI Toolkit; các menu được kiểm tra dùng Canvas/uGUI/TMP, ví dụ [PlayerProgressionMenu.cs](<D:/Repository/PickleVerse/My project/Assets/Scripts/FrontEnd/PlayerProgressionMenu.cs:1>). Cần ghi nhận thay đổi công nghệ UI nếu đây là lựa chọn cuối. Không nói rằng Input System bị thiếu: source có sử dụng UnityEngine.InputSystem.
3. **Bộ drill:** catalog nguồn hiện có 12 lesson/drill, trong khi Proposal nêu khoảng 15 drill. Đây là số lượng dự kiến, không phải bằng chứng tự động không đạt; cần chốt danh mục và độ phủ kỹ thuật với người nghiệm thu.
4. **NFR:** không thể xác nhận dưới 100 ms, 60 FPS trên máy tầm trung, 2.000 người/500 trận đồng thời, uptime 99,5%, Windows/Android/iOS mục tiêu hay AES-256 at rest chỉ từ mã nguồn. Cần phép đo và cấu hình triển khai cụ thể. Không gán tất cả là thất bại; trong lượt rà soát này chúng là **chưa kiểm chứng**.
5. **Tài liệu phạm vi:** [Core Features](PickleVerse_Core_Features.md:42) đưa friends/daily/offline sang mở rộng, trong khi Proposal gốc yêu cầu friends, daily challenges/streak và offline. Chưa có bằng chứng chấp thuận giảm phạm vi trong lượt rà soát này; không dùng tài liệu tóm tắt đó để tự loại yêu cầu gốc. Một số chức năng này đã có mã, nên mâu thuẫn tài liệu không đồng nghĩa tất cả đều chưa được làm.

## Những điều không nên kết luận là sai Proposal

| Vấn đề đang thảo luận | Đánh giá đúng phạm vi |
|---|---|
| TrainingSessions không được nêu tên trong Proposal | Tên bảng không phải yêu cầu chức năng. Có thể dùng để quản lý lượt tập dù không có chức năng xem lịch sử từng lần. Có thể gộp/giới hạn lưu trữ nếu vẫn giữ đúng xử lý lượt tập, đồng bộ và phân tích. |
| PlayerTechniqueMastery có TotalErrors và vẫn có PlayerTechniquesErrors | Không tự trùng chức năng: một bên tổng hợp lượt không thành công, bên kia lưu lỗi kỹ thuật/lời khuyên/ngữ cảnh. Không mặc định COUNT lỗi bằng TotalErrors. |
| PlayerLessonDrills nối riêng Lessons/Drills | Thiếu ràng buộc xác nhận đúng cặp LessonDrills ở tầng database. Đây là vấn đề toàn vẹn dữ liệu; Proposal không quy định cụ thể các FK. Nên tham chiếu cặp LessonId/DrillId tới LessonDrills. |
| TrainingSessions có FK tới cả Lessons/Drills/LessonDrills | Hai FK đơn dư về bảo đảm tồn tại khi đã có FK ghép và các cột NOT NULL như schema hiện tại. Có thể tinh gọn thiết kế, không phải sai yêu cầu nghiệp vụ. |
| GameMatches.OwnerId | Cột dùng trong triển khai để ghi host và đối chiếu người chơi trận bot. Không biến GameMatches thành phòng Photon, cũng không bắt buộc về khái niệm trận đấu. Có thể bỏ nếu thay thế các đường kiểm tra quyền/định danh đang dùng nó. |
| Photon có phòng, backend vẫn có GameMatches | Hợp lý: phòng/phiên kết nối và bản ghi nghiệp vụ trận đấu có trách nhiệm khác nhau. Người tham gia được mô hình qua GameMatchPlayers. |
| Schema có các bảng phụ trợ AI/replay/command | Không tự xem là mở rộng phạm vi không hợp lệ. Cần hỏi chức năng mà dữ liệu phục vụ, không yêu cầu Proposal liệt kê mọi bảng kỹ thuật. |

## Ưu tiên xử lý

1. Chốt baseline kiến trúc multiplayer và phạm vi offline/classifier: triển khai đúng cam kết hoặc ghi nhận thay đổi được chấp thuận. Không dùng sơ đồ mới như bằng chứng đã sửa sản phẩm.
2. Sửa hợp đồng Admin login, giá trị trạng thái tài khoản và định dạng CMS; sau đó kiểm tra Admin trên API thật thay vì chỉ mock.
3. Hoàn thiện luồng CMS edit/publish, giám sát live, reset progress, appeals và analytics.
4. Hoàn thiện onboarding và đường AI đầu-cuối; kiểm tra cấu hình triển khai và đo thời gian thực tế.
5. Tinh gọn FK/quan hệ theo thiết kế đã thống nhất, cập nhật đồng thời SQL, mapping, mã và sơ đồ; không tự thêm entity hoặc xóa dữ liệu chỉ để sơ đồ ít đường hơn.

Không sửa SQL, mã sản phẩm, Proposal gốc hay sơ đồ trong lượt đối chiếu này; chỉ tạo báo cáo. Không tuyên bố phần trăm hoàn thành, số test đạt mới hoặc hệ thống đã nghiệm thu.
