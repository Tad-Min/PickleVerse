# Thu thập và duyệt nhãn AI cho PickleVerse

## Chuẩn bị môi trường

Chạy API và AI local từ cùng phiên bản mã với schema `src/Database.sql`. Xác minh API sẵn sàng và Editor trỏ `http://127.0.0.1:8080`. Dùng `docker-compose.functional-local.yml` để bật tiếp nhận telemetry và phân tích theo luật. Khi thu thập, giữ phân loại ML và hiệu chỉnh Dink ở trạng thái tắt.

Chạy web admin từ `src/Frontend` với `VITE_USE_MOCK_DATA=false`, `VITE_API_BASE_URL=http://127.0.0.1:8080` và `npm run dev -- --host 127.0.0.1`. Đăng nhập bằng tài khoản có quyền admin để mở mục **Gán nhãn coaching** ở `/coaching`.

Đợt thu thử local có tài khoản `player` cho Unity và `reviewer` cho admin trong `artifacts/functional-completion-20261106/coaching-pilot.json`. File này chứa mật khẩu riêng cho development và đã được loại khỏi Git. Khi cần tạo hai tài khoản trên một database local mới, chạy `./scripts/Invoke-FunctionalChecks.ps1 -Build -ProvisionCoachingPilot` từ thư mục dự án. Lệnh xác minh stack development, dùng quy trình xác thực email nội bộ và lưu thông tin đăng nhập vào file trên.

## Xem 30 phiên mô phỏng trong admin

Khi khởi tạo volume PostgreSQL mới với `docker-compose.functional-local.yml`, Compose bật `pickleverse.seed_coaching_demo=on`. `src/Database.sql` nạp 30 phiên Volley mô phỏng theo seed 42, với 10 phiên mỗi mức và 24 lượt mỗi phiên. Compose mặc định để bảng demo rỗng. Khởi động lại một volume đã có dữ liệu sẽ dùng schema hiện tại của volume đó.

Đăng nhập web admin bằng `reviewer`, rồi vào **Gán nhãn coaching → Phiên mô phỏng**. Chọn một phiên để xem trình độ giả lập, nhóm dữ liệu và các cú đánh. Màn này chỉ đọc bảng demo. Luồng gán nhãn, xuất dữ liệu chuyên gia và analytics đọc các bảng người chơi riêng.

## Thu thử một phiên

1. Trong Unity Editor, đăng nhập bằng `player`. Chọn **Đã thành thạo** để vào menu, rồi mở **Luyện tập → Giao bóng đúng ô**.
2. Hoàn thành cả tám lượt và chờ màn hình kết quả đồng bộ. Đây là phiên thử để đối chiếu dữ liệu trước khi thu theo nhóm.
3. Đăng nhập web admin bằng `reviewer`. Mở **Gán nhãn coaching**, chọn phiên vừa chơi và đối chiếu số cú đánh, mã nút, thời điểm, vị trí trước hỗ trợ và kết quả với phiên thực tế. Người duyệt xác nhận nhãn nào có đủ căn cứ. Khi chỉ mới thu thử một phiên, vẫn có thể lưu ghi chú hoặc nhãn lỗi đã xác minh.

## Thu phiên người thật

1. Bố trí người chơi và người đánh giá chuyên môn. Mỗi người chơi dùng một tài khoản thử riêng và được thông báo về việc dùng dữ liệu cú đánh cho đánh giá AI. Ghi mã người tham gia trong danh sách điều phối riêng.
2. Cho người chơi hoàn thành các lượt **Luyện tập** đầu tiên trong Unity Editor và chờ đồng bộ. Thu ít nhất 20 cú có dữ liệu quan sát được trong tối đa ba phiên Training đầu tiên. Có mặt ở phiên hoặc lưu đoạn ghi hình khi cần đối chiếu một tình huống cụ thể. Ghi phiên bản game, bài, danh mục và điều kiện hỗ trợ để tái lập ngữ cảnh.
3. Mở phiên trong admin và đối chiếu số thứ tự cú đánh, thời điểm bấm/thả, vị trí, đường bóng và cờ hỗ trợ với quan sát thực tế. Thử toàn bộ quy trình với một phiên trước khi tổ chức thu theo nhóm.
4. Phân bổ người chơi vào các nhóm **Huấn luyện**, **Hiệu chỉnh** hoặc **Holdout**. Dùng cùng nhóm cho mọi phiên của một người. Holdout cần ít nhất 10 người đủ dữ liệu ở mỗi mức trình độ, tổng cộng ít nhất 30 người, ngoài những người thuộc nhóm huấn luyện và hiệu chỉnh. Hai nhóm còn lại cũng cần có đủ ba mức.

