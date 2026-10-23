# Đối chiếu tính năng cốt lõi PickleVerse với hiện trạng game

Ngày khảo sát: **08/10/2026**. Bản mã nguồn được đối chiếu: Git HEAD **`bea4159`**, commit `update sky and light`.

Tài liệu yêu cầu: [PickleVerse_Core_Features.docx](D:/Github/PickleVerse/Doc/PickleVerse_Core_Features.docx). Báo cáo bám theo **15 nhóm tính năng cốt lõi**, **3 tính năng mở rộng** và phần ngoài phạm vi của tài liệu này. Các yêu cầu trong proposal hoặc tài liệu cũ không tự động được thêm vào danh sách bắt buộc.

**Game hiện có nền tảng thi đấu 3D khá rõ: chơi bot cục bộ, điều khiển combo, animation, luật, sân và bối cảnh; multiplayer đã có code và bằng chứng kiểm tra nhiều phiên Windows. Tuy nhiên, vòng học kỹ thuật → chấm điểm → lưu tiến trình → nhận phản hồi → đề xuất bài tập chưa hình thành. Website quản trị chưa có trong bản mã nguồn khảo sát.**

## 1. Cách hiểu trạng thái và phạm vi kiểm tra

| Trạng thái | Ý nghĩa trong báo cáo |
|---|---|
| **Đã có** | Đã triển khai đủ hành vi của nhóm tính năng trong phạm vi prototype được nêu, có code/scene và bằng chứng hỗ trợ. Không đồng nghĩa đã nghiệm thu phát hành trên mọi nền tảng. |
| **Một phần** | Có chức năng con hoặc nền móng, nhưng còn thiếu yêu cầu trong nhóm, thiếu tích hợp hoặc thiếu xác minh cần thiết. Có khung code được ghi rõ, không coi là chức năng người chơi đã dùng được. |
| **Chưa có** | Chưa tìm thấy triển khai đáp ứng yêu cầu trong code và asset hiện tại. Tài liệu mô tả, nhãn UI hoặc package cài sẵn không đủ để tính là hoàn thành. |

Khảo sát đọc nội dung Word, quét source Unity và backend, đọc các script điều phối chính, đối chiếu tham chiếu scene/GUID, Build Settings, cấu hình multiplayer và báo cáo kiểm thử sẵn có. **Không sửa source, scene, model hay cấu hình; không chạy lại game, build, test hoặc gọi dịch vụ cloud trong đợt này.** Kết quả test được dẫn dưới đây là bằng chứng đã lưu từ các lần kiểm tra trước, không phải nghiệm thu mới trên HEAD hiện tại.

Vì một nhóm trong Word chứa nhiều chức năng con, báo cáo không tính phần trăm tiến độ bằng số file, số script hoặc số nhóm. Một nhóm có nhiều code vẫn có thể chưa hoàn thành luồng sử dụng thực tế.

### Tổng hợp 15 nhóm cốt lõi

| Phân hệ | Đã có | Một phần | Chưa có | Tổng |
|---|---:|---:|---:|---:|
| Game cho người chơi | 1 | 4 | 1 | 6 |
| AI cơ bản | 0 | 1 | 3 | 4 |
| Website quản trị | 0 | 0 | 5 | 5 |
| **Tổng** | **1** | **5** | **9** | **15** |

Nhóm duy nhất được xếp **Đã có** là **chơi với bot cơ bản và chọn độ khó**, trong phạm vi bản PC/local hiện tại. Bot thích ứng là nhóm AI riêng và vẫn **Chưa có**. Multiplayer được xếp **Một phần**, dù đã có triển khai và test đáng kể, vì phạm vi xác minh và điều khiển điện thoại chưa đầy đủ.

## 2. Ma trận đối chiếu với tài liệu Word

Các mã G, A, W và X dưới đây do báo cáo đặt để tra cứu; không phải mã yêu cầu có sẵn trong Word.

| Mã | Nhóm tính năng trong Word | Trạng thái | Hiện trạng chính | Phần còn thiếu chính |
|---|---|---|---|---|
| G01 | Tài khoản và hồ sơ | **Một phần** | Unity Services đăng nhập ẩn danh; backend có entity hồ sơ và hàm băm mật khẩu | Đăng ký/đăng nhập thật, lưu hồ sơ, tiến trình, lịch sử và liên kết danh tính Unity với backend |
| G02 | Hướng dẫn nhập môn | **Một phần** | BotMatch có hướng dẫn phím, combo, kitchen và luật cơ bản | Tutorial tương tác, bước thực hành, kiểm tra người chơi hiểu luật và lưu hoàn thành |
| G03 | Bài học và drill kỹ thuật | **Chưa có** | Các kỹ thuật đã đánh được trong trận; scene Training còn tồn tại | Drill riêng, mục tiêu, chấm 1–3 sao, XP, mở khóa bài; Training hiện thiếu script và TrainingUI |
| G04 | Chơi với bot | **Đã có trong prototype PC/local** | Trận đơn, bot đối thủ, ba mức Dễ/Vừa/Khó, vòng trận và kết quả | Nghiệm thu thiết bị di động; thích ứng thuộc A01, không tính vào nhóm cơ bản này |
| G05 | Trận trực tuyến 1v1 và 2v2 | **Một phần** | Phòng mã, custom, nhóm bạn, hàng chờ, Relay, đồng bộ và reconnect | Xác minh giữa nhiều máy qua Internet, trận chơi tay nhiều rally, điều khiển/tối ưu mobile và luồng build thống nhất |
| G06 | Bảng xếp hạng và tiến trình | **Một phần ở mức nền móng** | API bảng hạng giả; entity có rating; trận local có thống kê tạm trong RAM | Bảng hạng thật, lịch sử lưu lâu dài, thành thạo kỹ thuật, huy hiệu, màn hình tiến trình dùng dữ liệu thật |
| A01 | Bot đối thủ thích ứng | **Chưa có** | Bot dự đoán bóng, chọn cú và sai số theo mức khó cố định | Behavior tree theo yêu cầu Word, ước lượng trình độ và tự điều chỉnh chiến thuật/tỷ lệ lỗi |
| A02 | Xếp hạng và ghép trận | **Một phần** | Hàm Glicko-2 giản lược; matchmaking theo vùng kết nối/QoS | Cập nhật hạng sau trận thật, lưu hạng, ghép theo kỹ năng và ước lượng hạng khởi điểm |
| A03 | Phản hồi kỹ thuật | **Chưa có theo yêu cầu** | Có báo lỗi luật, đánh hụt và gợi ý timing đơn giản | Phân tích dữ liệu kỹ thuật qua nhiều cú đánh, nhận diện lỗi và lời khuyên cụ thể |
| A04 | Gợi ý bài tập | **Chưa có** | Chưa có kho drill hay hồ sơ điểm yếu | Đề xuất drill từ điểm yếu và đo tiến bộ sau bài tập |
| W01 | Quản lý người dùng | **Chưa có** | Chưa có website/admin API tương ứng | Tìm tài khoản, khóa/cấm, đặt lại tiến trình và phân quyền quản trị |
| W02 | Quản lý nội dung | **Chưa có** | Nội dung hướng dẫn và độ khó nằm trong code/asset | Tạo/sửa/xuất bản/gỡ bài học, drill, độ khó mà không sửa code |
| W03 | Giám sát trận đấu | **Chưa có** | Game có xử lý mất kết nối; chưa có màn hình vận hành | Danh sách trận đang/chưa kết thúc, nhật ký sự cố, thao tác xử lý từ quản trị |
| W04 | Xử lý báo cáo vi phạm | **Chưa có** | Chủ phòng custom có thể mời khách ra | Người chơi gửi report, hàng chờ duyệt, bằng chứng, quyết định và khiếu nại |
| W05 | Thống kê | **Chưa có** | Có số hit/rally cục bộ và báo cáo test | Người dùng hoạt động, retention, bài học phổ biến, lỗi kỹ thuật tổng hợp trên dashboard |

