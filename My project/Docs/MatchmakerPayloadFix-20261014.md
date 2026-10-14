# Sửa giới hạn dữ liệu vé Matchmaker — 2026-10-14

## Nguyên nhân

`BuildTicketPlayers` trước đây đưa toàn bộ danh sách `QosResult` và annotations của SDK vào chuỗi `relayQosJson`. [Unity giới hạn mỗi chuỗi customData ở 200 ký tự, mảng ở 100 phần tử và vé ở 25 KB](https://docs.unity.com/en-us/mps-sdk/matchmaking/mm-rate-limits). Một số đo đơn giản trong bài thử trước vẫn nằm dưới giới hạn, nhưng dữ liệu nhiều vùng thật làm chuỗi vượt giới hạn và bị từ chối với BadRequest 21400.

## Sửa

- Gửi `relayRegions` (mảng chuỗi) và `relayLatencies` (mảng số), tối đa 100 phần tử; bỏ annotations không dùng khi chọn vùng.
- Mỗi tên vùng trong metadata dài tối đa 200 ký tự. Số đo trùng vùng được lấy trung bình, sắp theo ping; không đặt lại giới hạn 150 ms.
- Không gửi `qosResults` như điều kiện matchmaking. Đọc các mảng sau khi đủ người để chọn vùng Relay chung phù hợp; nếu không có dữ liệu thì Relay tự chọn.
- Không ghi `relayQosJson` vào vé mới. Giữ khả năng đọc định dạng cũ để tương thích với metadata của các máy dùng cùng protocol.
- Cấu hình queue trên dịch vụ không cần thay đổi.

## Xác minh

- Unity biên dịch thành công.
- 16/16 phương thức NUnit chạy trực tiếp trên DLL Unity biên dịch, bằng luồng nền: kiểm tra hủy/đo QoS, chọn vùng, chuỗi/mảng đúng giới hạn và trường hợp nhiều annotations dài. Đây không phải báo cáo hoàn tất Unity Test Runner.
- Vé live tạo bằng **chính `BuildTicketPlayers`**: 11 vùng đo thật và 30 vùng giả lập với annotations dài. Protocol riêng biệt giữ vé thử tách khỏi người chơi thật.
- `PickleVerse-1v1`: vé 1 người được tạo và trạng thái `InProgress`; 41 vùng, chuỗi cũ 16.820 ký tự, chuỗi mới dài nhất 28 ký tự, phần players dài 1.361 byte.
- `PickleVerse-2v2`: vé nhóm 2 người được tạo và trạng thái `InProgress`; 41 vùng mỗi người, chuỗi mới dài nhất 32 ký tự, phần players dài 2.725 byte.
- Hai vé đã được xóa sau thử. Kết quả: `MatchmakerPayloadTests-20261014.json`, `MatchmakerPayloadLive-20261014.json`.
- Log Console được lưu trước khi dọn trong `MatchmakerPayloadConsoleHistory-20261014.json`. Không sửa scene hoặc UI. Chưa build lại Windows hoặc chạy một trận trên hai máy trong lượt sửa này.
