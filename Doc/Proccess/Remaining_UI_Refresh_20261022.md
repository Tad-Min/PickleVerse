# Nâng cấp các bảng UI còn lại — 22/10/2026

## Phạm vi và thiết kế

Lấy Sound Room trong Menu làm mẫu: nền xanh đêm, mặt kính chuyển sắc, viền vát mảnh, vàng champagne cho điểm nhấn, xanh mint cho tiến trình và chữ trắng sáng. Hoa văn sân pickleball và hình thoi được dựng bằng mesh UI tĩnh, không bổ sung ảnh nền hay texture hoa văn.

| Giao diện | Thay đổi |
| --- | --- |
| Bạn bè | Drawer bên trái, tìm kiếm, thẻ danh tính, danh sách và thao tác thêm/xóa cùng hệ viền và khoảng cách. |
| BXH Elo | Tab cùng drawer, thứ hạng và Elo có thứ bậc rõ ràng, nút tìm vị trí cá nhân có tương phản trên nền vàng. |
| Thành tựu | Tiêu đề lớn, thanh tiến trình mảnh; số lượng tách khỏi thanh màu để dễ đọc. |
| Lịch sử | Thẻ kết quả, thời gian, Elo và thao tác replay giữ nguyên chức năng; dùng bố cục và kiểu chữ chung. |
| Tiến trình luyện tập | Gộp thời gian, lỗi thường gặp và bài ôn nhiều nhất thành một hàng tổng quan; giữ nút tiếp tục và từng bài tập. |
| Nhiệm vụ ngày/tuần | Tabs, đồng hồ reset, thưởng tổng và nút nhận thưởng rõ ràng; trạng thái đã nhận vẫn đọc được. |
| Hồ sơ | Sidebar danh tính và rank, avatar mặc định dùng vòng/initial vẽ trực tiếp, thống kê và XP đồng bộ với các bảng khác. |
| Cửa hàng / Bộ sưu tập | Mặt thẻ, nút, kiểu chữ và hoa văn đồng bộ; UI phủ màn hình ở độ phân giải gốc, chỉ render studio 3D khi xem vợt. |

Các bảng có hiệu ứng xuất hiện hữu hạn và hiệu ứng sáng khi tương tác; hoa văn không chạy animation liên tục. Bố cục tính theo safe area, chia cột và làm tròn tọa độ bố cục chính.

## Tối ưu

- Tái sử dụng hàng và các thành phần chữ/nút; xóa listener khi gán lại dữ liệu để không nhận thưởng hoặc thực hiện hành động nhiều lần.
- Giữ từng trang danh sách đã dựng và chỉ cập nhật lại khi dữ liệu thay đổi. Các trang ẩn được tắt GameObject; bảng ngoài cùng giữ nội dung và tắt Canvas/raycaster khi đóng.
- Thay stencil Mask của các danh sách hình chữ nhật bằng RectMask2D.
- Bảng phủ kín màn hình tạm dừng camera 3D phía sau và khôi phục trạng thái camera khi đóng. Drawer bạn bè/BXH vẫn cho thấy sảnh phía sau.
- Cửa hàng tắt camera và component quay preview khi đang xem danh mục. Đổi tab không bật lại studio bị che kín.
- UI dùng Screen Space Overlay để chữ không bị hạ độ phân giải theo chính sách render 3D trên điện thoại.
- Không bổ sung texture, font hoặc model mới cho đợt nâng cấp này. Ảnh kiểm tra nằm trong `UnityWork`, không đi vào nội dung build.

## Xác minh

Kết quả: **2.019 kiểm tra UI đạt, 0 lỗi; 443/443 kiểm thử EditMode đạt** (lượt xác minh cuối được lưu trong `editmode-results.json`). Editor trở lại scene Menu, ngoài Play, scene không có thay đổi chưa lưu. Snapshot hồ sơ gốc khớp từng file; không còn hồ sơ QA. Hai font động đã được khôi phục nguyên trạng Git để không đưa atlas/glyph sinh trong Editor vào thay đổi.

Dữ liệu chi tiết và ảnh nằm ở `UnityWork/RemainingUi20261022/`: `report.json`, `editmode-results.json` và các PNG theo tên trang/độ phân giải.

Máy đo: Unity 6000.6.2f1, Windows/DX12, Intel i5-11400H, RTX 3050 Laptop GPU, RAM khoảng 16 GB. Mỗi mẫu trạng thái ổn định đo khoảng 2 giây sau thời gian làm ấm; đây là mẫu ngắn trong Editor, không phải kiểm tra tải dài trên thiết bị.

