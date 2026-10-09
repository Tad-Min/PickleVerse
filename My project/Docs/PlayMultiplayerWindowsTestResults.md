# PlayMultiplayer: Windows / Relay verification

## Kiểm tra mới nhất — 09/10/2026

[Báo cáo multiplayer mới nhất](MultiplayerVerification-20261009.md): 14/14 kịch bản Windows đạt, 61/61 EditMode đạt; đã sửa cập nhật phòng cũ, race khi nhập mã/bắt đầu custom, phân đội 3–1 khi khách vào đồng thời và phục hồi dữ liệu Relay bị bỏ lỡ. Bản giao `build_ba61269f7d80`, 0 build errors. Có thêm ca khách hủy trước khi host nhận sự kiện, input host/client và kiểm tra prediction trở về vị trí host.

Các kết quả bên dưới là lịch sử của các bản trước. Test dùng nhiều phiên trên cùng PC qua UGS/Relay thật; chưa kiểm tra hai PC khác mạng hoặc một trận đối kháng đầy đủ do người chơi thực hiện.

## Lịch sử kiểm tra

Build: `build_02b17799ff2e`, Windows x64, 0 build errors. Date: 2026-10-08 (Asia/Saigon).

Các ca dưới đây dùng nhiều Player Windows riêng biệt trên **cùng PC**, thông qua Unity Authentication, Sessions, Matchmaker và Relay thật. Không dùng giả lập matchmaker. Chưa kiểm tra giữa hai PC khác mạng.

| Ca | Kết quả / evidence |
|---|---|
| Tự ghép 1vs1 | NU3: cả hai vào MultiplayerCourt, submit tại 00:06 |
| 1vs1 reconnect | NU3: ngắt khách, host pause, nối lại cùng ID/quyền điều khiển, điểm giữ nguyên |
| 1vs1 hết trận 2/3 game | NU3: cả hai nhận games=2:1, game cuối 11:0; trở lại phòng Singles, một người |
| Custom 1vs2 không Ready | NC14: 3 người vào sân; games=2:1, game cuối 11:0; trở lại cùng phòng 3 người |
| Hai nhóm đôi | ND2: 4 người vào sân, mỗi nhóm giữ nguyên một đội |
| Bốn người riêng lẻ | NS4: cả 4 vào sân, 2 người mỗi đội |

Chi tiết log và receipt: `Temp/MultiplayerSmoke`. Các script chỉ hoạt động khi có `-pkvCase`. Test lifecycle dùng thao tác gọi swing của host và animation giao thật; các người nhận đứng yên. Ca này kiểm tra chạm bóng, điểm, đổi game/sân và quay lại phòng, không phải bằng chứng cho một rally đối kháng đầy đủ với người chơi.

Build có cảnh báo shader của package và Missing Script trong Menu do các script UI cũ đã bị xóa trong workspace. PlayMultiplayer/MultiplayerCourt phải được kiểm tra riêng để không nhầm cảnh báo Menu với scene multiplayer.

- NDS1: một nhóm đôi + hai người lẻ, cả 4 vào sân, nhóm giữ cùng đội.
- NC1: custom 1vs1 với mode Singles cũ, cả 2 vào sân, không có lỗi Scene Hash.
- NE2: Editor host Female + Player Windows Male, D/Shift di chuyển đúng (dx≈0.96 m), không điều khiển khách; giữ J chưa đánh, nhả J dùng clip PKV_Serve_FH_Underhand và chạm bóng hợp lệ. Báo cáo: Docs/PlayMultiplayerInputTest.json.


## Bản cuối build_2306fd395da1

- NA2: khách BtnSwap đổi đội; host từ chối bắt đầu khi một đội trống, đổi đội khách, kick, khách nhập mã vào lại; host bắt đầu không cần Ready. Cả hai vào sân.
- NDc2: khách hủy tìm cho cả nhóm, giữ hai thành viên; chủ nhóm chọn Singles giải tán nhóm và khách nhận mời ra. Cả hai PASS.
- 71/71 EditMode; PlayMultiplayer và MultiplayerCourt có 0 Missing Script. Build cuối 0 errors. Cảnh báo Menu/compute shader còn riêng trong project.
- NCancel: một người hủy tại Connecting, không khởi động lại bởi kết quả muộn; người còn chờ tự tìm lại từ 00:14, người hủy tìm phiên mới từ 00:00; cả hai ghép mới và vào sân.
- NC2: custom 2vs2 không Ready, đủ 4 người vào sân. SDK đôi lúc trả HTTP 429 trong lúc tạo session; retry đã phục hồi. Không có Scene Hash/NullReference trong các phiên build cuối.
- NDFull: một nhóm đôi + hai solo chơi hết trận **2:1 game**, game cuối 11:0; nhóm đôi trở lại cùng phòng 2 thành viên; hai solo trở lại phòng 2vs2 riêng mỗi người. Cả 4 PASS.
- NV1/NV2/NV3: Editor + 3 Player Windows ở phòng custom, cả 4 TxtName hiển thị đúng vị trí; Invite placeholder ẩn khi chỗ có người; các nút kick không che tên. Ảnh: Docs/MultiplayerScreenshots/PlayMultiplayer-Custom4.png.

Bản giao cuối: build_0dab91c6a51b, 0 errors. Bổ sung ẩn Invite placeholder khi chỗ custom đã có người; lớp network/gameplay giữ nguyên từ bản đã qua các ca trên.
