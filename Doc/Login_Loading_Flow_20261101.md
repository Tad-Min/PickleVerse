# Login → Loading → Menu — 01/11/2026

## Hành vi đã hoàn tất

Sau khi kiểm tra các ô bắt buộc, nút **Đăng nhập** mở scene Loading. Box trạng thái “Sẵn sàng vào sân/chờ vào sân” đã bỏ khỏi AccountScreen. Đăng ký, xác thực OTP và gửi lại OTP giữ chức năng hiện có.

Loading hiển thị trước khi gửi yêu cầu xác thực. Xác thực/tải game state chạy song song với tải additive Menu; camera Menu thực sự render dưới Loading khi còn chờ API. Sau khi dữ liệu hợp lệ, Menu trở thành scene active, các listener của Menu (nhạc, tài khoản, hồ sơ) được gắn một lần. Loading tiếp tục che Menu trong lúc ổn định các frame render.

Thanh tải đi 0–100% theo các giai đoạn:

- 0–80%: tiến trình tải scene và các mốc xác thực, hồ sơ/game state; không giả định phần trăm byte của HTTP.
- 82–97%: chờ cửa sổ frame Menu đã render ổn định bằng SceneWarmup. Nguồn mẫu là callback render camera Menu thực, không phải số lượt Update.
- 100%: cả dữ liệu và render đã sẵn sàng; hiển thị đủ 100% rồi fade lớp Loading và mở input.

Trong lúc tải, khóa input qua FrontEndModalInput và tạm dừng EventSystem; giữ camera, animation và vật liệu Menu hoạt động để làm nóng. backgroundLoadingPriority = Low trong flow và khôi phục khi kết thúc. Dùng generation của flow để tác vụ cũ không cleanup nhầm flow mới.

## Lỗi và thử lại

Sai mật khẩu, mất mạng, dữ liệu lỗi hoặc phiên mất hiệu lực lúc warm-up đều trở về Login, giữ email và thông báo lỗi; không để scene Menu hoặc khóa input bị sót. Email chưa xác thực mở form OTP. Nếu xác thực đã thành công nhưng game state lỗi, nút đăng nhập có thể thử lại bằng phiên đó mà không gửi mật khẩu lại. Mật khẩu được xóa khỏi input khi bắt đầu flow và không lưu ra file/PlayerPrefs.

Không dùng allowSceneActivation=false cho Menu trong flow này: Unity không hủy được operation đó; để activation hoàn tất tránh chặn hàng đợi khi cần quay về Login vì lỗi. Loading luôn che và chặn tương tác trong thời gian này. Có timeout 90 giây cho chuẩn bị/render để xử lý trường hợp không thể sẵn sàng.

## Kiểm thử và hiệu suất

- 48/48 kiểm tra E2E trong Game View render thật ở 1920×1080.
- 24/24 regression AccountService/API client.
- 7/7 kiểm tra cửa sổ frame warm-up: frame spike, thiết bị chậm, 30/60 FPS, giá trị không hợp lệ.
- E2E đi qua UI/controller, UnityApiTransport, ApiClient, AccountService và HTTP fixture loopback. Không gửi email hoặc ghi database thật. Docker daemon của phiên này chưa hoạt động, nên chưa chạy lại E2E PostgreSQL/SMTP thật.

E2E bao gồm: validation, Login → Loading → Menu, Menu render trước khi API xác thực chậm hoàn tất, chống bấm trùng, 100% đúng thời điểm, mở lại input, đổi nhân vật, logout, sai mật khẩu, OTP, lỗi kết nối, game-state failure, retry dùng lại phiên và mất phiên trong lúc warm-up. Thành công chỉ mở Menu sau khi renderer và dữ liệu cùng sẵn sàng; thất bại không để overlay/scene/input khóa bị rò rỉ.

Máy đo: NVIDIA GeForce RTX 3050 Laptop GPU, Unity Editor Game View 1080p, không giới hạn FPS. Lấy đúng 600 frame đầu sau khi Loading mở Menu, trước thao tác chụp ảnh, đổi nhân vật và kiểm tra profile:

| Mẫu | p50 | p95 | p99 | Max |
| --- | ---: | ---: | ---: | ---: |
| 600 frame sau reveal | 7,315 ms | 10,343 ms | 12,392 ms | 19,019 ms |
| 60 frame đầu | 8,153 ms | 11,403 ms | 15,801 ms | 15,801 ms |

Không có frame vượt 20 ms trong khoảng 600 frame. p95 nằm dưới ngân sách 16,67 ms cho mục tiêu 60 FPS. Đây là số đo Editor trên máy hiện tại, chưa là kiểm thử Windows/mobile release hoặc cam kết mọi thiết bị không bao giờ khựng. Báo cáo giữ cả mẫu thô để đánh giá đuôi phân phối; lần đo đầu có chụp ảnh/thao tác test lẫn vào khoảng đo được lưu riêng và không dùng làm kết luận hiệu suất.

Báo cáo/ảnh: `artifacts/login-loading-20261101/e2e.json`, `warmup-tests.json`, `account-tests.json`, `loading-parallel.png`, `menu-ready.png`.

## Mã chính và tái chạy

- `Assets/Scripts/FrontEnd/AccountScreen.cs`: bỏ Page.Sync, chuyển login qua flow chuẩn bị; bind Menu trên activeSceneChanged khi tải additive; giữ logic lỗi/OTP/retry.
- `Assets/Scripts/Settings/SceneLoading.Prepared.cs`: chuẩn bị song song, camera render warm-up, rollback, khóa/khôi phục input và quyền sở hữu flow.
- `Assets/Scripts/Settings/SceneLoading.cs`: dùng chung cleanup/priority, giữ các flow local/Photon hiện có.
- `Assets/Scripts/Settings/LoadingScreenView.cs`: nhãn sảnh Menu.
- `Assets/Editor/Auth/LoginLoadingVerification.cs`: E2E với Game View 1080p, tự lưu scene setup/chọn kích thước cũ.

```text
# Unity Editor không headless, dừng Play và lưu scene đang sửa:
unity command eval 'return PickleVerse.EditorTools.LoginLoadingVerification.Prepare();'
# Sau khi Play đã khởi động:
unity command eval 'return PickleVerse.EditorTools.LoginLoadingVerification.Begin();'
# Chờ e2e.json ghi kết quả; dừng Play rồi:
unity command eval 'return PickleVerse.EditorTools.LoginLoadingVerification.Restore();'
```

Không thay backend, schema, hợp đồng JSON, scene/prefab nghệ thuật hoặc lựa chọn nhân vật của Menu.
