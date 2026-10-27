# Sửa lỗi vào trận online tải nhầm Replay và Settings

Ngày 27/10/2026. Phạm vi thay đổi: client Unity; không sửa `src/Backend`, `src/Frontend`, database hoặc cấu hình máy chủ.

## Nguyên nhân

Luồng tạo/join Photon dùng `SceneRef.FromIndex` cho PhotonLobby; chuyển vào sân dùng tên scene qua `NetworkSceneManagerDefault`, cũng được chuyển thành build index. Danh sách global, danh sách trong profile MultiplayerWindows và các bản build cũ không cùng thứ tự. Cùng số thứ tự có thể chỉ đến scene khác trên máy nhận, làm xuất hiện UI Replay phía sau xác nhận hoặc tải Settings thay vì sân đấu. Settings không có camera sân nên Unity có thể hiện “No cameras rendering”.

Kiểm thử còn phát hiện một khác biệt trong Editor: chỉ số danh sách scene đã lọc của Fusion có thể khác danh sách `SceneUtility` khi có scene disabled. Vì vậy không đổi số thứ tự trở lại đường dẫn; resolver mới tìm trực tiếp đường dẫn chuẩn.

## Thay đổi

- Thêm `PhotonSceneManager`, kế thừa scene manager có sẵn của Fusion. Scene trên mạng được định danh bằng `SceneRef.FromPath` với đường dẫn chuẩn; Fusion tìm scene trong danh sách cục bộ khi tải. Không sửa SDK Photon hoặc file scene.
- Cả tạo và join phiên sử dụng manager này. PhotonLobby ban đầu và MultiplayerCourt sau xác nhận đều dùng đường dẫn chuẩn.
- Bổ sung kiểm tra PhotonLobby có trong cấu hình scene.
- Tăng network protocol từ 14 lên 15, đồng thời thêm hậu tố protocol vào bản sao Photon AppVersion khi kết nối. Bản cũ dùng index không vào cùng phiên với bản mới dùng path. Không ghi đè PhotonAppSettings trên đĩa.
- Không thêm vòng quét scene/UI, `Update`, hiệu ứng hoặc tải texture mỗi frame. Việc tìm đường dẫn chỉ diễn ra khi Fusion cần giải quyết định danh scene.

## Kiểm thử

| Kiểm tra | Kết quả |
|---|---|
| Unity compile | Thành công, không lỗi biên dịch ở bản cuối |
| Test hồi quy định danh scene | 6/6 đạt |
| Toàn bộ assembly PickleVerse.Multiplayer.Tests, gồm ready check, room và scene warmup | 92/92 đạt, không bỏ qua |
| Hai client Photon thật, 3 vòng tạo/join → hai bên xác nhận → loading → sân | 3/3 đạt |
| Định danh PhotonLobby/MultiplayerCourt, camera mỗi client | Đúng path, mỗi client có một camera sân |
| UI Replay/Settings sau loading | Không xuất hiện trong cả 3 vòng |
| Kết thúc phép thử | Không còn runner, loading, Replay hoặc Settings bị sót |
| Git whitespace và phạm vi backend/frontend | `git diff --check` đạt; backend/frontend không thay đổi |

Smoke test mạng dùng hai NetworkRunner qua Photon Cloud trong một Editor. Để chỉ kiểm tra vận chuyển scene/rendering, bộ thử dùng cấu hình matchmaking tạm trong bộ nhớ, tạm tách callback chuyển scene của cổng đăng nhập và không chạy bootstrap ghi kết quả trận. Mọi trạng thái tạm được khôi phục; các runner/phòng thử được đóng. Không tạo tài khoản, đăng nhập bằng tài khoản người dùng, tìm trận xếp hạng hay ghi kết quả/Elo lên backend. Đây không phải kiểm thử trọn trận trên hai máy vật lý.

Sau loading, mỗi vòng đo 577 khung hình trong khoảng 4 giây, chỉ hiển thị một sân như một client thông thường:

| Vòng | Median ms | P95 ms | Max ms |
|---|---:|---:|---:|
| 1 | 6,94 | 7,06 | 11,98 |
| 2 | 6,94 | 7,08 | 13,51 |
| 3 | 6,95 | 7,09 | 17,83 |

Đây là phép đo hiển thị scene sân trong Editor, chưa bao gồm đầy đủ nhân vật/rally và chưa đại diện FPS trên điện thoại hoặc hai máy người chơi. Không khẳng định mọi khung hình luôn trên 60 FPS. Sau thử nghiệm đã dừng Play và mở lại Menu như trước.

Kết quả cục bộ: `UnityWork/online-scene-identity-tests.json`, `UnityWork/online-multiplayer-suite.json`, `UnityWork/OnlineSceneFix/live-scene-flow.json`. Script kiểm thử tạm nằm ngoài Assets để không vào game/build.

## Khi thử trên hai máy

Hai máy phải pull và build lại bản có protocol 15. Không dùng lại executable/APK cũ cho một trong hai máy; chúng được tách hàng chờ/phiên để tránh lỗi trộn giao thức. Sau cập nhật, kiểm tra lại tìm trận và vào sân 1v1 trên hai thiết bị thực tế. Không cần thay backend cho bản sửa này.
