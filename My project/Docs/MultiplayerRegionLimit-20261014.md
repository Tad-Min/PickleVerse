# Bỏ giới hạn vùng/ping multiplayer — 2026-10-14

**Cập nhật sau khi chạy với dữ liệu thật:** cách ghi `relayQosJson` dưới đây đã được thay bằng hai mảng nguyên thủy `relayRegions`/`relayLatencies`, vì Matchmaker giới hạn mỗi chuỗi ở 200 ký tự. Xem `MatchmakerPayloadFix-20261014.md` và kết quả `MatchmakerPayloadLive-20261014.json` cho bản sửa hiện tại. Nội dung bên dưới ghi lại đợt sửa trước đó.

## Thay đổi trong project

- Bỏ `maximumLatencyMs` và ngưỡng 150 ms khỏi luồng 1vs1/2vs2.
- QoS nhận mọi số đo hợp lệ, kể cả ping cao và mất gói trên 10%. Các số đo không có phản hồi hoặc dữ liệu không hợp lệ được bỏ qua.
- Nếu đo QoS thất bại sau ba lần, tiếp tục ghép với dữ liệu rỗng. Hủy vẫn có hiệu lực và các phép đo SDK vẫn được tuần tự hóa.
- Vé nhóm giữ số đo của từng người trong chuỗi `CustomData.relayQosJson`; không gửi trường `qosResults` làm điều kiện matchmaking. Kiểm tra dịch vụ cho thấy chỉ xóa quy tắc vùng vẫn chưa đủ: khi gửi số đo với hai vùng không giao nhau, vé vẫn chưa ghép được; chuyển số đo sang chuỗi dữ liệu riêng thì cả hai queue ghép thành công.
- Sau khi đủ người, chọn vùng chung có ping tối đa thấp nhất cho cả trận. Nếu không có số đo chung, truyền vùng null để Relay tự chọn.
- Xóa `SameMeasuredRegion` trong hai file queue và công cụ sinh cấu hình. Cấu hình local vẫn giữ quy tắc phiên bản tương thích, Elo và ưu tiên 1vs1.
- [Unity mô tả việc tự chọn vùng Relay khi không chỉ định vùng](https://docs.unity.com/en-us/mps-sdk/networking/qos).

## Kiểm tra

- Unity đã biên dịch thay đổi thành công; trạng thái Console sau biên dịch: compilationFailed=false, consoleErrors=0, consoleWarnings=0.
- Bản đầu đạt 13 phương thức NUnit bằng harness .NET ngoài Editor. Bản cuối đạt 14/14 phương thức NUnit, gọi trực tiếp trên DLL Unity biên dịch và chạy trên luồng nền trong Editor. Bao gồm ping 900 ms, vùng khác nhau, QoS thất bại, không có phản hồi, hủy tìm, tuần tự hóa phép đo và chọn Relay từ dữ liệu đã lưu. Đây không phải báo cáo hoàn tất Unity Test Runner.
- Kết quả: `MultiplayerRegionLimitTests-20261014.json`.
- Thử dịch vụ thật với danh tính Authentication riêng và protocol test biệt lập: cả queue 1v1 (2 người) và 2v2 (4 người) tìm được cùng match cho hai vé. Các vé chứa dữ liệu ping **giả lập** 800/900 ms và vùng ưu tiên Asia/Europe khác nhau; dữ liệu đo được giữ trong `relayQosJson`. Bốn vé của bài thử thành công đã được xóa bằng đúng chủ sở hữu. Kết quả: `MultiplayerRegionLiveTickets-20261014.json`.

## Trạng thái triển khai

Người dùng đã cho phép triển khai **cả 1vs1 và 2vs2**. Hai queue trong `pickleverse-test` đã được cập nhật/đối chiếu; đọc lại xác nhận không còn `SameMeasuredRegion`. 1v1 giữ `SameProtocol`, `EloWindow`, `MutualPreference`; 2v2 dùng `SameProtocol` như cấu hình local để các bản khác nhau không vào chung trận. Thời gian chờ vẫn 360 giây (1v1) và 120 giây (2v2). Biên nhận và cấu hình: `MultiplayerRegionDeployment-20261014.json`, `MultiplayerRegionQueuesBefore-20261014.json`, `MultiplayerRegionQueuesAfter-20261014.json`.

Trong kiểm tra, Editor bị treo khi công cụ serialize kết quả Task trong eval. Không buộc tắt Editor; người dùng đã mở lại Unity. Menu được phục hồi, scene cuối vẫn `Assets/Scenes/UI/Menu.unity`, Edit Mode. Các lỗi thử nghiệm 403 do ID giả và 400 do định dạng mảng customData đã được lưu trong `MultiplayerRegionConsoleHistory-20261014.json`; các bài thử dùng danh tính thật và chuỗi QoS đã hoàn tất. Hai vé ID giả ban đầu không xóa được qua quyền player của bài thử; chúng dùng protocol riêng nên không ghép với người chơi thật, và chịu vòng đời/timeout 360 giây của queue. Chưa build lại Windows hoặc kiểm thử trận giữa hai máy khác mạng.
