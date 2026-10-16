# Nâng cấp UI các màn hình — 16/10/2026

Đã áp dụng hệ giao diện xanh đêm, viền cắt góc, điểm nhấn vàng/mint và font tiếng Việt đang dùng ở Menu/Music. Giữ các thao tác gameplay, kinh tế, điều khiển và lưu cài đặt hiện có.

| Màn hình | Thay đổi |
|---|---|
| ModeSelection | Bốn thẻ Đấu với bot, Đấu online, Luyện tập, Tạo phòng; Tutorial có lối vào riêng. Tạo phòng mở lựa chọn 1v1/2v2 và ô tham gia bằng mã. |
| Settings | Thanh danh mục có icon, trạng thái lựa chọn rõ; nền nội dung và nút áp dụng phân cấp; giữ chỉnh âm lượng, đồ họa, phím máy tính và bố trí cảm ứng. |
| PlayMultiplayer | Bỏ nút Custom. Phòng riêng có Đội A / VS / Đội B, hai hoặc bốn vị trí theo kiểu phòng, mã phòng, đổi đội, mời bạn và trạng thái kết nối/thử lại. Cửa sổ xác nhận tìm trận dùng cùng theme. |
| Shop | Thẻ Signature nổi bật, hai tab, ô tìm kiếm và thẻ sản phẩm mới; thẻ ngắn tự đổi sang bố cục ảnh bên trái để tránh chồng chữ. |
| Collection | Cùng hệ thẻ với Shop, giữ trạng thái sở hữu/chưa sở hữu/đang sử dụng và thao tác sử dụng skin. |
| Tutorial | Khung hướng dẫn, nhãn chương, gợi ý phím và các nút dùng cùng theme. Tiêu đề, nội dung và chuỗi phím có giới hạn co chữ cho màn hình hẹp. Giữ camera và mô phỏng bài học. |
| Replay | Thanh thông tin trận, dock điều khiển, đường tua mảnh, thời gian và tốc độ 0.5×/1×/2×; giữ khả năng đọc và tua bản ghi hiện có. |

Chi tiết skin vẫn dùng studio 3D tràn viền, xoay và zoom, tên/mô tả phía phải và thao tác mua/sử dụng phía dưới phải. Header và ví luôn nằm trên nền trình duyệt sản phẩm.

## Phòng riêng và lời mời

Luồng chủ phòng: Chọn chế độ → Tạo phòng → 1v1 hoặc 2v2 → Mời bạn bè → Sao chép mã.

Luồng khách: Chọn chế độ → Tạo phòng → nhập mã → Tham gia; có thể dùng Enter trong ô mã.

`RoomLaunchRequest` truyền ý định tạo/tham gia qua lần chuyển scene và chỉ tiêu thụ một lần. `PlayMultiplayerPresenter` khởi tạo danh tính trước, rồi gọi `IRoomService.SelectCustomAsync` hoặc `JoinAsync`. Giữ Unity Multiplayer Sessions, quyền chủ phòng, đổi đội, rời phòng và cơ chế hủy thao tác muộn hiện có. Luật khởi động phòng riêng hiện có vẫn được giữ: mỗi đội có ít nhất một người và không vượt số vị trí của kiểu phòng; chỉ chủ phòng được bắt đầu.

Cửa sổ mời hiển thị mã thật của Sessions, hướng dẫn người nhận và khóa các nút phía sau khi mở. Danh sách bạn bè của dự án hiện là `OfflineSocialDirectory`; bản này hỗ trợ mời bằng mã, chưa gửi thông báo lời mời trực tiếp đến các tài khoản trong danh sách demo.

## Hiệu ứng và chi phí

- Hiệu ứng xuất hiện trong 0,24 giây; ánh sáng quét khi trỏ/bấm trong 0,32–0,42 giây. Vùng bấm không bị dịch chuyển bởi hiệu ứng.
- Vệt sáng dùng 32 vertex, tắt khi kết thúc và có Canvas con để giới hạn việc dựng lại hình học.
- Dùng lại shader Arena Glass, icon vector và font hiện có. Không thêm texture, model, âm thanh hay particle system.
- Bốn material và asset theme có tổng dung lượng nguồn 5.784 byte, chưa tính mã/metadata. Đây không phải phép đo dung lượng bản build.
- Shop chỉ cập nhật safe area khi thay đổi. Đồng hồ phòng cập nhật tối đa 5 lần/giây khi cần hiện. Tutorial chỉ cập nhật chuỗi tiến độ và bố trí cảm ứng khi trạng thái thay đổi; Replay tách thanh tua khỏi phần tĩnh.
- Kiểm tra vi mô 10.000 vòng cập nhật của 6 component UI mới khi đứng yên ghi nhận 0 byte GC; đây chỉ là chi phí các hàm cập nhật, không phải FPS hay tổng chi phí dựng hình của game.

