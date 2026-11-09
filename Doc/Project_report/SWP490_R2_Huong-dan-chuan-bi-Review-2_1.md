# SWP490 · CAPSTONE PROJECT · TÀI LIỆU HƯỚNG DẪN CHO CÁC NHÓM
## Hướng dẫn chuẩn bị Review 2
**Chốt phạm vi đề tài, góp ý đặc tả – thiết kế và xác lập mục tiêu Defense 1.1 trước giai đoạn phát triển chính.**

*Phiên bản 1.0 — Ban hành 10/2026 — Đối tượng: sinh viên, GVHD, GV phản biện*

---

## 01 Tóm tắt nhanh

| 5 mục tiêu | 4 hồ sơ | BM01–BM04 | 1 Cam kết |
| :--- | :--- | :--- | :--- |
| Chốt phiếu đăng ký, góp ý SRS/SDD, chốt yêu cầu sản phẩm, chốt mục tiêu Defense 1.1, xác minh vai trò | Tất cả nộp dạng PDF, đặt tên theo quy ước thống nhất | Bốn biểu mẫu Word phát kèm tài liệu này | Căn cứ duy nhất cho Defense 1.1 và Viva |

Review 2 là mốc chốt phạm vi đề tài cuối cùng trước giai đoạn phát triển chính. Trên phạm vi đã chốt, hai giảng viên phản biện đánh giá đặc tả và thiết kế, rồi chốt những gì nhóm phải đạt khi ra Hội đồng. Kết quả Review 2 là căn cứ duy nhất để Defense 1.1 (Hội đồng 1.1) và Viva (Hội đồng 1.2) đánh giá đồ án.

| Thông tin | Nội dung |
| :--- | :--- |
| **Lịch review** | Thông báo riêng cho từng nhóm |
| **Hạn nộp hồ sơ** | Theo thông báo của môn học, áp dụng chung cho mọi nhóm |
| **Định dạng nộp** | File PDF, tên file `SWP490_<Mã đề tài>_R2_<Tên hồ sơ>.pdf` |
| **Ngôn ngữ** | Tài liệu viết bằng tiếng Anh; trình bày và trả lời có thể bằng tiếng Việt hoặc tiếng Anh |
| **Tham dự** | Bắt buộc với tất cả thành viên |
| **Thành phần đánh giá** | Hai giảng viên phản biện được phân công cho nhóm |

---

## 02 Năm mục tiêu của Review 2

| # | Mục tiêu |
| :---: | :--- |
| **1** | Chốt phiếu đăng ký phiên bản cuối. Sau Review 2, phạm vi đề tài không thay đổi; mọi đánh giá tiếp theo căn cứ trên phiếu này. |
| **2** | Góp ý chuyên môn về SRS và SDD trên phạm vi đã chốt, xác định các điểm cần điều chỉnh. |
| **3** | Chốt yêu cầu sản phẩm bắt buộc: danh sách tính năng bắt buộc, tiêu chí chấp nhận và yêu cầu phi chức năng (NFR). |
| **4** | Chốt mục tiêu Defense 1.1: main workflow, kịch bản demo, số lượng test case chức năng và ngưỡng đủ điều kiện ra Viva (mục 09). |
| **5** | Xác minh vai trò của từng thành viên thông qua khả năng giải thích và bảo vệ phần việc mình sở hữu. |

---

## 03 Hồ sơ cần nộp

Nhóm nộp bốn hồ sơ dưới dạng PDF. Hồ sơ có biểu mẫu thì điền biểu mẫu tương ứng, xoá phần hướng dẫn màu xám và các dòng ví dụ, rồi xuất PDF. Thứ tự trong bảng cũng là thứ tự nhóm nên thực hiện.