## 3. Phân tích từng tính năng game cho người chơi

### G01 Tài khoản và hồ sơ

**Yêu cầu:** Đăng ký, đăng nhập, lưu hồ sơ, tiến trình và lịch sử chơi.

| Chức năng con | Đã có gì | Đánh giá |
|---|---|---|
| Danh tính khi chơi online | `MultiplayerApp.InitializeAsync` khởi tạo profile Unity Services và gọi `SignInAnonymouslyAsync` | Có danh tính khách để vào dịch vụ online |
| Tên và giới tính nhân vật | UI test cho nhập tên, chọn Male/Female; gửi thông tin vào phòng | Có trong phiên multiplayer, chưa là hồ sơ tài khoản lưu tại backend |
| Đăng ký | `AuthService.Register` tạo `PlayerProfile`, băm mật khẩu bằng BCrypt, đặt rating 2000 | Có hàm nền; controller chưa lưu dữ liệu vào DB |
| Đăng nhập | `AuthController.Login` trả chuỗi `dummy-jwt-token` | Chưa xác thực email/mật khẩu, chưa phát token thật |
| Hồ sơ lâu dài | Entity có ID, tên, email, hash mật khẩu, rating, RD, volatility, thời điểm tạo | Chưa có repository/DbContext/API đọc–sửa hồ sơ và tích hợp Unity |
| Tiến trình và lịch sử | Chưa thấy cấu trúc lưu bài học, XP, huy hiệu hoặc lịch sử trận | Chưa có |

Backend có một vấn đề tích hợp trực tiếp: [Program.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Api/Program.cs:1) vẫn là ứng dụng mẫu `/weatherforecast`, không thấy `AddControllers`, `MapControllers` hay cấu hình authentication/DB. Vì vậy, việc tồn tại `AuthController` và `LeaderboardController` **chưa chứng minh các endpoint đó đã được đăng ký và hoạt động trong ứng dụng API hiện tại**. Package JWT và PostgreSQL/EF đã khai báo cũng chưa thay thế việc cấu hình và dùng chúng.

Unity hiện không thấy client gọi các API ASP.NET này. Luồng Unity Authentication và hồ sơ `PlayerProfile` chưa có mã nối với nhau. Tùy chọn `PKV_Modifier` lưu bằng `PlayerPrefs` chỉ là sở thích điều khiển, không phải hệ thống lưu tiến trình.

**Để hoàn thành:** Có tài khoản thật hoặc luồng guest được liên kết/nâng cấp rõ ràng; đăng nhập xác thực được; dữ liệu được lưu và đọc lại sau khởi động; Unity dùng cùng danh tính để gắn tiến trình, trận đấu và rating.

**Bằng chứng:** [MultiplayerApp.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/Multiplayer/MultiplayerApp.cs:83>), [AuthController.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Api/Controllers/AuthController.cs:25), [AuthService.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Core/Services/AuthService.cs:9), [PlayerProfile.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Core/Entities/PlayerProfile.cs:5).

### G02 Hướng dẫn nhập môn

**Yêu cầu:** Dạy luật pickleball, đặc biệt kitchen, và cách điều khiển để người mới chơi được.

BotMatch đã có màn hình trước trận, bảng hướng dẫn F1 và các nội dung về WASD/Shift, giữ–ghép–thả J/K/L, modifier U/I, giao chéo, hai lần nảy bắt buộc, kitchen, time-out và cách tính điểm. HUD còn gợi ý thời điểm thả J khi dự đoán bóng đến. Đây là **hướng dẫn bằng văn bản và hỗ trợ trong trận**.

Chưa có chuỗi tutorial như “di chuyển tới vị trí → giao đúng ô → chờ bóng nảy → thử volley ngoài kitchen”, kiểm tra hoàn thành từng bước, minh họa tình huống đúng/sai hoặc lưu người chơi đã học xong. Người mới vẫn phải tự đọc và thử trong trận có timing thực tế.

**Để hoàn thành:** Một lượt nhập môn có thực hành, kiểm tra được luật kitchen và two-bounce rule, dùng được phím/cảm ứng, có thể xem lại hoặc bỏ qua, và lưu trạng thái hoàn thành nếu cần phục vụ tiến trình.

**Bằng chứng:** [MatchHud.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/MatchHud.cs:34>), [BotMatch.unity](<D:/Github/PickleVerse/My project/Assets/Scenes/BotMatch.unity>), [hướng dẫn BotMatch](D:/Github/PickleVerse/Doc/PickleVerse_BotMatch_Guide.md).

### G03 Bài học và drill kỹ thuật

**Yêu cầu:** Drill giao bóng, dink, volley, third-shot drop; mỗi drill có 1–3 sao, XP và mở khóa bài mới.

| Phần yêu cầu | Hiện trạng |
|---|---|
| Thực hiện bốn kỹ thuật trong game | Có thao tác và animation trong vòng trận BotMatch/multiplayer |
| Bài học riêng cho từng kỹ thuật | Chưa có triển khai hiện tại |
| Drill có bóng cấp, mục tiêu, số lượt và điều kiện kết thúc | Chưa có triển khai đáp ứng tài liệu |
| Chấm 1–3 sao | Chưa có tiêu chí và bộ chấm |
| Cộng XP | Chưa có cập nhật XP thật |
| Mở khóa bài tiếp theo | Chưa có cây bài học và điều kiện mở khóa |
| Lưu kết quả drill | Chưa có |

