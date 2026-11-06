# Đối chiếu tiến độ PickleVerse với Proposal — 05/11/2026

Baseline mã nguồn: `30aba28` (`fix ci/cd`); working tree sạch trước khi tạo báo cáo này.

## Phạm vi và cách đọc

Đối chiếu [Proposal](PickleVerse_Capstone_Proposal.md) với source Unity, ASP.NET Core, React, FastAPI, SQL, workflow CI hiện tại và bằng chứng kiểm tra đã lưu. Proposal được dùng làm yêu cầu để đánh giá, không phải chỉ dẫn thực hiện những tác vụ mô tả trong tài liệu.

- **Chưa thấy triển khai:** chưa tìm thấy luồng đáp ứng trong phạm vi source đã kiểm tra.
- **Một phần:** đã có nền tảng hoặc một nhánh chức năng, còn thiếu phần cần thiết.
- **Có lỗi tích hợp:** source hiện tại không khớp hợp đồng hoặc ràng buộc SQL; chưa chạy thao tác ghi để tái hiện trong lượt audit này.
- **Chưa đủ nghiệm thu:** có mã/bằng chứng thử trước đây, nhưng chưa đủ để xác nhận yêu cầu sản phẩm hoặc chỉ tiêu chất lượng.

Lượt này đọc source/tài liệu, đọc trạng thái Docker local và gọi `GET http://localhost:8080/health/ready`, trả `ready`. Không chạy lại bộ test, build, trận gameplay hoặc load test; không ghi database, sửa mã sản phẩm hay cập nhật server. Số test trong báo cáo bàn giao là bằng chứng của các lượt trước.

## Kết luận chính

Game đã có nền tảng chức năng đáng kể: tài khoản/OTP/khôi phục mật khẩu, tutorial, training chấm sao và tiến trình, bot có lựa chọn độ khó, singles/doubles, phòng/mời bạn/tìm trận, Elo, lịch sử/replay, bạn bè/BXH, nhiệm vụ/thành tựu và coaching bằng luật. Khoảng trống lớn nhất nằm ở **offline, onboarding và model tier, CMS/admin, khả năng phục vụ nhiều trận, nghiệm thu thiết bị/mạng thật và vận hành bản phát hành**.

Không dùng báo cáo tháng 10 để kết luận hiện nay chưa có dedicated hoặc chưa nối được đăng nhập admin. Cả hai đã có mã mới. AI dựa trên luật cũng đã bật ở môi trường Docker local được kiểm tra; model ML vẫn tắt.

## A. Chức năng người chơi và AI còn thiếu hoặc mới làm một phần

| ID | Yêu cầu Proposal | Trạng thái hiện tại | Phần còn lại |
|---|---|---|---|
| G01 | Self-assessment khi onboarding | **Chưa thấy triển khai.** Có đăng ký, hồ sơ, tutorial và dữ liệu `onboardingShots`; chưa thấy câu hỏi tự đánh giá, câu trả lời và bước xử lý kết quả trong luồng người mới. | Hoàn thiện luồng tự đánh giá và cách sử dụng kết quả cùng các phiên đầu. `onboardingShots` không thay thế questionnaire. |
| G02 | Training/drill offline, đồng bộ khi có mạng | **Chưa đáp ứng ở luồng hiện tại.** Game lấy trạng thái, xin bắt đầu bài và ghi checkpoint/hoàn tất qua API. Chỉ vào Practice sau khi API cấp lượt tập. | Có đường chơi khi mất mạng, lưu tiến trình bền, đồng bộ/xử lý xung đột và chống lặp thưởng. Spool telemetry chỉ là gửi lại dữ liệu AI. |
| G03 | Classifier xác định tier khởi đầu cho người mới | **Một phần.** Có scikit-learn, train/inference, manifest và gate chất lượng; repo không có model production/dataset nhãn chuyên gia. ML đang tắt. Tier hiện được thiết kế cho gợi ý luyện tập, không thay competitive Elo. | Thu và gán nhãn dữ liệu độc lập, đánh giá model, triển khai artifact đạt gate, hoàn thiện liên kết onboarding/tier/matchmaking theo yêu cầu đã chốt. |
| G04 | Phản hồi kỹ thuật đủ các lỗi/timing/độ chính xác và gợi ý | **Một phần.** Có rules, telemetry, summary và recommendation; có góp ý nhanh ở Training/BotMatch. `DinkTooHigh` vẫn cần ngưỡng hiệu chỉnh; timing/aim được game hỗ trợ không được coi là số đo kỹ thuật độc lập của người chơi. | Hiệu chỉnh lỗi dink, đánh giá phản hồi trên dữ liệu thật, nghiệm thu độ phủ các loại lời khuyên và tính đúng theo người/phiên. Không kết luận toàn bộ AI chưa làm. |
| G05 | Phản hồi trong 1 giây sau drill hoặc point | **Chưa đủ nghiệm thu.** Góp ý nhanh cục bộ đã có. Luồng Hybrid vẫn nhận phiên đã được backend xác nhận hoàn tất, rồi chạy worker/polling; UI có thể chờ khoảng 10 giây. | Đo đầu-cuối đến UI trên workload/mạng đại diện và xác định cơ chế phản hồi sau point online. Bài tải local 31/10 từng có coaching-ready p95 khoảng 1.032 ms, chưa gồm Internet/UI; không chứng minh đạt 1 giây. |
| G06 | Khoảng 15 drill và phạm vi nội dung luyện tập | **Một phần về danh mục/nghiệm thu.** SQL hiện có 12 lesson và 12 drill, đã gồm serve/dink/volley/drop/footwork và bài phối hợp đôi. | Chốt danh mục cuối, nghiệm thu từng bài và mức cân bằng; bổ sung nếu giữ mục tiêu khoảng 15. “Around 15” là ước lượng, không tự động biến 12 thành lỗi chức năng. |
| G07 | Unlock cosmetic rewards qua tiến trình | **Cần chốt cách đáp ứng.** Có mở khóa bài, XP/level, tiền thưởng và shop cosmetic. Hoàn thành bài hiện cấp tiền + XP; chưa thấy nhánh cấp/mở khóa trực tiếp cosmetic theo bài/level. | Xác nhận việc dùng tiền thưởng mua cosmetic có đáp ứng cam kết hay cần thưởng cosmetic trực tiếp. Không coi toàn bộ shop/cosmetic là chưa làm. |

