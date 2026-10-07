# Multiplayer PickleVerse (PC test)

## Chạy

- Mở `Assets/Scenes/MultiplayerTest.unity`, Play, chọn tên/hồ sơ test và Male/Female.
- Mỗi phiên chạy dùng một hồ sơ khác nhau. Build mặc định lấy PID làm hồ sơ; có thể truyền `-pkvProfile Player1`.
- Nút `BtnPlay` trong Menu mở phòng 1vs1. Chuyển 2vs2 trong phòng; nhóm hai người khóa chuyển về 1vs1.
- Bấm Tìm trận: đồng hồ tăng từ 00:00; chỉ gửi tìm từ giây 6. Hủy dùng được khi chờ, tìm và kết nối. Hết 120 giây tìm thực sự thì thử lại bằng một lượt mới.
- Chia sẻ mã phòng 2vs2 để ghép cùng bạn. Chủ nhóm bắt đầu; cả hai hủy được. Custom chọn đội, tất cả Sẵn sàng rồi chủ phòng bắt đầu.
- Profile Windows: `Assets/Settings/BuildProfiles/MultiplayerWindows.asset`. Build tới `Builds/Multiplayer/PickleVerse.exe`.

## Điều khiển

| Phím | Hành động |
|---|---|
| WASD / mũi tên | Di chuyển theo phía sân của mình |
| Shift / Space | Chạy |
| J | Groundstroke, hoặc giao bóng khi tới lượt |
| K | Dink |
| L | Volley |
| J+K | Drop |
| J+L | Smash đứng |
| K+L | Reset volley (thử nghiệm) |
| U+J / U+K / U+L | Drive / Speed-up / Punch (hai cú sau thử nghiệm) |
| I + phím đánh | Hướng chéo sân |

Giữ tổ hợp rồi nhả một phím đánh để chốt. Ba phím đánh cùng lúc hoặc U+I hủy cú đánh. U/I riêng lẻ không đánh. Modifier mặc định dùng một lần; checkbox trong phòng đổi sang giữ phím.

## Cấu hình và kiến trúc

- Cấu hình: `Assets/Resources/MultiplayerSettings.asset`. Môi trường `pickleverse-test`, hai hàng chờ `PickleVerse-1v1` / `PickleVerse-2v2` đã được triển khai (xem `MultiplayerCloudDeployment.txt`).
- QoS đo trước khi gửi ticket, giới hạn 150 ms. Nhóm chọn vùng chung có ping tốt nhất; hàng chờ yêu cầu cùng vùng, không tự nới ngưỡng.
- `MultiplayerApp`: facade cho UI, phòng nhóm/custom, tìm/hủy, kết nối, trở về phòng và reconnect.
- `GroupMatchmaker`: ticket nguyên nhóm một/hai người cho hàng chờ 2vs2; xác minh hai bạn cùng đội.
- `NetworkPlayer`: input chủ sở hữu, dự đoán và sửa vị trí từ host, animation và socket vợt.
- `NetworkMatch` / `PickleballRules`: bóng vật lý ở host, trạng thái đồng bộ, lượt giao/nhận, hai lần nảy, kitchen/đà volley, lỗi, điểm chỉ đội giao, 11 điểm cách biệt 2.
- Host rời kết thúc phiên. Khách mất kết nối tạm dừng tối đa 30 giây, cùng danh tính có thể nối lại; quá hạn hoặc chủ động rời bị xử thua.
- Transport dùng heartbeat 500 ms, nhận diện mất dữ liệu sau 5 giây; khoảng chờ nối lại 30 giây bắt đầu khi host nhận diện mất kết nối.
- Phòng nhóm và trận tách riêng; sau trận bấm Về phòng để giữ nhóm. Custom xóa Sẵn sàng.
- Animation dùng controller Male/Female hiện có, Idle_Ready mặc định. Các marker đo từ clip được lưu trong `ShotAnimations.asset` và báo cáo `MultiplayerAnimationCalibration.json`.

## Kiểm tra

- EditMode assembly `PickleVerse.Multiplayer.Tests`: 25 ca kiểm thử thực đã qua; báo cáo `MultiplayerRulesTestResults.json`.
- Đã kiểm tra trong Play Mode: đăng nhập, Play mở 1vs1, chuyển 2vs2/tạo mã, hủy tại 00:02, tìm thực sự tại 00:07 và hủy về phòng.
- Kết quả nhiều phiên Windows, reconnect và vòng đời trận được ghi trong `MultiplayerWindowsTestResults.md`.
- Máy thứ hai qua Internet cần chạy cùng build/môi trường; chưa được coi là đã kiểm tra nếu chỉ chạy nhiều phiên trên một máy.

Các script trong `Tools` phục vụ kiểm tra Editor và không nằm trong Player. `MultiplayerSmokeRunner` chỉ hoạt động khi truyền `-pkvCase`; không tự động chơi trong phiên thông thường.

Ảnh UI và sân đã kiểm tra: `MultiplayerScreenshots/Room.png`, `MultiplayerScreenshots/Court.png`.

`SessionNetworkHandler` dùng giao diện công khai `INetworkHandler` của MPS, giữ phiên dịch vụ khi nối lại và lấy allocation Relay mới. Phần đọc `RelayJoinCode` từ metadata phiên được tách trong adapter, dành cho SDK đang cố định ở 2.3.3; cần kiểm tra adapter khi nâng SDK.
