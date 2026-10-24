# Console và chữ tiếng Việt — 24/10/2026

## Phạm vi đợt này

Unity Editor 6000.6.2f1, Windows; backend ASP.NET local, PostgreSQL, Redis.
Đợt này xử lý Console và dữ liệu tiếng Việt trước Replay và tính năng phòng, theo thứ tự đã thống nhất.

## Nguyên nhân và thay đổi

- Hai `NullReferenceException` cùng xuất phát từ `PlayerProgressionMenu.Close()`: AccountScreen chạy ở BeforeSceneLoad, tìm được menu đã deserialize nhưng menu chưa chạy Awake để gắn `UiModalPresentation`. Close giờ đóng modal an toàn cả trước và sau Awake.
- PostgreSQL lưu ký tự `?` thật, không phải glyph bị thiếu: ví dụ byte UTF-8 của `serve` chứa `3f3f`. Cả 12 lessons, 17 missions và 10 achievements bị ảnh hưởng; `src/Database.sql` vẫn có Unicode đúng.
- `scripts/Repair-CatalogText.ps1` đọc UTF-8 nghiêm ngặt từ Database.sql, tạo SQL sửa riêng text bị hỏng trong sáu bảng catalog, sao lưu trước khi chạy và thực hiện trong transaction. Script chuyển file qua docker cp / psql --file để không qua chuyển mã của shell. Các trường config gameplay và văn bản hợp lệ đã chỉnh riêng được giữ nguyên. Không chạy lại schema, không reset dữ liệu người chơi.
- Đã áp dụng phục hồi cho PostgreSQL local. Bản sao lưu: `UnityWork/CatalogTextRepair/before-d37bd1733f85479baef1ba7146430b97.sql`.
- `EquippedPaddle` đợi authenticated inventory sẵn sàng, dùng sự kiện Changed để cập nhật trang bị sau đăng nhập; không còn gọi API trước đăng nhập.
- Modal toàn màn hình dùng camera chỉ xóa nền trong lúc camera 3D tạm dừng, tránh lớp cảnh báo No cameras rendering trong Editor. Camera 3D được khôi phục khi đóng modal.
- `LobbyNavigation` bàn giao AudioListener qua sceneLoaded để giữ một listener trong lúc kích hoạt scene mới, loại bỏ log thiếu listener giữa các frame chuyển trang.

## Kết quả kiểm chứng

- Biên dịch Unity thành công, không có lỗi C#.
- FrontEnd lifecycle: 4/4 tests đạt, gồm Close trước Awake và camera modal/khôi phục camera.
- Shop: 33/33 tests đạt, gồm bật trang bị trước khi authenticated inventory sẵn sàng.
- Catalog text repair: 4/4 checks đạt trên PostgreSQL tạm; phục hồi đủ sáu catalog, giữ text hợp lệ/config riêng/dữ liệu người chơi, chạy lại không đổi dữ liệu đã tốt, API đọc đúng dấu.
- Online game integration: 76/76 checks đạt, gồm SQL seed, Unicode qua PostgreSQL/JSON, progression, replay repository, multiplayer và social hiện có. Những check replay/storage trong bộ này dùng storage giả lập, không phải kiểm tra S3 thật.
- Kiểm tra trực tiếp tài khoản đang có trong Unity: tiến trình luyện tập, nhiệm vụ ngày/tuần và 45 nhãn trong Training không còn chuỗi hỏng hoặc glyph tiếng Việt bị thiếu.
- Đã thử Menu → Training → ModeSelection → Menu sau bản sửa audio: 0 Error/Exception, không có log thiếu AudioListener hoặc warning trang bị chưa đăng nhập.
- dotnet build bộ kiểm thử backend thành công; vẫn có 50 cảnh báo nullable hiện hữu trong các model/logic dùng chung, tách biệt với Unity Console. Các file đó chưa thay đổi trong đợt này.

## Warning môi trường còn lại

Visual Studio messaging không bind được UDP 56594, SocketError AccessDenied / native 10013.
Windows đang loại trừ dải UDP 56522–56621; trước khi khởi động lại Editor, UDP 56342 cũng nằm trong dải 56302–56401.
Package Visual Studio tính messaging port bằng `56002 + (PID % 1000)`, nên khởi động lại Editor không bảo đảm giải quyết lâu dài.
Người dùng xác nhận đang dùng Visual Studio; giữ nguyên tích hợp/debugger. Chưa chỉnh dải cổng hoặc restart dịch vụ mạng Windows/Docker.
Không coi Console đã sạch hoàn toàn khi warning môi trường này còn tồn tại.

## Các bước tiếp theo đã thống nhất

1. Replay: nút trong lịch sử → phát trọn trận, pause, tua, tốc độ, tỉ số, quay lại lịch sử; kiểm tra S3 thật và phân biệt dữ liệu đã bị reset với lỗi lưu/đọc.
2. Fusion Custom 1vs1/2vs2, nhóm online 2vs2; chọn/đổi đội, Ready, kick, mã phòng có thể đổi; tham gia bằng mã không mật khẩu.
3. Lời mời bạn bè trong lobby có chấp nhận/từ chối; chuyển chủ khi host rời, mất mạng ở phòng chờ thì rời phòng, trong trận hỗ trợ kết nối lại.
4. Có thể kiểm tra nhiều client trên cùng Windows bằng bốn tài khoản test được người dùng cung cấp; không lưu thông tin đăng nhập trong tài liệu hay source.
