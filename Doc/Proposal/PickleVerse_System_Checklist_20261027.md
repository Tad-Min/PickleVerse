# PickleVerse — Checklist kiểm tra toàn hệ thống

Ngày kiểm tra: **27/10/2026**. Đối chiếu proposal và [bộ tiêu chí tiến độ](PickleVerse_Progress_Evaluation_Criteria.md).

**Kết luận:** đã có nền tảng gameplay, training, online và backend hoạt động ở phạm vi kiểm tra cục bộ. Hệ thống chưa đủ điều kiện kết luận hoàn thành theo proposal: admin login/CMS/analytics còn lỗi, Hybrid coaching đang tắt, thiếu dedicated authority, model đã đánh giá, offline progression và các bằng chứng NFR/UAT.

Checklist gồm **183 thành phần**. Đếm trạng thái chỉ phản ánh số thành phần được rà soát, **không phải phần trăm hoàn thành hệ thống**; chưa chốt trọng số con/owner nên không tự tính điểm tiến độ.

## 1. Cách đọc kết quả

- **[x] ĐẠT KIỂM TRA:** thành phần đạt kiểm tra ở phạm vi nêu trong dòng, có bằng chứng. Có thể dùng làm đầu vào nghiệm thu; không thay biên bản nghiệm thu toàn luồng/thiết bị của nhóm.
- **[ ] MỘT PHẦN:** đã có code hoặc tích hợp, còn thiếu điều kiện/bằng chứng để hoàn thành cả thành phần.
- **[ ] CHƯA CÓ:** chưa có khả năng đáp ứng trong source/luồng đã rà soát, hoặc khả năng đó đang tắt trong môi trường được kiểm tra; quan sát giải thích rõ từng trường hợp.
- **[ ] CHƯA KIỂM CHỨNG:** chưa đủ kết quả chạy thực tế để kết luận đạt hay không đạt.
- **[ ] LỖI:** có kết quả chạy hoặc đường đi mã nguồn cụ thể mâu thuẫn hành vi mong đợi; ghi rõ bằng chứng.

Đã đọc mã nguồn chính, hợp đồng Unity/API/React/Python, schema/catalog, cấu hình và tài liệu; chạy build/test cùng truy vấn cục bộ phù hợp. Không chơi lại 10 trận mỗi chế độ, không giả lập đầy đủ mạng xấu, không đo điện thoại/iOS thật, không chạy tải 2.000 người/500 trận hoặc xác nhận SLA tháng. Bằng chứng smoke lịch sử được ghi rõ, không gán là đã chạy mới.

Các mục shop/cosmetic, replay và âm thanh được đưa vào vì có trong sản phẩm hiện tại. Việc đưa vào checklist không tự mở rộng cam kết đề tài. Không sửa mã gameplay/backend/frontend trong đợt audit; file bổ sung là báo cáo và công cụ/bằng chứng audit.

Số mục lỗi không bằng số nguyên nhân độc lập: lỗi hợp đồng đăng nhập ảnh hưởng cả request, response và điều kiện release; lỗi trạng thái database ảnh hưởng nhiều nghiệp vụ admin.

## 2. Tổng hợp theo nhóm

| Nhóm | Tổng | Đạt kiểm tra | Một phần | Chưa có | Chưa kiểm chứng | Lỗi |
|---|---:|---:|---:|---:|---:|---:|
| G1. Gameplay, luật và điều khiển | 21 | 18 | 3 | 0 | 0 | 0 |
| G2. Training, progression và tiện ích người chơi | 28 | 13 | 14 | 0 | 1 | 0 |
| G3. Multiplayer, matchmaking, Elo và social | 24 | 14 | 6 | 1 | 3 | 0 |
| G4. Bot, telemetry và AI coaching | 25 | 15 | 4 | 3 | 3 | 0 |
| G5. Tài khoản, backend, dữ liệu và bảo mật | 22 | 14 | 4 | 0 | 4 | 0 |
| G6. Admin web và vận hành nội dung | 28 | 1 | 11 | 8 | 0 | 8 |
| G7. Hiệu năng, nền tảng, offline và khả dụng | 17 | 1 | 6 | 2 | 7 | 1 |
| G8. Kiểm thử, triển khai và bàn giao | 18 | 7 | 5 | 2 | 2 | 2 |
| **Tổng** | **183** | **83** | **53** | **16** | **20** | **11** |

## 3. Kết quả chạy kiểm tra mới

| Kiểm tra | Kết quả | Giới hạn |
|---|---|---|
| Backend/API/IntegrationChecks build | Đạt, 0 lỗi | Còn NU1900 khi không lấy được vulnerability feed |
| Admin TypeScript + production build | Đạt | Có cảnh báo bundle >500 kB; không chứng minh browser/API E2E |
| Unity EditMode | **463/463 đạt**, 0 failed/skipped | Editor Windows; không phải nghiệm thu thiết bị thật |
| Python AI | **11/11 đạt** | Fixture tổng hợp, chưa phải model/nhãn production |
| Auth | **124 assertions đạt** | Mail adapter thu nội bộ, không gửi thư thật |
| Online game | **76 assertions đạt** | Gồm một số ca online/social; không cộng trùng thành độ bao phủ |
| Online multiplayer/social | **31 assertions đạt** | Service/backend; không mô phỏng 31 trận |
| Activity/ranked | **22 assertions đạt** | Database tạm, không đổi tài khoản thật |
| Redis | **28 assertions đạt** | Namespace thử riêng |
| Room invitations | **20 assertions đạt** | Kiểm thử service và trạng thái |
| Hybrid backend | **32 assertions đạt** | Ingest/worker/Elo ledger/ownership |
| Hybrid HTTP/steady/burst | **6 assertions đạt** | FastAPI fixture riêng trên loopback, không phải Internet/Android |
| Configuration | **4 assertions đạt rồi dừng ở 1 lỗi** | Test còn kỳ vọng schema 37 bảng/208 cột; các ca phía sau chưa chạy |
| Admin observations trên DB tạm | Tái hiện ban/unban/manual verify lỗi 23514; create lesson lỗi 22P02; DAU sai | Xem artifact riêng; không tính việc phát hiện lỗi là sản phẩm đã pass |
| HTTP môi trường dev | Readiness 200; API bảo vệ trả 401; payload login admin hiện tại trả 400 | Không dùng tài khoản/mật khẩu thật |

Tổng các log backend ghi nhận **343 assertions đạt trước/ngoài một lỗi configuration**, gồm các ca trùng giữa suite. Không diễn giải con số này thành 343 ca yêu cầu độc lập hoặc tỷ lệ hoàn thành. Bảy suite backend hoàn tất, một suite configuration dừng; HTTP/AI là suite bổ sung thứ chín.

Benchmark mới: 10 phiên steady ×1.000 records, ingest p95 **59,00 ms**, coaching ready p95 **1.014,68 ms**; burst 100 phiên ×200 records, ingest p95 **417,80 ms**, backlog hết sau **3,30 giây**; **112 ACK/112 completed** gồm warm-up. Mốc ready không bao gồm UI/Internet và chưa đạt bằng chứng cho yêu cầu phản hồi sau từng point/drill trong 1 giây.

DB hiện tại và DB khởi tạo mới đã đối chiếu có **43 bảng/247 cột**; catalog hiện **12 lesson/12 drill**. Bốn cờ AI của container hiện tại đều `false`. Scene Menu trước/sau kiểm tra giữ nguyên, sạch và ngoài Play Mode.

## 4. Checklist chi tiết

### G1. Gameplay, luật và điều khiển — trọng số nhóm đề xuất 15%

