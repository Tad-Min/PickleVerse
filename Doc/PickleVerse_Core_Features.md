# PickleVerse (PKV)
**Mô tả các chức năng cốt lõi của hệ thống**

## 1. Tổng quan
PickleVerse là game pickleball 3D trên máy tính và điện thoại. Người chơi học các kỹ thuật cơ bản qua bài tập có chấm điểm, rồi áp dụng ngay vào trận đấu với bạn bè hoặc với bot. Hệ thống có AI cơ bản để chấm trình độ, ghép trận và chỉ ra lỗi kỹ thuật, cùng một website để quản trị viên vận hành.

Hệ thống gồm ba phần:
* **Game cho người chơi:** học, luyện tập và thi đấu.
* **AI cơ bản:** bot đối thủ, xếp hạng, phản hồi kỹ thuật.
* **Website quản trị:** quản lý người dùng, nội dung và theo dõi hoạt động.

## 2. Game cho người chơi

| Tính năng | Làm được gì | Vì sao cốt lõi |
| :--- | :--- | :--- |
| **Tài khoản và hồ sơ** | Đăng ký, đăng nhập, lưu hồ sơ, tiến trình và lịch sử chơi. | Mọi tính năng khác cần biết người chơi là ai. |
| **Hướng dẫn nhập môn** | Dạy luật pickleball (đặc biệt khu vực kitchen) và cách điều khiển trong game. | Người mới có thể chơi được ngay. |
| **Bài học và drill kỹ thuật** | Luyện từng kỹ thuật: giao bóng, dink, volley, third-shot drop. Mỗi drill chấm 1 đến 3 sao, cộng XP và mở khóa bài mới. | Đây là phần "học" của đề tài. |
| **Chơi với bot** | Đánh trận với bot, chọn mức độ khó. | Luyện tập bất cứ lúc nào, không cần đối thủ thật. |
| **Trận trực tuyến 1v1 và 2v2** | Đánh đơn hoặc đánh đôi với người khác qua mạng. Mời bạn bằng mã phòng hoặc vào hàng chờ ghép trận nhanh. | Đây là phần "chơi cùng nhau" và là thách thức kỹ thuật chính (đồng bộ thời gian thực). |
| **Bảng xếp hạng và tiến trình** | Xem thứ hạng, lịch sử trận đấu, mức thành thạo từng kỹ thuật và huy hiệu. | Cho người chơi thấy mình tiến bộ để muốn quay lại. |

## 3. Tính năng AI cơ bản

| Tính năng | Làm được gì | Vì sao cốt lõi |
| :--- | :--- | :--- |
| **Bot đối thủ thích ứng** | Bot tự điều chỉnh cách đánh và tỷ lệ mắc lỗi theo trình độ người chơi (dùng behavior tree). | Bot quá dễ hoặc quá khó đều làm người chơi chán. |
| **Xếp hạng và ghép trận** | Tính điểm kỹ năng kiểu Elo sau mỗi trận, ghép những người cùng trình độ. Người mới được ước lượng hạng khởi điểm. | Giải quyết việc khó tìm bạn chơi cùng trình độ. |
| **Phản hồi kỹ thuật** | Phân tích dữ liệu trong game (thời điểm đánh, vị trí tiếp bóng, di chuyển) để nhận ra lỗi như giao bóng trễ, dink quá cao, bước vào kitchen, rồi đưa lời khuyên cụ thể. | Thay thế huấn luyện viên, là điểm khác biệt của đề tài. |
| **Gợi ý bài tập** | Đề xuất drill tiếp theo dựa trên kỹ thuật người chơi còn yếu. | Nối phần phản hồi với phần luyện tập thành một vòng học. |

## 4. Website quản trị

| Tính năng | Làm được gì | Vì sao cốt lõi |
| :--- | :--- | :--- |
| **Quản lý người dùng** | Tìm tài khoản, khóa hoặc cấm, đặt lại tiến trình. | Giữ cộng đồng lành mạnh. |
| **Quản lý nội dung** | Tạo, sửa, xuất bản hoặc gỡ bài học, drill và độ khó. | Cập nhật nội dung mà không cần sửa code. |
| **Giám sát trận đấu** | Xem trận đang diễn ra và đã kết thúc, xử lý sự cố mất kết nối. | Vận hành hệ thống multiplayer. |
| **Xử lý báo cáo vi phạm** | Duyệt người chơi bị báo cáo và giải quyết khiếu nại. | Kiểm soát hành vi xấu. |
| **Thống kê** | Số người dùng hoạt động, tỷ lệ quay lại, bài học phổ biến, lỗi kỹ thuật hay gặp. | Giúp quản trị viên cải thiện sản phẩm. |

## 5. Tính năng mở rộng (làm nếu còn thời gian)
* Danh sách bạn bè và thách đấu trực tiếp.
* Daily challenge và phần thưởng chuỗi ngày liên tiếp.
* Chế độ offline cho phần luyện tập.

> **Ngoài phạm vi đề tài:** giải đấu, chế độ khán giả, mua vật phẩm qua MoMo/VNPay và phân tích video thật.