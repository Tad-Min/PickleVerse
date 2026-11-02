# Nền động Login và Loading — 02/11/2026

Đã thay nền cũ bằng toàn bộ 5 GIF mới do người dùng cung cấp. Luồng Login → Loading → Menu và các chức năng đăng nhập, đăng ký, OTP vẫn giữ nguyên.

| Nguồn người dùng | Sử dụng |
| --- | --- |
| `Assets/FrontEndContent/Login/Login.gif` | Nền động sau form Login/đăng ký/OTP |
| `Assets/FrontEndContent/Loading/Artwork/PKV_1.gif` | Loading: Bứt phá trên sân |
| `Assets/FrontEndContent/Loading/Artwork/PKV_2.gif` | Loading: Đón bóng. Tỏa sáng. |
| `Assets/FrontEndContent/Loading/Artwork/PKV_3.gif` | Loading: Sân đấu của bạn |
| `Assets/FrontEndContent/Loading/Artwork/PKV_4.gif` | Loading: Sẵn sàng cho cú đánh tiếp theo |

## Cách phát và tối ưu

- Giữ nguyên GIF gốc. `scripts/BuildFrontEndArtwork.py` tạo MP4 H.264 Baseline và poster JPG trong thư mục `Playback` cạnh các GIF. Không cần mạng khi chạy game.
- Tổng GIF: **35.806.542 byte**. Video: **2.549.849 byte**; poster: **1.149.973 byte**. Tài nguyên phát dẫn xuất tổng khoảng **3,70 MB**, giảm khoảng 89,7% so với GIF gốc. Hash nguồn và kích thước từng file nằm trong `artifacts/animated-artwork-20261102/conversion.json`.
- Bảo toàn thứ tự/disposal/timing GIF. Video 25 fps lặp các khung nguồn, không nội suy chuyển động. GIF gốc khoảng 12,5–16,7 fps; nhịp chuyển động nội dung độc lập với nhịp render UI/game. `PKV_3` được đưa từ chiều cao 675 xuống 674 pixel để tương thích H.264; sai số lượng tử thời gian tối đa một frame video.
- `CinematicArtwork` dùng VideoPlayer native, prepare bất đồng bộ, không có audio, dùng thời gian unscaled. Poster khung đầu hiển thị ngay; chỉ gán texture video sau callback khung hình đầu tiên. Sau đó tắt callback frame để tránh xử lý/upload texture từ C# liên tục. [Unity VideoPlayer API](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/video/videoplayer).
- Login chỉ dùng một bộ phát. Loading khởi đầu luân phiên giữa các artwork; phát lặp, chuẩn bị nền kế tiếp từ giây 6, chuyển sau ít nhất giây 7 bằng fade 0,85 giây tính từ khi nền kế tiếp sẵn sàng. Tối đa hai bộ phát trong lúc chuyển; bộ cũ dừng và bỏ tham chiếu clip sau fade.
- Disable/destroy dừng bộ phát. Có timeout 8 giây và poster dự phòng nếu codec/thiết bị không phát được. Texture poster không readable, không mipmap, dùng compression; nền không nhận raycast, không cản form.
- Cập nhật prefab `Resources/Loading/LoadingScreen.prefab`, scene Login và công cụ cài scene. Scene Loading đang dùng prefab này nên tự nhận đủ bốn nền mới. Đã kiểm tra không còn đường dẫn ảnh cũ trong code/scene/prefab.

## Kiểm thử đã chạy

- **10/10 asset checks**: startup Login, các nút/input auth còn đủ, mật khẩu che mặc định, form giữa màn hình, profile Menu giữ nguyên.
- **19/19 kiểm tra phát nền có GPU**: Login và cả bốn clip phát/loop, phát khi `timeScale = 0`, chuyển đủ bốn nền trong 36 giây, tối đa hai bộ phát, không còn decoder hoạt động sau destroy, không exception/video error/fallback warning.
- **48/48 E2E**: UI/controller/services/Unity HTTP transport thật với HTTP loopback fixture. Thành công đi đúng Login → Loading → Menu; render Menu dưới Loading trong khi auth chờ; chống click trùng; 100% sau data/render readiness; sai mật khẩu, OTP, mất kết nối, game-state retry và mất session trong warm-up đều xử lý được. Đây không phải kiểm thử PostgreSQL/SMTP thật.
- **7/7 SceneWarmupTests**: giữ điều kiện ổn định render trước reveal.

Game View render thật **1920×1080**, RTX 3050 Laptop, Unity 6000.6.2f1, uncapped; thống kê thời gian giữa các frame đã render, gồm chi phí Editor:

| Pha | Frame đo | p95 (ms) | Max (ms) | Frame >20 ms |
| --- | ---: | ---: | ---: | ---: |
| Login động | 809 | 4,969 | 21,957 | 1 |
| Login poster đối chiếu | 600 | 3,691 | 12,842 | 0 |
| Loading PKV_1 | 807 | 3,371 | 8,335 | 0 |
| Loading PKV_2 | 634 | 3,691 | 13,371 | 0 |
| Loading PKV_3 | 926 | 3,462 | 4,562 | 0 |
| Loading PKV_4 | 743 | 3,467 | 13,026 | 0 |
| Loading gồm crossfade | 12.000 | 3,895 | 15,980 | 0 |
| Menu sau reveal | 600 | 5,910 | 10,906 | 0 |

Không có frame >33,34 ms trong các pha này. Login có một frame Editor 21,957 ms, nên không khẳng định tuyệt đối không rớt FPS trên mọi frame/mọi máy. 60 frame đầu Menu max 10,478 ms. Screenshot và thao tác kiểm thử profile được tách khỏi phần đo Menu.

Lượt thử đầu phát hiện timeout bị kiểm tra nhầm khi bộ phát đã dừng; đã sửa bằng trạng thái yêu cầu phát, rồi chạy lại. H.264 Main ban đầu gây cảnh báo timestamp trong Unity; đã đổi Baseline/B-frame 0 và xác nhận lại. Bản Windows đầu phát hiện metadata màu chưa chỉ định; đã encode/tag BT.709/range limited rồi build và kiểm tra lại. Lượt thất bại ban đầu được giữ trong `playback-first-pass-timeout.json`, không dùng làm kết quả bản cuối; các phép đo Editor trong bảng ở trên dùng bản Baseline trước thay đổi metadata màu, runtime/timing không đổi.

## Windows development với asset cuối

Build cuối thành công, **0 lỗi**, 32,02 giây (incremental). Probe xác nhận Login và Loading đã có texture video, số frame video thay đổi thực, đủ 4 clip Loading được đóng gói. Đã xem cả hai ảnh render thực. Log cuối không có exception, cảnh báo timeout/fallback video, timestamp hoặc color primaries.

RTX 3050 Laptop/D3D12, target render **1920×1080**, uncapped, 600 frame mỗi pha, warm-up 2 giây và một pixel GPU readback để bảo đảm việc render hoàn tất:

| Pha | p95 toàn frame (ms) | p95 render đồng bộ (ms) |
| --- | ---: | ---: |
| Login động | 8,494 | 7,252 |
| UI ẩn đối chiếu | 5,912 | 5,087 |
| Loading động | 7,619 | 6,479 |

Đây là render ngoài màn hình có readback, không phải FPS trình chiếu cửa sổ hay GPU-only time. Counter GPU frame/draw trong chế độ cửa sổ ẩn không hợp lệ nên không dùng để kết luận. GC median/p95 cùng mức 34/51 byte giữa cả ba pha, không cho thấy tăng allocation theo frame do nền động. Đây cũng không phải phép đo hiệu suất gameplay hoặc bài test quá trình tải asset nặng của trận đấu.

Build benchmark chỉ chứa scene Login và dùng prefab Loading từ Resources; log có cảnh báo shader hậu kỳ URP bị strip và kết nối dịch vụ telemetry Unity khi offline. Các ảnh UI/nền phát đúng; không thay cấu hình hậu kỳ hoặc dịch vụ của game để xử lý cảnh báo riêng của benchmark này.

PlayerSettings đã khôi phục chính xác sau build, SHA-256 trước/sau đều `3C5A002983580BE0AF57CC9DE934A2003A0CC64F4A6CA835EC00CA7F0774DF1A`. Editor đã trở về scene Training, một scene, không dirty, không Play/compile. Hash cả năm GIF gốc không đổi. Dependency graph Login/Loading chứa đủ 5 MP4, không có GIF gốc được tham chiếu vào runtime.

## Bằng chứng và chạy lại

Thư mục `artifacts/animated-artwork-20261102/`:

- `conversion.json`, `video-streams.json`, `asset-checks.json`, `playback.json`, `warmup-tests.json`.
- `login-animated.png`, `loading-animated.png`: ảnh chụp nền video thực trong Game View.
- `e2e/e2e.json`, `e2e/loading-parallel.png`, `e2e/menu-ready.png`.
- `windows/player-build.json`, `windows/performance.json`, `windows/player.log`, `windows/PlayerLogin-1920x1080.png`, `windows/PlayerLoading-1920x1080.png`.

Trong Editor có render, gọi `LoginLoadingVerification.Prepare(outputDirectory)` để lưu setup scene/Game View và vào Play ở 1080p, sau đó `AnimatedArtworkVerification.Begin()` hoặc `LoginLoadingVerification.Begin()`. Dừng Play rồi gọi `LoginLoadingVerification.Restore()` để khôi phục setup. Các helper chỉ chạy khi gọi tường minh; probe Windows chỉ hoạt động trong development build với `--login-benchmark`, bị loại khỏi release.

Chưa kiểm thử trên mobile/WebGL hoặc bản release với backend thật; số đo này không thay thế kiểm tra thiết bị đích.