**Scene Training hiện không thể được tính là chế độ huấn luyện hoàn chỉnh.** Đối chiếu các GUID script của scene với source `.cs.meta` có file `.cs` thực trong Assets/Packages/PackageCache cho thấy **8 tham chiếu không có source tương ứng**, tại các dòng 1219, 1254, 1270, 1289, 1303, 1416, 1762 và 1911. Đây là kết quả đọc asset tĩnh, chưa phải thông báo Console từ một lượt Play mới.

Ngoài ra, `Assets/Scenes/TrainingUI.unity` được bật trong Build Settings nhưng file scene không tồn tại. Các script/model/voice mà `OfflineCoach.md` và `Sprint1-Tracking.md` mô tả cũng không còn trong inventory runtime hiện tại. Tài liệu đó chứng minh từng có một demo trước đây, **không chứng minh bản đang khảo sát còn chạy được demo đó**.

Word hiện yêu cầu phân tích dữ liệu trong game và loại phân tích video thật khỏi phạm vi. Vì vậy, không nhất thiết phải khôi phục hệ thống webcam cũ để làm G03/A03; có thể xây drill trực tiếp từ vòng bóng, kỹ thuật và dữ liệu vị trí/timing của game hiện tại.

**Để hoàn thành:** Tối thiểu bốn drill có mục tiêu, tiêu chí sao công khai, kết quả thật, XP/mở khóa và lưu tiến trình; luồng Training không còn tham chiếu thiếu và có thể vào từ menu.

**Bằng chứng:** [Training.unity](<D:/Github/PickleVerse/My project/Assets/Scenes/Training.unity:1219>), [EditorBuildSettings.asset](<D:/Github/PickleVerse/My project/ProjectSettings/EditorBuildSettings.asset:14>), [OfflineCoach.md](<D:/Github/PickleVerse/My project/Docs/OfflineCoach.md>), [ShotInput.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/ShotInput.cs:49>).

### G04 Chơi với bot

**Yêu cầu:** Đánh trận với bot, chọn mức độ khó.

Nhóm này đã có trong prototype PC/local:

- Scene BotMatch có người chơi nữ, bot nam, model sân, bóng, vợt và bối cảnh.
- Người chơi di chuyển, chạy, đánh cơ bản và combo; bot dùng cùng `RequestShot`, animation, contact và luật để đánh.
- Ba mức **Dễ/Vừa/Khó** thay đổi thời gian phản ứng, độ chính xác và một số lựa chọn cú.
- Bot tự giao bóng, dự đoán điểm tiếp bóng, di chuyển đón bóng, chọn Drive/Dink/Drop/Volley/Reset/Smash và trở về vị trí.
- Có tính điểm, chuyển rally/game, kết thúc trận, chơi lại, pause, time-out, hướng dẫn và kết quả.
- Không cần gọi backend hay Unity Services trong vòng trận local.

`LocalBot` dùng phản ứng khoảng **0,28 / 0,15 / 0,08 giây** theo mức khó; sai số ngắm trong `BotMatch.ShotTarget` khoảng **0,30 / 0,16 / 0,08 m**. Các giá trị được lựa chọn từ mức khó, không suy ra từ trình độ người đang chơi.

Báo cáo local đã lưu **29/29 test luật/input/quỹ đạo đạt**, các kiểm tra combo/contact và mô phỏng vòng trận. Điều khiển cảm ứng có trong scene, nhưng bằng chứng chưa bao gồm nghiệm thu bản Android/iOS trên thiết bị thật. Vật lý bóng, vùng hỗ trợ contact và hình học kiểm tra chân vẫn là mô hình phục vụ gameplay prototype.

**Kết luận:** Đủ tính năng “chơi bot + chọn mức khó” của G04 trong phạm vi local PC. Không dùng kết quả này để đánh dấu A01 bot thích ứng đã hoàn thành.

**Bằng chứng:** [LocalBot.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/LocalBot.cs:5>), [BotMatch.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/BotMatch.cs:126>), [MatchHud.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/MatchHud.cs:37>), [kết quả test local](D:/Github/PickleVerse/UnityWork/BotMatch/QA/editmode-tests.json).

### G05 Trận trực tuyến 1v1 và 2v2

**Yêu cầu:** Đánh đơn/đôi qua mạng, mời bằng mã phòng hoặc ghép trận nhanh.

| Chức năng con | Hiện trạng |
|---|---|
| 1v1 và 2v2 | Có mode, roster, spawn nhân vật, luật đơn/đôi và HUD |
| Mã phòng | Có tạo/join bằng mã, sao chép mã và phòng custom |
| Nhóm bạn đánh cùng đội | Có nhóm duo, ticket nhóm và kiểm tra không tách nhóm sang hai đội |
| Custom | Có đổi đội, sẵn sàng, chủ phòng bắt đầu, mời ra và kiểm tra đủ/cân bằng người |
| Ghép nhanh | Có hai queue, đo QoS, chờ 6 giây, timeout 120 giây tìm thực, hủy tìm/kết nối |
| Đồng bộ | NGO, Relay, host mô phỏng bóng/luật; client gửi input, dự đoán di chuyển và sửa theo host |
| Mất kết nối | Có giữ danh tính/ownership, pause và thử nối lại tối đa 30 giây; quá hạn xử thua |
| Host rời | Có kết thúc phiên/trở về phòng; chưa có tiếp tục trận bằng chuyển host |
| Kết quả/trở về phòng | Có kết quả trong phiên và giữ nhóm/phòng sau trận |
| Lịch sử/rating backend | Chưa có tích hợp lưu kết quả/cập nhật hạng |
| Điều khiển điện thoại | `NetworkPlayer` đọc `Keyboard.current`; chưa thấy đường input touch tương ứng BotMatch |

Hai queue `PickleVerse-1v1` và `PickleVerse-2v2` có file cấu hình; báo cáo triển khai ghi nhận được tạo trong môi trường `pickleverse-test` ngày 07/10/2026. Đây là bằng chứng triển khai đã lưu, không phải xác nhận trạng thái cloud đang hoạt động hôm nay.

[MultiplayerWindowsTestResults.md](<D:/Github/PickleVerse/My project/Docs/MultiplayerWindowsTestResults.md>) ghi nhận nhiều phiên Windows dùng Unity Services/Relay thật: 1v1, bốn solo 2v2, duo + hai solo, hai duo, custom, hủy tìm, reconnect, rời trận và xử lý quá hạn. Các ca kết thúc game 11:0. Báo cáo cũng nêu giới hạn: **các phiên chạy cùng máy**, trận tự động chủ yếu giao bóng cho đối phương đứng yên, **chưa xác minh từ máy khác qua Internet hoặc thay thế trận chơi tay nhiều cú trao đổi**.