## Gán nhãn và xuất dữ liệu

- Chuyên gia xác định **Mới chơi**, **Trung cấp** hoặc **Nâng cao** từ kỹ năng đã quan sát. Ghi rõ căn cứ trong bản đánh giá. Nếu bằng chứng thiếu, để trống trình độ và ghi nhận các nhãn kỹ thuật có căn cứ. Lựa chọn lúc onboarding là dữ liệu tự khai.
- Với mỗi cú được đánh giá, chọn mã lỗi, **Có lỗi** hoặc **Không có lỗi**, rồi ghi căn cứ. Bỏ qua cơ hội không quan sát được. Với `DinkTooHigh`, dùng cú Dink có số đo qua lưới và căn cứ từ quỹ đạo thực tế hoặc cú mẫu. Chú ý cờ tự ngắm khi đánh giá thao tác người chơi.
- Nhập tên chuyên gia, xác nhận phiên do người thật thực hiện và lưu. Hệ thống lưu tài khoản duyệt, thời điểm và phiên bản. Chuyên gia nên chốt nhãn trước khi xem dự đoán hoặc điểm của thuật toán.
- Xuất **dataset trình độ** và **dataset lỗi kỹ thuật**. Giữ file gốc cùng SHA-256 hiển thị sau khi tải. Xuất trước khi telemetry hết thời hạn 30 ngày. Lưu riêng khỏi Git và bảo toàn bản xuất trước khi reset development database. Bản xuất dùng mã người chơi đã được thay thế và vẫn chứa thông tin người đánh giá.

## Chạy đánh giá khi có dataset thật

Chạy từ `src/AiService` trong môi trường Python của dự án. Thay đường dẫn và giá trị phiên bản bằng dữ liệu ghi nhận. Ví dụ dưới đây chạy riêng đợt đánh giá `DinkTooHigh` với các cú Training có hỗ trợ tiếp xúc và tự ngắm.

```powershell
python train.py labels-tier.json models/tier-v1.joblib --version tier-v1 --mode training --assisted true
python calibrate_coaching.py labels-coaching.json models/coaching-candidate.json --version coaching-v1 --game-version 0.1.0 --assisted true --engine-aimed true --codes DinkTooHigh
```

- Model trình độ cần ít nhất 10 người holdout ở mỗi mức, macro-F1 ≥ 0,70 và cao hơn majority baseline ≥ 0,05. Khi dự đoán, chỉ trả mức trình độ nếu có ít nhất 20 cơ hội hợp lệ và độ tin cậy ≥ 0,65. Model dùng cho gợi ý luyện tập và bot.
- Coaching cần cả ca có lỗi và ca không lỗi cho mỗi mã lỗi được nghiệm thu. Dùng nhóm hiệu chỉnh để chọn ngưỡng và dùng nhóm holdout để đo precision ≥ 80% và recall ≥ 70%. Báo cáo riêng cho `DinkTooHigh` chỉ nghiệm thu mã lỗi đó. Cần thu và kiểm tra các mã lỗi còn lại trong ngữ cảnh có thể quan sát chúng.
- Chuyên gia xem ngưỡng, chỉ số và lời khuyên trong báo cáo. Khi duyệt, lưu file JSON với `expert`, `reviewerId`, `approvedCodes` và `reportSha256` đúng với báo cáo đã xem. Chạy lại đúng lệnh hiệu chỉnh, thêm `--review-file <đường-dẫn-file-duyệt>`. Giữ SHA-256 của artifact và báo cáo bên cạnh artifact đã duyệt.

Dữ liệu fixture chỉ dùng để kiểm tra phần mềm. Việc bật AI và triển khai Linux cuối cần dữ liệu người thật, đánh giá của chuyên gia và các kiểm tra chức năng local theo kế hoạch.