| # | Hồ sơ | Nội dung | Biểu mẫu | File nộp |
| :---: | :--- | :--- | :--- | :--- |
| **1** | Phiếu đăng ký phiên bản đề xuất chốt | Phiếu theo mẫu của môn học kèm Record of Changes (mục 04). Là căn cứ phạm vi cho mọi hồ sơ còn lại. | Mẫu phiếu của môn học + BM03 | `…_R2_Registration.pdf` |
| **2** | Report 1–5 và Project Tracking | SRS, SDD nhất quán với hồ sơ 1; kế hoạch kiểm thử; Project Tracking. Mã nguồn Iteration 1, database script, test và CI nằm trong repo gắn tag `review-2`. | Mẫu Report của môn học | `…_R2_Report<n>.pdf` |
| **3** | Báo cáo tự rà soát SRS/SDD | Kết quả chạy prompt chuẩn trên hồ sơ 1 và 2, kèm phản hồi của nhóm (mục 05). | BM02 | `…_R2_SelfReview.pdf` |
| **4** | Design Brief | Tóm lược phạm vi và thiết kế, mục 0–8 kèm Phụ lục A (mục 06 và 09). Tối đa 4 trang A4, không tính Phụ lục A. | BM01 | `…_R2_DesignBrief.pdf` |

Biểu mẫu BM04 – Review 2 Commitment được dùng sau buổi review (mục 08).

### Yêu cầu đối với SRS, SDD (Report 3, Report 4)
- Sơ đồ kiến trúc tổng quan (System Overview): các thành phần, dịch vụ bên ngoài và luồng dữ liệu chính.
- Logical ERD và thiết kế cơ sở dữ liệu; database script khớp với ERD.
- State machine diagram cho mỗi thực thể chính có vòng đời.
- Activity diagram hoặc flowchart cho các luồng nghiệp vụ chính của toàn hệ thống, thể hiện sự tham gia của các actor; không cần mô tả xử lý chi tiết bên trong ứng dụng.
- Sơ đồ được lưu dưới dạng mã nguồn (Mermaid hoặc PlantUML) trong repo.

### Khai báo công nghệ trong Report 4

| Nhóm | Nội dung khai báo |
| :--- | :--- |
| **Dịch vụ bên thứ ba** | Tên dịch vụ, mục đích (Authentication, Storage, Payment, AI APIs…), dữ liệu gửi đi |
| **Mã nguồn và DevOps** | Nền tảng (GitHub, GitLab, Azure DevOps), CI/CD, quy tắc nhánh |
| **Công cụ AI** | Công cụ, mô hình và chặng sử dụng (đặc tả, thiết kế, code, kiểm thử, review) |
| **Môi trường triển khai** | Dev, staging, production; nơi triển khai; cách triển khai và rollback |
| **Công nghệ phát triển** | Front-end, Back-end, Mobile, Database, kèm phiên bản |

---

## 04 Phiếu đăng ký phiên bản cuối

Review 2 là mốc cuối cùng được điều chỉnh phiếu đăng ký, và chốt phiếu là việc đầu tiên tại buổi review. Nhóm nộp bản đề xuất chốt; hai giảng viên phản biện xem xét và chốt chính thức. SRS, SDD, báo cáo tự rà soát và Design Brief đều căn cứ trên bản đề xuất này.

- Giữ nguyên mã và tên đề tài đã được duyệt; giữ mẫu phiếu đăng ký của môn học.
- Đánh số phiên bản tiếp theo và kèm Record of Changes theo biểu mẫu BM03.
- Mọi thay đổi phải nhất quán với SRS, SDD và các mục 1, 6, 7 của Design Brief.
- Nếu không có thay đổi, nộp lại phiếu hiện hành và đánh dấu "No changes" trong BM03.

Thay đổi phạm vi được chấp nhận khi có lý do rõ ràng: kết quả Review 1, hiểu biết mới từ Iteration 1, hoặc ràng buộc từ dữ liệu, đối tác, công nghệ. Thay đổi làm giảm đáng kể độ phức tạp nghiệp vụ của đề tài cần được giải trình kỹ.

---

## 05 Báo cáo tự rà soát SRS/SDD

