# Bạn bè và BXH offline

Menu → WelcomeCard có **Bạn bè** và **BXH Elo**. Cửa sổ dùng uGUI/TMP và font tiếng Việt của Menu; chặn thao tác phía sau, đóng bằng nút × hoặc Escape, theo safe area.

- 30 người mẫu cố định, ID `DEMO-001` đến `DEMO-030`, tên và Elo khai báo trong `Assets/Scripts/Social/OfflineSocialDirectory.cs`.
- Mỗi player có 6 bạn mẫu ban đầu. **Tìm bạn** hiển thị danh bạ mẫu; tìm theo tên hoặc ID, hỗ trợ tiếng Việt không dấu và không phân biệt hoa/thường. Bấm **Thêm bạn**, về **Bạn của tôi** để **Xóa bạn**. Chặn thêm trùng.
- Danh sách được giữ trong bộ nhớ theo ID player trong phiên chạy, gồm cả khi chuyển Scene và quay lại Menu. Khi mở game hoặc vào Play lần mới, danh sách trở về mẫu. Không ghi bạn bè vào JSON tiến trình.
- BXH gồm 30 người mẫu **và** player hiện tại (31 mục), Elo giảm dần. Bằng Elo thì ID theo thứ tự ordinal để vị trí ổn định; vị trí không đổi khi lọc tìm kiếm. **Vị trí của tôi** xóa bộ lọc và cuộn đến dòng player.
- Tên, Elo, rank và thứ hạng của player cập nhật từ `ProgressionRuntime`, bao gồm khi chỉnh player cục bộ hoặc hoàn tất trận. Không sao chép các giá trị mẫu vào hồ sơ, XP hoặc ví.
- Đây là dữ liệu demo offline; không gửi lời mời, tìm kiếm hoặc đồng bộ lên backend. Không giả lập trạng thái online.

Assembly `PickleVerse.Social` phụ thuộc `PickleVerse.Progression`; UI ở `OfflineSocialMenu` thuộc FrontEnd. Tạo/cập nhật cấu trúc bằng **PickleVerse → Social → Configure Offline Menu** khi Scene Menu đang mở và Editor đã dừng Play. Công cụ dùng API Unity và không tạo lại cửa sổ đã có.

Kiểm thử logic ở `Assets/Tests/Social`; kiểm tra tương tác và bố cục bằng `Tools/OfflineSocialValidation.cs` trong Play Menu. Báo cáo được lưu trong `Docs/Social`.

Nghiệm thu 12/10/2026: **117 kiểm tra đạt** (11 logic, 35 tương tác, 48 bố cục ở bốn độ phân giải, 23 hồi quy Menu). Build Windows Development thành công, 0 lỗi; còn 1 warning sẵn có của Pipeline về `RuntimePipelineConfig`. Chi tiết trong `Validation.json`, `LogicTests.json`, `InteractionValidation.json` và `LayoutValidation.json`; ảnh ở `Menu.png`, `Friends.png`, `Leaderboard.png`.
