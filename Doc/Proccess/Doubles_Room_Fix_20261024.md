# Sửa phòng chờ online 2vs2 — 24/10/2026

## Lỗi tái hiện

- Khi Fusion đang tải scene đầu của phòng 2vs2, quay lại chọn chế độ tải scene Unity ở chế độ Single. Scene riêng của runner đang tạo chưa được theo dõi bởi `MultiplayerApp.Room`, nên bị dỡ quá sớm.
- Console báo `ArgumentException: DestinationScene is invalid`, sau đó `Failed to load prefab: Destination scene is not valid`.
- `PlayMultiplayerPresenter.Start()` chỉ gắn sự kiện cho BtnInvite của customSlots, bỏ sót duoSlots. Nút ở ô đồng đội hiển thị và nhận raycast nhưng không có xử lý click.

## Thay đổi

- `RoomOperationGate` theo dõi toàn bộ thao tác tạo/vào phòng; rời trang đợi thao tác này cùng phần cleanup hoàn tất trước khi chuyển scene.
- Kiểm tra generation/epoch sau các await để kết nối hoàn tất muộn không gắn lại phòng đã hủy.
- Tách đóng phòng đã gắn khỏi bước đợi thao tác đang chạy để không tự chờ chính mình.
- Bỏ đăng ký thông báo của phòng cũ trước khi chủ động đóng nó; không coi thông báo do chính mình gửi là host bất ngờ rời và hủy thao tác đổi chế độ.
- `PhotonSession.LeaveAsync()` trả cùng một Task cho mọi lời gọi, thay vì lời gọi thứ hai kết thúc ngay khi shutdown còn đang chạy.
- Gắn BtnInvite cho cả duoSlots và customSlots; nút chỉ tương tác khi có mã phòng và phòng sẵn sàng.

## Kiểm chứng

- Unity biên dịch thành công, không có lỗi C#.
- 3/3 kiểm thử RoomOperationGate đạt: đợi tạo/cleanup trước khi điều hướng, tạo thất bại vẫn rời/thử lại được, thao tác thứ hai không thay thế thao tác đang được chờ.
- Fusion thật: tái hiện đúng khoảng thời gian scene đang tải; sau bản sửa, điều hướng được trì hoãn, session bị hủy không gắn lại, runner được dọn sạch và không phát sinh lỗi scene/prefab.
- Tạo phòng doubles, chuyển doubles → singles → doubles, quay lại từ phòng đã tạo đều thành công.
- Hai lời gọi LeaveAsync đồng thời trả cùng Task và hoàn tất shutdown trước khi luồng đi tiếp.
- EventSystem raycast đến đúng BtnInvite của đồng đội; dispatch pointer click mở hộp thoại, sao chép đúng mã, khóa điều khiển nền và đóng để khôi phục điều khiển.
- Console sau kiểm tra: 0 Error/Exception; còn warning cổng UDP Visual Studio 56594 đã ghi nhận từ đợt trước. Log Fusion thêm player là log thông tin bình thường.

## Giới hạn

- Đã kiểm tra phòng chờ và vòng đời kết nối; chưa kiểm chứng một trận 2vs2 đủ bốn client trong đợt này.
- Hộp thoại mời hiện tại chia sẻ Room Code. Gửi lời mời trong game tới danh sách bạn bè, nhận lời mời và chấp nhận/từ chối vẫn là tính năng riêng chưa triển khai; không coi việc mở hộp thoại là đã hoàn thành tính năng đó.
- Bằng chứng chạy và ảnh nằm trong `UnityWork/DoublesFix/` (thư mục được Git bỏ qua); không lưu thông tin đăng nhập vào source.
