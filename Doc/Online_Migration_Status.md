# Tiến độ chuyển game sang online

Phạm vi đã được người dùng duyệt: lấy dữ liệu prototype Unity làm dữ liệu gốc, chuyển tiến trình sang backend, mở lại các menu/gameplay, dùng Photon và lưu replay S3. Luồng multiplayer được chốt: tìm trận → tất cả người chơi xác nhận → tải sân → bắt đầu. Không triển khai Unity dedicated server riêng.

## Đã kiểm chứng

- `src/Seed/UnityCatalog.json` lấy từ Progression Config, Shop Catalog và TrainingCurriculum của Unity; `Tools/Build-UnityContentSeed.ps1` sinh phần schema bổ sung và seed cuối `src/Database.sql`.
- Docker PostgreSQL đã có 10 achievement, 17 nhiệm vụ, 3 cosmetic, 12 lesson, 12 drill. Đã sao lưu trước khi áp dụng; tài khoản hiện có được giữ nguyên.
- API Docker `/api/Game/state`, claim, purchase, equip và training đã được build/chạy. JSON HTTP đã kiểm tra bằng tài khoản tạm.
- Backend dùng lại các thuật toán C# thuần của Unity để giữ điều kiện mở khóa, sao, XP và phần thưởng. Lệnh ghi có khóa theo người chơi và mã chống xử lý lặp.
- 33 kiểm tra game qua PostgreSQL tạm: seed, mở khóa, thưởng lần đầu, mua đồng thời, idempotency và kết quả bot. 43 kiểm tra auth cũ cũng đã qua trước khi bổ sung các kiểm tra game.
- Editor: đăng nhập bằng tài khoản tạm, NameTag mở hồ sơ; tab achievement có 10 mục; tiến trình có 12 bài học. Ảnh kiểm chứng trong `artifacts/verification/online-*.png`.
- Photon Fusion 2.1.3 đã được nhập từ gói người dùng tải. App ID đã cấu hình tại `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`.
- Kiểm tra Photon Cloud thật: hai runner cùng một phòng, đều thấy hai người, vùng Asia. Báo cáo `artifacts/verification/photon-cloud-check.json`.

## Đang triển khai, chưa xác nhận hoàn tất

- Thay multiplayer NGO/Unity Services bằng Fusion và kiểm thử luồng xác nhận/tải sân/gameplay với hai client. SDK kết nối thành công chưa phải là kiểm thử gameplay.
- Nối hoàn chỉnh kết quả bot và multiplayer vào API, history và phần thưởng. API bot mới hiện có trong source; cần build Docker lại sau khi hoàn tất các endpoint còn lại.
- Chuyển repository replay sang S3; hiện vẫn còn repository local trong source cũ, chưa được coi là hoàn tất online-only.
- Nối danh sách bạn bè/bảng xếp hạng thật; menu social còn bị khóa để không hiển thị người mẫu như tài khoản thật.
- Kiểm thử training end-to-end sau khi chuyển service, toàn bộ menu, Windows build và hai client.
- Dọn tài khoản kiểm thử `artifacts/unity-account-fixture.json` sau khi kiểm thử xong; không xóa tài khoản người dùng.

Các file `_Recovery/*.unity` đã bị xóa trước đợt làm việc này và phải giữ nguyên trạng thái đó. Các file font cache phát sinh khi Play cần kiểm tra/dọn riêng, không gộp thành thay đổi sản phẩm.
