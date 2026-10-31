# Việc cần chốt trước khi nộp Review 2

Đối chiếu ngày 27/10/2026, mã nguồn main tại `087769927cdd38a5e4907ceb3cbabc60fc2e0baa`. Bốn file Word BM01–BM04 ở thư mục gốc Review_2 đã được điền bằng tiếng Anh theo biểu mẫu. Backend được cập nhật thành C# ASP.NET Core .NET 10, PostgreSQL và Redis. Không sửa code game, backend hay frontend.

## Nội dung đã chuẩn bị

- BM01: phạm vi proposal, 18 nhóm chức năng, 5 quyết định thiết kế, 3 rủi ro, 6 nhóm NFR, phân công đề xuất và 6 workflow. Phần chính 4 trang A4; phụ lục từ trang 5.
- BM02: giữ nguyên standard prompt; 14 finding có căn cứ, top 5, trạng thái và hướng xử lý. Không ghi finding nào đã sửa khi chưa có commit sửa thật.
- BM03: 6 thay đổi đề xuất về backend, thẩm quyền mô phỏng online, UI client, AI, bài tập/sân và triển khai. Quyết định duyệt của hội đồng để trống.
- BM04: 6 workflow, 12 tình huống demo, 4 vị trí thành viên đề xuất M1–M4, tiêu chí nghiệm thu và các việc cần khắc phục. Tên/MSSV, người review, xác nhận và chữ ký chờ nhóm/hội đồng điền.
- Kế hoạch 192 ca kiểm thử duy nhất, tối thiểu 96 ca chạy CI; đây là mục tiêu đề xuất. Không khẳng định đã chạy 192 ca.

## Đã có trong mã nguồn

Đã nhận diện tutorial cơ bản, 12 bài luyện tập có chấm sao/thưởng lần đầu/ôn lại, bot Behavior Tree điều chỉnh theo Elo, kỹ thuật và luật trận, online đơn/đôi, tìm trận/xác nhận, phòng riêng/mời bạn, kết quả/Elo, tiến trình/lịch sử/replay, bạn bè/BXH, nhiệm vụ/thành tựu. Chi tiết và nguồn đối chiếu nằm trong `Feature_Traceability_20261027.csv`. “Implemented” thể hiện có triển khai trong mã nguồn; chưa thay thế nghiệm thu đầy đủ.

GET tới public HTTP `/health/ready` đã trả 200 và `ready`. Chỉ kiểm tra đọc readiness; không tạo tài khoản, ghi dữ liệu, chạy tải hoặc đổi cấu hình server. HTTPS trên đường dẫn thử chưa xác minh được, không kết luận mọi HTTPS đều không hoạt động.

## Những điểm cần nhóm xác nhận hoặc bổ sung

| Ưu tiên | Việc cần xử lý | Tham chiếu |
|---|---|---|
| Cao | Cung cấp URL HTTPS dùng được ở bản release và bằng chứng TLS, cấu hình production, mã hóa dữ liệu lưu trữ. Cấu hình hiện thấy dùng remote HTTP trong khi client release có chặn remote HTTP. | SR08 |
| Cao | Đồng bộ hợp đồng đăng nhập frontend quản trị với API; kiểm tra RBAC bằng dữ liệu thật, kiểm tra cách ghi trạng thái tài khoản với constraint DB thực tế. | SR04–05 |
| Cao | Hoàn thiện sửa/publish/unpublish nội dung có thể chơi trong game, thống kê đúng theo kỳ, giám sát và xử lý khiếu nại/reset tiến trình. | SR06–07 |
| Cao | Giải quyết Photon Shared/peer authority so với yêu cầu dedicated trusted simulation trong proposal, hoặc trình duyệt thay đổi rõ ràng. Backend lưu kết quả không đồng nghĩa backend mô phỏng vật lý tin cậy. | SR01 |
| Cao | Bổ sung luyện tập offline với hàng đợi bền và đồng bộ khi kết nối lại, không mất/lặp thưởng. | SR02 |
| Cao | Bổ sung self-assessment/initial-tier classifier và đánh giá phân loại kỹ thuật bằng bộ dữ liệu có nhãn; tách rõ rule-based coaching hiện có với classifier đã đo chất lượng. | SR03 |
| Cao | Thu bằng chứng thiết bị Windows/Android/iOS, FPS/độ trễ phản hồi, RTT Việt Nam, 2.000 người/500 trận, uptime và bảo mật theo NFR proposal. | SR09 |
| Trước nộp | Cập nhật SRS/SDD/biểu đồ từ mốc 23/10 theo các thay đổi mới; bổ sung trạng thái/guard lời mời, Redis TTL và phiên bản schema/image đang chạy. | SR10–11, SR14 |
| Trước nộp | Nhóm/GVHD xác nhận thay đổi công nghệ và số bài hiện có: 12 bài so với “around 15” trong proposal. Không tự loại các yêu cầu bắt buộc khỏi phạm vi. | SR12–13, BM03 |
| Trước nộp | Chốt registration, tạo tag review-2 theo quy trình nhóm, chạy lại standard self-review trên tag và lưu CI/test evidence. Đợt này không tạo tag hay chạy kiểm thử sản phẩm. | SR14 |

## Thông tin hành chính để điền sau

- Mã đề tài/tên nhóm chính thức; phiên bản registration đã được duyệt.
- Tên, MSSV và phần việc thực tế của bốn thành viên; thay M1–M4 bằng phân công được cả nhóm xác nhận.
- Ngày Review 2, tên reviewer, quyết định BM03/BM04, ngày và chữ ký thật.
- PR/commit minh chứng đóng góp, link CI và kết quả nghiệm thu trên đúng phiên bản.

Các báo cáo trong `Prepared/`, bộ biểu đồ và ZIP cũ vẫn là baseline 23/10; không nộp chúng như thể đã đồng bộ với bốn biểu mẫu mới. Report 1/2 và Project Tracking chính thức cũng cần nhóm kiểm tra hoàn tất theo hướng dẫn Review 2.