Các bằng chứng này mạnh hơn việc chỉ có code networking, nhưng chưa đủ nghiệm thu toàn bộ trải nghiệm online trên PC và điện thoại. Những lần chỉnh clip/model sau báo cáo cũng cần hồi quy multiplayer, không mặc định build đã test trước đó chính là HEAD hiện nay.

**Để hoàn thành:** Chơi tay đủ rally với 2/4 người trên nhiều máy qua Internet; xác minh mọi kỹ thuật, latency, rớt mạng và quay về phòng; có input mobile và build thiết bị thật; thống nhất đường vào online từ bản game phát hành.

**Bằng chứng:** [MultiplayerApp.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/Multiplayer/MultiplayerApp.cs:114>), [GroupMatchmaker.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/Multiplayer/GroupMatchmaker.cs:34>), [NetworkPlayer.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/Multiplayer/NetworkPlayer.cs:98>), [NetworkMatch.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/Multiplayer/NetworkMatch.cs:114>), [cloud deployment](<D:/Github/PickleVerse/My project/Docs/MultiplayerCloudDeployment.txt>).

### G06 Bảng xếp hạng và tiến trình

**Yêu cầu:** Xem thứ hạng, lịch sử trận, mức thành thạo từng kỹ thuật và huy hiệu.

Backend `LeaderboardController.GetTop100` chỉ trả hai bản ghi cố định `pro_player` và `newbie`. Chưa thấy query dữ liệu người chơi thật, phân trang, hạng của bản thân hoặc API lịch sử trận. `[Authorize]` có trên controller nhưng cấu hình API hiện chưa thiết lập luồng xác thực tương ứng.

Trong BotMatch, `TotalHits`, `TotalRallies`, `LongestRally` và `RallyLog` có dữ liệu diễn biến trận thật, nhưng chỉ là dữ liệu trong đối tượng đang chạy; khởi tạo/chơi lại xóa chúng. Màn hình kết quả hiện số game thắng, số rally và rally dài nhất. **Đây là thống kê phiên hiện tại, chưa là lịch sử chơi lâu dài.**

Menu có các nhãn Level, XP, Career, Progression và nhiệm vụ. Trong scene hiện tại không thấy hệ thống source gắn các nhãn này với dữ liệu tiến trình. Không nên dùng việc nhìn thấy thanh XP/level để kết luận G06 hoàn thành.

| Phần cần có | Trạng thái |
|---|---|
| Rating trong entity | Có trường dữ liệu |
| Bảng hạng người chơi thật | Chưa có |
| Kết quả trận lưu và đọc lại | Chưa có |
| Điểm thành thạo từng kỹ thuật | Chưa có |
| Huy hiệu và điều kiện cấp | Chưa có |
| UI tiến trình dùng dữ liệu thật | Chưa có |

**Để hoàn thành:** Tài khoản đọc được kết quả đã lưu, hạng thật, lịch sử, mastery và huy hiệu; số liệu phải tồn tại qua nhiều phiên và được cập nhật từ trận/drill thực.

**Bằng chứng:** [LeaderboardController.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Api/Controllers/LeaderboardController.cs:12), [BotMatch.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/BotMatch.cs:283>), [MatchHud.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/MatchHud.cs:56>), [Menu.unity](<D:/Github/PickleVerse/My project/Assets/Scenes/HomeScene/Menu.unity>).

## 4. Phân tích từng tính năng AI cơ bản

### A01 Bot đối thủ thích ứng

**Yêu cầu:** Bot tự điều chỉnh cách đánh và tỷ lệ mắc lỗi theo trình độ người chơi, dùng behavior tree.

Bot hiện có năng lực đối thủ cơ bản: dự đoán bóng, nhận biết cú cần nảy, chọn kỹ thuật theo độ cao/vị trí và di chuyển có thời gian phản ứng. Tuy nhiên:

- `BotMatch` gán `opponent.difficulty = botDifficulty` mỗi bước mô phỏng.
- `LocalBot` dùng nhánh điều kiện và xác suất cố định; không thấy cấu trúc behavior tree.
- Chưa có hồ sơ trình độ người chơi, thống kê rolling window, bộ ước lượng kỹ năng hoặc cập nhật chiến thuật từ kết quả người chơi.
- Chưa có điều chỉnh tỷ lệ lỗi có mục tiêu để giữ trận cân bằng.

Bot thay cú theo trạng thái bóng là phản ứng với tình huống trận. Yêu cầu Word còn đòi **thay hành vi theo năng lực người chơi**, nên hiện chưa đáp ứng A01. Package AI Navigation và Sentis trong manifest không chứng minh behavior tree, inference hay thích ứng đang được dùng cho bot.

**Để hoàn thành:** Có behavior tree theo yêu cầu; lấy dữ liệu người chơi để ước lượng trình độ; điều chỉnh phản ứng, độ chính xác, lựa chọn cú và mức áp lực trong giới hạn; test được bot tự thay đổi khi người chơi mạnh/yếu mà không cần bấm đổi độ khó. Thích ứng cơ bản có thể chạy local, không bắt buộc chờ backend nếu phạm vi thiết kế cho phép.

**Bằng chứng:** [LocalBot.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/LocalBot.cs:16>), [BotMatch.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/BotMatch.cs:130>), [sai số bot](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/BotMatch.cs:228>).

### A02 Xếp hạng và ghép trận theo trình độ

**Yêu cầu:** Điểm kỹ năng kiểu Elo sau mỗi trận, ghép người cùng trình độ, ước lượng hạng khởi điểm.

| Thành phần | Có gì hiện tại | Khoảng trống |
|---|---|---|
| Công thức rating | `Glicko2Service.CalculateNewRating` tính rating/RD/reliability từ hai profile và kết quả | Chưa nối với kết thúc trận hay lưu dữ liệu |
| Volatility | Có trường `Volatility`; hàm giữ `newSigma = sigma` | Thuật toán Glicko-2 chưa đầy đủ; phần cập nhật volatility bị giản lược |
| Hạng khởi điểm | `AuthService` và entity mặc định 2000 | Là giá trị cố định, không phải ước lượng từ bài đánh giá |
| Ghép trận | Đo QoS và yêu cầu cùng vùng kết nối | Không có điều kiện chênh lệch rating/kỹ năng |
| Dữ liệu ticket | Tên, gender, group, region | Không truyền rating/độ tin cậy để ghép kỹ năng |
| Cập nhật sau trận | Local/online hiện kết thúc và hiển thị kết quả | Chưa gửi kết quả xác thực đến rating service |

