# Cải tổ Menu bằng tài nguyên Blender

Cập nhật: 16/10/2026. Scene: `Assets/Scenes/UI/Menu.unity`.

## Kết quả

- Ẩn nền header kéo dài, tên game và tagline. Giữ thẻ avatar/tên/cấp/rank/Elo bên trái cùng hai ô tiền tệ bên phải.
- Bỏ hộp nội dung lớn bên trái. Nút Chơi đứng riêng, có vợt/banh, động tác lấy đà, đánh bóng, chớp tiếp xúc và đường bay ngắn trước khi mở chọn chế độ.
- Chuyển Nhiệm vụ vào dock dưới. Giữ các luồng Tiến trình, Lịch sử, Bộ sưu tập, Cửa hàng, Cài đặt đang hoạt động.
- Bạn bè/BXH mở bằng tay nắm có mũi tên ở mép trái. Ngăn trượt trong 240 ms, chuyển tab, lọc tên/ID, tìm vị trí cá nhân và đóng bằng nút X, Esc hoặc bấm ngoài.
- Ngăn xã hội chiếm quyền điều khiển khi mở; nhạc mini không chồng lên nó. Đóng ngăn khôi phục điều khiển menu.
- Nút có phản hồi hover/chọn/nhấn, icon chuyển động nhẹ và vệt sáng ngắn. Vùng bấm giữ nguyên khi hình ảnh được animate.
- Trên màn hình dọc, camera menu hướng về nhân vật được chọn để tránh cắt mất nhân vật ở cạnh màn hình.

## Quyết định UI/UX

Mục tiêu là giữ sân và nhân vật làm trọng tâm, chỉ có một hành động chính nổi bật, chức năng xã hội mở theo nhu cầu. Viền vát champagne, enamel xanh đậm, điểm nhấn ngọc và icon nổi tạo phong cách anime/thể thao mà không thêm trang trí chuyển động liên tục.

