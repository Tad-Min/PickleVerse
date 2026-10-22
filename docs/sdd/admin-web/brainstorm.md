# Brainstorm: Admin Web Dashboard (PickleVerse)

**Project Scope**: `Full-Stack`  
*Admin Web Dashboard là ứng dụng web quản trị hoàn chỉnh kết nối trực tiếp với Backend API ASP.NET Core (`AdminController.cs`), cơ sở dữ liệu PostgreSQL (`Database.sql`), dịch vụ xác thực JWT và telemetry AI để quản lý người dùng, nội dung đào tạo, giám sát trận đấu, kiểm duyệt vi phạm và trực quan hóa phân tích.*

**Status**: Approved

---

## Problem
1. **Thiếu công cụ quản trị tập trung:** Ban quản trị dự án PickleVerse chưa có giao diện trực quan (GUI) để quản lý tài khoản người chơi, xử lý các trường hợp gian lận hoặc vi phạm quy tắc ứng xử mà phải can thiệp trực tiếp vào database qua câu lệnh SQL hoặc API tools (Swagger/Postman).
2. **Cập nhật nội dung đào tạo thủ công:** Khi cần thêm bài học (Lessons), bài tập (Drills), cập nhật ngưỡng đạt sao hoặc tham số độ khó kỹ thuật, hiện tại phải sửa dữ liệu cứng hoặc nạp file seed, chưa có luồng CRUD trực quan cho đội ngũ thiết kế nội dung (Game Designer / Coach).
3. **Mù thông tin vận hành trận đấu:** Không có bảng theo dõi các trận 1v1 / 2v2 thời gian thực, khó phát hiện tình trạng giật lag, rớt mạng hoặc xử lý các báo cáo lạm dụng phát sinh trong trận đấu.
4. **Dữ liệu AI Telemetry chưa được khai thác:** Dữ liệu phân tích cú đánh từ AI (giao bóng trễ, dink quá cao, phạm luật kitchen) được ghi nhận trong hệ thống nhưng chưa có biểu đồ nhiệt (Heatmap) và thống kê để ban huấn luyện / admin đánh giá người chơi đang gặp khó khăn ở kỹ thuật nào nhiều nhất.

---

## Goals
1. **Xác thực & Phân quyền bảo mật (RBAC):** Cung cấp cổng đăng nhập an toàn dành riêng cho Quản trị viên, bảo vệ các endpoint quản trị bằng JWT role guard (`Admin`).
2. **Quản lý người dùng toàn diện:** Tìm kiếm, tra cứu tài khoản, kích hoạt email thủ công, khóa/mở khóa tài khoản (Ban/Unban có lý do và thời hạn), phân quyền vai trò và reset điểm Elo/tiến trình.
3. **Hệ thống Quản lý nội dung (CMS):** Giao diện tạo mới và cập nhật bài học, drill kỹ thuật, nhiệm vụ sự kiện và vật phẩm thời trang mà không cần chạm vào mã nguồn game client.
4. **Giám sát trận đấu & Xử lý vi phạm:** Xem danh sách trận đấu gần đây, tiếp nhận và xử lý báo cáo vi phạm (AFK, toxic, nghi vấn cheat), lưu vết biên bản xử phạt.
5. **Analytics & AI Telemetry Heatmap:** Trực quan hóa số liệu người dùng (DAU, số trận, tỷ lệ hoàn thành bài tập) và bản đồ nhiệt lỗi kỹ thuật pickleball do AI phát hiện.

---

## Success metrics and acceptance signals
1. **Thời gian thao tác quản trị:** Giảm thời gian khóa tài khoản vi phạm hoặc tạo bài tập drill mới xuống dưới 30 giây qua giao diện web.
2. **Bảo mật phân quyền:** 100% người dùng chưa đăng nhập nhận HTTP 401, tài khoản có role `Player` nhận HTTP 403 khi cố truy cập trang hoặc API quản trị; chỉ role `Admin` mới được cấp quyền truy cập.
3. **Khả năng trực quan hóa dữ liệu:** Bản đồ nhiệt lỗi kỹ thuật (Technique-Error Heatmap) và biểu đồ thống kê tải trong < 1.5 giây với dữ liệu lên tới hàng nghìn lượt ghi nhận telemetry.
4. **Trải nghiệm vận hành (UX):** Giao diện responsive trên Desktop/Tablet, phản hồi tức thời (optimistic update hoặc loading state rõ ràng), thông báo toast xác nhận thành công/lỗi rõ ràng.

---

