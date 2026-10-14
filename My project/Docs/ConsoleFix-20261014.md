# Sửa Console — 14/10/2026

## Nguyên nhân

Hàng chờ `PickleVerse-1v1` trong môi trường `pickleverse-test` có quy tắc `SameProtocol` với `reference: "6"`, trong khi code hiện tại dùng protocol **7**. Vé thử với protocol 7 bị máy chủ từ chối ngay: `Failed`, `The ticket is not compatible with the match definition.` Vé thử đã được xóa.

Code cũ gộp `Failed` và `Timeout` thành `TimeoutException`, nên Console chỉ hiện “Chưa tìm được trận phù hợp”. Số đo QoS trong phiên này có vùng Asia Southeast đạt 68–114 ms; cảnh báo được điều tra xảy ra sau bước QoS.

Bằng chứng: `ConsoleLiveQueuesBefore-20261014.json`, `ConsoleTicketBefore-20261014.json`, `ConsoleBeforeFix-20261014.json`. [Unity mô tả lỗi vé không tương thích cấu hình](https://docs.unity.com/en-us/mps-sdk/matchmaking/mm-troubleshooting).

## Bản sửa local

- `GroupMatchmaker.ValidateAssignment`: phân biệt vé bị từ chối với vé hết hạn; giữ tên hàng chờ và thông báo gốc của máy chủ.
- `SearchRankedSinglesAsync`: vé hết hạn được tìm lại ở pha UTC tiếp theo, giữ nguyên đồng hồ và mốc 360 giây. Hủy vẫn kết thúc chờ; lỗi cấu hình vẫn được báo.
- Timeout bình thường của lượt tìm được ghi ở mức Log; các lỗi dịch vụ khác vẫn báo Warning.
- Cấu hình local 1v1 đã có sẵn Equality `SameProtocol` giữa các người chơi, không khóa vào một số protocol cố định. File này chưa được thay đổi trong đợt sửa.

## Xác minh

Unity biên dịch thành công, không có lỗi C#. Bốn kiểm tra `Failed`, `Timeout`, `InProgress`, `Found` đạt khi gọi trực tiếp các phương thức NUnit đã biên dịch. Lượt Unity Test Runner bị hủy vì Editor chuyển sang Play trong lúc chạy; các thông báo Test Framework phát sinh thuộc lượt đó. Scene Menu vẫn ở trạng thái có chỉnh sửa chưa lưu như lúc bắt đầu, không được lưu hoặc thay thế.

## Triển khai và kiểm tra dịch vụ

Bộ kiểm tra phê duyệt tự động ban đầu từ chối triển khai vì queue là cấu hình từ xa dùng chung. Người dùng sau đó đã cho phép rõ ràng triển khai queue 1vs1.

Đã triển khai **chỉ** `Assets/Settings/Multiplayer/Cloud/PickleVerse_1v1.mmq` vào **pickleverse-test**. Đọc lại xác nhận `SameProtocol.reference` là null, giữ timeout 360 giây, khoảng Elo và các quy tắc khác; queue 2v2 giữ cấu hình trước đó. Biên nhận: `ConsoleDeployment-20261014.json`; cấu hình sau triển khai: `ConsoleLiveQueuesAfter-20261014.json`.

Vé kiểm tra sau triển khai dùng danh tính SDK riêng và một protocol riêng để cách ly với người chơi thật. Bốn lượt đọc trạng thái đều `InProgress`, không còn bị từ chối cấu hình; vé đã xóa. Đây là xác minh vé được dịch vụ nhận, chưa phải một trận với hai người chơi. Bằng chứng: `ConsoleTicketAfter-20261014.json`.

Một host localhost riêng đã StartHost, Shutdown rồi Destroy thành công, không phát sinh lỗi Netcode mới. Sau đó dừng Play cũng không thêm lỗi Netcode. Bằng chứng: `ConsoleNetworkLifecycle-20261014.json`. Các lỗi Dispose trước đó xuất hiện cùng lượt Test Runner xung đột với Play, không tái hiện trong phép thử vòng đời riêng; không thay đổi package Netcode để xử lý log cũ.

Đã lưu toàn bộ lịch sử Console vào `ConsoleHistory-20261014.json` trước khi dọn. Bước dọn Console ban đầu cũng bị chặn vì nguy cơ mất bằng chứng; sau khi có bản lưu đầy đủ, thao tác được chấp nhận. Trạng thái cuối: **0 lỗi, 0 cảnh báo, không có lỗi biên dịch**; Editor ở Menu/Edit Mode, các chỉnh sửa scene chưa lưu vẫn còn.

Chưa build lại Windows trong đợt này. Máy khác cần cùng protocol 7; bản build cũ protocol 6 sẽ tìm trong nhóm protocol 6, không ghép chung với 7.
