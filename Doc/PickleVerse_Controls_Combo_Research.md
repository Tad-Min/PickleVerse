# PickleVerse nghiên cứu điều khiển và combo kỹ thuật đánh bóng

Ngày nghiên cứu: 06/10/2026. Phiên bản đề xuất: 0.1.

Tài liệu này đề xuất cơ chế điều khiển cho máy tính và điện thoại dựa trên bộ 20 animation của PickleVerse. Mục tiêu là giữ WASD để di chuyển, Shift để chạy, dùng J/K/L/U/I để chọn kỹ thuật, và tạo kỹ thuật khác bằng tổ hợp tối đa hai nút đánh bóng.

Đây là tài liệu thiết kế để thảo luận và kiểm chứng bằng thử nghiệm người chơi. Chưa có cơ chế nào trong tài liệu được lập trình, tích hợp hoặc kiểm thử trong game. Các mốc timing, khoảng cách và chỉ tiêu thử nghiệm là giả thuyết thiết kế, không phải thông số đã được xác nhận.

## 1. Phương án đề xuất

Chọn **ba cú đánh gốc trên J/K/L**, còn **U/I là nút bổ trợ**, không tự phát ra cú đánh:

| Phím | Vai trò | Ý nghĩa dễ nhớ |
|---|---|---|
| J | Groundstroke/Drive | Đánh bóng sau nảy, hướng tới trả sâu hoặc tấn công |
| K | Dink | Đánh mềm gần lưới, ưu tiên kiểm soát |
| L | Volley | Đánh bóng trước khi nảy, động tác chặn/đẩy ngắn |
| U | Bổ trợ tấn công | Tăng ý định đánh nhanh/mạnh trong giới hạn của kỹ thuật |
| I | Bổ trợ hướng chéo | Chọn phương án crosscourt thay cho hướng mặc định |

Ba tổ hợp giữa các cú gốc tạo ba lựa chọn chiến thuật rõ ràng:

| Tổ hợp | Kỹ thuật được yêu cầu | Ý nghĩa thiết kế |
|---|---|---|
| J + K | Drop; ở cú thứ ba là Third-shot drop | Đánh từ sâu trong sân nhưng muốn đưa bóng mềm vào NVZ đối phương |
| J + L | Overhead smash đứng | Chọn tấn công bóng cao bằng động tác trên đầu |
| K + L | Volley mềm/Reset volley | Chặn bóng trước nảy để giảm nhịp và đưa bóng ngắn trở lại |

Các tổ hợp với U/I mở rộng lực hoặc hướng của ba cú gốc. Mỗi cú đánh sử dụng tối đa **hai nút thuộc J/K/L/U/I**; phím di chuyển và Shift được dùng đồng thời như bình thường.

Trên điện thoại, dùng ba nút Drive/Dink/Volley và hai lựa chọn Tấn công/Chéo. Combo giữa các cú gốc được thực hiện bằng **chạm nút đầu, trượt sang nút thứ hai, nhấc ngón tay**. Người chơi không phải đặt hai ngón tay lên hai nút đánh trong lúc ngón còn lại điều khiển di chuyển.

“Combo” ở đây là cách chọn **một cú đánh**, không phải đánh bóng nhiều lần liên tiếp. J + K không phát Groundstroke rồi phát Dink; nó chọn thẳng một động tác Drop. Đây là quy ước điều khiển của game, không phải khẳng định các kỹ thuật pickleball ngoài đời được tạo bằng cách cộng hai kỹ thuật khác nhau.

## 2. Căn cứ và giới hạn nghiên cứu

Nguồn nội bộ chính là [PickleVerse_20_Animation_Production_Brief.md](./PickleVerse_20_Animation_Production_Brief.md), đặc biệt danh mục Action, mô tả chuyển động, bảng tái sử dụng, coverage luật, timing và input đề xuất. [Proposal](./PickleVerse_Capstone_Proposal.md) cung cấp phạm vi desktop/mobile, drills, telemetry và authoritative multiplayer; [bản dịch luật](./Luat_thi_dau_Pickleball.md) cung cấp bối cảnh nghiệp vụ.

Nghiên cứu bổ sung đối chiếu nguồn của USA Pickleball, Apple, Google và Microsoft. Các nguồn hỗ trợ định nghĩa kỹ thuật, luật và nguyên tắc thao tác; **không có nguồn nào quy định PickleVerse phải dùng J/K/L hay một combo cụ thể**. Bảng phím và các đánh đổi trong tài liệu là đề xuất cho dự án.

Brief hiện tại dùng J cho Groundstroke/Serve, K cho Dink hoặc Drop theo vị trí, L cho Volley và U cho Smash. Đề xuất này thay đổi hai điểm:

1. Drop chuyển sang J + K để người chơi chủ động chọn, thay vì K tự đổi thành Drop theo vị trí.
2. Smash chuyển sang J + L; U trở thành nút bổ trợ tấn công và bổ sung I để chọn hướng chéo.

Đây là phương án nghiên cứu mới. Brief gốc không bị sửa và chưa có quyết định triển khai thay thế.

### 2.1 Hiểu đúng ba cú đánh gốc

Ba cú gốc là **ba nhóm lựa chọn đầu vào**, không phải toàn bộ động tác cơ thể của môn thể thao. Serve, Drop và Smash vẫn có animation riêng. Forehand/backhand là hai cách thể hiện của một kỹ thuật, không cần hai nút riêng.