Trước khi nộp, nhóm tự rà soát mức độ nhất quán giữa phiếu đăng ký, SRS, SDD và code bằng prompt chuẩn của môn học. Mục đích là để nhóm tự phát hiện và xử lý các điểm lệch trước buổi review. Số lượng điểm phát hiện không bị trừ điểm; việc che giấu điểm lệch thì có.

- Chạy prompt chuẩn bằng một coding agent có quyền đọc toàn bộ repo, trên đúng phiên bản gắn tag `review-2`, với phiếu đăng ký phiên bản đề xuất chốt làm căn cứ phạm vi. Không chỉnh sửa prompt.
- Điền kết quả gốc của AI vào BM02, không chỉnh sửa, không xoá dòng nào. Ghi rõ công cụ, mô hình, ngày chạy và phiên bản phiếu đăng ký đã dùng.
- Điền cột Team response cho từng điểm: Fixed (kèm link commit), Kept (kèm lý do) hoặc Planned (kèm người chịu trách nhiệm và hạn).
- Các điểm mức High ở trạng thái Kept hoặc Planned phải được liệt kê lại trong mục 5 (Known Gaps) của Design Brief.

### Prompt chuẩn:
```text
You are reviewing the SRS and SDD of a software capstone project against its registration form and source code. Report only; do not change any file.

Inputs: the registration form, SRS, SDD (with diagrams), source code and database script in this repository.

Check:
		Scope: FRs in the SRS not covered by the registration form, and registration items with no FR.
		Requirements: FRs without an actor or testable acceptance criteria; NFRs without metric, threshold and measurement method.
		Data: entities missing from the Logical ERD; ERD entities or attributes missing from the database script, and tables not in the ERD.
		States: for each main entity, states used in code but missing from its state machine; transitions with no triggering FR; states with no exit.
		Flows: main business flows in activity diagrams with no implementation or no test.
		Architecture: components or third-party services in code but not in the architecture diagram, and the reverse.
		Design vs code: modules, APIs or business rules implemented differently from the SDD.

Output one table: ID | Category | Location | Finding | Severity (High/Medium/Low) | Evidence. Then list the 5 most important findings.

If something cannot be verified from the repository, say so; do not guess.
```

Giảng viên phản biện chạy lại prompt trên một số phần của repo để đối chiếu. Báo cáo bị cắt bớt hoặc chỉnh sửa kết quả gốc được xem là thiếu trung thực.

---

## 06 Design Brief

Design Brief là tài liệu chính giảng viên phản biện đọc trước buổi review. Tài liệu đi từ phạm vi đến thiết kế và trách nhiệm; các nội dung chi tiết được dẫn tới phiếu đăng ký, SRS và SDD, không chép lại. Trình bày bằng tiếng Anh, tối đa 4 trang A4 (font 11, lề 2 cm), đủ các mục 0–8 theo đúng thứ tự, dùng biểu mẫu BM01.

| Mục | Tên mục | Nội dung bắt buộc | Giới hạn |
| :---: | :--- | :--- | :--- |
| **0** | Header | Mã và tên đề tài, nhóm, GVHD, link repo (tag `review-2`), phiên bản, ngày | — |
| **1** | Scope | Tóm tắt thay đổi so với phiếu đăng ký hiện hành; những gì nhóm sẽ không làm, kèm lý do | Có thể ghi "No changes" |
| **2** | System at a Glance | Hệ thống giải quyết vấn đề gì, cho ai; tham chiếu sơ đồ kiến trúc | Tối đa 150 từ |
| **3** | Key Design Decisions | Đúng 5 quyết định quan trọng nhất: phương án đã cân nhắc, phương án chọn, lý do, người chịu trách nhiệm | 5 dòng |
| **4** | Top Risks | 3 rủi ro lớn nhất của thiết kế, tác động, cách kiểm chứng hoặc giảm thiểu | 3 dòng |
| **5** | Known Gaps | Các điểm chưa khớp giữa phiếu đăng ký, SRS, SDD và code, kèm kế hoạch xử lý | Không giới hạn |
| **6** | Mandatory Features | Tính năng bắt buộc đề xuất trong phạm vi mục 1, mỗi tính năng có tiêu chí chấp nhận đo được và test tương ứng | Theo phạm vi |
| **7** | Non-Functional Req. | Mỗi NFR có chỉ số, ngưỡng và cách đo | Tối đa 6 dòng |
| **8** | Ownership Map | Mỗi thành viên nhận 2–3 phần và ít nhất một main workflow; mọi phần quan trọng đều có người sở hữu | 1 dòng / thành viên |

