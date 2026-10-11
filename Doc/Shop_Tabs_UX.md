# Shop: Signature, toàn bộ catalog và màn xem 3D

Cập nhật ngày 10/10/2026 tại `My project/Assets/Scenes/Shop.unity`.

## Luồng giao diện

1. Menu → Cửa hàng mở **Signature / Thịnh hành** mặc định.
2. Banner lớn hiển thị vật phẩm mới nhất. Bên dưới là tối đa ba vật phẩm ra mắt gần đây tiếp theo.
3. **Tất cả sản phẩm** hiển thị toàn bộ catalog bằng lưới cuộn. Có tìm theo tên/bộ sưu tập và trạng thái không có kết quả.
4. Chọn banner hoặc bất kỳ thẻ nào mở màn chi tiết riêng, có model 3D, mô tả, sở hữu và các lựa chọn mua.
5. Kéo chuột hoặc chạm để xoay 360°. Cuộn hoặc nút **+ / −** để zoom gần/xa. Có Đặt lại góc và Xoay / Dừng.
6. Nút Quay lại hoặc Esc từ chi tiết trở về đúng tab và giữ vị trí cuộn. Esc/nút Đại sảnh ở trang duyệt trở về Menu. Nếu hộp xác nhận mua đang mở, Esc hủy xác nhận trước.

Màn duyệt dùng thumbnail render từ model. Geometry studio chỉ được camera hiển thị khi xem chi tiết, giúp giảm chi phí render lúc duyệt catalog. Preview chọn model theo ID sản phẩm, tạo instance khi cần; không bắt buộc tạo sẵn model cho mọi skin.

## Bố cục chi tiết tràn viền

Màn chi tiết đã đổi thành không gian 3D phủ toàn viewport, bỏ khung preview và đường viền cũ. Vợt nằm giữa màn hình, lớn hơn; tên/bộ sưu tập/mô tả nằm ở giữa sát bên phải. Nút mua hoặc trạng thái đã sở hữu nằm ở góc phải dưới. Header giữ nút Đại sảnh bên trái và hai thanh tiền bên phải. Các nút xoay/zoom/đặt lại góc nằm phía dưới bên trái.

Camera UI được chuyển sang studio khi mở chi tiết, giữ đúng tỷ lệ hình học theo màn hình thay vì kéo giãn RenderTexture. Quay lại tab sẽ khôi phục camera duyệt; zoom được reset đúng để không tăng/giảm kích thước ngoài ý muốn sau nhiều lần mở. Nút Đại sảnh trên header trở về Menu trực tiếp, còn nút quay lại nội dung/Esc vẫn trở về tab trước.

Đã kiểm tra thêm **14/14 trường hợp** về camera fullscreen, raycast kéo xoay ở giữa màn hình, vị trí thông tin/nút mua, zoom/quay lại nhiều lần, hộp xác nhận và không đổi ví. Ảnh 16:9/4:3 và kết quả ở `UnityWork/ShopFullScreen`.

## Cách thêm skin mới

Trong `Assets/Resources/Shop/Shop Catalog.asset`, thêm một entry vào danh sách sản phẩm:

- ID duy nhất và ổn định; tên, bộ sưu tập, mô tả.
- Giá Tokenpad/Elite theo chính sách; giá 0 nghĩa là không hỗ trợ loại tiền đó.
- Prefab preview đã căn giữa, thumbnail.
- **releaseOrder:** số lớn hơn là sản phẩm mới hơn.

Signature sắp giảm dần theo releaseOrder, không dựa vào vị trí của entry trong danh sách. Giá trị bằng nhau giữ thứ tự catalog ổn định. Hiện Gemstone = 2, THOT = 1; skin tiếp theo có thể dùng 3. Tab Tất cả không giới hạn hai sản phẩm và tự cập nhật số lượng/thẻ theo catalog.

Tên tab “Thịnh hành” hiện là khu vực giới thiệu sản phẩm mới, chưa dùng thống kê độ phổ biến từ backend. Đây không phải lịch xoay shop theo 24 giờ.

Lưới thay đổi 2–4 cột theo bề rộng viewport, tự tăng chiều cao nội dung và có scrollbar khi cần. Quay lại chi tiết không dựng lại lưới nên giữ trạng thái tìm kiếm và vị trí đang xem.

## Cơ sở UX từ VALORANT

Áp dụng sự phân cấp giữa khu vực featured nổi bật và danh sách cosmetic, cùng hành trình từ xem vật phẩm đến xác nhận sở hữu. PickleVerse vẫn dùng palette tối/mint/gold và ví tiền hiện có.

- [VALORANT Store](https://support.riotgames.com/en-us/valorant/store/guide-to-the-valorant-store): featured cosmetic và các nhóm tiền theo loại vật phẩm.
- [VALORANT 7.03](https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-7-03/): phản hồi/điều hướng rõ ràng sau khi sở hữu item.

Hai tab và màn chi tiết riêng là thiết kế theo yêu cầu PickleVerse, không khẳng định VALORANT có đúng cùng cấu trúc catalog này.

## Kiểm chứng

- **84/84 EditMode tests** đạt, gồm kiểm tra thứ tự mới nhất/giá trị bằng nhau/catalog rỗng cùng các kiểm thử giao dịch trước đó.
- **23/23 kiểm tra UI** đạt: Signature mặc định, camera nghỉ khi duyệt, chi tiết 3D, xoay 360°, drag, zoom, tìm kiếm, mask, quay lại, mua từ chi tiết và không đổi ví thật.
- Catalog giả lập **30 sản phẩm**: hiển thị đủ, cuộn được, Signature chọn đúng sản phẩm mới nhất, chọn entry bất kỳ mở đúng model, quay lại giữ vị trí; đổi tab không nhân đôi thẻ.
- Catalog giả lập được phục hồi; cửa hàng thật vẫn chỉ có hai skin người dùng cung cấp.
- Kiểm tra ảnh 16:9 và 4:3; không có Missing Script mới trong Shop. Chưa kiểm tra trên điện thoại thật.

Script nâng cấp có sao lưu scene trước khi đổi cấu trúc: `Tools/ShopTabsSetup.cs`. Không chạy lại script Upgrade khi tab đã tồn tại; chỉnh scene/component hiện có để giữ bố cục.

Kết quả và ảnh: `UnityWork/ShopTabs/editmode-tests.json`, `validation.json`, `Images`. Các component chính: ShopBrowser, ShopProductCard, ShopScreen và PaddlePreview.
