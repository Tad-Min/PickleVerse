# Bóng và HUD trong scene đấu bot

Đã cập nhật `My project/Assets/Scenes/BotMatch.unity`.

## Thay đổi

- Xóa object `BallShadow`. Object này vốn là đĩa hình trụ nhưng code cập nhật scale đồng đều cả ba trục, làm nó hiện thành khối đen dưới bóng.
- Thêm TrailRenderer màu vàng/xanh nhạt, thu nhỏ dần và mờ trong 0,18 s. Vệt xuất hiện khi bóng đang bay hoặc rơi sau khi thả giao bóng; không xuất hiện lúc giữ bóng, khi dead ball hoặc đặt lại điểm.
- Bóng lúc chuẩn bị giao đi theo vị trí bàn tay trái của người giao, cho cả nhân vật nữ và nam. Theo hiệu chỉnh mới nhất, tay giữ nguyên animation Idle gốc: đã xóa phần IK/xoay tay trái, chỉ dùng offset vị trí bóng so với bone bàn tay. Bóng rời vị trí tay tại mốc thả 0,5 s rồi rơi theo trọng lực đến contact.
- Thay thanh hướng dẫn lớn giữa đáy màn hình bằng ô 208 × 56 ở góc phải dưới, chỉ hiện phím và kỹ thuật đang chọn hoặc đang thực hiện, ví dụ `J + K` / `Drop`. Ô tự ẩn khi hết động tác; không chặn raycast lên các nút.
- Chừa khoảng trống cho ô này ở bên dưới nhóm nút cảm ứng. Hướng dẫn đầy đủ vẫn mở bằng F1.
- Pipeline dựng lại BotMatch áp dụng cùng cấu hình bóng/HUD qua `BallPresentationSetup.Configure`.

Các asset animation và hệ thống multiplayer không nằm trong phần chỉnh sửa này.

## Kiểm chứng

- Đã xem cận cảnh nữ và nam trong Play Mode. Bản hiệu chỉnh mới so sánh khớp LeftArm, LeftForeArm và LeftHand với clip Idle gốc: sai lệch rotation và position đều bằng 0. Người chơi và bot đều giao bóng hợp lệ sau khi bỏ phần chỉnh tay.
- Người chơi giữ/thả J, bấm nhanh J và bot đều giao bóng vào Rally, không có lỗi giao bóng.
- 12 tổ hợp đánh bóng qua parser → animation → contact: pass.
- HUD: ẩn lúc chờ, hiện khi chọn JK, giữ hiển thị trong swing, ẩn sau swing: pass.
- 29/29 test luật và điều khiển trong Edit Mode: pass.
- Trail được kiểm tra bằng quỹ đạo trong game và render quỹ đạo mẫu; bề rộng tại đầu vệt đo được khoảng 0,055 m.
- Scene đã lưu, chỉ còn một camera; camera cận cảnh QA không được lưu vào scene.

Backup trước sửa gồm cả scene trên đĩa và scene đang mở có thay đổi chưa lưu. Backup, JSON kiểm tra và hình ảnh nằm trong `UnityWork/BallPresentationFix`. Thư mục này được Git ignore theo cấu hình dự án.

Bằng chứng cho bản giữ nguyên Idle: `UnityWork/IdleHandBallFix/idle-pose-check.json`, `result.json` và `Images/`. Đây là bản thay thế pose giữ bóng bằng IK trước đó.

Trong Unity, menu **PickleVerse → Update ball and compact shot HUD** áp dụng lại cấu hình này cho scene BotMatch đang mở ở Edit Mode.
