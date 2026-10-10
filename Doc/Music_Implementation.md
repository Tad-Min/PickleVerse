# Nhạc nền và trình phát nhạc — 10/10/2026

## Cách sử dụng

Nhạc tự phát khi vào các màn giao diện. Tại Menu, bấm khung đĩa nhỏ ở góc dưới bên trái, ngay trên thanh điều hướng, để mở trình phát. Tên bài, nghệ sĩ và thể loại chạy chữ khi dài hơn khung. Ở các màn khác có biểu tượng đĩa nhỏ để mở cùng trình phát.

Trong trình phát có danh sách 4 bài, đĩa xoay, tên/nghệ sĩ, thể loại, thời gian đã phát và tổng thời lượng. Có nút **Trước**, **Phát/Tạm dừng**, **Sau**, chọn trực tiếp một bài và kéo thanh tiến độ. Hết bài tự phát bài tiếp theo, hết danh sách quay về bài đầu. Đĩa dừng xoay khi tạm dừng.

Nút **Âm lượng / Cài đặt** mở màn cài đặt hiện có. Thanh **Nhạc nền** áp dụng riêng cho nhạc, thanh **Hiệu ứng** áp dụng cho SFX; thanh tổng điều khiển cả hai. Esc đóng cửa sổ nhạc trước, không thoát trang đang xem.

| Scene | Nhạc nền | UI nhạc |
|---|---|---|
| Menu | Có | Đĩa nhỏ + tên bài chạy chữ |
| ModeSelection | Có | Biểu tượng đĩa |
| PlayMultiplayer — phòng chờ | Có | Biểu tượng đĩa |
| Shop / Collection | Có | Biểu tượng đĩa, kể cả khi xem skin tràn viền |
| BotMatch / MultiplayerCourt | Không | Ẩn |
| Training / SampleScene và scene khác | Không | Ẩn |

Mở Cài đặt dạng overlay giữ chính sách của scene nền: từ Menu vẫn có nhạc; từ trận đấu vẫn không có nhạc. Khi trở về màn giao diện sau trận, tiếp tục bài/vị trí đã lưu trong phiên. Chuyển giữa các trang giao diện không khởi động lại bài. Tạm dừng do người chơi vẫn được giữ khi đổi trang.

## Tài nguyên

| Bài / nghệ sĩ | Thời lượng nhập từ Unity | Ảnh đĩa |
|---|---:|---|
| Echo — Apollo On The Run | 2:50 | Đĩa Nhạc Apollo on the sun.png |
| Stars in the Sky — BEKSY. | 2:30 | Đĩa Nhạc Vũ Trụ Stars in the Sky.png |
| Taking It Slow — JVNA | 3:06 | Đĩa Nhạc Taking It Slow Future Bass NCS.png |
| Keep You — Rameses B | 2:42 | Đĩa Nhạc Keep you.png |

MP3 gốc trong `Assets/Audio/Sound` và PNG gốc trong `Assets/Audio/Đĩa_nhạc` được giữ nguyên. Import nhạc: **Streaming**, **Load In Background**, stereo **44.100 Hz**, Vorbis quality **0,8**, không preload toàn bộ âm thanh. Ảnh đĩa: Sprite, alpha, texture tối đa 512, không mipmap. Import được cấu hình bằng AudioImporter/TextureImporter trong Unity.

## UI/UX tham khảo và quyết định

Ảnh tham khảo của người dùng định hướng bố cục danh sách bên trái, đĩa ở giữa và thông tin/điều khiển dễ nhìn. Dùng màu tối và màu nhận diện từng đĩa phù hợp giao diện PickleVerse. Metadata luôn đứng yên khi đĩa quay để vẫn đọc được tên bài. Widget ở Menu nằm trên thanh điều hướng, tránh nhân vật và nút chơi.