[Epic — Fortnite sidebar](https://www.fortnite.com/news/keep-the-text-chat-going-in-fortnite-and-beyond) là nguồn tham khảo cho cách gom chức năng xã hội vào sidebar. [Riot — Client Cleanup](https://www.leagueoflegends.com/en-us/news/dev/client-cleanup-next-stop-champ-select/) nhấn mạnh khả năng phản hồi của nút; menu này phản hồi ngay khi nhấn, sau đó chạy động tác Chơi dài 0,4 giây. Các lựa chọn màu sắc/viền/bố cục là thiết kế riêng cho PickleVerse, không dùng asset trích từ game tham khảo.

## Blender và dung lượng

Nguồn chỉnh sửa: `My project/Tools/Blender/Lobby_UI_Source.blend`.

Recipe ban đầu: `My project/Tools/Blender/build_lobby_ui.py`, đã chạy với Blender 5.1.1. Nguồn `.blend` hiện đã có vợt PickleVerse từ `Paddle.png` và bóng gốc `pickleball_ball_cartoon.glb`. Dùng `bake_lobby_equipment.py` để bake lại đúng hai ô vợt/bóng từ file nguồn hiện tại; không chạy recipe ban đầu để thay thế file nguồn đã chỉnh sửa. File JSON giữ keyframe để Unity chạy bằng RectTransform.

| Tài nguyên | Cấu hình |
|---|---|
| Sprite sheet | 1024×1024 RGBA, 16 sprite, PNG 415.771 byte (khoảng 406 KiB) |
| Desktop | BC7, không mipmap, không Read/Write |
| Android/iOS | ASTC 6×6, giới hạn 1024 |
| Panel | Border 9-slice để co giãn mà giữ góc |
| Animation | Curve + transform/alpha; không video, không camera 3D riêng cho từng nút |
| Font | Chữ dùng TMP; không bake chữ vào PNG |
| Nguồn Blender | Để ngoài Assets, không tự nhập vào bản build |

Dùng Sprite Editor Data Provider của Unity để chia sprite và giữ file ID ổn định khi nhập lại. Package `com.unity.2d.sprite` được thêm qua UPM cho công việc biên tập này.

## Thích ứng màn hình

- Dock desktop mặc định khoảng 784×62 đơn vị Canvas tại 1280×720; bỏ nền footer kéo dài.
- Canvas dựa theo chiều cao khi ngang, chiều rộng khi dọc.
- Điện thoại tăng vùng bấm và cỡ chữ theo DPI; DPI không hợp lệ dùng cạnh ngắn làm fallback.
- Dock đổi thành hai hàng trong màn hình dọc. Hồ sơ/tiền tệ xếp lại để không đè nhau.
- Dùng safe area hiện có. Điện thoại và tablet có vùng bấm chính tối thiểu 44 dp trong các trường hợp kiểm thử.
- Trường mô phỏng DPI chỉ có trong Editor, không có trong bản build.

## Xác minh

- EditMode: **344/344**.
- Luồng bấm thực qua EventSystem/raycast: **32/32**; hồ sơ, tiến trình, nhiệm vụ ngày/tuần, lịch sử, bạn bè, BXH, tìm kiếm, vị trí cá nhân, cửa hàng, bộ sưu tập, cài đặt, animation Chơi và quay lại menu.
- Layout: **78/78** tại 1280×720, 1920×1080, 2560×1080, 1024×768, 1334×750, 2400×1080, 2532×1170, 2048×1536, 1080×1920; có mô phỏng touch/DPI 264–460 và vùng khuyết.
- Kiểm tra giới hạn màn hình, va chạm bố cục, chữ dock, vùng cuộn và vùng bấm.
- Benchmark riêng phần cập nhật UI mới: 10.000 vòng trên 12 component, không cấp phát managed trong vòng đo; không thêm RawImage/RenderTexture cho nút. Đây không phải phép đo toàn bộ render hoặc FPS trận đấu.
- Dùng profile QA riêng và khôi phục profile ban đầu sau kiểm thử.

Kết quả/ảnh native: `UnityWork/LobbyRefresh20261016`. Script: `Tools/LobbyUiValidation.cs`. Author lại scene/tài nguyên: `Tools/RefreshLobbyUi.cs`.

Bạn bè/BXH giữ nguồn dữ liệu cục bộ/demo đang có, chưa biến thành dịch vụ bạn bè hoặc xếp hạng online. Các kích thước trên được mô phỏng trong Editor; chưa xác minh trên điện thoại thật. FPS còn phụ thuộc chi phí sân/nhân vật, GPU và thiết lập đồ họa.

## Cập nhật vợt Chơi ngay

- Thay trực tiếp toàn bộ hai ô Paddle/Ball trong atlas; 14 ô còn lại giữ nguyên từng pixel. Không giữ bản atlas cũ trong Assets, không thêm model/camera/RenderTexture chạy riêng cho nút.
- Vợt dùng sprite bake từ model mới trong `.blend`, quad tăng từ 1,08 lên 1,95 lần chiều cao nút. Phần vợt nhìn thấy nhô ra phía trên và dưới, không che tiêu đề; vùng bấm không đổi.
- Căn lại bóng và flash theo điểm chạm của vợt. Giữ phản hồi hover, động tác 0,4 giây và điều hướng sang chọn chế độ.
- Cache bố cục kiểm tra cả hướng màn hình, tránh giữ dock dọc khi quay lại màn hình ngang.
- Kiểm tra bổ sung: 83/83 trên 9 kích thước/DPI mô phỏng; 6/6 luồng raycast, chuyển động, flash, chọn chế độ và quay lại menu. Texture runtime giữ 2.098.080 byte như trước, PNG giảm 254.629 byte (38%).
- Kết quả: `UnityWork/LobbyPlayEquipment20261016`; authoring: `Tools/UpdateLobbyEquipment.cs`; kiểm tra: `Tools/LobbyEquipmentValidation.cs`.

## Dock Menu: Chơi ngay bên trái, nav bên phải

- Xóa GameObject `BtnHome`. Sáu chức năng còn lại giữ callback đang có; nav neo góc dưới bên phải, lề 24 đơn vị trên màn ngang và 28 trên màn dọc.
- Chơi ngay neo dưới bên trái; tâm dọc trùng tâm cụm nav. Màn ngang dùng một hàng sáu nút; màn dọc dùng ba cột, hai hàng bên phải.
- Vợt nằm phía trên nút, đầu cán chạm phần viền vát trên. Bóng và flash được căn theo điểm chạm ở 0,15 giây. Hover dùng viền/vệt sáng, không phóng vợt làm cán rời viền.
- Ô nhạc đặt phía trên hình vợt để tránh chồng lên nhau; module âm thanh phát sự kiện bố cục Menu, không tham chiếu ngược tới assembly FrontEnd.
- Kiểm tra native bổ sung tại `UnityWork/LobbyDock20261016`: 128/128 kiểm tra bố cục trên 9 kích thước/DPI mô phỏng và 6/6 kiểm tra bấm Chơi ngay, animation, flash, chọn chế độ, quay lại Menu. Atlas giữ nguyên, không thêm tài nguyên đồ họa.

## Tư thế vợt theo ảnh tham chiếu

Vợt được hạ xuống và dịch sang trái: đầu vợt nhô khỏi góc trái trên, cán nằm trong nút và kết thúc gần đường viền xanh phía dưới. Bóng giữ ở phía trên đầu vợt. Khi nhấn Chơi ngay, bóng hạ tới điểm tiếp xúc trong nhịp lấy đà rồi bay theo curve cũ; flash nằm đúng điểm chạm. Trên màn hình cảm ứng chừa thêm khoảng chữ để cán không che tiêu đề. Ô nhạc tránh cả đầu vợt và bóng. Không thay đổi atlas hoặc thêm tài nguyên. Kết quả native: `UnityWork/LobbyPaddlePose20261016`.

