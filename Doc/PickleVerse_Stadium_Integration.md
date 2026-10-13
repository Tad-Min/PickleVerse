# Tích hợp Stadium Enhanced

Ngày kiểm tra: 13/10/2026. Model nguồn: `Assets/Models/Stadium_Enhanced/Stadium_Enhanced.glb`.

## Các scene

Menu, BotMatch, MultiplayerCourt, Training và Tutorial đã dùng chung `Assets/LocalMatchContent/StadiumEnhanced/PlayableStadium.prefab`. Sân, khán đài, hàng rào, mái, cây và đèn thuộc bối cảnh cũ được gỡ khỏi các scene này. File GLB nguồn được giữ nguyên; các mesh hiệu chỉnh và mesh gộp nằm trong thư mục StadiumEnhanced.

## Căn tọa độ và luật

- Model đã có đúng kích thước 6,096 × 13,4112 m, với Kitchen sâu 2,1336 m mỗi phía.
- Đặt tâm sân tại X/Z = 0, trục dọc sân là Z. Hạ model 0,051 m, căn mặt thi đấu về Y = 0. Lớp Kitchen cao thêm 1 mm và vạch sơn cao 3 mm; phần chạy ngoài sân thấp hơn 8 mm để các lớp không chồng nhau khi render.
- Giữ nguyên engine luật: biên ngoài tại X = ±3,048 m, Z = ±6,7056 m, Kitchen tại Z = ±2,1336 m. BotMatch, Training, Tutorial và NetworkMatch dùng cùng tọa độ này.
- Hiệu chỉnh mesh lưới và băng trắng của model theo profile lưới trong engine: giữa sân 0,8636 m, tại biên 0,9144 m, khoảng cách trong hai cột 6,7056 m. Các đoạn collider lưới Multiplayer được căn theo cùng profile; mặt collider nền tại Y = 0. Bóng hiện dùng mô phỏng phân tích, không thêm collider thứ hai vào BotMatch/Training/Tutorial.
- Dấu sân, mục tiêu, vùng hồi vị và camera hướng dẫn được đối chiếu với sân mới. 12 bài luyện vẫn dùng kỹ thuật, input và chấm điểm hiện có.

## Ánh sáng

Quét mesh bằng các thành phần hình học nối nhau của vật liệu phát sáng, không đặt đèn theo ước lượng từ tên object:

- 8 cột Y, mỗi cột 5 đầu đèn: **40 spotlight** đặt dưới đúng mặt đèn.
- 6 hàng đèn mái thẳng, mỗi hàng 11 đèn, cộng 4 đoạn góc cong, mỗi đoạn 15 đèn: **126 spotlight**.
- Ban ngày: ánh sáng mặt trời và đèn cột Y; spotlight mái tắt, vật liệu đèn mái không phát sáng.
- Ban đêm: tắt sun, các mặt đèn Y và mái phát sáng. Trong Menu, các giá trị dim theo chu kỳ trời ngày/đêm. Sau lần tối ưu, 166 vị trí đèn được giữ lại nhưng nguồn chiếu sáng được gộp thành 8 nguồn Y và 16 nguồn mái; các nguồn mái hướng vào sân, intensity 110 và range 32 m.
- Chỉ 4 đầu đèn Y gần sân có bóng thời gian thực. Các đầu đèn còn lại chiếu sáng không tạo thêm shadow map, giúp tránh chi phí của 166 bản đồ bóng.

## Camera và nhân vật

- Hai nhân vật Menu đứng ở hai nửa sân, với root tại mặt thi đấu Y = 0. Vợt/bóng trưng bày vẫn nằm trong khung hình.
- Intro BotMatch dùng anchor bắt đầu ở Z = ±10,8 m, trong phần chạy ngoài sân của model, rồi đi về vị trí thi đấu. Đường quay nằm trong khoảng trống mái.
- Cảnh kết thúc dùng anchor Z = ±4,8 m. Giữ hành vi hiện có: quay về nhân vật người chơi sau khi thông báo chạy hết, hiện Thắng hoặc Thua đúng kết quả.

## Hiệu năng và kiểm tra

Gộp hình học tĩnh theo vật liệu và khu vực không đổi hình dạng, UV hay texture: từ **450 xuống 66 renderer**. Tất cả 5 scene chia sẻ mesh/material/prefab đã tạo, không tạo 5 bản model riêng. Các vật liệu mặt đèn được giữ riêng để điều khiển ngày/đêm.

Kiểm tra trên Unity 6000.6.2f1/Windows:

- 230 EditMode tests đạt.
- 63 kiểm tra scene/hình học/ánh sáng/HUD đạt sau tối ưu, gồm biên sân, Kitchen, profile băng lưới, nền và collider lưới Multiplayer, số fixture, ngày/đêm và không thiếu script.
- Cả 12 bài Training chạy bằng input, animation, tiếp xúc vợt và bóng tới kết quả 3 sao trên hồ sơ QA.
- Tutorial qua 32 kiểm tra, gồm diễn giải luật và tiếp xúc thực của 9 tổ hợp đánh.
- Intro được kiểm tra tại 8 mốc camera; nhân vật nằm trong khung và đường quay không đi vào mái/khán đài.
- Cảnh kết thúc qua 82 kiểm tra, gồm thắng/thua, hai phía sân, chờ thông báo, camera, animation, nút chơi lại/chọn mode.
- Menu sau khi quay về từ Training: mẫu 3 giây đạt trung bình 116,64 FPS ban ngày (P95 10,88 ms) và 92,97 FPS ban đêm (P95 11,22 ms), cùng cấu hình người dùng. Một số khung chậm ngắn xuất hiện ở mẫu ban ngày trong Editor; không coi đây là cam kết FPS tuyệt đối trên mọi thiết bị.

Báo cáo, vị trí đèn, backup scene và ảnh nằm trong `UnityWork/Stadium/`. Chưa kiểm tra trận online giữa hai thiết bị thật sau lần thay bối cảnh này; phần luật Multiplayer và hình học/collider đã được kiểm tra trong Editor.

Kết quả tối ưu bốn scene thi đấu và giới hạn của bài đo được ghi trong [PickleVerse_Stadium_Performance.md](PickleVerse_Stadium_Performance.md).
