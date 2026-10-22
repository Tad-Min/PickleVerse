# Tiến độ chuyển game sang online — cập nhật 21/10/2026

Phạm vi đã được người dùng duyệt: lấy dữ liệu prototype Unity làm dữ liệu gốc, chuyển tiến trình sang backend, mở lại các menu/gameplay, dùng Photon và lưu replay S3. Luồng multiplayer được chốt: tìm trận → tất cả người chơi xác nhận → tải sân → bắt đầu. Không triển khai Unity dedicated server riêng.

## Đã kiểm chứng

- `src/Database.sql` là file SQL duy nhất để khởi tạo database mới: chứa schema gốc, cấu hình OTP/role, schema online và seed Unity. `src/Seed/UnityCatalog.json` lấy từ Progression Config, Shop Catalog và TrainingCurriculum của Unity; `Tools/Build-UnityContentSeed.ps1` chỉ cập nhật khối seed được đánh dấu, giữ nguyên phần schema.
- Docker PostgreSQL đã có 10 achievement, 17 nhiệm vụ, 3 cosmetic, 12 lesson, 12 drill. Đã sao lưu trước khi áp dụng; tài khoản hiện có được giữ nguyên.
- API Docker `/api/Game/state`, claim, purchase, equip và training đã được build/chạy. JSON HTTP đã kiểm tra bằng tài khoản tạm.
- Backend dùng lại các thuật toán C# thuần của Unity để giữ điều kiện mở khóa, sao, XP và phần thưởng. Lệnh ghi có khóa theo người chơi và mã chống xử lý lặp.
- Bộ kiểm tra tổng hợp đã qua 115 kiểm tra trên PostgreSQL tạm: auth/OTP/JWT, seed, mở khóa bài, thưởng, mua đồng thời, kết quả bot, xác nhận và kết quả online, bạn bè và phân trang. Log: `artifacts/verification/full-online-checks.log`. Các sửa lời mời mới nhất được kiểm tra bổ sung riêng trong `social-checks.log`.
- Editor: đăng nhập bằng tài khoản tạm, NameTag mở hồ sơ; tab achievement có 10 mục; tiến trình có 12 bài học. Ảnh kiểm chứng trong `artifacts/verification/online-*.png`.
- Photon Fusion 2.1.3 đã được nhập từ gói người dùng tải. App ID đã cấu hình tại `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`.
- Kiểm tra Photon Cloud thật: hai runner cùng một phòng, đều thấy hai người, vùng Asia. Báo cáo `artifacts/verification/photon-cloud-check.json`.
- Hai client thật (Editor + Windows development) đã qua phòng riêng 1v1: cả hai xác nhận, tải sân, bắt đầu, di chuyển được xác nhận bằng state mạng, ghi replay và lưu rời trận. Báo cáo: `photon-gameplay-editor.json`, `photon-gameplay-windows.json`. Không có lỗi runtime mới trong ca đã đạt.
- Replay hiện dùng API/S3, không ghi save người chơi/replay xuống đĩa ở luồng chạy chính. Kiểm tra `photon-replay-check.json` đọc lại được pose xương, 61 frame trong chunk đầu và 4 chunk cho mỗi người của trận thật. Local repository còn lại chỉ là adapter cho kiểm thử định dạng cũ.
- Prefab NetworkMatch/NetworkPlayer đã được chuyển bằng Unity Editor API sang Fusion NetworkObject. Đã gỡ NGO, Unity Multiplayer Services và Multiplayer Center qua Package Manager; mã và test riêng của transport cũ được giữ dưới cờ `PICKLEVERSE_LEGACY_NGO`.
- API online dùng xác nhận từ JWT từng người, khóa hồ sơ theo thứ tự UUID, lấy tên/Elo/vợt từ DB; chỉ cấp thưởng kết quả bình thường khi mọi người gửi cùng tỉ số. Rời trận phải do chính tài khoản đó gửi. Heartbeat 8 giây và thời gian kết nối lại 35 giây phía backend; đọc lại hồ sơ sẽ đối soát trận bị bỏ dở.
- API bạn bè/bảng xếp hạng và menu đã được nối dữ liệu thật, hỗ trợ tìm tên/ID, phân trang, lời mời chờ chấp nhận và xóa bạn; không dùng danh sách DEMO ở luồng chạy chính.
- Windows build sau khi bỏ UGS/NGO đã thành công. Build Profile và danh sách scene chung có PhotonLobby, Training và Loading.
- Tìm trận công khai đã qua Editor/Windows: tự ghép trên Photon, cả hai xác nhận, tải sân, di chuyển, replay và lưu rời trận. Báo cáo `photon-public-editor.json`, `photon-public-windows.json`. Race HTTP/SceneDone đã được sửa và kiểm tra lại.
- 11 kiểm tra social bổ sung đã qua: ID lời mời mới, vô hiệu chấp nhận cũ sau xóa bạn, block, phân trang và tìm kiếm. API Docker đã được cập nhật sau kiểm thử.
- Tìm trận 2v2 với hai nhóm đôi đã qua bằng 1 Editor + 3 Windows client. Bốn báo cáo `photon-doubles-a.json` đến `photon-doubles-d.json` cùng match ID, đều có đủ 4 actor, điều khiển được xác nhận qua mạng, replay và lưu rời trận.
- Editor kiểm tra trực tiếp: nút NameTag mở hồ sơ, tab achievement 10 hàng; bảng xếp hạng có dữ liệu tài khoản thật; training có 12 hàng và 4 bài mở ban đầu, checkpoint serve tải lại khớp server; shop/bộ sưu tập dùng OnlinePlayerEconomy với 3 cosmetic; tutorial skip và rời trận bot đã lưu backend. `online-menu-check.json` và ảnh `online-*-final.png` ghi nhận kết quả.
- ReplayView đã tải chunk từ S3, phát, seek và đổi 2x trong Editor; playhead được xác nhận thay đổi trên frame thật.
- Kết nối lại đã qua Editor/Windows: client Windows ngắt runner giữa trận, vào lại cùng match ID, nhận lại điều khiển và lưu rời trận. `photon-reconnect-editor.json` và `photon-reconnect-windows.json` đều đạt; không có lỗi runtime mới. Recorder nối manifest và thời gian từ S3 sau khi scene cũ flush xong, không ghi đè các chunk trước.
- Replay sau nối lại đã được đọc qua API/S3: hai người đều có 15 chunk hoàn chỉnh và pose xương hợp lệ (`photon-replay-check.json`).
- 372/372 test Editor của project đã qua (`unity-editor-checks.json`).

