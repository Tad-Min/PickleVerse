# Sách Hướng Dẫn Vận Hành Hệ Thống Quản Trị
## PickleVerse Administrator Guide

**Dự án:** PickleVerse – Multiplayer Pickleball Skill-Training and Match Game with AI Coaching  
**Phiên bản hệ thống:** 1.0.0  
**Ngày phát hành:** 2026-10-22  

---

## 1. Giới thiệu Tổng quan
Admin Web Dashboard là nền tảng quản trị tập trung dành cho Ban điều hành, Huấn luyện viên (Coach) và Kiểm duyệt viên (Moderator) của PickleVerse. Hệ thống cho phép:
- Quản lý tài khoản người chơi, phân quyền vai trò (Role-Based Access Control - RBAC), xử lý kỷ luật (Khóa/Mở khóa tài khoản), kích hoạt thủ công và reset điểm Elo.
- Hệ thống CMS quản lý giáo trình đào tạo: Tạo bài học (Lessons), bài tập kỹ thuật (Drills), đăng bán vợt/trang phục vào Shop và tạo nhiệm vụ sự kiện.
- Giám sát các trận đấu trực tuyến 1v1 Singles và 2v2 Doubles theo thời gian thực với cơ chế tự động cập nhật (Auto-polling).
- Thẩm định và xử lý các báo cáo vi phạm (Cheating, toxicity, AFK) từ người chơi.
- Trực quan hóa **Bản đồ nhiệt lỗi kỹ thuật AI (Technique-Error Heatmap)** dựa trên dữ liệu telemetry thu thập từ game client.

---

## 2. Hướng dẫn Khởi chạy Môi trường

### 2.1 Khởi chạy Backend API & Database
1. Khởi động Docker Desktop trên máy tính.
2. Khởi chạy cụm dịch vụ PostgreSQL, Redis, MinIO và Backend API:
   ```bash
   cd src/Backend
   docker compose up -d
   ```
   *Backend API mặc định lắng nghe tại: `http://localhost:5000` (hoặc cổng cấu hình trong `.env`).*

### 2.2 Khởi chạy Ứng dụng Quản trị Web (Admin Web)
1. Mở cửa sổ dòng lệnh tại thư mục Frontend:
   ```bash
   cd src/Frontend
   ```
2. Cài đặt các gói phụ thuộc (nếu chưa cài đặt):
   ```bash
   npm install
   ```
3. Chạy môi trường phát triển (Dev Server):
   ```bash
   npm run dev
   ```
4. Truy cập giao diện trên trình duyệt tại: **`http://localhost:5173`**

---

## 3. Quy trình Đăng nhập & Phân quyền Bảo mật (RBAC)

1. Mở trình duyệt và truy cập trang đăng nhập tại `/login`.
2. Nhập thông tin tài khoản được cấp quyền:
   - **Tài khoản / Email:** Nhập username hoặc email của quản trị viên.
   - **Mật khẩu:** Nhập mật khẩu bảo mật.
3. **Cơ chế kiểm soát an ninh (Zero-Trust RBAC):**
   - Nếu tài khoản có vai trò `Admin`, hệ thống cấp phát JWT token, lưu trữ phiên làm việc an toàn và điều hướng vào trang Dashboard.
   - Nếu tài khoản chỉ có vai trò `Player` (người chơi thông thường), hệ thống từ chối truy cập và hiển thị thông báo lỗi: *"Tài khoản của bạn không có vai trò Quản trị viên (Admin). Truy cập bị từ chối."*
   - Khi token hết hạn hoặc nhận mã lỗi `401 Unauthorized`/`403 Forbidden` từ API, hệ thống tự động xóa phiên và đưa người dùng về lại màn hình đăng nhập.

---

## 4. Hướng dẫn Vận hành các Phân hệ Chức năng

### 4.1 Phân hệ Quản lý Người dùng (`/users`)
* **Tìm kiếm & Phân trang:**
  - Nhập tên người chơi hoặc địa chỉ email vào thanh tìm kiếm ở đầu trang.
  - Sử dụng dropdown để lọc người dùng theo trạng thái (`ACTIVE`, `BANNED`, `UNVERIFIED`).
* **Gán vai trò Quản trị (Assign Role):**
  - Nhấn vào biểu tượng khiên bảo mật bên phải mỗi dòng người dùng.
  - Chọn vai trò mong muốn: `Player` hoặc `Admin`.
  - Nhấn **Xác nhận gán** để cập nhật quyền hạn ngay lập tức.
* **Xử lý kỷ luật Khóa tài khoản (Ban User):**
  - Nhấn vào biểu tượng cảnh báo màu đỏ bên cạnh người dùng cần kỷ luật.
  - Nhập bắt buộc **Lý do vi phạm** (VD: *Gian lận điểm Elo, sử dụng ngôn từ xúc phạm...*).
  - Chọn thời hạn khóa cụ thể (hoặc để trống nếu muốn khóa vĩnh viễn).
  - Nhấn **Xác nhận khóa**. Trạng thái người dùng sẽ chuyển sang `BANNED` và người chơi sẽ bị ngắt kết nối khỏi game.
* **Mở khóa tài khoản (Unban User):**
  - Đối với các tài khoản đang ở trạng thái `BANNED`, nhấn nút **Mở khóa** màu xanh để phục hồi quyền truy cập.
