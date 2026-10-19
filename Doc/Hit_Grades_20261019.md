# Phản hồi chất lượng cú đánh — 19/10/2026

Đã tích hợp cho BotMatch, Training, Tutorial và MultiplayerCourt. Nhân vật vẫn di chuyển và đánh đồng thời, sử dụng animation chân/thân trên đang có.

## Hai vùng tiếp xúc

- Vùng vợt giữ tầm bắt bóng đã có: tối đa 0,30 m cho cú thường, 0,16 m cho smash và 0,45 m cho giao bóng. Lõi chấm điểm bán kính 0,145 m cùng thời điểm trong ±30 ms được chấm **Hoàn hảo**; các tiếp xúc hợp lệ khác được chấm **Tốt**. Tiêu chí Hoàn hảo không được làm mất cú đánh hợp lệ.
- Vùng hỗ trợ: hộp trước nhân vật, tâm cục bộ `(0; 0,95; 0,64)` m, kích thước `(1,36; 1,40; 1,06)` m. Bóng cũng phải nằm trong tầm vợt tối đa 0,38 m và đúng cửa sổ đánh; không nhận bóng phía sau, dưới đất hoặc ngoài tầm. Smash dùng riêng tầm vợt 0,16 m đã hiệu chỉnh để không nhận bóng quá sớm trước khi vợt đưa lên cao.
- Cửa sổ tiếp xúc chung ±65 ms. Vùng hỗ trợ nhận bóng trong toàn bộ cửa sổ này, bao gồm phần trước nhịp tiếp xúc. Giao bóng dùng vùng vợt riêng, vì đường thả bóng underhand ở cạnh hông có thể nằm ngoài hộp phía trước. Khoảng thời gian và đường di chuyển của cả bóng, vợt, nhân vật đều được xét cùng nhau.
- Đây là hitbox tính toán trong bộ mô phỏng bóng hiện có, không thêm Collider cản bóng hoặc phụ thuộc vào OnTriggerEnter. Chọn MatchActor trong Scene View để xem hai vùng bằng Gizmos.

Chỉ cú chạm hợp lệ mới hiện đánh giá. Các lỗi Kitchen, giao bóng và luật thi đấu tiếp tục xử lý theo hệ thống hiện có. Chất lượng hiện là phản hồi, không tự tăng lực hoặc thay đổi quỹ đạo.

## Hình ảnh, animation và âm thanh

**Hoàn hảo** có chữ vàng cam với ánh kim lướt qua và VFX tiếp xúc vàng rõ hơn; **Tốt** dùng chữ xanh ngọc sáng. Chữ hiện khoảng 0,82 giây, không nhận raycast và nhường hiển thị cho thông báo trọng tài/bài học.

Phân loại trước khi áp dụng IK tiếp xúc. Bóng đổi hướng, pose tiếp xúc, VFX và âm thanh dùng cùng sự kiện xác nhận chạm. Tiếp tục dùng đúng SFX giao bóng/thường/tăng lực; không chồng thêm tiếng đánh thứ hai. UI và mesh VFX được dùng lại, không tạo texture, model hoặc audio mới.

Online: máy chủ chấm và gửi một byte đánh giá trong ContactClientRpc. Chỉ người sở hữu nhân vật thấy chữ; đối thủ/đồng đội không tạo thêm overlay. Host bỏ qua nhánh nhận RPC đã xử lý để tránh phát trùng hiệu ứng.

## Xác minh

- 29/29 kiểm thử PaddleContactTests: tiếp xúc nhanh, sai thời điểm, vùng trước/sau, giới hạn độ cao/tầm với và chuyển động ở hai đầu sân; thêm đường thả bóng thực tế từng gây lỗi, các thời điểm bắt bóng thông thường và chống mở rộng quá tầm smash.
- 396/396 tình huống nam/nữ, thuận/trái tay, sáu loại cú đánh, đứng/chạy ngang/chạy tới, nhịp mô phỏng 30/60/120 Hz và các nhịp swing ngắn/dài. Có cả Hoàn hảo và Tốt; xác minh một lần SFX/VFX, di chuyển vẫn hoạt động.
- 12/12 tổ hợp phím BotMatch; 12/12 bài Training đạt 3 sao; 32 kiểm tra Tutorial đạt, bao gồm chín tổ hợp đánh.
- 18 kiểm tra NGO host cục bộ ở 1v1/2v2 đạt: vùng chạm thật, phân loại, quyền hiển thị và chống phát trùng RPC. Chưa kiểm thử kết nối hai máy vật lý.
- 1.000 lần cập nhật hiệu ứng chữ: 0 byte managed phát sinh, tổng khoảng 11,36 ms trong Editor trên máy kiểm thử. Đây là chi phí phần cập nhật chữ, không phải cam kết FPS của toàn trận.
- Unity không có lỗi biên dịch/runtime hiện hành sau kiểm thử. Hồ sơ người dùng và các font đang sửa trước tác vụ được giữ nguyên.

Báo cáo chi tiết và ảnh: `UnityWork/HitGrades20261019/`. Công cụ kiểm tra trực tiếp: `My project/Tools/HitGradeValidation.cs`, `HitGradeOnlineValidation.cs`.

## Sửa lỗi tiếp xúc sau phản hồi người chơi

Tái hiện sau cinematic BotMatch: tại khoảng 0,808 giây của động tác giao, bóng ở tọa độ cục bộ `(-0,394; 0,389; 0,1042)` m, cách mặt vợt khoảng 0,24 m. Bóng trong tầm giao đã có nhưng nằm ngoài hộp phía trước, nên phiên bản đầu không phát bóng. Các kiểm thử ban đầu đặt bóng ở điểm tiếp xúc lý tưởng đã bỏ sót trường hợp này.

Bản sửa khôi phục tầm vợt cũ, dành vùng giao riêng và nhận vùng hỗ trợ trong toàn cửa sổ. Đánh giá Hoàn hảo/Tốt được giữ nguyên dưới dạng phản hồi. Trường hợp tái hiện sau sửa đã phát bóng, luật không báo lỗi.

Kiểm thử bổ sung theo khung hình thật: 36 ca giao thường/tăng lực/chéo, nam/nữ, hai đầu sân, hai sân, 30/60/144 FPS; 12 bài Training nhận bóng; chín tổ hợp Tutorial hoàn thành; rally tự chơi có chạm bóng liên tục. Báo cáo: `UnityWork/ContactRepair20261019/native-summary.json` và `unit-tests.json`. Công cụ mới `ContactRepairLiveValidation.cs` và `ContactRepairOnlineValidation.cs` kiểm tra từ input tới bóng được phát đi, không gọi ConsumeContact để giả lập kết quả thành công.

Host MultiplayerCourt cục bộ đạt 120/120 ca: 24 giao bóng và 96 cú rally qua 12 tổ hợp phím, 1v1/2v2, nam/nữ, hai đầu sân và cả hai sân. Kết quả kiểm tra lại 396 tình huống chuyển động/animation: 396 đạt, có 119 Hoàn hảo và 277 Tốt. Chưa kiểm thử hai máy qua mạng thật. Các phép thử dùng hồ sơ QA riêng và khôi phục hồ sơ người chơi sau khi kết thúc.

Sau hiệu chỉnh tầm smash, đã kiểm tra lại riêng bài smash chạy theo khung hình thật và 8/8 trường hợp smash online (1v1/2v2, nam/nữ, hai đầu sân), đều đạt. Tổng hợp cuối: `UnityWork/ContactRepair20261019/result.json`.