Tiêu chí chấp nhận đạt yêu cầu khi viết được thành test và cho kết quả đạt/không đạt rõ ràng. Đạt: *"A fully booked slot cannot be selected; the confirmation email is sent within 60 seconds."* Chưa đạt: *"The booking screen is user-friendly."*

---

## 07 Diễn biến buổi Review 2

Buổi review tập trung vào hỏi đáp và chứng minh trên hệ thống thực, không trình bày lại tài liệu. Nhóm chuẩn bị máy chạy được hệ thống và bộ test, mở sẵn repo và các sơ đồ. Năm phần của buổi review đi theo đúng thứ tự năm mục tiêu:

1. **Chốt phiếu đăng ký:** Rà soát Record of Changes và mục 1 (Scope) của Design Brief; giảng viên phản biện chấp nhận hoặc điều chỉnh từng thay đổi và chốt phiếu.  
   *Nhóm chuẩn bị:* giải trình lý do của từng thay đổi.
2. **Trình bày Design Brief:** 5 quyết định thiết kế, 3 rủi ro và các điểm Kept/Planned trong báo cáo tự rà soát.  
   *Nhóm chuẩn bị:* trình bày trực tiếp trên Design Brief.
3. **Tình huống thay đổi:** Giảng viên đưa một tình huống trong phạm vi đã chốt (thêm một vai trò người dùng, thêm trạng thái cho thực thể chính, dịch vụ bên thứ ba ngừng hoạt động, tải tăng mười lần…). Nhóm phân tích tác động trên sơ đồ và code: thực thể, trạng thái, luồng, API, test nào thay đổi.  
   *Nhóm chuẩn bị:* nắm vững sơ đồ và cấu trúc code; được dùng AI nhưng phải trình bày lập luận.
4. **Chốt yêu cầu sản phẩm và mục tiêu Defense 1.1:** Chốt tính năng bắt buộc, tiêu chí chấp nhận, NFR; main workflow, kịch bản demo, số test case và ngưỡng đạt (mục 09).  
   *Nhóm chuẩn bị:* sẵn sàng điều chỉnh mục 6–7 và Phụ lục A của Design Brief.
5. **Xác minh sở hữu:** Giảng viên chọn ngẫu nhiên từng thành viên, hỏi về phần thành viên đó nhận trong Ownership Map và main workflow mình sở hữu: mở code, giải thích, trả lời các câu hỏi "nếu… thì sao".  
   *Nhóm chuẩn bị:* mỗi thành viên nắm chắc phần mình sở hữu.

### Kết quả xác minh sở hữu

| Mức | Biểu hiện | Theo dõi sau Review 2 |
| :--- | :--- | :--- |
| **Đạt** | Giải thích được, mở đúng code, xử lý được tình huống giả định | Theo dõi như bình thường |
| **Đạt một phần** | Nắm ý chính nhưng lúng túng ở chi tiết hoặc khi có thay đổi | GVHD kiểm tra lại phần này trong giai đoạn phát triển |
| **Chưa đạt** | Không giải thích được phần mình nhận sở hữu | GVHD theo dõi; Defense 1.1 kiểm tra lại thành viên này |

---

## 08 Kết quả Review 2 và Cam kết