## Non-goals (explicitly out of scope)
1. **Không can thiệp vào mô phỏng vật lý bóng thời gian thực từ web:** Admin Web chỉ đọc dữ liệu trận đấu và ghi nhận biên bản, không can thiệp điều khiển trái bóng hay can thiệp trực tiếp phiên chơi trên game server.
2. **Không tích hợp cổng thanh toán MoMo/VNPay trong đợt này:** Việc nạp tiền mua vật phẩm thật nằm ngoài phạm vi đề tài capstone (đã ghi rõ trong Proposal).
3. **Không xây dựng phân tích video thực tế (Computer Vision qua camera thật):** Toàn bộ phân tích kỹ thuật dựa trên dữ liệu telemetry trong game Unity.
4. **Không làm hệ thống giải đấu / khán giả (Spectator Mode) trên web:** Đây là tính năng dự kiến cho roadmap hậu đề tài.

---

## Users / context
* **Quản trị viên hệ thống (Super Admin / Admin):** Người vận hành nền tảng, quản lý tài khoản, phân quyền, cấu hình hệ thống.
* **Huấn luyện viên / Điều phối viên nội dung (Coach / Content Manager):** Người thiết kế bài học, tạo drill kỹ thuật, theo dõi biểu đồ lỗi phổ biến của người chơi để tinh chỉnh giáo trình.
* **Kiểm duyệt viên (Moderator):** Người chuyên trách duyệt các báo cáo vi phạm, xem xét lịch sử trận đấu và xử lý kháng cáo.
* **Môi trường hoạt động:** Trình duyệt web hiện đại trên máy tính (Chrome, Edge, Firefox), hiển thị tối ưu trên độ phân giải từ 1280x720 trở lên.

---

## Constraints
1. **Backend tương thích 100%:** Phải kết nối trực tiếp vào các controller ASP.NET Core hiện có tại `src/Backend/API/Controllers/AdminController.cs` và các model DTOs của `IAdminService.cs`.
2. **Cơ chế xác thực:** Sử dụng Bearer JWT Token được sinh ra từ `AuthController.cs` với Claim Role `Admin`.
3. **Tech Stack quy định:**
   - Framework: ReactJS (Vite) + TypeScript.
   - Styling & Design System: Tailwind CSS + bộ component `shadcn/ui`.
   - Server State & Cache: TanStack React Query.
   - Biểu đồ: Recharts hoặc Chart.js.
4. **Vị trí mã nguồn:** Toàn bộ mã nguồn Admin Web đặt trong thư mục `src/Frontend/` của repository PickleVerse.

---

## Research Findings
*(Nghiên cứu tiêu chuẩn thiết kế Game Operations & Telemetry Dashboard - Cập nhật 2026)*

### 1. Quản lý trạng thái & Dữ liệu Telemetry (React Query & Virtualization)
- **Bằng chứng (Evidence):** Dữ liệu telemetry game (lỗi đánh bóng, thời gian, sự kiện trận đấu) phát sinh theo tần suất cao. Việc đưa trực tiếp telemetry vào state React mà không phân trang server hoặc ảo hóa danh sách (virtualization) dễ gây nghẽn render loop (Nguồn: *React Admin Dashboard Best Practices 2026* - Tier 2 - High).
- **Hệ quả (Inference):** Bảng danh sách trận đấu và lịch sử vi phạm cần sử dụng phân trang từ server (`page`, `pageSize`) kết hợp cùng `@tanstack/react-query` để cache và tự động làm mới khi cần.

### 2. Quy trình xử lý vi phạm (Moderation Queue & Optimistic Updates)
- **Bằng chứng (Evidence):** Các bảng điều khiển kiểm duyệt game (như Riot Games, Steam Support) áp dụng cơ chế Side-Drawer (ngăn kéo bên cạnh) để xem chi tiết lịch sử người chơi mà không làm mất ngữ cảnh danh sách báo cáo đang duyệt (Nguồn: *Game Operations UX Patterns* - Tier 2 - High).
- **Hệ quả (Inference):** Trang kiểm duyệt báo cáo (`reports`) sẽ dùng bảng danh sách kèm modal/drawer hiển thị chi tiết lý do báo cáo, bằng chứng trận đấu và nút hành động nhanh (Khóa tài khoản / Bác đơn).