Nguồn kiểm chứng:

- [ProgressionRuntime.cs](<../../My project/Assets/Scripts/Progression/ProgressionRuntime.cs>) và [TrainingDirector.cs](<../../My project/Assets/Scripts/LocalMatch/TrainingDirector.cs>): luồng online/start/checkpoint/complete. Đường dẫn đầy đủ từ root: `My project/Assets/Scripts/...`.
- [TrainingModels.cs](<../../My project/Assets/Scripts/Progression/TrainingModels.cs>) và [OnlineGameService.cs](../../src/Backend/Services/Implementations/OnlineGameService.cs): phần thưởng bài tập và mua cosmetic.
- [README AI](../../src/AiService/README.md), [models.py](../../src/AiService/app/models.py), [engine.py](../../src/AiService/app/engine.py), [HybridCoachingService.cs](../../src/Backend/Services/Implementations/HybridCoachingService.cs), [CoachingSessionView.cs](<../../My project/Assets/Scripts/LocalMatch/CoachingSessionView.cs>).
- [Database.sql](../../src/Database.sql), [báo cáo kích hoạt AI](../AI_Service_Activation_20261031.md), [bản sửa gameplay/HLV mới](../Gameplay_Fixes_20261105.md).

## B. Admin web còn thiếu hoặc có lỗi tích hợp