Theme: `My project/Assets/Resources/ScreenUiTheme.asset` và `My project/Assets/FrontEndContent/ScreenRefresh`. Các scene/prefab được sửa qua API Unity Editor bằng `My project/Tools/AnimeScreensSetup.cs`, không sửa YAML bằng tay. Công cụ này giữ binding hiện có; không chạy lại các công cụ tạo scene đời cũ để áp bố cục.

## Kiểm tra

- 7/7 kiểm thử ý định điều hướng phòng và 15/15 kiểm thử quy tắc phòng đạt.
- 245 kiểm tra UI/tương tác đạt ở 1280×720, 1920×1080, 2560×1080, 2400×1080 và 1024×768: vùng bấm, giới hạn safe area, chữ thẻ chế độ, các tab cài đặt, dữ liệu nháp, ô phím, chế độ cảm ứng, Shop/Collection, studio và tua/tốc độ Replay.
- 222 kiểm tra nội dung Tutorial đạt ở 3 độ phân giải; đã sửa tràn chữ ở màn hình 4:3. 8 kiểm tra camera/nhãn/cài đặt/điều khiển Tutorial hiện có cũng đạt.
- Phòng 1v1/2v2, quyền chủ phòng, khóa thao tác bận, tiêu thụ ý định một lần và chuẩn hóa mã đã kiểm tra bằng trạng thái giả lập. Thêm 18 kiểm tra bố cục phòng/cửa sổ mời ở 1024×768 và 2400×1080 đạt.
- Sau khi người dùng cho phép, đã kiểm thử thật trên Unity Gaming Services, môi trường `pickleverse-test`: tạo đúng 2 phòng 1v1/2v2 qua giao diện ModeSelection, xác nhận dung lượng 2/4 vị trí qua Sessions, làm mới dữ liệu từ máy chủ, mở cửa sổ mời và sao chép mã. Nút bắt đầu khóa khi chưa có đối thủ. Cả hai phòng đã được xóa khi quay lại, và không còn trong danh sách Sessions đã tham gia của tài khoản. Báo cáo đầy đủ ở `UnityWork/AnimeScreens20261016/live-room-validation.json`; ảnh thật có tiền tố `Preview/live-`.
- Ảnh chụp và báo cáo: `UnityWork/AnimeScreens20261016/`. Các báo cáo phân biệt kiểm tra UI giả lập với kết nối thật.

Đã hoàn tất kiểm thử tạo/xóa phòng thật sau khi được người dùng cho phép. Chưa kiểm thử hai máy cùng tham gia phòng và vào trận; phép thử được cho phép chỉ tạo/xóa hai phòng trên một máy. Chưa đo FPS trên các điện thoại vật lý; kiểm tra kích thước thực hiện bằng Game View của Unity Editor.

## Tham khảo thiết kế

- [VALORANT 12.00 — Lobby / Queue Select](https://playvalorant.com/en-gb/news/game-updates/valorant-patch-notes-12-00/): phân biệt chọn kiểu chơi và phòng, làm rõ lựa chọn và dùng điều hướng nhất quán. Bố cục/nhận diện của PickleVerse được thiết kế riêng, không sao chép tài nguyên Riot.
- [Fortnite — Sidebar and Game Menu](https://dev.epicgames.com/documentation/en-us/fortnite/exploring-the-sidebar-and-game-menu-in-fortnite-creative): tham khảo việc đặt thao tác tổ đội và bạn bè ở vị trí dễ tiếp cận; áp dụng vào cửa sổ mời trong ngữ cảnh phòng.
- [Unity — UI optimization tips](https://unity.com/how-to/unity-ui-optimization-tips): tắt raycast cho trang trí, giới hạn Canvas động, tránh dựng lại layout và animation liên tục khi đứng yên.
- UI Music hiện có của PickleVerse là tham chiếu trực tiếp cho màu sắc, icon vòng tròn, viền kim loại và phân cấp chữ của đợt nâng cấp.
