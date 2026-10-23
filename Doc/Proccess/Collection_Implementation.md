# Bộ sưu tập và trang bị vợt — 10/10/2026

## Luồng sử dụng

1. Mở `Assets/Scenes/UI/Menu.unity`, bấm **Bộ sưu tập**.
2. `Assets/Scenes/Collection.unity` hiển thị vợt mặc định **PICKLEVERSE**, **GEMSTONE** và **THOT**. Có ô tìm theo tên/bộ sưu tập và lưới cuộn khi catalog tăng.
3. Skin sở hữu có thumbnail sáng và trạng thái rõ ràng; skin chưa sở hữu chìm tối nhưng vẫn xem được 3D.
4. Bấm skin để xem tràn viền. Tên/mô tả, bố cục bên phải, kéo chuột/chạm xoay 360°, cuộn hoặc bấm +/− để zoom dùng chung với Shop.
5. **Sử dụng** chỉ bật với skin đã sở hữu; skin đang chọn hiện **Đang sử dụng**. Skin chưa sở hữu hiện nhãn và chỉ dẫn đến cửa hàng, không được trang bị.
6. Khi về đại sảnh hoặc vào trận, vợt hiển thị theo lựa chọn đã lưu. Nhân vật nữ tại Menu vẫn nhảy với tay không.

Cập nhật hiệu năng: luồng Menu → Shop/Collection giữ Menu ở trạng thái nghỉ và khôi phục khi quay về, tránh tải lại toàn bộ sân/nhân vật/UI. Chi tiết số đo và kiểm chứng ở [Lobby_Return_Performance.md](Lobby_Return_Performance.md).

Vợt mặc định lấy từ `Assets/Models/Paddle/Paddle.glb`, luôn sở hữu, không bán thêm trong Shop. Hai skin premium vẫn giữ nguyên giá và luồng mua. Mua skin không tự thay vợt đang chọn.

## Dữ liệu và cách tích hợp

- `ShopCatalog.defaultPaddle` chứa vợt mặc định; `paddles` vẫn chỉ chứa sản phẩm bán. `CollectionItems` hợp nhất hai nhóm để hiển thị trong Collection.
- Mỗi offer có `previewPrefab` cho studio và `gameplayPrefab` riêng đã căn theo hệ tọa độ vợt hiện tại. Các prefab dùng chung mesh/texture, không nhân đôi GLB.
- `PlayerEconomyState.equippedPaddle` lưu ID trong JSON ví hiện tại `PKV.Economy.Demo.v1`. Wallet cũ giữ nguyên số dư, quyền sở hữu và receipt; lựa chọn rỗng/không còn hợp lệ trở về `paddle.default`.
- `IPlayerEconomy.EquipPaddleAsync` kiểm tra ID/quyền sở hữu và ghi lựa chọn trước khi phát sự kiện thay đổi. Ghi thất bại không thay đổi trạng thái trong bộ nhớ. Đổi skin không trừ tiền.
- `PaddleAppearance` thay model con, giữ nguyên socket, điểm chạm bóng, collider, animation và kích thước tính luật của vợt gốc. Renderer gốc được phục hồi khi chọn mặc định. Khi animation ẩn toàn bộ vợt, skin cũng được ẩn theo.
- `EquippedPaddle` đọc lựa chọn và nghe sự kiện ví; dùng ở vợt trên sân Menu và vợt người chơi trong BotMatch. Chế độ local mới có thể dùng `EquippedPaddle.Attach(actor.paddle)`.
- Online: chủ nhân gửi ID qua owner-only ServerRpc; server kiểm tra ID có trong catalog, lưu `NetworkPlayer.PaddleSkin`, mọi bản sao cập nhật model từ NetworkVariable. Nhân vật nam và nữ dùng cùng hệ căn chỉnh. Protocol tăng **3 → 4** vì layout dữ liệu mạng thay đổi; các máy chơi cùng nhau cần cùng bản build mới.
- Inventory vẫn là prototype local như Shop hiện tại. Khi có database, thay adapter `IPlayerEconomy`, đưa kiểm tra quyền sở hữu vào server trước khi chấp nhận RPC. Chưa thêm chức năng nạp tiền.

Scene Training cũ đang thiếu nhiều script và chưa có nhân vật MatchActor hoạt động; thay đổi này tích hợp với các trận BotMatch/online hiện có, không xây lại Training.

## Hiển thị premium ở Menu

Vợt trên sân dùng skin đã chọn, tăng nhẹ kích thước trưng bày và có nguồn sáng bổ sung nhỏ. Gemstone dùng vật liệu ánh kim/đá quý với glint vừa phải. Shader dùng target 3.5, biến thể ánh sáng ở fragment stage và hỗ trợ bóng của đèn bổ sung. Đã sửa lỗi biên dịch FXC thiếu bộ nhớ từng làm vợt hồng ở biến thể ban đêm.

## Kiểm chứng

- **92/92 EditMode tests** đạt, gồm 8 trường hợp mới về trang bị, wallet cũ, skin chưa sở hữu, lưu/đọc lại và lỗi lưu.
- **30/30 kiểm tra runtime xuyên scene** đạt: trạng thái sáng/tối, preview/zoom/360°, nút trang bị, tìm kiếm, sở hữu mới, Menu, BotMatch, giữ điểm chạm/socket, nhân vật nam và mô phỏng rally với skin premium. Shop giữ tab Signature và hai sản phẩm bán.
- **9/9 kiểm tra NGO host cục bộ** đạt: owner RPC, cập nhật NetworkVariable, loại ID lạ, đổi bên, nhân vật nữ/nam, protocol và bảo toàn ví thật. Chưa chạy hai client Internet.
- Collection và Shop có đúng một EventSystem, GraphicRaycaster và không có Missing Script; Collection có trong danh sách build chung và profile MultiplayerWindows.
- Đã xem ảnh Collection 16:9, chi tiết tràn viền, cận cảnh grip nữ/nam và Menu ngày/đêm. Shader Gemstone không còn thông báo lỗi sau khi render ban đêm. Chưa kiểm thử trên điện thoại thật.
- Kiểm thử dùng storage giả, trả lại provider gốc; không mua hoặc đổi lựa chọn trong ví thật.

QA: `UnityWork/Collection/validation.json`, `network-validation.json`, `Images/`. Helper: `My project/Tools/CollectionSetup.cs`, `CollectionValidation.cs`, `PaddleNetworkValidation.cs`, `CollectionVisualProbe.cs`. Mọi scene/prefab/catalog được tạo/lưu bằng API Unity.