Glicko-2 thuộc hướng rating kỹ năng tương tự mục đích của Elo; không nhất thiết phải đổi tên thuật toán chỉ vì Word viết “kiểu Elo”. Điểm chưa hoàn thành là **luồng dữ liệu và ghép theo trình độ**, cùng độ đầy đủ của thuật toán hiện chọn. Trường `DuprRating` trong code cũng chưa là bằng chứng tích hợp với hệ thống DUPR bên ngoài.

**Để hoàn thành:** Chốt công thức/kiểm thử; cập nhật rating từ kết quả trận đáng tin cậy đúng một lần; lưu trạng thái cần cho lượt sau; ticket và queue dùng rating; có đánh giá hoặc cơ chế placement cho người mới và tiêu chí xử lý nhóm 2v2.

**Bằng chứng:** [Glicko2Service.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Core/Services/Glicko2Service.cs:10), [AuthService.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Core/Services/AuthService.cs:17), [GroupMatchmaker.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/Multiplayer/GroupMatchmaker.cs:45>), [queue 1v1](<D:/Github/PickleVerse/My project/Assets/Settings/Multiplayer/Cloud/PickleVerse_1v1.mmq:33>), [queue 2v2](<D:/Github/PickleVerse/My project/Assets/Settings/Multiplayer/Cloud/PickleVerse_2v2.mmq:33>).

### A03 Phản hồi kỹ thuật

**Yêu cầu:** Phân tích timing, vị trí tiếp bóng, di chuyển để nhận ra lỗi như giao trễ, dink cao, vào kitchen và đưa lời khuyên cụ thể.

Game đã có nền dữ liệu có thể khai thác: loại cú, thời điểm contact, vị trí bóng/nhân vật, quỹ đạo, fault và kết quả rally. Có kiểm tra đánh hụt, lỗi chân giao, timeout, kitchen; BotMatch còn có gợi ý khi thả J và tạo chuỗi “Chạm tốt/Chạm lệch” trong logic contact.

Nhưng chưa thấy pipeline thu mẫu kỹ thuật, phân tích nhiều lượt, đặt ngưỡng từng kỹ thuật, ghi nguyên nhân và sinh lời khuyên. `RallyLog` chỉ lưu chuỗi lỗi/điểm; dữ liệu này chưa đủ cấu trúc để đánh giá timing, dink quá cao và quá trình tiến bộ một cách có hệ thống. Chuỗi chất lượng contact trong code cũng không đồng nghĩa đã có màn hình coach tổng hợp.

Thông báo “Volley trong kitchen” là trọng tài báo lỗi. Muốn đáp ứng A03 cần diễn giải thành phản hồi hành động, chẳng hạn: “Bạn volley khi còn di chuyển về vạch; hãy dừng đà trước contact”, và có dữ liệu chứng minh nhận định đó. Đây là đề xuất tiêu chí, không phải chức năng đã được cài.

Demo webcam/Offline Coach trong tài liệu cũ không được tính là triển khai hiện tại, và cũng không trùng hoàn toàn phạm vi phân tích dữ liệu in-game của Word mới.

**Để hoàn thành:** Thu dữ liệu cú đánh có định danh/timestamp; nhận diện tối thiểu các lỗi Word nêu; hiển thị lời khuyên có nguyên nhân và ngữ cảnh; lưu tổng hợp điểm yếu; xác minh tình huống đúng/sai và giới hạn độ tin cậy.

**Bằng chứng:** [TryContact và ShotFeedback](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/BotMatch.cs:199>), [HUD timing](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/MatchHud.cs:34>), [fault text](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/BotMatch.cs:357>), [OfflineCoach.md](<D:/Github/PickleVerse/My project/Docs/OfflineCoach.md>).

### A04 Gợi ý bài tập

**Yêu cầu:** Đề xuất drill tiếp theo theo kỹ thuật còn yếu.

Chưa có catalog drill, hồ sơ mastery, kết quả phân tích điểm yếu hoặc hệ thống xếp thứ tự bài tập. Lời nhắc “chơi lại để luyện timing” cuối trận là thông báo chung, chưa chọn bài cụ thể cho người chơi.

**Để hoàn thành:** G03 cung cấp bài tập và kết quả; A03 cung cấp điểm yếu; hệ thống ánh xạ chúng sang drill có lý do đề xuất, đồng thời cập nhật đề xuất khi người chơi tiến bộ. Bản đầu có thể dùng quy tắc rõ ràng, không nhất thiết là mô hình học máy.

**Bằng chứng đối chiếu:** [MatchHud.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/MatchHud.cs:56>) và inventory runtime hiện không có module drill/recommendation/mastery.

## 5. Phân tích từng tính năng website quản trị

Trong các thư mục source hiện tại, `src` chứa solution ASP.NET API/Core/Tests; chưa thấy ứng dụng frontend quản trị hoặc controller quản trị tương ứng năm nhóm của Word. API tài khoản/bảng hạng đang có là nền móng backend, không phải website quản trị đã hoàn thành.

| Mã | Yêu cầu chi tiết | Chức năng dễ bị nhầm với phần đã có | Phần phải bổ sung để hoàn thành |
|---|---|---|---|
| W01 | Tìm tài khoản, khóa/cấm, đặt lại tiến trình | Nút mời khách ra phòng custom | Website/API tìm người dùng thật, trạng thái tài khoản, quyền admin, khóa/cấm và reset tiến trình; ghi người thực hiện và kết quả |
| W02 | Tạo, sửa, xuất bản/gỡ bài học, drill và độ khó | Builder Unity; nút chọn Dễ/Vừa/Khó | Mô hình nội dung, trạng thái nháp/xuất bản, API CRUD, giao diện biên tập và game đọc phiên bản nội dung được xuất bản |
| W03 | Xem trận đang diễn ra/đã kết thúc, xử lý mất kết nối | Reconnect 30 giây trong game và HUD kết quả | Registry trận, lưu vòng đời/sự cố, dashboard vận hành và thao tác xử lý có kiểm soát |
| W04 | Duyệt report, xử lý vi phạm/khiếu nại | Host loại khách khỏi một phòng | Người chơi gửi report, lưu bằng chứng, hàng chờ duyệt, quyết định, thông báo và khiếu nại |
| W05 | Người dùng hoạt động, retention, bài học phổ biến, lỗi thường gặp | `TotalRallies`, số hit và báo cáo NUnit | Schema sự kiện, thu/lưu dữ liệu thật, tổng hợp theo thời gian, dashboard và các định nghĩa chỉ số rõ ràng |

**Tất cả W01–W05: Chưa có.** Mời ra khỏi phòng không khóa tài khoản toàn hệ thống; tự reconnect không tạo dashboard giám sát; đổi asset trong Editor không đáp ứng cập nhật bài học “không cần sửa code”. Báo cáo kiểm thử đo chất lượng phần mềm, không đo người dùng hoạt động hay retention sản phẩm.

