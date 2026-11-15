# Sân đấu chiều tà và khu đô thị quanh sân — 15/11/2026

Phạm vi: hai sân thi đấu `Resources/Courts/Professional` và `LowPoly` (model trong
`Assets/Models/Stadium_Enhanced`), bối cảnh quanh sân và ánh sáng ban ngày của các scene trận đấu.
Không đổi kích thước/vị trí vùng chơi, lưới, vạch, collider, script gameplay hay netcode.

## Đã làm

**Sân (Blender → GLB cùng đường dẫn, giữ GUID):** gom vật liệu về bộ vật liệu chung có màu theo đỉnh
(Professional 24 → 8, LowPoly 17 → 9); mặt sân acrylic có hạt và normal map, lưới đan đen có băng trắng,
rào lưới thép bọc nhựa, thép mạ, sơn tĩnh điện, bê tông, gạch lát, gỗ dầu. Bốn cột đèn hộp vuông được thay
bằng cột thon có chân đế khía, vành đồng và đỉnh hình đàn lia ôm đầu đèn LED có lá tản nhiệt (cùng object,
cùng đèn, cùng vật liệu bật/tắt đêm). Từng đỉnh của mặt sân, vạch và lưới được kiểm tra không đổi.

**Bối cảnh (dùng chung cho hai sân, lồng trong prefab sân):** quảng trường lát gạch quanh sân; 4 sân
pickleball lân cận (2 bộ màu mỗi bên) có rào và lưới; vành đai đường hai làn bo góc cong, vỉa hè, vạch kẻ,
vạch qua đường; lối đi uốn lượn, hồ hình hạt đậu, giàn hoa vòm, đài phun hai tầng, ki-ốt cà phê mái vòm có
mái hiên sọc; 32 đèn đường/đèn công viên cần uốn và hoa văn xoắn; ghế gang uốn chữ S; nhà phố (phía hướng
nắng thấp để không che công viên, phía đối diện cao tầng hắt nắng vàng trong khung hình) và đường chân trời mờ.
Không có collider hay script nào được thêm vào phần trang trí.

**Cây:** 100 cây (xà cừ, sấu/long não, phượng vĩ, cọ) + 192 bụi/hàng rào cây + dây leo trên giàn. Tán lá là
thẻ lá alpha (atlas vẽ thủ tục 1024²) với pháp tuyến hình vòm tán, gió trên GPU, xuyên sáng khi ngược nắng.
Mỗi cây có LOD0/1/2 + billboard chéo (LOD3); GPU instancing; cây không đổ bóng thời gian thực.

**Ánh sáng chiều tà:** mặt trời vàng cam (1, .74, .50), cao 21°, sau-trái camera đầu sân gần nên mặt đối
thủ được chiếu sáng và bóng đổ dài chéo sân; ambient trilight trời xanh/đất ấm; sương mù ấm 55–330 m;
bầu trời thủ tục (gradient, quầng mặt trời theo hướng đèn, mây trôi viền sáng). Bóng tĩnh của nhà, cây,
rào, khán đài được nướng sẵn (Cycles) thành mặt nạ 1024² cho mặt đất; shader lấy `min(bóng nướng, bóng thời
gian thực)` nên người chơi và bóng vẫn đổ bóng thật. Ban đêm giữ nguyên như cũ; đèn đường và cửa sổ nhà sáng.

**Độ nét:** mipmap + anisotropic 8 cho mặt đất, normal map không cần tangent (khung đạo hàm), alpha-to-coverage
cho lá/lưới, lưới thép ở xa chuyển sang dither ổn định thay vì thành mảng đặc. Mobile bật MSAA 2x (trước đây
tắt hẳn; lựa chọn "tắt" của người chơi vẫn được tôn trọng). Texture Android: ASTC 6×6, ASTC 5×5 cho ảnh có alpha.

## So sánh trước/sau

