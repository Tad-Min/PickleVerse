# PickleVerse — Rà soát đề xuất và tiêu chí đánh giá tiến độ

Ngày rà soát: **27/10/2026**. Góc nhìn: trưởng nhóm dự án.

**Trạng thái: đề xuất để nhóm thống nhất, chưa phải biên bản nghiệm thu.** Các ngưỡng ghi “đề xuất” bên dưới là cách đo bổ sung; chúng không tự thay thế cam kết trong proposal.

## 1. Nhận định của trưởng nhóm

Hệ thống có một vòng trải nghiệm hợp lý: **học kỹ thuật → luyện drill → thi đấu → nhận phản hồi → chọn bài luyện tiếp theo**. Đây nên là luồng chính để đánh giá sản phẩm. Tám nhóm cần theo dõi là gameplay, training, multiplayer, AI, backend, admin, yêu cầu phi chức năng và bàn giao.

Phạm vi 4 sinh viên/14 tuần khá rộng: Windows, Android, iOS; 1v1/2v2; AI; website quản trị; triển khai và nhiều chỉ tiêu vận hành. Rủi ro chính là tích hợp và chứng minh chất lượng. Số màn hình, số commit hoặc số API chỉ mô tả khối lượng công việc, không đủ chứng minh hệ thống đã hoàn thành.

Đợt rà soát này đọc proposal, tài liệu triển khai, một số mã nguồn liên quan và artifact kiểm chứng hiện có. **Không chạy lại game, bộ kiểm thử, thiết bị thật hoặc kiểm thử tải trong đợt này.** Vì chưa có danh mục công việc cùng trọng số và kết quả nghiệm thu cập nhật, chưa có cơ sở chốt một tỷ lệ hoàn thành hiện tại.

## 2. Các điểm cần làm rõ trước khi chấm tiến độ

| Điểm rà soát | Bằng chứng hiện có | Ảnh hưởng đến đánh giá |
|---|---|---|
| Kiến trúc multiplayer khác phương án đăng ký | Proposal yêu cầu dedicated server có thẩm quyền mô phỏng. `PhotonSession.cs` dùng `GameMode.Shared` ở cả tạo và vào phòng. Tài liệu triển khai cũng xác nhận một client giữ state authority. | Có thể ghi nhận tiến độ online, nhưng chưa nghiệm thu được cam kết dedicated server/chống gian lận bằng mô phỏng trên server. Việc đổi thư viện sang Photon đã nằm trong lựa chọn của proposal; khác biệt cần xử lý là topology và mô hình tin cậy. |
| Backend khác công nghệ dự kiến | Proposal nêu Spring Boot hoặc FastAPI; API hiện là ASP.NET Core, target `net10.0`. | Cập nhật quyết định kiến trúc, SDD và phương án triển khai cho khớp sản phẩm. Không xem thay đổi ngôn ngữ tự thân là lỗi chức năng. |
| Mốc thời gian phản hồi AI chưa thống nhất | Proposal: trong 1 giây sau drill hoặc point. Hybrid AI xử lý sau phiên, có mục tiêu nội bộ p95 dưới 5 giây. | Phải xác định thời điểm bắt đầu/kết thúc phép đo và thời điểm người chơi nhận phản hồi. Mốc 5 giây không tự thay cho mốc 1 giây. |
| Phần AI đã có triển khai nhưng chưa đủ bằng chứng cho mọi cam kết | Có rule engine, pipeline huấn luyện và artifact kiểm tra cục bộ. Tài liệu Hybrid AI xác nhận chưa có model production hoặc bộ dữ liệu nhãn chuyên gia. | Chấm riêng bot, luật phản hồi, gợi ý drill và classifier. Kiểm thử bằng dữ liệu tổng hợp chứng minh pipeline, chưa chứng minh độ chính xác trên người chơi. |
| Phạm vi offline và tính năng xã hội không nhất quán | Proposal đưa offline, friends và daily challenge vào yêu cầu; bản Core Features xếp chúng vào mở rộng. `ProgressionRuntime` hiện yêu cầu trạng thái tài khoản/server. | Lấy proposal làm baseline tạm thời cho lần rà soát này. Đánh dấu chưa nghiệm thu hoặc ghi nhận thay đổi phạm vi được xác nhận; không tự bỏ khỏi mẫu số. Spool telemetry không đồng nghĩa đã có luyện tập offline và đồng bộ tiến trình. |
| Admin chưa đủ các luồng đã hứa | README frontend và controller hiện có cho thấy còn thiếu edit/publish nội dung, appeals và một số analytics. Có chế độ mock để trình diễn UI. | Chỉ nghiệm thu khi chạy qua API và dữ liệu thật. Việc có màn hình quản trị hoặc build thành công chưa chứng minh quản trị đầy đủ. |
| Chỉ số hoạt động có sai lệch cụ thể | `AdminService.GetAnalyticsSummaryAsync` gán `ActiveUsersToday = totalUsers`. | DAU chưa được tính đúng từ hoạt động trong ngày. Cần bộ dữ liệu đối chiếu cho từng chỉ số trước khi chấm analytics đạt. |
| Bằng chứng kiểm thử thuộc nhiều thời điểm | Gói Review 2 ngày 23/10 ghi 184 ca sản phẩm chưa chạy; báo cáo và artifact Hybrid AI ngày 27/10 có kết quả kiểm tra mới hơn. | Không kết luận “toàn bộ chưa test”, cũng không cộng các con số pass thành mức bao phủ toàn hệ thống. Cần ánh xạ từng kết quả vào yêu cầu, phiên bản và môi trường tương ứng. |
| Các NFR chưa có đầy đủ bằng chứng nghiệm thu được đối chiếu trong lần rà soát này | Benchmark AI hiện là máy phát triển/loopback; tài liệu còn yêu cầu nghiệm thu Android và gameplay mạng thật. | Không suy rộng kết quả local thành đạt 60 FPS đa nền tảng, RTT dưới 100 ms, 2.000 người chơi, 500 trận hoặc uptime 99,5%. |

