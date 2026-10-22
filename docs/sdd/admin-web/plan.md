# Plan: Admin Web Dashboard (PickleVerse)

**Source**: [docs/sdd/admin-web/brainstorm.md](file:///d:/Repository/PickleVerse/docs/sdd/admin-web/brainstorm.md) (v1.0), [docs/sdd/admin-web/brd.md](file:///d:/Repository/PickleVerse/docs/sdd/admin-web/brd.md) (v1.0), [docs/sdd/constitution.md](file:///d:/Repository/PickleVerse/docs/sdd/constitution.md) (v1.0.0)  
**Feature Slug**: `admin-web`  
**Project Scope**: `Full-Stack Application`  
**Status**: Draft for Approval  

---

## 1. Spec Summary

### 1.1 Functional Requirements
* **FR-01 & FR-02 (Authentication & Route Guards):** Đăng nhập an toàn bằng JWT qua `POST /api/Auth/login`, kiểm soát truy cập và bảo vệ toàn bộ định tuyến quản trị bằng quyền `Role = "Admin"`, tự động xử lý HTTP 401/403.
* **FR-03 $\rightarrow$ FR-06 (User Management):** Bảng danh sách người dùng phân trang server-side, tìm kiếm theo tên/email, lọc trạng thái (`ACTIVE`/`BANNED`/`UNVERIFIED`), gán vai trò (`Admin`/`Player`), khóa tài khoản có lý do (`Ban`/`Unban`), kích hoạt email thủ công và reset điểm Elo.
* **FR-07 & FR-08 (Content CMS):** Giao diện thêm mới và quản lý bài học (`Lessons`), bài tập kỹ thuật (`Drills`), đăng bán vật phẩm thời trang (`Cosmetics`) và nhiệm vụ định kỳ (`Missions`).
* **FR-09 (Match Monitoring):** Giám sát danh sách 50 trận đấu gần nhất và các trận đang diễn ra theo thời gian thực với cơ chế **Auto-polling (15–30s)** kết hợp **Manual Refresh**.
* **FR-10 & FR-11 (Moderation & Tribunal):** Quản lý hàng đợi tố cáo vi phạm, xem xét bằng chứng/replay và thực thi chế tài xử phạt (Duyệt Ban hoặc Bác đơn).
* **FR-12 & FR-13 (Analytics & AI Telemetry Heatmap):** 5 thẻ thống kê vận hành (DAU, tổng trận, thời gian chơi, vi phạm chờ duyệt) và **Bản đồ nhiệt lỗi kỹ thuật AI (Technique-Error Heatmap)** cho 5 kỹ thuật bóng chính.

### 1.2 Acceptance Criteria
1. Khởi tạo hoàn chỉnh dự án React + TypeScript tại `src/Frontend/` với Vite, cấu hình đầy đủ Tailwind CSS và bộ component `shadcn/ui` theo phong cách Slate/Zinc.
2. 100% trang giao diện tương ứng với 6 phân hệ (Login, Dashboard, Users, CMS, Matches, Reports, Analytics) hoạt động trơn tru.
3. Không có lỗi TypeScript (`tsc --noEmit`) và không sử dụng kiểu dữ liệu `any`.
4. Gọi API thành công tới [AdminController.cs](file:///d:/Repository/PickleVerse/src/Backend/API/Controllers/AdminController.cs) với token Bearer JWT hợp lệ; kiểm thử chặn truy cập khi chưa đăng nhập (401) hoặc role Player (403).
5. Giao diện responsive trên Desktop/Tablet, tải trang lần đầu $\le 1.5$s, tuân thủ WCAG 2.1 AA.

---

## 2. Implementation Reality

### 2.1 Thành phần hiện có (Existing Assets)
* **Backend Controllers:** [AdminController.cs](file:///d:/Repository/PickleVerse/src/Backend/API/Controllers/AdminController.cs) (14 endpoints), [AuthController.cs](file:///d:/Repository/PickleVerse/src/Backend/API/Controllers/AuthController.cs).
* **Service Interfaces & DTOs:** [IAdminService.cs](file:///d:/Repository/PickleVerse/src/Backend/Services/Interfaces/IAdminService.cs), [AllDTOs.cs](file:///d:/Repository/PickleVerse/src/Backend/Services/DTOs/AllDTOs.cs) (chứa đầy đủ các DTO: `AdminUserDto`, `AssignRoleRequest`, `BanUserRequest`, `CreateLessonRequest`, `CreateDrillRequest`, `CreateCosmeticRequest`, `CreateMissionRequest`, `AdminReportDto`, `ReviewReportRequest`, `AdminAnalyticsResponse`, `AiTechniqueErrorHeatmapDto`).
* **Cơ sở dữ liệu:** [Database.sql](file:///d:/Repository/PickleVerse/src/Database.sql) (schema PostgreSQL gồm `Users`, `Roles`, `UserRoles`, `Profiles`, `Matches`, `MatchPlayers`, `Reports`, `Lessons`, `Drills`, `Cosmetics`, `Missions`).
* **Frontend:** Thư mục `src/Frontend/` đang trống, sẵn sàng để scaffold ứng dụng web Vite React.

### 2.2 Ràng buộc & Giả định (Assumptions & Decisions)
* **Quyết định CORS:** Backend `Program.cs` cần đảm bảo cấu hình CORS cho phép origin của Vite dev server (`http://localhost:5173`).
* **Xác thực Admin:** Cần một tài khoản có Role `Admin` trong database để kiểm thử đăng nhập thực tế.
* **Tương thích DTO:** Toàn bộ type definition trên Frontend phải khớp 1:1 với C# DTOs trong `AllDTOs.cs`.

---

## 3. Requirement Traceability

| Mã Yêu cầu | Độ ưu tiên | Nhiệm vụ (Task IDs) | Bằng chứng nghiệm thu (Acceptance Evidence) | Loại kiểm thử |
|---|---|---|---|---|
| **FR-01** | Must | Task 1.3, Task 1.4 | Đăng nhập lấy được JWT token, lưu LocalStorage, hiển thị Profile Admin | E2E / Component Test |
| **FR-02** | Must | Task 1.5 | Chặn route khi chưa đăng nhập, tự động logout khi nhận 401/403 | Route Guard Test |
| **FR-03** | Must | Task 2.1, Task 2.2 | Bảng hiển thị danh sách người dùng, tìm kiếm & phân trang server hoạt động | API Integration |
| **FR-04** | Must | Task 2.3 | Gán vai trò thành công, backend cập nhật `UserRoles` | Mutation Test |
| **FR-05** | Must | Task 2.4 | Modal Ban/Unban hoạt động, cập nhật trạng thái `BANNED`/`ACTIVE` | Mutation Test |
| **FR-06** | Should | Task 2.5 | Nút kích hoạt email và reset Elo hoạt động có confirmation dialog | Action Test |
| **FR-07** | Must | Task 3.1, Task 3.2 | Form tạo Lesson và Drill gửi dữ liệu hợp lệ lên server, trả về ID mới | Form Validation |
| **FR-08** | Should | Task 3.3 | Form đăng bán Cosmetic và tạo Mission thêm thành công vào database | Form Validation |
| **FR-09** | Must | Task 4.1, Task 4.2 | Bảng hiển thị 50 trận gần nhất, Auto-polling mỗi 15-30s và nút Manual Refresh | Polling Test |
| **FR-10** | Must | Task 5.1 | Danh sách tố cáo hiển thị đúng theo trạng thái (PENDING, RESOLVED, DISMISSED) | API Integration |
| **FR-11** | Must | Task 5.2 | Modal xử lý tố cáo: duyệt Ban hoặc bác bỏ, cập nhật trạng thái report | Mutation Test |
| **FR-12** | Must | Task 6.1 | Dashboard hiển thị 5 thẻ số liệu chính xác từ `GET /api/Admin/analytics/summary` | Rendering Test |
| **FR-13** | Must | Task 6.2 | Bản đồ nhiệt (Heatmap Matrix) trực quan hóa lỗi của 5 kỹ thuật AI rõ ràng | Visual Render |

---

## 4. Platform & Architecture

* **Platform Target:** Web Application (Tối ưu cho Desktop 1920x1080 và Tablet $\ge 1024$px).
* **Core Framework:** React 18+ (Vite) + TypeScript (Strict Mode).
* **Styling & Design System:** Tailwind CSS + `shadcn/ui` (Radix UI primitives).
* **Server State & Caching:** TanStack React Query v5.
* **Data Visualization:** Recharts (Bản đồ nhiệt & Bar Chart).
* **Cấu trúc thư mục tại `src/Frontend/`:**
  ```text
  src/Frontend/
  ├── src/
  │   ├── api/              # Axios client, Auth API, Admin API client
  │   ├── components/
  │   │   ├── ui/           # Radix/shadcn atomic components (Button, Dialog, Card,...)
  │   │   ├── layout/       # AdminLayout, Sidebar, Navbar, ProtectedRoute
  │   │   └── modules/      # UsersTable, MatchList, CMSForms, HeatmapMatrix
  │   ├── context/          # AuthContext (quản lý session, login/logout)
  │   ├── hooks/            # useAuth, useUsers, useMatches, useAnalytics
  │   ├── pages/            # Login, Dashboard, Users, CMS, Matches, Moderation, Analytics
  │   ├── types/            # TypeScript interfaces ánh xạ 1:1 từ Backend DTOs
  │   ├── App.tsx           # React Router DOM configuration
  │   ├── main.tsx          # React DOM entrypoint
  │   └── index.css         # Tailwind & Design tokens
  ├── index.html
  ├── package.json
  ├── tsconfig.json
  └── vite.config.ts
  ```

---

## 5. Backend & Database Specification

* **Backend Framework:** C# ASP.NET Core Web API (.NET 8/9).
* **API Documentation:** Swagger UI tại `/swagger`.
* **Database:** PostgreSQL (Relational) — Schema: [src/Database.sql](file:///d:/Repository/PickleVerse/src/Database.sql).
* **Danh sách API Endpoints tích hợp:**

| Phương thức | Đường dẫn API | Vai trò bảo vệ | Mô tả chức năng |
|---|---|---|---|
| `POST` | `/api/Auth/login` | Public | Đăng nhập lấy JWT Bearer Token |
| `GET` | `/api/Admin/users` | Admin | Lấy danh sách người dùng (search, status, page, pageSize) |
| `POST` | `/api/Admin/users/assign-role` | Admin | Gán vai trò cho người dùng (Player / Admin) |
| `POST` | `/api/Admin/users/ban` | Admin | Khóa tài khoản có lý do và thời hạn |
| `POST` | `/api/Admin/users/{userId}/unban` | Admin | Mở khóa tài khoản người dùng |
| `POST` | `/api/Admin/users/{playerId}/reset-elo` | Admin | Đặt lại điểm Elo về mặc định (2000) |
| `POST` | `/api/Admin/users/{userId}/verify-email` | Admin | Kích hoạt email tài khoản thủ công |
| `POST` | `/api/Admin/cms/lessons` | Admin | Tạo bài học mới |
| `POST` | `/api/Admin/cms/drills` | Admin | Tạo bài tập kỹ thuật mới thuộc bài học |
| `POST` | `/api/Admin/cms/cosmetics` | Admin | Đăng bán vật phẩm vào Shop |
| `POST` | `/api/Admin/cms/missions` | Admin | Tạo nhiệm vụ sự kiện hàng ngày/tuần |
| `GET` | `/api/Admin/matches/recent` | Admin | Lấy danh sách 50 trận đấu gần đây |
| `GET` | `/api/Admin/reports` | Admin | Lấy danh sách tố cáo người chơi theo trạng thái |
| `POST` | `/api/Admin/reports/review` | Admin | Xử lý duyệt tố cáo / khóa tài khoản vi phạm |
| `GET` | `/api/Admin/analytics/summary` | Admin | Lấy tổng quan số liệu người dùng & trận đấu |
| `GET` | `/api/Admin/analytics/error-heatmap` | Admin | Lấy ma trận dữ liệu lỗi kỹ thuật AI |

---

## 6. Design Template Specification

* **Chosen Template:** **SaaS Landing & Dashboard (ID: `saas-landing`)**
* **Source:** [templates/02-saas-landing.md](file:///d:/Repository/PickleVerse/.agents/plugins/sdd-hybrid/skills/sdd-plan/templates/02-saas-landing.md)
* **Theme & Color Palette (SaaS Slate/Zinc Dark Mode):**
  - **Background (`bg-background`):** `#0F172A` (Slate 900)
  - **Card / Surface (`bg-card`):** `#1E293B` (Slate 800)
  - **Border (`border-border`):** `#334155` (Slate 700)
  - **Primary CTA / Active:** `#4F46E5` (Indigo 600)
  - **Accent / Highlight:** `#06B6D4` (Cyan 500)
  - **Text Primary (`text-foreground`):** `#F8FAFC` (Slate 50)
  - **Text Muted (`text-muted-foreground`):** `#94A3B8` (Slate 400)
  - **Success / Warning / Danger:** `#10B981` (Emerald), `#F59E0B` (Amber), `#EF4444` (Rose)
* **Fonts:**
  - **Display / Headings:** `'Plus Jakarta Sans', sans-serif`
  - **Body / Interface:** `'Inter', sans-serif`
* **UI Components (shadcn/ui):** `Button`, `Input`, `Card`, `Table`, `Dialog`, `DropdownMenu`, `Badge`, `Switch`, `Tabs`, `Toast/Sonner`.

---

## 7. Constitution Compliance Check

| Nguyên tắc | Trạng thái | Đánh giá tuân thủ |
|---|---|---|
| **P1: Strict Layer Separation** | Tuân thủ | Tầng backend đã tuân thủ triệt để Controller $\rightarrow$ Service $\rightarrow$ Repository. Frontend tổ chức tách biệt api $\rightarrow$ hooks $\rightarrow$ components $\rightarrow$ pages. |
| **P2: Zero-Trust RBAC** | Tuân thủ | Backend bảo vệ bởi `[Authorize(Roles = "Admin")]`. Frontend dùng ProtectedRoute và Axios Interceptor tự động logout khi gặp 401/403. |
| **P3: Test-Driven Verification** | Tuân thủ | Mọi milestone đều có test check xác minh trước khi đánh dấu hoàn thành. |
| **P4: Strict TypeScript & Type-Safe DTOs** | Tuân thủ | Toàn bộ payload và response có interface tương ứng với C# DTOs trong `src/Frontend/src/types/`, không dùng `any`. |
| **P5: Centralized Error Handling** | Tuân thủ | Sử dụng Axios Interceptor toàn cục để bắt lỗi và hiển thị toast notification thống nhất. |
| **P6: UI State & Telemetry Decoupling** | Tuân thủ | Dùng React Query cache, phân trang server-side cho danh sách lớn, không kéo unpaginated data vào component state. |
| **P7: WCAG 2.1 AA & SaaS Slate/Zinc** | Tuân thủ | Áp dụng đúng bảng màu Slate 900 / Indigo với độ tương phản $\ge 4.5:1$. |
| **P8: Zero Console Noise** | Tuân thủ | Bật rule `no-console` của ESLint, loại bỏ mọi log rác khi build. |

---

## 8. Milestones & Task Breakdown

### Milestone 1: Project Scaffolding, Design System & Auth/RBAC (FR-01, FR-02)
*Mục tiêu: Dựng bộ khung React Vite + TypeScript + Tailwind + shadcn/ui, thiết lập Axios Interceptor, AuthContext và Route Guard bảo vệ toàn bộ trang Admin.*

- [x] **Task 1.1 — Scaffold Frontend Application**
  - **Files:** `src/Frontend/package.json`, `src/Frontend/vite.config.ts`, `src/Frontend/tsconfig.json`, `src/Frontend/index.html`
  - **Test/Check:** Lệnh `npm install` thành công và `npm run build` không lỗi.
  - **Steps:** Khởi tạo dự án Vite React TypeScript tại `src/Frontend/`, cài đặt các package: `react-router-dom`, `@tanstack/react-query`, `axios`, `lucide-react`, `recharts`, `clsx`, `tailwind-merge`.
- [x] **Task 1.2 — Configure Tailwind CSS & Slate/Zinc Design Tokens**
  - **Files:** `src/Frontend/tailwind.config.js`, `src/Frontend/src/index.css`
  - **Test/Check:** Khởi động dev server `npm run dev`, trang chủ hiển thị đúng màu nền `#0F172A` và font `Plus Jakarta Sans` / `Inter`.
  - **Steps:** Thiết lập cấu hình Tailwind với dark mode class, định nghĩa CSS variables cho theme Slate/Zinc, import Google Fonts vào `index.html`.
- [x] **Task 1.3 — Define TypeScript DTO Types & Axios API Client**
  - **Files:** `src/Frontend/src/types/api.ts`, `src/Frontend/src/api/client.ts`
  - **Test/Check:** Typescript compile `npm run type-check` vượt qua; Axios client tự động gắn Header `Authorization: Bearer <token>` từ LocalStorage.
  - **Steps:** Khởi tạo interface cho tất cả DTOs theo [AllDTOs.cs](file:///d:/Repository/PickleVerse/src/Backend/Services/DTOs/AllDTOs.cs). Tạo Axios instance với base URL `http://localhost:5000` (hoặc cấu hình từ `.env`).
- [x] **Task 1.4 — Build Login Page & AuthContext**
  - **Files:** `src/Frontend/src/context/AuthContext.tsx`, `src/Frontend/src/pages/Login.tsx`
  - **Test/Check:** Gọi `POST /api/Auth/login` thành công lưu token và user info. Nếu tài khoản có Role Player, báo lỗi và từ chối lưu token.
  - **Steps:** Xây dựng form đăng nhập Slate card hiện đại, kết nối AuthContext, xử lý lỗi đăng nhập.
- [x] **Task 1.5 — Implement ProtectedRoute & Admin Layout with Navigation**
  - **Files:** `src/Frontend/src/components/layout/ProtectedRoute.tsx`, `src/Frontend/src/components/layout/AdminLayout.tsx`, `src/Frontend/src/components/layout/Sidebar.tsx`
  - **Test/Check:** Truy cập `/dashboard` khi chưa login tự chuyển về `/login`. Khi API trả về 401/403, tự xóa token và logout.
  - **Steps:** Tạo ProtectedRoute bọc toàn bộ subroutes, dựng Sidebar thu gọn/mở rộng với 6 mục điều hướng chính (Dashboard, Users, CMS, Matches, Reports, Analytics).

---

### Milestone 2: User Management & Moderation Queue (FR-03, FR-04, FR-05, FR-06, FR-10, FR-11)
*Mục tiêu: Hoàn thiện phân hệ quản lý tài khoản người chơi và phân hệ xử lý vi phạm.*

- [x] **Task 2.1 — Build User API Hooks & Query Cache**
  - **Files:** `src/Frontend/src/api/adminApi.ts`, `src/Frontend/src/hooks/useUsers.ts`
  - **Test/Check:** Hook `useUsers(search, status, page, pageSize)` fetch dữ liệu từ `GET /api/Admin/users` và cache qua React Query.
- [x] **Task 2.2 — Implement Users Listing Page with Server-Side Search & Filter**
  - **Files:** `src/Frontend/src/pages/Users.tsx`, `src/Frontend/src/components/modules/users/UsersTable.tsx`
  - **Test/Check:** Ô tìm kiếm debounce gửi request `search`, dropdown lọc trạng thái `status`, thanh phân trang đổi `page` mượt mà.
- [x] **Task 2.3 — Implement Role Assignment Modal**
  - **Files:** `src/Frontend/src/components/modules/users/AssignRoleModal.tsx`
  - **Test/Check:** Chọn Role Admin/Player $\rightarrow$ gọi `POST /api/Admin/users/assign-role` $\rightarrow$ toast thông báo thành công và làm mới bảng.
- [x] **Task 2.4 — Implement Ban & Unban User Actions**
  - **Files:** `src/Frontend/src/components/modules/users/BanUserModal.tsx`
  - **Test/Check:** Nhập lý do khóa $\rightarrow$ gọi `POST /api/Admin/users/ban`. Nút Unban gọi `POST /api/Admin/users/{id}/unban`. Trạng thái chuyển đúng giữa `ACTIVE` và `BANNED`.
- [x] **Task 2.5 — Implement Manual Verify Email & Reset Elo Dialogs**
  - **Files:** `src/Frontend/src/components/modules/users/VerifyEmailDialog.tsx`, `src/Frontend/src/components/modules/users/ResetEloDialog.tsx`
  - **Test/Check:** Bấm kích hoạt gọi `POST /api/Admin/users/{id}/verify-email`. Bấm reset Elo gọi `POST /api/Admin/users/{id}/reset-elo`.
- [x] **Task 2.6 — Build Moderation & Reports Page**
  - **Files:** `src/Frontend/src/pages/Moderation.tsx`, `src/Frontend/src/components/modules/moderation/ReportReviewModal.tsx`
  - **Test/Check:** Lọc báo cáo theo `status=PENDING`. Modal Review gọi `POST /api/Admin/reports/review` với tùy chọn khóa tài khoản kèm lý do.

---

### Milestone 3: Content Management System (Lessons, Drills, Cosmetics, Missions) (FR-07, FR-08)
*Mục tiêu: Xây dựng giao diện tạo mới bài học, bài tập kỹ thuật, đăng bán vật phẩm và tạo nhiệm vụ.*

- [x] **Task 3.1 — Build CMS API Hooks & Tab Layout**
  - **Files:** `src/Frontend/src/pages/CMS.tsx`, `src/Frontend/src/hooks/useCMS.ts`
  - **Test/Check:** Trang CMS hiển thị 4 Tabs: *Bài học (Lessons)*, *Bài tập (Drills)*, *Vật phẩm (Cosmetics)*, *Nhiệm vụ (Missions)*.
- [x] **Task 3.2 — Implement Create Lesson & Drill Forms**
  - **Files:** `src/Frontend/src/components/modules/cms/CreateLessonForm.tsx`, `src/Frontend/src/components/modules/cms/CreateDrillForm.tsx`
  - **Test/Check:** Validate form (tên không rỗng, order $\ge 1$, technique hợp lệ). Submit gọi `POST /api/Admin/cms/lessons` và `POST /api/Admin/cms/drills` hiển thị toast thành công kèm ID.
- [x] **Task 3.3 — Implement Create Cosmetic & Mission Forms**
  - **Files:** `src/Frontend/src/components/modules/cms/CreateCosmeticForm.tsx`, `src/Frontend/src/components/modules/cms/CreateMissionForm.tsx`
  - **Test/Check:** Submit gọi `POST /api/Admin/cms/cosmetics` (paddle, outfit) và `POST /api/Admin/cms/missions` (daily, weekly).

---

### Milestone 4: Match Monitoring & Real-time Operations (FR-09)
*Mục tiêu: Xây dựng bảng theo dõi trận đấu với khả năng Auto-polling và xem chi tiết trận.*

- [x] **Task 4.1 — Build Matches API Hook with Auto-polling & Window Visibility**
  - **Files:** `src/Frontend/src/hooks/useMatches.ts`
  - **Test/Check:** Query gọi `GET /api/Admin/matches/recent?limit=50`. Hỗ trợ refetchInterval (15s/30s) và tự động dừng khi tab ẩn (`document.hidden`).
- [x] **Task 4.2 — Implement Matches Monitoring Page with Manual Refresh & Polling Switch**
  - **Files:** `src/Frontend/src/pages/Matches.tsx`, `src/Frontend/src/components/modules/matches/MatchesTable.tsx`
  - **Test/Check:** Switch bật/tắt auto-polling; nút bấm Manual Refresh kích hoạt refetch tức thì; bảng hiển thị thể loại, tỷ số, người thắng.
- [x] **Task 4.3 — Implement Match Detail Drawer / Modal**
  - **Files:** `src/Frontend/src/components/modules/matches/MatchDetailDrawer.tsx`
  - **Test/Check:** Bấm vào dòng trận đấu mở Drawer hiển thị chi tiết người chơi tham gia, thời lượng và thông tin set điểm.

---

### Milestone 5: Analytics & AI Technique Error Heatmap (FR-12, FR-13)
*Mục tiêu: Xây dựng Dashboard tổng quan và Bản đồ nhiệt lỗi kỹ thuật AI.*

- [x] **Task 5.1 — Implement Summary Analytics Cards**
  - **Files:** `src/Frontend/src/pages/Dashboard.tsx`, `src/Frontend/src/components/modules/analytics/SummaryCards.tsx`
  - **Test/Check:** Hiển thị 5 thẻ số liệu chính xác từ `GET /api/Admin/analytics/summary` (Total Users, Active Today, Matches, PlayTime, Pending Reports).
- [x] **Task 5.2 — Build AI Technique Error Heatmap Matrix Component**
  - **Files:** `src/Frontend/src/pages/Analytics.tsx`, `src/Frontend/src/components/modules/analytics/TechniqueErrorHeatmap.tsx`
  - **Test/Check:** Truy vấn `GET /api/Admin/analytics/error-heatmap`. Trực quan hóa ma trận lỗi của 5 kỹ thuật chính (Serve, Dink, Volley, Third-shot drop, Kitchen) với thang màu cảnh báo Slate $\rightarrow$ Amber $\rightarrow$ Red. Tooltip hiển thị số lượng chi tiết khi hover.
- [x] **Task 5.3 — Build Aggregate Activity & Trends Charts**
  - **Files:** `src/Frontend/src/components/modules/analytics/ActivityCharts.tsx`
  - **Test/Check:** Biểu đồ cột/vùng Recharts hiển thị xu hướng trận đấu và phân bổ người chơi.

---

### Milestone 6: End-to-End Integration, Linter & Quality Gate Verification
*Mục tiêu: Đảm bảo toàn bộ hệ thống hoạt động ổn định, kiểm tra CORS, kiểm tra bảo mật RBAC, loại bỏ console log và kiểm thử E2E.*

- [x] **Task 6.1 — Verify Backend CORS & JWT Role Configuration**
  - **Files:** `src/Backend/API/Program.cs`
  - **Test/Check:** Frontend gọi API từ localhost:5173 không bị lỗi CORS; token JWT mang claim Role Admin được xác thực thành công.
- [x] **Task 6.2 — Full Quality Gate & Linter Pass**
  - **Files:** Toàn bộ thư mục `src/Frontend/`
  - **Test/Check:** Chạy `npm run lint` và `tsc --noEmit` đạt 0 lỗi, không còn `console.log` debug rác trong production code.
- [x] **Task 6.3 — End-to-End Verification Run & Admin Guide Documentation**
  - **Files:** `Doc/Administrator_Guide.md`
  - **Test/Check:** Thực hiện kịch bản E2E hoàn chỉnh: Đăng nhập Admin $\rightarrow$ Khóa/Mở tài khoản $\rightarrow$ Tạo bài học/Drill $\rightarrow$ Xem trận đấu polling $\rightarrow$ Duyệt vi phạm $\rightarrow$ Xem Heatmap AI. Viết tài liệu hướng dẫn vận hành cho Quản trị viên theo đúng cam kết trong Proposal.

---

## 9. Dependencies, Rollout, and Operational Checks

1. **Ordering & Dependencies:**
   - Milestone 1 (Scaffolding & Auth) là tiền đề bắt buộc trước khi làm các Milestone 2–5.
   - Milestone 2, 3, 4, 5 độc lập với nhau và có thể triển khai song song hoặc tuần tự.
   - Milestone 6 là cổng chất lượng cuối cùng trước khi bàn giao.
2. **Rollback Strategy:**
   - Toàn bộ code frontend nằm cô lập trong thư mục `src/Frontend/`, không làm ảnh hưởng hay phá vỡ game client Unity hoặc các controller backend hiện có.
3. **Observability:**
   - Bắt lỗi tập trung qua Axios Interceptor và thông báo Toast Sonner. Phía backend log hoạt động qua `ILogger`.