### 3. Trực quan hóa bản đồ nhiệt lỗi kỹ thuật (Technique-Error Heatmap)
- **Bằng chứng (Evidence):** Dữ liệu lỗi kỹ thuật PickleVerse gồm 2 chiều: Kỹ thuật (Serve, Dink, Volley, Drop, Kitchen) và Dạng lỗi (Late Timing, Too High, Foot Fault, Wrong Angle). Biểu đồ dạng Ma trận nhiệt (Heatmap Matrix) hoặc Treemap/Bar kết hợp cho phép phát hiện điểm nóng kỹ thuật trong vòng 3 giây quan sát (Nguồn: *Sports Analytics Dashboard Guidelines* - Tier 2 - High).
- **Hệ quả (Inference):** Xây dựng component trực quan hóa ma trận lỗi AI bằng Recharts/Tailwind Grid với mã màu gradient trực quan (xanh lá an toàn $\rightarrow$ đỏ cảnh báo lỗi cao).

---

## Approaches considered

| Phương án kiến trúc | Ưu điểm | Nhược điểm | Lựa chọn? |
|---|---|---|---|
| **A. Custom React + Vite + shadcn/ui + Tailwind (Đề xuất)** | • Nhẹ, tốc độ build cực nhanh.<br>• Kiểm soát 100% giao diện, code đẹp chuẩn shadcn/ui.<br>• Phù hợp hoàn hảo với đề bài Proposal.<br>• Dễ dàng tùy biến theo theme thể thao PickleVerse. | Cần tự dựng layout và bảng biểu thủ công theo từng trang. | **CHỌN (Khuyên dùng)** |
| **B. Dùng Admin Framework có sẵn (Refine / React-Admin)** | • Có sẵn CRUD generator và phân trang.<br>• Tốc độ dựng form nhanh. | • Khó tùy biến giao diện cho đồ án capstone.<br>• Khung sườn cồng kềnh, sinh code thừa.<br>• Không đồng bộ với yêu cầu công nghệ của đề tài. | Loại |
| **C. Tích hợp trực tiếp vào Unity Client (In-Game Admin Panel)** | • Không cần dựng thêm web riêng. | • Trái với yêu cầu kiến trúc đa tầng (Proposal Package 6).<br>• Trải nghiệm gõ phím, duyệt bảng biểu trên Unity kém thuận tiện hơn web. | Loại |

---

## Decision & rationale
* **Quyết định:** Chọn **Phương án A (React + Vite + TypeScript + Tailwind CSS + shadcn/ui + TanStack Query + Recharts)**.
* **Lý do:**
  1. Tuân thủ chính xác đề xuất kỹ thuật trong [PickleVerse_Capstone_Proposal.md#L82-L85](file:///d:/Repository/PickleVerse/Doc/PickleVerse_Capstone_Proposal.md#L82-L85).
  2. Tận dụng trọn vẹn các controller API đã viết sẵn trong `src/Backend/API/Controllers/AdminController.cs`.
  3. Giao diện hiện đại, thẩm mỹ cao (Dark/Light mode, thẻ card số liệu, hiệu ứng micro-interaction) giúp gây ấn tượng mạnh mẽ cho hội đồng đánh giá đồ án tốt nghiệp.

---

## Assumptions and validation plan

| Giả định | Nguồn / Bên phụ trách | Rủi ro nếu sai | Kế hoạch kiểm chứng |
|---|---|---|---|
| Backend API đang chạy và có thể truy cập qua CORS từ localhost của frontend web | Backend Team / `src/Backend` | Web không gọi được API, lỗi chặn CORS | Kiểm tra cấu hình CORS trong `Program.cs` của Backend API trước khi ghép nối |
| Tài khoản đăng nhập có Role Admin đã có cơ chế tạo/cấp quyền | `src/Database.sql` & `AuthService` | Không đăng nhập được vào trang Admin | Kiểm tra bảng Roles và script seed role Admin trong `src/Database.sql` |
| Token JWT chứa Role claim hợp lệ để AdminController xác thực | `AuthController.cs` / JWT Middleware | API trả về 403 Forbidden dù nhập đúng mật khẩu | Kiểm tra payload của token giải mã bằng jwt.io hoặc test fixture |

---

## Key User Decisions (Resolved)
1. **Cập nhật dữ liệu trận đấu:** Trang giám sát trận đấu sẽ hỗ trợ cả hai: cơ chế **tự động làm mới (Auto-polling mỗi 15-30s)** kết hợp cùng **nút bấm tải lại thủ công (Manual Refresh)**.
2. **Phong cách giao diện (Theme):** Thiết kế theo phong cách **SaaS Chuyên nghiệp tối giản (shadcn Slate/Zinc)** với Dark/Light mode linh hoạt, thẻ card số liệu gọn gàng và tương phản cao.
3. **Chi tiết trận đấu:** Danh sách bảng trận đấu tóm tắt (thời gian, thể loại, người chơi, tỷ số) kèm Side-Drawer hoặc Modal xem nhanh chi tiết set điểm khi cần.
