# BotMatch — scene đấu đơn với bot cục bộ

Scene: `My project/Assets/Scenes/BotMatch.unity`.

Bản này tạo một trận 1v1 có thể chơi trong Unity: nhân vật nữ do người chơi điều khiển, nhân vật nam do bot điều khiển. Bot chạy bằng C# trong project, có ba mức cố định. AI thích ứng và dịch vụ backend được để cho giai đoạn tiếp theo theo phạm vi đã thống nhất.

## Mở và chơi

1. Mở project `D:\Github\PickleVerse\My project` bằng Unity `6000.6.2f1`.
2. Mở `Assets/Scenes/BotMatch.unity`.
3. Bấm **Play**, click vào Game View và bấm **Enter / Tiếp tục**.
4. Chọn mức bot **Dễ / Vừa / Khó** ở góc trái. Khi mới thử, chọn Dễ.
5. Bấm **F1** để xem hướng dẫn; **Esc** để tạm dừng; **T** để gọi time-out trước giao bóng.

Scene đã được thêm vào Build Settings. Thứ tự các scene có sẵn được giữ; khi muốn build trực tiếp trận bot, chọn BotMatch làm scene khởi động trong Build Profile.

## Điều khiển

Đã sửa lỗi giao bóng trong PlayerLoop thật: gameplay lấy mẫu clip trực tiếp và áp dụng grip sau đó, tránh pass Animation tự động ghi đè cổ tay. Giữ rồi thả J hoặc nhấp J đều thực hiện serve; một tap hoàn tất giữa hai frame cũng được ghi nhận. Báo cáo tái hiện và kiểm tra người chơi/bot nằm trong `UnityWork/BotMatch/ServeCameraFix`.

| Phím | Hành động |
|---|---|
| WASD | Di chuyển; W luôn tiến về lưới, kể cả sau đổi sân |
| Shift + WASD | Chạy; hướng chéo được chuẩn hóa tốc độ |
| J | Drive / Groundstroke sau nảy |
| K | Dink sau nảy |
| L | Volley trước nảy |
| J + K | Drop; dùng ở cú thứ ba là third-shot drop |
| J + L | Smash đứng khi bóng cao |
| K + L | Reset volley mềm |
| J + U | Drive có lực |
| K + U | Speed-up sau nảy, dùng động tác Drive |
| L + U | Volley có lực |
| J/K/L + I | Biến thể chéo của cú gốc |
| Backspace | Hủy lựa chọn cú; rally vẫn tiếp tục |

**Giữ để chọn, thả phím gốc để swing.** Ví dụ: giữ J, thêm K, thả J để thực hiện Drop. Nhả K sau đó không tạo một cú thứ hai. Chỉ ghép tối đa hai nút; U/I đứng riêng không swing. Nếu thả modifier trước phím gốc, modifier đó không được áp dụng. Mất focus, tạm dừng hoặc kết thúc rally sẽ hủy lựa chọn đang giữ.

Khi giao bóng, dùng **J, J+U hoặc J+I**. Các lựa chọn khác không tạo một kiểu giao khác. J+K trong rally là Drop shot, không phải Drop serve.

Thả phím không đồng nghĩa với chạm bóng ngay. Độ trễ contact: Drive khoảng 0,433 s; Dink 0,367 s; Volley/Reset 0,233 s; Drop 0,533 s; Smash 0,5 s; Serve 0,8 s. Cần tới đúng vị trí và chuẩn bị swing trước khi bóng đến. Marker màu cam cho biết điểm nảy dự đoán; HUD có gợi ý timing cho Drive. Một swing trượt để bóng tiếp tục diễn ra.

Trên cảm ứng, dùng joystick trái; vành ngoài là chạy. Giữ/thả các nút bên phải; các nút JK/JL/KL cho phép chọn combo bằng một ngón. U/I là nút bật/tắt modifier. Nút **Cảm ứng** bật bộ điều khiển này khi thử trên desktop. Không có thao tác Jump.

Camera hiện thấp và gần nhân vật: cao 5,3 m, phía sau 4,21 m, FOV 60°. Camera theo vị trí người chơi và đổi phía khi đổi sân. Góc nghiêng 31° giữ đối thủ dưới vùng HUD phía trên.

