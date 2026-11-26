# Vợt Blue Phoenix và hiệu ứng Lam Phượng Tung Cánh — 26/11/2026

Mã skin: `paddle.bluephoenix`. Ảnh gốc: `ArtSource/Paddles/BluePhoenix/reference.png` (Electric Blue Phoenix Paddle.png của người dùng).

## Model 3D (Blender 5.2, chỉ dùng script, chạy nền)

Máy không có Python hệ thống, nên toàn bộ xử lý ảnh chạy trong Python của Blender (numpy), không cài thêm gói.

1. `ArtSource/Paddles/BluePhoenix/make_phoenix_maps.py`: đo đường viền vợt theo từng hàng ảnh (đầu vợt rộng 678 px, cán 130 px), nắn và phóng ảnh lên 2048, tách vùng lửa xanh, bạc, đá đen, mặt trăng và thân phượng hoàng. Kết quả: bản đồ màu, độ cao, normal (OpenGL) và mask (R kim loại, G che khuất, B phát sáng, A độ bóng) cho mặt vợt và cho bộ cán.
   - **Nổi**: phượng hoàng, lửa xanh, mây bạc chạm và khung bạc. **Chìm**: nền đá đen và mặt trăng (lõm, có vân miệng hố).
   - Cán dùng texture quấn quanh 360° (sinh theo thuật toán): da đen có vân chạm, dải lửa xanh xoắn ốc (bước 64 mm, chiếm 40% vòng) mép lửa răng cưa, dây bạc quấn ngược. Khiên cổ vợt, vòng bạc và chuôi lấy trực tiếp từ ảnh (chiếu thẳng).
2. `ArtSource/Paddles/BluePhoenix/build_blue_phoenix.py`: dựng hình học thật, cao 0,4118 m như các vợt khác.
   - Đầu vợt: khung bạc quét theo đường viền, mặt trước và mặt sau là **lưới chạm nổi dời đỉnh theo bản đồ độ cao** (nổi tối đa 2,4 mm), cạnh vợt có viền bảo vệ với đường sáng xanh.
   - Cán: khiên vát cạnh có biểu tượng phượng hoàng, vòng bạc có gờ, cán bọc da hình elip, **hai dây bạc nổi thật quấn theo đúng đường xoắn của dải lửa**, chuôi dạng thùng có biểu tượng phượng hoàng.
   - 25.886 tam giác (đầu 13.196, cán 12.690). Chế độ `build`, `render`, `thumbnail`, `export` giống quy trình Dragon Ruby; Cycles dùng CUDA (OptiX trên máy này lỗi nạp kernel).
3. Unity: shader `PickleVerse/Paddle/Blue Phoenix` (PBR, clear coat trên sơn đen và men xanh, lửa xanh thở và trôi dọc vợt, tia điện chạy trong lửa, viền sáng xanh). `PickleVerse > Shop > Rebuild Blue Phoenix paddle and effect` tạo vật liệu, prefab shop/thi đấu/replay, thumbnail và mục catalog (kèm mô tả hiệu ứng trong chi tiết sản phẩm).

## Hiệu ứng đánh bóng "Lam Phượng Tung Cánh"

Nguồn: `ArtSource/VFX/BluePhoenix/build_phoenix_vfx.py` (texture sprite, mesh phượng hoàng, biểu tượng, ảnh xem trước).