Trong thuật ngữ pickleball, Groundstroke là đánh sau nảy; Volley là đánh trước nảy; Dink và Drop đều có mục tiêu mềm nhưng khác bối cảnh gần lưới/sâu trong sân. Vì vậy, tên nút phải thể hiện cả kỹ thuật và tình huống phù hợp. Nguồn đối chiếu: [USA Pickleball Basic Terms](https://usapickleball.org/blog-category/pickleball-basic-terms/).

## 3. Đối chiếu đầy đủ 20 animation

| # | Action trong brief | Cách kích hoạt hoặc tái dùng trong phương án này |
|---|---|---|
| 01 | `PKV_Idle_Ready` | Tự phát khi chờ/đứng ổn định; cũng là tư thế chờ chọn cú |
| 02 | `PKV_Walk_Forward` | Di chuyển tốc độ thường; bước vào/ra Kitchen hợp lệ |
| 03 | `PKV_Run_Forward` | WASD + Shift hoặc joystick vùng chạy |
| 04 | `PKV_Jump` | Hành động phụ trợ tùy chọn; chưa cần bật trong điều khiển thi đấu mặc định |
| 05 | `PKV_Overhead_Smash` | J + L khi bóng cao và trong tầm với; ưu tiên smash đứng |
| 06 | `PKV_Serve_FH_Underhand` | J trong trạng thái giao bóng; J + U/J + I thay thông số giao bóng |
| 07 | `PKV_Groundstroke_FH` | J phía thuận tay; tái dùng cho Drive, Return, Third-shot drive và biến thể phù hợp |
| 08 | `PKV_Groundstroke_BH` | Như 07 nhưng phía trái tay; vẫn cùng tay cầm paddle |
| 09 | `PKV_Dink_FH` | K hoặc K + I phía thuận tay |
| 10 | `PKV_Dink_BH` | K hoặc K + I phía trái tay |
| 11 | `PKV_Volley_FH` | L, L + U, L + I; ứng viên tái dùng cho K + L ở phía thuận tay |
| 12 | `PKV_Volley_BH` | Như 11 nhưng phía trái tay |
| 13 | `PKV_ThirdShotDrop_FH` | J + K phía thuận tay; có thể dùng cho Drop ở cú sau |
| 14 | `PKV_ThirdShotDrop_BH` | J + K phía trái tay |
| 15 | `PKV_Shuffle_Left` | Di chuyển ngang trái ở nhịp phù hợp, giữ thân hướng sân |
| 16 | `PKV_Shuffle_Right` | Di chuyển ngang phải ở nhịp phù hợp |
| 17 | `PKV_Backpedal` | Lùi ngắn có kiểm soát; bóng quá sâu cần quay/chạy theo controller |
| 18 | `PKV_SplitStep` | Phản ứng phụ trợ theo nhịp đối thủ đánh; không cần nút riêng |
| 19 | `PKV_NVZ_MomentumRecovery` | Phục hồi thăng bằng sau volley khi phù hợp; không xóa lỗi momentum |
| 20 | `PKV_Fault_Reaction` | Tự phát sau khi kết quả fault đã được xác định |

Bộ này có **10 clip đánh bóng: 05–14**. Mười clip còn lại phục vụ chờ, di chuyển, nhảy, footwork hoặc phản ứng. Không phải mọi clip trong nhóm còn lại đều “bị động”: Jump có thể là hành động chủ động nếu được bật.

Brief **không có Action Win/Celebration**. Nếu cần animation thắng/thua riêng, phải nghiên cứu bổ sung hoặc thay thế clip khác; không xem như đã có trong bộ 20.

### 3.1 Những cách tái dùng chắc chắn và những cách cần kiểm tra

Brief đã cho phép Groundstroke dùng cho Return/Drive, Dink đổi hướng crosscourt/straight, Drop đổi target, Serve đổi lực/hướng và Volley dùng gần Kitchen. Các biến thể hướng/lực ở mức vừa phải phù hợp với định hướng này.

**Reset volley và Speed-up là phần mở rộng được đề xuất ở đây.** Chưa thể xác nhận hai kiểu đó đọc đúng bằng clip hiện có chỉ từ tên Action:

- K + L cần thử xem Volley FH/BH có thể hiện được động tác hấp thụ lực, chặn mềm và phục hồi gọn không.
- K + U cần thử xem Groundstroke FH/BH có đủ gọn để biểu diễn cú tấn công sau nảy ở gần lưới không.
- L + U cần kiểm tra clip Volley có thể hiện cú đẩy mạnh hơn mà vẫn giữ backswing ngắn không.

Nếu pose và đường paddle không phù hợp, tạm bỏ biến thể khỏi bản đầu hoặc điều chỉnh asset trong một nhiệm vụ được giao riêng. Không tăng tốc clip cực đoan hoặc gán tên mới để coi như đã có chuyển động đúng.

## 4. Vì sao chọn phương án này

| Phương án | Ưu điểm | Vấn đề với yêu cầu hiện tại | Đánh giá |
|---|---|---|---|
| Mỗi kỹ thuật có một nút riêng | Trực tiếp, dễ giải thích lúc đầu | Khó giữ đúng ba cú gốc; số nút trên mobile tăng theo kỹ thuật | Có thể dùng làm tùy chọn hỗ trợ |
| Một nút đánh, game tự chọn kỹ thuật | Ít nút, thao tác đơn giản | Người chơi khó chủ động chọn Dink/Drop/Volley; ít thể hiện quyết định chiến thuật | Phù hợp chế độ làm quen riêng |
| Ba cú gốc + combo hai nút + U/I bổ trợ | Giữ ba lựa chọn cơ bản, có chiều sâu và tái dùng animation | Cần giải quyết timing, nhận diện combo và thao tác mobile | Phương án chính để thử nghiệm |

Ba cú gốc giúp người mới phân biệt **sau nảy – mềm gần lưới – trước nảy**. Ba combo chính bổ sung **tiếp cận lưới – kết thúc bóng cao – phòng thủ giảm nhịp**. U/I thêm biến thể chiến thuật mà không yêu cầu thêm sáu animation riêng.

Không đưa chuỗi ba hoặc bốn nút vào bản đầu. Độ khó nên đến từ vị trí, lựa chọn kỹ thuật và timing, thay vì ghi nhớ một chuỗi dài trong khi bóng đang bay.

## 5. Điều khiển máy tính

### 5.1 Di chuyển

| Input | Hành vi đề xuất |
|---|---|
| W | Tiến về phía lưới ở bên sân của người chơi |
| S | Lùi khỏi lưới |
| A/D | Di chuyển ngang trái/phải theo góc nhìn gameplay |
| Hai hướng WASD cùng lúc | Di chuyển chéo; tốc độ tổng không cao hơn di chuyển thẳng |
| Giữ Shift + WASD | Chạy khi di chuyển, phù hợp với khoảng cách và trạng thái nhân vật |
| Không có hướng di chuyển | Về Ready khi phù hợp |

WASD luôn điều khiển di chuyển, không đổi thành aim khi giữ nút đánh. Sau đổi bên sân, W vẫn có ý nghĩa tiến về lưới; tránh bắt người chơi đảo trí nhớ điều khiển vì nhân vật đổi phía.

Shuffle được dùng cho di chuyển ngang ở tốc độ thấp/vừa gần lưới; Backpedal cho lùi ngắn. Khi chạy xa, nhân vật cần định hướng thân/chân phù hợp với hướng chạy. Không thể khẳng định một clip Run Forward tạo được mọi hướng chân chỉ bằng đổi tên state.

Shift chỉ là chạy, không tự tăng lực bóng, đổi kỹ thuật hoặc phát Jump. Nhảy không bắt buộc để Smash. Nếu bật Jump sau này, Space là ứng viên nút riêng, nằm ngoài bộ nút đánh J/K/L/U/I.

### 5.2 Ba cú gốc

| Nút | Ý định | Animation | Thời điểm hợp lý | Điểm người chơi phải học |
|---|---|---|---|---|
| J | Groundstroke/Drive | 07/08 | Bóng đã nảy, có khoảng trống để vung | Trả sâu, điều chỉnh vị trí và đánh trước thân |
| K | Dink | 09/10 | Bóng đã nảy, giao tranh mềm gần NVZ | Giữ bóng thấp, lực vừa đủ, không dựng bóng cho đối thủ |
| L | Volley | 11/12 | Bóng chưa nảy; có thể tiếp xúc trong tầm | Đọc bóng nhanh, chặn ngắn, kiểm soát chân và đà |

J không có nghĩa là “mạnh tối đa”; một cú Return có kiểm soát cũng dùng J. K không tự chuyển thành Drop khi đứng xa. L không tự chuyển thành Dink nếu bóng đã nảy trước contact.

Trong thiết kế đầu vào này, J/K/J + K yêu cầu contact sau nảy, L và các biến thể Volley yêu cầu contact trước nảy. Nếu pha bóng thay đổi làm yêu cầu đó không còn đúng tại contact, cú có thể trượt/không kết nối và cần phản hồi rõ. Đây là quy tắc chọn kỹ thuật của game, không phải một lỗi pickleball mới.

### 5.3 Vai trò của U và I

**U — Tấn công:** thay đổi ý định lực/tốc độ của một cú gốc. Hiệu quả vẫn phụ thuộc bóng đến, chiều cao contact, tư thế, timing và giới hạn của cú đánh. U không tạo một kỹ thuật thần kỳ hoặc bảo đảm thắng điểm.

**I — Chéo:** chọn target crosscourt trong vùng phù hợp của kỹ thuật. Đây là thay đổi hướng, không tạo xoáy hay làm bóng tự bẻ cong. Hướng chéo vẫn có thể ra ngoài nếu contact/timing không tốt.

U/I đứng riêng chỉ có vai trò bổ trợ; không phát swing. Muốn Smash phải dùng J + L, không dùng U một mình như sơ đồ cũ trong brief.

### 5.4 Sự thuận tay của từng tổ hợp

Trong cách đặt tay phải thường dùng, ngón trỏ ở J, ngón giữa ở K và ngón áp út ở L. U nằm phía trên J, I nằm phía trên K. Vì vậy, dùng đủ năm phím không có nghĩa mọi cặp đều thuận tay như nhau:

| Cặp | Điểm cần quan sát khi thử |
|---|---|
| J + K, K + L | Hai ngón liền nhau, dễ giữ cùng lúc; cần tránh nhả tạo cú đơn ngoài ý muốn |
| J + L | Ngón trỏ và áp út; cần kiểm tra có vô tình bấm K ở giữa không |
| J + U, K + I | Hai phím cùng cột vốn thường do một ngón phụ trách; phải đổi cách đặt ngón hoặc dùng modifier một lần |
| K + U, L + I | Dùng hai ngón ở hai hàng; cần thử mức xoay/di chuyển bàn tay |
| L + U, J + I | Khoảng với chéo hơn; cần thử với bàn tay nhỏ và bàn phím khác nhau |

Đây là phân tích hình học bố cục phím, chưa phải kết quả đo mức thoải mái. Với J + U và K + I, **không nên bắt người chơi duy trì cách đặt ba ngón trên J/K/L rồi cố giữ hai phím bằng cùng một ngón**.

Đề xuất cho phép PC chọn chế độ modifier một lần: bấm U hoặc I để chọn, rồi bấm–thả một cú gốc để thực hiện; bỏ modifier sau một thao tác như mobile. Chế độ này vẫn là tổ hợp hai lựa chọn và dùng đúng năm phím, nhưng không cần giữ cặp cùng cột. UI phải cho biết đang dùng chế độ giữ modifier hay modifier một lần; không diễn giải đồng thời hai chế độ cho cùng thao tác.

Ba combo giữa J/K/L giữ cơ chế hai phím chồng thời gian. U/I cần được so sánh giữa hai chế độ giữ và một lần trong thử nghiệm; nếu phần lớn người chơi khó giữ cặp cùng cột, ưu tiên modifier một lần làm mặc định. Không xem việc đặt U/I ở hàng trên là quyết định đã tối ưu.

## 6. Danh mục combo đầy đủ

Trong trạng thái rally, có ba nút đơn và chín tổ hợp hợp lệ. Thứ tự trong một tổ hợp không đổi kết quả: J + K và K + J cùng yêu cầu Drop.

| Tổ hợp | Kết quả yêu cầu | Clip ứng viên | Điều kiện contact | Tác dụng chiến thuật |
|---|---|---|---|---|
| J + K | Drop | 13/14 | Sau nảy | Đưa bóng mềm từ sâu trong sân vào gần lưới |
| J + L | Overhead smash đứng | 05 | Bóng cao, trong tầm; xét thực tế trước/sau nảy | Tấn công bóng cao có cơ hội |
| K + L | Reset volley | 11/12, cần kiểm tra pose | Trước nảy | Hấp thụ nhịp bóng nhanh, trả mềm vào vùng ngắn |
| J + U | Drive tấn công | 07/08 | Sau nảy | Gây áp lực bằng bóng nhanh hơn |
| K + U | Speed-up sau nảy gần lưới | 07/08, cần kiểm tra độ gọn | Sau nảy, có cơ hội tấn công | Chuyển từ giao tranh mềm sang bóng nhanh |
| L + U | Punch/Volley tấn công | 11/12, cần kiểm tra biến thể | Trước nảy | Đẩy bóng nhanh vào khoảng trống |
| J + I | Groundstroke/Drive chéo | 07/08 | Sau nảy | Mở góc, điều đối thủ ngang sân |
| K + I | Dink chéo | 09/10 | Sau nảy | Giao tranh mềm theo đường chéo |
| L + I | Volley chéo | 11/12 | Trước nảy | Chặn bóng về hướng chéo |

U + I không hợp lệ vì không có cú gốc. Tổ hợp ba nút đánh trở lên không được hỗ trợ. J + K + I cũng không được hỗ trợ, dù mong muốn Drop chéo là hợp lý; chỉ nghiên cứu mở rộng sau khi bản hai nút đã dễ dùng.

### 6.1 J + K — Drop

Người chơi dùng J khi muốn đánh sau nảy và thêm K để chọn chủ đích mềm, đưa bóng ngắn vào NVZ đối phương. Game chọn trực tiếp clip 13/14; không ghép hai clip Groundstroke và Dink nối tiếp.

Chỉ gọi **Third-shot drop** nếu cú đó thực sự là cú thứ ba của rally, theo thứ tự Serve → Return → cú tiếp theo của bên Serve. Drop ở cú thứ năm hoặc thứ bảy vẫn là Drop. Bộ nút không ép mọi cú thứ ba phải là Drop: người chơi có thể chọn J để Third-shot drive.

Drop quá cao có thể trở thành cơ hội tấn công của đối phương; quá thấp có thể mắc lưới; quá sâu không đạt mục tiêu tiếp cận lưới. Bấm đúng combo chưa chứng minh đã thực hiện kỹ thuật tốt.

### 6.2 J + L — Overhead smash

J + L là quy ước chọn động tác tấn công trên đầu. Nó không phải thao tác Drive rồi Volley. Dùng clip 05 và giữ cùng tay thuận của nhân vật.

Cần bóng ở vùng overhead và nhân vật đứng đúng khoảng cách. Bóng cao nhưng ở phía sau quá xa vẫn có thể không đánh được. Nếu Smash tiếp xúc trước khi bóng nảy, phải áp dụng các luật Volley/NVZ như mọi cú đánh trên không khác. Nếu bóng nảy rồi mới được Smash, xét như cú đánh sau nảy.

Ưu tiên Smash đứng. Không tự phát Jump khi nhận J + L; brief chưa có clip Jumping Smash hoàn chỉnh.

### 6.3 K + L — Reset volley

K biểu thị ý định mềm, L biểu thị đánh trước nảy. Tổ hợp này yêu cầu chặn mềm để giảm tốc bóng đến. Nó bổ sung lựa chọn phòng thủ, giúp L + U không trở thành đáp án cho mọi tình huống.

Reset ngoài đời có nhiều bối cảnh; bản đầu giới hạn nút này ở **Reset volley trước nảy** để tránh một tổ hợp tự thay đổi giữa ba kiểu kỹ thuật. Reset sau nảy hoặc half-volley có thể nghiên cứu riêng.

Tái dùng clip Volley chỉ là ứng viên. Cần motion ngắn, thể hiện hấp thụ lực thay vì đẩy mạnh; animation và trajectory phải đồng ý với nhau. Nguồn kỹ thuật đối chiếu: [USA Pickleball về Reset](https://usapickleball.org/pickleball-skills/level-three/what-is-a-pickleball-reset-shot-and-how-to-hit-it/), [cân bằng và kiểm soát Reset](https://usapickleball.org/pickleball-skills/level-one/pickleball-reset-technique-to-keep-the-ball-down/).

### 6.4 J/K/L + U — Ba cách tấn công

- **J + U:** Drive sau nảy với ý định tốc độ cao hơn cú J thường.
- **K + U:** Speed-up sau nảy trong giao tranh gần lưới. Chuyển sang clip Groundstroke phù hợp, không giữ nguyên clip Dink mềm rồi chỉ làm bóng bay nhanh bất thường.
- **L + U:** Volley có lực đẩy mạnh hơn; vẫn dùng động tác ngắn, không biến thành cú vung dài như Groundstroke.

K + U không phải “Dink mạnh” theo nghĩa vẫn là Dink; khi chuyển sang đánh nhanh, loại cú đánh thực hiện phải được ghi nhận là Speed-up/Drive tương ứng. Nếu bóng đến thấp, người chơi chọn U có nguy cơ dựng bóng hoặc đánh hỏng; không có bonus vô điều kiện.

### 6.5 J/K/L + I — Biến thể hướng chéo

Cùng kỹ thuật gốc, cùng yêu cầu bóng nảy và cùng clip phù hợp, nhưng target chuyển sang đường chéo. Không coi đây là ba motion mới, cũng không gọi mọi cú chéo là một kỹ thuật độc lập.

I giúp tạo lựa chọn hướng trong bố cục bàn phím mà tay phải đang ở J/K/L/U/I. Người chơi không phải liên tục chuyển tay từ nhóm phím này sang chuột hoặc cụm mũi tên để chọn hướng cơ bản.

## 7. Combo được nhận như thế nào

### 7.1 Vấn đề cần giải quyết

Nếu game phát J ngay khi nhấn xuống, đến lúc K được nhấn thì Groundstroke có thể đã bắt đầu. Muốn đổi thành Drop sẽ phải ngắt hoặc thay cú đang diễn ra. Nếu game luôn đợi một cửa sổ nhận combo sau mỗi lần nhấn, Volley đơn sẽ chịu thêm độ trễ cố định.

Đề xuất chính là **chọn trong lúc giữ, thực hiện khi thả**. Người chơi có thể bấm nhanh một nút cho cú đơn hoặc giữ ngắn để ghép thêm một nút. Không yêu cầu phải nhấn hai phím ở đúng cùng một mili giây.

### 7.2 Cú đơn

1. Nhấn J: hiển thị lựa chọn Groundstroke đang chuẩn bị.
2. Có thể tiếp tục di chuyển và quan sát bóng.
3. Thả J: chốt ý định Groundstroke và bắt đầu chu kỳ swing nếu nhân vật đang sẵn sàng.

K/L dùng cùng quy tắc. Giữ lâu không tự tăng lực và không tạo nhiều cú đánh. UI trạng thái chọn phải xuất hiện nhanh, nhưng chưa được tuyên bố bóng đã chạm paddle.

### 7.3 Combo hai cú gốc

Ví dụ J + K:

1. Nhấn và giữ J; nhân vật đang chọn cú, chưa chốt swing.
2. Khi J vẫn giữ, nhấn K. Lựa chọn đổi thành Drop và hai nút được đánh dấu.
3. Thả **một trong hai phím gốc**: chốt Drop một lần.
4. Phím còn lại không được phát thêm cú K/J khi nhả sau đó. Phải nhả hết các phím của thao tác cũ trước khi bắt đầu thao tác mới.

J + L và K + L tương tự. Phím được nhấn trước không quyết định FH/BH. Sau khi đã ghép hai cú gốc, không thể đổi sang cú gốc thứ ba trong cùng thao tác.

### 7.4 Combo có U/I

Ví dụ J + U:

1. Giữ U rồi nhấn J, hoặc giữ J rồi thêm U.
2. Khi J vẫn đang giữ, UI hiển thị Drive tấn công.
3. Thả J khi U vẫn đang giữ để chốt J + U.
4. Nếu thả U trước J, trở về lựa chọn J thường; thả J sau đó thực hiện cú thường.

I dùng quy tắc tương tự. U/I không tự chốt swing khi nhả vì chúng là nút bổ trợ. UI luôn biểu thị tổ hợp hiện tại, tránh việc người chơi tưởng đang chọn cú thường nhưng game dùng một modifier cũ.

### 7.5 Tổ hợp sai và hủy lựa chọn

| Trường hợp | Phản hồi đề xuất |
|---|---|
| U hoặc I đứng riêng | Chỉ biểu thị nút bổ trợ, không swing |
| U + I | Báo không có cú gốc; không swing |
| Ba nút đánh trở lên trước khi chốt | Hủy lựa chọn hiện tại, báo “Chỉ ghép tối đa hai nút”; phải nhả hết để thử lại |
| Nhấn J, thả J, rồi nhấn K | Hai thao tác riêng; không truy hồi thành Drop |
| Nhấn nhiều lần khi đang swing | Không tạo thêm hit; báo chưa sẵn sàng nếu cần |
| Mất focus, mất touch, ngắt thiết bị hoặc rally kết thúc | Hủy lựa chọn chưa chốt; không phát cú đánh lúc focus quay lại |
| Muốn hủy lựa chọn bằng tay | Backspace trên PC hoặc kéo ra vùng Hủy trên mobile; không dừng rally |

Một lỗi thao tác tổ hợp không tự làm mất điểm. Bóng vẫn diễn ra; người chơi có thể mất cơ hội trả bóng nếu chọn chậm. Điểm/fault xuất phát từ diễn biến gameplay và luật, không phải từ việc gõ sai combo.

### 7.6 Có cần giới hạn vài chục mili giây giữa hai phím không

Bản đề xuất không đặt một cửa sổ bắt buộc 80–150 ms cho việc ghép. Có thể chuẩn bị trong lúc bóng đang bay, thêm nút thứ hai rồi thả đúng thời điểm dự định. Chỉ được ghép khi thao tác đầu chưa chốt và phím gốc vẫn giữ.

Tốc độ chọn combo vẫn có giá trị vì bóng không dừng chờ. Tuy nhiên, người chơi không cần vượt qua một bài kiểm tra tốc độ ngón tay để truy cập Drop hoặc Reset. Sau thử nghiệm, nếu giữ–thả làm timing khó hơn đáng kể, so sánh với tùy chọn chọn kỹ thuật trước rồi bấm thực hiện; không mặc định phương án này đã tối ưu.

## 8. Hướng đánh và độ sâu khi không dùng chuột

### 8.1 Aim theo vùng mục tiêu

Để bộ phím yêu cầu có thể dùng độc lập, đề xuất **aim hỗ trợ theo vùng**, thay vì điều khiển một điểm liên tục bằng chuột:

| Nhóm cú | Vùng mục tiêu mặc định | Khi thêm I |
|---|---|---|
| Groundstroke/Drive | Vùng sâu, theo làn đánh từ vị trí contact | Vùng sâu phía chéo |
| Dink | Vùng ngắn trong NVZ đối phương | NVZ phía chéo |
| Volley | Vùng giữa/sâu theo làn đánh | Vùng giữa/sâu phía chéo |
| Drop | Vùng ngắn ở NVZ đối phương | Chưa có modifier I trong bản tối đa hai nút |
| Reset volley | Vùng ngắn ở NVZ đối phương | Chưa có modifier I trong bản tối đa hai nút |
| Smash | Vùng tấn công theo làn đánh với khoảng an toàn | Chưa có modifier I trong bản tối đa hai nút |

“Theo làn đánh” nghĩa là target theo hướng dọc sân từ vị trí chạm bóng, không phải luôn vào chính giữa sân. “Chéo” là hướng từ một nửa sân sang nửa đối diện bên đối thủ; không tính theo tay thuận của nhân vật.

Khi contact gần đường giữa sân, dùng vùng trung tâm có biên an toàn; không ép một cú chéo cực gắt do dấu trái/phải thay đổi rất nhỏ. Khoảng trung tâm cụ thể cần thử nghiệm. Đối với Serve, target luôn phải nằm ở ô giao chéo hợp lệ, bất kể quy tắc làn đánh của rally.

### 8.2 Aim hỗ trợ không bảo đảm kết quả

UI thể hiện **vùng muốn đánh tới**, không phải lời hứa bóng chắc chắn rơi đúng. Contact lệch, timing kém, bóng đến khó hoặc tư thế mất cân bằng có thể khiến kết quả khác target.

Không tự theo dõi đối thủ để tìm mọi khoảng trống tốt nhất. Người chơi phải chọn vị trí, cú thường/chéo, đánh mềm/tấn công và nhịp di chuyển. Đây là mức đơn giản hóa có chủ đích để phù hợp bàn phím và màn hình cảm ứng.

Nhược điểm là chưa có aim tự do đến mọi điểm sân. Mouse aim hoặc aim bằng gesture có thể là phương án mở rộng, nhưng cần thiết kế tương đương trên mobile trước khi dùng cho thi đấu chung.

## 9. Giao bóng và các pha đầu rally

### 9.1 Giao bóng dùng J theo trạng thái

Serve là trạng thái đặc biệt đã được brief dự kiến. Khi người chơi có quyền giao và đang chuẩn bị Serve:

| Input | Kết quả đề xuất |
|---|---|
| J | Underhand volley serve cơ bản; target giữa ô giao chéo |
| J + U | Serve có ý định lực/tốc độ cao hơn trong giới hạn hợp lệ |
| J + I | Serve rộng hơn trong cùng ô giao chéo hợp lệ |
| K, L hoặc các combo khác | Báo không phù hợp trạng thái Serve; không đổi thành kiểu giao khác |

Cả ba lựa chọn hợp lệ dùng clip 06, giữ đúng động tác thấp lên cao, release và contact. Đây là tái dùng của một kiểu Serve, không tạo thêm hai animation.

J + K trong trạng thái này **không phải Drop serve**. Drop serve là một phương pháp giao có bóng nảy trước contact, khác Drop shot trong rally; bộ 20 chưa có một clip Drop serve riêng để xác nhận coverage.

Giữ phím chuẩn bị không làm dừng timer giao bóng. Nếu luật dùng mốc 10 giây, thời hạn tính tới contact Serve thực tế, không phải chỉ tới lúc thả J. Với clip contact sau 800 ms kể từ đầu clip, bấm lúc gần hết giờ vẫn có thể quá hạn.

### 9.2 Return of serve

Ưu tiên J sau khi bóng Serve nảy, dùng Groundstroke 07/08 để trả sâu có kiểm soát. J + I là lựa chọn trả chéo. Không ép Return phải dùng U; tốc độ cao không phải luôn là mục tiêu tốt nhất.

Người chơi có thể chọn kỹ thuật khác nếu cơ chế cho phép và đáp ứng điều kiện của cú đó, nhưng nội dung coaching cần giải thích tác dụng chiến thuật. L hoặc Smash trước khi Serve nảy không thể vượt Two-bounce rule.

### 9.3 Cú thứ ba

Sau Return, bên Serve phải để bóng nảy trước cú trả đầu tiên của mình. Có hai lựa chọn rõ:

- J hoặc J + U: Third-shot drive, dùng Groundstroke.
- J + K: Third-shot drop, dùng Drop.

Nếu chọn Drop, tiến lên bằng WASD/Shift sau cú đánh theo tình huống. Combo không tự dịch chuyển nhân vật lên NVZ và animation Drop không chứa sẵn một pha chạy lên lưới.

## 10. Timing và cơ chế đánh bóng

### 10.1 Phải phân biệt bốn thời điểm

1. **Chọn cú:** nút đang được giữ/chạm; còn có thể ghép hoặc hủy.
2. **Chốt cú:** người chơi thả nút theo quy tắc; ý định kỹ thuật và target được xác định.
3. **Contact dự kiến:** thời điểm marker contact của clip sẽ đi qua.
4. **Contact thực tế:** bóng thực sự nằm trong vùng paddle có thể đánh, đủ điều kiện của cú và được gameplay xác nhận.

Chốt cú chưa phải Contact. Bóng ở ngoài tầm thì swing có thể trượt. Người chơi cần thả nút đủ sớm để paddle đến bóng đúng lúc.

### 10.2 Timing lấy từ brief

Các số dưới đây tính ở 30 FPS từ frame bắt đầu 1, tốc độ phát clip gốc. Thời điểm contact tính bằng **(contact frame − 1) / 30**; normalized contact tính bằng **(contact frame − 1) / (frame cuối − 1)**.

| Loại clip | Phím liên quan | Frame | Contact | Contact từ đầu clip | Normalized contact | Độ dài clip |
|---|---|---|---|---|---|---|
| Serve | J trong Serve | 1–46 | 25 | 800 ms | 0,533 | 1,5 s |
| Groundstroke | J và biến thể | 1–31 | 14 | khoảng 433 ms | 0,433 | 1,0 s |
| Dink | K/K + I | 1–25 | 12 | khoảng 367 ms | 0,458 | 0,8 s |
| Volley | L và biến thể | 1–19 | 8 | khoảng 233 ms | 0,389 | 0,6 s |
| Drop | J + K | 1–37 | 17 | khoảng 533 ms | 0,444 | 1,2 s |
| Smash | J + L | 1–31 | 16 | 500 ms | 0,500 | 1,0 s |

Serve có ReleaseBall khoảng frame 16, tương ứng 500 ms từ đầu clip; Contact ở 800 ms. Reset/Punch cần xác nhận marker của motion được tái dùng trước khi áp dụng số Volley cho chúng.

### 10.3 Các số này cho thấy rủi ro gì

Một combo đã nhận chính xác vẫn có thể quá chậm. Ví dụ Volley contact khoảng 233 ms sau đầu clip có thể không kịp một quả bóng tới trong 100 ms; chưa kể thời gian nhận input và mạng. Không thể kết luận “nút L phản ứng tức thì” từ việc clip Volley ngắn nhất.

Do đó, cần kiểm chứng nhịp bóng cùng nhịp animation trước khi chốt điều khiển. Có thể yêu cầu người chơi chuẩn bị sớm, hoặc điều chỉnh phần anticipation và hit window trong một công việc animation/gameplay riêng. Không mặc định tua clip nhanh gấp nhiều lần, bỏ qua contact hay dịch chuyển bóng tới paddle.

Giả định ban đầu của bảng là swing bắt đầu khi chốt cú. Nếu sau này cho phát một phần anticipation trong lúc giữ, phải đặc tả lại chính xác khoảng thời gian còn lại tới contact; bảng trên không còn được dùng nguyên như độ trễ sau nhả nút.

### 10.4 Timing tốt thay đổi chất lượng, không thay luật

Timing tốt giúp contact trong vùng paddle hợp lý và đạt quỹ đạo mong muốn. Timing sớm/trễ có thể làm sai góc, lệch lực, đánh hỏng hoặc trượt.

Một cú “Perfect” vẫn có thể lỗi nếu Volley trong NVZ. Một cú contact đẹp vẫn có thể ra ngoài nếu người chơi chọn góc không phù hợp. Chấm kỹ thuật phải tách **chất lượng đánh** khỏi **tính hợp lệ**.

### 10.5 Một thao tác chỉ có một lần đánh bóng

Sau khi chốt, lựa chọn kỹ thuật được khóa cho cú đó. Mỗi cú có một lần xác nhận hit, rồi follow-through và recovery. Không lấy việc giữ phím, key repeat hoặc nhiều marker làm lý do đánh bóng thêm lần nữa.

Không mặc định phải chờ hết toàn bộ clip mới được chơi tiếp; thời điểm sẵn sàng phụ thuộc recovery đã kiểm tra. Ngược lại, cũng không cắt mọi clip ngay sau Contact để có tốc độ đánh vô hạn. Bản đầu không nên tích lũy hàng đợi nhiều cú đánh được bấm trước.

### 10.6 Lực và cân bằng

U chỉ thay ý định tấn công. Đầu ra còn phụ thuộc kiểu cú, tốc độ/chiều cao bóng đến, vị trí contact và mức ổn định của nhân vật. Cú mạnh hơn cần có đánh đổi về kiểm soát hoặc cơ hội bị trả nhanh; không đặt một phần trăm bonus cố định khi chưa thử nghiệm.

Chạy có thể giúp tới bóng nhưng làm tư thế đánh thiếu ổn định. Đề xuất truyền đạt bằng feedback và mức sai lệch có kiểm soát, thay vì Shift luôn khiến mọi cú hỏng. Không thêm stamina bắt buộc vào phạm vi nghiên cứu này.

## 11. Forehand, backhand và di chuyển khi đánh

Người chơi chọn kỹ thuật; hệ thống chọn FH/BH theo điểm contact tương đối với thân và tay thuận. Không dùng A cho backhand hoặc D cho forehand, vì A/D đã là di chuyển.

Khi bóng nằm phía tay cầm paddle, ưu tiên FH; phía đối diện ưu tiên BH. Ở vùng gần giữa thân, cần một khoảng chuyển tiếp để tránh clip đổi liên tục khi bóng dao động nhỏ. Khi swing đã được chốt, không đổi FH/BH đột ngột giữa clip chỉ vì nhân vật di chuyển qua đường giữa.

Vẫn có thể di chuyển trong lúc chọn và trong mức phù hợp khi đánh. Không khóa WASD trong cả 1–1,5 giây chỉ vì clip dài. Tuy nhiên, chuyển hướng hoặc chạy cực nhanh giữa swing có thể phá weight transfer và contact; mức xoay/di chuyển cho phép cần quan sát với asset thật.

Root của animation giữ in-place. Footwork, tốc độ di chuyển và quỹ đạo Jump do cơ chế di chuyển quyết định. Recovery NVZ không tự dời chân ra khỏi vùng cấm để “cứu” người chơi.

## 12. Luật không được bị combo bỏ qua

Nguồn chính thức đã đối chiếu là [USA Pickleball Official Rulebook 2026](https://usapickleball.org/docs/rules/USAP-Official-Rulebook.pdf): Serving ở Section 7; Two-bounce ở Section 10; NVZ ở Section 11. Bản dịch nội bộ dùng số phần khác, vì vậy không sao chép số điều giữa hai bản theo cùng một số.

Các yêu cầu gameplay sau kế thừa coverage trong brief:

| Tình huống | Cách áp dụng trong nghiên cứu điều khiển |
|---|---|
| Volley khi chạm Kitchen/vạch NVZ | Bấm L không tự gây lỗi; nếu contact Volley thực sự diễn ra và vi phạm thì xét fault |
| Volley ngoài NVZ rồi đà gây tiếp xúc NVZ | Tiếp tục theo dõi đà; không coi clip kết thúc hoặc bóng chết là hết trách nhiệm |
| Đã vào NVZ rồi nhảy ra để Volley | Cần điều kiện cả hai chân tiếp đất hoàn toàn ngoài NVZ trước Volley |
| K đánh bóng đã nảy khi đang ở NVZ | Hợp lệ về mặt NVZ; không phạt chỉ vì đứng trong Kitchen |
| L/K + L/L + U/L + I trong hai pha bắt buộc nảy | Nếu thực hiện contact trước nảy thì vi phạm Two-bounce |
| J + L Smash trước nảy | Xét như Volley về điều kiện NVZ và pha bắt buộc nảy |
| J + L Smash sau nảy | Xét contact sau nảy; tên Smash không tự biến nó thành Volley |
| Serve có U/I | Vẫn phải tuân thủ release, tư thế chân, motion và ô giao hợp lệ |
| Chọn Drop nhưng bóng ra ngoài | Chấm theo điểm rơi thực tế; nhãn kỹ thuật không tạo kết quả hợp lệ |

### 12.1 Phân biệt từ chối đầu vào, swing miss và fault

- **Từ chối đầu vào:** tổ hợp ba nút hoặc kỹ thuật không có trong Serve state. Không phát cú, không tự cộng/trừ điểm.
- **Swing miss:** đã thực hiện cú nhưng bóng ngoài tầm hoặc không còn phù hợp cửa sổ contact. Bóng tiếp tục cho tới khi có diễn biến kết thúc rally.
- **Fault:** có hành vi gameplay thực sự vi phạm luật, ví dụ Volley contact trong NVZ hoặc lỗi đà sau Volley. Kết quả do hệ thống có thẩm quyền xác định.

Đề xuất cho chế độ thi đấu tiêu chuẩn: **không tự chuyển cú sang kỹ thuật an toàn để tránh fault**. Có thể cảnh báo “Cần bóng nảy” hoặc “Đang chạm NVZ”, nhưng một contact Volley sai luật vẫn phải có hậu quả. Training có thể có chế độ hướng dẫn chặn lỗi, song phải được ghi rõ và không áp dụng âm thầm cho thi đấu chung.

Kiểm tra vị trí và điều kiện tại contact thực tế, không chỉ lúc thả phím. Một người thả L khi đang ngoài Kitchen có thể bước vào Kitchen trước khi paddle chạm bóng. Ngược lại, người đang giữ L trong Kitchen có thể kịp ra ngoài và thiết lập lại chân trước contact.

## 13. Thiết kế điều khiển điện thoại

### 13.1 Bố cục đề xuất

Ưu tiên màn hình ngang để hai ngón cái làm việc ở hai phía:

| Khu vực | Thành phần | Vai trò |
|---|---|---|
| Trái dưới | Joystick di chuyển | Tiến/lùi/ngang/chéo |
| Vành ngoài joystick | Trạng thái chạy | Tương đương giữ Shift |
| Phải dưới | Ba nút Drive, Dink, Volley | Tương đương J/K/L |
| Gần cụm đánh | Tấn công và Chéo | Tương đương bổ trợ U/I |
| Trên cụm đánh | Tên cú đang chọn, tổ hợp và cảnh báo ngắn | Người chơi biết sắp phát kỹ thuật nào |
| Phía trên màn hình | Điểm số, pause, thông tin ít thao tác | Tránh chiếm vùng ngón cái |

Ba nút gốc nên bố trí thành tam giác: **Dink phía trên; Drive dưới trái; Volley dưới phải**, để cả ba cặp đều có đường trượt trực tiếp. Vùng nối giữa hai nút là hành lang gesture, không phải một nút đánh thứ tư. Vị trí cuối cùng phải thử trên thiết bị và tay thật.

Nhãn chính dùng tên kỹ thuật/tiếng Việt; có thể kèm J/K/L nhỏ để học bảng tương đương PC. Không chỉ hiện các ký tự phím khó hiểu cho người chơi mobile.

### 13.2 Cú đơn bằng một ngón

Chạm Drive/Dink/Volley để chọn, nhấc ngón trong nút đó để chốt cú đơn. Giữ ngắn để quan sát/chọn không tăng lực. Bóng và nhân vật vẫn vận động theo gameplay.

### 13.3 Combo bằng chạm–trượt–nhấc

| Thao tác | Kết quả |
|---|---|
| Drive → Dink, nhấc | Drop |
| Drive → Volley, nhấc | Smash |
| Dink → Volley, nhấc | Reset volley |
| Dink → Drive, nhấc | Drop, tương đương thứ tự ngược trên PC |
| Volley → Drive, nhấc | Smash |
| Volley → Dink, nhấc | Reset volley |

Ngón tay phải đi vào vùng nhận của nút thứ hai; không suy ra combo chỉ từ việc trượt một đoạn ngắn gần mép. Khi đã nhận cặp, UI đổi tên cú và đánh dấu cả hai nút. Nhấc trong nút thứ hai hoặc hành lang nối của cặp đó sẽ chốt cặp đã nhận.

Nếu chỉ bắt đầu trượt nhưng chưa tới nút thứ hai, nhấc trong hành lang thì **hủy**, không phát một cú đơn ngoài ý muốn. Kéo ra vùng Hủy cũng hủy. Trượt sang nút gốc thứ ba trong cùng lần chạm hủy vì vượt giới hạn hai nút.

Sau khi nhận cặp, quay về nút đầu không đổi về cú đơn; muốn đổi phải hủy và thao tác lại. Quy tắc này tránh một cú Drop bị biến thành Drive vì ngón tay rung nhẹ lúc nhấc.

### 13.4 U/I trên mobile dùng lựa chọn cho một cú

Một ngón cái khó giữ modifier và nút đánh riêng biệt. Vì vậy, modifier trên mobile là **lựa chọn một lần**:

1. Chạm Tấn công hoặc Chéo; chip được đánh dấu và hiển thị sẽ áp dụng cho cú kế tiếp.
2. Chạm một nút gốc rồi nhấc: tạo đúng một tổ hợp tương đương PC.
3. Lựa chọn modifier được xóa sau thao tác chốt, dù cú đánh sau đó thành công, trượt hay bị từ chối. Cũng xóa khi hủy, rally kết thúc hoặc mất touch/focus.
4. Chạm lại chip đang chọn để bỏ. Chỉ một trong Tấn công/Chéo được chọn tại một thời điểm.

| Thao tác mobile | Tương đương PC |
|---|---|
| Tấn công rồi Drive | J + U |
| Tấn công rồi Dink | K + U |
| Tấn công rồi Volley | L + U |
| Chéo rồi Drive | J + I |
| Chéo rồi Dink | K + I |
| Chéo rồi Volley | L + I |

Nếu đã chọn modifier rồi trượt giữa hai nút gốc, đó là ba lựa chọn logic: **hủy và nhắc bỏ modifier trước khi dùng combo giữa hai cú gốc**. Không âm thầm bỏ U/I hoặc chọn một kết quả tùy ưu tiên. Khi đang chạm cụm đánh, không đổi modifier bằng một touch khác.

Sự khác biệt PC/mobile là thao tác vật lý, còn danh sách kỹ thuật và giới hạn hai lựa chọn vẫn giống nhau. PC cũng có thể cung cấp tùy chọn modifier một lần trong phần hỗ trợ điều khiển.

### 13.5 Chạy bằng joystick

Đề xuất joystick vùng trong để đi và vùng ngoài để chạy. Đi vào vùng chạy tương đương Shift; kéo về trong trở về tốc độ thường. Ngón phải vẫn rảnh để chọn cú đánh.

Giả thuyết để thử: vào chạy ở khoảng 85% bán kính hữu dụng, ra chạy dưới khoảng 70%; dùng hai ngưỡng để tránh đi/chạy đổi liên tục do rung ngón. Đây không phải thông số bắt buộc. Màn hình nhỏ, độ dài ngón và khả năng dừng sát NVZ phải quyết định việc điều chỉnh.

Cần hiển thị rõ đang chạy và cho tùy chọn khóa chạy khi người chơi cần. Không để joystick ngoài và PC Shift tạo tốc độ/độ chính xác khác nhau về luật hoặc gameplay.

### 13.6 Aim và không gian chạm

Mobile dùng cùng vùng aim mặc định/chéo như PC. Gesture giữa các nút chỉ chọn kỹ thuật, không đồng thời điều khiển một điểm aim tự do; tránh một thao tác mang hai nghĩa khó đoán.

Theo [Apple Game controls](https://developer.apple.com/design/human-interface-guidelines/game-controls), cần đặt nút dễ tới bằng ngón cái, tránh safe area và thích ứng điều khiển theo ngữ cảnh; Apple nêu tối thiểu 44 × 44 pt cho điều khiển thường dùng. [Google Touch target size](https://support.google.com/accessibility/android/answer/7101858?hl=en-GB) khuyến nghị vùng chạm ít nhất 48 × 48 dp và khoảng cách phù hợp. pt/dp không phải số pixel vật lý dùng nguyên cho mọi máy.

Kích thước tối thiểu chưa bảo đảm chơi tốt. Với cụm cần trượt combo, phải thử nút lớn hơn, hành lang rõ và vùng nhận không chồng lấn. Dùng phản hồi tên cú/biểu tượng, không chỉ màu; haptic là bổ trợ, không phải cách duy nhất biết đã nhận combo.

## 14. Tương đương giữa hai nền tảng và lựa chọn hỗ trợ

| Nội dung | Nguyên tắc |
|---|---|
| Kỹ thuật | PC/mobile cùng ba cú gốc, cùng ba combo chính và sáu biến thể |
| Đầu ra | Cùng ý định hợp lệ và tình huống phải tạo cùng giới hạn bóng/luật |
| Timing | Thao tác khác nhau được phép; không trao Perfect miễn phí riêng cho mobile |
| Di chuyển | Shift và vùng chạy joystick cùng ý nghĩa |
| Aim | Cùng vùng target và mức hỗ trợ; PC không mặc định có aim tự do vượt mobile |
| Lỗi | Cảnh báo, miss và fault theo cùng chính sách |
| Remap | Cho đổi phím, vị trí/kích thước cụm cảm ứng và bố cục tay thuận |

Với người khó giữ hai phím hoặc trượt chính xác, có thể cung cấp **nút tắt chọn Drop/Smash/Reset** hoặc chọn kỹ thuật trước rồi thực hiện. Đây là alias của cùng ý định, không thêm cú gốc, không đổi sức mạnh hoặc timing contact.

Tùy chọn này cũng giúp kiểm chứng xem lợi ích của combo có đủ lớn so với thao tác trực tiếp. Nó phù hợp với nguyên tắc cung cấp phương án thay thế khi thao tác đồng thời gây khó khăn, theo [Xbox Accessibility Guideline 107](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/107). Không kết luận người dùng bắt buộc phải chơi một dạng combo duy nhất.

Trên PC cần thử thực tế các tổ hợp WASD + Shift + hai phím đánh trên nhiều bàn phím. Khả năng nhận nhiều phím tùy thiết bị; một combo không được coi là hỏng kỹ thuật người chơi nếu bàn phím không nhận đủ input. Đây là yêu cầu thử thiết bị, chưa có kết quả xác nhận.

## 15. Ví dụ luồng chơi

### 15.1 Serve → Return → Third-shot drop

1. A vào Serve state, chọn J rồi thả. Clip 06 release bóng rồi contact để giao.
2. B di chuyển bằng WASD, cho bóng nảy, chọn J để Return sâu bằng Groundstroke.
3. A đọc điểm rơi Return, di chuyển và để bóng nảy. A giữ J, thêm K, thả một phím gốc để chọn Drop.
4. Drop dùng clip 13/14; nếu đánh tốt, bóng tới gần NVZ đối phương.
5. A tiến lên bằng WASD + Shift khi phù hợp, giảm tốc trước NVZ. Combo không tự làm bước 5.

### 15.2 Dink rồi chuyển nhịp tấn công

1. Hai người giao tranh bằng K hoặc K + I sau nảy.
2. Đối phương đưa bóng hơi cao và trong tầm đánh sau nảy.
3. Người chơi chọn K + U để yêu cầu Speed-up bằng Groundstroke phù hợp.
4. Nếu chọn nhầm với bóng quá thấp, không mặc định cú đó thành công; đối phương có thể phản công.
5. Đối phương có thể chọn K + L để Reset volley, hoặc L + U để phản công nếu đủ điều kiện.

### 15.3 Smash nhưng vẫn lỗi NVZ

1. Đối phương đánh bóng cao; người chơi đang đứng sát NVZ.
2. Người chơi chọn J + L, thực hiện Smash trước khi bóng nảy.
3. Nếu contact/đà vi phạm NVZ, kết quả là fault dù bóng được đánh mạnh và rơi đúng sân.
4. Clip recovery hoặc reaction chỉ thể hiện kết quả; không hủy lỗi.

### 15.4 Mobile vừa chạy vừa Drop

1. Ngón trái kéo joystick ra vùng chạy để tới bóng Return.
2. Ngón phải chạm Drive, trượt tới Dink; tên đang chọn đổi thành Drop.
3. Ngón trái giảm về vùng đi để ổn định; ngón phải nhấc để chốt.
4. Contact thực tế sau nảy quyết định cú đánh. Trượt đúng combo chỉ chứng minh chọn đúng ý định.

### 15.5 Chọn đúng combo nhưng sai timing

Người chơi cần Drop nhưng thả J + K khi bóng đã sát nhân vật. Nếu clip còn khoảng 533 ms tới contact, paddle có thể tới quá muộn. Feedback phải nói “Drop chọn đúng, thả quá trễ”, thay vì chỉ nói “Combo sai”.

## 16. Tutorial, drills và dữ liệu coaching

### 16.1 Thứ tự học

1. Di chuyển WASD/joystick và phân biệt đi/chạy.
2. J: Return sau nảy và chọn thời điểm thả.
3. K: Dink gần lưới; phân biệt với đánh mạnh.
4. L: Volley; học Two-bounce và NVZ trước khi tăng tốc bóng.
5. J + K: Drop; phân biệt Dink gần lưới và Drop từ sâu.
6. J + L: Smash đứng với bóng cao phù hợp.
7. K + L: Reset volley, nếu motion đã được xác nhận phù hợp.
8. U/I: biến thể tấn công và chéo; Speed-up chỉ giới thiệu sau khi thao tác cơ bản ổn định.

Không đưa toàn bộ chín tổ hợp vào một màn hình bắt người mới học ngay. Mỗi drill cần hiển thị tên cú, input tương ứng nền tảng, điều kiện bóng và feedback nguyên nhân.

### 16.2 Dữ liệu cần phân biệt trong nghiên cứu

| Nhóm thông tin | Ví dụ | Mục đích |
|---|---|---|
| Ý định | Người chơi chọn J + K/Drop | Biết họ muốn làm gì |
| Thao tác | Có ghép đúng cặp, thả khi nào, có hủy/ba nút không | Tách lỗi điều khiển khỏi lỗi kỹ thuật |
| Contact | Có hit, trước/sau nảy, FH/BH, vị trí chân và độ ổn định | Đánh giá cơ hội và luật thực tế |
| Kết quả | Điểm rơi, độ cao bóng, net/out/fault, tạo cơ hội cho đối thủ | Đánh giá chất lượng kỹ thuật |

Không dùng “đã bấm combo đúng” làm nhãn “đã đánh kỹ thuật tốt”. Không dùng tên clip Drop làm bằng chứng cú đó là cú thứ ba. Cần pha rally và dữ liệu thực tế.

Training body tracking là một phương thức đầu vào khác. Tài liệu này không định nghĩa cách nhận diện động tác cơ thể; nó chỉ đề xuất ngôn ngữ ý định kỹ thuật để các phương thức có thể được đối chiếu về sau.

## 17. Kế hoạch kiểm chứng trước khi chốt

Những việc dưới đây là kế hoạch nghiên cứu tiếp theo, chưa được thực hiện trong lượt tạo tài liệu này. Không cần lập trình để bắt đầu đánh giá khả năng nhớ nút, vị trí ngón và sự rõ ràng của quy tắc; có thể dùng bảng phím và mô hình giấy. Timing bóng/animation và mạng cần prototype thật ở giai đoạn được giao riêng.

### 17.1 Các giả thuyết cần kiểm tra

| Giả thuyết | Cách kiểm chứng | Điều sẽ khiến phải đổi thiết kế |
|---|---|---|
| Ba cú gốc dễ hiểu | Người mới giải thích và chọn J/K/L cho tình huống | Thường xuyên nhầm Dink với Drop hoặc Volley với Smash |
| Combo hai cú gốc dễ nhớ | Yêu cầu chọn Drop/Smash/Reset sau hướng dẫn ngắn | J + L khó nhớ hoặc J + K hay thành hai cú đơn |
| Giữ–thả giúp tránh nhận sai | Quan sát thứ tự nhấn/nhả và lỗi ngoài ý muốn | Người chơi bị chậm hơn đáng kể hoặc liên tục quên thả |
| Gesture tam giác khả dụng | Thử tay thật trên các kích thước màn hình | Phải nhìn cụm nút quá lâu hoặc hay chạm nút thứ ba |
| U/I thêm chiều sâu mà không quá tải | So sánh chơi ba cú + ba combo với thêm modifier | Modifier ít dùng hoặc hay gây chọn nhầm |
| Target vùng đủ chiến thuật | Đánh giá lựa chọn hướng, vị trí và phản công | Người chơi không thể thực hiện chiến thuật mong muốn |
| Bộ clip hiện có hỗ trợ biến thể | Quan sát Reset/Speed-up/Punch khi có asset thật | Motion và bóng không tương ứng hoặc deformation không đạt |
| Timing phù hợp nhịp bóng | Thử từng loại bóng đến với marker thực tế | Volley quá chậm, chỉ thắng nhờ bấm đoán sớm |

### 17.2 Các phép thử tối thiểu

- Người mới và người đã quen game thể thao thử cùng một bài hướng dẫn; ghi thời gian hiểu bảng phím và lỗi thao tác.
- Thử J/K/L và ba combo chính trước, rồi mới thêm U/I để xác định nguồn gây khó.
- Thử cả thứ tự nhấn ngược, thả từng phím, giữ modifier, nhấn ba nút và mất focus.
- Trên điện thoại, thử vừa giữ joystick vừa trượt cả ba cặp, hủy giữa hành lang và modifier một lần.
- So sánh combo với alias một nút ở cùng kỹ thuật; đánh giá độ thoải mái và chất lượng lựa chọn.
- Khi có prototype được giao riêng, kiểm tra FH/BH, Two-bounce, NVZ, momentum, Serve timer, swing miss và thời điểm recovery.
- Kiểm tra nhiều bàn phím và thiết bị mobile, không chỉ thiết bị của nhóm phát triển.

### 17.3 Chỉ số và tiêu chí dự kiến

Theo dõi tỷ lệ chọn đúng kỹ thuật, nhận sai ngoài ý muốn, hủy thao tác, thời gian chọn, thời gian nhìn xuống cụm nút, cảm giác mỏi tay và chất lượng quyết định chiến thuật. Đo riêng độ trễ từ thao tác chốt tới phản hồi UI, và thời gian từ chốt tới contact; hai số này không được gộp.

Mục tiêu thử ban đầu có thể là chọn đúng trên 90% sau hướng dẫn và lỗi phát cú ngoài ý muốn dưới 5%, nhưng cần điều chỉnh theo nhóm thử và bài tập. Đây là tiêu chí nội bộ đề xuất, không phải chuẩn ngành hoặc kết quả đo.

Nếu combo tốt trên bàn phím nhưng khó trên mobile, điều chỉnh thao tác cảm ứng hoặc dùng alias; không làm kỹ thuật mobile yếu hơn để bù cho bố cục khó dùng. Nếu nhịp animation chưa phù hợp bóng, phải sửa giả thuyết timing trước khi kết luận người chơi phản xạ kém.

## 18. Phạm vi đề xuất cho bản đầu

| Ưu tiên | Nội dung |
|---|---|
| Cốt lõi | WASD, Shift, J/K/L, Serve theo trạng thái, FH/BH, aim vùng, luật và timing rõ |
| Combo chính | J + K Drop; J + L Smash đứng; K + L Reset volley sau khi xác nhận motion |
| Mở rộng có kiểm soát | U/I và các biến thể; Speed-up/Punch cần kiểm tra motion |
| Hỗ trợ thao tác | Remap, bố cục mobile, modifier một lần, alias cho combo |
| Để sau | Lob, Drop serve riêng, Jumping Smash, spin chuyên biệt, combo ba nút, aim tự do |

Lob không có clip trong danh mục hiện tại. Không gán I thành Lob trong bản đề xuất này chỉ để dùng hết năm phím. Jumping Smash cũng không được coi là đã có chỉ vì tồn tại Jump và Smash riêng.

Mức phù hợp của phương án: giữ đúng ba cú gốc, có ba combo tạo lựa chọn kỹ thuật khác và dùng đủ J/K/L/U/I. Người chơi mới có thể bắt đầu với J/K/L; người chơi tiến bộ học combo, lực/hướng và timing. Quyết định cuối cùng phải dựa trên khả năng thao tác và nhịp animation/ball thực tế.

## 19. Tài liệu tham khảo và cách dùng nguồn

### 19.1 Tài liệu dự án

- [Brief bộ 20 animation](./PickleVerse_20_Animation_Production_Brief.md): nguồn chính cho Action, coverage, tái sử dụng và frame timing.
- [Capstone Proposal](./PickleVerse_Capstone_Proposal.md): phạm vi sản phẩm, nền tảng, coaching, telemetry và authoritative multiplayer.
- [Luật thi đấu nội bộ](./Luat_thi_dau_Pickleball.md): ngữ cảnh nghiệp vụ; không mặc định số điều trùng bản 2026.

### 19.2 Nguồn ngoài đã đối chiếu ngày 06/10/2026

- [USA Pickleball Basic Terms](https://usapickleball.org/blog-category/pickleball-basic-terms/): phân biệt kỹ thuật; không dùng làm căn cứ cho bảng combo.
- [USA Pickleball về Reset shot](https://usapickleball.org/pickleball-skills/level-three/what-is-a-pickleball-reset-shot-and-how-to-hit-it/): bối cảnh phòng thủ giảm nhịp.
- [USA Pickleball Reset technique](https://usapickleball.org/pickleball-skills/level-one/pickleball-reset-technique-to-keep-the-ball-down/): kiểm soát lực và cân bằng.
- [USA Pickleball Official Rulebook 2026](https://usapickleball.org/docs/rules/USAP-Official-Rulebook.pdf): đối chiếu luật Serve, Two-bounce và NVZ.
- [Apple Game controls](https://developer.apple.com/design/human-interface-guidelines/game-controls): điều khiển cảm ứng, vị trí nút và thích ứng theo ngữ cảnh.
- [Google Touch target size](https://support.google.com/accessibility/android/answer/7101858?hl=en-GB): vùng chạm và khoảng cách; không quy đổi dp trực tiếp thành pixel cố định.
- [Xbox Accessibility Guideline 107](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/107): phương án đầu vào thay thế và khả năng tùy chỉnh.

Các công thức nút, tên modifier, bố cục tam giác, quy tắc chốt/hủy, aim vùng và kế hoạch thử nghiệm là đề xuất nghiên cứu của tài liệu này. Nguồn bên ngoài không chứng minh hệ thống đã tồn tại hoặc phương án đã đạt hiệu quả trong PickleVerse.
