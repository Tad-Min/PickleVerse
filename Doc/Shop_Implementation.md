# Shop, skin vợt và ví tiền demo

Ngày triển khai: 10/10/2026.

## Cách mở và sử dụng

Scene `My project/Assets/Scenes/Shop.unity` đã được dựng từ scene placeholder cũ và thêm vào danh sách build. Nút **Cửa hàng** tại `Assets/Scenes/UI/Menu.unity` mở Shop; **Đại sảnh** hoặc Esc quay về Menu.

Shop đã cập nhật thành hai tab: **Signature / Thịnh hành** mở mặc định, có banner sản phẩm mới nhất và dãy ra mắt gần đây; **Tất cả sản phẩm** dùng lưới cuộn và ô tìm kiếm. Chọn sản phẩm ở bất kỳ tab nào để mở màn chi tiết 3D riêng, kéo chuột/chạm để xoay, cuộn hoặc nút +/− để zoom; nút Xoay / Dừng điều khiển vòng quay tự động. Quay lại giữ tab và vị trí cuộn. Khi mua, hộp xác nhận hiện loại tiền, giá và số dư sau mua. Hủy không trừ tiền. Skin đã sở hữu được đánh dấu và không thể mua lần nữa. Chi tiết cập nhật tại `Shop_Tabs_UX.md`.

Hai thanh số dư ở phía trên của Menu và Shop dùng đúng ảnh Tokenpad/Elite do người dùng cung cấp, biểu tượng ở cuối bên phải mỗi thanh. Ánh sáng quét chéo qua biểu tượng mỗi **1 giây**, trong khoảng 0,38 giây rồi nghỉ đến chu kỳ tiếp theo. Không có nút hoặc luồng nạp tiền.

## Catalog và giá mẫu

| Skin | Tokenpad | Elite Token |
|---|---:|---:|
| GEMSTONE | Không hỗ trợ | 650 |
| THOT | 900 | 250 |

THOT mua bằng **một trong hai loại tiền**, không yêu cầu cả hai. Mua bằng loại này thì loại còn lại không bị trừ.

Ví mới khởi tạo **1.000 Tokenpad và 1.000 Elite**, mỗi loại một lần. Đây là lựa chọn demo cho yêu cầu số dư ban đầu; vào lại scene hoặc mở lại game không cấp thêm 1.000. Catalog, giá, số dư khởi tạo và mức thưởng nằm ở `Assets/Resources/Shop/Shop Catalog.asset`; UI đọc giá từ cùng catalog.

Quyền sở hữu skin được lưu trong ví cục bộ. Scene **Collection** đã bổ sung thao tác **Sử dụng / Đang sử dụng**, vợt mặc định và đồng bộ skin lên vợt ở Menu, BotMatch và nhân vật online. Shop vẫn giữ thao tác mua/xem trước. Chi tiết cập nhật ở [Collection_Implementation.md](Collection_Implementation.md).

## Thưởng Tokenpad

| Trận hoàn thành | Thắng | Thua |
|---|---:|---:|
| Đấu bot | +100 | +50 |
| Online | +150 | +75 |

- Chỉ thưởng sau khi **kết thúc cả trận**, không cộng theo điểm hoặc từng game.
- Đấu bot tự động dùng để kiểm thử không cấp thưởng. Rời trận trước khi kết thúc không cấp thưởng.
- Online chỉ đủ điều kiện khi trận kết thúc theo số game thắng; forfeit do rời/mất kết nối không cấp thưởng trong prototype này.
- Mỗi trận có receipt ID; callback kết quả lặp hoặc reconnect nhận lại kết quả không cộng hai lần.
- Thưởng trận **chỉ tăng Tokenpad**. Elite chỉ có số dư demo ban đầu và bị trừ khi mua, chưa có API cấp Elite/nạp tiền.

## Giảm dung lượng model

Hai GLB đã được ghi lại đúng đường dẫn người dùng yêu cầu. Bản trước tối ưu ở `UnityWork/Shop/BeforeModels`.

| Model | Trước | Sau | Giảm |
|---|---:|---:|---:|
| paddle-skin-gemstone-model.glb | 12.556 MB | 3.953 MB | 68,52% |
| paddle-skin-thot-model.glb | 14.867 MB | 6.650 MB | 55,27% |

MB trong bảng dùng 1.000.000 byte. Tối ưu trực tiếp dữ liệu GLB; không decimate hay sửa hình học. Bộ đệm geometry, vị trí, normal, UV, index, cấu trúc mesh/node đều được giữ nguyên. Gemstone vẫn có 11.662 đỉnh / 18.080 tam giác; THOT 11.525 đỉnh / 19.447 tam giác.

Base color giữ **2048×2048**, mã hóa JPEG chất lượng 95, không subsample màu. Normal THOT giữ 2048×2048 và PNG không mất dữ liệu pixel. Hai map metallic/roughness từ 4096 xuống 2048 để giảm dung lượng phần vật liệu; điều này không đổi silhouette. Texture có thay đổi do nén màu/lấy mẫu map vật liệu, vì vậy không gọi đây là phép nén hoàn toàn lossless. Đã kiểm tra trực tiếp cả hai model trong studio Unity.

## Đá quý và studio

Gemstone dùng material/shader riêng, giữ base color và metallic/roughness của model. Shader nhận diện vùng đá xanh/đá trắng trong atlas, tạo phản xạ theo góc nhìn và ánh sáng, cộng glint rất nhỏ tại các vùng đó. `_Glitter = 0,24`; không dùng bloom mạnh hoặc làm sáng toàn bộ vợt.

