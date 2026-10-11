# Căn viền đĩa và tối ưu Chọn chế độ → Đại sảnh — 11/10/2026

## Viền đĩa nhạc

PNG có khoảng trong suốt không cân ở các cạnh. Ví dụ Echo: tâm vùng đĩa nhìn thấy ở khoảng `(0,499; 0,523)` trong ảnh, khác tâm khung `(0,5; 0,5)`. Khi xoay quanh tâm khung, đĩa lệch so với đường tròn đứng yên.

Đã đo vùng alpha của cả 4 sprite và lưu `MusicTrack.discBounds`. `MusicUI.FitRecord()` dùng tâm này làm pivot, đặt tâm phần đĩa nhìn thấy vào tâm `Disc Square`, và chuẩn hóa kích thước phần đĩa. Anchor tương đối giữ tâm đúng khi canvas đổi kích thước. PNG và nội dung artwork không bị sửa.

Viền dùng glyph `RecordFrame` riêng với một vòng tròn và các vạch nhỏ. Nhãn `LOBBY RADIO` được dời lên để không đè lên viền. Các vòng trang trí ở profile giữ nguyên.

Kiểm chứng: 4 bài × 4 góc xoay 0/90/180/270°. Sai lệch tâm dưới `0,0001` đơn vị world; khoảng hở theo hai trục khoảng 17–19 đơn vị canvas ở kích thước kiểm tra, chênh nhau dưới 2. Đã xem ảnh ở 16:9 và 4:3.

## Nguyên nhân khựng khi quay về đại sảnh

Trước đây `MenuPlayMultiplayer.Play()` và `ModeSelectionMenu.Back()` dùng tải scene Single. Mỗi lượt quay về tải lại sân, nhân vật, material sky, animation và UI; Menu không phải instance vừa rời đi. Phép đo ghi nhận khung hình lớn nhất trong cửa sổ quay về khoảng **397–443 ms**.

Đã mở rộng `LobbyNavigation` đang dùng cho Shop/Collection để hỗ trợ `OpenModes()`:

1. Rời Menu: giữ một scene đại sảnh đã tải; tạm dừng script, renderer, camera, đèn, collider và input trước khi kích hoạt ModeSelection.
2. Quay về: kích hoạt lại chính scene, instance nhân vật, sky và trạng thái đã giữ. Nhạc vẫn dùng player hiện có; widget chỉ hiện khi Menu là active scene.
3. Gỡ scene chọn chế độ sau khi Menu đã hiện. Không tải lại model hoặc làm đợt dọn tài nguyên toàn cục của Single load ở khung hình quay về.
4. Vào tutorial/trận đấu bằng Single load vẫn giải phóng đại sảnh tạm giữ. Bắt đầu trực tiếp từ ModeSelection trong Editor, không có Menu để giữ, vẫn dùng tải Menu bất đồng bộ khi quay về.

Không hạ chất lượng đồ họa, thay render scale, MSAA hoặc FPS/vsync của người chơi. Khi ở ModeSelection, đại sảnh được giữ trong bộ nhớ nhưng không render hay chạy mô phỏng.

## Kết quả đo

Đo 3 lượt trong Unity Editor, cùng cấu hình High / render scale 1,25 / MSAA 4 / FPS 120 / vsync bật; cố định ban ngày ở cả trước và sau để so sánh. Thời gian bao gồm chi phí Editor.

| Lượt | Khung hình lớn nhất lúc quay về trước sửa | Sau sửa | FPS trung bình ở Menu sau quay về |
|---|---:|---:|---:|
| 1 | 442,72 ms | 19,56 ms | khoảng 139 |
| 2 | 396,56 ms | 19,38 ms | khoảng 143 |
| 3 | 404,45 ms | 20,86 ms | khoảng 130 |

Đã loại bỏ các khung hình khựng hàng trăm ms trong luồng bình thường Menu → ModeSelection → Menu. Trong cửa sổ quay về sau sửa không có mẫu trên 25 ms, nhưng vẫn có một mẫu 20,86 ms; kết quả này không phải cam kết mọi khung hình trên mọi máy đều vượt 50 FPS. Menu 3D và trang chọn chế độ UI có tải render khác nhau.

Báo cáo gốc: `UnityWork/ModePerformance/before.json`, `after-cache.json`; progress, tiền tệ và thiết lập thật không bị thay đổi trong phép đo.

## Kiểm tra chức năng

- 24 kiểm tra navigation: tái sử dụng đúng instance; dừng renderer/đèn/script ở nền; một camera/listener/EventSystem; giữ bối cảnh đêm và animation; Back ở trang cấu hình bot; tutorial giải phóng cache; quay về được khi mở ModeSelection trực tiếp; dữ liệu thật giữ nguyên.
- 40 kiểm tra hồi quy Shop/Collection: tiếp tục tái sử dụng đại sảnh và cập nhật skin/tiền tệ đúng khi quay về.
- 21 kiểm tra UI hiện có: lựa chọn tutorial, widget nhạc, modal và liên kết scene vẫn hoạt động.
- 128/128 EditMode tests đạt.

Helper: `ModeReturnBenchmark.cs`, `ModeNavigationValidation.cs`, `RecordBoundsAnalysis.cs`, `RecordAlignmentSetup.cs`, `RecordAlignmentValidation.cs` trong `My project/Tools`. Ảnh/báo cáo kiểm tra ở `UnityWork`, không đưa vào Assets của game.