| Trạng thái | Desktop, FPS trung bình | Mobile preview, FPS trung bình |
| --- | ---: | ---: |
| Menu, bảng đóng | 136,0 | 136,5 |
| Bảng tiến trình mở | 141,2 | 141,8 |
| Drawer BXH mở | 125,2 | 137,0 |
| Danh mục Shop | 141,8 | 144,0 |
| Danh mục Collection | 142,5 | 144,0 |

Trong 36 vòng mở/đóng sau khi đã mở các trang, thời gian gọi mở UI trung vị là **5,21 ms desktop / 4,82 ms mobile preview**; P95 là **10,19 / 6,44 ms**. Hai nhóm danh sách giữ nguyên số hàng đã tạo (68 hàng progression và 68 hàng social), không tăng qua các vòng thử. Mobile preview không phát sinh Gen0 GC trong mẫu lặp này.

**Giới hạn phép đo chuyển cảnh:** thời gian cả khung hình mở bảng P95 vẫn là **25,44 ms desktop / 19,80 ms mobile preview**, cao hơn ngân sách 16,67 ms của 60 FPS. Các đỉnh ngắn trong Editor vẫn còn; kết quả này không chứng minh tuyệt đối không giật khi chuyển bảng trên mọi máy. Cần đo Player trên A32 để phân biệt chi phí Editor, CPU/GPU thiết bị và tinh chỉnh tiếp nếu cần. Không dùng FPS trung bình ở bảng trên để che các đỉnh này.

Ma trận kiểm tra:

- Desktop: 1920×1080, 1280×720, 2560×1080, 1024×768.
- Mô phỏng điện thoại: 2400×1080, 1600×720, 915×412, 1024×768; Touchscreen và chính sách render mobile được bật, DPI preview 420.
- Kiểm tra vùng an toàn bất đối xứng, nút nhận raycast, chữ nút/tiêu đề, cuộn cuối danh sách, tìm kiếm có dấu, trạng thái trống, vị trí BXH và chuyển trang.
- Mở/đóng 36 lần mỗi cấu hình desktop/mobile, đổi trang và lọc danh sách lặp lại; kiểm tra số hàng không tăng sau khi đã mở các trang.
- Nhận thưởng bằng hồ sơ thử riêng, xác minh bấm lại không cộng thưởng lần hai.
- Shop/Collection: chuyển tab, danh mục, xem 3D, xoay, zoom, đặt lại góc, quay lại, trạng thái bật/tắt camera.
- Chờ shader Editor hoàn tất trước khi chụp vợt; cyan tạm thời là `FallbackLoading` của URP trong lúc biên dịch shader.

## Giới hạn và dữ liệu

Đây là phép đo trong Unity Editor trên Windows, bao gồm mô phỏng kích thước, input và cấu hình render điện thoại. Galaxy A32 chưa kết nối nên chưa đo FPS, nhiệt độ hoặc cảm giác chạm trên thiết bị thật. FPS Editor không đại diện cho FPS A32.

Nguồn dữ liệu và quy tắc thưởng/mua vật phẩm giữ theo hệ thống hiện có. Drawer bạn bè/BXH hiện dùng `OfflineSocialDirectory`; giao diện tiếp tục ghi rõ dữ liệu mẫu, chưa đồng bộ online. Đợt này không triển khai dịch vụ bạn bè/BXH mới.

Hồ sơ người chơi được sao lưu trước mỗi phiên thử, bài kiểm tra dùng ID riêng; sau khi dừng Play, khôi phục snapshot và xóa đúng hồ sơ thử đó. Không commit, push hoặc tạo bản APK trong tác vụ này.

## Tệp chính và cách chạy lại

- `Assets/Scripts/Settings/LobbyPanelStyle.cs`, `LobbyEtching.cs`: phong cách và hoa văn dùng chung.
- `UiRowPool.cs`, `UiListPages.cs`, `UiModalPresentation.cs`: vòng đời danh sách và bảng.
- `Assets/Scripts/FrontEnd/PlayerProgressionMenu*.cs`, `OfflineSocialMenu*.cs`: bố cục và nội dung các bảng Menu.
- `Assets/Scripts/Shop/ShopPanelPresentation.cs`, `ShopProductCard.cs`: cửa hàng và bộ sưu tập.
- `Tools/RemainingUiSetup.cs`: cập nhật ba scene bằng Unity Editor API; từ chối chạy nếu có scene chưa lưu.
- `Tools/RemainingUiValidation.cs`: `Prepare` ngoài Play → vào Play → `Start` → chờ `report.json` báo passed/failed và trở lại Menu → dừng Play → `RestoreProfiles`.

Không chạy `Prepare` lần nữa trước khi đã phục hồi dữ liệu của phiên thử trước. Công cụ lưu lại Game View và cấu hình đồ họa sau phiên thử. Các công cụ trong `Tools` nằm ngoài `Assets`, không được biên dịch vào game.
