# Bộ điều khiển cảm ứng PickleVerse

Ngày kiểm tra: 20/10/2026. Unity 6000.6.2f1, Windows DX12.

## Chức năng

BotMatch, MultiplayerCourt, Training và Tutorial dùng chung `MobileTouchPad`. Điện thoại tự bật cảm ứng; trên desktop vẫn có thể bật nút Cảm ứng để thử.

| Thao tác | Kết quả |
| --- | --- |
| Giữ rồi thả Drive / Dink / Volley | Cú đánh cơ bản |
| Drive kéo lên | Smash |
| Dink kéo lên | Drop |
| Volley kéo lên | Reset |
| Kéo trái | Thêm Chéo vào kỹ thuật cơ bản |
| Kéo phải | Thêm Lực vào kỹ thuật cơ bản |
| Kéo xuống | Hủy, không gửi cú đánh |
| Kéo trở về giữa | Trở về kỹ thuật cơ bản |
| Joystick trái; kéo xa | Di chuyển; chạy |

Giao bóng chỉ dùng Drive, Drive + Lực hoặc Drive + Chéo. Kéo lên khi đang giao vẫn giữ giao bóng. Dink + Lực giữ nguyên cơ chế tăng tốc sau nảy của game, được gameplay xử lý như Drive + Lực.

Hai nút Lực/Chéo riêng và ba nút combo cũ được ẩn. Một ngón điều khiển joystick và một ngón chọn kỹ thuật hoạt động độc lập. Ngón thứ ba không chiếm quyền của cú đánh đang chọn.

## Quyết định UX

