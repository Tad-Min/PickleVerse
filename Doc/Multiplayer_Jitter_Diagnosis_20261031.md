# Chẩn đoán giật chuyển động 1v1 — 31/10/2026

Người dùng xác nhận thử trên hai máy khác nhau: nhân vật hoặc bóng giật/dịch chuyển, giao diện vẫn mượt. Đây là audit source hiện tại (Unity 6000.6.2f1, Fusion 2.1.3), chưa phải đo đạc trên hai máy gặp lỗi.

## Các bằng chứng trong code

| Vị trí | Quan sát | Tác động có thể có |
|---|---|---|
| PhotonSession.cs, StartAsync/JoinAsync | Cả hai dùng GameMode.Shared | Photon Cloud chuyển/phân phối trạng thái; không chạy mô phỏng C# thay cho client |
| CourtBootstrap.cs và NetworkMatch.cs | Shared master tạo match, match tạo các player; IsServer là HasStateAuthority | Trong dự án, một máy người chơi mô phỏng bóng và nhân vật; input người kia đi qua cloud tới máy đó |
| PhotonCommandChannel.cs, FixedUpdateNetwork | CollectLocalInput và dự đoán vị trí chạy theo tick mạng | Root di chuyển theo tick; phần hình ảnh cần nội suy riêng để mượt ở tốc độ render cao hơn |
| NetworkPlayer.cs, Reconcile | Sau khi bỏ input đã ack và replay input còn lại, gán trực tiếp transform.position | Chênh lệch giữa dự đoán và state nhận được hiện ngay thành kéo giật vị trí; chưa có lớp làm mượt hiệu chỉnh hình ảnh |
| NetworkPlayer.cs, Update | Nhân vật từ xa Lerp tới Frame.position mới nhất | Chưa dùng hai snapshot cùng thời điểm render để hấp thụ biến động thời gian gói tin |
| NetworkMatch.cs, LateUpdate | Ngoại suy bóng tối đa 0,08 giây rồi SetBallPosition trực tiếp | Snapshot cũ hơn giới hạn có thể làm bóng chậm/đứng rồi nhảy khi snapshot mới tới; đổi hướng ở va chạm cũng cần xử lý tính liên tục |
| PhotonCommandChannel.cs và NetworkPlayer.Photon.cs | Lệnh đánh/vung/chạm bóng đi qua JSON Outbox mạng và ack; Render parse các Outbox | Có thêm lịch chờ gửi/nhận và cấp phát JSON. Mức tác động cần đo, không kết luận đây là nút thắt chính chỉ từ source |

MultiplayerSettings.photonRegion mặc định `asia`; chưa xác nhận region/RTT thực tế của hai bản game người dùng đang chạy. NetworkConditions.Enabled=false trong config source, nên không có bằng chứng source đang chủ động giả lập trễ/mất gói. Không suy ra tick rate thực tế từ các số 0 trong TickRateSelection.

## Kiểm tra tiếp để xác định nguyên nhân thực tế

Đo đồng thời trên hai máy: version/build đang chạy, Photon region, vai trò state authority, FPS/frame time, RTT, jitter/mất gói, tuổi snapshot bóng, độ lớn correction vị trí và số lệnh chờ Outbox. Đổi người tạo phòng giữa hai lượt để xem hiện tượng đi theo vai trò authority hay theo thiết bị/đường mạng. Log Player hiện có ở máy làm việc chỉ là bản cũ, không cung cấp số đo của trận đang được báo lỗi.

Ưu tiên cải thiện nội suy snapshot của nhân vật/bóng và tách correction hiển thị khỏi state mô phỏng. Sau đó đánh giá cách vận chuyển lệnh đánh bóng và so sánh bằng số đo ở các điều kiện mạng khác nhau. Shared Mode vẫn có thể dùng; thay topology hoặc tăng tick rate riêng lẻ không thay cho kiểm tra/làm mượt netcode.

Tham khảo chính thức: [Fusion 2 network topologies](https://doc.photonengine.com/fusion/v2/manual/network-topologies), [snapshot interpolation](https://doc.photonengine.com/fusion/v2/manual/advanced/network-buffers), [controller code và các timeframe](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/networked-controller-code). Không có thay đổi runtime hoặc phép đo hiệu năng nhiều máy trong lượt audit này.
