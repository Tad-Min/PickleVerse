# PickleVerse - Bộ 20 animation cho gameplay bàn phím

Ngày lập: 2026-09-30.

## 1. Mục tiêu và căn cứ

Bộ này gồm **20 clip tổng cộng**, đã tính cả idle, walk, run, jump và smash. Mục tiêu là hỗ trợ nhân vật 3D trong Unity chơi bằng bàn phím, thể hiện được serve, dink, volley, third-shot drop và hành vi tại Kitchen/NVZ. Đây là lựa chọn triển khai của dự án, không phải số animation do proposal bắt buộc.

Căn cứ trực tiếp:
- `PickleVerse_Capstone_Proposal.pdf`, mục Proposed Solutions và Functional Requirements: 5 nhóm kỹ thuật; physics, rules, in-game telemetry; client Unity; authoritative multiplayer.
- `PickleVerse_72_Pickleball_Animations(1).md`: thư viện tham khảo 72 động tác/biến thể. Không cần 72 clip riêng. Mục Tier 1 trong tài liệu này chưa tính walk và jump, vì vậy không dùng nguyên bộ đó khi ngân sách là 20 clip tổng.
- Phương án mới của người dùng: Training có body tracking, gameplay bàn phím sử dụng animation. Proposal PDF hiện tại mô tả telemetry và ghi real-video swing analysis là roadmap; nếu đưa body tracking vào bản nộp chính thức, cần đồng bộ scope, SRS và đặc tả kiểm thử.

20 clip đáp ứng **phần thể hiện chuyển động** của 5 nhóm. Để xác nhận đáp ứng chức năng capstone, còn phải có input, bóng bay/nảy, lựa chọn cú đánh, kiểm tra luật, drills và telemetry. Body tracking không tự thay thế các chức năng này.

Giả định sản xuất: mẫu nhân vật thuận tay phải, đã có hoặc sẽ được bổ sung armature/skin weights; cầm paddle ở tay phải. Phải kiểm tra model thật trước khi keyframe. Không suy ra tên bone từ tên model. 1v1 và 2v2 dùng cùng bộ clip, không nhân đôi animation theo số người.

## 2. Danh mục chốt - 20 Blender Actions

FH = forehand (thuận tay); BH = backhand (trái tay). Backhand vẫn sử dụng tay cầm paddle đã chọn, không chuyển paddle sang tay khác.

Thời lượng, FPS và frame dưới đây là **mốc sản xuất đề xuất**, cần điều chỉnh theo nhịp gameplay và rig. Chọn 30 FPS, frame bắt đầu bằng 1; thời lượng = (frame cuối - 1) / 30.

