# Cinematic mở đầu BotMatch

Scene: `My project/Assets/Scenes/BotMatch.unity`.

Khi mở scene và bắt đầu Play, camera bay qua toàn cảnh sân, lần lượt quay người chơi nữ và đối thủ nam bước từ ngoài baseline vào vị trí thi đấu. Camera orbit quanh nhân vật để thấy cả sân và khán đài, sau đó chuyển mượt về góc camera người chơi. Chữ **“Trận đấu bắt đầu”** lướt từ trái vào giữa màn hình rồi ra bên phải và biến mất.

Thời lượng mặc định khoảng **10,95 giây**: toàn cảnh 0,85 s, mỗi nhân vật 4 s, trở về camera gameplay 1 s, chữ bắt đầu 1,1 s. Có thể bỏ qua bằng Enter, Space hoặc nút **Bỏ qua**. Nút Chơi lại cũng chạy đoạn mở đầu; reset phục vụ kiểm thử nội bộ vẫn có thể vào thẳng lượt giao.

Trong cinematic, input đánh bóng/di chuyển và đồng hồ giao bóng được khóa. Sau khi chữ biến mất, HUD chơi được phục hồi và bắt đầu lượt giao bóng bình thường. Bóng và marker quỹ đạo tạm ẩn trong hai cảnh bước vào sân.

## Clip đã tách

| Nhân vật | Clip độc lập | Duration |
|---|---|---:|
| Nữ | `Assets/LocalMatchContent/Clips/female/Entrance_Walk.anim` | 1,0 s |
| Nam | `Assets/LocalMatchContent/Clips/male/Entrance_Walk.anim` | 1,041667 s |

Chuyển động được đọc từ buffer của `female_Walking_Woman_withSkin.glb` và `male_Walking_withSkin.glb`, không nhân bản mesh/texture vào clip. Cả hai clip có 196 curve binding, khớp hierarchy hiện tại. Tọa độ X/Z của Hips được giữ tại vị trí rest để clip chạy tại chỗ; actor được di chuyển theo đường vào sân. Thời gian, chuyển động bước chân và body animation được giữ. Có hiệu chỉnh nhẹ độ cao toàn model trong đoạn đi bộ để khớp mặt sân; khi kết thúc, model trở lại đúng vị trí và pose Idle cũ.

Hai GLB nguồn được xóa sau khi xác minh clip và scene. Backup nguồn cùng SHA-256 nằm trong `UnityWork/CinematicIntro/SourceBackup` và `extraction-manifest.json`; thư mục UnityWork được Git ignore theo cấu hình dự án.

## Cấu hình

`BotMatchIntro` điều khiển trình tự, camera và overlay trên Canvas hiện có. Dùng chính `Match Camera`, không thêm camera thường trực. `MatchCamera` tạm ngừng follow trong cinematic rồi tiếp tục với các tham số góc chơi hiện tại.

`BotMatchIntroSetup` tạo/gắn clip và UI; builder BotMatch cũng gọi cấu hình này khi dựng lại scene. Menu Unity: **PickleVerse → Add opening cinematic to BotMatch**.

Báo cáo đo, kiểm tra sequence và các hình preview nằm trong `UnityWork/CinematicIntro`.

## Kết quả kiểm chứng

- Chạy đủ 5 giai đoạn trên frame thật: khoảng 11,23 s; camera trở về đúng vị trí, sai lệch đo được bằng 0.
- Input và đồng hồ giao bóng được khóa suốt cinematic; kết thúc vào PreServe, bóng/HUD hiện lại, nhân vật trở về Idle.
- Bỏ qua đoạn mở đầu: pass. Phát giao bóng sau intro: pass.
- 29/29 test luật và điều khiển trong Edit Mode: pass.
- Hai GLB đã được xóa; clip nữ và nam vẫn hợp lệ, scene chỉ dùng một camera.
