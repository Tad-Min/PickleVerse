# Tối ưu quay lại đại sảnh từ Shop/Bộ sưu tập — 10/10/2026

## Nguyên nhân và cách sửa

Trước đây `ShopScreen.GoLobby` và nhánh thoát của `Back` gọi `SceneManager.LoadScene("Menu")`. Unity tải lại toàn bộ sân, nhân vật, rig phụ, UI, sky và thực hiện dọn tài nguyên của luồng tải scene Single. Đo trong Editor ghi nhận khung hình đầu khi quay về dài **525,16 ms từ Shop** và **517,26 ms từ Collection**.

`LobbyNavigation` thay luồng Menu → Shop/Collection bằng tải **additive bất đồng bộ**. Chỉ giữ một Menu ở trạng thái nghỉ:

- Tắt camera, đèn, renderer, canvas, input, animation, physics/collider và các script mô phỏng của Menu khi đang ở Shop/Collection.
- Giữ GameObject UI/TextMeshPro và dữ liệu nhân vật để không dựng lại text mesh, rig hoặc vật liệu sky khi quay về.
- Khi bấm Đại sảnh, khôi phục chính scene Menu trước đó, đúng ánh sáng và trạng thái component. Ví và vợt đã chọn được đọc lại khi component hoạt động trở lại.
- Hiển thị Menu trước, rồi unload scene Shop/Collection. Không chạy đợt tải lại Menu và dọn toàn bộ tài nguyên ngay trên khung hình quay lại.
- Khóa lệnh chuyển cảnh lặp trong khi đang chuyển. Có đường tải Menu bất đồng bộ khi chạy Shop trực tiếp trong Editor mà chưa đi qua Menu.
- Khi rời đại sảnh bằng luồng scene Single, Menu nghỉ được giải phóng và cache được xóa. Không giữ sân Menu trong trận đấu.

Không giảm render scale, MSAA, chất lượng shadow, texture hoặc mức đồ họa đã chọn của người chơi. Không sửa model, animation, luật đấu hay dữ liệu ví thật.

## Kết quả đo

Đo trong **Unity 6000.6.2f1 Editor**, giữ cấu hình đang dùng: High, render scale **1,25**, MSAA **4**, VSync bật. Các số đo bao gồm chi phí Editor, không thay thế benchmark bản build trên từng thiết bị.

| Luồng quay về | Khung hình lớn nhất trước tối ưu | Khung hình lớn nhất sau tối ưu — lần đo cuối | Khung hình >25 ms trong 450 ms sau bấm nút |
|---|---:|---:|---:|
| Shop → Menu | 525,16 ms | 23,48 ms | 0 |
| Collection → Menu | 517,26 ms | 12,60 ms | 0 |

Trong cửa sổ chuyển cảnh đã đo, không có mẫu dưới 40 FPS. Lần đo trước đó với mã tối ưu ghi nhận tối đa 13,15/12,13 ms. FPS thực tế vẫn phụ thuộc phần cứng, tải nền và Editor; kết quả khẳng định đã loại bỏ khung giật khoảng nửa giây do tải lại Menu.

Ở đoạn Menu đứng ổn định sau đó, lần đo cuối có một mẫu 26,25 ms khi thử chuyển cưỡng bức từ ban ngày sang ban đêm. Mẫu này nằm ngoài cửa sổ quay về; không phải tải lại scene. Chu kỳ sky tự nhiên vẫn được giữ nguyên.

## Kiểm chứng chức năng

**40/40 kiểm tra runtime đạt** qua nhiều vòng Shop/Collection:

- Dùng lại đúng instance Menu; lúc nghỉ không render sân hoặc chạy mô phỏng nhân vật.
- Chỉ một camera, AudioListener và EventSystem đang hoạt động.
- Xem chi tiết 3D rồi quay về; sky, đèn và animation được phục hồi.
- Skin Gemstone, mặc định và THOT hiển thị đúng sau khi chọn trong Collection; renderer vợt gốc/skin không bị chồng hoặc mất.
- Giao dịch ví giả cập nhật ngay ở Menu khi quay lại; ví thật và cấu hình đồ họa không bị thay đổi.
- Cài đặt vẫn nhận đúng host đại sảnh.
- Menu được dọn khi sang ModeSelection; chạy Shop trực tiếp vẫn quay được về Menu.
- Không có lỗi biên dịch/runtime mới trong phiên kiểm tra. Các cảnh báo Missing Script của prefab Menu cũ vẫn là vấn đề có sẵn.

**92/92 EditMode tests** của dự án cũng đạt sau thay đổi. Kết quả đầy đủ lưu tại `UnityWork/LobbyPerformance/editmode-tests.json`.

Mã chính: `My project/Assets/Scripts/Shop/LobbyNavigation.cs`, `ShopLauncher.cs`, `ShopScreen.cs`. Có marker profiler `Lobby.Return.Lighting` và `Lobby.Return.Components` để đo tiếp trên bản build.

Báo cáo ở `UnityWork/LobbyPerformance/before.json`, `after-components.json`, `final.json`, `navigation-validation.json`. Helper đo/kiểm chứng: `My project/Tools/LobbyReturnBenchmark.cs`, `LobbyNavigationValidation.cs`.