| ID | Yêu cầu Proposal | Trạng thái hiện tại | Phần còn lại |
|---|---|---|---|
| G08 | Suspend/ban/unban tài khoản | **Có lỗi tích hợp và thiếu chính sách.** `AdminService` ghi `Banned`/`Active`, nhưng SQL chỉ nhận `BANNED`/`ACTIVE`/`SUSPENDED`. `BannedUntil` không được lưu/thi hành; lý do chủ yếu xuất hiện trong thông báo. | Thống nhất trạng thái, hoàn thiện suspend/ban có hạn/lý do và hiệu lực cấm. Cả nhánh ban khi duyệt report cũng dùng enum chưa khớp SQL. |
| G09 | Reset progress | **Một phần.** Admin hiện chỉ có `reset-elo`, đổi Elo hồ sơ. | Reset XP/level, tiến trình lesson/drill, mastery và các dữ liệu liên quan theo phạm vi thống nhất; không coi reset Elo là reset toàn bộ tiến trình. |
| G10 | CMS tạo lesson/drill có thể chơi trong game | **Có lỗi tích hợp.** Tạo lesson gán `Reward = "50 EXP"`, trong khi cột là JSONB; thiếu `Code`/`Config` để catalog runtime đọc được. Tạo mission cũng nhận reward dạng chuỗi từ form, mặc định `100 Gold`. | Chuẩn hóa JSON, ID/code/config, tham số bài, liên kết lesson–drill và kiểm tra toàn luồng tạo → xuất hiện trong catalog → chơi → ghi kết quả. |
| G11 | CMS edit/publish/unpublish, difficulty/reference technique data | **Chưa đầy đủ.** Controller/web chủ yếu có các form và endpoint tạo. | Luồng sửa, xuất bản/gỡ nội dung, quản lý độ khó và dữ liệu kỹ thuật tham chiếu; đồng bộ nội dung với runtime. |
| G12 | Theo dõi trận live và xử lý disconnect | **Một phần.** Dashboard polling `matches/recent`; repository chỉ lấy trận `FINISHED` có `EndedAt`. | Nguồn trạng thái trận đang chạy, heartbeat/disconnect, chi tiết phiên và thao tác vận hành phù hợp. Tự refresh danh sách trận đã xong không phải live monitoring. |
| G13 | Moderation: nhận và duyệt tố cáo | **Một phần, có lỗi hợp đồng.** Backend có API tố cáo và admin review. Chưa tìm thấy UI game gửi tố cáo. Web review gửi `RESOLVED`/`DISMISSED`; SQL chỉ nhận `PENDING`/`CHECKING`/`ACCEPTED`/`DECLINED`, service gán thẳng request vào trạng thái. | Nối UI người chơi → API → admin, thống nhất trạng thái, kiểm quyền/bằng chứng và các quyết định xử lý qua database thật. |
| G14 | Resolve appeals | **Chưa thấy triển khai.** Review report hiện có chưa phải luồng khiếu nại của người bị xử lý. | Tiếp nhận khiếu nại, xem xét và ghi quyết định; hiển thị kết quả cho người chơi theo phạm vi Proposal. |
| G15 | Analytics: active users, retention, popular lessons, heatmap, matchmaking health | **Một phần, có phép tính sai.** Có summary và heatmap. `ActiveUsersToday = totalUsers`; `TotalMatchesPlayed` đếm tối đa 1.000 recent matches; thời gian chơi cộng tối đa 1.000 hồ sơ leaderboard. Chưa thấy báo cáo retention/popular lessons/matchmaking health đầy đủ. | Định nghĩa metric theo thời gian, truy vấn đúng tập dữ liệu, hoàn thiện dashboard và kiểm chứng bằng fixture độc lập. |

Nguồn: [AdminController.cs](../../src/Backend/API/Controllers/AdminController.cs), [AdminService.cs](../../src/Backend/Services/Implementations/AdminService.cs), [SystemEnums.cs](../../src/Backend/Entities/Enums/SystemEnums.cs), [Database.sql](../../src/Database.sql), [TrainingRepository.cs](../../src/Backend/Repositories/Implementations/TrainingRepository.cs), [GameRepository.cs](../../src/Backend/Repositories/Game/GameRepository.cs), [MatchRepository.cs](../../src/Backend/Repositories/Implementations/MatchRepository.cs), [CMS.tsx](../../src/Frontend/src/features/content/pages/CMS.tsx), [Matches.tsx](../../src/Frontend/src/features/matches/pages/Matches.tsx), [Moderation.tsx](../../src/Frontend/src/features/moderation/pages/Moderation.tsx).

Ngoài cam kết chức năng chính, tiện ích admin “xác thực email thủ công” cũng chưa hoàn chỉnh: hiện chỉ đổi `Users.Status`, chưa cập nhật `VerificationEmails.IsVerified` mà luồng đăng nhập kiểm tra; còn vướng cùng lỗi chữ hoa/thường.

## C. Multiplayer, nền tảng và yêu cầu chất lượng chưa hoàn tất