## Thể thức và luật trong vòng trận

Trận dùng luật đấu đơn với side-out scoring, thắng hai trong ba game. Mỗi game đến 11 điểm và cách hai; thắng rally khi nhận giao chỉ đổi quyền giao. Ô giao chẵn/lẻ theo điểm bên giao. Hai bên đổi sân giữa các game và ở mốc 6 điểm trong game quyết định.

Các điều kiện được xét theo diễn biến bóng và tư thế tại contact: giao chéo, loại trừ Kitchen và vạch Kitchen khi giao, hạn 10 giây, chân giao sau baseline, động tác giao đi lên, đầu vợt dưới cổ tay và contact thấp. Return và cú trả đầu của bên giao phải nảy. Rally xử lý out, bóng không qua lưới, nảy hai lần, chạm người, chạm lưới của nhân vật và contact sai phía lưới. Bóng chạm dây trên lưới có thể tiếp tục qua sân.

Volley trong Kitchen hoặc chạm vạch là lỗi. Theo dõi đà đến hết follow-through và khi nhân vật lấy lại kiểm soát; lỗi đà có thể thay kết quả tạm thời sau bóng chết. Điểm được chốt sau bước này. Dink/Drive sau nảy trong Kitchen được phép. Mỗi bên có hai time-out, tối đa 60 giây; nghỉ giữa game tối đa 120 giây, có thể tiếp tục sớm.

