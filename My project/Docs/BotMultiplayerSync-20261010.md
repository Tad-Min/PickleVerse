# Đồng bộ BotMatch → MultiplayerCourt — 10/10/2026

## Đã thực hiện

- Đồng bộ Arena, ánh sáng, camera và prefab Female/Male/bóng từ BotMatch hiện tại qua Unity Editor. Các reference model, material, grip, IK, animation và chuyển động phụ của nhân vật được giữ theo nguồn.
- Sao chép marker điểm rơi và hướng đánh có sẵn trong BotMatch thành prefab; người chơi chỉ thấy marker phục vụ thao tác của mình.
- Chuyển multiplayer sang GameControls và ShotInput dùng chung với BotMatch: hỗ trợ phím tùy chỉnh, tổ hợp đánh, modifier và hủy tổ hợp. Input chỉ điều khiển nhân vật do người chơi sở hữu; host xác thực và mô phỏng trạng thái.
- Dùng chung ServeBallMotion cho hai scene. Bóng theo tay trái, thả xuống ở mốc animation 0,5 giây và kiểm tra tiếp xúc giao bóng từ 0,8 giây. Kiểm tra chân giao/kitchen dùng vị trí chân của MatchActor.
- Giữ quỹ đạo, va chạm vợt, forehand/backhand, animation và âm thanh gameplay dùng chung. Bóng cập nhật cả vị trí Rigidbody và Transform để tránh pose cũ khi reset/đổi sân.
- Đồng bộ quy tắc đường giữa khi giao, chiều cao tiếp xúc và Dink sau khi bóng nảy với BotMatch. Trận thắng 2/3 game, mỗi game 11 điểm, cách biệt 2, chỉ đội giao ghi điểm.
- Tự chuyển game và đổi sân sau 4,2 giây; đổi sân ở mốc 6 điểm trong game quyết định. Chọn đội giao đầu trận ngẫu nhiên, đổi đội giao đầu ở game sau.
- Time-out bằng phím T (hoặc phím đã gán): mỗi đội 2 lần/game, tối đa 60 giây, chỉ trước lượt giao. Đội xin time-out bấm Enter để tiếp tục; hết hạn tự tiếp tục.
- Trong đợt đồng bộ gameplay ban đầu, HUD chỉ hiện `0 : 0` và không có TechniqueBar theo yêu cầu lúc đó. Người dùng sau đó đã yêu cầu đồng bộ toàn bộ UI, bỏ các giới hạn cũ; bản hiện tại có HUD đầy đủ, xem `MultiplayerHudSync-20261010.md`.

## Bằng chứng kiểm tra

| Kiểm tra | Kết quả | Báo cáo |
|---|---|---|
| So sánh nội dung Arena, Female, Male, Ball với BotMatch | 0 khác biệt trong các thuộc tính được so sánh; tổng 194 object | BotMultiplayerGraphics-20261010.json |
| Luật và phòng multiplayer | 39/39 pass | BotMultiplayerTests-20261010.json |
| Editor host + Windows client trên bản build cuối | Pass input owner, time-out, phím giao tùy chỉnh H, giao Female/Male, bóng theo tay, tự game tiếp/đổi sân, HUD | BotMultiplayerRuntime-20261010.json |
| Sai lệch visual bóng cầm trên tay | Tối đa 0,0162 m trong 116 mẫu | BotMultiplayerRuntime-20261010.json |
| Custom trên hai phiên Windows | Kết thúc bằng lượt giao thật; kết quả 1:2 game, game cuối 0:11; hai người về phòng | BotMultiplayerWindows-20261010.json |
| Tự ghép 2vs2 trên bốn phiên Windows | Cả bốn vào sân, đủ hai đội | BotMultiplayerWindows-20261010.json |
| Missing Script trong scene runtime multiplayer | 0 | Kiểm tra trực tiếp Editor |
| Build Windows cuối | Succeeded, 0 error, 499 warning | BotMultiplayerBuild-20261010.json |

Build cuối: `build_0c3cfbace6c7`, hoàn tất lúc 00:50 ngày 10/10/2026 (UTC+7), tại `Builds/Multiplayer/PickleVerse.exe`. Tất cả máy cần dùng nguyên thư mục build mới vì protocol đã tăng lên **3** khi thêm trạng thái đồng bộ.

Phép thử Windows lifecycle/2vs2 dùng build trước trong cùng đợt đồng bộ; build cuối thêm sửa cập nhật visual bóng và loại bỏ cảnh báo API tìm object. Runtime Editor/Windows cuối đã kiểm tra chính sửa visual này. Lifecycle dùng máy nhận đứng yên để kiểm tra giao, nảy, tính điểm và kết quả; chưa thay thế kiểm tra rally thủ công đầy đủ trên máy khác mạng.

## Giới hạn và cập nhật HUD

Các phiên test dùng Unity Services/Relay thật nhưng đều chạy trên cùng máy; chưa xác minh hai máy vật lý ở hai mạng khác nhau trong đợt này. Build còn cảnh báo shader và thiếu PipelineRuntimeConfig; kết nối gameplay vẫn thành công. Log có cảnh báo font ellipsis/TLS trong môi trường test.

Bộ duyệt tự động từng từ chối bản vá thay thế/thêm HUD theo giới hạn cũ. Người dùng đã duyệt đồng bộ toàn bộ UI ngày 10/10/2026. Phần HUD hiện đã áp dụng, ghi nhận tại `MultiplayerHudSync-20261010.md`.

Ảnh sân sau đồng bộ: `BotMultiplayerCourt-20261010.png`. Các tiến trình test và phòng do tôi tạo đã được dọn. Không commit/push và không hoàn nguyên các thay đổi sẵn có của người dùng.