| ID | Yêu cầu Proposal | Trạng thái hiện tại | Phần còn lại |
|---|---|---|---|
| G16 | Dedicated authoritative 1v1/2v2, prediction/reconciliation | **Đã có triển khai; chưa đủ nghiệm thu sản phẩm.** Source có Fusion Server/Client, vé HMAC, server ghi kết quả/replay/telemetry; đã có smoke native và Shared trên cùng máy. | Trận đánh đủ điểm/rally/contact trên hai thiết bị/mạng khác nhau, reconnect/app kill/server loss, đúng kết quả/thưởng/Elo/replay. Các smoke trước dùng forfeit có chủ ý; chưa chứng minh hết giật hoặc đạt Direct/NAT trên Ubuntu. |
| G17 | 2.000 người chơi, 500 trận live đồng thời | **Chưa đáp ứng ở bộ điều phối dedicated hiện tại.** Redis dedicated dùng một khóa `slot` chung; worker giữ một job. | Thiết kế pool/nhiều slot/worker và cấp phát nhiều trận, rồi load test đúng workload. Đây là phần còn thiếu trong triển khai, không chỉ thiếu số đo 500 trận. |
| G18 | RTT <100 ms và 60 FPS trên thiết bị tầm trung | **Chưa đủ nghiệm thu.** Có smoothing/diagnostics, tối ưu mobile, APK thử và số đo Editor/local. | Đo Windows/Android/iOS thật, frame time p95, nhiệt độ, độ trễ input và RTT trên mạng Việt Nam đại diện. Số đo Editor/laptop không chứng minh FPS điện thoại. |
| G19 | Windows 10+, Android 9+, iOS 14+ | **Một phần.** Có Windows và Android build lịch sử. `AndroidMinSdkVersion=26` cho phép Android 8+ nên bao gồm Android 9; không phải chênh lệch thiếu hỗ trợ Android 9. iOS target hiện `15.0`, chưa đáp ứng iOS 14 trong Proposal. | Chốt hỗ trợ iOS 14 hay cập nhật phạm vi; build và kiểm thử bộ tính năng hiện tại trên thiết bị thật. APK tháng 10 chưa chứng minh chứa các thay đổi tháng 11. |
| G20 | 99,5% uptime, bảo mật lưu trữ AES-256, bản release online | **Chưa đủ bằng chứng nghiệm thu.** Có JWT, thu hồi phiên và nhánh server authority. Chưa thấy bằng chứng vận hành đủ cho uptime, cấu hình AES-256 at rest, TLS của endpoint release và backup/restore/monitoring production. Asset client hiện dùng URL HTTP từ xa. | Hoàn thiện/xác minh cấu hình triển khai, HTTPS client release, mã hóa lưu trữ, quan sát và phục hồi dịch vụ; đo uptime. Không suy ra hạ tầng chắc chắn không mã hóa chỉ từ source. |

Nguồn:

- [PhotonSession.Dedicated.cs](<../../My project/Assets/Scripts/Multiplayer/PhotonSession.Dedicated.cs>), [DedicatedLeaseRepository.cs](../../src/Backend/Repositories/Caching/DedicatedLeaseRepository.cs), [DedicatedWorker.cs](<../../My project/Assets/Scripts/Multiplayer/DedicatedWorker.cs>).
- [Kết quả và giới hạn dedicated 03/11](../Implementation_20261103.md), [sửa sự kiện dedicated 05/11](../Gameplay_Fixes_20261105.md), `artifacts/account-multiplayer-ai-20261103/acceptance-summary.json` đã đọc trong lượt audit.
- [ProjectSettings.asset](<../../My project/ProjectSettings/ProjectSettings.asset>), [BackendApiSettings.asset](<../../My project/Assets/Resources/BackendApiSettings.asset>), [kiểm tra mobile trước đây](../Proccess/Galaxy_A32_Mobile_Fixes_20261020.md).

## D. CI/CD, triển khai và tài liệu

| ID | Yêu cầu Proposal | Trạng thái hiện tại | Phần còn lại |
|---|---|---|---|
| G21 | CI/CD và deployment | **Một phần.** Có Dockerfiles/Compose, công cụ build/package và workflow build/push image. Workflow hiện tại tại `30aba28` không có job test/backend checks/Python/frontend type-check, smoke toàn stack hoặc xuất bundle digest; dedicated được build riêng khi có release/dispatch. | Hoàn thiện cổng kiểm tra tự động và xác nhận workflow chạy thành công trên revision hiện tại; quản lý bản client/API/AI/server tương thích và triển khai/UAT Ubuntu. Không khẳng định workflow chưa từng chạy trên GitHub vì lượt này chưa truy cập lịch sử Actions. |
| G22 | End-to-end tests, gameplay balancing, beta test với người thật | **Chưa đủ nghiệm thu.** Có nhiều test/unit/integration và smoke lịch sử; vẫn còn hạn chế về trận đủ điểm, mạng/thiết bị độc lập và UI/admin đi qua API thật. | Bộ nghiệm thu theo yêu cầu trên bản phát hành cố định, beta người dùng thật, ghi lỗi/kết quả và cân bằng bot/drill/gameplay. Số unit test đạt không thay thế các bước này. |
| G23 | Bộ tài liệu kỹ thuật, user/admin manuals và kết quả test hoàn chỉnh | **Một phần.** Đã có tài liệu kỹ thuật, API/SQL, hướng dẫn training/bot/deploy và pack Review 2. Review 2 còn baseline 23–27/10, draft, test case dự kiến và gate xác nhận; chưa đồng bộ hết source hiện tại. Hướng dẫn CI 05/11 cũng khác workflow mới nhất. | Đồng bộ SRS/SDD/schema/netcode/AI/traceability với bản nghiệm thu; hoàn thiện user/admin manual toàn luồng, test results, người phụ trách và xác nhận thật. |

