# Căn cú chạm và phản hồi đánh bóng

Ngày thực hiện: 15/10/2026. Áp dụng cho BotMatch, MultiplayerCourt, Training và Tutorial; sử dụng rig nam/nữ và bộ nút hiện có.

## Nguyên nhân đã xác nhận

- Cửa sổ nhận cú đánh cũ mở trước/sau mốc animation 115 ms, với bán kính hỗ trợ khác nhau giữa các chế độ: 45–65 cm. Bóng có thể được trả khi vợt còn ở phần lấy đà.
- Chọn thuận tay/trái tay bằng dấu tọa độ X không phù hợp một số clip drop: điểm chạm thuận tay có thể nằm bên trái trục giữa nhân vật, còn điểm trái tay gần giữa người.
- Điểm smash cấu hình ở cao độ 2,02 m nhưng tay bị giới hạn theo chiều dài xương. Đo trên rig thực tế cho thấy điểm cấu hình lệch mặt vợt khoảng 41 cm ở nữ và 35 cm ở nam.
- Multiplayer lấy mẫu động tác khi RPC đến và nội suy bóng từ snapshot cũ, làm hình ảnh cú chạm có thêm độ lệch thời gian.

## Tham khảo thiết kế

[EA — FIFA 22 Gameplay Deep Dive](https://careers.ea.com/ea-play/news/pitch-notes-fifa-22-gameplay-deep-dive) mô tả việc đồng bộ các animation tương tác và giữ khả năng phản hồi. Áp dụng ở đây: lấy **thời điểm chạm** làm mốc; tách phần lấy đà khỏi phần vung tay theo đà, điều chỉnh thời gian lấy đà theo bóng đang tới.

[Riot — Clarity in League](https://www.leagueoflegends.com/en-us/news/dev/clarity-in-league/) đặt trọng tâm vào hình ảnh khớp vùng tác động, phân cấp độ nổi bật và hạn chế nhiễu. Áp dụng ở đây: VFX ngắn tại bóng, cú mạnh rõ hơn cú thường, không phủ kín sân.

## Thay đổi

### Animation và vùng nhận cú đánh

- Dùng `PaddleContact` cho phép quét chuyển động tương đối của cả bóng lẫn mặt vợt giữa hai mẫu, thay vì chỉ đo bóng với vị trí vợt hiện tại.
- Xác định điểm tiếp xúc trên đoạn bóng đã di chuyển; bóng trả từ điểm đó.
- Thu cửa sổ về ±65 ms; giới hạn hỗ trợ khoảng cách tối đa 30 cm, riêng smash 16 cm. Các kiểm tra nảy bóng, Kitchen, độ cao smash, bên sân và chạm người vẫn chạy trong bộ luật hiện có.
- Chọn clip theo khoảng cách đến điểm chạm thực của hai tư thế thuận/trái tay. Hiệu chỉnh điểm smash một lần trên từng rig khi khởi tạo.
- BotMatch/Multiplayer dự đoán đường bóng, kể cả một lần nảy, để điều chỉnh thời gian lấy đà từ 100 ms đến mốc thông thường +80 ms khi bóng nằm trong vùng dự đoán hợp lý. Không đưa nhân vật tự động tới bóng.
- Training dùng thời gian bóng còn tới điểm nhận để căn động tác, chấp nhận thao tác muộn hơn trước khi vẫn còn ít nhất 100 ms chuẩn bị.
- Khi xác nhận tiếp xúc, lấy mẫu đúng tư thế chạm và dùng IK hai khớp điều chỉnh tay có giới hạn, giữ chiều dài xương. Giữ tư thế chạm 35 ms rồi chạy tiếp phần vung theo đà; hiệu chỉnh tay tan trong 120 ms.

### Âm thanh và VFX

- Phát âm thanh hiện có ngay tại điểm chạm đã xác nhận; giữ cách chọn âm giao bóng, thường và mạnh.
- Vòng xung lực cùng sáu tia ngắn: trắng ngả ngọc cho cú thường, vàng nhạt cho cú mạnh/smash; dink/reset nhỏ hơn.
- Thời lượng 180 ms cho cú thường, 240 ms cho cú mạnh. VFX không xuất hiện khi vung hụt hay cú đánh bị luật từ chối.
- Một mesh tái sử dụng cho mỗi nhân vật, 114 đỉnh, một material unlit; không tạo GameObject mỗi lần đánh và không thêm đèn realtime.
- Material `Assets/Resources/PaddleImpact.mat` được tạo bằng Unity Editor và được tham chiếu qua Resources để có mặt trong bản build.

### Multiplayer

- RPC động tác mang thời gian bắt đầu của server và thời gian lấy đà đã chọn; replica lấy mẫu theo đồng hồ server.
- RPC cú chạm mang vị trí thật, kết quả hợp lệ, vận tốc bóng sau đánh và thời gian server. Host không phát lặp lại âm/VFX.
- Snapshot bóng có timestamp. Client trình bày ở nhịp render, dự đoán tối đa 80 ms; snapshot trước cú chạm không kéo bóng quay ngược qua vợt sau khi đã nhận RPC xác nhận.
- Protocol nâng từ **8 lên 9** vì thay đổi payload mạng. Hai máy cần build cùng phiên bản này.

## Kiểm thử

| Phạm vi | Kết quả |
|---|---|
| Unity EditMode | 337/337, gồm 8 trường hợp mới về quét bóng/vợt và bất biến vị trí sân |
| Rig và cú chạm | 138/138: nam/nữ, thuận/trái tay, drive/dink/drop/volley/reset/smash, lấy đà thường/nhanh, bước mẫu 30/60/120 Hz, giao bóng, dự đoán và vung hụt |
| Độ lệch tâm mặt vợt sau hiệu chỉnh trong bộ kiểm tra | Tối đa khoảng 12,6 cm; gồm các cú tiếp xúc gần mép vợt |
| BotMatch | 12/12 tổ hợp phím ghi nhận một cú hợp lệ; mô phỏng tự chơi 240 giây có 59 cú đánh, 28 rally, rally dài nhất 6 cú |
| Training | 12/12 bài đạt 3 sao bằng điều khiển và tiếp xúc bóng thực, gồm hai bài đánh đôi |
| Tutorial | 32/32 kiểm tra; toàn bộ chín bài nút đánh hoàn thành bằng cú chạm thực |
| Multiplayer | 20/20 kiểm tra NGO host cục bộ: trúng/hụt, giao bóng, quỹ đạo, luật và tiến trình |
| VFX | 2.000 lần phát + cập nhật + ẩn: 0 byte cấp phát managed trong vòng đo, tổng CPU khoảng 49 ms ở Editor trên máy này |
| Hình ảnh | Render kiểm tra cú thường nữ và cú mạnh nam trên sân BotMatch, thấy bóng ở mặt vợt và vòng/tia tiếp xúc |

Số đo VFX là chi phí CPU của đoạn phát/cập nhật, không phải phép đo GPU hoặc cam kết FPS toàn trận. Multiplayer đã kiểm tra ở host cục bộ; chưa chạy lại hai máy qua Internet và chưa đo với ping cao trong lần thay đổi này.

## Tái kiểm tra

Các script chạy qua Unity CLI `run_script` trong thư mục `My project/Tools`:

- `ShotFeelValidation.Run`: bộ kiểm tra rig/cú chạm và dự đoán.
- `ShotFeelGameValidation.Bot`: 12 tổ hợp cùng mô phỏng tự chơi; cần profile QA.
- `TrainingDiagnostics.All`, `TutorialValidation.Run`: các bài học và bài tập.
- `BotContactValidation.Run`: NGO host cục bộ; vào Play Mode mới sau khi biên dịch để NGO khởi tạo đầy đủ.
- `ShotFeelVisualValidation.Run`: render và đo VFX; cần profile QA.
- `ShotFeelSetup.Run`: tạo material bằng Editor nếu cần khôi phục asset.

Kết quả chi tiết và ảnh nằm trong `UnityWork/ShotFeel`; kết quả Training/Tutorial trong `UnityWork/Training` và `UnityWork/Tutorial`. Các lượt luyện tập/mô phỏng dùng profile QA riêng.