**Bằng chứng:** [Program.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Api/Program.cs), [AuthController.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Api/Controllers/AuthController.cs), [LeaderboardController.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Api/Controllers/LeaderboardController.cs), [KickAsync](<D:/Github/PickleVerse/My project/Assets/Scripts/Multiplayer/MultiplayerApp.cs:238>), [xử lý mất kết nối trong trận](<D:/Github/PickleVerse/My project/Assets/Scripts/Multiplayer/NetworkMatch.cs:242>).

## 6. Tính năng mở rộng và ngoài phạm vi

### Ba mục mở rộng

| Mã | Mục trong Word | Trạng thái | Phân tích |
|---|---|---|---|
| X01 | Danh sách bạn bè và thách đấu trực tiếp | **Chưa có** | Mã phòng và duo hỗ trợ chơi cùng người quen, nhưng chưa có friend list, add/remove friend, trạng thái online và lời mời thách đấu theo tài khoản |
| X02 | Daily challenge và phần thưởng chuỗi ngày | **Chưa có** | Menu có DAILY/WEEKLY/CLAIM REWARD; chưa thấy engine nhiệm vụ, lịch reset, trạng thái hoàn thành, nhận thưởng hoặc streak lưu dữ liệu |
| X03 | Offline cho phần luyện tập | **Một phần về nền tảng offline** | BotMatch chạy local không cần backend; phần drill/học offline chưa có và Training hiện thiếu tham chiếu. Không tính đấu bot offline thành hoàn thành chế độ drill offline |

### Những mục Word loại khỏi phạm vi

Giải đấu, khán giả, mua vật phẩm qua MoMo/VNPay và phân tích video thật **không đưa vào số tính năng chưa hoàn thành cốt lõi**. Scene `Shop` hoặc tab Store không tạo yêu cầu phải làm thanh toán trong đợt cốt lõi này. Demo coach/webcam cũ cũng không nên kéo phạm vi trở lại phân tích video nếu chưa có quyết định đổi yêu cầu.

## 7. Toàn bộ chức năng hiện tại và mức hoàn thiện thực tế

### 7.1 Gameplay local và phần trình bày

| Hệ thống hiện có | Mức hiện tại | Giới hạn cần hiểu |
|---|---|---|
| Vòng trận đấu bot 1v1 | Có Intro/PreServe/Rally/Settling/BetweenPoints/BetweenGames/Timeout/MatchOver | Là trận local, chưa ghi tài khoản/backend |
| Điều khiển PC | WASD, Shift, J/K/L, U/I, giữ–ghép–thả; hủy thao tác và xử lý mất focus | Timing contact là phần animation, không đánh tức thời khi thả |
| Combo | Drive/Dink/Volley, Drop/Smash/Reset, lực và hướng chéo | Ghép tối đa hai nút ở parser local; các biến thể chia sẻ một số animation |
| Điều khiển cảm ứng local | Joystick, chạy ở vành ngoài, nút cơ bản/combo, toggle modifier | Có kiểm tra Editor; chưa nghiệm thu điện thoại thật |
| Bot | Đón bóng, chọn cú, footwork, tự giao; ba mức cố định | Không behavior tree/thích ứng trình độ |
| Luật đơn | Side-out scoring, 11 cách 2, thắng 2/3 game, giao chéo, two-bounce, kitchen và momentum, out, double bounce, lỗi giao, time-out, đổi sân | Mô hình chân/contact/va chạm là proxy gameplay; chưa khẳng định mô phỏng mọi điều khoản thi đấu chuyên nghiệp |
| Bóng local | Quỹ đạo trọng lực, nảy, tiếp xúc vợt, xử lý lưới, điểm nảy/đích dự đoán | Chưa mô phỏng spin và khí động học đầy đủ; có contact assist |
| Hiển thị bóng | Vệt bay nhẹ; object bóng đen dưới bóng bị tắt; giữ bóng theo tay trái qua `HeldBallPosition` | Các thay đổi này được xác minh cho BotMatch; không mặc định toàn bộ online dùng cùng code |
| Animation | Idle, locomotion, footwork, serve, FH/BH các cú, smash, phản ứng fault, recovery; nữ có Dance Win | Nam thiếu Dance Win trong bộ local; Jump có asset nhưng không dùng ở trận bot |
| Duration clip | Bộ clip theo tài liệu duration đã được chuẩn hóa, có báo cáo so sánh curve/GUID | Tốc độ runtime online vẫn có override riêng, xem mục 8 |
| Tóc/váy | Spring/secondary bones, giới hạn góc, ràng buộc thân/chân; profile nam/nữ | Là mô phỏng phụ cho hình ảnh, không phải cloth simulation đầy đủ; mobile chưa đo hiệu năng |
| Ngoại hình nữ | Có báo cáo sửa atlas/normal, thêm lớp mái và sửa weight đuôi tóc | Atlas 4K tăng nhu cầu bộ nhớ; bằng chứng sửa model không thay thế nghiệm thu thiết bị |
| Camera gameplay | Theo người chơi và phía sân; có chuyển phía khi đổi sân | Camera online dùng component khác |
| Cinematic vào trận | Hai nhân vật đi vào sân, góc máy bay/chuyển cảnh, trả về gameplay, chữ “Trận đấu bắt đầu”, bỏ qua | Có trên BotMatch; chưa thấy tích hợp chuỗi này vào multiplayer |
| Sky/ánh sáng | Chọn Ban ngày/Ban đêm trước trận; ngày bật sun, đêm bật 4 đèn sân; sky/fog/ambient đổi tương ứng | Không còn random 50/50; lựa chọn khóa trong trận; hệ thống hiện gắn với BotMatch |
| HUD | Điểm, game, server clock, trạng thái/lỗi, độ khó, pause/help/time-out, combo nhỏ khi chọn/đánh | Không phải UI tài khoản, bài học hay tiến trình lâu dài |
| Thống kê trận local | Hit, rally, rally dài nhất, log và kết quả | Chỉ nằm trong RAM, chơi lại xóa |

Nguồn chính: [BotMatch.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/BotMatch.cs>), [SinglesRules.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/Rules/SinglesRules.cs>), [MatchActor.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/MatchActor.cs>), [TouchControl.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/TouchControl.cs>), [CharacterSecondaryMotion.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/CharacterMotion/CharacterSecondaryMotion.cs>), [BotMatchIntro.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/BotMatchIntro.cs>), [MatchEnvironmentLighting.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/LocalMatch/MatchEnvironmentLighting.cs>).

### 7.2 Gameplay online

