# Galaxy A32: hiệu năng và điều khiển cảm ứng

## Thay đổi

- Cần trái chỉ điều khiển tốc độ đi thường, không tự chuyển sang chạy ở mép. Giữ nút **CHẠY** riêng bên phải để chạy nhanh. Thả nút chạy vẫn giữ hướng di chuyển; các ngón điều khiển chạy, di chuyển và cú đánh độc lập.
- Bố trí Drive thấp bên trái cụm kỹ thuật, Dink ở trên, Volley cao bên phải và Chạy ở dưới bên phải. Cụm nút được phản chiếu cho chế độ thuận tay trái. Cấu hình cũ được nâng lên phiên bản 3, giữ độ lệch tùy chỉnh của bốn nút hiện có.
- Nhãn SMASH/DROP/RESET, CHÉO, LỰC và HỦY có nền tương phản; hướng chọn dùng nền vàng và chữ tối. Toàn bộ nhãn được giới hạn trong màn hình, kể cả thao tác kéo xuống. Các nhãn không chặn raycast.
- Music Player phủ kín màn hình; nội dung bên trong tuân theo vùng tránh camera và thanh điều hướng. Giữ nguyên thứ tự lớp của playlist. Camera 3D được tạm ngưng khi bảng nhạc phủ kín màn hình và khôi phục khi đóng. Canvas riêng cho đĩa xoay tránh làm lại toàn bộ playlist mỗi khung hình.
- HUD mobile dùng Screen Space Overlay để giữ nét chữ trong khi giảm độ phân giải phần sân.

## Ngân sách đồ họa mobile

Phần 3D giới hạn trong 1280×720 pixel (giữ đúng tỷ lệ màn hình), tiếp tục giảm có kiểm soát khi không đạt ngân sách khung hình. Ví dụ màn hình 2400×1080 dựng sân tối đa 1280×576, còn HUD giữ 2400×1080. Giới hạn mobile là 60 FPS hoặc 30 nếu người chơi đã chọn 30. Không dùng mục tiêu 120 FPS của bài luyện tập trên điện thoại.

Mobile dùng MSAA 1, bóng chính tối đa 1024, một cascade, khoảng cách bóng tối đa 16 m; bỏ soft shadow và shadow của đèn phụ. Các spotlight trắng và cường độ chiếu sân vẫn hoạt động. Chỉ sửa bản sao URP trong runtime.

Blender tạo LOD cho 20 mesh trang trí của sân Stadium Enhanced: **754.096 → 165.890 tam giác**. Không đổi kích thước/transform, collider, lưới, vùng kitchen hay vạch sân. PC dùng LOD gốc; mobile chọn LOD nhẹ. Mesh LOD lưu nhị phân, chỉ số 16 bit khi đủ và nén Medium: khoảng **2,59 MiB** cho toàn bộ thư mục nguồn, không thêm texture.

Tạo lại LOD bằng `Tools/StadiumMobileLOD.cs` (chạy entry `Export` trong Editor), `Tools/Blender/BuildStadiumMobileLOD.py` (Blender nền), sau đó entry `Import`. Dữ liệu trung gian nằm ngoài Assets tại `UnityWork/MobileStadiumLOD`. Dữ liệu dùng trực tiếp hệ tọa độ mesh Unity, không chuyển trục giữa hai ứng dụng. Tool giữ mesh gốc và thêm LOD thay thế.

## Kiểm chứng

- **443/443 EditMode tests**: giao bóng, các kỹ thuật, cảm ứng nhiều ngón, hủy/thả, settings migration và các hệ thống liên quan.
- **516 kiểm tra UI/render** ở BotMatch, Training, Tutorial, MultiplayerCourt. Music kiểm tra 2400×1080, 1600×720, 915×412, 1024×768 và vùng an toàn lệch hai bên. Cảm ứng kiểm tra ba tỷ lệ landscape, năm mục tiêu raycast, sáu hướng combo, nhãn không tràn màn hình, chạy độc lập.
- Scene multiplayer được dựng offline để kiểm tra render/HUD; không giả định đã kiểm tra đường truyền hay trận online hai thiết bị.
- Hồ sơ QA tạm, trạng thái settings/LOD và lựa chọn Game View được khôi phục sau đo. Ảnh/báo cáo chi tiết nằm trong `UnityWork/GalaxyA32QA` (Git bỏ qua).

So sánh lúc tối ở Game View 2400×1080, cùng camera; đây là số liệu **Unity Editor trên laptop**, không phải Galaxy A32:

| Scene | Draw calls trước/sau | Tam giác trước/sau |
| --- | ---: | ---: |
| BotMatch | 161 / 107 | 316.631 / 176.728 |
| Training | 219 / 130 | 2.773.631 / 515.531 |
| Tutorial | 225 / 133 | 2.828.657 / 549.873 |
| MultiplayerCourt | 141 / 94 | 74.073 / 50.365 |

Chưa có Galaxy A32 kết nối ADB, nên chưa xác nhận FPS, nhiệt độ máy hoặc độ trễ cảm ứng thực tế trên thiết bị. APK mới cần được cài cập nhật trên A32 để đo lại; các số FPS Editor không được dùng làm cam kết FPS điện thoại.

APK thử nghiệm `0.1.1-mobile-test`, version code `20261021`, khoảng 195 MB, ARM64/API 26+. Đã kiểm tra CRC toàn bộ ZIP, chữ ký APK và chứng chỉ trùng bản trước; package vẫn là `com.pickleverse.personaltest` để cài cập nhật giữ dữ liệu. Toàn bộ 443 test đã chạy lại thành công trên chính workspace dùng build cuối. APK, log và workspace build nằm ngoài Git; cache tạm được dọn sau xác minh.

Tham khảo: [Unity: tối ưu cấu hình URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/configure-for-better-performance.html), [Unity: tối ưu bóng đổ URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shadows-optimization.html).