**Ưu tiên xử lý:** thống nhất baseline và mô hình authority; chứng minh một luồng hoàn chỉnh bằng client/API thật; sau đó hoàn thiện AI, admin và bằng chứng hiệu năng. Các hạng mục hình ảnh, sân bổ sung hoặc cosmetic chỉ nên ưu tiên sau các điểm nghẽn này.

## 3. Cách tính tiến độ có thể kiểm chứng

### 3.1. Trọng số đề xuất

| Nhóm | Trọng số | Sản phẩm cần đánh giá |
|---|---:|---|
| G1. Gameplay và luật | 15% | Di chuyển, đánh bóng, vật lý, ghi điểm và luật đơn/đôi |
| G2. Training và progression | 15% | Tutorial, kỹ thuật, drill, sao, XP, mở khóa, tiến trình |
| G3. Multiplayer và xếp hạng | 25% | Phòng, ghép trận, 1v1/2v2, đồng bộ, mất kết nối, kết quả và Elo |
| G4. AI và coaching | 15% | Bot thích ứng, telemetry, nhận diện lỗi, gợi ý drill, classifier |
| G5. Backend, dữ liệu và bảo mật | 10% | Tài khoản, phân quyền, lưu trữ, giao dịch, quyền truy cập |
| G6. Admin web | 8% | Quản trị người dùng/nội dung, giám sát, moderation, analytics |
| G7. Yêu cầu phi chức năng | 7% | Hiệu năng, tải, khả dụng, tương thích, offline và đồng bộ |
| G8. Kiểm thử và bàn giao | 5% | Quản lý bằng chứng, UAT, build triển khai, tài liệu và bàn giao |
| **Tổng** | **100%** | |

Mỗi nhóm được chia thành đầu việc có tiêu chí chấp nhận riêng. Chốt trọng số con trước khi bắt đầu chấm; tổng trọng số con bằng trọng số nhóm. Một yêu cầu chỉ có một trọng số, dù bằng chứng của nó được tham chiếu ở nhiều nhóm. Ví dụ độ trễ AI tính ở G7, độ chính xác phản hồi tính ở G4.

### 3.2. Mức hoàn thành cho từng đầu việc

| Hệ số | Điều kiện để được ghi nhận |
|---:|---|
| 0 | Chưa bắt đầu hoặc chưa có bằng chứng cho mức đầu tiên |
| 0,25 | Có yêu cầu, tiêu chí chấp nhận, thiết kế/hợp đồng và người phụ trách rõ ràng |
| 0,50 | Có bản thực hiện chạy được và kiểm tra ở phạm vi module; minh chứng gắn với phiên bản |
| 0,75 | Đã tích hợp trong môi trường kiểm thử, đi hết luồng thực và kiểm tra tình huống lỗi chính |
| 1,00 | Đạt tiêu chí chấp nhận, được người kiểm tra khác xác nhận, có bằng chứng và tài liệu bàn giao liên quan |