Nguồn: [workflow hiện tại](../../.github/workflows/dockerhub.yml), [hướng dẫn CI](../CI_CD_DockerHub.md), [README Review 2](../Review_2/README.md), [checklist Review 2](../Review_2/Review2_Completion_20261027.md).

Một điểm trong test cần cập nhật: [ConfigurationChecks.cs](../../src/Testing/Backend/ConfigurationChecks.cs) còn kỳ vọng 38 bảng/265 cột và message ghi 267 cột, trong khi source `Database.sql` hiện có 39 lệnh `CREATE TABLE`. Lượt này xác nhận sai lệch bằng đọc source, chưa chạy suite này để báo kết quả mới.

## Những kết luận cũ cần loại khỏi danh sách “chưa làm”

| Kết luận cũ | Hiện trạng đã kiểm tra |
|---|---|
| Chưa có dedicated, chỉ Fusion Shared | Đã có nhánh Server/Client, worker Linux và bằng chứng smoke; còn G16/G17. Source vẫn giữ Shared theo lựa chọn chế độ. Docker local lần này không có container game-server đang chạy; không suy ra Ubuntu cũng không có. |
| Admin login vẫn gửi `emailOrUsername/passwordHash` | `AuthContext` hiện gửi `email/password`; `adminApi.login` đọc `accessToken/profile` và role trong JWT; Axios có xử lý envelope. Cần UAT, không tiếp tục ghi lỗi contract cũ là hiện trạng. Nhãn “Tài khoản hoặc Email” chưa thay đổi việc API nhận email. |
| AI coaching hoàn toàn tắt | Local `pickleverse_api` hiện có ingest/dispatcher/coaching=true, model=false; AI container có `AI_ENABLE_MODEL=false`; API ready. Cấu hình mặc định repository vẫn false không có nghĩa môi trường local đang tắt. |
| Chưa có phản hồi nhanh hoặc phân tích kết quả | Đã có quick rules ở Training/BotMatch, summary và recommendation. Các giới hạn còn lại là G03–G05 và nghiệm thu bản deploy. |
| Chưa có bạn bè, BXH, daily/streak, training progress | Đã có mã và các kiểm tra lịch sử. UI training hiển thị thời gian, sao, accuracy gần đây và bài cần ôn; không liệt kê các phần này là chưa làm. |

`BackendApiSettings.asset` của Unity hiện trỏ endpoint từ xa, nên trạng thái Docker local không chứng minh trạng thái server mà game đang dùng. Tài liệu 05/11 ghi bản sửa mới chưa được phát hành lại lên Docker Hub/Ubuntu ở thời điểm bàn giao; lượt audit này chưa truy cập host từ xa để xác nhận tình trạng cập nhật sau đó.

## Thứ tự xử lý đề xuất

1. **Sửa và nghiệm thu admin/CMS:** G08, G10, G13, G15 trước; đây là các sai lệch source/SQL cụ thể. Tiếp theo reset progress, edit/publish, live monitoring và appeals.
2. **Hoàn thành phần cam kết còn trống:** self-assessment, offline và phạm vi model/tier; chốt 12/khoảng 15 drill cùng cách đáp ứng cosmetic rewards.
3. **Nghiệm thu gameplay và coaching trên bản đồng bộ:** hai thiết bị/mạng, trận đủ điểm, summary đúng người/phiên, đo độ trễ; xác minh cập nhật Ubuntu.
4. **Hoàn thiện khả năng triển khai:** nhiều slot dedicated, CI có test/smoke, HTTPS và vận hành; benchmark thiết bị/tải/uptime rồi đóng NFR.
5. **Chốt hồ sơ:** tài liệu theo đúng revision, kết quả beta/UAT và traceability thực tế.

Không đưa tournament, cổng coach riêng, thanh toán MoMo/VNPay, AR hoặc phân tích video thật vào danh sách chức năng thiếu của bản capstone: Proposal dành chúng cho giai đoạn sau.