Online có vòng phòng/ghép/kết nối/trận/kết quả, lựa chọn nhân vật nam/nữ, điều khiển PC và combo; bóng Rigidbody ở host, đồng bộ vị trí/trạng thái/điểm, animation từ controller, marker đội, camera theo nhân vật, luật nhận/giao đơn/đôi, kitchen/momentum, out/two-bounce và xử lý bỏ trận. Có gắn `CharacterSecondaryMotion` khi tạo model online.

Các tính năng này nằm trong hệ thống riêng `MultiplayerApp`, `NetworkMatch`, `NetworkPlayer`, `PickleballRules`, không phải BotMatch tự chuyển sang online. Một số cú Reset/SpeedUp/Punch được code/UI đánh dấu thử nghiệm. Trận online hiện hoàn tất một game 11 cách 2; chưa thấy vòng best-of-three, time-out thi đấu và đổi sân như local.

Không có bằng chứng đang dùng backend ASP.NET cho kết quả online, xếp hạng, hồ sơ kỹ thuật hoặc quản trị. `Finish` hiển thị kết quả trong phiên; Unity Services Session dùng để tổ chức người chơi không tự tạo kho lịch sử trận theo yêu cầu Word.

### 7.3 Scene và đường vào game

| Scene | Hiện trạng đọc từ asset/source |
|---|---|
| `BotMatch` | Scene gameplay local có MatchActor, BotMatch, HUD, touch, intro và lighting |
| `MultiplayerTest` | UI vào dịch vụ, phòng, nhóm, tìm trận và custom |
| `MultiplayerCourt` | Sân online, collider sân/lưới, bootstrap network và HUD |
| `HomeScene/Menu` | Giao diện home/progression/mission; chức năng custom script được tìm thấy là nút Play mở MultiplayerTest |
| `Training` | Scene cũ có model/prefab, nhưng 8 tham chiếu script chưa giải được từ source hiện có |
| `TrainingUI` | Có trong Build Settings, thiếu file scene |
| `Shop` | Trong scene hiện tại chỉ thấy Camera và Directional Light; chưa có chức năng shop |
| `SampleScene` | Scene mẫu Camera/Light/Volume; không có vòng gameplay |
| `Main` | Scene dựng model/prefab, không thấy custom gameplay script trong phần scene khảo sát |

Global Build Settings hiện mở đầu bằng `SampleScene`, tiếp theo `Training`, `TrainingUI` và `BotMatch`. Build Profile `MultiplayerWindows` override danh sách bằng `MultiplayerTest`, `MultiplayerCourt`, `Menu`. Vì thế **đã có scene chơi được không có nghĩa đã có một bản build thống nhất mở menu rồi đi được tới tất cả chế độ**. Nút Play của Menu hiện dẫn online, chưa thấy route home tương ứng đến BotMatch/drill.

Nguồn: [EditorBuildSettings.asset](<D:/Github/PickleVerse/My project/ProjectSettings/EditorBuildSettings.asset:7>), [MultiplayerWindows.asset](<D:/Github/PickleVerse/My project/Assets/Settings/BuildProfiles/MultiplayerWindows.asset:27>), [MenuPlayMultiplayer.cs](<D:/Github/PickleVerse/My project/Assets/Scripts/Multiplayer/MenuPlayMultiplayer.cs:7>).

### 7.4 Backend và kiểm thử hiện có

| Thành phần | Giá trị đã có | Chưa chứng minh |
|---|---|---|
| ASP.NET API/Core/Tests | Solution .NET, controller khung, entity, BCrypt và công thức rating | API tài khoản/rating hoạt động, DB/persistence, game client tích hợp |
| Auth unit test | Có test tạo profile và kiểm tra hash/mặc định rating | Đăng nhập thật, endpoint, tài khoản trùng, lưu DB hoặc phiên xác thực |
| Rating unit test | Có test tăng hạng khi thắng, giảm khi thua | Thuật toán đầy đủ, placement, 2v2, cập nhật đúng một lần sau trận |
| Local Unity tests | Source test luật/input/quỹ đạo; báo cáo lưu 29 đạt | Toàn bộ UI/animation/mobile và gameplay tay hiện tại đều đạt |
| Multiplayer Unity tests | Báo cáo lưu 25 đạt, gồm clock/chord/luật | Cảm giác gameplay, tất cả animation và các điều kiện mạng thực |
| Multiplayer Windows build | Báo cáo đã lưu Succeeded, 0 lỗi, 1 cảnh báo | HEAD hiện tại đã build/hồi quy, hoặc Android/iOS đã chạy |
| Báo cáo intro/motion/lighting | Có kết quả kiểm tra đã lưu cho từng thay đổi | Mọi hệ thống kết hợp trên tất cả platform đã nghiệm thu |

Nguồn kiểm thử: [AuthServiceTests.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Core.Tests/AuthServiceTests.cs), [Glicko2ServiceTests.cs](D:/Github/PickleVerse/src/Backend/PickleVerse.Core.Tests/Glicko2ServiceTests.cs), [SinglesMatchTests.cs](<D:/Github/PickleVerse/My project/Assets/Tests/LocalMatch/SinglesMatchTests.cs>), [MultiplayerRulesTestResults.json](<D:/Github/PickleVerse/My project/Docs/MultiplayerRulesTestResults.json>), [MultiplayerBuildResult.json](<D:/Github/PickleVerse/My project/Docs/MultiplayerBuildResult.json>), [test summary motion](D:/Github/PickleVerse/UnityWork/SecondaryMotion/test-summary.json), [environment selection validation](D:/Github/PickleVerse/UnityWork/EnvironmentSelection/validation.json).

## 8. Các khoảng trống tích hợp cần ghi nhận

### 8.1 Tài khoản và backend chưa nối vào trận

Trận local không gọi backend; online dùng Unity Services nhưng chưa gọi ASP.NET để ghi kết quả/rating. Chưa có danh tính chung và kho dữ liệu nền cho tiến trình, lịch sử, AI học trình độ và website quản trị. Đây là thiếu sót xuyên suốt G01/G06/A02/W01/W03/W05, không chỉ thiếu một màn hình đăng nhập.

### 8.2 Huấn luyện bị đứt khỏi bản source hiện tại

Training thiếu script và TrainingUI thiếu file. Các mô tả coach trước đây không nên được đưa vào danh sách “đã hoàn thành” của bản hiện nay. Kể cả khôi phục được demo cũ, vẫn phải làm sao/XP/mở khóa và drill theo Word mới.

### 8.3 Local và online đang khác hành vi

Hai chế độ có rule engine, input parser, camera và playback animation riêng. Ví dụ:

