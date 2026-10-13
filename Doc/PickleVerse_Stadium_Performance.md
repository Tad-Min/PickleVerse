# Tối ưu hiệu năng và ánh sáng sân vận động

Ngày kiểm tra: 13/10/2026. Áp dụng cho BotMatch, Training, Tutorial và MultiplayerCourt; prefab sân dùng chung cũng cập nhật ánh sáng trong Menu.

**Cập nhật ánh sáng theo phản hồi:** đèn sân và mặt đèn dùng trắng trung tính; intensity mái 80 (trước 110), Y chiếu sân 45 (trước 65). Có thêm 8 đầu đèn Y hướng ra khán đài, intensity 8, không tạo bóng phụ. Ban ngày các nguồn phụ này tắt. Multiplayer dùng lựa chọn ngày/đêm trong snapshot roster của chủ trận; máy khách và người kết nối lại áp dụng snapshot đó thay vì lựa chọn riêng. Các số FPS trong bảng bên dưới là bài đo trước lần tinh chỉnh màu/cường độ này; JSON mới tại `UnityWork/StadiumPerformance/night-white-tune.json`.

Lần tinh chỉnh này qua 233/233 EditMode tests, 73 kiểm tra scene và 27 kiểm tra runtime ánh sáng. Đã kiểm tra payload ngày/đêm của host áp dụng đúng khi lựa chọn lưu trên máy khách trái ngược; đây là kiểm tra deserialize/callback, chưa phải thử truyền mạng giữa hai thiết bị. Bài đo Editor ban đêm: BotMatch 123,44 FPS, Training 140,96 FPS, Tutorial 132,09 FPS, MultiplayerCourt với bốn nhân vật mô phỏng 115,26 FPS trung bình. Ảnh kiểm tra nằm trong `UnityWork/NightLighting/`.

## Thay đổi

- Giữ 166 vị trí đèn đã quét từ model, nhưng gộp nguồn chiếu sáng theo cụm: **8 nguồn ở cột Y, 16 nguồn ở mái**. Các mặt đèn trong từng cụm vẫn phát sáng. Ban ngày dùng sun + Y, ban đêm dùng Y + mái. Giữ 4 nguồn Y có bóng thời gian thực cho nhân vật.
- Đèn mái được hướng vào vùng thi đấu thay vì chiếu thẳng xuống khu vực dưới mái; range tăng từ 13 lên 32 m, intensity từ 5 lên 110 ở thời điểm đêm hoàn toàn. Có thể chỉnh `roofNightIntensity` trên StadiumEnvironment. Ánh sáng phủ sân và vạch, khán đài giữ tối hơn sân.
- Dùng vật liệu Simple Lit cho 12 vật liệu nền, sân, vải, cây và biển hiệu. Giữ mesh, albedo, UV và chi tiết normal; normal RGB của glTF được chuyển sang texture Normal Map đúng định dạng Unity. Ghế có clearcoat và các vật liệu kim loại giữ shader hiện có. Vật liệu nhân vật và vợt premium không thay đổi.
- Giảm shadow caster ở ghế/cây/biển trang trí. Giữ hình học sân, bóng đổ nhân vật và lưới. SSAO chạy nửa độ phân giải; camera sân bỏ bản sao màu opaque không dùng.
- Thêm `MatchRenderBudget`: tự điều chỉnh độ phân giải 3D, MSAA tối đa 2, bóng mặt trời tối đa 2048, atlas đèn phụ tối đa 1024, khoảng cách bóng tối đa 30 m. Màn hình lớn được giới hạn theo lượng pixel của 1920×1080 trước khi điều chỉnh tiếp. Không đổi timestep, input hay engine luật.
- HUD của bốn scene chuyển sang Overlay để chữ và nút giữ độ phân giải màn hình. Cấu hình đồ họa đã lưu của người dùng vẫn giữ nguyên; rời scene sân sẽ trả lại cấu hình tương ứng của Menu.
- Trong Editor, chỉ giữ tối đa một tiến trình nhập asset khi Play để giảm áp lực bộ nhớ. Khôi phục số tiến trình trước đó khi về Edit mode, reload assembly hoặc đóng Editor. Cơ chế này không chạy trong bản game phát hành.

## Bài đo trước và sau