* **Kích hoạt tài khoản & Reset Elo:**
  - Với tài khoản chưa xác thực email (`UNVERIFIED`), nhấn biểu tượng dấu tích xanh để kích hoạt tài khoản thủ công.
  - Nhấn biểu tượng xoay để đưa điểm Elo về mức mặc định (2000).

### 4.2 Phân hệ Quản trị Nội dung đào tạo & Shop (`/cms`)
Phân hệ gồm 4 thẻ (Tabs) riêng biệt:
1. **Thêm Bài học (Lesson):**
   - Nhập tên bài học, mô tả giáo trình, cấp độ mở khóa (Level) và thứ tự hiển thị.
   - Nhấn **Lưu bài học mới** để đồng bộ vào cơ sở dữ liệu.
2. **Thêm Bài tập kỹ thuật (Drill):**
   - Chọn Lesson ID tương ứng và nhóm kỹ thuật AI (*Serve, Dink, Volley, Third-Shot Drop, Kitchen*).
   - Thiết lập số lần mục tiêu cần đạt (Target) và hướng dẫn thực hiện.
3. **Đăng bán Vật phẩm vào Shop (Cosmetics):**
   - Nhập mã Item Code (VD: `PADDLE_TITANIUM_2026`), tên hiển thị, giá bán và chọn loại tiền tệ (*Gold* hoặc *Gem*).
4. **Tạo Nhiệm vụ (Missions):**
   - Thiết lập nhiệm vụ hàng ngày (`DAILY`) hoặc hàng tuần (`WEEKLY`), số lần yêu cầu và phần thưởng nhận được.

### 4.3 Phân hệ Giám sát Trận đấu thời gian thực (`/matches`)
* **Cơ chế Auto-polling:**
  - Thanh công cụ phía trên hỗ trợ công tắc bật/tắt tự động làm mới sau mỗi 10, 15 hoặc 30 giây.
  - Hệ thống tự động phát hiện khi tab trình duyệt bị ẩn (`visibilitychange`) và tạm dừng polling để tiết kiệm tài nguyên máy chủ.
* **Nút bấm làm mới thủ công:**
  - Nhấn vào nút xoay để tải lại danh sách 50 trận đấu gần nhất ngay lập tức.
* **Xem Biên bản trận đấu:**
  - Nhấn vào biểu tượng con mắt ở dòng trận đấu tương ứng để mở modal xem chi tiết tỷ số các set, thời gian thi đấu và tên người chiến thắng.

### 4.4 Phân hệ Hội đồng Xử lý Vi phạm (`/moderation`)
* **Lọc báo cáo vi phạm:**
  - Lọc theo các trạng thái: `PENDING` (Chờ xử lý), `RESOLVED` (Đã duyệt vi phạm), `DISMISSED` (Đã bác bỏ).
* **Quy trình Thẩm định:**
  1. Nhấn nút **Thẩm định** tại báo cáo cần xem xét.
  2. Đọc lý do phản ánh từ người tố cáo và kiểm tra mã trận đấu liên quan.
  3. Chọn phán quyết:
     - **Duyệt vi phạm:** Xác nhận hành vi xấu của người bị tố cáo. Có thể tích chọn *"Áp dụng chế tài: Khóa tài khoản người chơi này"* và nhập lý do khóa.
     - **Bác đơn:** Đánh dấu báo cáo không hợp lệ hoặc không đủ bằng chứng.
  4. Nhấn **Hoàn tất thẩm định** để lưu biên bản xử phạt.

### 4.5 Phân hệ Thống kê & Bản đồ nhiệt AI (`/analytics`)
* **Bản đồ nhiệt lỗi kỹ thuật (Technique-Error Heatmap):**
  - Trực quan hóa ma trận lỗi của 5 kỹ thuật chính (*Serve, Dink, Volley, Third-Shot Drop, Kitchen*).
  - Phân loại lỗi theo mức độ nghiêm trọng bằng mã màu tương phản:
    - **Xanh lục đậm:** Lỗi thấp ($\le 9$ lần).
    - **Vàng cam:** Lỗi trung bình ($10 - 24$ lần).
    - **Đỏ rực nhấp nháy:** Cảnh báo điểm nóng kỹ thuật ($\ge 25$ lần) cần huấn luyện bổ sung.
* **Biểu đồ cột xu hướng lỗi:**
  - Thống kê tỷ trọng lỗi giữa các kỹ thuật để đánh giá độ khó và cân bằng lại gameplay.

---

## 5. Xử lý Sự cố Thường gặp (Troubleshooting)

| Hiện tượng | Nguyên nhân | Biện pháp khắc phục |
|---|---|---|
| Đăng nhập bị báo lỗi "Tài khoản không có vai trò Quản trị viên" | Tài khoản đang có RoleId = 1 (Player) | Sử dụng pgAdmin hoặc kết nối DB đổi `role_id = 2` (Admin) trong bảng `user_roles`. |
| Gọi API bị lỗi Network Error / CORS blocked | Backend chưa cho phép origin của web hoặc chưa khởi động | Đảm bảo Backend API đang chạy trên port 5000 và đã cấu hình policy `AdminWebPolicy` trong `Program.cs`. |
| Màn hình tự động văng ra trang Login | Token JWT đã hết hạn hoặc không hợp lệ | Đăng nhập lại để nhận token mới. |
| Danh sách trận đấu không tự cập nhật | Tab trình duyệt đang bị ẩn hoặc công tắc Auto-polling bị tắt | Nhấn chuyển lại tab hoặc bật lại công tắc Auto-polling trên thanh công cụ. |