| Kết luận | Điều kiện | Việc tiếp theo |
| :--- | :--- | :--- |
| **Đạt** | Thiết kế đứng vững trước tình huống thay đổi; mọi thành viên ở mức Đạt hoặc Đạt một phần | Nhóm vào giai đoạn phát triển theo Cam kết |
| **Đạt có điều kiện** | Có điểm thiết kế cần sửa, hoặc có thành viên ở mức Chưa đạt | GVHD kiểm tra danh sách việc phải sửa theo hạn ghi trong Cam kết |
| **Review lại** | Thiết kế không đáp ứng được tình huống thay đổi, hoặc nhiều thành viên ở mức Chưa đạt | Một buổi review lại với một giảng viên phản biện trước khi nhóm tiếp tục phát triển |

### Review 2 Commitment (`…_R2_Commitment.pdf`)
Trong vòng 3 ngày làm việc sau buổi review, nhóm hoàn thiện biểu mẫu BM04 và nộp có xác nhận của hai giảng viên phản biện và GVHD. Cam kết gồm:
- Phiếu đăng ký phiên bản cuối. Sau mốc này phạm vi đề tài không thay đổi.
- Yêu cầu sản phẩm bắt buộc: tính năng bắt buộc (mỗi tính năng có tiêu chí chấp nhận, thuộc ít nhất một workflow, có một người sở hữu), NFR và danh sách ngoài phạm vi.
- Cam kết theo từng main workflow: người sở hữu, actor, trigger, kết quả nghiệp vụ, đường đi trạng thái; tính năng và test case; kịch bản demo chính và ngoại lệ; số test case theo loại; tiêu chí đạt riêng của workflow.
- Cam kết theo từng thành viên: phần sở hữu; phần phải bàn giao trước Defense 1.1 (test case, test tự động, kịch bản demo, việc phải sửa, tài liệu); kết quả xác minh sở hữu; chữ ký xác nhận.
- Bảng đối chiếu: tổng hợp theo workflow và theo thành viên phải cho cùng con số cam kết; mỗi test case và mỗi việc phải sửa chỉ được tính cho một người.
- Việc phải sửa: theo biên bản Review 2 và các điểm Planned trong báo cáo tự rà soát, mỗi việc gắn với một workflow, một người chịu trách nhiệm và hạn.

### Khi ra Hội đồng, nhóm phải chứng minh

| Hạng mục | Defense 1.1 – xét điều kiện bảo vệ | Viva – bảo vệ chính thức |
| :--- | :--- | :--- |
| **Phạm vi** | Sản phẩm đáp ứng phiếu đăng ký phiên bản cuối | — |
| **Workflow, tính năng** | Đạt toàn bộ ngưỡng ở mục 09.4; đối chiếu từng workflow với khối cam kết tương ứng | Demo theo kịch bản đã chốt, trên dữ liệu do Hội đồng đưa ra |
| **NFR** | Kết quả đo theo đúng cách đo đã chốt | Giải thích kết quả và giới hạn |
| **SRS, SDD** | Chạy lại prompt rà soát trên bản cuối; không còn điểm High chưa giải trình | — |
| **Việc phải sửa** | Tất cả đã hoàn thành, có commit tương ứng | — |
| **Cá nhân** | Mỗi thành viên mở repo, chỉ ra phần mình sở hữu, chạy test; đối chiếu với khối cam kết cá nhân | Vấn đáp cá nhân, có yêu cầu thực hiện một thay đổi nhỏ trên phần mình sở hữu |
| **Hồ sơ** | Report 1–6, Full Software Package, Review 2 Commitment | Report 7, slide bảo vệ, bộ sản phẩm cuối cùng |

---

## 09 Mục tiêu Defense 1.1

Defense 1.1 xác định nhóm có đủ điều kiện ra Viva hay không. Căn cứ là bộ mục tiêu chốt tại Review 2: main workflow, kịch bản demo và test case chức năng. Nhóm đề xuất trong Phụ lục A của Design Brief (BM01); giảng viên phản biện chốt và ghi vào Review 2 Commitment (BM04).

*Các con số dưới đây là mức tối thiểu chung của môn học. Giảng viên phản biện có thể đặt mức cao hơn tùy quy mô đề tài; nhóm có thể cam kết cao hơn, không thấp hơn.*