| # | Tên Action | Chức năng | Dải frame | Contact frame | Loop | Tham chiếu danh sách 72 |
|---:|---|---|---|---|---|---|
| 01 | `PKV_Idle_Ready` | Chờ trong tư thế thể thao, paddle trước người | 1-61 (2.0s) | Không | Có | NVZ Ready Stance, #67 |
| 02 | `PKV_Walk_Forward` | Đi bộ, bước vào/ra Kitchen ở tốc độ thấp | 1-31 (1.0s) | Không | Có | Di chuyển nền; tái dùng cho #65-66 |
| 03 | `PKV_Run_Forward` | Chạy đón bóng và tiến từ baseline lên lưới | 1-25 (0.8s) | Không | Có | Di chuyển nền |
| 04 | `PKV_Jump` | Nhảy có takeoff/landing; phụ trợ gameplay | 1-25 (0.8s) | Không | Không | Nền cho hành vi nhảy; không phải cú đánh riêng |
| 05 | `PKV_Overhead_Smash` | Đánh bóng cao trên đầu khi có cơ hội | 1-31 (1.0s) | 16 | Không | #55 |
| 06 | `PKV_Serve_FH_Underhand` | Một kiểu forehand volley serve cơ bản | 1-46 (1.5s) | 25 | Không | #1 |
| 07 | `PKV_Groundstroke_FH` | Đánh bóng sau nảy, return và drive thuận tay | 1-31 (1.0s) | 14 | Không | #9, #15; tái dùng cho #43 |
| 08 | `PKV_Groundstroke_BH` | Đánh bóng sau nảy, return và drive trái tay | 1-31 (1.0s) | 14 | Không | #10, #16; tái dùng cho #44 |
| 09 | `PKV_Dink_FH` | Cú chạm mềm gần lưới sau khi bóng nảy | 1-25 (0.8s) | 12 | Không | #23 |
| 10 | `PKV_Dink_BH` | Dink phía trái tay | 1-25 (0.8s) | 12 | Không | #24 |
| 11 | `PKV_Volley_FH` | Chặn/đẩy bóng trước khi bóng nảy, thuận tay | 1-19 (0.6s) | 8 | Không | #33; tái dùng #69 |
| 12 | `PKV_Volley_BH` | Chặn/đẩy bóng trước khi bóng nảy, trái tay | 1-19 (0.6s) | 8 | Không | #34; tái dùng #70 |
| 13 | `PKV_ThirdShotDrop_FH` | Drop mềm từ sâu trong sân, phía thuận tay | 1-37 (1.2s) | 17 | Không | #41 |
| 14 | `PKV_ThirdShotDrop_BH` | Drop mềm từ sâu trong sân, phía trái tay | 1-37 (1.2s) | 17 | Không | #42 |
| 15 | `PKV_Shuffle_Left` | Di chuyển ngang trái, thân hướng đối thủ | 1-25 (0.8s) | Không | Có | Footwork hỗ trợ dink/volley |
| 16 | `PKV_Shuffle_Right` | Di chuyển ngang phải, thân hướng đối thủ | 1-25 (0.8s) | Không | Có | Footwork hỗ trợ dink/volley |
| 17 | `PKV_Backpedal` | Lùi bước ngắn, quan sát bóng | 1-31 (1.0s) | Không | Có | Footwork nền |
| 18 | `PKV_SplitStep` | Bước tách chân nhỏ trước phản ứng đón bóng | 1-16 (0.5s) | Không | Không | #68 |
| 19 | `PKV_NVZ_MomentumRecovery` | Thu trọng tâm, giữ thăng bằng sau volley | 1-25 (0.8s) | Không | Không | #71 |
| 20 | `PKV_Fault_Reaction` | Phản ứng ngắn sau khi hệ thống đã xác định fault | 1-31 (1.0s) | Không | Không | #72; có thể tái dùng cho lỗi khác |

Không tạo riêng Power Serve, Wide Serve, Crosscourt Dink, Down-the-Line Dink, Return Serve, Third Shot Drive, Kitchen Entry/Exit hoặc Kitchen Volley. Các trạng thái này tái dùng clip và thay target, lực, shot type, vị trí nhân vật hoặc state luật.

## 3. Yêu cầu chuyển động cho từng clip

**01 - Idle Ready.** Hai chân khoảng rộng bằng vai, gối hơi chùng; thân không dựng cứng; paddle ở phía trước thân, dễ chuyển sang FH/BH. Loop thở/chuyển trọng tâm nhẹ, chân không trượt. Đây cũng là tư thế chờ ngoài NVZ, không cần thêm một NVZ idle.

**02 - Walk Forward.** Chu kỳ bước tự nhiên, tay cầm paddle ổn định; ánh nhìn hướng sân. Root không tiến trong Blender. Tốc độ Unity khớp bước chân. Dùng walk để bước vào Kitchen đón bóng đã nảy và đi ra; không biểu diễn entry/exit thành hai cú đánh.

**03 - Run Forward.** Chạy thể thao vừa phải, gối chùng và paddle giữ chắc. Tránh chạy như sprint 100m hoặc vung paddle ra sau quá xa. Có foot plant rõ và loop liên tục. Unity điều khiển hướng di chuyển.

**04 - Jump.** Anticipation nhỏ, takeoff khoảng frame 8, landing khoảng frame 20; gối hấp thụ lực rồi về ready. Không buộc root bay lên trong FBX: controller điều khiển quỹ đạo nhảy và đồng bộ pose. Clip này không chứa cú smash. Smash đứng là bản cần ưu tiên; jumping smash có thể làm ở giai đoạn sau.

**05 - Overhead Smash.** Chuẩn bị paddle trên/sau vai phải; xoay thân vừa đủ, tiếp xúc trước và trên đầu, follow-through qua phía đối diện rồi về ready. Không gán mọi quả bóng cao thành smash thành công; phải có vùng với tới và timing. Quy tắc volley/NVZ vẫn áp dụng nếu đánh trước khi bóng nảy.

**06 - Serve FH Underhand.** Tay trái giữ bóng trước người, thả khoảng frame 16; tay phải đánh từ thấp lên cao, tiếp xúc frame 25. Chọn điểm contact thấp hơn eo và đầu paddle không cao hơn cổ tay; chân đứng trong vùng phát hợp lệ. Không quay tay kiểu tennis serve trên đầu. Physics/game logic điều khiển bóng sau khi rời tay. Đây là volley serve, không phải drop serve và không phải third-shot drop.

