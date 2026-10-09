# Kiểm tra multiplayer — 09/10/2026

Bản Windows giao để test: `build_ba61269f7d80`, `Builds/Multiplayer/PickleVerse.exe`. Tám ca đầu và input dùng `build_4f2eaea4aeb1`; bản giao chỉ sửa thêm thao tác ghi file và thời gian chờ trong công cụ smoke test, gameplay/service giữ nguyên. Mỗi ca Windows có build ID trong JSON.

## Các lỗi đã sửa

- Thêm phiên bản trạng thái phòng; bỏ cập nhật SDK cũ để không ghi đè đội hoặc phiên tìm mới.
- Cho khách đã nằm trong roster hoàn tất nhập phòng nếu host vừa bắt đầu trận. Khách khác vẫn bị chặn khi phòng đang chơi.
- Phân đội custom dựa vào chỗ host đã phân. Phản hồi SDK tạm thiếu thành viên không còn làm phòng bốn người thành 3–1; khách rời thì xóa chỗ.
- Khi đang kết nối, đọc lại phiên đấu mỗi 4 giây để nhận metadata Relay nếu sự kiện SDK bị bỏ lỡ. Hạn kết nối và thao tác hủy vẫn giữ nguyên.
- Sửa smoke runner để chờ đúng điều kiện bắt đầu của UI/service; thêm log chẩn đoán chỉ cho phiên chạy với `-pkvCase`. File mã phòng của công cụ kiểm thử được ghi xong rồi mới công bố để tránh lỗi chia sẻ file giữa hai tiến trình Windows.
- Test hủy lúc kết nối chờ đủ hạn kết nối 35 giây của hệ thống. Nếu sự kiện hủy đến trước khi host đăng ký nhận sự kiện SDK, người còn chờ có thể cần hết hạn này mới tự ghép lại.

Kiểm tra thêm `V1009K7` đã tái hiện nhánh khách hủy trước khi host nhận sự kiện: host chỉ còn một kết nối, tự ghép lại tại `00:50` và sau đó hai người vào sân mới. Đồng hồ phía host tiếp tục, người đã hủy tìm lượt mới từ `00:00`.

Các file sửa: `MultiplayerApp.cs`, `MultiplayerApp.Room.cs`, `MultiplayerApp.Matchmaking.cs`, `MultiplayerTypes.cs`, `MultiplayerSmokeRunner.cs`, `MultiplayerSmokeChecks.cs` trong `Assets/Scripts/Multiplayer`.

## Kết quả Windows / Relay

**14/14 kịch bản đạt**. Các trận lifecycle kết thúc 2:1 game, game cuối 11:0.

| Ca kiểm tra | Kết quả | Run ID |
|---|---|---|
| Custom 2vs2: 4 người, đội 2–2, không Ready | Đạt | V1009E6 |
| 1vs1: vào sân, reconnect cùng ID/điểm, thắng 2/3 game, về phòng Singles | Đạt | V1009A4 |
| 2vs2: bốn người riêng lẻ | Đạt | V1009B4 |
| 2vs2: một nhóm đôi và hai người riêng lẻ | Đạt | V1009C5 |
| 2vs2: hai nhóm đôi, giữ cùng đội | Đạt | V1009D4 |
| Custom 1vs2: 3 người, thắng 2/3 game, về cùng phòng | Đạt | V1009F4 |
| Custom: đổi đội, chặn đội trống, kick, nhập lại mã, host bắt đầu | Đạt | V1009G4 |
| Nhóm: khách hủy cho cả nhóm, giữ nhóm; chủ chọn Singles giải tán | Đạt | V1009H4 |
| Chủ nhóm rời: hủy tìm, chuyển quyền chủ nhóm | Đạt | V1009I5 |
| Chủ custom rời: đóng phòng, thông báo khách | Đạt | V1009J5 |
| Hủy khi Connecting: bỏ kết quả muộn, người còn chờ tự ghép lại, giữ đồng hồ | Đạt | V1009K6 |
| Host rời trận: kết thúc phiên, khách về phòng | Đạt | V1009L5 |
| Khách chủ động rời: xử thua và về phòng | Đạt | V1009M5 |
| 2vs2 hết trận: nhóm đôi trở về cùng phòng 2 người; solo về phòng riêng | Đạt | V1009C7 |

