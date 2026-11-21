# Sửa phản hồi HLV, sự kiện dedicated và camera — 05/11/2026

## Thay đổi

1. Phân tích phiên có `summary`: tổng lượt đánh, lần tiếp xúc, lượt giao và lỗi quan sát được kèm mẫu số. Phân biệt không ghi nhận lỗi với không có số liệu. Thêm phân tích đánh hụt/bóng nảy hai lần; giữ gate năm cơ hội cho gợi ý theo lỗi, prerequisite bài tập, timing/aim do game hỗ trợ, dink chưa hiệu chỉnh và ML mặc định tắt. Backend và Unity giữ trường mới; Axios có hợp đồng tương ứng. JSON đã lưu trước đó vẫn đọc được qua formatter dự phòng.
2. BotMatch lấy góp ý ngắn từ lỗi do SinglesRules xác nhận hoặc kết quả lượt bóng. Không phụ thuộc phiên telemetry, HTTP hay worker. Xếp góp ý khi bóng dừng, dựng UI ở LateUpdate; tự xóa khi chuẩn bị lượt mới. Góp ý không bật giữa rally.
3. Dedicated: chuẩn hóa nguồn gói reliable ở client thành authority server (`PlayerRef.None`); server giữ nguyên mã người gửi để kiểm tra quyền. Shared mode giữ kênh cũ. Không thay cấu trúc snapshot, vé hoặc protocol 18. Tiêu đề lỗi mới dùng tiếng Việt; client cũng dịch các nhãn OUT/FAULT/Side Out từ server protocol 18 cũ khi nhận.
4. `CourtFollowView` dùng chung camera gameplay của Training cho Training/BotMatch/MultiplayerCourt: cao 5,3 m, sau người chơi 4,25 m, FOV 56, hệ số bám 3; đảo theo bên sân. Đấu đôi dùng cao 6,5 m/FOV 64 và giữ giữa sân, cùng quy tắc Training. Online tiếp tục bám vị trí render được làm mượt. Intro/kết quả giữ camera cinematic riêng.

## Nguyên nhân dedicated đã tái hiện

Người dùng xác nhận chỉ thiếu động tác đánh bóng, cả hai máy, dedicated Docker Ubuntu tại endpoint được cấu hình trong BackendApiSettings. Prefab thật vẫn chuyển động khi sample riêng (22 xương thay đổi ở cả hai nhân vật).

Trong phiên Fusion Server + hai Client cách ly, gói `swing` tới client có callback player raw **3/4**, trong khi object state authority raw **0**. Trước sửa, bộ lọc `source != Object.StateAuthority` bỏ gói; cả hai bản sao giữ `PKV_Idle_Ready`, `Swinging=false`. Sau chuẩn hóa tại transport, cả hai phát `PKV_Groundstroke_BH`, `Swinging=true`. Bộ lọc quyền trên NetworkPlayer/NetworkMatch không bị bỏ đi. Thông báo lỗi, sound và react dùng cùng transport này.

