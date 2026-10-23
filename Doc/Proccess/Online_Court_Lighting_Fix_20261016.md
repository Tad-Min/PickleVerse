# Sửa ánh sáng sân online — 16/10/2026

## Nguyên nhân và thay đổi

`MultiplayerCourt` dùng chung bộ đèn với BotMatch. Khi dữ liệu trận chọn ban ngày, bộ điều khiển tắt toàn bộ đèn mái; mái sân vận động lại che nắng, trong khi đèn cột Y ở mức ban ngày chưa đủ chiếu sáng mặt sân.

Thêm `minimumCourtIllumination` trên `MatchEnvironmentLighting`, mặc định bằng 0. Riêng scene `MultiplayerCourt` đặt mức 0,65. `StadiumEnvironment.ApplyNight` dùng mức lớn hơn giữa độ tối và ngưỡng chiếu sáng trong nhà để điều khiển đèn sân; bầu trời, mặt trời và đèn khán đài vẫn theo thời điểm ngày/đêm nhận từ host.

| Trạng thái online | Đèn mái | Đèn Y hướng sân | Đèn hướng khán đài |
|---|---:|---:|---:|
| Ban ngày | 16 nguồn, cường độ 52 | 8 nguồn, cường độ 32,05 | Tắt |
| Ban đêm | 16 nguồn, cường độ 80 | 8 nguồn, cường độ 45 | 8 nguồn, cường độ 8 |

Đèn chiếu sân dùng màu trắng và tắt nhiệt độ màu. Giữ nguyên vị trí, hướng, tầm chiếu và số nguồn sáng đã tối ưu; không thêm đèn hoặc bản đồ bóng. Các spotlight mái không đổ bóng. Những scene khác giữ ngưỡng mặc định bằng 0 và cấu hình cũ.

Scene được lưu bằng API Unity Editor qua `My project/Tools/OnlineCourtLightingFix.cs`, không sửa YAML bằng tay.

## Kiểm tra

- 26 kiểm tra runtime đạt qua `OnlineCourtLightingValidation.Run`: JSON dữ liệu trận đi qua hàm thật `NetworkMatch.ApplyEnvironment`, đổi ngày/đêm từ trạng thái ngược, màu trắng, nón chiếu tới tâm sân, số nguồn sáng và bộ nhớ đệm khi đổi riêng ngưỡng trong nhà. Thử cả renderer PC và Mobile trong Editor.
- Đã xem ảnh GameView hoàn chỉnh cho cả bốn trường hợp PC/Mobile × ngày/đêm. Renderer Mobile dùng giới hạn đèn trên mỗi vật thể nên độ sáng thấp hơn PC, nhưng các đèn chiếu sân hoạt động.
- 27 kiểm tra hồi quy đạt qua `NightLightingValidation.Run` cho BotMatch, Training, Tutorial, MultiplayerCourt, gồm màu đèn, mức sáng ban đêm, đèn khán đài và ưu tiên thời điểm do host gửi.
- Unity biên dịch thành công; Console hiện không có lỗi.

Báo cáo và ảnh: `UnityWork/OnlineCourtLighting20261016/runtime-validation.json`, `runtime-desktop-day.png`, `runtime-desktop-night.png`, `runtime-mobile-day.png`, `runtime-mobile-night.png`; hồi quy tại `UnityWork/NightLighting/runtime-validation.json`.

Các phép thử chạy trong Editor, không mở phiên dịch vụ đám mây và không thay thế phép thử kết nối trên hai máy hoặc đo FPS trên thiết bị thật.