## Kiểm tra tự động và Editor

- 61/61 EditMode đạt: chính sách phòng, tổ hợp phím/ShotInput, luật, 10–10 → 12–10, thắng 2/3 game, đồng hồ và đổi sân.
- UI: mặc định 1Vs1; tên ngẫu nhiên bốn chữ cái và đổi tên; phòng 2Vs2/copy mã; hủy tại 00:02; custom không Ready và chặn bắt đầu thiếu đội.
- Tìm thật tại **6,021 giây**, UI `00:06`; timeout tại **126,030 giây** tổng cộng, trở về phòng và mở thử lại. Đếm theo thời gian thực trong Editor chạy nền.
- Hai scene `PlayMultiplayer` / `MultiplayerCourt` và năm prefab multiplayer: **0 Missing Script**; cả hai scene có trong build profile.
- Editor host + Windows client và Windows host + Editor client: D/Shift đúng hướng sân, input chỉ cho nhân vật sở hữu, giữ J chưa đánh, nhả J chạy clip giao BotMatch, animation chạm và làm bóng sống được đồng bộ. Vị trí dự đoán trở về vị trí host sau nhả phím, sai lệch đo được **0 m** ở cả hai vai trò.
- Windows build thành công, **0 errors**. Bản build tăng dần này còn một cảnh báo cấu hình Pipeline; cảnh báo Menu Missing Script/shader ở lần build toàn phần trước chưa được xử lý trong đợt kiểm tra multiplayer.
- Console sau build hiện tại chỉ có cảnh báo cấu hình Pipeline, không có error.

## Phạm vi và phần cần test thêm

Các phiên Windows sử dụng Authentication, Sessions, Matchmaker và Relay thật, danh tính riêng, trên **cùng một PC**. Chưa kiểm tra hai PC vật lý khác mạng.

Test kết thúc trận dùng giao bóng/animation thật và người nhận đứng yên. Kết quả chứng minh đồng bộ bóng/điểm/game/phòng cho các ca này; chưa thay thế một trận đối kháng do người chơi thực hiện với mọi tổ hợp đánh, kitchen và rally dài. Chưa kiểm tra mất mạng vật lý, host bị tắt cưỡng bức và khách không nối lại trong hết 30 giây; ca reconnect dùng ngắt kết nối NGO có kiểm soát.

Log vẫn có cảnh báo HTTP 429/Curl certificate, glyph ellipsis và một cảnh báo allocation quá số frame trong ca hủy bổ sung. Các lần thất bại trước khi sửa được giữ ở `Temp/MultiplayerVerification20261009`; báo cáo Windows chính ghi ca chạy sau sửa. Không kết luận rằng mọi lỗi mạng bên ngoài đã được loại bỏ.

## Bằng chứng

- [Windows/Relay](MultiplayerVerification-20261009-Windows.json)
- [EditMode](MultiplayerVerification-20261009-Tests.json)
- [Build](MultiplayerVerification-20261009-Build.json)
- [Scene/prefab](MultiplayerVerification-20261009-Scenes.json)
- [UI](PlayMultiplayerUiTest.json)
- [Đồng hồ/timeout](MultiplayerVerification-20261009-Timeout.json)
- [Input host](MultiplayerVerification-20261009-HostInput.json)
- [Input client](MultiplayerVerification-20261009-ClientInput.json)
- [Console sau build](MultiplayerVerification-20261009-Console.json)
- [Khách hủy trước khi host nhận sự kiện](MultiplayerVerification-20261009-CancelClient.json)

Log và receipt theo từng danh tính: `Temp/MultiplayerSmoke`. Dùng `Tools/RunMultiplayerSmoke.ps1` và `Tools/StopMultiplayerSmoke.ps1` để chạy lại có chọn lọc; normal build không tự chạy bot/kiểm thử.

