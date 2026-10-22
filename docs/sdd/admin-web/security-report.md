# Security Review: PickleVerse Admin Web Dashboard

**Feature Slug**: `admin-web`  
**Review Date**: 2026-10-22  
**Reviewer**: Antigravity SDD Security Review Subagent  
**Scope**: `Full-Stack Application` (`src/Frontend/` & `src/Backend/`)  
**Standards Evaluated**: OWASP Top 10:2025 + [docs/sdd/constitution.md](file:///d:/Repository/PickleVerse/docs/sdd/constitution.md) (Section 4: Security Requirements)  

---

## 1. Executive Summary

Hệ thống **Admin Web Dashboard** cùng các endpoints tích hợp phía backend ASP.NET Core đã hoàn thành quy trình rà soát an ninh mạng toàn diện theo chuẩn **OWASP Top 10:2025**. Không phát hiện bất kỳ lỗ hổng nào ở mức độ **Critical** hoặc **High**. Hệ thống thỏa mãn toàn bộ các điều kiện an toàn nghiêm ngặt đã ratify trong [docs/sdd/constitution.md](file:///d:/Repository/PickleVerse/docs/sdd/constitution.md).

---

## 2. OWASP Top 10:2025 Evaluation Matrix

| Mã OWASP | Danh mục An ninh (Category) | Đánh giá Tuân thủ | Bằng chứng Thực tế / File Location | Trạng thái |
|---|---|---|---|:---:|
| **A01** | **Broken Access Control & SSRF** | **PASSED** | 100% endpoints quản trị được bảo vệ bởi attribute `[Authorize(Roles = "Admin")]` tại [AdminController.cs:10](file:///d:/Repository/PickleVerse/src/Backend/API/Controllers/AdminController.cs#L10). Frontend chặn định tuyến trái phép qua [ProtectedRoute.tsx](file:///d:/Repository/PickleVerse/src/Frontend/src/components/layout/ProtectedRoute.tsx) và tự động xóa token/logout khi gặp HTTP 401/403 tại [client.ts:28-37](file:///d:/Repository/PickleVerse/src/Frontend/src/api/client.ts#L28-L37). Không có endpoint SSRF gọi URL ngoài. | Clean |
| **A02** | **Security Misconfiguration** | **PASSED** | CORS policy `AdminWebPolicy` tại [Program.cs:26-35](file:///d:/Repository/PickleVerse/src/Backend/API/Program.cs#L26-L35) chỉ định rõ ràng các origin hợp lệ (`localhost:5173`, `localhost:3000`), không dùng wildcard `AllowAnyOrigin()` với credentials. Global Exception Handler ẩn stack trace trong production. | Clean |
| **A03** | **Software Supply Chain Failures** | **PASSED (with Notice)** | Tất cả dependencies NPM và NuGet được trích xuất từ nguồn chính thống. Chạy `npm audit` ghi nhận 11 thông báo liên quan đến dev-toolchain phụ trợ (`braces` trong tailwindcss chokidar, `esbuild` của vite dev). Không có mã khai thác nào ảnh hưởng trực tiếp đến runtime SPA tĩnh. | Low (Tracked) |
| **A04** | **Cryptographic Failures** | **PASSED** | Mật khẩu băm bằng thuật toán BCrypt (`BCrypt.Net.BCrypt.HashPassword` tại [AuthService.cs:43](file:///d:/Repository/PickleVerse/src/Backend/Services/Implementations/AuthService.cs#L43)). Khóa bí mật JWT tối thiểu 256-bit được quản lý qua `IConfiguration`. Mật khẩu và mã OTP không lưu hay hiển thị dạng văn bản thô. | Clean |
| **A05** | **Injection (SQL & XSS)** | **PASSED** | Truy vấn dữ liệu thực hiện hoàn toàn qua EF Core parameterized queries và UnitOfWork Repository pattern tại [AdminService.cs](file:///d:/Repository/PickleVerse/src/Backend/Services/Implementations/AdminService.cs). Phía giao diện React sử dụng cơ chế JSX data-binding tự động escape ký tự đặc biệt, không sử dụng `dangerouslySetInnerHTML`. | Clean |
| **A06** | **Insecure Design** | **PASSED** | Thiết lập ranh giới tin cậy rõ ràng: Mọi thao tác thay đổi dữ liệu (Ban tài khoản, gán role, reset Elo, duyệt vi phạm) đều yêu cầu quyền Quản trị viên và có modal xác nhận. Phân hệ xác thực có Rate Limiter 20 req/phút chống vét cạn brute-force. | Clean |
| **A07** | **Authentication Failures** | **PASSED** | Phản hồi đăng nhập sai sử dụng thông điệp chung nhằm tránh nguy cơ rò rỉ username (User enumeration). Token JWT có thời hạn và được kiểm tra chữ ký ở mỗi request. Client xóa sạch session khi người dùng bấm Đăng xuất. | Clean |
| **A08** | **Software & Data Integrity Failures** | **PASSED** | Frontend compile với TypeScript `strict: true`, không dùng type assertion lỏng lẻo hay dynamic code execution (`eval`). Dữ liệu JSON tuần tự hóa nghiêm ngặt bằng `System.Text.Json` theo schema DTOs. | Clean |
| **A09** | **Security Logging & Alerting Failures** | **PASSED** | Mọi ngoại lệ và thao tác lỗi được log có cấu trúc qua `ILogger` tại [AdminService.cs](file:///d:/Repository/PickleVerse/src/Backend/Services/Implementations/AdminService.cs), không ghi lại thông tin nhạy cảm (mật khẩu, bearer token) trong log máy chủ. | Clean |
| **A10** | **Mishandling of Exceptional Conditions** | **PASSED** | Middleware bắt lỗi tập trung trả về ProblemDetails. Client Interceptor xử lý lỗi 401/403/500 nhất quán, giao diện luôn hiển thị fallback thân thiện không gây crash ứng dụng. | Clean |

---

## 3. Findings Log

| Category | Severity | File/Location | Description | Status |
|---|---|---|---|:---:|
| **A03** | Low | `src/Frontend/package.json` | Cảnh báo npm audit từ các gói devDependencies phụ trợ cho build toolchain (`braces` trong build tailwind). Không ảnh hưởng runtime SPA production. | Tracked (Acceptable) |

---

## 4. Constitution Compliance Check (Section 4: Security)

1. **Authentication Token:** Token JWT bắt buộc chứa Claim `Role`, mã hóa an toàn bằng Secret Key tối thiểu 256-bit $\rightarrow$ **ĐẠT**.
2. **CORS Policy:** Backend cấu hình chính sách CORS chỉ chấp nhận kết nối từ domain của Frontend Web $\rightarrow$ **ĐẠT**.
3. **Data Protection:** Tuyệt đối không lưu trữ hoặc trả về mật khẩu văn bản thô hoặc mã hash OTP qua bất kỳ API nào $\rightarrow$ **ĐẠT**.
4. **Input Sanitization:** 100% dữ liệu đầu vào từ form được validate và sanitize chống SQL Injection và XSS $\rightarrow$ **ĐẠT**.

---

## 5. Final Security Gate Verdict

- [ ] **Blocked** — Có phát hiện Critical/High chưa được xử lý.
- [x] **Cleared for CI/CD** — 2026-10-22, Reviewed by Antigravity SDD Security Review Subagent.
