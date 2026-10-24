# Mời bạn bè vào phòng — 25/10/2026

## Hành vi

- Nút Mời bạn ở phòng Doubles/Custom mở danh sách bạn bè đã kết bạn, có ô tìm theo tên hoặc ID, tag tên/trạng thái và nút Mời từng người.
- Danh sách phân biệt đang ở lobby, đang bận, offline và đã ở trong phòng; nút Đã gửi ngăn gửi trùng.
- Người nhận ở Menu, Chọn chế độ hoặc phòng chờ nhìn thấy thông báo tên người mời, chế độ và thời gian còn lại, có Chấp nhận/Từ chối.
- Chấp nhận tự tham gia đúng phòng và region Fusion, rồi đóng thông báo. Người nhận không nhập mã thủ công.
- Bạn bè phải đang ở lobby. Trong lúc tìm trận, tải sân, luyện tập hoặc thi đấu không nhận lời mời vào phòng khác.
- Lời mời hết hạn sau 60 giây. Presence/lời mời cập nhật qua REST khoảng 3 giây, lease online 20 giây. Phòng đóng, đầy, người gửi đổi phiên hoặc quan hệ bạn bè không còn hợp lệ thì không thể nhận lời mời.

## Triển khai

- Không thêm bảng Rooms. PostgreSQL tiếp tục quản lý Friendships/BlockedUsers; Redis lưu presence, lease phòng và inbox lời mời có TTL.
- API có xác thực tại `api/Game/lobby`: POST presence, GET friends, POST invitations và POST invitations/{id}/respond.
- Backend dùng danh tính từ JWT, tên từ hồ sơ máy chủ, kiểm tra bạn bè, trạng thái lobby, quyền nhận, chỗ trống và hạn lời mời. Gửi trùng và thay đổi quyết định được xử lý nguyên tử trong Redis; một tài khoản khác không ghi đè đăng ký phòng đang hoạt động.
- `RoomInvitationsRuntime` tồn tại qua các scene, cập nhật presence và thông báo nhận lời mời; `RoomFriendInvitePanel` thay nội dung hộp thoại cũ bằng danh sách tìm kiếm, vẫn giữ giao diện phòng và mã join thủ công ở khu vực riêng.
- Mở phòng từ lời mời giữ region qua RoomLaunchRequest, dùng luồng tạo/join có kiểm soát vòng đời từ đợt sửa 2vs2 trước.
- Đã build và cập nhật riêng container backend-api local, giữ nguyên các volume PostgreSQL, Redis và S3.

## Kiểm chứng

- Backend: 20/20 kiểm tra trên PostgreSQL tạm và Redis namespace riêng; gồm tìm tên tiếng Việt, bạn bè hợp lệ, gửi trùng, quyền nhận, busy, accept/decline, idempotency, phòng đầy/đóng, host đổi phiên, hết hạn và presence hết lease.
- Unity: 9/9 kiểm thử RoomLaunchRequest, gồm bảo toàn region qua scene và xóa ý định cũ khi tạo Custom.
- Live: tài khoản chính và Test1 đăng nhập qua API thật, dùng hai NetworkRunner Fusion trong cùng Editor.
- Chiều gửi: tìm Test1 trong picker, click nút Mời qua raycast/EventSystem, tài khoản còn lại nhận đúng tên/phòng và accept để tham gia party Doubles thật.
- Chiều nhận: Test1 tạo phòng Custom Doubles và gửi mời ngược lại; thông báo hiện trong UI, click Chấp nhận qua EventSystem, tham gia đúng phòng/region và đồng bộ hai thành viên; thông báo tự đóng.
- Phép thử không phát sinh Error/Exception Unity. Có warning disconnect ServerLogic trong quá trình dọn các peer thử nghiệm; không coi toàn bộ warning môi trường/Fusion đã được giải quyết bởi tính năng này.
- Các session thử nghiệm và quan hệ bạn bè tạo riêng cho phép thử đã được dọn. Không thay đổi Elo hoặc kết quả trận đấu.
- Bằng chứng và ảnh trong `UnityWork/RoomInvitations/` (Git bỏ qua). Kiểm tra này không thay thế kiểm thử đa thiết bị hoặc trận đấu đủ bốn người.