Unity 6000.6.2f1, Windows, RTX 3050 Laptop, i5-11400H, RAM 16 GB, Game View 1280×720. Cấu hình đã lưu: High, renderScale 1,25, MSAA 4, FPS 120, VSync bật. Trước sửa, sân dùng đầy đủ cấu hình đó; sau sửa, render budget giới hạn riêng phần 3D. Mỗi mẫu lấy 6 giây sau thời gian ổn định, giữ cả khung hình chậm trong thống kê.

| Scene | Trời | FPS trung bình trước | FPS trung bình sau | P95 sau (ms) | Khung >16,67 ms / tổng sau |
|---|---|---:|---:|---:|---:|
| BotMatch | Ngày | 60,10 | 144,00 | 7,05 | 1 / 865 |
| BotMatch | Đêm | 32,77 | 143,02 | 7,57 | 2 / 859 |
| Training | Ngày | 40,78 | 144,00 | 7,07 | 3 / 865 |
| Training | Đêm | 31,84 | 137,63 | 8,70 | 2 / 826 |
| Tutorial | Ngày | 36,21 | 140,83 | 8,09 | 0 / 846 |
| Tutorial | Đêm | 26,70 | 140,77 | 7,96 | 0 / 845 |
| MultiplayerCourt | Ngày | 49,73 | 136,68 | 11,13 | 4 / 821 |
| MultiplayerCourt | Đêm | 32,66 | 109,82 | 14,74 | 5 / 659 |

Tổng cộng 6.586 khung hình, **99,74% khung nằm trong ngân sách 60 FPS**. Không còn tình trạng duy trì 27–40 FPS trong bài đo cuối. Vẫn có 17 khung vượt 16,67 ms, nên chưa thể khẳng định mọi khung hình luôn trên 60 FPS.

BotMatch chạy rally bằng bot tự chơi; Training chạy bài Drive với animation và tiếp xúc bóng thực; Tutorial chạy diễn giải giao bóng; MultiplayerCourt dùng bốn prefab nhân vật thực đang di chuyển để đo tải render. **Multiplayer là bài đo render, chưa phải trận online giữa các thiết bị.**

Editor đã có lúc thiếu bộ nhớ khả dụng của Windows. Build kiểm tra độc lập thất bại với lỗi `Not enough memory resources are available` khi biên dịch script; chưa có kết quả FPS của bản phát hành. Các scene/source kiểm tra tạm đã gỡ và tên/cấu hình ứng dụng đã khôi phục. Không quy toàn bộ khung chậm còn lại cho thiếu RAM khi chưa đo được bản độc lập.

## Kiểm tra chức năng

- 230/230 EditMode tests: đạt, không có test thất bại.
- 63 kiểm tra hình học, ánh sáng, HUD: đạt. Biên sân, Kitchen, lưới và collider Multiplayer giữ khớp với luật.
- 19 kiểm tra render budget và UI: đạt. Nút nhận raycast, cài đặt mở/đóng giữa trận, HUD khôi phục và cấu hình đã lưu không đổi.
- Cả 12 bài Training chạy tới kết quả 3 sao bằng mô phỏng input/animation/tiếp xúc bóng trên hồ sơ QA.
- Tutorial đạt 32 kiểm tra, gồm các lỗi và 9 tổ hợp đánh bóng.
- Cảnh kết thúc BotMatch đạt 82 kiểm tra, gồm thắng/thua, chờ thông báo, camera, animation và nút điều hướng.

Kết quả JSON nằm trong `UnityWork/StadiumPerformance/`: `before-partial.json`, `final-optimized.json`, `ui-validation.json`. Ảnh ánh sáng tại `UnityWork/Stadium/BotMatch-night-play.png`. Các file QA không được đưa vào Assets của bản game.

## Tái tạo và bảo trì

`Tools/StadiumSetup.cs` dựng prefab và scene. Sau khi tái tạo, chạy `StadiumRenderSetup.Run` và `StadiumRenderSetup.Materials` để áp dụng layout HUD, thiết lập renderer và vật liệu. `StadiumValidation.Geometry` kiểm tra vị trí sân/đèn. `StadiumPerformanceBenchmark.Run` đo các mẫu FPS trong Play mode và bảo vệ hồ sơ người dùng bằng hồ sơ QA tạm; `StadiumRenderValidation.Run` kiểm tra UI và vòng đời cấu hình.

Giữ nguyên file GLB nguồn. Các asset phát sinh được lưu trong `Assets/LocalMatchContent/StadiumEnhanced` và dùng chung giữa các scene.
