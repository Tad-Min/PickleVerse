# Tiến trình luyện tập và quay về đại sảnh

Kiểm tra ngày 12/10/2026 trên Unity 6000.6.2f1, Game view 1280×720, cùng hồ sơ và thiết lập đồ họa hiện tại.

## Thay đổi

- `BtnProgression` ở nav dưới được bật, tăng độ tương phản và nối tới `OpenTrainingProgress`. Bảng hiển thị sao, độ chính xác, lỗi, số lần làm lại và nút tiếp tục bài đang dở. Nút Nhiệm vụ mở nhiệm vụ ngày/tuần riêng.
- Giữ Menu tạm ngưng qua luồng Menu → ModeSelection → Training → ModeSelection → Menu. Khi đổi trang, chỉ unload trang đi khỏi; không khởi tạo lại sân, hai nhân vật và UI đại sảnh. Camera, đèn, input, render và mô phỏng của Menu đều tắt khi đang luyện tập.
- Khi rời bài, checkpoint được lưu trước khi điều hướng; tránh lần ghi trùng từ `OnDestroy`.
- `LobbyRenderBudget` điều chỉnh render scale **chỉ cho 3D ở Menu** trong khoảng 65–100% (không vượt cấu hình người dùng), khởi đầu ở 75% và điều chỉnh theo thời gian khung hình. Nav, bảng tiến trình và UI nhạc dùng Overlay ở độ phân giải màn hình. Giữ bốn spotlight và bóng đổ; không giảm độ phân giải texture hay sửa model.
- Khi Menu bị tạm ngưng, render scale trở về giá trị người dùng. Không ghi thay đổi vào tùy chọn đồ họa; kiểm tra Training khôi phục mức 125% của cấu hình đang dùng.

## Số đo

Trước sửa, quay về Menu phải tạo lại đại sảnh: khung hình chậm nhất của ba lượt là **447–557 ms**. Bản giữ đại sảnh đã loại bỏ đoạn tải lại này. Cảnh đêm vẫn chậm ở render scale 125%, nên bổ sung điều chỉnh riêng cho 3D đại sảnh.

Số đo cuối sau khi quay về Menu (mỗi mẫu 3 giây, trên máy hiện tại):

| Bối cảnh | FPS trung bình | P95 thời gian khung hình | Khung chậm nhất | Số khung dưới 60 FPS |
|---|---:|---:|---:|---:|
| Ban ngày | 98,25 | 11,60 ms | 11,83 ms | 0 / 295 |
| Ban đêm | 66,59 | 15,52 ms | 15,69 ms | 0 / 200 |

Đây là kết quả đo trong Editor trên máy hiện tại, không phải cam kết mọi cấu hình phần cứng hoặc mọi tình huống đều đạt trên 60 FPS.

## Kiểm tra

- 230 EditMode tests đạt.
- 29 kiểm tra luồng đạt: click/raycast nút nav, đọc sao/lỗi từ hồ sơ QA, ba vòng qua Training, cùng instance Menu khi trở về, chỉ một camera/input/listener đang bật, không còn controller Training, checkpoint và tiếp tục bài, khôi phục render scale khi vào luyện tập.
- Test dùng hồ sơ QA riêng và khôi phục hồ sơ người chơi; không thay tiền, phần thưởng hay tùy chọn đồ họa của hồ sơ thật.
- Báo cáo và ảnh: `UnityWork/TrainingReturn/`. Công cụ: `Tools/TrainingReturnBenchmark.cs`, `TrainingReturnValidation.cs`, `TrainingReturnSetup.cs`.