Các mức có tính tích lũy. Với đầu việc tài liệu hoặc vận hành, thay “module” bằng bản tài liệu/cấu hình tương ứng; vẫn phải có bước kiểm tra độc lập và nghiệm thu. Có code nhưng chưa chứng minh chạy được không tự đạt mức 0,50. Có UI dùng mock chưa đủ mức tích hợp 0,75.

```text
Tiến độ có bằng chứng (%) = Σ(trọng số đầu việc × hệ số hoàn thành)

Phạm vi đã nghiệm thu (%) = Σ(trọng số các đầu việc có hệ số 1,00)
```

Ví dụ một đầu việc trọng số 4 điểm, đã tích hợp nhưng chưa nghiệm thu, đóng góp `4 × 0,75 = 3` điểm tiến độ và 0 điểm phạm vi đã nghiệm thu. Báo cáo song song hai chỉ số này để tránh gọi thiết kế/code dở dang là chức năng hoàn chỉnh.

Chưa đủ thông tin để chấm thì ghi **CHƯA ĐÁNH GIÁ**, không bịa điểm. Khi cần công bố điểm bảo thủ, chỉ ghi nhận mức có bằng chứng; công bố kèm tỷ trọng chưa đánh giá. Không sửa trọng số hoặc loại yêu cầu chỉ để tăng phần trăm.

## 4. Tiêu chí chấp nhận theo từng nhóm

Các số lượng chạy thử và ngưỡng chất lượng mới trong phần này là **đề xuất nghiệm thu**. Các cam kết có sẵn trong proposal được giữ nguyên cho tới khi có quyết định thay đổi phạm vi.