### Main workflow
Main workflow là một luồng nghiệp vụ trọn vẹn, từ hành động khởi đầu của một actor đến kết quả nghiệp vụ cuối cùng. Workflow hợp lệ khi có ít nhất hai actor hoặc đi qua ít nhất ba tính năng bắt buộc, và làm thay đổi trạng thái của ít nhất một thực thể chính.
- Số lượng tối thiểu bằng số thành viên của nhóm.
- Sở hữu: mỗi workflow có đúng một người sở hữu; mỗi thành viên sở hữu ít nhất một workflow.
- Truy vết: mỗi workflow tương ứng với một activity diagram trong SDD.

| ID | Workflow | Actors | Kết quả nghiệp vụ | Tính năng | Trạng thái thực thể | Owner |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-01** | Đặt và hoàn tất lịch khám | Bệnh nhân, Lễ tân, Bác sĩ | Lịch khám hoàn tất, hồ sơ khám được ghi nhận | F-01, F-04, F-07 | Appointment: Pending → Confirmed → Completed | Nguyễn Văn A |

### Kịch bản demo
- Mỗi main workflow có ít nhất một kịch bản chính và một kịch bản ngoại lệ (dữ liệu sai, vi phạm quy tắc nghiệp vụ, không đủ quyền, dịch vụ ngoài lỗi…).
- Demo chạy trên môi trường staging đã khai báo trong Report 4, không chạy trên máy cá nhân.
- Dữ liệu mẫu được nạp bằng script có trong repo, để demo lặp lại được.
- Hội đồng có thể chọn ngẫu nhiên kịch bản và thay đổi dữ liệu đầu vào.

| ID | Workflow | Loại | Tiền điều kiện và dữ liệu | Các bước chính | Kết quả mong đợi |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **DS-01a** | WF-01 | Chính | Khung giờ 9:00 còn 1 chỗ | Đặt lịch → Lễ tân xác nhận → Bác sĩ hoàn tất | Lịch ở trạng thái Completed; email xác nhận được gửi |
| **DS-01b** | WF-01 | Ngoại lệ | Khung giờ 9:00 đã đủ chỗ | Đặt lịch vào khung giờ đã đủ | Hệ thống từ chối, đề xuất khung giờ khác |

### Test case chức năng
Số test case tối thiểu tính theo công thức dưới đây và ghi thành con số cố định trong Review 2 Commitment.

| Nguồn | Tối thiểu | Yêu cầu |
| :--- | :---: | :--- |
| **Mỗi tính năng bắt buộc** | 3 | 1 luồng chính; 1 kiểm tra dữ liệu hoặc quy tắc nghiệp vụ; 1 ngoại lệ hoặc phân quyền |
| **Mỗi main workflow** | 1 | Test end-to-end theo kịch bản chính |
| **Mỗi chuyển trạng thái của thực thể chính** | 1 | Bao gồm cả chuyển trạng thái không hợp lệ bị từ chối |
| **Mỗi NFR đã chốt** | 1 | Test đo theo đúng chỉ số và ngưỡng |

*Ví dụ:* $$15\text{ tính năng} \times 3 + 5\text{ workflow} + 12\text{ chuyển trạng thái} + 4\text{ NFR} = 66\text{ test case}$$

Test case được quản lý trong Report 5 với các trường: ID, tính năng hoặc workflow liên quan, tiền điều kiện, các bước, dữ liệu, kết quả mong đợi, cách chạy (tự động hoặc thủ công), kết quả thực tế.

### Ngưỡng đủ điều kiện ra Viva

