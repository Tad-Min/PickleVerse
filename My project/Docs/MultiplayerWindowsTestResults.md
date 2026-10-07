# Kiểm tra multiplayer Windows — 2026-10-07

## Kết quả đã xác minh

| Ca | Kết quả | Bằng chứng trong Temp/MultiplayerSmoke |
|---|---|---|
| Tự ghép 1vs1 | Hai phiên kết thúc 11:0, trở về Singles | singles-T1155 |
| Mất kết nối rồi nối lại | Host tạm dừng; giữ danh tính/điểm, phục hồi ownership, sau đó chơi hết game | singles-T1155, reconnect PASS |
| Bốn solo 2vs2 | Đủ 4 người, 11:0, trở về Doubles | solos4-G1120 |
| Duo + hai solo | Duo cùng đội; 11:0; giữ phòng nhóm hai người | duoSolos-Z1203 |
| Hai duo | Giữ hai nhóm/đội sau trận 11:0 | duos2-Q1150A/B |
| Custom 1vs1 và quản lý khách | Hoán đổi đội xóa Sẵn sàng; chặn bắt đầu sai; mời ra/vào lại; 11:0 và reset Sẵn sàng | customAdmin-J1126 |
| Custom 2vs2 | Đủ/cân bằng hai đội; tất cả Sẵn sàng; 11:0; về phòng bốn người, reset Sẵn sàng | custom2-W1200 |
| Hủy nhóm trước giây 6 | Khách hủy cả nhóm, giữ nhóm; rời nhóm hủy và mở lại 1vs1 | duoControls-K1134 |
| Chủ nhóm rời trước trận | Hủy tìm, chuyển quyền cho thành viên còn lại | partyDeparture-S1153 |
| Chủ custom rời trước trận | Phòng đóng, khách được thông báo | customDeparture-X1201 |
| Hủy lúc kết nối | Bỏ qua kết quả muộn; người còn chờ tự tìm lại giữ đồng hồ; lượt mới đặt lại 00:00; ghép lại và 11:0 | singles-B1210 (behavior cancelConnect) |
| Host rời trong trận | Khách kết thúc phiên và trở về phòng | custom1-C1210 (hostLeaves PASS) |
| Khách chủ động rời | Đội của khách bị xử thua; khách và host về phòng | custom1-D1214 (clientLeaves PASS) |
| Khách ngắt đột ngột, không nối lại | Host kết thúc và xử thua đội khách với lý do Mất kết nối quá 30 giây | custom1-E1214.PE1214C1.log; dừng cưỡng bức riêng PE1214C2 |

- Thời gian tìm thực: 126.084 giây từ click, gồm 6 giây chờ và 120 giây tìm. Trở về phòng/hiện thử lại. Xem MultiplayerTimeoutTest.json.
- Menu Play từ phòng 2vs2 trở về mặc định 1vs1, giữ hồ sơ đăng nhập. Tools/MultiplayerFinalValidation.cs, MenuDefault.
- Input System J press/release đi qua input owner và RPC: Female chỉ giao khi nhả phím, chạy Serve_FH_Underhand và tiếp xúc hợp lệ. Xem MultiplayerInputPlayTest.json.
- Hai scene multiplayer và hai network prefab: 0 Missing Script. Male/Female mặc định Idle_Ready; BtnPlay có persistent listener. Tools/MultiplayerFinalValidation.cs, Assets.
- 25/25 ca NUnit qua: đúng mốc giây 6, tổ hợp phím, lượt giao đôi, sai người nhận, line call, hai lần nảy, kitchen/momentum và 10–10 → 12–10. Xem MultiplayerRulesTestResults.json.
- Build Windows thành công, 0 lỗi. Menu có các Missing Script từ script UI cũ bị xóa trước đợt multiplayer; build đầy đủ có thể báo các cảnh báo đó. Hai scene mới đã kiểm tra riêng và không có Missing Script. Cảnh báo Pipeline thiếu RuntimePipelineConfig thuộc công cụ Editor.

Hủy nhóm đã được chạy lại trên build cuối: duoControls-F1215, cả hai receipt PASS.

Build cuối `build_e5b938bdf04c`: Succeeded, 0 lỗi, 1 cảnh báo Pipeline. Hồi quy nối lại và game 11:0 đã qua trên cả hai Player: singles-G1218. Đã dừng các Player test và để Unity mở MultiplayerTest.

## Phạm vi bằng chứng

Các Player Windows có danh tính riêng, dùng Unity Services/Relay thật, chạy trên cùng máy. **Chưa xác minh bằng một máy khác qua Internet.** Các game tự động dùng animation giao bóng, bóng vật lý, nảy và điểm ở host cho đối phương đứng yên. Chưa thay thế một trận chơi tay với nhiều cú trao đổi hoặc kiểm tra cảm giác/timing của tất cả clip.

Các lỗi đã sửa qua test: shader/material bị strip, quyền sửa player data của khách, polling gây 429, join hoàn tất muộn đổi InMatch về Connecting, reconnect chỉ phục hồi Lobby. Receipt cũ mang FAIL không được tính là đã qua. Game 0:0 do forfeit cũng không được tính là game đủ 11 điểm; smoke runner hiện chặn trường hợp đó.

## Chạy lại

PowerShell tại thư mục dự án, sau khi build, chạy từng lượt và dùng RunId mới:

```powershell
./Tools/RunMultiplayerSmoke.ps1 -Case singles -RunId Test1 -Reconnect -Lifecycle
./Tools/RunMultiplayerSmoke.ps1 -Case solos4 -RunId Test2 -Lifecycle
./Tools/RunMultiplayerSmoke.ps1 -Case duoSolos -RunId Test3 -Lifecycle
./Tools/RunMultiplayerSmoke.ps1 -Case duos2 -RunId Test4 -Lifecycle
./Tools/RunMultiplayerSmoke.ps1 -Case customAdmin -RunId Test5 -Lifecycle
./Tools/RunMultiplayerSmoke.ps1 -Case custom2 -RunId Test6 -Lifecycle
./Tools/RunMultiplayerSmoke.ps1 -Case singles -RunId Test7 -Behavior cancelConnect -Lifecycle
./Tools/RunMultiplayerSmoke.ps1 -Case custom1 -RunId Test8 -Behavior hostLeaves
./Tools/RunMultiplayerSmoke.ps1 -Case custom1 -RunId Test9 -Behavior clientLeaves
```

Kết quả/PID ở Temp/MultiplayerSmoke. Dừng đúng một lượt:

```powershell
./Tools/StopMultiplayerSmoke.ps1 -ProcessFile Temp/MultiplayerSmoke/singles-Test1.processes.json
```

Máy thứ hai dùng cùng build/môi trường pickleverse-test và chọn hồ sơ riêng. Test Internet gồm 1vs1, 4 solo, duo+2 solo, 2 duo và custom.