| Nhóm | Điều kiện đạt có thể quan sát | Bằng chứng tối thiểu |
|---|---|---|
| G1. Gameplay và luật | 100% ca luật bắt buộc đã thống nhất đạt: giao bóng, two-bounce, kitchen/NVZ, bóng trong/ngoài sân, ghi điểm, đổi lượt và kết thúc trận; bao phủ cả đơn và đôi. Không còn lỗi khiến trận không thể tiếp tục. | Danh sách luật/biến thể được chọn, kịch bản biên, test report và video các tình huống tiêu biểu |
| G2. Training và progression | Chốt danh sách khoảng 5 kỹ thuật/15 drill từ proposal thành ID cụ thể; mỗi drill có mục tiêu, điều kiện mở khóa, cách chấm 1–3 sao và thưởng. Chơi đủ danh sách; replay/retry không cấp thưởng trùng; tiến trình đúng sau đăng xuất/đăng nhập. | Ma trận kỹ thuật–drill–test, catalog phiên bản, log giao dịch và hồ sơ trước/sau |
| G3. Multiplayer và xếp hạng | Tạo/vào phòng, ready, hủy hàng chờ, timeout và ghép đúng đội hoạt động. Đề xuất chạy tối thiểu 10 trận 1v1 và 10 trận 2v2 trên client độc lập; 2v2 có đủ 4 người thật. Tỷ số, lượt giao và kết quả cuối rally thống nhất ở các client; trận chỉ chốt thưởng/Elo một lần. Mất mạng, thoát app và mất client giữ authority được xử lý theo policy đã chốt. Kiểm tra thêm RTT mô phỏng 100/200 ms, mất gói 1%/3%, jitter 20 ms; ghi rõ từng cấu hình. | Build ID, sơ đồ topology, cấu hình mạng, log các client/backend, video và dữ liệu kết quả/Elo. Đây là mẫu nghiệm thu chức năng, không thay thế kiểm thử tải |
| G4. AI và coaching | Bot có 3 mức khó và chính sách thích ứng được mô tả; chạy được trong local/online mà không phá luật. Phản hồi truy được về telemetry và nhãn đúng, thiếu dữ liệu phải được biểu diễn là chưa đủ thông tin. Đề xuất precision ≥0,85 và recall ≥0,80 cho từng loại lỗi được tuyên bố hỗ trợ trên tập đánh giá độc lập; tối thiểu 50 cơ hội quan sát có nhãn cho mỗi loại lỗi. Drill đề xuất liên quan đến lỗi và tôn trọng prerequisite. Classifier chỉ đạt khi vượt gate dữ liệu/model bên dưới. | Policy bot, fixture/seed, dữ liệu có nhãn độc lập, cách chia người chơi, confusion matrix, danh sách lỗi thực sự được hỗ trợ và báo cáo recommendation |
| G5. Backend, dữ liệu và bảo mật | Đăng ký/xác thực/đăng nhập/refresh/logout hoạt động. Chưa đăng nhập bị từ chối, Player không dùng API Admin, người dùng không đọc/sửa dữ liệu tài khoản khác. Retry thao tác ghi không nhân đôi dữ liệu; lỗi giữa giao dịch không tạo trạng thái nửa chừng. Có bằng chứng cấu hình HTTPS, JWT và mã hóa lưu trữ AES-256 theo proposal. | Test HTTP thực tế, kiểm thử ownership/giao dịch, log đã che thông tin nhạy cảm và cấu hình triển khai. Quyền mô phỏng trận đánh giá riêng ở G3 |
| G6. Admin web | Chạy tắt mock; quản lý tài khoản, tạo/sửa/publish/unpublish bài học, giám sát trận và xử lý báo cáo/khiếu nại đúng quyền trong phạm vi đã chốt. Nội dung publish xuất hiện trong catalog và có thể chơi trong game. Analytics khớp bộ dữ liệu chuẩn theo khoảng thời gian xác định. | Video trình duyệt → API → DB → game; test Player/Admin; fixture analytics với kết quả kỳ vọng |
| G7. NFR | Đạt từng phép đo trong mục 5 trên môi trường được mô tả. Nếu giữ offline trong baseline, phải luyện tập khi mất mạng và đồng bộ tiến trình đúng một lần sau kết nối lại, kể cả khi app bị đóng. | Profiler, báo cáo thiết bị, tải, giám sát, kịch bản mất mạng/app kill và đối chiếu dữ liệu |
| G8. Kiểm thử và bàn giao | 100% ca bắt buộc trong phạm vi đã chạy; 100% ca P0/P1 đạt; đề xuất ít nhất 95% tổng ca trong phạm vi đạt. Không còn lỗi P0/P1. Có build cài được, hướng dẫn triển khai/khôi phục, SRS/SDD/API/schema cập nhật và UAT có kết quả ghi nhận. | Test register cập nhật, lỗi còn lại, release manifest, tài liệu, checklist triển khai và biên bản UAT |

**P0**: chặn luồng cốt lõi hoặc gây mất/sai dữ liệu nghiêm trọng, truy cập trái phép nghiêm trọng. **P1**: chức năng bắt buộc không dùng được và không có cách khắc phục tạm chấp nhận được. Độ ưu tiên của tính năng và mức nghiêm trọng của lỗi cần ghi thành hai trường riêng khi theo dõi.

### Gate riêng cho classifier và ý nghĩa huấn luyện

- Gate hiện được mô tả trong `src/AiService/README.md`: chia train/holdout theo người chơi; holdout ít nhất 10 người mỗi tier; macro-F1 ≥0,70 và hơn majority baseline ít nhất 0,05. Không dùng model fixture làm model nghiệm thu.
- Nhãn kỹ thuật phải được đánh giá độc lập. Tách người chơi giữa train và test để tránh kết quả đẹp do cùng người xuất hiện ở cả hai tập.
- Phân biệt classifier ước lượng tier với bộ phát hiện lỗi kỹ thuật: vượt gate của một phần không chứng minh phần còn lại đạt.
- Không coi thông số aim/timing được game hỗ trợ là bằng chứng độc lập về lỗi của người chơi. Chỉ công bố các loại lỗi có thể quan sát và đã hiệu chỉnh; ví dụ `DinkTooHigh` hiện còn gate hiệu chỉnh riêng.
- Phản hồi sau phiên, phản hồi sau point và phản hồi ngay sau drill là ba thời điểm khác nhau; phải ghi đúng hành vi đã làm được.
- Phản hồi dựa trên gameplay chứng minh hỗ trợ học trong game. Muốn tuyên bố cải thiện kỹ thuật pickleball ngoài đời cần nghiên cứu riêng; dữ liệu trong game chưa chứng minh kết quả đó.