## Trạng thái bàn giao dev

- Windows build cuối thành công, 0 lỗi: `artifacts/windows-photon/PickleVerse.exe`; tóm tắt `windows-build-check.json`. Warning còn lại là Pipeline không bật trong Player; đây là công cụ điều khiển Editor, không cần cho gameplay.
- Đã dọn bốn tài khoản thử, dữ liệu trận/tiến trình và object replay thuộc các UUID thử; các file chứa credential fixture cũng đã xóa. Giữ tài khoản và dữ liệu người dùng thật. `final-dev-health.json` ghi nhận trạng thái Docker/Swagger và số tài khoản sau dọn.
- Luồng tài khoản, ví/skin, nhiệm vụ/achievement, training, tutorial, trận bot, Photon 1v1/2v2, kết nối lại và replay dùng API/S3 ở runtime. Các adapter local còn trong source phục vụ kiểm thử định dạng/pure rules; lựa chọn đồ họa/âm thanh/phím vẫn là thiết lập riêng trên thiết bị.
- Editor được để ngoài Play Mode. Chạy Play, đăng nhập tài khoản thật để sử dụng; API dev tại `http://localhost:8080`, Swagger tại `http://localhost:8080/swagger/index.html`.

## Giới hạn mô hình dev

Fusion Shared dùng một client làm state authority cho luật/vật lý trận; Photon Cloud kết nối và đồng bộ. Backend kiểm tra đồng thuận/tính hợp lệ và tính thưởng, chưa phải cơ chế chống gian lận với mô phỏng trên dedicated server. Hai client thông đồng vẫn nằm ngoài bảo đảm hiện tại. Tài liệu chính thức: [Network topologies](https://doc.photonengine.com/fusion/v2/manual/network-topologies), [FixedUpdateNetwork trên proxy trong Fusion 2](https://doc.photonengine.com/fusion/v2/getting-started/migration/coming-from-fusion-v1).

Giữ trạng thái các file `_Recovery/*.unity` đã xóa trước đợt làm việc này. Không reset các sửa đổi có sẵn trong `Menu.unity` và các font về HEAD; các cache font do Unity tạo vẫn cần phân biệt với thay đổi mã sản phẩm khi người dùng commit.
