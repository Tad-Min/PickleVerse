# Luồng chọn chế độ và thiết lập trận bot

Ngày cập nhật: 08/10/2026.

Scene mới: `My project/Assets/Scenes/HomeScene/ModeSelection.unity`.

## Cách chơi

1. Mở `Assets/Scenes/HomeScene/Menu.unity`, bấm Play trong Unity.
2. Nhấn nút **Play** của Home để mở **Chọn chế độ**.
3. Chọn **Đấu với bot**.
4. Chọn **Dễ / Vừa / Khó** và **Ban ngày / Ban đêm**. Ảnh sân bên trái đổi theo bối cảnh; lựa chọn có viền sáng và nhãn **ĐÃ CHỌN**.
5. Nhấn **VÀO SÂN**. BotMatch nhận thiết lập, chạy cinematic sẵn có và bắt đầu lượt giao bóng.

**Multiplayer** và **Training** có thẻ riêng, nhãn **Sắp có** và chưa cho mở từ màn hình này. Nút **Quay lại** hoặc Esc đi từ thiết lập bot về danh sách chế độ, rồi về Home. Bàn phím dùng navigation/Submit của Input System UI; màn hình cũng nhận thao tác chuột và cảm ứng.

Mặc định lần đầu là **Vừa / Ban ngày**. Khi xác nhận Vào sân, lựa chọn lưu cục bộ trong PlayerPrefs (`PKV.Bot.Difficulty`, `PKV.Bot.Night`) và được dùng lại cho các lượt sau. Chơi lại trong BotMatch giữ cùng thiết lập, chạy lại cinematic. Để đổi thiết lập, dùng **Chọn chế độ** ở bảng pause/kết thúc trận.

Các nút chọn độ khó và ngày/đêm đã được gỡ khỏi hierarchy BotMatch. Nhãn độ khó đang dùng vẫn hiển thị trong HUD để người chơi biết thiết lập trận hiện tại. Nếu mở riêng BotMatch trong Editor, scene dùng lựa chọn đã lưu và vẫn có bảng Vào sân cơ bản.

## Bố cục và tham khảo UX

- Trang một dùng ba thẻ chế độ lớn, mô tả ngắn, nhãn trạng thái rõ; thẻ bot là đường chơi hiện có.
- Trang hai đặt ảnh sân và luật tóm tắt bên trái, độ khó/bối cảnh bên phải; một nút chính Vào sân ở cuối.
- Chỉ hiện thiết lập liên quan sau khi chọn bot; tách bước chọn chế độ khỏi bước xác nhận trận.
- Giữ palette xanh sân/teal của game, nền tối để dễ đọc, accent lime cho lựa chọn và hành động chính.
- Canvas scale theo màn hình, có vùng an toàn, Layout Group cho thẻ và lựa chọn độ khó; ảnh xem trước giữ tỷ lệ và crop khi khung đổi kích thước.
- Có phản hồi hover/pressed/focus, chuyển trang fade ngắn và lớp thông báo khi tải sân; chặn nhấn Vào sân nhiều lần.

Nguồn tham khảo thiết kế: [Rocket League Play Menu](https://www.rocketleague.com/news/first-look-play-menu-changes-coming-to-rocket-league) tổ chức các nhóm chơi thành card/submenu; [Fortnite Discover](https://dev.epicgames.com/documentation/fortnite/exploring-discover-in-fortnite-creative) có bước chọn thẻ, xem thông tin và đi tới lobby trước khi Play. Việc áp dụng hai bước gọn cho PickleVerse là quyết định thiết kế của màn hình này, không sao chép asset của các game đó.

## Thành phần triển khai

| Thành phần | Vai trò |
|---|---|
| `Assets/Scripts/FrontEnd/ModeSelectionMenu.cs` | Chuyển trang, lựa chọn, tóm tắt, Back/Esc, focus, safe area, tải sân |
| `Assets/Scripts/LocalMatch/BotMatchLaunchSettings.cs` | Lưu thiết lập local và tín hiệu bắt đầu trận từ front-end |
| `Assets/Scripts/Multiplayer/MenuPlayMultiplayer.cs` | Giữ component/listener Play đang có của Home, đổi đích sang ModeSelection |
| `BotMatch.cs` / `MatchEnvironmentLighting.cs` | Nhận mức khó/bối cảnh; tự bắt đầu cinematic khi được gọi từ màn hình mới |
| `MatchHud.cs` | Bỏ picker trong trận và thêm đường về chọn chế độ |
| `Assets/FrontEndContent` | Hai ảnh sân từ BotMatch và font Vietnamese SDF riêng cho màn hình |
| `Tools/ModeSelectionSetup.cs` | Dựng scene/asset và cập nhật scene bằng Unity Editor API; nằm ngoài runtime Assets |
| `Tools/ModeSelectionValidation.cs` | Kiểm tra luồng thật qua Button pointer-click trong Play Mode; nằm ngoài runtime Assets |

Menu, ModeSelection và BotMatch đã được thêm vào đầu danh sách scene của Global Build Settings và profile `MultiplayerWindows`. Các scene multiplayer trong profile vẫn có ở phía sau. Scene TrainingUI bị thiếu từ trước vẫn cần xử lý riêng nếu dùng danh sách build global có bật nó; đợt này chưa triển khai Training.

## Kiểm tra

**22/22 kiểm tra luồng đạt** trong Editor Play Mode, qua listener của Button và scene load thực:

- Home Play → chọn chế độ → thiết lập bot.
- Thẻ Multiplayer/Training không mở scene.
- Ba mức độ khó và hai bối cảnh cập nhật đúng dấu chọn/ảnh.
- Back về danh sách chế độ và giữ lựa chọn đang thiết lập.
- Khó/Ban đêm truyền sang BotMatch, bốn spotlight bật và sun tắt.
- Cinematic tự bắt đầu, chạy hết và trở về trạng thái giao bóng.
- Không còn picker độ khó/bối cảnh trong BotMatch.
- Quay về chọn chế độ; thiết lập đã xác nhận được đọc lại.
- Dễ/Ban ngày truyền sang BotMatch, sun bật và spotlight tắt.
- Chơi lại giữ thiết lập, chạy cinematic.
- Scene chọn chế độ có một EventSystem, tham chiếu hợp lệ và không Missing Script.

Đã kiểm tra hình ở 1280×720, 960×720 và 1920×810; chỉnh ảnh để không bị kéo giãn ở tỷ lệ 4:3. Font riêng có đủ glyph cho các nhãn hiện có; không phát hiện chữ tràn trong trang bot được kiểm tra. Kết quả và ảnh lưu tại `UnityWork/ModeSelection/validation.json` và `UnityWork/ModeSelection/Images`.

Các cảnh báo Missing Script từ prefab UI cũ của Home vẫn xuất hiện khi tải Home; luồng Play đã qua kiểm tra và scene ModeSelection mới không có Missing Script. Chưa chạy build hoặc nghiệm thu trên điện thoại thật trong đợt này.

Backup hai scene trước thay đổi ở `UnityWork/ModeSelection/Before`. Báo cáo audit tính năng trước đó là ảnh chụp hiện trạng tại thời điểm khảo sát, không tự cập nhật theo luồng mới này.
