# Profile, thành tựu, nhiệm vụ và rank

Triển khai trên Menu hiện tại bằng uGUI/TMP. `NameTag` mở Hồ sơ, nút Thành tựu đổi nội dung, `BtnMissions` mở nhiệm vụ ngày/tuần, `BtnCareer` mở 20 trận gần nhất và tỷ số từng game. Coin/gem nằm dưới logo; nhãn ngày/đêm vẫn dùng ô Currency hiện có. Modal chặn chuột và điều hướng bàn phím ở Menu, đóng bằng Escape và theo safe area.

## Dữ liệu và cấu hình

- Assembly `Assets/Scripts/Progression/PickleVerse.Progression.asmdef` độc lập với UI/multiplayer. FrontEnd, LocalMatch và Multiplayer cùng gọi dịch vụ tiến trình.
- Chỉnh danh sách, mục tiêu, thưởng và XP trận trong Inspector của `Assets/Resources/Progression Config.asset`. Mặc định có đúng 10 thành tựu, 7 nhiệm vụ ngày, 10 nhiệm vụ tuần và hai thưởng tổng theo kế hoạch.
- JSON: `Application.persistentDataPath/LocalPlayers/<ID>.json`; bản dự phòng `<ID>.json.bak`. Trên máy kiểm thử: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/My project/LocalPlayers/`.
- Ghi vào file tạm, flush xuống đĩa rồi thay file nguyên tử. Lease `<ID>.lock` dùng `FileShare.None`; hệ điều hành giải phóng lease khi phiên kết thúc. `selection.lock` bảo vệ chọn danh tính đồng thời. `preferred.txt` lưu lựa chọn ID trên Menu.
- Player mới: tên 4 chữ cái, ID chữ/số tối đa 24 ký tự, Elo 100, level 1, XP/Coin/gem 0. Các phiên không có `-pkvProfile` chọn player đang rảnh hoặc tạo mới. `-pkvProfile <ID>` chọn rõ một player; ID đang có lease bị từ chối.
- Nút Chỉnh player chỉ hiện trong Editor/Development Build. Đổi ID giữ tên của dữ liệu đã có. Đổi tên trong phòng lưu cùng dữ liệu cục bộ. ID này chọn Unity Authentication profile; ID online của Authentication được quản lý riêng.
- Thời gian hoạt động dùng đồng hồ không phụ thuộc timeScale, không tính lúc mất focus/chạy nền; lưu mỗi 30 giây, khi mất focus, đổi player và thoát.

## Kết quả, XP và nhiệm vụ

Kết quả có ID duy nhất. Áp dụng kết quả và nhận thưởng thực hiện trong giao dịch lưu, có chống trùng qua các lần mở game. XP dư được giữ và phần thưởng có thể tăng nhiều level. Trận rank, bot thường, bot thay thế và tìm trận 2v2 tính tiến trình; phòng riêng chỉ có lịch sử/thời gian. Người bỏ trận nhận thua và Elo nhưng không nhận XP hoặc tiến trình nhiệm vụ/thành tựu. Trận gián đoạn chưa xác nhận không đổi Elo.

Điểm đội lấy từ thay đổi điểm do luật xác nhận; giao/trả hợp lệ chỉ ghi một lần khi quỹ đạo được luật chấp nhận. Lịch sử lưu UTC, hiển thị UTC+7. Trận chưa xong giữ tỷ số hiện tại và nhãn chưa hoàn tất.

Ngày reset 00:00 UTC, tuần reset 00:00 UTC thứ Hai. Tự nhận các phần thưởng đã đạt ở kỳ hết hạn, kể cả quay lại sau nhiều kỳ offline. Kết quả xác nhận sau reset vẫn được ghi vào kỳ chứa thời điểm kết thúc; kỳ cũ được giữ để chống mất/trùng thưởng. Chuỗi thắng Profile, chuỗi rank và chuỗi tuần được quản lý riêng.

`JsonUtility` biểu diễn trường object null bằng một đối tượng mặc định rỗng. Khi đọc/rollback, store chuẩn hóa pending match rỗng hoặc đã áp dụng thành null để không tạo trận gián đoạn giả.

## Ghép rank và bot

Protocol multiplayer là **5**; các máy cần cùng bản build mới. Cấu hình `Assets/Settings/Multiplayer/Cloud/PickleVerse_1v1.mmq` dùng timeout 360 giây, Equality vùng/protocol, Difference Elo 120 và Intersection khóa cặp. Bộ sinh cấu hình giữ các quy tắc này. Menu Editor `PickleVerse > Multiplayer > Deploy Ranked 1v1 Test Configuration` chỉ triển khai queue 1v1. Biên nhận triển khai thực tế vào `pickleverse-test` nằm ở `Docs/MultiplayerCloudDeployment.txt`.

**Điều chỉnh bắt buộc khi tích hợp dịch vụ:** [Unity giới hạn mảng custom data ở 100 phần tử](https://docs.unity.com/en-us/mps-sdk/matchmaking/mm-rate-limits), trong khi danh sách đầy đủ có thể có 241 khóa cặp. Vì vậy adapter chia khóa chuẩn hóa `min(E,O):max(E,O)` thành ba pha UTC, mỗi pha 10 giây, theo `(E+O)%3`. Mỗi ticket gửi tối đa 81 khóa; đủ cả khoảng ±120 trong mỗi vòng 30 giây. Hai phía cùng một cặp luôn dùng cùng pha. Việc hủy/tạo ticket không đặt lại đồng hồ tìm trận. Cách gửi một mảng 241 phần tử trong bản kế hoạch không được dịch vụ chấp nhận.

6 giây chờ đầu, đổi ưu tiên tại 30 giây, bot tại 360 giây tính từ nút Tìm trận. Gate chung bảo đảm chỉ một nhánh thắng; hủy ngăn bot và ticket cũ được dọn. Adapter giãn các yêu cầu tạo ticket tối thiểu 1,05 giây để tuân thủ giới hạn một yêu cầu/giây khi đổi ưu tiên trùng gần đầu pha UTC. 2v2 giữ luồng/thời hạn cũ, không đổi Elo hoặc tạo bot thay thế. Các quy tắc Difference/Intersection dựa trên [tài liệu Unity Matchmaker](https://docs.unity.com/en-us/mps-sdk/matchmaking/mm-rules).

Bot thay thế có ID và tên riêng, xuất hiện trong đội hình 1v1 trên **MultiplayerCourt**. Trận dùng cùng phiên kết nối, NetworkMatch/NetworkPlayer, nhân vật, luật, camera, timeout, kết quả và tổng kết như đấu người. Bot là nhân vật do host điều khiển; đội hình vẫn có hai người, chỉ một máy cần báo tải sân xong. Cờ Automated tách quyền sở hữu của host khỏi điều khiển bàn phím và camera người chơi. Không chuyển sang Scene BotMatch. Bot Elo E+120 hoặc E−100 sau ít nhất 5 trận thua rank; Elo bot có thể 0. Độ chính xác theo đúng công thức trong kế hoạch: 220→26%, 9000→99%, 9100→100%. Quyết định thất bại tạo cú đánh ra ngoài sân; bóng vẫn đi qua luật và mô phỏng quỹ đạo. Chế độ BotMatch chọn riêng vẫn dùng độ khó hiện có.

Khi trận multiplayer kết thúc, **MultiplayerSummary** tự mở toàn màn hình. Trang đọc kết quả đã áp dụng và lưu theo ID trận, hiển thị thắng/thua, tên đối thủ, Elo thực tế cộng/trừ, Elo trước/sau, rank hiện tại, tỷ số từng game và level/XP. Đang nhận kết quả thì hiển thị trạng thái xác nhận; không tính lại Elo bằng công thức trên UI. Trang chặn thao tác sân, theo safe area, có nút về phòng chờ và Menu. Hai phiên đấu người thật cũng dùng trang này.

## Kiểm chứng

- Đợt kiểm tra cập nhật: 44 kiểm thử Progression, 29 LocalMatch, 53 Multiplayer đều đạt, tổng 126. Gồm sáu trường hợp Elo, streak/rounding/floor, ranh giới rank, accuracy và seed cố định, giao/trả từ luật, reset/offline/late result, nhận trùng, lease/backup/reopen, 20 trận, giới hạn mảng dịch vụ, deadline 360 giây và đội hình bot chỉ áp dụng 1v1. `Tests.json` giữ kết quả đợt trước; báo cáo mới ở `SixMinuteValidation.json`.
- `RankedMultiplayerUI.json`: 25 kiểm tra đạt; bot vào MultiplayerCourt, tên/Elo và camera đúng, tổng kết tự mở, +60 Elo đưa player từ 1990 lên 2050/Bạch Kim, không cộng trùng, chuột bấm về phòng chờ; bố cục/glyph/safe area và chặn chuột phía sau ở bốn độ phân giải. Editor rút ngắn đồng hồ bằng gate để thử luồng; Windows smoke chờ đủ thời gian thật.
- `SixMinuteHuman.P6HUM20261011A/B.txt`: hai phiên Windows ghép người và hoàn tất trận 1–2 game. Tổng kết A −20 Elo/rank Vàng; B +20 Elo/rank Bạch Kim; cả hai về phòng thành công.
- `SixMinuteBot.P6BOT20261011A.txt` và `.history.json`: chờ đủ 360 giây, vào MultiplayerCourt với đối thủ tên AFXN/Elo 1120; hoàn tất trận 2–0, hai game 11–0 qua gameplay thật, bot có cú trả bóng thực tế. Player Elo 1000→1060, +10 XP, 22 điểm, 8 cú trả và 23 lần giao hợp lệ; tổng kết tự mở với Elo/rank đã lưu. Toàn bộ phiên gồm chờ và trận kéo dài khoảng 471,6 giây.
- `SixMinuteCancel.P6CAN20261011A.txt`: hủy ở khoảng giây 359; sau mốc 360 vẫn ở PlayMultiplayer/WaitingRoom, không có kết nối hoặc đội hình bot. Tổng hợp cả bốn phiên Windows và kiểm thử mới trong `SixMinuteValidation.json`.
- `UI-validation.json`: 113 kiểm tra đạt ở 1280×720, 1920×1080, 1024×768, 2560×1080; nhãn/glyph, bounds, mở/đóng, cuộn tới hàng cuối của thành tựu/ngày/tuần/lịch sử, nhận thưởng, dữ liệu cập nhật, Escape, chuột/bàn phím và thời gian focus/background. Kiểm tra NameTag gửi tín hiệu chuột qua Input System tại nền, avatar và tên ở cả bốn độ phân giải; xác nhận raycast tới nút và Profile thực sự mở. Image nền NameTag bật Raycast Target để toàn bộ vùng nhận cú bấm. Chạy bằng `ProgressionUiValidation.Run()` trong Play Menu; helper dùng player thử riêng rồi khôi phục ID ban đầu.
- `FinalNetwork.PRG20261011A/B.txt`: hai phiên độc lập ghép 1v1 và hoàn tất ba game bằng giao bóng gameplay. A thua, XP 5/Elo 100; B thắng, XP 10/Elo 120. Tỷ số mỗi game đúng ở cả hai phía. Mở lại xác nhận pending rỗng và không cộng kết quả/thưởng lần hai.
- Biên nhận `FinalBot.PRG20261011BOT.txt` và `FinalBot.PRG20261011CANCEL.txt` thuộc phiên bản trước, mốc 300 giây/chuyển Scene BotMatch. Luồng hiện tại dùng 360 giây và MultiplayerCourt.
- `Doubles.PRG20261011D1/D2/D3/D4.txt`: bốn phiên hoàn tất trận tìm 2v2, lịch sử mỗi player có đủ hai đối thủ và ba tỷ số game; đội thắng XP 10, đội thua XP 5, tất cả giữ Elo 100.
- `Forfeit.PRGFORFEIT1/2.txt`: người chủ động rời thua, Elo 500→480, XP 0, counter nhiệm vụ/thành tựu 0; đối thủ thắng, Elo 500→520, XP 10, lưu tỷ số game chưa hoàn tất.
- `ReconnectCrash.PRGCRASH1/2.txt`: reconnect giữ danh tính, quyền điều khiển và điểm số. Lưu snapshot tỷ số khi NetworkMatch despawn, giữ pending cho tới khi kết quả được xác nhận hoặc phân loại gián đoạn.
- `CrashFinal.PRGCRASH1/2.txt` và `NetworkValidation.json`: sau reconnect, tắt host đột ngột; client ghi gián đoạn với tỷ số game chưa hoàn tất, Elo 1000/XP 0 giữ nguyên. `CustomFinal.PRGCUSTOM1/2.txt` xác nhận phòng riêng chỉ ghi lịch sử, cả hai giữ Elo 100/XP 0 và thống kê trống.
- `Expiry.PRGEXP1/2.txt`: host vẫn hoạt động nhưng từ chối reconnect trong phiên smoke. Sau 30 giây, host xác nhận thắng, Elo 500→520/XP 10; client xác nhận thua do hết hạn, Elo 500→480/XP 0, không cộng counter nhiệm vụ/thành tựu. Callback thử được khôi phục khi kết thúc kiểm tra.
- `Build.json`: Windows Development tại `Builds/ProgressionWindows/PickleVerse.exe`, đã cập nhật NameTag, trang tổng kết và bot multiplayer sau 6 phút. Đợt sửa Console mới nhất thành công với 0 lỗi và 1 thông báo của package Pipeline rằng công cụ điều khiển Player build chưa được bật.

Đợt sửa Console (`ConsoleFixes.json`): 133 kiểm thử logic và 113 kiểm tra UI đạt. Runtime tiến trình không tạo singleton khi ngừng Play/thoát; callback mạng đọc tiến trình hiện có và kiểm tra lại sau `await`. Adapter xóa vé tuần tự với khoảng cách tối thiểu 1,1 giây, thử lại HTTP 429 có giới hạn và coi vé đã hết hạn là đã dọn xong; lỗi vĩnh viễn vẫn được báo. Giới hạn dịch vụ được đối chiếu với [Unity Matchmaker rate limits](https://docs.unity.com/en-us/mps-sdk/matchmaking/mm-rate-limits). `ConsoleMatchmaking.json` ghi lần thử tìm/hủy với dịch vụ thật.

43 component trên prefab Career/Collection giữ GUID và dữ liệu đã lưu, nay trỏ tới các component tương thích trong FrontEnd. Các font tiếng Việt có typeface đậm/nghiêng và glyph dấu “…”; shader TMP chuyển sang pragma debug hiện hành. Package Sentis không còn được mã/model hiện tại sử dụng đã được gỡ qua Package Manager. Warning build giảm từ 542 xuống 1; thông báo còn lại thuộc Pipeline, không bật thêm server điều khiển Player chỉ để loại thông báo này.

Ảnh giao diện ở `Assets/Docs/Progression/`. Smoke runner chỉ chạy khi truyền `-pkvCase`, không bật tự động chơi trong phiên bình thường. Các profile PRG20261011…/UIT… được dùng riêng cho nghiệm thu; không cộng dữ liệu thử vào player ban đầu.
