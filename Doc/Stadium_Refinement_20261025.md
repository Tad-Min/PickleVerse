# Nâng cấp hai sân PickleVerse — 25/10/2026

Đã kiểm tra GitHub và pull fast-forward từ `3abbe48` lên `8e06f7c` (`fix format coding`) trước khi làm sân. Những thay đổi dưới đây nằm trên bản đã pull. Không sửa backend/database, không chạy container hay migration. So sánh SHA-256 của **140 file backend** sau khi pull: **0 file thay đổi**. Không có diff trong `src`, gameplay scripts, scene `.unity` hoặc ProjectSettings.

## Kết quả đồ họa

- Hai model được chỉnh bằng Blender 5.1.1 và thay vào đúng hai đường dẫn GLB hiện tại; giữ GUID của asset và prefab.
- Sân chuyên nghiệp có bảng PICKLEVERSE và các câu “Vào sân. Tỏa sáng.”, “Hết mình từng pha bóng”, “Chơi đẹp • Chơi hết mình”. Chữ tiếng Việt được đóng trong một atlas, không tạo mesh cho từng chữ.
- Thêm ghế gỗ bo cạnh, thùng nước, chai nước, khăn, lối lát và cây/bụi cây ngoài sân. Ghế cũ bị trùng đã được bỏ khỏi mesh. Hai sân có thảm cỏ dưới cây; hàng cây thưa, giữ khoảng trời nhìn thấy từ menu.
- Đổi bề mặt sang xanh dịu, vật liệu nhám, hạn chế phản chiếu gắt. Dùng 5 map nhỏ, mipmap/trilinear và ASTC 6×6 cho Android/iOS. Vật thể trang trí không có collider hay script Update riêng.
- Giảm geometry lan can, gộp các phần dùng chung vật liệu; giữ nguyên mesh vị trí vạch sân và lưới. Prefab được căn lại mặt thi đấu về `y = 0`, giữ điểm vào sân, điểm camera kết quả và vùng giới hạn camera.
- Giữ bốn đèn pha trắng tại vị trí/góc chiếu cũ; mức đêm giảm từ 155→115 ở sân chuyên nghiệp, 125→95 ở sân ngoài trời. Giới hạn một nguồn bóng đổ mềm của đèn pha trên desktop. Cấu hình mobile hiện có tiếp tục bỏ bóng đổ phụ.
- Menu, BotMatch và MultiplayerCourt dùng prefab nâng cấp. Replay lấy hai sân qua cùng hệ thống tài nguyên. Training/Tutorial vẫn giữ loại sân đang dùng; đã kiểm tra tham chiếu của hai scene này, không thay loại sân ngoài yêu cầu.

Bốn ảnh chọn sân ngày/đêm trong ModeSelection cũng đã được render lại từ sân mới, giữ GUID và giới hạn import 512px.

## Dung lượng nguồn chạy game

| Model | GLB trước | GLB sau | Tam giác sau | Mesh renderer phần model |
|---|---:|---:|---:|---:|
| ProfessionalStadium | 3.488 MB | 1.863 MB | 28,832 | 17 |
| PickleballCourt_LowPoly | 0.169 MB | 0.798 MB | 10,982 | 20 |

Tổng hai GLB: **3.657 → 2.661 MB**, giảm **27.2%**. Năm PNG dùng chung: **250.2 KB**. Đây là số byte của asset nguồn, không phải phép đo APK sau build. Hai `.blend` khoảng 1,8 MB nằm ngoài Assets nên không đi vào game. Ảnh render, bản gốc tạm và log nằm trong BlenderWork đã được Git bỏ qua.

## Kiểm thử đã thực hiện

- Biên dịch script Unity: không có lỗi biên dịch ở lần kiểm tra cuối.
- 16/16 kiểm tra prefab/geometry: bề mặt thi đấu và lưới không đổi vị trí, điểm cinematic/camera giữ nguyên, không thiếu vật liệu/mesh, không có collider trang trí, đủ bốn đèn trắng.
- 20/20 kiểm tra tham chiếu Menu, BotMatch, MultiplayerCourt, Training, Tutorial, Replay: không thiếu script/mesh/vật liệu ở phần được kiểm tra.
- 4/4 ảnh xem trước sân đã import và vẫn được ModeSelection tham chiếu.
- 56/56 kiểm tra trình diễn trong Play mode: menu thật, đổi hai sân ở BotMatch, nhân vật đi vào sân và được camera bắt đúng, mô phỏng giao/đánh bóng có phát sinh cú đánh, ánh sáng ngày/đêm, camera kết quả và chữ Thắng/Thua.
- Các phép thử Play mode đã hoàn tất không ghi nhận log Error/Exception/Assert.
- Phép thử BotMatch là fixture Editor đang đăng xuất, gọi mô phỏng cục bộ; không gửi kết quả/nhận thưởng lên server. UI đăng nhập được bỏ khỏi phiên thử trong bộ nhớ, không sửa mã xác thực hay scene lưu trên đĩa. Kết quả này không thay cho kiểm thử kết nối online thật.