**07 - Groundstroke FH.** Chuẩn bị bên phải, xoay vai/thân vừa phải; contact trước người, follow-through rõ nhưng gọn, về ready. Dùng cho return sau serve, rally sau nảy và drive. Animation không khóa bóng vào paddle.

**08 - Groundstroke BH.** Paddle đi ngang phía trước thân để chuẩn bị bên trái; giữ cùng tay thuận. Chọn backhand một tay để giảm yêu cầu rig, trừ khi model/thiết kế đã chốt hai tay. Không tạo bằng việc mirror FH khiến paddle đổi tay. Contact trước người rồi phục hồi.

**09 - Dink FH.** Gối thấp vừa phải, paddle phía phải; backswing rất ngắn, mặt paddle hơi mở, nâng nhẹ ra trước và phục hồi. Khoảng di chuyển paddle ngắn và mềm. Không dùng groundstroke mạnh chỉ giảm playback speed.

**10 - Dink BH.** Mô tả như dink FH nhưng paddle chuẩn bị phía trái và cánh tay đi qua thân; tránh xuyên khuỷu tay/paddle vào ngực.

**11 - Volley FH.** Paddle ở phía trước, chuẩn bị nhanh bên phải; đẩy/chặn ngắn, ít backswing, contact frame 8, về ready sớm. Không dùng đường vung underhand dài của serve.

**12 - Volley BH.** Paddle phía trước và hơi trái, động tác chặn ngắn; cùng tay cầm paddle. Không mở vai quá lớn. Feet có tư thế ngoài NVZ khi minh họa volley hợp lệ.

**13 - Third Shot Drop FH.** Chuẩn bị từ tư thế ở baseline/transition zone, swing từ thấp lên cao mềm hơn drive nhưng biên độ lớn hơn dink gần lưới. Contact frame 17, follow-through có kiểm soát và về ready. Unity tính đường bóng rơi vào Kitchen đối thủ; không bake việc chạy lên lưới vào Action này.

**14 - Third Shot Drop BH.** Chuẩn bị phía trái, mặt paddle mở vừa phải, swing mềm về trước/lên, giữ thân cân bằng. Cùng nguyên tắc trajectory và recovery như FH.

**15-16 - Shuffle Left/Right.** Bước ngang ngắn, gối chùng; ở nhịp chậm không bắt chéo chân; vai vẫn hướng sân. Tạo loop chân riêng và giữ paddle ready. Cho phép chuyển từ shuffle sang dink/volley mà không phải quay toàn thân.

**17 - Backpedal.** Lùi ngắn, đầu giữ hướng bóng, trọng tâm kiểm soát. Bóng rất sâu nên chuyển sang chạy/quay theo controller thay vì bắt nhân vật backpedal toàn sân.

**18 - Split Step.** Nhún rất nhỏ và tiếp đất với hai chân rộng, về tư thế phản ứng. Đây là clip phụ trợ, không phải animation nhảy cao. Dùng vào nhịp đối thủ đánh, tránh phát liên tục khi đang chạy.

**19 - NVZ Momentum Recovery.** Thu trọng tâm về sau, gối chùng, bước điều chỉnh nhỏ và giữ paddle phía trước. Bản mặc định là giữ thăng bằng ngoài NVZ. Game có thể phát clip khi cần phục hồi nhưng không được xóa fault hoặc kết thúc momentum chỉ vì clip đã hết.

**20 - Fault Reaction.** Hạ paddle và dừng nhẹ sau khi nhận kết quả fault. Không ngã hoặc ăn mừng dài. Không tự bước vào Kitchen: đây là phản hồi cho một sự kiện đã được game xác định, không phải tác nhân tạo lỗi.

## 4. Tái sử dụng và phân biệt kỹ thuật

| Trạng thái gameplay | Clip tái dùng | Tham số/hành vi cần thay |
|---|---|---|
| Return of serve FH/BH | Groundstroke FH/BH | Chờ bóng nảy, target sâu, lực phù hợp |
| Third shot drive | Groundstroke FH/BH | Nhãn shot type và trajectory drive |
| Dink crosscourt/straight | Dink FH/BH | Hướng aim/target, sửa pose giới hạn |
| Drop crosscourt/straight | ThirdShotDrop FH/BH | Target và đường bóng |
| Deep/wide serve | Serve FH Underhand | Target vùng phát và lực |
| Volley gần Kitchen | Volley FH/BH | Vị trí chân và kiểm tra NVZ |
| Kitchen entry/exit | Walk/Run/Backpedal | Controller dịch chuyển, foot contact state |
| Hướng chéo khi chạy | Walk/Run + hướng controller | Hướng root/controller và blend tốc độ |