Studio có camera render vào RenderTexture riêng và ánh sáng key/fill/rim. Chỉ một model được bật tại một thời điểm. Model được căn giữa và scale đồng đều cho studio; không sửa mesh gốc. Thumbnail được render từ chính model, không phải ảnh minh họa thay thế. Camera studio không có AudioListener; scene có một AudioListener và một EventSystem.

## Nền móng dữ liệu

Assembly `PickleVerse.Shop` chứa catalog, adapter ví, UI và preview; không phụ thuộc LocalMatch/Multiplayer. Các chế độ chơi gọi dịch vụ thưởng khi hoàn tất trận.

- `IPlayerEconomy`: đọc ví, mua item theo ID/loại tiền/request ID, cấp thưởng theo receipt trận.
- `LocalPlayerEconomy`: adapter demo, dùng `IEconomyStorage` để tách logic giao dịch khỏi nơi lưu.
- `LocalEconomyStorage`: PlayerPrefs JSON tại khóa `PKV.Economy.Demo.v1`.
- Một lần mua ghi **số dư đã trừ + quyền sở hữu + receipt** trong cùng JSON. Nếu ghi thất bại, trạng thái trong bộ nhớ không đổi.
- Snapshot trả cho UI là bản sao; UI không tự sửa số dư. WalletBar nghe sự kiện thay đổi để đồng bộ ở cả Menu và Shop.
- Mua lại request ID cũ không trừ thêm tiền; dùng request ID đó cho item/loại tiền khác bị từ chối.

Ví demo hiện dùng chung cho thiết bị, chưa tách theo tài khoản đăng nhập. Khi có backend, thay adapter bằng `PlayerEconomy.Configure(...)`, gắn với danh tính người chơi, tải catalog/giá có thẩm quyền và thực hiện giao dịch/thưởng ở server. Client chỉ gửi item ID, loại tiền và request ID; không được tự quyết định giá, số dư hoặc kết quả thưởng trong bản production. PlayerPrefs chỉ phù hợp cho demo hiện tại.

Không cài package Economy/IAP, không triển khai cloud, không gọi cổng thanh toán và không tạo chức năng nạp tiền.

## UI/UX áp dụng

- Tham khảo cách làm nổi bật cosmetic và xem trước item của Fortnite/VALORANT; dùng hai bộ sưu tập với màu nhận diện riêng, model lớn, background tối và một vùng mua rõ ràng.
- Tách chọn/xem sản phẩm khỏi xác nhận mua. Không dùng countdown giả, số lượng giả hoặc khuyến mãi không có dữ liệu.
- Hộp xác nhận hiển thị số dư còn lại; mặc định focus vào Hủy, khóa điều hướng trong hai nút xác nhận để tránh thao tác mua vô ý bằng bàn phím.
- Hiển thị thiếu tiền và trạng thái đã sở hữu; điều khiển bị khóa khi đang xử lý giao dịch.
- Layout dùng CanvasScaler và safe area; đã xem 16:9 và 4:3. Tên thẻ sản phẩm co cỡ chữ để không chồng logo coin ở 4:3.

Nguồn tham khảo:

- [Fortnite Item Shop](https://www.fortnite.com/item-shop): thẻ cosmetic và xem item trong cửa hàng trong game.
- [VALORANT Store](https://support.riotgames.com/en-us/valorant/store/guide-to-the-valorant-store): cửa hàng cosmetic và loại tiền khác nhau theo nhóm vật phẩm.
- [VALORANT 7.03](https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-7-03/): trạng thái/điều hướng sau khi sở hữu vật phẩm.
- [Unity Virtual Shops](https://docs.unity.com/en-us/services/solutions/virtual-shops): tách catalog, số dư, inventory và giao dịch.

## Kiểm chứng và giới hạn

- **81/81 EditMode tests** hiện có, gồm 13 trường hợp kiểm tra ví/giao dịch mới, đạt.
- **19/19 kiểm tra xuyên scene**: Menu mở Shop, hai model và thumbnail, hai loại tiền, hủy/mua/xem 360°, kéo pointer, lưu/đọc lại, Menu phản ánh số dư, thưởng bot đúng một lần, adapter thưởng online chỉ cộng Tokenpad.
- Kiểm tra GPU: bật/tắt glint thay đổi 613 pixel của preview; bật/tắt sheen thay đổi 177 pixel trong ảnh UI kiểm thử. Xác nhận hiệu ứng thực sự render, không chỉ có giá trị material.
- Không có Missing Script trong Shop; shader được hỗ trợ trên Editor đang chạy; Shop có trong build scene list.
- Khi tải Menu, các prefab Collection/Career/HoverVisuals cũ vẫn ghi cảnh báo Missing Script. Luồng Menu → Shop đã qua kiểm tra; không sửa các prefab ngoài phạm vi cửa hàng trong đợt này.
- Kiểm thử dùng storage giả và trả lại provider gốc; ví demo thật vẫn 1.000/1.000 sau kiểm tra.
- Chưa kiểm thử trên điện thoại thật, chưa chạy hai client Internet để xác minh thưởng online đầu cuối, chưa đo bản build mobile. Luồng online đã tích hợp theo kết quả server hiện có, phần thưởng vẫn là adapter local demo.

Kết quả/ảnh ở `UnityWork/Shop`: `optimization.json`, `editmode-tests.json`, `flow-validation.json`, `visual-probe.json`, `Images`. Script chuẩn bị nằm tại `My project/Tools/OptimizeShopGLB.py`, `ShopSceneSetup.cs`, `ShopFlowValidation.cs` và `ShopVisualProbe.cs`.