### FPS ở scene thực tế

Máy đo: RTX 3050 Laptop GPU, Unity 6000.6.2f1, cửa sổ Game 1920×1080. Mỗi mẫu lấy sau thời gian làm nóng; cấu hình được đổi trong bộ nhớ và khôi phục sau test.

| Scene/trường hợp | FPS trung bình | p95 frame (ms) |
|---|---:|---:|
| Menu — actual lobby | 142.8 | 7.74 |
| BotMatch — Professional day | 142.2 | 7.06 |
| BotMatch — Professional night | 142.2 | 8.48 |
| BotMatch — LowPoly day | 141.3 | 7.12 |
| BotMatch — LowPoly night | 141.7 | 8.12 |

### So sánh render với bốn nhân vật chuyển động

Cùng fixture render hai prefab sân thật, bốn prefab nhân vật nam/nữ trên quỹ đạo nhỏ có pha thời gian lặp lại, camera thi đấu và MatchRenderBudget đang dùng trong game. Mỗi mẫu 5 giây sau 3 giây làm nóng, reset vị trí/pha chuyển động/ngân sách trước từng mẫu. Bản cũ là prefab/GLB/vật liệu sao chép riêng vào thư mục Editor, không ghi đè asset đang nâng cấp. Cột “Mobile preview” dùng GPU máy tính với cấu hình mobile; không phải FPS đo trên điện thoại.

| Sân | Cấu hình | Thời điểm | FPS trước | FPS sau | p95 sau (ms) | Scale 3D trước → sau |
|---|---|---|---:|---:|---:|---:|
| Professional | Desktop | Ngày | 100.4 | 102.6 | 11.24 | 0.78 → 0.75 |
| Professional | Desktop | Đêm | 108.5 | 120.0 | 10.73 | 0.78 → 0.75 |
| LowPoly | Desktop | Ngày | 100.2 | 112.2 | 12.55 | 0.75 → 0.60 |
| LowPoly | Desktop | Đêm | 116.7 | 120.9 | 13.24 | 0.70 → 0.68 |
| Professional | Mobile preview | Ngày | 141.2 | 144.0 | 7.03 | 0.67 → 0.67 |
| Professional | Mobile preview | Đêm | 139.0 | 144.0 | 7.04 | 0.67 → 0.67 |
| LowPoly | Mobile preview | Ngày | 144.0 | 144.0 | 7.03 | 0.67 → 0.67 |
| LowPoly | Mobile preview | Đêm | 144.0 | 132.3 | 13.62 | 0.67 → 0.67 |

8/8 mẫu sau nâng cấp đạt FPS trung bình ≥60 và p95 ≤16,67 ms. Bộ điều chỉnh chất lượng hiện có tự đổi scale phần 3D, vì vậy cần đọc cả cột scale khi so sánh; UI Overlay giữ độ phân giải màn hình. Các mẫu Editor phụ thuộc tải hệ thống, culling và nhiệt độ GPU; có trường hợp nhanh hơn, có trường hợp chậm hơn bản gốc. Không suy diễn FPS trung bình thành cam kết mọi frame đều trên 60. Chưa đo Galaxy A32 thực, chưa thử trận Photon hai máy và chưa build lại APK trong nhiệm vụ này.

## File và bằng chứng

- Nguồn Blender: [ArtSource/Stadiums](../ArtSource/Stadiums/README.md).
- Công cụ Editor: `My project/Assets/Editor/VenueRefinement/` (không đóng gói runtime).
- Nhật ký và dữ liệu thô: `BlenderWork/VenueRefinement20261025/geometry-validation.json`, `scene-validation.json`, `presentation-validation.json`, `before-performance.json`, `after-performance.json`, `final-validation.json`.
- Ảnh Unity cuối: `BlenderWork/VenueRefinement20261025/Unity-Menu.png`, `Unity-Professional-Day.png`, `Unity-Professional-Night.png`, `Unity-LowPoly-Day.png`, `Unity-LowPoly-Night.png`.
- Ảnh Play mode: `Live-Menu.png`, `Live-Professional-Entrance.png`, `Live-Professional-Win.png`, `Live-LowPoly-Day.png` trong cùng thư mục.

## Tài liệu kỹ thuật tham chiếu

Blender xuất glTF theo các node vật liệu được hỗ trợ; hệ số màu cần arrangement Mix RGBA/Multiply phù hợp. Đã kiểm tra màu sau khi nhập Unity và lưu nguồn Blender dùng node hiện hành. [Blender 5.1 glTF manual](https://docs.blender.org/manual/en/5.1/addons/import_export/scene_gltf2.html).