Nguồn thiết kế chính là bốn tài liệu nội bộ: [luật](Luat_thi_dau_Pickleball.md), [nghiên cứu điều khiển](PickleVerse_Controls_Combo_Research.md), [phân tích project](PickleVerse_Doc_Analysis.md), [proposal](PickleVerse_Capstone_Proposal.md). Điều kiện volley serve và cách gọi điểm đấu đơn được đối chiếu với [USA Pickleball Official Rulebook 2026](https://usapickleball.org/docs/rules/USAP-Official-Rulebook.pdf). Số phần của bản dịch nội bộ và bản 2026 khác nhau.

## Model và animation

Sân lấy từ `Assets/Models/court/Court.glb`, giữ tỷ lệ mét; bóng, vợt và khán đài lấy từ các thư mục tương ứng trong `Assets/Models`. Hai nhân vật dùng rig 59 joint và socket `PKV_PaddleSocket_R` đã chỉnh tay. Vợt gắn vào socket, vùng contact đặt tại tâm phần đầu vợt theo tọa độ mesh.

Thư viện scene có 20 clip nữ, 19 clip nam: đầy đủ bộ gameplay PKV, thêm Dance Win của nữ. Có Idle, Walk, Run, Shuffle trái/phải, Backpedal, Split Step, Serve, FH/BH cho Drive/Dink/Volley/Drop, Smash, Momentum Recovery và Fault Reaction. Bộ nam không có asset Dance Win nên khi thắng dùng tư thế Ready và thông báo kết quả. Clip Jump không được đưa vào scene.

Một số clip nguồn có root translation và marker khác brief. Các bản `.anim` dùng trong scene được tạo từ dữ liệu chuyển động của GLB, giữ root X/Z tại vị trí Ready để controller sở hữu di chuyển. Contact được đo từ pose vợt và ánh xạ vào thời điểm gameplay; animation tiếp tục qua follow-through. Smash đứng có lớp pose tay trên animation để với bóng cao. Serve có căn cổ tay để đầu vợt thấp, và kiểm tra hướng đi lên cùng vị trí đầu vợt thực tế.

Các model dùng trong scene có bản texture 1K tại `Assets/LocalMatchContent/Models`; hình học, UV, rig và dữ liệu animation được giữ. Các GLB gốc trong `Assets/Models` được giữ nguyên. Scene có một Match Camera, một Match Sun và một EventSystem; camera/light của model nhập được loại khỏi instance.

## Code

| Thành phần trong `Assets/Scripts/LocalMatch` | Trách nhiệm |
|---|---|
| `Rules/SinglesRules.cs` | Luật, court geometry, rally, điểm/game, time-out; C# thuần, không phụ thuộc Unity |
| `BotMatch.cs` | Điều phối trận và kiểm tra contact, bóng, fault, chuyển trạng thái |
| `BallFlight.cs` | Quỹ đạo trọng lực, nảy bóng và tham số shot |
| `MatchActor.cs` | Di chuyển, animation, socket, pose và theo dõi đà |
| `LocalBot.cs` | Dự đoán intercept, chọn cú, footwork và reaction theo mức cố định |
| `ShotInput.cs` | Bộ nhập giữ–ghép–thả, chốt một cú cho mỗi thao tác |
| `MatchHud.cs`, `TouchControl.cs`, `MatchCamera.cs` | HUD uGUI, cảm ứng và camera theo phía người chơi |
| `Editor/BotMatchSceneBuilder.cs` | Tạo scene và asset qua API Unity Editor |
| `Editor/BotMatchDiagnostics.cs` | Kiểm thử scene trong Editor; không đi vào player build |

Người chơi và bot cùng đi qua `RequestShot`, animation, contact và Rules Engine. Bot không dịch chuyển tức thời tới bóng và không được tự ghi điểm. Bóng chạy với tick 120 Hz; quỹ đạo mềm được tính đủ độ cao qua lưới từ điểm contact. Backend không được gọi trong vòng trận này.

## Kiểm tra

Kết quả và bằng chứng nằm tại `UnityWork/BotMatch/QA`:

- `editmode-tests.json`: 29 tests pass, gồm side-out, deuce, hai lần nảy, Kitchen, đà sau dead ball, giao bóng, vạch, time-out, combo và đổi sân game quyết định.
- `contact-checks.json`: cả 12 tổ hợp đã tạo đúng một hit qua parser, animation và contact trong scene.
- `input-devices.json`: keyboard Input System giữ–ghép–thả, Backspace, Shift, tâm joystick, chạy ở vành ngoài, Drop và modifier cảm ứng. Trong phép đo 0,75 s, đi bộ khoảng 2,26 m và chạy khoảng 3,72 m, gồm giai đoạn tăng tốc.
- `scene-audit.json`: tham chiếu hợp lệ, rig 59 joint mỗi nhân vật, không Jump, một camera/light/EventSystem.
- `smoke-600s.json`: mô phỏng hai bên bằng cùng controller, kết thúc trận; log có rally, fault và điểm. Đây là kiểm thử tự động, không phải phép đo kỹ năng một người chơi thật.

## Phạm vi của bản đầu

Đây là vòng trận đấu đơn offline. Chưa có adaptive AI, Elo, multiplayer hay ghi kết quả backend. Điều khiển cảm ứng đã kiểm tra trong Editor; chưa xác nhận build và hiệu năng trên thiết bị Android/iOS thực.

Vật lý là mô hình trọng lực/nảy đơn giản; chưa có khí động học và spin đầy đủ. Contact có hỗ trợ quanh tâm vợt, mặc định bán kính 0,45 m; bóng hiển thị được phóng 1,5 lần để dễ đọc nhưng bán kính vật lý vẫn 0,037 m. Vị trí chân dùng proxy quanh bone Foot, còn giới hạn di chuyển là phần sân và runoff hiện tại. Các phần quản trị giải, khiếu nại trọng tài, chấn thương, thiết bị hỏng và thi đấu xe lăn không được mô phỏng. Có thể tạm dừng vì đây là trận luyện cục bộ.

## Dựng lại scene

Các asset đã lưu đủ để mở scene và chơi. Khi cần dựng lại từ model nguồn:

1. Từ root repository, chạy `UnityWork/BotMatch/extract_animation_data.py` để đọc chuyển động từ GLB.
2. Chạy `UnityWork/BotMatch/prepare_scene_models.py` để tạo bản model texture 1K.
3. Trong Unity, ở Edit Mode chọn **PickleVerse → Create or rebuild BotMatch scene**.

Builder giữ bản sao các scene đang có thay đổi chưa lưu trong `UnityWork/BotMatch/SceneBackups` trước khi mở scene mới. Các thư mục `ClipData` và `model-variants.json` phục vụ dựng lại; gameplay không cần đọc chúng khi chạy.
