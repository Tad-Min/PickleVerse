# Màn chuyển cảnh Loading

## Các luồng

- ModeSelection → BotMatch.
- ModeSelection → Training (và nút tiếp tục luyện tập từ đại sảnh).
- ModeSelection → Tutorial.
- PlayMultiplayer → MultiplayerCourt, thông qua trình quản lý scene của NGO.

Scene: `My project/Assets/Scenes/UI/Loading.unity`. UI dùng chung: `Assets/Resources/Loading/LoadingScreen.prefab`.

Loading giữ ảnh toàn màn hình, tự crop theo tỉ lệ màn hình, dùng safe area cho chữ và thanh tiến độ. Màu navy/vàng, viền vát và font theo UI âm nhạc hiện có. Ba ảnh thay nhau qua các lần tải; nếu tải lâu sẽ chuyển ảnh mềm. Đầu thanh là quả bóng xoay từ 16 ảnh 128×128 được render từ `pickleball_ball_cartoon.glb`, không cần camera 3D phụ.

## Tiến độ và sẵn sàng

Tiến độ không chạy tới 100% bằng một bộ đếm thời gian cố định:

1. 0–80%: tiến độ tải của thao tác chuyển scene.
2. 82–96%: camera của scene đích thực sự render dưới lớp loading; hệ thống đo cửa sổ 36 khung hình, tối thiểu 0,8 giây. Các khung hình đứng do kích hoạt scene/shader phải rời cửa sổ trước khi được xem là sẵn sàng.
3. Với online, chờ NGO hoàn tất đồng bộ scene và tất cả người chơi báo render đã sẵn sàng. Trong lúc chờ giữ dưới 100%.
4. Sau khi mọi máy đã chuẩn bị, chạy 100% và fade UI. Mỗi máy xác nhận đã bỏ lớp loading rồi server mới bắt đầu đồng hồ cinematic chung.

Mục tiêu ban đầu là nhịp 60 FPS, hoặc 30 FPS nếu người chơi chủ động chọn giới hạn đó. Máy chậm hơn có thể hoàn tất sau khi nhịp render đã ổn định; không dùng số giây chờ để tự kết luận tải xong. Input gameplay và các phím tắt được khóa trong lúc loading.

Training giữ cơ chế cache đại sảnh để tránh tải lại sân menu. Không thay việc tải scene online bằng `SceneManager` cục bộ. Unity phân biệt trạng thái tải Unity scene và hoàn tất đồng bộ NGO; vì vậy bước online theo các sự kiện hoàn tất scene của NGO. Tham khảo [Unity: scene events](https://docs-multiplayer.unity3d.com/netcode/2.3.2/basics/scenemanagement/using-networkscenemanager/).

Giao thức multiplayer nâng lên 12 vì bổ sung trạng thái chuẩn bị và xác nhận bỏ loading. Các client cùng trận cần cùng phiên bản giao thức.

## Ảnh và dung lượng

Ảnh dựng trong Blender từ ProfessionalStadium, model nam/nữ, vợt mặc định và quả bóng của dự án. Script nguồn: `My project/Tools/Blender/render_loading_art.py`. Không sửa/xóa các model nguồn.

Dùng AgX, ánh sáng tương phản ấm/lạnh, độ sâu trường ảnh cho ảnh cận bóng. AgX được dùng để giữ chuyển vùng sáng mềm theo hướng dẫn [Blender: color management](https://docs.blender.org/manual/en/5.0/render/color_management/displays_views.html).

Ba ảnh JPEG 2048×1152 và 16 ảnh xoay của bóng có tổng dung lượng nguồn khoảng 1,24 MiB. Texture không đọc lại từ CPU, không có mipmap; BC7/DXT5 trên desktop và ASTC 6×6 trên mobile. Màn loading chỉ chạy Canvas, không phát video hoặc render một sân phụ.

## Kiểm tra

- 7 unit test về cửa sổ khung hình: tải chưa xong, spike kích hoạt, 30 FPS, máy chậm ổn định và nhịp giật thất thường.
- 16 kiểm tra runtime luồng offline: UI 0–100, chuyển đúng scene, dọn canvas, giữ đầu cinematic, cache đại sảnh và hủy lớp loading.
- Online 1v1 và 2v2: host/client độc lập qua NGO loopback đều qua kiểm tra tải scene, chờ client chậm thêm 3 giây, hiển thị 100%, bỏ lớp loading rồi mới bắt đầu cinematic. Lượt 2v2 có hai người chơi và hai bot, đủ bốn nhân vật; chưa phải phép thử bốn máy thật qua Internet.
- Giao diện qua kiểm tra ở 1280×720, 1920×1080, 2400×1080 và 1024×768: ảnh phủ màn hình, thanh tiến độ/quả bóng/chữ nằm trong màn hình. Ảnh chụp thực tế lưu cùng báo cáo.
- Công cụ: `Tools/LoadingFlowValidation.cs`, `Tools/LoadingNetworkBuild.cs`; dữ liệu trong `UnityWork/Loading20261019/`.
- Bootstrap test network chỉ biên dịch khi bật `PKV_LOADING_QA`, dùng hai process trên loopback và identity giả, không tạo tài khoản hoặc phiên cloud.

Bản Windows QA đã build thành công và chạy kiểm tra mạng bằng Direct3D 12. Trong quá trình build, Unity vẫn ghi 7 thông báo biên dịch shader Direct3D 11 hết bộ nhớ; không coi đây là build sạch mọi shader/backend. Kiểm thử này xác nhận luồng loading và đồng bộ, không cam kết một mức FPS cho mọi phần cứng. Tài nguyên và cấu hình build thử đã được dọn, Editor trở về scene Menu.