Phân biệt quan trọng: dink là cú mềm gần lưới sau nảy; third-shot drop là cú mềm từ sâu trong sân ở pha serve -> return -> cú thứ ba. Hai clip cần khác biên độ và cách chuẩn bị. Cùng clip drop có thể dùng cho drop ở các cú sau; không bắt buộc mọi cú thứ ba là drop.

Hướng/độ khó/force không tạo thêm clip. Sai số, timing window, target size và accuracy là gameplay. Không dùng tăng tốc/giảm tốc clip quá mức để thay đổi kỹ thuật.

Lob không nằm trong bộ này. Nếu sau này thêm lob cho gameplay keyboard, có thể thay `PKV_Jump` hoặc `PKV_Fault_Reaction` bằng một forehand lob để giữ trần 20.

## 5. Coverage và điều kiện nghiệm thu kỹ thuật

| Nhóm capstone | Clip trực tiếp | Điều cần demo được trong game |
|---|---|---|
| Serve | 06 | Motion đúng, thả/contact khớp, phát vào service court chéo hợp lệ |
| Dink | 09-10 | Chạm mềm sau nảy gần lưới; target NVZ đối thủ; phân biệt với drive |
| Volley | 11-12 | Đánh trước nảy khi đủ điều kiện; motion ngắn; bắt lỗi khi ở NVZ |
| Third-shot drop | 13-14 | Serve -> return -> drop sau nảy; bóng rơi gần lưới và nhân vật có thể tiến lên |
| Kitchen/NVZ | 01, 09-12, 15-19, 20 | Phân biệt vào Kitchen hợp lệ với volley/momentum fault; hiển thị kết quả |

Các trường hợp luật tối thiểu phải kiểm tra:
- Người chơi vào Kitchen và đánh bóng đã nảy: hợp lệ nếu không vi phạm luật khác.
- Volley khi chạm Kitchen hoặc đường biên NVZ: fault.
- Volley ngoài Kitchen, sau đó đà từ cú volley gây tiếp xúc NVZ: fault, kể cả bóng vừa chết.
- Sau khi chạm NVZ, trước khi volley phải có cả hai chân tiếp đất hoàn toàn ngoài NVZ. Không cho nhảy từ Kitchen để vượt kiểm tra.
- Two-bounce rule: receiver phải cho serve nảy, bên serve phải cho return nảy trước cú trả đầu tiên.
- Serve chạm NVZ hoặc đường NVZ: fault; các đường service court còn lại cần áp dụng đúng luật line calls.

Đây là yêu cầu cho **rule engine**, không phải điều kiện suy ra từ tên Action. Cần state bóng, chân/ground contact, phase rally và momentum episode. NVZ là vùng trên mặt sân, không phải cột trigger vô hạn trên không. Không dùng mỗi tâm capsule để phán toàn bộ lỗi chạm chân/paddle.

Nếu engine tự chặn thao tác volley không hợp lệ, phản hồi phải nói rõ lý do; nếu cho phép người chơi phạm lỗi để học luật, kết quả fault do server xác định. Chốt một chính sách trong SRS và dùng nhất quán.

Multiplayer: server kiểm tra contact tick, shot window, vị trí/ground contact hợp lệ và kết quả bóng. Client animation/Animation Event không tự trao điểm hoặc thay đổi physics có thẩm quyền.

## 6. Điều kiện cho Blender và Unity

