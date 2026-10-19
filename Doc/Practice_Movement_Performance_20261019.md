# Training / Tutorial: di chuyển và hiệu năng

Ngày kiểm tra: 19/10/2026. Unity 6000.6.2f1.

## Các lỗi đã sửa

- Training trước đây chặn vị trí sau khi actor đã tính vận tốc. Giữ phím vào giới hạn sân vẫn làm animation chạy. Giới hạn nay được áp dụng ngay trong `MatchMovement.Step`, trước khi tính vận tốc thực tế.
- Animation chân dựa vào vận tốc thực thay vì phím đang giữ. Dừng nhận điều khiển sẽ trở về idle; phản ứng lỗi không còn giữ pose vô hạn. Các đoạn chuyển pose vẫn dùng bộ trộn hiện có, và di chuyển không hủy cú đánh.
- Training/Tutorial dùng giới hạn riêng: ngang ±3,1 m, cuối sân ±8,1 m và cách lưới 0,85 m. BotMatch/online giữ giới hạn và xử lý lỗi lưới trước đó.
- Tutorial chỉ ghi nhận chạy khi nhân vật thật sự di chuyển. Giữ chạy vào lưới không được tính hoàn thành. Khi tới giới hạn có hướng dẫn đổi hướng/thả phím.
- Hai đoạn di chuyển tự động trong tutorial giảm tốc gần điểm đến và dừng sau khi diễn giải xong.
- Đồng đội trong hai bài đánh đôi đổi sang khoảng trống khi mép sân không đủ chỗ, có ngưỡng giữ bên để tránh liên tục đổi hướng.
- Mô phỏng và pose chia bước tối đa 1/60 giây khi khung hình chậm; camera, HUD và đường hướng dẫn cập nhật một lần mỗi khung hình.

## Hiệu năng

Training/Tutorial dùng bộ điều chỉnh độ phân giải 3D theo thời gian khung hình, ưu tiên tối đa 120 FPS theo giới hạn người chơi đã chọn. Mức thấp nhất là 50% ở màn hình 1080p, thấp hơn theo tỉ lệ trên màn hình lớn. Canvas HUD vẫn giữ độ phân giải hiển thị. Không đổi thiết lập đồ họa đã lưu, mesh sân hoặc bố trí đèn.

Đo trong Play Mode, Game View 1920×1080, Core i5-11400H, RTX 3050 Laptop và RAM 16 GB. Lượt đo chi tiết có Game View được chọn và ứng dụng có focus trong mọi mẫu:

| Trường hợp | FPS trung bình | P95 thời gian khung hình |
| --- | ---: | ---: |
| Training di chuyển, ban ngày | 142,9 | 7,28 ms |
| Training di chuyển, ban đêm | 127,9 | 8,77 ms |
| Tutorial di chuyển, ban ngày | 141,2 | 7,12 ms |
| Tutorial di chuyển, ban đêm | 144,0 | 7,09 ms |

Khảo sát riêng toàn bộ 12 bài Training và 18 bước Tutorial (gồm trang hoàn thành): FPS trung bình từng bài từ 116,5 đến 145,1. P95 lớn nhất là 16,38 ms. Mỗi bài đo 1,8 giây sau 1,2 giây chờ; tất cả mẫu đều có focus. Một số khung hình riêng lẻ vẫn vượt 16,67 ms trong Editor, vì vậy không kết luận mọi khung hình đều trên 60 FPS.

Đây là số đo Unity Editor trên máy kiểm tra, không phải cam kết FPS trên mọi thiết bị. Lượt khảo sát ban đầu có khung hình chậm hơn; lượt chẩn đoán sau bổ sung trạng thái focus và bộ đếm thời gian CPU để kiểm tra điều kiện đo. `Application.targetFrameRate` đã lưu là 120; số FPS Game View có thể vượt mức này trong Editor.

## Kiểm thử

- 9/9 unit test: giới hạn lưới hai phía, trượt dọc biên, đổi hướng, nhả phím và giữ giới hạn cũ của các chế độ đấu.
- 170/170 kiểm tra runtime: 12 bài Training, bốn hướng di chuyển, dừng/đổi hướng/nhả phím, khoảng cách đồng đội, tất cả demo Tutorial và 9 tổ hợp đánh bóng.
- 12/12 bài Training chạy tới kết quả 3 sao, gồm hồi vị và phối hợp đánh đôi.
- 32/32 kiểm tra luồng Tutorial: camera, luật, di chuyển, 9 cú đánh và lưu hoàn thành.
- 396/396 trường hợp đánh bóng nam/nữ, đứng/chạy, 30/60/120 Hz; 12 kỹ thuật BotMatch vẫn chạm bóng đúng.
- Console sau kiểm tra: 0 lỗi, không có lỗi biên dịch. Đã thoát Play Mode khi kết thúc.

Các bài kiểm tra dùng hồ sơ QA riêng và khôi phục hồ sơ ban đầu. Tiến trình tutorial dùng bộ nhớ tạm trong kiểm thử. Không cấp thưởng hoặc mở khóa vào hồ sơ thật.

## Công cụ và dữ liệu

- `My project/Tools/PracticeMovementValidation.cs`: kiểm tra di chuyển và các bài hướng dẫn.
- `My project/Tools/PracticePerformance.cs`: khảo sát từng bài và so sánh hiệu năng.
- `My project/Tools/PracticeFrameDiagnosis.cs`: đối chiếu khung hình, focus và các marker CPU.
- `UnityWork/PracticeSmooth20261019/`: báo cáo JSON, ảnh chụp trạng thái ban đầu và kết quả đo. Thư mục này nằm ngoài Assets, không đi vào bản game.
