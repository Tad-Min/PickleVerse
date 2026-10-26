# Cây và cỏ tự nhiên cho hai sân — 26/10/2026

Đã tích hợp cây có thân, rễ, cành cấp 1/2, cành nhỏ và từng lá có mép nhọn,
độ cong, gân lá. Thảm cỏ có các lá riêng, cong và thuôn ở đầu; mặt đất có
texture hữu cơ thay cho màu xanh phẳng. Hai mẫu cây, một bụi cây và một cụm
cỏ được dùng chung giữa Professional và LowPoly. Độ chi tiết được thiết kế
cho góc nhìn sân đấu và menu của game thời gian thực.

## Tài nguyên và tối ưu

- Nguồn Blender: `ArtSource/Stadiums/NaturalVegetation.blend`,
  `ProfessionalStadium.blend`, `PickleballCourt_LowPoly.blend`.
- Các file nguồn đã đóng gói texture; preview gió lặp ở frame 1–180, có cả
  biên độ dương và âm. Hai sân dùng các bản sao liên kết của bốn mesh LOD0.
- Model game dùng chung: `My project/Assets/EnvironmentContent/NaturalPark/NaturalVegetation.glb`:
  **1,686,208 byte (1,69 MB thập phân)** cho toàn bộ 12 mesh/LOD.
  Không nhân đôi geometry cây cỏ vào từng GLB sân.
- Tổng ba GLB runtime: **3,830,304 byte**. So với bản cây dạng khối
  đầu lượt làm việc này, tổng GLB tăng khoảng 1,17 MB để có cành/lá/cỏ thật.
  Đây là dung lượng file nguồn GLB, không phải số tăng kích thước APK đã đo.
- Bốn texture mới tổng 565.624 byte PNG; bark/leaf tối đa 512px, grass/ground
  256px; mipmap, texture không Read/Write, Android/iOS ASTC 6x6.
- Normals được chia sẻ giữa các mặt lá/cỏ, dữ liệu gió lượng tử hóa 8 bit;
  đã loại texture nhúng lặp khỏi GLB. Model prototype giảm từ bản xuất ban
  đầu khoảng 3,67 MB xuống 1,69 MB dù đã tăng mật độ cỏ gần camera.
- Mỗi cây/bụi/cụm cỏ có ba LOD. Cỏ: 5.520 / 480 / 60 tam giác mỗi patch;
  giữ chi tiết cao trong khoảng 9m, giảm mật độ khoảng 27m và cull gần 39m
  với góc nhìn phối cảnh 54°, LOD bias 1. Khoảng cách thực tế phụ thuộc FOV,
  độ lớn patch và chất lượng. Shader làm thấp cỏ dần từ 24–38m.
- Chính sách mobile hiện có bỏ LOD0. Các mesh/material dùng chung và bật hỗ
  trợ instancing; Unity chọn batching tùy cấu hình URP. Không thêm Update,
  Animator, bone hay Collider cho từng cây/cọng cỏ. Cỏ không đổ bóng.
- Gió chạy trên GPU, dùng cùng phép biến dạng trong forward, shadow và depth.
  Mặt lá/cỏ là geometry opaque theo hình dáng thật, không có hình chữ nhật
  trong suốt gây overdraw.
- Đã dọn bốn material cây dạng khối cũ và texture lawn không còn tham chiếu.
  Giữ texture bark cũ vì vẫn được ghế gỗ sử dụng. File Blender nằm ngoài Assets.

## Kiểm chứng đã thực hiện

**112/112 kiểm tra chức năng/asset đạt; 8/8 cấu hình render đạt
ngân sách trung bình và P95 dưới 16,67ms. Unity biên dịch và shader hiện tại
không có lỗi.**

- 11 kiểm tra cây/cỏ: ba LOD, vertex mask, vật liệu, hỗ trợ shader, bỏ tán khối,
  không thêm vật lý hay hành vi CPU cho từng cây.
- 5 kiểm tra gió/mobile: bật gió làm thay đổi ảnh render cây/cỏ; tắt gió cho
  hai ảnh giống nhau; kiểm tra trọng số gốc bằng 0; mobile chọn LOD1 trở xuống.
- 16 kiểm tra hình học: mặt sân, lưới, giới hạn camera, vị trí vào sân/kết quả,
  đèn và collider không thay đổi so với bản sân trước đó.
- 20 kiểm tra scene Menu/BotMatch/MultiplayerCourt/Training/Tutorial/Replay:
  không thiếu script/mesh/material; các scene dùng sân mới vẫn khớp mặt sân.