## 5. Đo các yêu cầu phi chức năng như thế nào

| Yêu cầu trong proposal | Cách đo/điều kiện bổ sung đề xuất | Giới hạn cần công bố |
|---|---|---|
| RTT <100 ms | Chốt vùng server, nhà mạng, loại kết nối và thiết bị. Đề xuất p95 RTT <100 ms, ít nhất 1.000 mẫu cho mỗi cấu hình mạng đại diện; tách RTT khỏi thời gian phản hồi input và độ lệch trạng thái. | Ping thấp không chứng minh prediction/reconciliation hoặc gameplay đúng; ghi riêng bằng chứng cơ chế đồng bộ |
| 60 FPS ổn định trên thiết bị tầm trung | Chỉ rõ model máy, OS, độ phân giải, mức đồ họa; đo training/1v1/2v2. Đề xuất sau warm-up chạy liên tục 20 phút, p95 frame time ≤16,7 ms và p99 ≤33,3 ms. | Số đo Windows Editor không thay cho Android/iOS; ghi cả nhiệt, bộ nhớ và crash |
| Phản hồi AI trong 1 giây sau drill/point | Đề xuất p95 ≤1.000 ms từ sự kiện kết thúc đã xác định tới lúc UI hiển thị phản hồi dùng được; tách upload, chờ hàng đợi, phân tích, tải và render kết quả. | Benchmark local hiện có 10 phiên steady, coaching ready p95 khoảng 855 ms; chưa phải phép đo UI qua Internet và không chứng minh phản hồi sau từng point |
| 2.000 người chơi, 500 trận đồng thời | Mô tả người online/người trong trận, tỷ lệ 1v1/2v2 và hạ tầng. Đề xuất giữ tải mục tiêu ít nhất 30 phút, theo dõi kết nối, server tick, CPU/RAM, API/AI/DB và mất dữ liệu; tỷ lệ lỗi request ngoài lỗi nghiệp vụ dự kiến <1%. | 2.000 kết nối API không thay cho 500 trận có mô phỏng. Phải nêu rõ phần nào là người giả lập, phần nào là gameplay thực |
| Uptime 99,5% | Chốt cửa sổ giám sát, ví dụ tháng; định nghĩa sự cố từ các luồng cốt lõi, thu thập số phút dịch vụ dùng được/tổng phút trong cửa sổ. | Chạy demo hoặc soak test ngắn chưa chứng minh SLA theo tháng |
| Windows 10+, Android 9+, iOS 14+ | Chốt phiên bản Unity/build hỗ trợ các OS này; cài và kiểm tra luồng cốt lõi trên ma trận thiết bị/OS tối thiểu cùng thiết bị đại diện. | Export/build thành công chưa chứng minh tương thích runtime. Nếu không hỗ trợ được OS đã đăng ký, ghi chênh lệch và quyết định thay đổi |
| Luyện tập offline, sync khi reconnect | Mất mạng trước và trong drill; đóng/mở lại app; kết nối lại và retry. Tiến trình hợp lệ không mất, không thưởng trùng, không lẫn tài khoản; có policy xử lý xung đột và dữ liệu không đáng tin. | Không đồng nhất cơ chế gửi telemetry sau mạng lỗi với offline progression |

Đối với chỉ tiêu chưa thể đo trên hạ tầng hiện có, ghi **CHƯA KIỂM CHỨNG** cùng kế hoạch và người phụ trách. Nếu muốn giảm mục tiêu để phù hợp capstone, lập thay đổi phạm vi cụ thể; chưa có thay đổi được xác nhận thì cam kết cũ vẫn là cơ sở đánh giá.

## 6. Các mốc kiểm tra cho kế hoạch 14 tuần

Đây là mốc tương đối theo proposal; chưa có ngày bắt đầu dự án được đối chiếu nên không quy đổi thành ngày lịch hoặc kết luận nhóm đang chậm bao nhiêu tuần.