| Check | ID | Thành phần nhỏ | Trạng thái | Kết quả quan sát / việc còn lại | Bằng chứng |
|---|---|---|---|---|---|
| [x] | G1-01 | Di chuyển, chạy và giới hạn sân | ĐẠT KIỂM TRA | PracticeMovementTests đạt; đã kiểm tra logic di chuyển, chưa thay cho đo cảm giác điều khiển trên điện thoại. | [U](#e-u), [MOVE](#e-move) |
| [x] | G1-02 | Chọn và kết hợp các kiểu đánh | ĐẠT KIỂM TRA | TouchControlsTests/TutorialTests kiểm tra mask và tổ hợp cú đánh. | [U](#e-u), [SHOT](#e-shot) |
| [x] | G1-03 | Tiếp xúc vợt–bóng, cửa sổ timing | ĐẠT KIỂM TRA | 29 PaddleContactTests đạt ở phạm vi thuật toán tiếp xúc. | [U](#e-u), [CONTACT](#e-contact) |
| [ ] | G1-04 | Đường bay, nảy bóng và collision trong trận thực | MỘT PHẦN | Có mô phỏng và smoke online cũ; cần chơi nhiều rally trên build hiện tại, kiểm tra xuyên bóng/kẹt bóng. | [RULE](#e-rule), [PHYS](#e-phys) |
| [x] | G1-05 | Luật giao bóng: vị trí chân và động tác | ĐẠT KIỂM TRA | SinglesMatchTests và MultiplayerRulesTests đạt các nhánh luật tương ứng. | [U](#e-u), [RULE](#e-rule) |
| [x] | G1-06 | Thời hạn và lỗi giao bóng | ĐẠT KIỂM TRA | Có ServeTimeout/ServeMotion/ServeMiss; kiểm thử luật đạt. | [U](#e-u), [RULE](#e-rule) |
| [x] | G1-07 | Ô giao bóng, đường biên và kitchen line | ĐẠT KIỂM TRA | Kiểm thử quy tắc ranh giới và giao chéo sân đạt. | [U](#e-u), [RULE](#e-rule) |
| [x] | G1-08 | Luật two-bounce | ĐẠT KIỂM TRA | Luật local/online có kiểm thử; không cho volley sớm theo trạng thái rally. | [U](#e-u), [RULE](#e-rule) |
| [x] | G1-09 | Lỗi bóng nảy hai lần và đánh hai lần | ĐẠT KIỂM TRA | Các trạng thái DoubleBounce/DoubleHit được kiểm tra. | [U](#e-u), [RULE](#e-rule) |
| [x] | G1-10 | Volley trong kitchen và lỗi đà di chuyển | ĐẠT KIỂM TRA | Có KitchenVolley/KitchenMomentum; bộ kiểm thử luật hiện tại đạt. | [U](#e-u), [RULE](#e-rule) |
| [x] | G1-11 | Bóng out, chạm lưới và lỗi sân | ĐẠT KIỂM TRA | Nhánh luật và va chạm tương ứng có kiểm tra module; cần duy trì QA hình ảnh thực tế. | [U](#e-u), [RULE](#e-rule) |
| [x] | G1-12 | Tính điểm, đổi người giao và kết thúc game/match | ĐẠT KIỂM TRA | SinglesMatchTests và MultiplayerRulesTests đạt; gồm cấu trúc điểm/game. | [U](#e-u), [RULE](#e-rule) |
| [x] | G1-13 | Luật và thứ tự giao bóng đánh đôi | ĐẠT KIỂM TRA | 24 MultiplayerRulesTests đạt; đây là xác nhận logic, trận bốn người đánh giá ở G3. | [U](#e-u), [MPRULE](#e-mprule) |
| [x] | G1-14 | Điều khiển bàn phím và ánh xạ input | ĐẠT KIỂM TRA | Logic input và shot mask có kiểm thử; bindings có trong client. | [U](#e-u), [SHOT](#e-shot) |
| [x] | G1-15 | Cảm ứng nhiều ngón, joystick, chạy và đánh độc lập | ĐẠT KIỂM TRA | 16 MobileTouchPadEventTests và 32 TouchControlsTests đạt trong Editor. | [U](#e-u), [TOUCH](#e-touch) |
| [x] | G1-16 | Thiết lập cảm ứng và nâng cấp cấu hình cũ | ĐẠT KIỂM TRA | 3 TouchSettingsMigrationTests đạt; chưa phải thử nghiệm thao tác trên máy thật. | [U](#e-u), [TOUCH](#e-touch) |
| [ ] | G1-17 | HUD, scoreboard, thông báo và chuyển scene | MỘT PHẦN | Có UI và các kiểm thử navigation/lifecycle; còn cần nghiệm thu màn hình/độ phân giải trên thiết bị thật. | [U](#e-u), [NET](#e-net), [D-MOBILE](#e-d-mobile) |
| [x] | G1-18 | Tutorial dạy luật và cách điều khiển | ĐẠT KIỂM TRA | 15 TutorialTests đạt; curriculum và tiến trình có tích hợp backend. | [U](#e-u), [TUTORIAL](#e-tutorial) |
| [x] | G1-19 | Animation nhân vật và tư thế cú đánh | ĐẠT KIỂM TRA | 7 ActorPoseMixerTests đạt ở phạm vi phối tư thế; chất lượng hình ảnh cần QA khi chơi. | [U](#e-u), [POSE](#e-pose) |
| [ ] | G1-20 | Sân, ánh sáng, camera và asset đồ họa | MỘT PHẦN | Đã có nhiều scene/sân và tối ưu render; chưa có biên bản QA bản hiện tại cho tất cả tổ hợp nền tảng. | [PHYS](#e-phys), [D-MOBILE](#e-d-mobile) |
| [x] | G1-21 | Âm nhạc, playlist và chính sách âm thanh | ĐẠT KIỂM TRA | 23 MusicPolicyTests đạt; không suy ra thiết bị âm thanh nào cũng đã được thử. | [U](#e-u), [AUDIO](#e-audio) |

### G2. Training, progression và tiện ích người chơi — trọng số nhóm đề xuất 15%

| Check | ID | Thành phần nhỏ | Trạng thái | Kết quả quan sát / việc còn lại | Bằng chứng |
|---|---|---|---|---|---|
| [x] | G2-01 | Catalog bài học lấy từ backend | ĐẠT KIỂM TRA | Kiểm thử DB/catalog đạt; catalog hiện có 12 lesson/drill chơi được. | [BGAME](#e-bgame), [CATALOG](#e-catalog) |
| [ ] | G2-02 | Số lượng khoảng 15 drill theo proposal | MỘT PHẦN | Hiện 12 lesson và 12 drill; cần chốt danh mục cuối hoặc ghi nhận thay đổi so với khoảng 15 drill. | [DB](#e-db), [CATALOG](#e-catalog), [PROPOSAL](#e-proposal) |
| [ ] | G2-03 | Drill Serve | MỘT PHẦN | Có trong catalog, đã test tính sao/thưởng backend; chưa nghiệm thu thao tác end-to-end trên build/thiết bị hiện tại. | [TRAIN](#e-train), [BGAME](#e-bgame) |
| [ ] | G2-04 | Drill Drive | MỘT PHẦN | Có curriculum, input và kiểm tra thưởng; còn bước nghiệm thu gameplay hiện tại. | [TRAIN](#e-train), [BGAME](#e-bgame) |
| [ ] | G2-05 | Drill Dink | MỘT PHẦN | Có curriculum và kiểm tra thưởng; cần xác nhận độ khó/vùng đích khi chơi thật. | [TRAIN](#e-train), [BGAME](#e-bgame) |
| [ ] | G2-06 | Drill Volley | MỘT PHẦN | Có curriculum và kiểm tra thưởng; còn bước nghiệm thu gameplay hiện tại. | [TRAIN](#e-train), [BGAME](#e-bgame) |
| [ ] | G2-07 | Drill lực và hướng chéo | MỘT PHẦN | Có lesson modifiers, combo mask và prerequisite; cần nghiệm thu người dùng. | [TRAIN](#e-train), [TRAINMODEL](#e-trainmodel) |
| [ ] | G2-08 | Drill Third-shot drop | MỘT PHẦN | Có lesson drop; chưa có kết quả nghiệm thu độc lập đầy đủ của drill trên thiết bị. | [TRAIN](#e-train), [TRAINMODEL](#e-trainmodel) |
| [ ] | G2-09 | Drill Reset | MỘT PHẦN | Có lesson reset; cần kiểm tra tình huống hoàn thành/thất bại qua UI thật. | [TRAIN](#e-train), [TRAINMODEL](#e-trainmodel) |
| [ ] | G2-10 | Drill Smash | MỘT PHẦN | Có lesson smash; còn bước nghiệm thu end-to-end. | [TRAIN](#e-train), [TRAINMODEL](#e-trainmodel) |
| [ ] | G2-11 | Drill footwork/hồi vị | MỘT PHẦN | Có lesson footwork; cần đối chiếu metric hồi vị và trải nghiệm thực tế. | [TRAIN](#e-train), [TRAINMODEL](#e-trainmodel) |
| [ ] | G2-12 | Drill drop rồi tiến lên | MỘT PHẦN | Có lesson approach; còn bước nghiệm thu end-to-end. | [TRAIN](#e-train), [TRAINMODEL](#e-trainmodel) |
| [ ] | G2-13 | Drill đánh đôi giữ khoảng cách | MỘT PHẦN | Có doubles-cover; chưa đủ bằng chứng nghiệm thu gameplay đồng đội. | [TRAIN](#e-train), [TRAINMODEL](#e-trainmodel) |
| [ ] | G2-14 | Drill đánh đôi chuyển trạng thái | MỘT PHẦN | Có doubles-transition và prerequisite; cần QA full flow. | [TRAIN](#e-train), [TRAINMODEL](#e-trainmodel) |
| [x] | G2-15 | Điều kiện mở khóa bài học | ĐẠT KIỂM TRA | Test prerequisite và từ chối yêu cầu mở bài khóa trực tiếp đạt. | [U](#e-u), [BGAME](#e-bgame), [TRAINMODEL](#e-trainmodel) |
| [x] | G2-16 | Chấm kết quả 1–3 sao | ĐẠT KIỂM TRA | TrainingTests và backend kiểm tra accuracy/timing; server bỏ qua sao tự khai báo của client. | [U](#e-u), [BGAME](#e-bgame), [TRAINMODEL](#e-trainmodel) |
| [x] | G2-17 | XP, level và thưởng hoàn thành lần đầu | ĐẠT KIỂM TRA | Kiểm thử progression và thưởng server đạt. | [U](#e-u), [BGAME](#e-bgame), [PROGRESSION](#e-progression) |
| [x] | G2-18 | Chơi lại bài không nhận thưởng trùng | ĐẠT KIỂM TRA | Đã kiểm tra retry, review, command ID và trạng thái bài đã hoàn tất. | [U](#e-u), [BGAME](#e-bgame) |
| [x] | G2-19 | Checkpoint và tiếp tục bài khi online | ĐẠT KIỂM TRA | Có kiểm tra dữ liệu và lưu trạng thái; chỉ xác nhận tiếp tục bằng trạng thái online. | [U](#e-u), [BGAME](#e-bgame), [TRAIN](#e-train) |
| [x] | G2-20 | Lưu tiến trình theo tài khoản | ĐẠT KIỂM TRA | State và wallet tồn tại qua context DB mới; không dùng local save làm nguồn chuẩn. | [BGAME](#e-bgame), [RUNTIME](#e-runtime) |
| [x] | G2-21 | Mastery và thống kê luyện tập cơ bản | ĐẠT KIỂM TRA | Hoàn thành drill cập nhật bảng tiến trình/mastery; chính xác trong phạm vi số liệu game được backend chấp nhận. | [BGAME](#e-bgame), [TRAINAPI](#e-trainapi) |
| [ ] | G2-22 | Biểu đồ xu hướng accuracy theo thời gian | CHƯA KIỂM CHỨNG | Có thống kê luyện tập, chưa đối chiếu được biểu đồ/trend đủ yêu cầu và dữ liệu theo thời gian. | [PLAYERUI](#e-playerui) |
| [ ] | G2-23 | Lịch sử trận và xem replay | MỘT PHẦN | Đã test metadata/chunk/ownership và có smoke cũ; cần nghiệm thu xem lại bằng UI trên bản hiện tại. | [BGAME](#e-bgame), [REPLAY](#e-replay), [PHOTON](#e-photon) |
| [x] | G2-24 | Achievement, badge và nhận thưởng | ĐẠT KIỂM TRA | Catalog 10 achievements; kiểm thử điều kiện/claim/idempotency đạt ở backend/module. | [U](#e-u), [BGAME](#e-bgame), [PROGRESSION](#e-progression) |
| [x] | G2-25 | Daily/weekly challenge và chu kỳ reset | ĐẠT KIỂM TRA | Có 17 mission definitions; kiểm tra chu kỳ và thưởng ở backend/module đạt. | [U](#e-u), [BGAME](#e-bgame), [PROGRESSION](#e-progression) |
| [x] | G2-26 | Streak và phần thưởng đăng nhập theo ngày | ĐẠT KIỂM TRA | 18 DailyStreakTests đạt; vẫn cần QA UI bản phát hành. | [U](#e-u), [STREAK](#e-streak) |
| [x] | G2-27 | Cửa hàng, ví và mua cosmetic bằng tiền game | ĐẠT KIỂM TRA | Kiểm thử không đủ tiền, trừ số dư và persistence đạt; đây là phần đã triển khai thêm. | [U](#e-u), [BGAME](#e-bgame), [SHOP](#e-shop) |
| [x] | G2-28 | Bộ sưu tập, trang bị vợt và lưu lựa chọn | ĐẠT KIỂM TRA | PaddleEquipment/ProfileEconomy và kiểm thử server inventory đạt. | [U](#e-u), [BGAME](#e-bgame), [SHOP](#e-shop) |

### G3. Multiplayer, matchmaking, Elo và social — trọng số nhóm đề xuất 25%

| Check | ID | Thành phần nhỏ | Trạng thái | Kết quả quan sát / việc còn lại | Bằng chứng |
|---|---|---|---|---|---|
| [x] | G3-01 | Tạo/vào phòng bằng mã | ĐẠT KIỂM TRA | 15 MultiplayerRoomServiceTests cùng 9 RoomLaunchRequestTests đạt ở phạm vi service/policy. | [U](#e-u), [ROOM](#e-room), [MP](#e-mp) |
| [x] | G3-02 | Chọn 1v1/2v2 và chia đội | ĐẠT KIỂM TRA | Room/roster/rules có kiểm thử; cấu hình đội được bảo toàn. | [U](#e-u), [ROOM](#e-room), [MPRULE](#e-mprule) |
| [x] | G3-03 | Ready check cho từng người | ĐẠT KIỂM TRA | 11 MatchReadyPolicyTests và backend kiểm tra đủ đồng thuận đạt. | [U](#e-u), [BMP](#e-bmp), [READY](#e-ready) |
| [x] | G3-04 | Ngăn người ngoài start/report/leave trận | ĐẠT KIỂM TRA | Đã chạy các kiểm tra ownership tương ứng trên database tạm. | [BMP](#e-bmp), [ONLINE](#e-online) |
| [ ] | G3-05 | Thực thi một trận online 1v1 trên nhiều client | MỘT PHẦN | Có smoke Editor/Windows cũ; chưa chạy lại 10 trận theo checklist mới trên build hiện tại. | [PHOTON](#e-photon), [BMP](#e-bmp), [MP](#e-mp) |
| [ ] | G3-06 | Thực thi một trận online 2v2 bốn người | MỘT PHẦN | Có bốn artifact doubles passed cũ; chưa đủ 10 trận bốn người độc lập/mạng thiết bị thật ở phiên bản hiện tại. | [PHOTON](#e-photon), [U](#e-u), [MP](#e-mp) |
| [ ] | G3-07 | Đồng bộ di chuyển, bóng, tỷ số và lượt giao | MỘT PHẦN | Có network state/command và smoke; thiếu đối chiếu trạng thái cuối rally qua nhiều trận/mạng xấu hiện tại. | [PHOTON](#e-photon), [NET](#e-net) |
| [ ] | G3-08 | Prediction và reconciliation theo cam kết dedicated server | MỘT PHẦN | Có xử lý trạng thái/hiển thị ở Fusion Shared; chưa chứng minh cơ chế prediction/reconciliation đúng kiến trúc đã đăng ký. | [MP](#e-mp), [NET](#e-net), [PROPOSAL](#e-proposal) |
| [ ] | G3-09 | Dedicated headless server giữ quyền mô phỏng | CHƯA CÓ | Cả tạo và join dùng GameMode.Shared; chưa có dedicated gameplay server đáp ứng proposal. | [MP](#e-mp), [PROPOSAL](#e-proposal) |
| [ ] | G3-10 | Chống gian lận bằng mô phỏng độc lập trên server | MỘT PHẦN | Backend kiểm tra kết quả/đồng thuận; authority vật lý ở client, chưa bảo đảm chống các client thông đồng. | [MP](#e-mp), [BMP](#e-bmp) |
| [x] | G3-11 | Ghép trận theo rating | ĐẠT KIỂM TRA | RedisChecks kiểm tra cửa sổ Elo, ghép và trạng thái hàng chờ đạt. | [BREDIS](#e-bredis), [QUEUE](#e-queue) |
| [x] | G3-12 | Hủy, timeout, từ chối và tái xếp hàng | ĐẠT KIỂM TRA | Kiểm tra thời hạn và ready consent ở Redis đạt; không tự suy ra UI mọi tình huống đã nghiệm thu. | [BREDIS](#e-bredis), [QUEUE](#e-queue) |
| [x] | G3-13 | Giữ thành viên party cùng đội khi ghép đôi | ĐẠT KIỂM TRA | Có kiểm tra ghép nhóm và chính sách đội ở backend. | [BREDIS](#e-bredis), [QUEUE](#e-queue) |
| [x] | G3-14 | Bot dự phòng sau thời gian tìm trận | ĐẠT KIỂM TRA | 22 activity/ranked checks đạt; tài khoản bot ảo không được đăng nhập. | [BRANK](#e-brank), [BREDIS](#e-bredis) |
| [ ] | G3-15 | Kết nối lại và heartbeat | MỘT PHẦN | Backend interruption và smoke reconnect cũ có bằng chứng; cần kiểm tra app kill/mạng thật bản hiện tại. | [BMP](#e-bmp), [PHOTON](#e-photon), [ONLINE](#e-online) |
| [ ] | G3-16 | Client giữ authority rời trận/mất kết nối | CHƯA KIỂM CHỨNG | Có policy rời/forfeit; chưa có bằng chứng nghiệm thu mới cho toàn bộ tình huống authority rời và chuyển quyền. | [MP](#e-mp), [ONLINE](#e-online) |
| [ ] | G3-17 | Mô phỏng mạng RTT/jitter/mất gói theo tiêu chí | CHƯA KIỂM CHỨNG | Chưa có kết quả ma trận 100/200 ms, loss 1%/3%, jitter 20 ms được đối chiếu trong đợt này. | [NET](#e-net), [CRITERIA](#e-criteria) |
| [x] | G3-18 | Elo tính từ dữ liệu chuẩn ở backend | ĐẠT KIỂM TRA | Đã kiểm tra Elo mới/legacy, rating snapshot và bỏ qua delta/rating client giả. | [BHYBRID](#e-bhybrid), [BMP](#e-bmp), [ELO](#e-elo) |
| [x] | G3-19 | Chốt kết quả, thưởng và Elo đúng một lần | ĐẠT KIỂM TRA | Đồng thuận, transaction và ledger/idempotency đã kiểm tra thực tế. | [BMP](#e-bmp), [BHYBRID](#e-bhybrid), [ONLINE](#e-online) |
| [x] | G3-20 | Kết quả xung đột không cấp thưởng | ĐẠT KIỂM TRA | Đã chạy ca hai người báo winner khác nhau và người ngoài báo kết quả. | [BMP](#e-bmp), [ONLINE](#e-online) |
| [x] | G3-21 | Leaderboard và vị trí người chơi | ĐẠT KIỂM TRA | Redis Sorted Set, TTL và thứ hạng/pagination có kiểm tra đạt. | [BREDIS](#e-bredis), [BMP](#e-bmp), [SOCIAL](#e-social) |
| [x] | G3-22 | Tìm bạn, gửi/nhận/chấp nhận/xóa/chặn | ĐẠT KIỂM TRA | Backend kiểm tra quan hệ, stale request và quyền chấp nhận đạt; client UI đã nối API. | [BMP](#e-bmp), [SOCIAL](#e-social) |
| [x] | G3-23 | Mời bạn vào phòng và phản hồi lời mời | ĐẠT KIỂM TRA | 20 kiểm tra backend và 9 RoomLaunchRequestTests đạt; có minh chứng UI hai peer ngày 25/10. | [BINV](#e-binv), [U](#e-u), [INV](#e-inv) |
| [ ] | G3-24 | Nghiệm thu UI social và lời mời trên hai thiết bị thật | CHƯA KIỂM CHỨNG | Lần trước dùng hai runner trong Editor; chưa có bằng chứng hai thiết bị vật lý ở bản hiện tại. | [D-INV](#e-d-inv), [SOCIAL](#e-social) |

### G4. Bot, telemetry và AI coaching — trọng số nhóm đề xuất 15%

| Check | ID | Thành phần nhỏ | Trạng thái | Kết quả quan sát / việc còn lại | Bằng chứng |
|---|---|---|---|---|---|
| [x] | G4-01 | Behavior Tree cho bot đối thủ | ĐẠT KIỂM TRA | BhTreeTests/AdaptiveBotGraphTests đạt; graph thật được kiểm tra. | [U](#e-u), [BOT](#e-bot) |
| [x] | G4-02 | Các mức khó và policy adaptive | ĐẠT KIỂM TRA | Kiểm tra biên rating, contact chance, snapshot policy và graph đạt. | [U](#e-u), [BOT](#e-bot), [ELO](#e-elo) |
| [x] | G4-03 | Bot dùng chung policy giữa local và online | ĐẠT KIỂM TRA | SeededGraphIsEquivalentAcrossContextsAndCommandsAreLegal đạt ở module/graph. | [U](#e-u), [BOTADAPTER](#e-botadapter) |
| [x] | G4-04 | Bot tôn trọng two-bounce và kitchen | ĐẠT KIỂM TRA | MandatoryBounceAndKitchenExcludeVolleyBranches đạt. | [U](#e-u), [BOTADAPTER](#e-botadapter) |
| [ ] | G4-05 | Cân bằng bot với người chơi thật | CHƯA KIỂM CHỨNG | Chưa có playtest có mẫu người chơi/độ khó và kết quả cân bằng độc lập. | [BOT](#e-bot), [D-HYBRID](#e-d-hybrid) |
| [x] | G4-06 | Collector input/contact/miss/landing/fault | ĐẠT KIỂM TRA | NetworkCoachingTests và fixture kiểm tra record/ownership; nghiệm thu telemetry qua nhiều rally vẫn cần làm. | [U](#e-u), [COLLECT](#e-collect) |
| [ ] | G4-07 | Spool telemetry theo tài khoản | MỘT PHẦN | Có file spool, checkpoint và bảo vệ owner; chưa nghiệm thu đầy đủ app kill/reconnect trên thiết bị thật. | [SPOOL](#e-spool), [U](#e-u) |
| [x] | G4-08 | Chia part, sắp thứ tự và gửi lại telemetry | ĐẠT KIỂM TRA | Đã kiểm tra out-of-order, duplicate, conflict và chỉ tạo job khi đủ part. | [BHYBRID](#e-bhybrid), [HYBRID](#e-hybrid) |
| [x] | G4-09 | Ownership và từ chối telemetry giả định dạng | ĐẠT KIỂM TRA | Ca tài khoản khác, session lạ, NaN và vector sai đạt. | [BHYBRID](#e-bhybrid), [HYBRID](#e-hybrid) |
| [x] | G4-10 | Worker, lease, retry và phục hồi job | ĐẠT KIỂM TRA | Đã kiểm tra worker với fixture và recovery; HTTP test nối FastAPI riêng đạt. | [BHYBRID](#e-bhybrid), [WORKER](#e-worker) |
| [x] | G4-11 | Service AI stateless và hợp đồng HTTP | ĐẠT KIỂM TRA | 11 test Python và 6 kiểm tra HTTP/worker đạt; Python không tự ghi DB/Elo. | [PY](#e-py), [BHTTP](#e-bhttp), [AIHTTP](#e-aihttp) |
| [x] | G4-12 | Luật phát hiện early/late/late serve | ĐẠT KIỂM TRA | Fixture kiểm tra dấu timing và phản hồi tương ứng đạt; chưa xác nhận precision/recall thực nghiệm. | [PY](#e-py), [AIENGINE](#e-aiengine) |
| [x] | G4-13 | Luật phát hiện kitchen/momentum/net/out | ĐẠT KIỂM TRA | Có rule/fault mapping và fixture; độ chính xác trên dữ liệu người thật đánh giá riêng. | [PY](#e-py), [AIENGINE](#e-aiengine) |
| [x] | G4-14 | Luật vùng đích và hồi vị | ĐẠT KIỂM TRA | Có điều kiện quan sát/không dùng số liệu thiếu làm lỗi; đạt kiểm tra fixture. | [PY](#e-py), [AIENGINE](#e-aiengine) |
| [ ] | G4-15 | Nhận diện DinkTooHigh đã hiệu chỉnh | MỘT PHẦN | Có gate và test bảo vệ; mặc định chưa bật khi thiếu threshold đã hiệu chỉnh. | [PY](#e-py), [AIENGINE](#e-aiengine), [AIREADME](#e-aireadme) |
| [x] | G4-16 | Không suy lỗi từ aim/timing do game hỗ trợ | ĐẠT KIỂM TRA | Các ca assisted timing/engine-generated target không thành bằng chứng độc lập đạt. | [PY](#e-py), [AIENGINE](#e-aiengine) |
| [x] | G4-17 | Gợi ý drill theo điểm yếu và prerequisite | ĐẠT KIỂM TRA | Có kiểm tra số cơ hội tối thiểu, history, fallback và prerequisite; chưa có UAT về hữu ích. | [PY](#e-py), [BHTTP](#e-bhttp), [AIENGINE](#e-aiengine) |
| [ ] | G4-18 | Độ chính xác phản hồi với nhãn độc lập | CHƯA KIỂM CHỨNG | Chưa có bộ nhãn độc lập/confusion matrix để đo precision ≥0,85 và recall ≥0,80 theo tiêu chí đề xuất. | [AIREADME](#e-aireadme), [D-HYBRID](#e-d-hybrid), [CRITERIA](#e-criteria) |
| [x] | G4-19 | Pipeline huấn luyện classifier tier | ĐẠT KIỂM TRA | Test player split, artifact manifest/hash và gate đạt bằng fixture tổng hợp. | [PY](#e-py), [AITRAIN](#e-aitrain) |
| [ ] | G4-20 | Model production vượt gate holdout | CHƯA CÓ | Không có model production/nhãn chuyên gia đã được nghiệm thu; pipeline test không thay được model thật. | [AIREADME](#e-aireadme), [AITRAIN](#e-aitrain) |
| [ ] | G4-21 | Tự đánh giá trình độ trong onboarding | CHƯA CÓ | Có đăng ký và dữ liệu phiên đầu cho model; chưa thấy questionnaire/luồng self-assessment như proposal. | [ACCOUNTUI](#e-accountui), [HYBRID](#e-hybrid), [PROPOSAL](#e-proposal) |
| [ ] | G4-22 | UI hiển thị coaching và drill gợi ý | MỘT PHẦN | Có màn GÓP Ý SAU BUỔI CHƠI; chưa nghiệm thu thủ công toàn bộ UI và chuyển drill. | [COACHUI](#e-coachui), [SPOOL](#e-spool) |
| [ ] | G4-23 | Coaching được bật cho người dùng môi trường hiện tại | CHƯA CÓ | Ingest/dispatcher/coaching/model đều false; có code nhưng luồng Hybrid chưa được bật để dùng thực tế. | [FLAGS](#e-flags), [COACHUI](#e-coachui) |
| [ ] | G4-24 | Phản hồi sau point/drill trong 1 giây | MỘT PHẦN | Luồng hiện xử lý sau phiên; local coaching-ready p95 mới khoảng 1,015 giây, chưa đo đến UI và chưa đáp ứng cam kết đầy đủ. | [BHTTP](#e-bhttp), [BENCH](#e-bench), [PROPOSAL](#e-proposal) |
| [ ] | G4-25 | Kiểm chứng tác dụng huấn luyện ngoài đời | CHƯA KIỂM CHỨNG | Chỉ có telemetry trong game; chưa có nghiên cứu người chơi trước/sau ngoài đời. Không dùng mục này để tự nâng kết quả AI. | [PROPOSAL](#e-proposal), [D-HYBRID](#e-d-hybrid) |

### G5. Tài khoản, backend, dữ liệu và bảo mật — trọng số nhóm đề xuất 10%

| Check | ID | Thành phần nhỏ | Trạng thái | Kết quả quan sát / việc còn lại | Bằng chứng |
|---|---|---|---|---|---|
| [x] | G5-01 | Đăng ký và xác thực đầu vào | ĐẠT KIỂM TRA | 124 assertions auth đạt; sai confirm password không tạo tài khoản/OTP. | [BAUTH](#e-bauth), [AUTH](#e-auth) |
| [x] | G5-02 | OTP gắn email, hết hạn và chống dùng lại | ĐẠT KIỂM TRA | Đã kiểm tra email binding, expiry, replay và số lần nhập sai. | [BAUTH](#e-bauth), [AUTH](#e-auth) |
| [x] | G5-03 | Gửi lại OTP và cooldown | ĐẠT KIỂM TRA | Đã kiểm tra resend/cooldown; test dùng mail adapter thu nội bộ, không gửi email thật. | [BAUTH](#e-bauth), [AUTH](#e-auth) |
| [ ] | G5-04 | SMTP gửi thư tới hộp thư thật ở phiên bản hiện tại | CHƯA KIỂM CHỨNG | Có adapter SMTP và bằng chứng lịch sử; không gửi thư thật trong đợt kiểm tra này. | [BAUTH](#e-bauth), [AUTH](#e-auth) |
| [x] | G5-05 | Đăng nhập và hồ sơ server | ĐẠT KIỂM TRA | Đã chạy đăng nhập sau OTP và kiểm tra hồ sơ trả về. | [BAUTH](#e-bauth), [AUTH](#e-auth) |
| [x] | G5-06 | Refresh token rotation và logout | ĐẠT KIỂM TRA | Đã kiểm tra token cũ bị từ chối sau rotation/logout. | [BAUTH](#e-bauth), [AUTH](#e-auth) |
| [x] | G5-07 | Từ chối đăng nhập/refresh tài khoản bị khóa | ĐẠT KIỂM TRA | Các ca suspended login và refresh rejected đạt. | [BAUTH](#e-bauth), [AUTH](#e-auth) |
| [x] | G5-08 | JWT bảo vệ API bắt buộc | ĐẠT KIỂM TRA | HTTP API cốt lõi/admin không token trả 401; JWT coaching test đạt. | [BHTTP](#e-bhttp), [HTTP](#e-http), [PROGRAM](#e-program) |
| [ ] | G5-09 | Phân quyền Player/Admin tại server | MỘT PHẦN | Có Authorize Roles=Admin và chặn chưa đăng nhập; chưa chạy lại HTTP Player→403/Admin→200 trong đợt này. | [ADMIN](#e-admin), [PROGRAM](#e-program) |
| [ ] | G5-10 | Thu hồi access token ngay khi ban/đổi role | CHƯA KIỂM CHỨNG | Đã xác nhận chặn login/refresh; chưa kiểm chứng việc vô hiệu JWT đang còn hạn ở tất cả endpoint. | [PROGRAM](#e-program), [AUTH](#e-auth) |
| [x] | G5-11 | Quyền sở hữu trận, tiến trình và replay | ĐẠT KIỂM TRA | Đã chạy ca truy cập chéo tài khoản/outsider; replay metadata/chunk được bảo vệ. | [BGAME](#e-bgame), [BMP](#e-bmp), [REPLAY](#e-replay) |
| [x] | G5-12 | Transaction, idempotency và chống thưởng lặp | ĐẠT KIỂM TRA | Kiểm tra đồng thời, retry và rating ledger đạt trên DB tạm. | [BGAME](#e-bgame), [BMP](#e-bmp), [BHYBRID](#e-bhybrid) |
| [x] | G5-13 | Khởi tạo schema/catalog từ một SQL | ĐẠT KIỂM TRA | Khởi tạo fresh DB thành công, 43 bảng/247 cột; test seed repeatable đạt. | [BGAME](#e-bgame), [BHYBRID](#e-bhybrid), [DB](#e-db) |
| [ ] | G5-14 | Schema khớp mô hình entity/runtime | MỘT PHẦN | Các luồng game/Hybrid đạt; enum trạng thái và dữ liệu tạo lesson của Admin chưa khớp schema, đã tái hiện lỗi PostgreSQL. | [BGAME](#e-bgame), [BHYBRID](#e-bhybrid), [DB](#e-db), [ADMINOBS](#e-adminobs) |
| [x] | G5-15 | Redis queue, presence và cache leaderboard | ĐẠT KIỂM TRA | 28 Redis checks và 20 invitation checks đạt với namespace tách biệt. | [BREDIS](#e-bredis), [BINV](#e-binv), [QUEUE](#e-queue) |
| [x] | G5-16 | Replay chunk, checksum/manifest và lưu trữ | ĐẠT KIỂM TRA | Backend kiểm tra chunk/order/idempotency/ownership; test này dùng storage adapter trong bộ kiểm thử. | [BGAME](#e-bgame), [REPLAY](#e-replay) |
| [ ] | G5-17 | S3 thật cho replay trên build hiện tại | CHƯA KIỂM CHỨNG | Có container S3 và smoke lịch sử; chưa chạy lại upload/download replay trên storage thật trong đợt này. | [REPLAY](#e-replay), [PHOTON](#e-photon) |
| [x] | G5-18 | Vô hiệu API cũ cho client tự cấp điểm/Elo | ĐẠT KIỂM TRA | Các endpoint finish/submit-drill/save replay cũ trả 410; luồng mới dùng match/session có mã. | [LEGACY](#e-legacy), [PROGRAM](#e-program) |
| [ ] | G5-19 | HTTPS cho bản release | MỘT PHẦN | Có redirection và policy release HTTPS; môi trường hiện tại là Development/HTTP loopback, chưa nghiệm thu triển khai HTTPS. | [PROGRAM](#e-program), [TRANSPORT](#e-transport) |
| [ ] | G5-20 | Mã hóa dữ liệu lưu trữ AES-256 | CHƯA KIỂM CHỨNG | Chưa có bằng chứng cấu hình mã hóa volume/DB/object storage đạt cam kết. | [DEPLOY](#e-deploy), [PROPOSAL](#e-proposal) |
| [x] | G5-21 | Mật khẩu/token được hash và quản lý phiên | ĐẠT KIỂM TRA | Auth test kiểm tra keyed OTP hash, refresh hash, rotation và phiên không hợp lệ đạt. | [BAUTH](#e-bauth), [AUTH](#e-auth) |
| [ ] | G5-22 | Quản lý secret và hạn chế abuse | MỘT PHẦN | Có cấu hình env, auth rate limit; còn fallback/development settings trong source và chưa có rà soát triển khai production đầy đủ. | [PROGRAM](#e-program), [DEPLOY](#e-deploy) |

### G6. Admin web và vận hành nội dung — trọng số nhóm đề xuất 8%

| Check | ID | Thành phần nhỏ | Trạng thái | Kết quả quan sát / việc còn lại | Bằng chứng |
|---|---|---|---|---|---|
| [x] | G6-01 | Admin production build và TypeScript | ĐẠT KIỂM TRA | npm run build thành công; còn cảnh báo bundle lớn, không có lỗi TypeScript. | [FBUILD](#e-fbuild), [WEBREADME](#e-webreadme) |
| [ ] | G6-02 | Admin đăng nhập qua API thật | LỖI | Frontend gửi emailOrUsername/passwordHash; backend cần email/password. Payload hiện tại đã thử và bị trả HTTP 400. | [HTTP](#e-http), [WEBAUTH](#e-webauth), [DTOS](#e-dtos) |
| [ ] | G6-03 | Admin đọc đúng token/role từ response | LỖI | Frontend cần token/role/userId ở top-level; backend trả success/accessToken/refreshToken/profile. Cần thống nhất cả request và response. | [WEBAUTH](#e-webauth), [DTOS](#e-dtos) |
| [ ] | G6-04 | Route guard và khôi phục phiên admin | MỘT PHẦN | Có guard/localStorage/interceptor; chưa nghiệm thu phiên API thật do hợp đồng đăng nhập đang lỗi. | [WEBAUTH](#e-webauth), [WEBCLIENT](#e-webclient) |
| [ ] | G6-05 | Danh sách/tìm/lọc/phân trang tài khoản | MỘT PHẦN | Có API và UI; chưa nghiệm thu từ trình duyệt qua phiên admin thật. | [ADMIN](#e-admin), [WEBAPI](#e-webapi) |
| [ ] | G6-06 | Khóa/mở khóa tài khoản | LỖI | Đã thử service trên DB tạm: cả ban/unban thất bại 23514:users_status_check do Active/Banned khác bộ giá trị DB. | [ADMIN](#e-admin), [WEBAPI](#e-webapi), [ADMINOBS](#e-adminobs) |
| [ ] | G6-07 | Khóa có thời hạn và lưu lý do | MỘT PHẦN | Request có reason/bannedUntil, nhưng service chỉ đổi Status; chưa lưu/thi hành đầy đủ thời hạn và lý do. | [ADMIN](#e-admin), [DTOS](#e-dtos) |
| [ ] | G6-08 | Gán vai trò Player/Admin | MỘT PHẦN | Có API/service; cần test quyền, hiệu lực phiên và thao tác trình duyệt thực. | [ADMIN](#e-admin), [WEBAPI](#e-webapi) |
| [ ] | G6-09 | Reset toàn bộ tiến trình người chơi | CHƯA CÓ | Hiện có reset-elo, chưa có luồng reset toàn bộ XP/drill/achievement/progression đúng yêu cầu. | [ADMIN](#e-admin), [WEBAPI](#e-webapi), [PROPOSAL](#e-proposal) |
| [ ] | G6-10 | Reset Elo quản trị | MỘT PHẦN | Có endpoint/service/UI; cần kiểm tra đồng bộ ledger/cache/tiến trình sau reset và nghiệm thu API thật. | [ADMIN](#e-admin), [WEBAPI](#e-webapi) |
| [ ] | G6-11 | Xác thực email thủ công | LỖI | Thất bại 23514:users_status_check; đồng thời service không cập nhật Verificationemails.Isverified. Login sau thử vẫn email_not_verified. | [ADMIN](#e-admin), [AUTH](#e-auth), [ADMINOBS](#e-adminobs) |
| [ ] | G6-12 | Tạo lesson/drill trong database | LỖI | Tạo lesson thất bại PostgreSQL 22P02 trên fresh DB; service gán Reward dạng 50 EXP trong khi cột là JSONB. Phần drill cần kiểm tra tiếp sau khi sửa. | [ADMIN](#e-admin), [WEBCMS](#e-webcms), [ADMINOBS](#e-adminobs), [SQL](#e-sql) |
| [ ] | G6-13 | Nội dung tạo từ CMS xuất hiện trong game | LỖI | Đang bị chặn ngay ở bước insert lesson; sau đó vẫn còn khoảng cách code/config vì catalog chỉ đọc lesson có code và runtime config hợp lệ. | [ADMIN](#e-admin), [CATALOG](#e-catalog), [ADMINOBS](#e-adminobs) |
| [ ] | G6-14 | Sửa lesson/drill đã tạo | CHƯA CÓ | Chưa có API/UI edit phù hợp trong controller hiện tại. | [ADMIN](#e-admin), [WEBAPI](#e-webapi), [WEBREADME](#e-webreadme) |
| [ ] | G6-15 | Publish/unpublish và phiên bản nội dung | CHƯA CÓ | Chưa có workflow xuất bản/gỡ nội dung theo proposal. | [ADMIN](#e-admin), [CATALOG](#e-catalog), [WEBREADME](#e-webreadme) |
| [ ] | G6-16 | Chỉnh difficulty và reference technique data | MỘT PHẦN | Có trường/cấu hình cố định; chưa có luồng quản trị cập nhật đầy đủ và áp dụng vào runtime. | [WEBCMS](#e-webcms), [ADMIN](#e-admin) |
| [ ] | G6-17 | Tạo cosmetic và mission từ CMS dùng được trong game | MỘT PHẦN | Có endpoint tạo bản ghi, nhưng runtime đọc config/code chuẩn; cần hoàn thiện contract xuất bản và kiểm thử game đọc được. | [ADMIN](#e-admin), [CATALOG](#e-catalog) |
| [ ] | G6-18 | Xem lịch sử trận đã kết thúc | MỘT PHẦN | Có recent matches và UI polling; cần nghiệm thu API thật, dữ liệu trả về và truy cập replay. | [ADMIN](#e-admin), [MATCHREPO](#e-matchrepo) |
| [ ] | G6-19 | Giám sát các trận đang diễn ra | CHƯA CÓ | Endpoint hiện đọc Matchhistories; chưa có dữ liệu trạng thái live/ticket/heartbeat để vận hành trận đang chạy. | [ADMIN](#e-admin), [MATCHREPO](#e-matchrepo), [WEBREADME](#e-webreadme) |
| [ ] | G6-20 | Xử lý disconnect từ dashboard | CHƯA CÓ | Chưa có hành động vận hành tương ứng trong API hiện tại. | [ADMIN](#e-admin), [WEBREADME](#e-webreadme) |
| [ ] | G6-21 | Danh sách và xử lý báo cáo vi phạm | MỘT PHẦN | Có reports/review và UI; chưa nghiệm thu end-to-end với tài khoản bị báo cáo. | [ADMIN](#e-admin), [WEBAPI](#e-webapi) |
| [ ] | G6-22 | Người chơi gửi báo cáo từ game | MỘT PHẦN | Có endpoint backend, chưa thấy luồng UI game gọi gửi báo cáo được nghiệm thu. | [MATCHAPI](#e-matchapi), [ACCOUNTUI](#e-accountui) |
| [ ] | G6-23 | Khiếu nại và giải quyết appeals | CHƯA CÓ | Chưa có API/UI/trạng thái appeals phù hợp. | [ADMIN](#e-admin), [WEBREADME](#e-webreadme) |
| [ ] | G6-24 | Đếm active users theo ngày | LỖI | ActiveUsersToday lấy totalUsers; fresh DB không có activity nhưng vẫn báo mọi tài khoản là active. | [ADMIN](#e-admin), [ADMINOBS](#e-adminobs) |
| [ ] | G6-25 | Tổng số trận không bị cắt ở 1.000 | LỖI | TotalMatchesPlayed lấy Count của GetRecentMatchesAsync(1000), không phải tổng thực khi quá 1.000 trận. | [ADMIN](#e-admin), [MATCHREPO](#e-matchrepo) |
| [ ] | G6-26 | Retention và bài học phổ biến | CHƯA CÓ | Chưa có API/cohort definition và tập dữ liệu kiểm chứng. | [ADMIN](#e-admin), [WEBREADME](#e-webreadme) |
| [ ] | G6-27 | Analytics sức khỏe matchmaking | CHƯA CÓ | Chưa có queue time, fill rate, timeout/fallback rate trong dashboard/API analytics. | [ADMIN](#e-admin), [WEBREADME](#e-webreadme) |
| [ ] | G6-28 | Heatmap lỗi kỹ thuật | MỘT PHẦN | Có heatmap API legacy; cần kiểm tra nguồn Hybrid mới, bộ dữ liệu chuẩn và mapping kỹ thuật trên UI. | [ADMIN](#e-admin), [HYBRID](#e-hybrid) |

### G7. Hiệu năng, nền tảng, offline và khả dụng — trọng số nhóm đề xuất 7%

| Check | ID | Thành phần nhỏ | Trạng thái | Kết quả quan sát / việc còn lại | Bằng chứng |
|---|---|---|---|---|---|
| [ ] | G7-01 | RTT p95 dưới 100 ms trên mạng đại diện | CHƯA KIỂM CHỨNG | Chưa có phép đo tối thiểu 1.000 mẫu/cấu hình mạng đại diện theo bộ tiêu chí. | [PHOTON](#e-photon), [CRITERIA](#e-criteria) |
| [ ] | G7-02 | FPS/frame time Windows build | CHƯA KIỂM CHỨNG | Có build/smoke lịch sử và Unity tests; chưa có profiler p95/p99 trong 20 phút trên build hiện tại. | [U](#e-u), [CRITERIA](#e-criteria) |
| [ ] | G7-03 | 60 FPS trên Android tầm trung | CHƯA KIỂM CHỨNG | Có tối ưu và APK lịch sử; tài liệu xác nhận chưa đo trên Galaxy A32 thật. | [D-MOBILE](#e-d-mobile), [PROJECT](#e-project) |
| [ ] | G7-04 | FPS, cảm ứng và nhiệt độ trên iOS thật | CHƯA KIỂM CHỨNG | Chưa có thiết bị/báo cáo nghiệm thu iOS được đối chiếu. | [PROJECT](#e-project), [CRITERIA](#e-criteria) |
| [x] | G7-05 | Pipeline AI tải steady/burst local | ĐẠT KIỂM TRA | 112 ACK/112 completed; ingest p95 ~59 ms steady, ~418 ms burst; backlog ~3,30 giây. Chỉ là baseline local. | [BHTTP](#e-bhttp), [BENCH](#e-bench) |
| [ ] | G7-06 | AI từ kết thúc point/drill đến UI ≤1 giây | MỘT PHẦN | Ready p95 local mới ~1.015 ms, chưa gồm Internet/UI; phản hồi hiện sau phiên. Không nghiệm thu đạt mục tiêu 1 giây. | [BENCH](#e-bench), [COACHUI](#e-coachui) |
| [ ] | G7-07 | 2.000 người chơi đồng thời | CHƯA KIỂM CHỨNG | Chưa có load report cho workload người chơi thật/giả lập được định nghĩa. | [DEPLOY](#e-deploy), [CRITERIA](#e-criteria) |
| [ ] | G7-08 | 500 trận có mô phỏng đồng thời | CHƯA KIỂM CHỨNG | Chưa có hạ tầng dedicated và load report cho 500 trận; tải API/AI không thay thế chỉ tiêu này. | [MP](#e-mp), [DEPLOY](#e-deploy) |
| [ ] | G7-09 | Uptime 99,5% trong cửa sổ đã chốt | CHƯA KIỂM CHỨNG | Container đang chạy không chứng minh uptime tháng; chưa có chuỗi giám sát phù hợp. | [DOCKER](#e-docker), [CRITERIA](#e-criteria) |
| [ ] | G7-10 | Windows 10+ | MỘT PHẦN | Có exe/smoke Windows lịch sử; chưa có ma trận nghiệm thu OS tối thiểu của build hiện tại. | [PHOTON](#e-photon), [PROJECT](#e-project) |
| [ ] | G7-11 | Android 9+ | MỘT PHẦN | MinSdkVersion=26; có APK lịch sử. Cần cài và kiểm tra Android 9 cùng thiết bị đại diện. | [PROJECT](#e-project), [D-MOBILE](#e-d-mobile) |
| [ ] | G7-12 | iOS 14+ theo proposal | LỖI | iOSTargetOSVersionString hiện 15.0; cấu hình build hiện tại không bao phủ iOS 14 đã đề xuất. | [PROJECT](#e-project), [PROPOSAL](#e-proposal) |
| [ ] | G7-13 | Màn hình, safe area và cảm ứng thiết bị thật | MỘT PHẦN | Có tests/mô phỏng nhiều tỷ lệ trong Editor, chưa đủ nghiệm thu thiết bị vật lý. | [U](#e-u), [D-MOBILE](#e-d-mobile) |
| [ ] | G7-14 | Training/drill chơi hoàn toàn offline | CHƯA CÓ | Runtime yêu cầu signed-in và server snapshot; không có luồng offline progression đáp ứng proposal. | [RUNTIME](#e-runtime), [TRAIN](#e-train) |
| [ ] | G7-15 | Đồng bộ tiến trình offline khi reconnect | CHƯA CÓ | Spool chỉ telemetry, không phải nguồn tiến trình; chưa có reconciliation cho offline progression. | [RUNTIME](#e-runtime), [SPOOL](#e-spool) |
| [ ] | G7-16 | Chống mất dữ liệu khi app kill/mạng lỗi | MỘT PHẦN | Backend ACK/lease/retry đã test; app kill/spool/UI trên thiết bị thật chưa nghiệm thu. | [BHYBRID](#e-bhybrid), [SPOOL](#e-spool) |
| [ ] | G7-17 | Backup/restore và phục hồi dịch vụ | MỘT PHẦN | Có volume/restart và dữ liệu dev; chưa có diễn tập restore cùng RPO/RTO, runbook production được xác nhận. | [DEPLOY](#e-deploy), [DOCKER](#e-docker) |

### G8. Kiểm thử, triển khai và bàn giao — trọng số nhóm đề xuất 5%

| Check | ID | Thành phần nhỏ | Trạng thái | Kết quả quan sát / việc còn lại | Bằng chứng |
|---|---|---|---|---|---|
| [x] | G8-01 | Build backend/API và bộ IntegrationChecks | ĐẠT KIỂM TRA | Build hiện tại thành công, 0 lỗi; còn NU1900 do không lấy được vulnerability feed. | [BBUILD](#e-bbuild) |
| [x] | G8-02 | Build admin web | ĐẠT KIỂM TRA | Build production và TypeScript đạt; chưa có browser E2E tự động. | [FBUILD](#e-fbuild) |
| [x] | G8-03 | Biên dịch Unity và EditMode | ĐẠT KIỂM TRA | 463/463 đạt, 0 failed/skipped; Menu trước/sau giữ nguyên và sạch. | [U](#e-u), [UNITYSTATE](#e-unitystate) |
| [x] | G8-04 | Kiểm thử rule/contract/classifier pipeline Python | ĐẠT KIỂM TRA | 11/11 đạt; dữ liệu classifier là fixture tổng hợp, không phải chất lượng production. | [PY](#e-py) |
| [x] | G8-05 | Hồi quy auth/game/online/social/Redis/AI | ĐẠT KIỂM TRA | Các suite đã chạy trên DB tạm/namespace riêng; xem bảng kết quả, không cộng thành tỷ lệ bao phủ toàn hệ thống. | [BAUTH](#e-bauth), [BGAME](#e-bgame), [BMP](#e-bmp), [BRANK](#e-brank), [BREDIS](#e-bredis), [BINV](#e-binv), [BHYBRID](#e-bhybrid), [BHTTP](#e-bhttp) |
| [ ] | G8-06 | Bộ kiểm thử configuration khớp schema mới | LỖI | Dừng sau 4 assertions đạt vì còn kỳ vọng 37 bảng/208 cột; schema mới 43/247. Các ca phía sau chưa được thực thi. | [BCONFIG](#e-bconfig), [CONFIGSRC](#e-configsrc), [DB](#e-db) |
| [ ] | G8-07 | Test register ánh xạ kết quả mới vào yêu cầu | MỘT PHẦN | Có 184 ca dự kiến của snapshot cũ; chưa cập nhật traceability với log/phiên bản hiện tại. | [R2](#e-r2), [TESTREGISTER](#e-testregister) |
| [ ] | G8-08 | CI tự động cho toàn bộ stack | CHƯA CÓ | Không tìm thấy pipeline CI trong repository hiện tại; build/test lần này là chạy local. | [DEPLOY](#e-deploy), [CRITERIA](#e-criteria) |
| [x] | G8-09 | Container dev API/DB/Redis/AI/S3 | ĐẠT KIỂM TRA | Các service hiện chạy; readiness API 200, PostgreSQL/Redis/AI healthy theo Docker. | [DOCKER](#e-docker), [HTTP](#e-http), [DEPLOY](#e-deploy) |
| [ ] | G8-10 | Đóng gói và triển khai admin web | MỘT PHẦN | Có dist sau build, Compose chưa cung cấp hosting admin; chưa nghiệm thu URL triển khai chung với backend. | [FBUILD](#e-fbuild), [DEPLOY](#e-deploy) |
| [ ] | G8-11 | Dedicated game server fleet/orchestration | CHƯA CÓ | Không có fleet/dedicated authority tương ứng kiến trúc proposal. | [MP](#e-mp), [DEPLOY](#e-deploy) |
| [ ] | G8-12 | Triển khai production có HTTPS và giám sát | CHƯA KIỂM CHỨNG | Môi trường kiểm tra hiện là Docker Development/loopback; chưa có deployment production được xác nhận. | [DEPLOY](#e-deploy), [HTTP](#e-http) |
| [ ] | G8-13 | SRS/SDD/API/schema khớp sản phẩm hiện tại | MỘT PHẦN | Có bộ tài liệu Review 2 nhưng còn mốc 37 bảng và target dedicated; cần cập nhật 43 bảng/247 cột và Hybrid/Fusion thực tế. | [R2](#e-r2), [DB](#e-db), [AIREADME](#e-aireadme) |
| [ ] | G8-14 | Hướng dẫn người chơi, admin và triển khai | MỘT PHẦN | Có hướng dẫn gameplay và tài liệu tổng hợp; còn cần bộ hướng dẫn khớp build cuối và luồng admin sau sửa lỗi. | [GUIDES](#e-guides), [R2](#e-r2), [WEBREADME](#e-webreadme) |
| [ ] | G8-15 | UAT với người dùng thật | CHƯA KIỂM CHỨNG | Chưa có biên bản UAT, danh sách người thử, kịch bản và kết quả được đối chiếu. | [R2](#e-r2), [CRITERIA](#e-criteria) |
| [ ] | G8-16 | Baseline yêu cầu, owner, hạn và trọng số con | MỘT PHẦN | Có tiêu chí/trọng số nhóm; chưa có backlog con được nhóm chốt owner và nghiệm thu độc lập. | [CRITERIA](#e-criteria), [R2](#e-r2) |
| [ ] | G8-17 | Chốt release không còn P0/P1 | LỖI | Admin login và CMS→game còn lỗi cản luồng bắt buộc; chưa đủ điều kiện chốt release theo tiêu chí. | [HTTP](#e-http), [ADMINOBS](#e-adminobs), [WEBAUTH](#e-webauth) |
| [x] | G8-18 | Bằng chứng tái lập và snapshot của đợt audit | ĐẠT KIỂM TRA | Đã lưu source hash, lệnh/runner, kết quả mới và checklist máy đọc; đây là đầu ra rà soát, không phải nghiệm thu sản phẩm của hội đồng. | [MANIFEST](#e-manifest) |

## 5. Danh sách cần ưu tiên xử lý

| Mức | Việc cần làm | Điều kiện đóng |
|---|---|---|
| P1 | Sửa hợp đồng đăng nhập admin cả request và response | Tắt mock, Admin đăng nhập thật; Player bị 403 ở API admin; UI gọi được nghiệp vụ thật |
| P1 | Hoàn thiện CMS→catalog→drill chơi được | Sửa Reward JSONB và hợp đồng code/config; tạo/sửa/publish/unpublish rồi game chơi và lưu được kết quả |
| P1 | Chốt topology và mô hình chống gian lận | Có dedicated authority đạt proposal hoặc thay đổi phạm vi được xác nhận và cập nhật tài liệu |
| P1 | Nghiệm thu Hybrid và quyết định bật coaching | Đạt UI/telemetry/app kill/reconnect trên thiết bị, dữ liệu phản hồi đúng; bật cờ theo kết quả nghiệm thu |
| P1 | Quyết định phần model/onboarding, offline và iOS 14 | Có triển khai/bằng chứng tương ứng hoặc thay đổi phạm vi rõ ràng; không tự xóa yêu cầu |
| P1 | Sửa ban/unban/manual verification khớp schema | Không còn lỗi 23514; email được xác thực thật, trạng thái ban/unban có hiệu lực đúng |
| P2 | Sửa DAU và tổng số trận | Kiểm tra fixture độc lập với kỳ vọng đúng, không chỉ đổi giao diện |
| P2 | Cập nhật configuration test và tài liệu schema | Không còn kỳ vọng cứng 37/208; chạy được các ca cấu hình đang bị chặn phía sau |
| P2 | Bổ sung live monitoring, appeals và analytics còn thiếu | Endpoint + UI + dữ liệu thật + test cho từng mục trong baseline |
| Trước release | Chạy đủ ma trận online/thiết bị/NFR/UAT và traceability | Từng yêu cầu có case, build ID, môi trường và người xác nhận; không còn P0/P1 |

Các mức trên là ưu tiên xử lý đề xuất của đợt rà soát. Không suy ra mọi mục thiếu đều là lỗi vận hành hiện tại; nhiều mục là khoảng cách với phạm vi đã đề xuất.

## 6. Bằng chứng và cách chạy lại

Bản máy đọc: [system-checklist.json](../../artifacts/verification/progress-audit-20261027/system-checklist.json). Runner backend: [Run-BackendChecks.ps1](../../artifacts/verification/progress-audit-20261027/Run-BackendChecks.ps1). Kiểm tra admin bổ sung: [AdminAudit.cs](../../artifacts/verification/progress-audit-20261027/AdminAudit.cs).

IntegrationChecks tạo database `pkv_auth_checks_<random>` và Redis prefix ngẫu nhiên, rồi dọn trong finally. AdminAudit dùng database `pkv_progress_audit_<random>` riêng. Không chạy các cờ Unity fixture có thể tạo tài khoản trong DB dev. FastAPI fixture riêng 18081 đã dừng sau phép thử. Không thay dịch vụ AI thật hoặc các cờ tính năng đang chạy.

Các artifact dưới `artifacts/` có thể bị Git ignore. Khi bàn giao checklist cần giữ kèm JSON/log và manifest; chỉ giữ Markdown sẽ không đủ bằng chứng tái lập. Manifest ghi HEAD và hash của từng nguồn/bằng chứng được tham chiếu, bao gồm nội dung working tree chưa commit.

| Mã | Nguồn / bằng chứng | Dòng bắt đầu tham chiếu |
|---|---|---:|
| <a id="e-accountui"></a>ACCOUNTUI | [Account UI](../../My%20project/Assets/Scripts/FrontEnd/AccountScreen.cs) | 1 |
| <a id="e-admin"></a>ADMIN | [Admin service](../../src/Backend/Services/Implementations/AdminService.cs) | 15 |
| <a id="e-adminobs"></a>ADMINOBS | [Quan sát admin trên database tạm — chạy mới](../../artifacts/verification/progress-audit-20261027/admin-observations.json) | 1 |
| <a id="e-aiengine"></a>AIENGINE | [Technique rule engine](../../src/AiService/app/engine.py) | 1 |
| <a id="e-aihttp"></a>AIHTTP | [FastAPI service](../../src/AiService/app/main.py) | 1 |
| <a id="e-aireadme"></a>AIREADME | [AI model gate và giới hạn](../../src/AiService/README.md) | 22 |
| <a id="e-aitrain"></a>AITRAIN | [Classifier training pipeline](../../src/AiService/train.py) | 1 |
| <a id="e-audio"></a>AUDIO | [Music player](../../My%20project/Assets/Scripts/Audio/MusicPlayer.cs) | 1 |
| <a id="e-auth"></a>AUTH | [Auth service](../../src/Backend/Services/Implementations/AuthService.cs) | 1 |
| <a id="e-bauth"></a>BAUTH | [Auth 124 assertions — chạy mới](../../artifacts/verification/progress-audit-20261027/auth.log) | 1 |
| <a id="e-bbuild"></a>BBUILD | [Backend build mới](../../artifacts/verification/progress-audit-20261027/backend-build.log) | 1 |
| <a id="e-bconfig"></a>BCONFIG | [Configuration — có lỗi, dừng sớm](../../artifacts/verification/progress-audit-20261027/configuration.log) | 1 |
| <a id="e-bench"></a>BENCH | [Benchmark local mới](../../artifacts/verification/progress-audit-20261027/hybrid-http-benchmark.json) | 1 |
| <a id="e-bgame"></a>BGAME | [Online game 76 assertions — chạy mới](../../artifacts/verification/progress-audit-20261027/online-game.log) | 1 |
| <a id="e-bhttp"></a>BHTTP | [Hybrid HTTP 6 assertions — chạy mới](../../artifacts/verification/progress-audit-20261027/hybrid-http.log) | 1 |
| <a id="e-bhybrid"></a>BHYBRID | [Hybrid 32 assertions — chạy mới](../../artifacts/verification/progress-audit-20261027/hybrid.log) | 1 |
| <a id="e-binv"></a>BINV | [Room invitations 20 assertions — chạy mới](../../artifacts/verification/progress-audit-20261027/room-invitations.log) | 1 |
| <a id="e-bmp"></a>BMP | [Online/social 31 assertions — chạy mới](../../artifacts/verification/progress-audit-20261027/online-multiplayer-social.log) | 1 |
| <a id="e-bot"></a>BOT | [Behavior Tree bot](../../My%20project/Assets/Scripts/LocalMatch/BhTreeBot.cs) | 1 |
| <a id="e-botadapter"></a>BOTADAPTER | [Adaptive bot context](../../My%20project/Assets/Scripts/LocalMatch/AdaptiveBotContext.cs) | 1 |
| <a id="e-brank"></a>BRANK | [Activity/ranked 22 assertions — chạy mới](../../artifacts/verification/progress-audit-20261027/activity-ranked.log) | 1 |
| <a id="e-bredis"></a>BREDIS | [Redis 28 assertions — chạy mới](../../artifacts/verification/progress-audit-20261027/redis.log) | 1 |
| <a id="e-catalog"></a>CATALOG | [Game catalog loader](../../src/Backend/Services/Game/GameCatalogService.cs) | 17 |
| <a id="e-coachui"></a>COACHUI | [Coaching UI](../../My%20project/Assets/Scripts/FrontEnd/PlayerProgressionMenu.Training.cs) | 124 |
| <a id="e-collect"></a>COLLECT | [Online coaching collector](../../My%20project/Assets/Scripts/Multiplayer/NetworkMatch.Coaching.cs) | 1 |
| <a id="e-configsrc"></a>CONFIGSRC | [Configuration test expectations](../../src/Testing/Backend/ConfigurationChecks.cs) | 96 |
| <a id="e-contact"></a>CONTACT | [Paddle contact](../../My%20project/Assets/Scripts/LocalMatch/PaddleContact.cs) | 1 |
| <a id="e-criteria"></a>CRITERIA | [Tiêu chí tiến độ đã đề xuất](PickleVerse_Progress_Evaluation_Criteria.md) | 31 |
| <a id="e-d-hybrid"></a>D-HYBRID | [Hybrid implementation và giới hạn](../Hybrid_AI_Implementation.md) | 1 |
| <a id="e-d-inv"></a>D-INV | [Room invitation QA lịch sử](../Proccess/Friend_Room_Invitations_20261025.md) | 1 |
| <a id="e-d-mobile"></a>D-MOBILE | [Mobile QA lịch sử và giới hạn thiết bị](../Proccess/Galaxy_A32_Mobile_Fixes_20261020.md) | 1 |
| <a id="e-db"></a>DB | [Schema/catalog và cleanup — truy vấn mới](../../artifacts/verification/progress-audit-20261027/database-readonly.json) | 1 |
| <a id="e-deploy"></a>DEPLOY | [Development Compose](../../docker-compose.yml) | 1 |
| <a id="e-docker"></a>DOCKER | [Container status — đọc mới](../../artifacts/verification/progress-audit-20261027/docker-status.txt) | 1 |
| <a id="e-dtos"></a>DTOS | [Backend DTO contract](../../src/Backend/Services/DTOs/AllDTOs.cs) | 23 |
| <a id="e-elo"></a>ELO | [Elo policy](../../My%20project/Assets/Scripts/Progression/EloPolicy.cs) | 1 |
| <a id="e-fbuild"></a>FBUILD | [Frontend production build mới](../../artifacts/verification/progress-audit-20261027/frontend-build.log) | 1 |
| <a id="e-flags"></a>FLAGS | [Bốn cờ AI của container đang chạy](../../artifacts/verification/progress-audit-20261027/live-hybrid-flags.json) | 1 |
| <a id="e-guides"></a>GUIDES | [Hướng dẫn Training](../Proccess/PickleVerse_Training_Guide.md) | 1 |
| <a id="e-http"></a>HTTP | [HTTP readiness/auth/login contract — chạy mới](../../artifacts/verification/progress-audit-20261027/runtime-probes.json) | 1 |
| <a id="e-hybrid"></a>HYBRID | [Hybrid coaching backend](../../src/Backend/Services/Game/HybridCoachingService.cs) | 1 |
| <a id="e-inv"></a>INV | [Room invitation backend](../../src/Backend/Services/Game/RoomInvitationService.cs) | 1 |
| <a id="e-legacy"></a>LEGACY | [Legacy submit disabled](../../src/Backend/API/Controllers/TrainingController.cs) | 44 |
| <a id="e-matchapi"></a>MATCHAPI | [Player report API](../../src/Backend/API/Controllers/MatchesController.cs) | 47 |
| <a id="e-matchrepo"></a>MATCHREPO | [Recent match query](../../src/Backend/Repositories/Implementations/MatchRepository.cs) | 37 |
| <a id="e-move"></a>MOVE | [Di chuyển trong trận](../../My%20project/Assets/Scripts/LocalMatch/MatchMovement.cs) | 1 |
| <a id="e-mp"></a>MP | [Photon topology](../../My%20project/Assets/Scripts/Multiplayer/PhotonSession.cs) | 168 |
| <a id="e-mprule"></a>MPRULE | [Luật multiplayer](../../My%20project/Assets/Scripts/Multiplayer/PickleballRules.cs) | 9 |
| <a id="e-net"></a>NET | [Network match](../../My%20project/Assets/Scripts/Multiplayer/NetworkMatch.Photon.cs) | 1 |
| <a id="e-online"></a>ONLINE | [Online match backend](../../src/Backend/Services/Game/OnlineGameService.OnlineMatches.cs) | 1 |
| <a id="e-photon"></a>PHOTON | [Danh mục Photon smoke lịch sử cùng hash nguồn](../../artifacts/verification/progress-audit-20261027/historical-smoke-inventory.json) | 1 |
| <a id="e-phys"></a>PHYS | [Ball flight](../../My%20project/Assets/Scripts/LocalMatch/BallFlight.cs) | 1 |
| <a id="e-playerui"></a>PLAYERUI | [Player progression dashboard](../../My%20project/Assets/Scripts/FrontEnd/PlayerProgressionMenu.cs) | 1 |
| <a id="e-pose"></a>POSE | [Actor pose mixer](../../My%20project/Assets/Scripts/LocalMatch/ActorPoseMixer.cs) | 1 |
| <a id="e-program"></a>PROGRAM | [Backend HTTP/JWT pipeline](../../src/Backend/API/Program.cs) | 57 |
| <a id="e-progression"></a>PROGRESSION | [Progression engine](../../My%20project/Assets/Scripts/Progression/ProgressionEngine.cs) | 1 |
| <a id="e-project"></a>PROJECT | [Unity platform settings](../../My%20project/ProjectSettings/ProjectSettings.asset) | 202 |
| <a id="e-proposal"></a>PROPOSAL | [Proposal gốc](PickleVerse_Capstone_Proposal.md) | 26 |
| <a id="e-py"></a>PY | [Python 11/11 — chạy mới](../../artifacts/verification/progress-audit-20261027/python-tests.xml) | 1 |
| <a id="e-queue"></a>QUEUE | [Ranked search backend](../../src/Backend/Services/Game/OnlineGameService.RankedSearch.cs) | 1 |
| <a id="e-r2"></a>R2 | [Review 2 giới hạn tài liệu](../Review_2/README.md) | 1 |
| <a id="e-ready"></a>READY | [Ready policy](../../My%20project/Assets/Scripts/Entites/Multiplayer/MatchReadyPolicy.cs) | 1 |
| <a id="e-replay"></a>REPLAY | [Online replay backend](../../src/Backend/Services/Game/OnlineReplayService.cs) | 1 |
| <a id="e-room"></a>ROOM | [Room service](../../My%20project/Assets/Scripts/Multiplayer/Services/RoomService.cs) | 1 |
| <a id="e-rule"></a>RULE | [Luật singles](../../My%20project/Assets/Scripts/LocalMatch/Rules/SinglesRules.cs) | 34 |
| <a id="e-runtime"></a>RUNTIME | [Server-owned progression runtime](../../My%20project/Assets/Scripts/Progression/ProgressionRuntime.cs) | 19 |
| <a id="e-shop"></a>SHOP | [Online economy](../../My%20project/Assets/Scripts/Shop/OnlinePlayerEconomy.cs) | 1 |
| <a id="e-shot"></a>SHOT | [Shot input](../../My%20project/Assets/Scripts/LocalMatch/ShotInput.cs) | 1 |
| <a id="e-social"></a>SOCIAL | [Social backend](../../src/Backend/Services/Game/OnlineSocialService.cs) | 1 |
| <a id="e-spool"></a>SPOOL | [Durable telemetry spool](../../My%20project/Assets/Scripts/Progression/CoachingRuntime.cs) | 1 |
| <a id="e-sql"></a>SQL | [Schema SQL lesson reward JSONB](../../src/Database.sql) | 232 |
| <a id="e-streak"></a>STREAK | [Daily streak engine](../../My%20project/Assets/Scripts/Progression/DailyStreakEngine.cs) | 1 |
| <a id="e-testregister"></a>TESTREGISTER | [Review 2 test register — snapshot cũ](../Review_2/Test_Case_Register.csv) | 1 |
| <a id="e-touch"></a>TOUCH | [Mobile touch pad](../../My%20project/Assets/Scripts/LocalMatch/MobileTouchPad.cs) | 1 |
| <a id="e-train"></a>TRAIN | [Training director](../../My%20project/Assets/Scripts/LocalMatch/TrainingDirector.cs) | 34 |
| <a id="e-trainapi"></a>TRAINAPI | [Training backend](../../src/Backend/Services/Game/OnlineGameService.Training.cs) | 24 |
| <a id="e-trainmodel"></a>TRAINMODEL | [Training curriculum/engine](../../My%20project/Assets/Scripts/Progression/TrainingModels.cs) | 1 |
| <a id="e-transport"></a>TRANSPORT | [Unity API transport](../../My%20project/Assets/Scripts/Services/Api/UnityApiTransport.cs) | 1 |
| <a id="e-tutorial"></a>TUTORIAL | [Tutorial curriculum](../../My%20project/Assets/Scripts/LocalMatch/TutorialCurriculum.cs) | 20 |
| <a id="e-u"></a>U | [Unity EditMode 463/463 — chạy mới](../../artifacts/verification/progress-audit-20261027/unity-tests.json) | 1 |
| <a id="e-unitystate"></a>UNITYSTATE | [Trạng thái Editor trước/sau](../../artifacts/verification/progress-audit-20261027/unity-state.json) | 1 |
| <a id="e-webapi"></a>WEBAPI | [Admin API adapter](../../src/Frontend/src/api/adminApi.ts) | 1 |
| <a id="e-webauth"></a>WEBAUTH | [Admin auth context](../../src/Frontend/src/context/AuthContext.tsx) | 49 |
| <a id="e-webclient"></a>WEBCLIENT | [Admin HTTP client](../../src/Frontend/src/api/client.ts) | 1 |
| <a id="e-webcms"></a>WEBCMS | [Admin CMS UI](../../src/Frontend/src/features/content/pages/CMS.tsx) | 1 |
| <a id="e-webreadme"></a>WEBREADME | [Admin README và giới hạn](../../src/Frontend/README.md) | 39 |
| <a id="e-worker"></a>WORKER | [Hybrid job worker](../../src/Backend/API/Workers/HybridAiWorker.cs) | 1 |
| <a id="e-manifest"></a>MANIFEST | [Source snapshot SHA-256](../../artifacts/verification/progress-audit-20261027/source-manifest.json) | 1 |
