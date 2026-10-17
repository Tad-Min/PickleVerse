# Vừa di chuyển vừa đánh bóng

Cập nhật: 15/10/2026. Áp dụng cho nam/nữ trong BotMatch, MultiplayerCourt, Training và các bài thực hành cú đánh của Tutorial.

## Hành vi sau sửa

- Người chơi có thể bắt đầu đi/chạy, đổi hướng, đi chéo, lùi, dừng và tiếp tục di chuyển trong một cú đánh. Các lệnh này không hủy cú đánh hoặc đặt lại thời gian chạm bóng.
- Thân trên tiếp tục động tác vung vợt, chạm bóng và theo đà; chân dùng động tác đi/chạy/bước ngang/lùi theo hướng đang điều khiển.
- Đổi động tác chân được hòa trong 100 ms. Khi bắt đầu đánh từ tư thế đang di chuyển, lớp chân lấy tư thế đang hiển thị làm điểm bắt đầu.
- Nhịp chân dựa vào tốc độ cơ thể và giữ pha giữa các hướng. Sau khi theo đà xong, nhân vật hòa sang di chuyển toàn thân hoặc tư thế sẵn sàng.
- Giữ nguyên tốc độ di chuyển có trọng lượng trong lúc vung vợt: 60% tốc độ đi/chạy hiện có, có tăng/giảm tốc. Không khóa nhân vật tại chỗ.
- Bóng vẫn cần ở trong tầm vợt và đúng cửa sổ tiếp xúc. Chạy ra xa bóng vẫn có thể đánh hụt. Âm thanh/VFX chỉ xảy ra theo sự kiện chạm bóng; không thêm cú chạm giả vì nhấn nút.
- Đà volley vẫn được kiểm tra với Kitchen sau tiếp xúc; di chuyển không xóa nghĩa vụ đó.
- Các nút tùy chỉnh và joystick cảm ứng dùng chung cơ chế này.

## Cơ sở thiết kế

[Epic — Animation in Lyra](https://dev.epicgames.com/documentation/unreal-engine/animation-in-lyra-sample-game-in-unreal-engine) mô tả phân lớp chuyển động toàn thân và hành động thân trên; hệ thống này lấy cảm hứng từ Paragon/Fortnite. Nguyên tắc áp dụng ở đây là cho locomotion và hành động hoạt động đồng thời, với mặt nạ xương và trọng số hòa riêng.

[EA — FIFA 22 Gameplay Deep Dive](https://careers.ea.com/ea-play/news/pitch-notes-fifa-22-gameplay-deep-dive) mô tả phối hợp animation, tính phản hồi và đồng bộ chuyển động trong game thể thao. Với PickleVerse, quyết định thiết kế là giữ sự kiện chạm bóng độc lập với đổi hướng, thay vì dùng đổi hướng để ngắt toàn bộ cú vung.

Đây là cách áp dụng nguyên tắc vào bộ clip Legacy hiện có của Unity; không thay engine hoặc triển khai hệ thống animation của các game tham khảo.

## Triển khai

- `MatchActor.ObserveMovement` chỉ cập nhật hướng và đi/chạy. Loại bỏ cơ chế hủy cú vung khi phát hiện lệnh di chuyển mới và các nhánh giao bóng bị hủy do di chuyển.
- `ActorPoseMixer` có bộ đệm riêng cho hòa toàn thân và hòa chân. Lớp chân gồm Hips cùng hai nhánh chân; giữ hướng thân trên của clip đánh. Gait thay đổi trong cú vung không khởi động lại clip đánh.
- Khi bắt đầu cú đánh, lấy lại tư thế chân hiện tại để tránh dùng bộ đệm từ lần đổi hướng trước.
- Grip/IK của vợt chạy sau bước hòa tư thế. Giữ thời điểm chạm và phần theo đà cùng hệ thống SFX/VFX hiện có.
- Dự đoán vị trí cơ thể dùng cùng `MatchMovement` khi tính thời gian tiếp xúc và chọn thuận/trái tay. Tất cả chế độ dùng chung actor.
- Giữ riêng lịch sử trạng thái đang vung tại từng bước dự đoán di chuyển trong Multiplayer, để kết thúc cú đánh hiện tại không thay đổi cách phát lại các bước trước.

## Multiplayer

RPC đánh bóng mang snapshot hướng/đi-chạy cùng lệnh đánh. Gói di chuyển đến sau chỉ cập nhật locomotion. Loại bỏ RPC ngắt cú đánh theo di chuyển. Host và replica đều phối hợp thân trên/chân, giữ quy tắc server quyết định cú chạm.

Protocol hiện tại: **11**. Hai máy phải dùng bản build cùng protocol.

## Kiểm thử

Kết quả chi tiết: `UnityWork/MovingStrike`. Script tái kiểm tra nằm trong `My project/Tools/MovementBlendValidation.cs`, `MovementNetworkValidation.cs`, `MovementBlendVisuals.cs`.

| Phạm vi | Kết quả |
|---|---|
| EditMode | 344/344; gồm 7 kiểm tra lớp chân, giữ hướng thân trên/tay, đổi hướng liên tiếp và reset bộ đệm |
| Rig thật | 440 kiểm tra nam/nữ, hai đầu sân, đi/chạy/đổi hướng/dừng giữa cú vung; lấy mẫu 30/60/120 Hz |
| Tiếp xúc khi di chuyển | 240 tổ hợp cú đánh, hướng di chuyển và lấy đà thông thường/tối thiểu 100 ms; mỗi cú thành công phát VFX một lần |
| Replica | Kiểm tra chân và cú vung tiếp tục chạy trong khi vị trí root do mạng quản lý |
| Bộ đệm chân | 4.000 vòng capture/apply/đổi gait không cấp phát managed trong vòng đo |
| Cú đánh trước đó | 138 kiểm tra timing, pose, giao bóng và VFX |
| Training | 12 bài hoàn thành 3 sao, gồm bài di chuyển và đánh đôi |
| Tutorial | 32 kiểm tra camera, luật và thực hành cú đánh |
| NGO cục bộ | 20 kiểm tra tiếp xúc/giao bóng/luật; 15 kiểm tra đồng thời di chuyển và đánh với 4 nhân vật 2v2 |
| Hình ảnh | Render native các thời điểm trước di chuyển, đang vừa đi vừa đánh và đổi hướng trong cú đánh |

Kiểm thử sử dụng profile QA riêng và khôi phục profile ban đầu; không gửi vé tìm trận công khai. Multiplayer được kiểm tra bằng host NGO cục bộ. Chưa kiểm thử hai máy qua Internet hoặc điện thoại thật.