| Mốc | Điều kiện kết thúc cần kiểm chứng |
|---|---|
| Cuối tuần 2 — nền tảng | Build chạy được; sân/bóng/điều khiển cơ bản; tài khoản và DB đi hết luồng. Chốt topology, baseline yêu cầu, owner và test plan. Có build/kiểm tra tự động nền tảng |
| Cuối tuần 4 — học và luyện | Serve/dink/volley có drill, sao, XP và lưu tiến trình thật. Sớm chốt schema telemetry, kế hoạch lấy nhãn AI và khung đăng nhập/phân quyền Admin |
| Cuối tuần 7 — online 1v1 | Hai client độc lập chơi hết trận, lưu lịch sử và xử lý disconnect. Có thử nghiệm sớm bốn client/đội hình 2v2 để phát hiện rủi ro trước tuần 10 |
| Cuối tuần 9 — bot, rating và dữ liệu | Bot thích ứng, Elo, matchmaking và telemetry tích hợp; chống ghi thưởng/Elo lặp. Có dữ liệu đánh giá AI độc lập hoặc ghi rõ điểm nghẽn về dữ liệu |
| Cuối tuần 11 — vòng trải nghiệm hoàn chỉnh | 2v2 bốn người; bộ kỹ thuật/drill đã chốt; phản hồi lỗi và gợi ý drill đi hết luồng thật. Classifier được chấm theo gate riêng; friends/daily/offline được theo dõi đúng baseline |
| Cuối tuần 13 — sẵn sàng nghiệm thu | Admin dùng API thật; nội dung quản trị chơi được; analytics đúng. Kiểm thử tích hợp, tải, thiết bị và bảo mật có kết quả. Không còn P0/P1; mọi chênh lệch được ghi rõ |
| Tuần 14 — bàn giao | Build phát hành và môi trường triển khai tái lập được; UAT hoàn tất, hồi quy bản cuối, tài liệu khớp sản phẩm, danh sách giới hạn và kết quả nghiệm thu được xác nhận |

Điều chỉnh quản lý đề xuất: không để tích hợp Admin, tìm dữ liệu AI và kiểm thử thiết bị tới cuối dự án. Các việc này cần tiến hành sớm theo các mốc trên, dù package hoàn thiện vẫn theo lịch proposal.

## 7. Báo cáo tiến độ hằng tuần

### Mẫu một dòng công việc

| ID yêu cầu/đầu việc | Nhóm và trọng số | Người phụ trách | Hạn | Hệ số có bằng chứng | Test và phiên bản | Kết quả nghiệm thu | Điểm nghẽn/bước tiếp theo |
|---|---|---|---|---|---|---|---|
| Ví dụ: MP-2V2-01 — hoàn tất trận bốn người | G3 / trọng số con chưa chốt | Chưa gán | Tuần 11 | Chưa đánh giá | Build/hash, case ID, video/log | Chưa nghiệm thu | Thực hiện trận trên bốn client độc lập |

Không dùng dòng ví dụ để tính điểm thực tế. Mỗi dòng thật phải có điều kiện đạt, người làm và người kiểm tra khác nhau; kết quả âm tính và ca chưa chạy cũng cần được giữ lại.

### Chỉ số trưởng nhóm cần đọc

| Chỉ số | Công thức/cách dùng |
|---|---|
| Tiến độ có bằng chứng và phạm vi đã nghiệm thu | Theo mục 3; báo cáo chênh lệch so với kế hoạch đã chốt |
| Tỷ lệ hoàn thành cam kết tuần | Tổng trọng số đầu việc được nghiệm thu đúng hạn / tổng trọng số cam kết tuần; không dùng số ticket đơn thuần |
| Bao phủ yêu cầu bắt buộc | Số yêu cầu bắt buộc đã ánh xạ test / tổng yêu cầu bắt buộc |
| Tỷ lệ ca đã chạy | Số ca đã thực thi / tổng ca thuộc phạm vi; ca chưa chạy vẫn trong mẫu số |
| Tỷ lệ ca đạt trên phạm vi | Số ca đạt / tổng ca thuộc phạm vi; đọc kèm tỷ lệ ca đã chạy |
| Lỗi và điểm nghẽn | P0/P1 còn mở, tuổi lỗi, owner, hạn xử lý; đầu việc bị chặn và ảnh hưởng mốc |
| NFR | Số đo mới nhất, môi trường, kích thước mẫu, xu hướng và cam kết chưa kiểm chứng |

Màu trạng thái đề xuất dựa trên chênh lệch điểm tiến độ thực tế so với kế hoạch tại ngày báo cáo:

