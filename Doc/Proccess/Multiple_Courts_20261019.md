# Hai sân mới — 19/10/2026

## Luồng sử dụng

- Menu dùng `ProfessionalStadium.glb`.
- Chọn chế độ → Đấu với bot → Chọn sân: **Chuyên nghiệp** (`ProfessionalStadium.glb`) hoặc **Ngoài trời** (`PickleballCourt_LowPoly.glb`). Ảnh sân đổi theo lựa chọn ngày/đêm. Lựa chọn được lưu trong `PKV.Bot.Venue` và giữ khi chơi lại.
- MultiplayerCourt chỉ dùng hai sân mới. Host chọn ngẫu nhiên một trong hai khi tạo `NetworkMatch`, ghi vào `MatchRoster.venue`, rồi mọi client áp dụng cùng giá trị. Không chọn ngẫu nhiên riêng trên client; đổi game trong cùng trận giữ nguyên sân.
- Training và Tutorial tiếp tục dùng sân đang có. Các scene khác giữ chức năng cũ; Replay được bổ sung ngân sách dựng hình sau khi phép đo phát hiện chậm.

## Sân, ánh sáng và camera

Hai prefab tại `My project/Assets/Resources/Courts` dùng lại mesh và texture nhập từ GLB, với material Simple Lit có giữ màu, texture và alpha cutout của lưới/hàng rào. Không sửa hai file GLB nguồn (LowPoly 169.016 byte, Professional 3.488.344 byte).

Gốc model Professional đã chứa độ cao 0,45 m. Công cụ authoring bù độ cao tương đối, đưa mặt sân về y=0. Các vạch ngoài đạt x=±3,048 m, z=±6,7056 m; kitchen ±2,1336 m; tâm lưới cao 0,8636 m. Giữ hệ vật lý và luật thi đấu hiện có.

Mỗi sân có bốn cột và spotlight trắng, tối đa hai nguồn tạo bóng; không dùng bộ 166 đèn của sân cũ. Ban ngày dùng mặt trời và mức đèn hỗ trợ, ban đêm dùng spotlight. Hai scene trận dùng chung hệ điều khiển ánh sáng với ngưỡng hỗ trợ ban ngày 0,2. Cường độ đèn đêm: Professional 155, LowPoly 125. Các nguồn sáng được đặt ngoài vùng thi đấu và hướng vào mặt sân.

Mốc vào sân, vị trí kết quả và giới hạn camera được lưu trên từng prefab. `CourtVenueSelection` cập nhật liên kết ánh sáng, intro và kết quả khi thay sân. Camera quay cận được giữ trong phạm vi hàng rào; cinematic online vẫn quay hai thành viên cùng đội trong 2v2. Cánh cổng LowPoly mở theo bản lề đã có.

## Hiệu năng và kiểm tra

Máy đo: RTX 3050 Laptop, Core i5-11400H, RAM khoảng 16 GB. Unity Editor 6000.6.2f1, Game View 1920×1080, giới hạn 144 FPS, dùng ngân sách độ phân giải 3D thích ứng có sẵn. Các số sau là giá trị trung bình của những đoạn đo ngắn trong Editor:

| Trường hợp | FPS trung bình |
|---|---:|
| BotMatch, hai sân × ngày/đêm | 136–144 |
| MultiplayerCourt, 1v1/2v2 × hai sân × ngày/đêm | 137–144 |
| Menu, ModeSelection, Settings, PlayMultiplayer, Shop, Collection | Khoảng 144 |
| Training | 121 |
| Tutorial | 123 |
| Quay lại Menu qua ModeSelection → Training → ModeSelection | 136 |
| Replay thật có hai nhân vật, trước tối ưu | 33 |
| Replay thật sau khi tự áp dụng ngân sách dựng hình | 114 |

Replay trước đó dùng render scale 1,25 và không giới hạn chi phí như màn đấu. `ReplayEnvironment.Awake` nay gắn `MatchRenderBudget`, tự điều chỉnh độ phân giải 3D và bóng; UI Overlay giữ độ phân giải đầy đủ. Phép thử trước/sau trên cùng bản ghi cho thấy cải thiện; vẫn có một số khung hình dài trong Editor, nên đây không phải cam kết mọi khung hình đều trên 60 FPS trên mọi thiết bị.

- **177 kiểm tra tích hợp đạt**: click chọn sân, lưu lựa chọn, mặt sàn/vạch/lưới, cả hai intro của bot, rematch, host chọn sân và payload JSON, cinematic online theo đồng hồ NGO thật cho 1v1/2v2, đợi thông báo cuối trận trước kết quả, kiểm tra missing script của 12 scene trong Build Settings.
- **100 kiểm tra bổ sung đạt**: nút nằm trong màn hình và nhận raycast ở 1280×720, 1920×1080, 2340×1080, 1024×768, 800×360; xem ảnh renderer Mobile; quay về Menu còn đúng một stadium và một EventSystem.
- **7/7 EditMode tests đạt** trong `MatchEnvironmentPayloadTests`, gồm hai sân × ngày/đêm, tính bất biến của snapshot và tương thích payload cũ.
- Đã xem ảnh gameplay đêm, menu, intro 1v1/2v2, kết quả và UI chọn sân.

Online được kiểm bằng host loopback thật với các nhân vật mô phỏng đối phương/đồng đội. Chưa kiểm kết nối hai máy qua Internet, chưa đo trên điện thoại thật hoặc bản build độc lập. PlayMultiplayer trong phép rà scene chỉ kiểm dựng UI, không gọi dịch vụ tìm trận.

## Công cụ và báo cáo

Authoring qua Unity Editor API, không sửa YAML bằng tay: `My project/Tools/NewStadiumSetup.cs`, theo thứ tự `Build`, `Scenes`, `Previews`, `Mode`.

Kiểm tra: `NewStadiumValidation.Start`, `NewStadiumExtras.Start`, `ReplayVenuePerformance.Verify` trong thư mục Tools. Báo cáo và ảnh tại `UnityWork/NewStadiums20261019`; mốc FPS sân cũ tại `UnityWork/StadiumPerformance/newVenues-before.json`.

Các script kiểm tra dùng hồ sơ QA riêng và khôi phục hồ sơ, lựa chọn chơi, renderer và kích thước Game View khi kết thúc. Asset font động do Editor ghi lại được tách khỏi thay đổi tính năng; giữ nguyên bản font đã sửa sẵn trước tác vụ.