Ảnh ghép: `BlenderWork/SunsetDistrict20261115/compare/` (thư mục gitignored), ví dụ
`SunsetDistrict-BeforeAfter.jpg`. Ảnh Unity: `before-*.png`, `after-*.png`, ảnh trong trận thật `Live-*.png`;
ảnh Blender Eevee: `blender/final-*.png`.

### Tài sản (prefab sân, tính cả bối cảnh)

| Chỉ số | Professional trước → sau | LowPoly trước → sau |
|---|---:|---:|
| Tam giác, mọi LOD | 597.897 → 480.456 | 457.542 → 463.574 |
| Tam giác bộ LOD0 (PC gần) | 507.787 → 384.108 | 386.018 → 367.226 |
| Tam giác bộ LOD1 (mobile) | 100.671 → 146.452 | 67.654 → 129.570 |
| Vật liệu khác nhau | 29 → 19 | 22 → 19 |
| Renderer đổ bóng thời gian thực | 44 → 16 | 42 → 18 |
| Collider / script / đèn | 0 / 5 / 4 → 0 / 5 / 4 | 0 / 5 / 4 → 0 / 5 / 4 |
| File model GLB được tham chiếu | 3,27 MB → 5,42 MB | 2,25 MB → 4,29 MB |
| Texture runtime (Editor, PC BC7) | 3,72 MB → 11,90 MB | 3,90 MB → 11,90 MB |

Model tăng vì bối cảnh nay có cả khu đô thị, 4 sân phụ và 100 cây (trước chỉ có cỏ và 13 cây thưa).
Texture Android ước tính theo kích thước/định dạng ASTC (không phải số đo từ APK): khoảng 0,9 MB → 3,7 MB.
Bộ LOD1 tăng chủ yếu do mesh bối cảnh tĩnh (46,8 nghìn tam giác, không có LOD) và tán cây LOD1.

### FPS trong Editor (RTX 3050 Laptop, i5-11400H, Game view 1920×1080)

Fixture: prefab sân thật, 4 nhân vật chạy, `MatchEnvironmentLighting` và `MatchRenderBudget` thật, camera
gameplay; làm nóng 3 s, lấy mẫu 5 s. "Mobile" là chính sách render mobile chạy trên GPU PC, **không phải số đo
điện thoại**. Lần đo sau thứ nhất có một ca tụt do Editor biên dịch shader mới (123 FPS, p95 14,8 ms); bảng
dưới là lần đo thứ hai khi cache đã ấm. Cả hai lần đều có lệnh CLI thăm dò mỗi 12 s, như lần đo trước.

| Sân | Cấu hình | FPS trước → sau | p95 ms trước → sau | GPU ms trước → sau | Draw call trước → sau | Tam giác trong khung trước → sau |
|---|---|---:|---:|---:|---:|---:|
| Professional | Desktop ngày | 144,0 → 143,6 | 7,16 → 7,28 | 2,49 → 2,44 | 320 → 284 | 1.046.786 → 1.078.397 |
| Professional | Desktop đêm | 142,8 → 141,4 | 7,24 → 7,31 | 2,17 → 2,18 | 172 → 176 | 498.399 → 571.972 |
| LowPoly | Desktop ngày | 143,2 → 137,4 | 7,64 → 8,48 | 2,28 → 2,33 | 272 → 305 | 947.786 → 963.141 |
| LowPoly | Desktop đêm | 144,0 → 144,0 | 7,21 → 7,25 | 2,20 → 2,08 | 151 → 188 | 456.887 → 523.196 |
| Professional | Mobile ngày | 143,0 → 143,6 | 7,34 → 7,25 | 2,14 → 2,16 | 172 → 175 | 474.074 → 546.263 |
| Professional | Mobile đêm | 144,0 → 136,5 | 7,20 → 9,78 | 2,28 → 2,16 | 132 → 144 | 321.859 → 398.553 |
| LowPoly | Mobile ngày | 141,2 → 138,8 | 7,27 → 7,27 | 2,15 → 2,13 | 151 → 185 | 426.146 → 497.235 |
| LowPoly | Mobile đêm | 140,2 → 136,4 | 7,52 → 7,38 | 1,94 → 2,11 | 118 → 154 | 287.135 → 366.645 |