| Tiêu chí | Ngưỡng tối thiểu | Cách Defense 1.1 kiểm tra |
| :--- | :--- | :--- |
| **Main workflow** | 100% workflow chạy được end-to-end | Chọn ngẫu nhiên ít nhất 2 workflow, nhóm demo trực tiếp |
| **Kịch bản demo** | 100% kịch bản chính đạt; ít nhất 80% kịch bản ngoại lệ đạt | Demo theo kịch bản, Hội đồng có thể thay dữ liệu |
| **Số test case** | Không thấp hơn con số đã cam kết | Đối chiếu Report 5 với Commitment |
| **Tỷ lệ test đạt** | Ít nhất 90% tổng số; 100% test luồng chính của tính năng bắt buộc | Chạy lại bộ test tự động; đối chiếu test report |
| **Tự động hoá** | Ít nhất 50% test case chạy tự động trong CI | Chạy CI trên bản nộp |
| **Defect** | Không còn defect mức Critical hoặc High đang mở | Đối chiếu defect list |
| **Cá nhân** | Mỗi thành viên demo được workflow mình sở hữu và hoàn thành cam kết cá nhân | Kiểm tra từng thành viên |

*Kết luận Defense 1.1:* đạt tất cả tiêu chí thì nhóm đủ điều kiện ra Viva. Không đạt tiêu chí nào thì nhóm chuyển sang Hội đồng 2, kèm biên bản ghi rõ tiêu chí chưa đạt.

---

## 10 Quy định sử dụng AI

Sinh viên được sử dụng công cụ AI (trợ lý, coding agent) ở mọi giai đoạn của đồ án, với các điều kiện sau:

- **Khai báo:** Công cụ, mô hình và chặng sử dụng được khai báo trong Report 4.
- **Chịu trách nhiệm:** Mỗi thành viên chịu trách nhiệm về mọi nội dung thuộc phần mình sở hữu, kể cả nội dung do AI tạo ra, và phải giải thích được nội dung đó.
- **Bảo vệ dữ liệu:** Không đưa vào công cụ AI dữ liệu thật của đối tác, dữ liệu cá nhân, mật khẩu, khoá API hay thông tin bí mật; dùng dữ liệu giả lập khi cần.
- **Trung thực:** Không chỉnh sửa kết quả gốc của báo cáo tự rà soát; không trình bày nội dung AI tạo ra như kết quả phân tích của nhóm khi chưa kiểm chứng.
- **Kiểm chứng:** Mọi đầu ra của AI đưa vào sản phẩm phải qua review hoặc test trước khi hợp nhất vào nhánh chính.

---

## 11 Checklist nộp hồ sơ

- [ ] `…_R2_Registration.pdf`: phiếu đăng ký phiên bản đề xuất chốt kèm Record of Changes (BM03)
- [ ] `…_R2_Report<n>.pdf`: Report 1–5 (SRS, SDD nhất quán với phiếu đề xuất chốt) và Project Tracking cập nhật
- [ ] Report 4 có sơ đồ kiến trúc, Logical ERD, state machine, activity diagram và bảng khai báo công nghệ
- [ ] Repo gắn tag `review-2`; Iteration 1 chạy được; CI chạy test tự động
- [ ] `…_R2_SelfReview.pdf` (BM02): kết quả gốc, thông tin lần chạy, Team response cho mọi điểm
- [ ] `…_R2_DesignBrief.pdf` (BM01): đủ mục 0–8, tối đa 4 trang, kèm Phụ lục A, có link repo
- [ ] Mỗi thành viên đã ôn kỹ phần mình sở hữu và main workflow của mình

### Danh sách biểu mẫu

| Biểu mẫu | File phát kèm | Dùng cho |
| :---: | :--- | :--- |
| **BM01** | `SWP490_R2_BM01_Design_Brief.docx` | Design Brief và Phụ lục A – Defense 1.1 Targets |
| **BM02** | `SWP490_R2_BM02_Self_Review_Report.docx` | Báo cáo tự rà soát SRS/SDD |
| **BM03** | `SWP490_R2_BM03_Record_of_Changes.docx` | Record of Changes của phiếu đăng ký |
| **BM04** | `SWP490_R2_BM04_Review2_Commitment.docx` | Review 2 Commitment, hoàn thiện sau buổi review |

---

*Mọi thắc mắc về lịch, phân công giảng viên phản biện và hồ sơ, nhóm liên hệ GVHD hoặc bộ phận quản lý Capstone. Tài liệu có thể được cập nhật; phiên bản mới nhất sẽ được thông báo tới các nhóm.*