- Phượng hoàng xanh điện dang cánh (sải ~2 m, đuôi ~2,5 m): đầu, mỏ, mào lửa, thân, mỗi cánh 9 lông sơ cấp + 10 lông thứ cấp + 2 lớp lông phủ, 5 lông đuôi có mắt lửa và 4 dải lông mảnh. 4.600 tam giác.
- `PhoenixSpirit.shader`: thân và cánh đi trên một khung cứng của đường bay, cánh xòe ra khi vừa rời mặt vợt rồi vỗ (sóng lan từ vai ra đầu cánh); đuôi uốn theo quỹ đạo thật của bóng và gợn sóng. **Đầu lông cháy thành tro đen** có viền than xanh, vệt cháy lan vào rồi lùi ra, tro vỡ thành lỗ; khi tan, cả con chim cháy rã.
- Đi kèm: dải lửa xanh và dải khói lửa đen song song theo sóng bay; tro đen điểm than xanh rơi từ cánh, lửa đen liếm ra từ đuôi (`BlackAsh.shader`), quầng sáng xanh quanh thân, tia lửa xanh quanh bóng, vệt bóng lửa xanh.
- Lúc chạm: vòng lửa xanh, hình phượng hoàng dang cánh bùng ra, tia lửa xanh và một chùm tro đen.
- `SpineBallMotion` là phần chung tách từ `DragonBallMotion` (rồng giữ nguyên hành vi và dữ liệu prefab); `PhoenixBallMotion` dùng lại.

**Chỉ người cầm vợt này mới tạo hiệu ứng**: `BallSkinVfx` chọn hiệu ứng theo skin vợt đang hiển thị của người vừa đánh trúng (đã đồng bộ khi online). Đối thủ đánh trả bằng vợt khác thì phượng hoàng tan thành tro và bóng về vệt thường. Chỉ là phần trình bày: không đổi vật lý, luật, dữ liệu mạng hay replay. Có trong thi đấu bot, online, phòng riêng và luyện tập.

## Đăng bán trên trang quản trị

Vợt **không** được thêm vào `src/Database.sql`, nên không cần reset database. `PaddleSkins` của backend đã có `paddle.bluephoenix`, nên trang **Vật phẩm cửa hàng** liệt kê BLUE PHOENIX trong danh sách vợt có model; form tự điền tên, mô tả, bộ sưu tập, admin nhập giá (ví dụ 900 ELITE TOKEN) và bấm đăng bán như Dragon Ruby. Sau đó admin sửa giá hoặc ngừng bán như các vật phẩm khác.

- Chỉ đăng bán sau khi bản game mới (có model) đã phát hành cho người chơi: bản game cũ hơn 25/11/2026 không đọc được mã lạ (D-48).
- Client mới không còn loại hẳn vợt chưa bán khỏi bộ nhớ khi đồng bộ catalog, nên vợt admin đăng bán lúc game đang chạy sẽ hiện ở lần tải hồ sơ kế tiếp mà không cần khởi động lại.

## Kiểm thử

- Unity 6000.6.2f1 batch: **753/753 EditMode** đạt (`artifacts/blue-phoenix-20261126/editmode-results.xml`), gồm test mới: catalog Blue Phoenix (đủ model, 2 renderer đúng shader, 15k–30k tam giác, kích thước như vợt khác, mặt chạm nổi, mô tả hiệu ứng, mới nhất trong shop), hiệu ứng trong thư viện (3 renderer, tro đen, giới hạn hạt, không shader lỗi), mesh phượng hoàng đúng trục/UV thứ hai/màu đỉnh, catalog nhận vợt được đăng bán sau. Các test Dragon Ruby và `ServerKnowsEverySkinThatHasAModel` vẫn đạt.
- Backend build 0 lỗi/0 cảnh báo; `--check-api-contract` **84/84** (không dùng database), gồm quy tắc đăng bán với nội dung gợi ý của Blue Phoenix.
- Quay thử trong sân BotMatch bằng `PickleVerse > VFX > Film paddle skin effect` (render Editor, không phải trận thật): ảnh trong `artifacts/blue-phoenix-20261126/film/` (góc camera thi đấu, góc nghiêng, góc bám bóng, cận cảnh vợt).

Chưa chạy:
- `--check-content` (đăng bán, mua, ngừng bán Blue Phoenix trên PostgreSQL tạm): Docker Desktop không chạy và dịch vụ của nó cần quyền quản trị. Khi bật Docker: `./scripts/Invoke-FunctionalChecks.ps1 -Build -Check content`.
- `npm run type-check` của web admin: `src/Frontend` chưa có `node_modules` (chỉ thêm một dòng dữ liệu demo).
- Trận Play Mode hay online hai máy với vợt này; FPS trên máy yếu/mobile.