Đây là kết quả đo SDK đang cài, không suy diễn chỉ từ tên tham số callback. Tài liệu tham khảo chính thức: [Photon PlayerRef/authority](https://doc.photonengine.com/fusion/v2/manual/playerref), [Photon data streaming](https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-streaming). Trang API mô tả callback chung; kết quả raw của SDK được kiểm tra trực tiếp.

## Kiểm thử và giới hạn

- Unity Editor 6000.6.2f1, CLI đã cập nhật beta.16 theo phê duyệt: **489/489 Editor tests**, gồm tám regression mới.
- Phiên Fusion thật cách ly: một Server, hai Client, prefab dedicated thật; nhận và phát swing ở cả hai bản sao. Đã shutdown toàn bộ runner thử. Đây chưa phải trận Ubuntu hai thiết bị hoàn chỉnh.
- Play Mode: góp ý bot hoạt động khi không có coaching session; handler thông báo của NetworkMatch hiển thị banner **LỖI** qua MultiplayerCourtUI/MatchAnnouncements thật; ảnh render 1920×1080 và phép đo text không tràn khung. Tổng kết sử dụng fixture để kiểm tra trình bày. Camera pose/FOV đúng cấu hình dùng chung. Không diễn giải fixture thành một trận đánh thật.
- Python **16/16**; gồm phiên sạch, phiên ngắn đánh hụt, giao bóng hụt, lỗi trọng tài chưa phân loại, phiên rỗng và các gate cũ. ML test dùng dữ liệu tổng hợp để kiểm tra pipeline, không bật model trong game.
- Backend solution build **0 lỗi/0 cảnh báo**; **34/34** kiểm tra API/architecture/wire qua HTTP loopback, có kiểm tra trường summary và session được giữ qua serializer/Unity ApiClient. Không dùng database.
- Frontend `npm run type-check` đạt.
- Windows development `0.1.0-gameplay.20261105` build cuối **0 lỗi, 2 cảnh báo** có sẵn về `SimulationMessagePtr` và `DEVELOPMENT_BUILD` bị deprecated trong SDK/Unity hiện tại. Client đã được mở chạy thật với GPU RTX 3050 Ti, nạp scene khởi động và không ghi nhận exception trong log; runtime Pipeline không bật ở Player nên không dùng phép đo này để khẳng định FPS hoặc nghiệm thu gameplay. Còn cảnh báo Curl/UnityTLS ở yêu cầu nền chưa định danh như bàn giao trước; không tắt xác minh TLS. Tiến trình thử được dọn riêng.
- Bằng chứng trong `artifacts/gameplay-fixes-20261105/`: `unity-editor-tests.json`, `python-tests.xml`, `dedicated-events-after.json`, `presentation.json`, `bot-feedback.png`, `online-fault-notice.png`, `session-analysis.png` và báo cáo build Windows. Probe có thể chạy lại bằng `My project/AgentScripts/GameplayRegressionProbe.cs`.

Các lần probe đầu có lỗi cấu hình kiểm thử (enum, synchronous spawn, scene disabled/missing); đã sửa probe và dọn bằng thoát Play Mode. Python trong sandbox bị treo ở bài training; chạy lại trên môi trường thật đạt. Không thay cấu hình TLS hoặc shader để che lỗi môi trường.

Tài liệu công cụ đã đối chiếu tại package Pipeline đang cài: `Documentation~/commands/scripts.md`, `commands/build-and-compilation.md`, `commands/editor-lifecycle-and-observability.md`, `commands/capture.md`. Lượt build Windows đầu báo Succeeded nhưng có MissingReferenceException tại `URPPreprocessBuild.LogIncludedAssets` do cache tham chiếu pipeline runtime đã hủy sau Play Mode. Đã dừng Play Mode và dispose cache bằng API công khai `UnityEditor.Rendering.CoreBuildData.instance.Dispose()` trước build lại; không sửa package hoặc thiết lập đồ họa. Báo cáo đầu giữ riêng ở `windows-build-first-pass.json`.

## Cập nhật môi trường thử

- Cần client mới trên **cả hai máy** để sửa việc nhận swing/thông báo từ dedicated. Bản Windows development trỏ endpoint hiện có trong BackendApiSettings, cho HTTP theo chính sách development hiện tại.
- Gói chạy thử: `artifacts/gameplay-fixes-20261105/PickleVerse-Windows-0.1.0-gameplay.20261105.zip`, kèm `.sha256`; giải nén toàn bộ rồi chạy `PickleVerse.exe`. Không đóng gói folder BackUpThisFolder hoặc metadata runtime Pipeline. Project giữ lại phiên bản và preloaded assets ban đầu sau build.
- Gói cuối 249.567.263 byte, 435 tệp; kiểm tra CRC ZIP và SHA-256 đạt: `c39b3c25cfb31a055dcf878f425facbae35d2c835f6e75807f24f9e200fca2cb`.
- Để nhận đủ thống kê mới, cần build/phát hành lại **cả API và AI image**; API cũ sẽ không giữ `summary` do AI mới trả. Client mới vẫn có formatter cho phân tích cũ.
- Native server mới có tiêu đề lỗi tiếng Việt; client mới vẫn dịch đúng nhãn của server protocol 18 đang chạy. Không bắt buộc thay native server để thử riêng bản sửa nhận animation/thông báo. Không có thay đổi schema, không reset database/migration. Không đổi protocol 18.
- Chưa push image mới lên Docker Hub, chưa điều khiển/cập nhật Ubuntu hoặc server người dùng. Release Docker Hub ngày 03/11 không tự chứa các bản sửa ngày 05/11.

Nghiệm thu tiếp theo trên hai máy: một trận bot có lỗi/hụt và tổng kết, dedicated 1v1 ở cả hai vai trò giao/nhận, thông báo lỗi lưới/ngoài sân, đổi bên và một trận doubles để xem camera. Đối chiếu phiên tổng kết đúng người/trận.

Cuối phiên, `Menu Vietnamese Bold.asset` có dữ liệu atlas/glyph động chưa commit. Bộ xét duyệt tự động từ chối thao tác dọn cache vì nguy cơ ghi đè thay đổi người dùng; file được giữ nguyên. Không vượt qua chặn hoặc khôi phục file này bằng Git. Lỗi online bổ sung chỉ có đuôi stack trace được người dùng yêu cầu bỏ qua, nên không suy đoán hoặc sửa theo log thiếu đó.