Tham khảo cách giữ nút rồi kéo để chọn biến thể trong [hướng dẫn FIFA Mobile của EA](https://www.ea.com/ea-originals/news/gameplay-guide-button-controls) và [điều khiển mobile của eFootball](https://www.konami.com/efootball/en-us/page/new_controls). Áp dụng nguyên tắc ít nút, hướng kéo nhất quán; hình nút dùng tài nguyên demo của dự án.

- Joystick mờ hơn cụm đánh, đặt sát vùng ngón cái bên trái.
- Drive lớn hơn Dink/Volley; cụm kỹ thuật chừa toàn bộ sóng và chữ Hủy bên dưới.
- Sóng trắng và tên hướng xuất hiện ngay khi giữ/kéo. Các nút còn lại mờ đi. Gợi ý combo dưới nút đang giữ được ẩn để không chồng chữ Hủy.
- Layout tính theo vùng an toàn, tỷ lệ màn hình và cỡ nút; hỗ trợ đổi tay trái, độ mờ và vị trí tùy chỉnh. Bố trí tùy chỉnh bị chồng nút được đưa về bố trí an toàn.
- Training/Tutorial đưa hướng dẫn lên trên; gợi ý thao tác ở phía trên joystick để giữ nhân vật ở giữa sân nhìn rõ. Bố cục desktop được phục hồi khi tắt cảm ứng.
- Có ngưỡng vào/ra khác nhau để chống rung khi ngón tay gần ranh giới. Không thêm thời gian chờ giữ nút. Khi nhả tay, lựa chọn cuối được chuyển trực tiếp vào gameplay hiện có.
- Preview hướng kéo không đưa các bit phím trung gian vào bộ đọc bàn phím: không đánh sớm khi đổi hướng, không tích lũy nhầm tổ hợp. Pause, mất focus và tắt pad hủy thao tác đang giữ.

## Tài nguyên và hiệu suất

`Assets/Resources/Touch/ControlsAtlas.png`: một ảnh 1024×512, 705.265 byte PNG, tám sprite dùng chung. Android/iOS dùng ASTC 4×4, khoảng 512 KiB dữ liệu texture khi thiết bị hỗ trợ định dạng đó; không mipmap, không giữ bản đọc CPU. Không đưa bộ ảnh hiệu ứng lớn của prototype vào game.

Sóng dùng mesh uGUI nhỏ, tối đa 170 vertex và 160 tam giác khi có hướng được nhấn. Không dùng particle system, camera hoặc render texture riêng cho nút trong game. Geometry chỉ đánh dấu cập nhật khi hướng/độ kéo đổi; layout chỉ tính lại khi kích thước/vùng an toàn/cài đặt đổi.

Đo 10.000 cập nhật kéo liên tục trong Editor: tổng 5,252 ms CPU, không cấp phát managed trong vòng đo. Đây là chi phí xử lý sự kiện kéo; không bao gồm giai đoạn dựng mesh và kết xuất sau đó.

Đo `Camera.Render` sau năm lượt làm nóng, 30 lượt mỗi biến thể, offscreen 844×390:

| Scene | Không pad, CPU ms/lượt | Có pad, CPU ms/lượt |
| --- | ---: | ---: |
| BotMatch | 1,531 | 1,713 |
| Training | 1,779 | 2,514 |
| Tutorial | 1,915 | 2,554 |
| MultiplayerCourt | 1,018 | 1,055 |

Các số trên đo CPU gửi lệnh render trong Editor, gồm thay đổi bố cục HUD; không phải FPS toàn gameplay hoặc thời gian GPU trên điện thoại. Không suy ra cam kết 60 FPS từ những số này.

## Kiểm tra

- 142/142 EditMode tests của LocalMatch đạt, gồm 36 test mới về touch, layout và chuyển cài đặt.
- 115/115 EditMode tests multiplayer đạt.
- GraphicRaycaster sau render nhận đúng bốn nút, nút được nhận ở đầu danh sách hit, trong cả bốn scene.
- BotMatch: 12 kiểu đánh và ba kiểu giao qua `CommitTouchShot` đều khởi động đúng động tác trên nhân vật thật.
- Training: giao bóng qua adapter touch khởi động đúng Serve; phiên kiểm tra sau đó được bỏ trong bộ nhớ, không nhận thưởng.
- Tutorial: đủ chín bài kỹ thuật nhận đúng cú đánh và đặt trạng thái buffered.
- Kiểm tra hình học vùng an toàn ở 667×375, 844×390, 915×412, 1280×720, 1920×1080, 1024×768 và 390×844, cả hai tay, ba cỡ nút, có lề giả lập. Kiểm tra ảnh gameplay landscape 667×375 và 844×390, bao gồm tay trái và sóng Hủy.
- Desktop: cảm ứng tắt mặc định; reflow hướng dẫn chỉ áp dụng khi pad hoạt động.

Kết quả JSON, smoke logs, ảnh và raycast audit nằm trong `UnityWork/TouchControls/`, ngoài tài nguyên build. MultiplayerCourt được xem UI offline bằng cách tạm tắt bootstrap trong bộ nhớ; sau kiểm tra đã tải lại scene gốc và xác minh bootstrap bật.

Chưa đo FPS/độ trễ trên điện thoại vật lý, chưa chạy lại trận online giữa hai điện thoại. Vì vậy các kiểm tra trên xác nhận chức năng, bố cục mô phỏng và chi phí Editor, chưa xác nhận hiệu năng mọi thiết bị.

## Mã chính

- `Assets/Scripts/LocalMatch/MobileTouchPad.cs`: layout, hình nút, đa chạm và gesture.
- `Assets/Scripts/LocalMatch/TouchShotGesture.cs`: trạng thái lựa chọn và ánh xạ kỹ thuật.
- `Assets/Scripts/LocalMatch/TouchWaveGraphic.cs`: mesh sóng trắng.
- `Assets/Scripts/LocalMatch/MobileTouchHud.cs`: bố cục hướng dẫn khi bật pad.
- `Assets/Scripts/LocalMatch/Editor/TouchControlsDiagnostics.cs`: import atlas, chụp ảnh, audit raycast và benchmark Editor.
- Các host gameplay và `NetworkPlayer` dùng adapter commit; multiplayer vẫn đi qua RPC/server hiện có.