| Khác biệt | BotMatch/local | Multiplayer hiện tại |
|---|---|---|
| Thể thức | Thắng 2/3 game; time-out và đổi sân | Kết thúc một game 11 cách 2; chưa thấy các cơ chế tương đương |
| Modifier | Phải còn giữ lúc chốt; tối đa hai nút | OneShot mặc định hoặc Hold; có quy tắc chord riêng |
| Cảm ứng | Có `TouchControl` và đường input local | `NetworkPlayer` hiện chỉ đọc keyboard |
| Camera | `MatchCamera` | `CourtCamera` với thông số/góc nhìn khác |
| Bóng chờ giao | Theo `leftHand.TransformPoint(heldBallOffset)` | `BeginRally` đặt bóng ở offset trước người; khi release mới lấy `LeftHand.position`, chưa thấy bám tay liên tục ở trạng thái chờ |
| Intro/sky | Có cinematic và lựa chọn ngày/đêm | Chưa thấy tích hợp các hệ thống tương ứng vào vòng online |

Những khác biệt này không tự động là lỗi của từng chế độ, nhưng phải được quyết định rõ khi nghiệm thu “một game” chung. Không thể coi một sửa chữa trong LocalMatch đã lan sang Multiplayer nếu chúng dùng đường code khác.

### 8.4 Duration asset và duration runtime online chưa thống nhất

Tài liệu duration và clip hiện có quy định Groundstroke/Smash 1,0 giây, Serve 1,5 giây, ThirdShotDrop 1,2 giây, Volley 0,6 giây. Tuy nhiên, `ShotAnimations.asset` online hiện giữ Groundstroke/Smash/Drop **0,8 giây**, Serve **1,3 giây**, Volley **0,55 giây**. `NetworkPlayer.Swing` tính `Animator.speed = clip.length / duration`; `NetworkMatch` cũng dùng duration và marker từ thư viện này để lên lịch contact.

Vì vậy, clip đã được retime đúng trên đĩa **chưa đồng nghĩa tốc độ và contact trong online trùng BotMatch hoặc trùng duration đã quy định**. Báo cáo calibration multiplayer còn ghi chiều dài nguồn khoảng 2,0417 giây, phản ánh phép đo trước thay đổi duration. Cần hồi quy/căn lại online nếu mục tiêu là hai chế độ chơi giống nhau; đợt khảo sát này chỉ ghi nhận, không chỉnh.

Nguồn: [duration report](D:/Github/PickleVerse/Doc/Animation_Duration_Update_Report.md), [ShotAnimations.asset](<D:/Github/PickleVerse/My project/Assets/Settings/Multiplayer/ShotAnimations.asset:16>), [NetworkPlayer.Swing](<D:/Github/PickleVerse/My project/Assets/Scripts/Multiplayer/NetworkPlayer.cs:177>), [NetworkMatch.TrySwing](<D:/Github/PickleVerse/My project/Assets/Scripts/Multiplayer/NetworkMatch.cs:126>), [calibration cũ](<D:/Github/PickleVerse/My project/Docs/MultiplayerAnimationCalibration.json>).

### 8.5 Chưa có bằng chứng hoàn thành đa nền tảng

Local có touch và project có URP mobile settings; online chưa có touch tương ứng. Chưa thấy bằng chứng nghiệm thu Android/iOS, layout theo safe area, hiệu năng trên máy thật, bộ nhớ texture/mesh và cảm giác input nhiều ngón. Cài package Input System hoặc có Mobile RPAsset chỉ tạo nền tảng, chưa chứng minh bản điện thoại đã hoàn thành.

### 8.6 Các báo cáo cũ có phạm vi và thời điểm riêng

Báo cáo BotMatch ban đầu ghi chưa có multiplayer, trong khi hiện nay multiplayer đã được thêm. Báo cáo OfflineCoach lại mô tả các asset không còn trong source hiện tại. Báo cáo multiplayer có kết quả trước các đợt chỉnh duration/model. Vì vậy, kết luận trong file này ưu tiên **source và asset hiện có**, sau đó dùng báo cáo cũ để hỗ trợ phần đã được kiểm tra và ghi rõ giới hạn.

## 9. Thứ tự hoàn thiện đề xuất

Đây là thứ tự đề xuất cho giai đoạn sau, không phải thay đổi đã thực hiện trong đợt khảo sát.

| Ưu tiên | Công việc | Các nhóm được mở đường |
|---|---|---|
| 1 | Chốt scene khởi động/menu và đường vào BotMatch/online/Training; xử lý tham chiếu Training thiếu và quyết định giữ hay thay demo cũ | G02, G03, X03; giúp có bản game thống nhất để nghiệm thu |
| 2 | Hoàn thiện API startup, tài khoản/danh tính chung, DB và lưu kết quả trận/drill | G01, G06, A02; nền dữ liệu cho website |
| 3 | Xây bốn drill tối thiểu, 1–3 sao, XP, mở khóa và lưu tiến trình | G03; tạo vòng học thật theo mục tiêu đề tài |
| 4 | Thu dữ liệu timing/vị trí/kỹ thuật, phản hồi có nguyên nhân và gợi ý drill | A03, A04, G06 |
| 5 | Behavior tree và thích ứng bot theo năng lực; tích hợp rating và ghép trận kỹ năng | A01, A02 |
| 6 | Hồi quy online sau retime/model; kiểm tra nhiều máy Internet, bổ sung touch và nghiệm thu mobile | G05 và yêu cầu PC/điện thoại xuyên suốt |
| 7 | Website quản trị người dùng/nội dung/trận/report/thống kê, kết nối dữ liệu thật | W01–W05 |
| 8 | Friend list, thách đấu, daily/streak; phần luyện tập offline hoàn chỉnh | X01–X03, sau phần cốt lõi |

Về khả năng chứng minh đề tài, ưu tiên quan trọng là nối được chuỗi **bài học/drill → kết quả kỹ thuật → tiến trình lưu lại → phản hồi → drill tiếp theo**, đồng thời giữ hai vòng thi đấu đã có. Phần hình ảnh và animation đã làm nhiều việc hỗ trợ trải nghiệm; chúng chưa thay thế các nhóm chức năng học tập, AI và quản trị còn thiếu trong Word.

## 10. Ghi nhận phạm vi báo cáo

File này là kết quả nghiên cứu hiện trạng, không phải biên bản nghiệm thu phát hành. Các mục “chưa có” dựa trên source/asset trong workspace được khảo sát; nếu có backend/frontend hoặc nhánh khác chưa pull vào đây, cần đối chiếu thêm trước khi kết luận cho toàn bộ hệ thống bên ngoài repository.

Trong đợt này chỉ tạo Markdown báo cáo; giữ nguyên tài liệu Word, file khóa Word, code, scene, model, backend và cấu hình dự án. Hash SHA-256 của tài liệu Word tại thời điểm đọc: `68e5ca85c3538e7cbe0867c7f3a95fdd1e5b430bfb170d5cf53aeebb63e6ffd3`.