- 4 ảnh preview chọn sân được cập nhật và vẫn có dependency từ ModeSelection.
- 56 kiểm tra trên Menu/BotMatch thật, chọn cả hai sân, intro theo nhiều mốc
  thời gian, mô phỏng bóng cục bộ, camera kết quả và chữ Thắng/Thua.
- Ba nguồn Blender đã được mở bằng Blender 5.1.1, xác nhận texture đóng gói,
  có animation, không còn các object tán khối cũ.

### Bài đo render tách riêng

RTX 3050 Laptop GPU / i5-11400H, Unity Editor 6000.6.2f1, game view 1920×1080,
bốn actor thật đang di chuyển, MatchRenderBudget và chất lượng ngày/đêm hiện
có. Warm-up 3 giây, lấy mẫu 5 giây mỗi cấu hình. Scene đo đã xác nhận không
có root gameplay/menu dư trước khi tạo fixture.

| Sân | Cấu hình | Ánh sáng | FPS trung bình | P95 ms |
|---|---|---|---:|---:|
| Professional | Desktop | Ngày | 144.0 | 7.05 |
| Professional | Desktop | Đêm | 144.2 | 7.22 |
| LowPoly | Desktop | Ngày | 115.5 | 13.97 |
| LowPoly | Desktop | Đêm | 98.3 | 12.34 |
| Professional | Mobile preview trên PC | Ngày | 144.0 | 7.07 |
| Professional | Mobile preview trên PC | Đêm | 144.0 | 7.08 |
| LowPoly | Mobile preview trên PC | Ngày | 142.8 | 7.74 |
| LowPoly | Mobile preview trên PC | Đêm | 140.4 | 8.07 |

Mobile preview sử dụng GPU PC và render scale mobile; các con số này không
đại diện FPS Galaxy A32. Render scale điều chỉnh thích ứng, không phải phép
so sánh chất lượng ở cùng độ phân giải 3D cố định. Các lượt đo ban đầu trước
khi tách fixture có menu do sign-in gate tự tải nên không dùng để suy ra mức
tăng FPS trước/sau.

### Bài đo scene thật

Scene Menu và BotMatch thật, tài khoản giữ trạng thái đăng xuất trong fixture.

| Scene/cấu hình | FPS trung bình | P95 ms | Frame chậm nhất ms |
|---|---:|---:|---:|
| Menu — actual lobby | 143.5 | 7.13 | 24.07 |
| BotMatch — Professional day | 141.5 | 7.13 | 101.89 |
| BotMatch — Professional night | 143.7 | 7.07 | 41.37 |
| BotMatch — LowPoly day | 140.0 | 7.09 | 16.23 |
| BotMatch — LowPoly night | 137.2 | 12.24 | 17.91 |

Các mẫu đo scene thật có frame chậm đơn lẻ trong Editor, lớn nhất khoảng
102ms, mặc dù FPS trung bình/P95 đạt ngân sách. Không có cơ sở cam kết mọi
frame luôn trên 60 FPS hoặc không có hitch khi tải scene. Chưa đo trên điện
thoại vật lý, chưa build/profiling APK mới và chưa chạy trận Photon hai máy.
Góc cận một cây và một patch cỏ LOD0 đạt khoảng 142–143 FPS trong fixture PC.

## Phạm vi được giữ nguyên

Đối chiếu SHA-256 **140 file backend không có thay đổi**.
Không sửa database, cấu hình Docker, gameplay/input, scene YAML bằng tay hoặc
ProjectSettings. Ba font được đối chiếu với bản lúc bắt đầu lượt này và giữ
nguyên, kể cả font đã có thay đổi từ trước. Editor được trả về Menu, tắt Play,
scene không dirty. Không commit/push.

`git diff --check` chỉ báo khoảng trắng cuối dòng tại các trường `m_Name:`
rỗng do Unity tự serialize trong hai prefab; không chỉnh tay YAML để loại bỏ.

Ảnh và bằng chứng đo đầy đủ nằm tại `BlenderWork/NaturalPark20261026` (thư mục
ignored, không đưa log/ảnh QA vào game). Các file chính: `summary.json`,
`after-performance.json`, `presentation-validation.json`,
`wind-closeup-validation.json`, `blender-source-validation.json`.

## Cơ sở kỹ thuật tham khảo

- [Unity: Level of Detail](https://docs.unity3d.com/6000.0/Documentation/Manual/LevelOfDetail.html)
- [Unity: GPU instancing](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/reduce-draw-calls/gpuinstancing)
- [Unity: URP asset settings](https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/urp-reference/universalrp-asset)

Geometry và texture thực vật được tạo trong dự án bằng Blender/Python;
không cần tải gói cây cỏ bên ngoài để dùng các asset này.
