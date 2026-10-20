# Phát hiện backend ngoài phạm vi xác thực đã duyệt

## P1 — Player truy cập được API quản trị

- Source: `src/Backend/API/Controllers/AdminController.cs`, attribute trên controller chỉ có `[Authorize]`.
- Kiểm tra đọc trên Docker dev ngày 20/10/2026: đăng nhập tài khoản fixture được tạo với role Player, gọi `GET /api/Admin/users`, nhận HTTP 200.
- Các action gán role/khóa tài khoản nằm cùng controller. Chưa gọi các action thay đổi dữ liệu để thử lỗi này.
- Patch đề xuất: đổi thành `[Authorize(Roles = "Admin")]` và kiểm tra chưa đăng nhập -> 401, Player -> 403, Admin -> 200 bằng fixture tạm; không seed tài khoản admin mặc định.
- Trạng thái: người dùng đã duyệt riêng; đã sửa và kiểm thử thực tế HTTP 401/403/200. Role của fixture được khôi phục sau kiểm thử, không tạo admin mặc định.

## Replay/S3 — dành cho đợt multiplayer

- `StorageService.GetFileUrl` dùng URL nội bộ container `http://s3-storage:9000`; Windows client không sử dụng trực tiếp hostname đó được.
- `UploadFileAsync` thêm GUID trước `replays/match_...json`, tạo key có prefix/thư mục. `ReplayService.GetReplayAsync` chỉ lấy `Path.GetFileName`, làm mất prefix khi đọc lại.
- `ReplaysController.SaveReplay` có `[Authorize]` nhưng chưa kiểm tra người gọi thuộc trận nào trước khi ghi đè replay của trận đó.
- Chưa thay đổi; cần xử lý quyền sở hữu và hợp đồng replay khi chốt Photon/S3.

Các phát hiện trên đối chiếu trực tiếp source repository; kiểm tra quyền admin chỉ thực hiện GET, không sửa dữ liệu người dùng.