Tất cả 8 ca sau nâng cấp: FPS trung bình ≥ 136, p95 ≤ 9,8 ms (< 16,67 ms). Editor gần chạm trần ~144 FPS
nên FPS trung bình không phân biệt được chi phí; thời gian GPU gần như giữ nguyên. Draw call và tam giác trong
khung tăng 2–25 % ở phần lớn ca (thêm cả một khu đô thị trong tầm nhìn, far clip 100 → 460 m).
Tam giác trong khung của Unity tính cả lượt shadow và 4 nhân vật.

## Kiểm thử

- Hình học: 12/12 đạt — mặt sân/vạch/lưới (từng đỉnh, sai số 0,1 mm), điểm vào sân và điểm kết quả,
  giới hạn camera, 4 đèn (vị trí, hướng, góc, cường độ, màu, bóng), số collider, danh sách script của cả hai sân.
- Ba shader mới biên dịch không lỗi; script biên dịch sạch.
- Play mode trên scene Menu và BotMatch thật (đăng xuất; chỉ gỡ cổng chuyển về Login trong phiên tạm,
  không sửa scene hay mã xác thực): **62/64 đạt**, 0 lỗi runtime. Đạt: Menu, tạo BotMatch, đổi hai sân lúc chạy,
  bối cảnh có mặt và không có collider, cảnh vào sân đúng khung và chạm đất, 4 đèn trắng ngày/đêm, bầu trời
  chiều tà, camera kết quả và chữ Thắng/Thua.
  **Không đạt 2 kiểm tra "mô phỏng cục bộ đánh trúng bóng"** (cả hai sân): trận vẫn chạy (pha Rally, tỉ số 2-0,
  2 rally) nhưng người chơi tự động vô-lê cú giao bóng nên bị bắt lỗi "phải để bóng nảy", số cú đánh = 0.
  Mã mô phỏng không đọc vật lý, collider hay hình học sân, nên lỗi này không do thay đổi đồ họa; tuy vậy
  chưa chạy lại fixture này trên bản gốc để đối chứng.
- Training/Tutorial (sân `Stadium_Enhanced.glb`, không đổi model) nhận ánh sáng chiều tà; ảnh kiểm tra
  `after-Training.png`, `after-Tutorial.png`.

## Chưa kiểm chứng

- Chưa đo trên điện thoại Android, chưa build APK mới, chưa đo dung lượng APK.
- Chưa chạy trận Photon hai máy, chưa build dedicated server (phần trang trí không thêm collider/script;
  `StadiumEnvironment` chỉ thêm 3 trường và lệnh đặt biến shader toàn cục).
- Chưa chạy bộ EditMode test (không test nào tham chiếu sân hay ánh sáng).
- Menu và Replay dùng prefab sân mới nhưng ánh sáng riêng của chúng, nên không có bóng nướng chiều tà.

## File

- Sửa: `Assets/Models/Stadium_Enhanced/{ProfessionalStadium,PickleballCourt_LowPoly}.glb`,
  `Assets/Resources/Courts/{Professional,LowPoly}.prefab`, `Scripts/LocalMatch/MatchEnvironmentLighting.cs`,
  `Scripts/LocalMatch/StadiumEnvironment.cs`, `Scripts/Settings/SettingsRuntime.cs`.
- Mới: `Assets/EnvironmentContent/SunsetDistrict/` (texture, vật liệu, shader, prefab cây/bối cảnh),
  `Assets/Models/Stadium_Enhanced/District/` (SunsetDistrict, SunsetTrees, VenueFixtures), 
  `Assets/Resources/Lighting/GoldenHourSky.mat`, công cụ Editor `SunsetDistrictInstaller.cs`, `SunsetDistrictProbe.cs`.
- Nguồn Blender và script: `ArtSource/Stadiums/SunsetDistrict/` (xem README ở đó). `ArtSource/` nằm trong
  `.gitignore`; các file này đã được thêm bằng `git add -f`, file mới sau này cũng cần làm như vậy.