- **Xanh:** chậm không quá 5 điểm phần trăm và không có điểm nghẽn đe dọa mốc gần nhất.
- **Vàng:** chậm trên 5 đến 10 điểm, hoặc có rủi ro phải có phương án xử lý và hạn cụ thể.
- **Đỏ:** chậm trên 10 điểm, lỡ mốc bắt buộc, hoặc có P0/P1 đang chặn mốc gần nhất.

Chưa có baseline kế hoạch hoặc thiếu bằng chứng đáng kể thì ghi **CHƯA ĐỦ DỮ LIỆU**, không gắn màu xanh. Điểm cao ở UI/cosmetic không bù điều kiện bắt buộc còn thiếu ở luật, multiplayer, dữ liệu hay AI.

## 8. Điều kiện kết luận hoàn thành hệ thống

Chỉ kết luận **hoàn thành phạm vi đã chốt** khi mọi đầu việc bắt buộc đạt tiêu chí nghiệm thu; không còn P0/P1; NFR có bằng chứng theo cam kết; build/tài liệu/triển khai và UAT đầy đủ. Nếu còn phần mở rộng, phải tách rõ với phạm vi bắt buộc ngay từ baseline.

Nếu giữ nguyên proposal, dedicated authority, classifier đã đánh giá, offline và các nền tảng/NFR đã nêu vẫn phải có kết quả tương ứng. Nếu nhóm chọn sản phẩm phù hợp hơn với thời gian còn lại, cần ghi quyết định thay đổi, tác động tới mục tiêu đề tài và cập nhật proposal/SRS/SDD trước khi dùng baseline mới.

## 9. Nguồn đối chiếu trong repository

- [Proposal gốc](PickleVerse_Capstone_Proposal.md): chức năng, NFR, công nghệ và kế hoạch 14 tuần.
- [Core Features](PickleVerse_Core_Features.md): phân loại chức năng cốt lõi/mở rộng để đối chiếu phạm vi.
- [PhotonSession.cs](../../My%20project/Assets/Scripts/Multiplayer/PhotonSession.cs): tạo/vào phòng dùng `GameMode.Shared`, dòng 168 và 199 tại thời điểm đọc.
- [API.csproj](../../src/Backend/API/API.csproj): nền tảng ASP.NET Core hiện tại.
- [ProgressionRuntime.cs](../../My%20project/Assets/Scripts/Progression/ProgressionRuntime.cs): hồ sơ trong bộ nhớ dựa vào trạng thái server/tài khoản.
- [AdminController.cs](../../src/Backend/API/Controllers/AdminController.cs), [AdminService.cs](../../src/Backend/Services/Implementations/AdminService.cs) và [Frontend README](../../src/Frontend/README.md): phạm vi API quản trị, chế độ mock và analytics.
- [Hybrid AI — triển khai và nghiệm thu](../Hybrid_AI_Implementation.md) và [AI Service README](../../src/AiService/README.md): cách xử lý sau phiên, gate model, kết quả local và phần chưa nghiệm thu.
- [Benchmark HTTP](../../artifacts/verification/hybrid-http-benchmark.json), [Python test report](../../artifacts/verification/hybrid-python-tests.xml): artifact hiện hữu đã đọc, không phải kết quả chạy mới của đợt rà soát này.
- [Review 2 README](../Review_2/README.md), [Validation Report](../Review_2/Validation_Report.md), [Feature Traceability](../Review_2/Feature_Traceability.csv): snapshot 23/10, dùng tham khảo lịch sử; trạng thái phải được đối chiếu lại với source hiện tại.
- [Tài liệu triển khai tổng hợp](../PickleVerse_Project_Documentation.md): giới hạn Fusion Shared và các luồng đã nối backend; các phần lịch sử phản ánh thời điểm tài liệu gốc.
- [Báo cáo mời bạn bè](../Proccess/Friend_Room_Invitations_20261025.md): minh chứng tích hợp hai peer và giới hạn so với nghiệm thu trận bốn người/đa thiết bị.

Các kết quả cũ cần gắn đúng build/hash, thời gian, môi trường và case ID trước khi được dùng để chốt điểm. Workspace đang có thay đổi chưa commit; chỉ tham chiếu HEAD sẽ không định danh đủ phiên bản sản phẩm đã được đọc.