Tham khảo [hướng dẫn đổi/tắt Lobby Music của Fortnite](https://www.epicgames.com/help/c-34254770/c-33726977/how-to-change-or-turn-off-lobby-music-in-fortnite-a18733187) cho việc để người chơi chọn bài và tạm dừng chủ động; [Fortnite Festival](https://www.fortnite.com/news/take-your-stage-in-fortnite-festival) mô tả bài nhạc dùng tại lobby. Không dùng lyrics hoặc thông tin bài chưa có trong tài nguyên. Bố cục đã xem ở 16:9 và 4:3.

## Cấu trúc triển khai

- `MusicLibrary`: catalog dữ liệu chứa bài, nghệ sĩ, thể loại, AudioClip, Sprite, màu nhận diện và prefab UI. Resource: `Assets/Resources/Audio/Front End Music.asset`.
- `MusicPlayer`: một instance dùng chung, tồn tại qua chuyển scene. Tối đa **2 nguồn stereo 2D** để crossfade khoảng **0,65 giây**, không tạo AudioListener. Không thay DSP buffer 256 hoặc pool SFX đang có.
- Chính sách dựa vào **active scene**, không dựa vào việc Menu đang được cache trong nền. Vào sân đấu dừng ngay cả hai voice, hủy phần tải/fade đang chờ và xóa clip khỏi nguồn phát. Không để đoạn crossfade tiếp tục trong trận.
- Chỉ tạo player lần đầu khi vào scene được phép; bắt đầu trực tiếp ở trận đấu không tự tạo/phát nhạc.
- `MusicUI`, `MusicTrackRow`: UI dùng uGUI/TMP, đĩa PNG người dùng cung cấp, thanh tiến độ và caption có RectMask2D. Canvas nhạc tách khỏi UI Menu/Shop để animation đĩa và marquee không dựng lại toàn bộ UI các scene.
- `FrontEndModalInput`: quyền bàn phím cho popup. ModeSelection và Shop không xử lý Esc điều hướng khi cửa sổ nhạc đang mở.
- Âm lượng nghe `SettingsRuntime.AudioLevelsChanged`, không tự sửa cài đặt người chơi khi chuyển scene. SFX vẫn dùng hệ thống hiện có.
- Không gọi cloud, tạo phòng multiplayer, mua vật phẩm, reset ví hoặc bổ sung cơ chế nạp tiền. Kiểm tra phòng/sân online vô hiệu hóa auto-start networking trong harness, chỉ kiểm tra audio/UI scene.

## Kiểm chứng

- **34/34 kiểm tra runtime**: 4 bài có sample âm thanh đầu ra; artwork/title đúng; đĩa xoay; marquee; pause/resume; seek; vòng playlist; âm lượng; Cài đặt; không restart qua Menu/Shop/Collection/ModeSelection/phòng online; không nhạc ở BotMatch và sân online; chỉ một listener; ví/cài đặt thật không đổi.
- **3/3 kiểm tra bổ sung**: Esc chỉ đóng nhạc tại Shop/ModeSelection; SFX vẫn hoạt động trong BotMatch khi nhạc đã dừng.
- **106/106 EditMode tests** đạt, gồm chính sách scene, vòng playlist, quyền input của popup và import stereo/streaming.
- Đo lại luồng quay về Menu với nhạc chạy: khung hình lớn nhất trong cửa sổ chuyển cảnh **14,10 ms từ Shop** và **12,81 ms từ Collection**, không có mẫu >25 ms tại cửa sổ đó. Đo trong Editor, giữ cấu hình đồ họa của người chơi; mẫu đổi cưỡng bức trời ngày/đêm vẫn có chi phí riêng của Menu.
- Không xác nhận bản build điện thoại hoặc phòng online với hai client qua Internet. Kiểm chứng âm thanh là nguồn AudioSource đang chạy trên PC trong Unity Editor.

Báo cáo: `UnityWork/Music/import-before.json`, `import-after.json`, `validation.json`, `input-validation.json`, `editmode-tests.json`, `Images/`. Hiệu năng: `UnityWork/LobbyPerformance/with-music.json`. Helper: `My project/Tools/MusicSetup.cs`, `MusicValidation.cs`, `MusicInputValidation.cs`.