1. Kiểm tra trước armature, deform bones, skin weights, rest pose, scale, forward direction và paddle. Model 3D có sẵn không có nghĩa là đã rig.
2. Giữ cùng skeleton/rest pose cho 20 Actions. Không tạo 20 armature khác nhau. Không áp transform vào rig đang hoạt động một cách mù quáng.
3. Animation in-place: object/root không tịnh tiến qua sân; Unity điều khiển movement. Hip/body có thể chuyển động cục bộ cho weight shift và bước chân. Với jump, tránh cộng hai lần quỹ đạo nhảy.
4. Ghi rõ scale: Unity dùng mét; đo chiều cao model thật rồi chuẩn hóa có kiểm soát. Kiểm tra hướng forward sau khi export thay vì ngầm giả định trục của Blender trùng Unity.
5. Paddle có hand socket gắn tay thuận; giữ grip liên tục. Ball chỉ gắn tay trái ở trạng thái chuẩn bị serve và phải detach lúc release.
6. Mỗi cú đánh có marker `Contact`; serve có thêm `ReleaseBall`. Các clip cần `Recover`; locomotion có foot plant markers khi hữu ích. Marker Blender phải được xuất sang metadata; không giả định Unity tự nhập thành Animation Events.
7. Frame contact trong bảng là giá trị khởi đầu. Lưu `contactNormalizedTime = (contactFrame - startFrame) / (endFrame - startFrame)` và đồng bộ với animation/gameplay tick.
8. Dùng contact socket/hit window nhỏ quanh thời điểm contact; nếu bóng nằm ngoài tầm với thì swing miss. Không cho một input đánh hai lần trong cùng action.
9. Dùng IK vừa phải cho tay/paddle và chân khi tích hợp, không kéo tay xuyên thân để cứu mọi cú đánh. Input/controller xử lý vị trí trước.
10. Làm full-body shots trước để giảm lỗi blend. Nếu dùng upper-body layer, tạo Avatar Mask phù hợp và kiểm tra lower-body locomotion không phá weight transfer.
11. FBX cho Unity: bake pose của các deform bones cần thiết; clip boundaries/tên Action rõ; constraints phải có kết quả được bake. Kiểm tra chỉ đúng Actions mục tiêu được export. Chọn Humanoid khi rig/map phù hợp, Generic nếu rig không đáp ứng; đừng ép Humanoid để đạt tên chuẩn.
12. Xuất thử 01 + 06 + 07 trước. Kiểm tra scale, axes, rest pose, deformation, paddle attachment, clip timing, foot sliding trong Unity rồi mới xuất batch còn lại.

## 7. Input bàn phím đề xuất

Đây là phương án để thử nghiệm, không phải controls bắt buộc của proposal.

| Input | Hành vi |
|---|---|
| WASD | Di chuyển |
| Shift | Chạy khi thích hợp |
| Mũi tên trái/phải | Chỉnh hướng aim trong giới hạn |
| J | Groundstroke; ở Serve State chuyển sang serve |
| K | Soft shot: dink gần NVZ, drop khi ở sâu hơn trong sân |
| L | Yêu cầu volley; rule engine kiểm tra tính hợp lệ |
| U | Yêu cầu overhead smash với bóng đủ cao/tầm với |
| Space | Jump nếu bật tính năng này |

FH/BH chọn theo vị trí contact trong local space của nhân vật và tay thuận, không theo nửa sân toàn cục. Aim/shot context không tự đảm bảo thành công. Bên server xác định bóng đã nảy hay chưa, distance/contact và fault.

## 8. Thứ tự sản xuất

**Batch A - 13 clip lõi:** 01, 02, 03, 05, 06, 07, 08, 09, 10, 11, 12, 13, 14. Lúc này cần rule engine NVZ dù chưa có reaction clip riêng. Làm 3 clip thử 01, 06, 07 trước để kiểm tra rig/export.

**Batch B - 4 clip footwork:** 15, 16, 17, 18. Chỉnh loop, transitions và phản ứng gần lưới.

**Batch C - 3 clip phụ trợ:** 19, 20, 04. Làm sau khi timing và luật hoạt động.

Nếu cần rút xuống **18 clip**, bỏ Jump (04) và Fault Reaction (20), dùng UI fault. Vẫn giữ 5 nhóm kỹ thuật. Nếu đã có những clip nền đạt chất lượng thì tái dùng, không tạo lại; tổng sau tích hợp vẫn theo danh mục tối đa 20.

## 9. Prompt giao cho Codex kết nối Blender trên máy

