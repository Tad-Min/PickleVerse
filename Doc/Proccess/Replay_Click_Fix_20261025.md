# Sửa click Xem replay — 25/10/2026

## Nguyên nhân và thay đổi

`PlayerProgressionMenu.AddHistory` tạo nút `WatchReplay` bằng helper `Panel`, mặc định đặt `Image.raycastTarget=false` cho các panel trang trí. Phần chữ cũng không nhận raycast nên khi bấm vào vùng Replay, EventSystem chọn Button của hàng lịch sử và mở Detail.

Đặt `raycastTarget=true` riêng cho Image của `WatchReplay` và gán Image này làm `Button.targetGraphic`. Giữ nguyên callback Replay và callback Detail của hàng. Thao tác này cũng được áp dụng khi hàng lịch sử được lấy lại từ pool.

## Xác minh trong Unity Editor

- Script biên dịch thành công, không có lỗi biên dịch.
- Chạy Play Mode với lịch sử thật của tài khoản được cung cấp; không tạo hoặc sửa bản ghi trận đấu.
- `EventSystem.RaycastAll` tại ba điểm trái/giữa/phải của mỗi nút đều chọn `WatchReplay` cho cả trận hoàn tất và trận chưa hoàn tất.
- Gửi `IPointerClickHandler` đến đối tượng do EventSystem chọn: vùng tiêu đề vẫn mở/đóng Detail, chiều cao hàng lần lượt 158/112; click Replay giữ chiều cao 112 và chuyển sang scene Replay.
- Bản ghi hoàn tất `9cee8a3f7f97420d869037d801b6e36d` tải được qua online repository, có 122 frame trong bộ đệm ban đầu. Đã kiểm tra pause, seek đến giữa trận (122,598 giây), tốc độ 2×, tiếp tục phát và tỉ số.
- Nút trở về khôi phục Menu/lịch sử. Sau đó hàng được tái sử dụng mở được bản ghi chưa hoàn tất `f7cdd31c243845c38f36203adb980d7c`, có 122 frame trong bộ đệm ban đầu, rồi trở lại lịch sử thành công.

Nhật ký kiểm tra trực tiếp nằm ở `UnityWork/ReplayClick/live-validation.txt` (thư mục làm việc được Git bỏ qua). Các bước trên kiểm tra đường nhận click của Unity và luồng dữ liệu thật; không thay thế kiểm thử toàn bộ thời lượng của mọi bản ghi hoặc toàn bộ kho S3.
