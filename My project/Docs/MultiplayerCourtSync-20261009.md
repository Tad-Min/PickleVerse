# Đồng bộ MultiplayerCourt với BotMatch — 09/10/2026

Đã cập nhật `Assets/Scenes/MultiplayerCourt.unity` và ba prefab trong `Assets/Prefabs/Multiplayer/BotMatch` từ nội dung hiện tại của BotMatch, qua Unity Editor API.

## Các thay đổi

- Nền sân mở rộng, vị trí khán đài và biển tài trợ; bổ sung hai khán đài như BotMatch.
- Skybox BlueSunset/MoonBurst, ánh sáng mặt trời, sương và bốn đèn góc sân. Multiplayer hiện dùng chế độ ban ngày được lưu trong scene nguồn, giống nhau trên mọi máy; không đọc tùy chọn ngày/đêm riêng của BotMatch trong PlayerPrefs.
- Female: rig tóc đuôi ngựa, mái tóc và váy; Male: rig tóc ngắn. Sao chép mesh, material, vợt, tham chiếu tay trái, animation và thông số chạm bóng hiện tại.
- BallVisual có vệt bóng mới của BotMatch. NetworkMatch dùng TrailRenderer trên prefab, bật khi bóng đang sống và xóa vệt khi đặt lại rally.
- Camera giữ thông số theo dõi của BotMatch và dùng Skybox.

Thêm menu **PickleVerse → Multiplayer → Sync court and players from BotMatch** để đồng bộ lại sau các lần chỉnh BotMatch. Bản sao trước khi thay đổi được lưu tại `Temp/MultiplayerCourtSync/20261009-170631`.

## Kiểm tra

| Nội dung | Trước đồng bộ | Sau đồng bộ |
|---|---:|---:|
| Arena | 29 khác biệt | 0 |
| FemaleActor | 48 khác biệt | 0 |
| MaleActor | 20 khác biệt | 0 |
| BallVisual | 1 khác biệt | 0 |

Đối chiếu hierarchy và thuộc tính serialized đã chuẩn hóa tham chiếu nội bộ; bỏ qua tên root và vị trí/hướng spawn của actor/ball. Camera theo dõi: cao 5,3 m, cách 4,21 m, pitch 31°, FOV 60°.

- Scene và năm prefab multiplayer: **0 Missing Script**. CourtBootstrap còn nguyên, không có BotMatch controller trong sân multiplayer.
- Thử áp dụng ánh sáng ngày và đêm: skybox, mặt trời, bốn đèn và material mặt đèn hoạt động đúng; đã trả scene về ban ngày.
- Editor host + Windows client qua Sessions/Relay: hai người vào MultiplayerCourt thành công, Female/Male mặc định Idle_Ready; chuyển động phụ khởi tạo với 79/67 bones.
- Input D/Shift di chuyển nhân vật sở hữu 1,35 m và không di chuyển nhân vật còn lại. Nhả J chạy animation giao bóng và đưa bóng vào rally; chỉ một TrailRenderer, ghi nhận 10 điểm trên vệt bay.
- Build Windows `build_2eecb0c0e553` thành công, **0 lỗi**, tại `Builds/Multiplayer/PickleVerse.exe`.

Build toàn phần còn **542 cảnh báo**: 43 lượt cảnh báo Missing Script ở Menu, 498 cảnh báo shader và một cảnh báo cấu hình Pipeline. Các cảnh báo này thuộc các phần ngoài nội dung sân/actor vừa đồng bộ. Không kết luận toàn project đã sạch cảnh báo.

Phiên kiểm tra dùng cùng một PC; chưa kiểm tra máy vật lý khác mạng trong đợt đồng bộ này. Các tiến trình test đã được đóng, Editor trả về MultiplayerCourt ngoài Play Mode.

## Bằng chứng

- [Dữ liệu đối chiếu và kiểm tra runtime](MultiplayerCourtSync-20261009.json)
- [Ảnh sân trong trận multiplayer](MultiplayerCourtSync-20261009.png)