```text
Bạn làm animation cho nhân vật PickleVerse hiện có trong thư mục project và Blender, đích xuất Unity. Đọc toàn bộ PickleVerse_20_Animation_Production_Brief.md trước khi thao tác.

Mục tiêu: tạo chính xác các Blender Actions trong danh mục 20 clip. Chế độ gameplay dùng bàn phím, không phụ thuộc body tracking. Animation phải thể hiện serve, dink, volley, third-shot drop và footwork/NVZ. Không tạo 72 clip.

Trước tiên, kiểm tra công cụ Blender thực sự gọi được trong phiên này; liệt kê file model, mesh, armature, tên bone, rig type, skin weights, rest pose, scale và paddle. Không suy luận skeleton từ tên file. Nếu không có armature thì làm rigging/weights cần thiết trước khi animation. Nếu không thể điều khiển Blender, báo chính xác blocker, không nhận đã tạo animation.

Dùng model/rig hiện có, giữ mesh/materials/rest pose và animation nguồn. Tạo bản làm việc riêng trong thư mục animation của project. Không xóa asset nguồn. Với 20 Action mục tiêu, khi chạy lại hãy cập nhật Action mục tiêu và giữ mọi Action khác.

Mẫu motion: thuận tay phải, paddle ở tay phải, backhand vẫn dùng tay đó. Nếu model có tay thuận khác thì ghi rõ và điều chỉnh sau khi kiểm tra. Cùng một skeleton cho toàn bộ clip. Root in-place; Unity điều khiển movement/trajectory/jump. Tất cả clip đánh gồm anticipation -> contact -> follow-through -> recovery. Không cho paddle xuyên người, grip trượt hoặc root drift.

Đầu tiên tạo và kiểm tra 3 Action mẫu: PKV_Idle_Ready, PKV_Serve_FH_Underhand, PKV_Groundstroke_FH. Xuất test FBX và preview. Sau khi xử lý vấn đề rig/scale/axes/contact, hoàn thành Batch A, B, C theo brief. Không cần hỏi lại để làm từng batch đã được giao; báo lỗi không giải quyết được và phần đã hoàn tất.

Áp dụng đúng mô tả từng Action và timing trong brief. Dink phải ngắn/mềm; volley phải là cú đẩy/chặn ngắn; third-shot drop phải khác dink ở preparation và swing; serve underhand không được thành overhead tennis serve. Không chuyển tay cầm paddle để giả lập BH bằng mirror FH.

Thêm timeline markers ReleaseBall cho serve, Contact cho các shot, Recover cho các shot/recovery, foot plant khi cần. Xuất animation_manifest.json để lưu actionName, frameStart, frameEnd, fps, loop, contactFrame, contactNormalizedTime, releaseFrame và recoveryFrame. Marker không tự trở thành Unity Animation Event, cần metadata để cấu hình bên Unity.

Tạo file .blend nguồn, export FBX cho Unity và preview từng Action từ góc bên hông/front khi cần. Ghi QA report: root drift, clip boundaries, looping, grip, contact position, knee/elbow deformation, foot sliding và lỗi chưa sửa. Không tuyên bố đã import vào Unity nếu chưa thực sự import.

Không bake animation của bóng bay theo preset vào character FBX. Rule engine, ball physics và authoritative hit timing thuộc gameplay. Không tạo thêm animation riêng cho crosscourt, power/deep serve, return serve, third shot drive, entry/exit Kitchen; tái dùng theo bảng trong brief.
```

## 10. Kiểm tra trước khi nghiệm thu

- Đúng 20 Action với tên ổn định; hoặc 18 nếu quyết định bỏ 04/20.
- Loop không giật ở idle/walk/run/shuffle/backpedal, không root drift.
- Một contact marker mỗi shot; release chỉ ở serve; ball/paddle/grip khớp hình ảnh ở contact.
- Forehand/backhand đều chạy với tay thuận đã chọn.
- Dink, volley và drop có chuyển động đọc được là ba loại khác nhau.
- Model không xoắn gối/khuỷu, bàn tay không xuyên paddle/body.
- Thử được đủ 5 dòng coverage ở mục 5 trong gameplay, ghi video và test result theo từng dòng.
- Multiplayer không phụ thuộc FPS render để phán điểm/fault; client không tự xác nhận hit.
- Brief này là kế hoạch, chưa phải chứng nhận model được rig hoặc animations đã được tạo/kiểm tra trong Blender/Unity.

## 11. Nguồn đối chiếu

- Proposal PDF và thư viện 72 animation do người dùng gửi.
- [USA Pickleball - Basic Terms](https://usapickleball.org/pickleball-skills/level-one/pickleball-basic-terms/): phân biệt motion dink, drop, groundstroke, volley.
- [USA Pickleball - Rules Summary](https://usapickleball.org/rules/summary/): serve, two-bounce rule và line calls.
- [USA Pickleball - Official Rulebook 2026](https://usapickleball.org/docs/rules/USAP-Official-Rulebook.pdf): Section 7 Serving; Section 11 Non-Volley Zone Infractions; định nghĩa momentum/act of volleying.

Timing, tên clip, workflow Blender/Unity, bộ 20 và input mapping là đề xuất kỹ thuật cho PickleVerse, không phải yêu cầu hoặc định mức của USA Pickleball.
