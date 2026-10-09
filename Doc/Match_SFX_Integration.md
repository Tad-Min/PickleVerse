# Tích hợp SFX trận đấu

Ngày: 09/10/2026. Nguồn do người dùng cung cấp: `My project/Music/Sound EFX`.

## Ánh xạ đang dùng

| File | Thời điểm phát |
|---|---|
| `smash-normal.wav` | Vợt chạm bóng khi Drive/Dink/Volley, đánh chéo I và các combo không bật tăng lực |
| `smash-serve.wav` | Chạm bóng giao đầu; được ưu tiên riêng thay vì âm đánh thường/mạnh |
| `smash-strong.wav` | Chạm bóng với modifier tăng lực U |
| `Ma_Sat_Giay.wav` | Bắt đầu chạy và các nhịp chân khi đang chạy thực sự |
| `foot-step.wav` | Các nhịp bước khi đi bộ, kể cả cinematic hai nhân vật đi vào sân |
| `Countdown.wav` | Một nhịp mỗi giây của thời hạn giao bóng, tổng 10 nhịp từ 10 đến 1 |
| `Coi.wav` | Khi luật trận xác nhận lỗi; một tiếng cho mỗi sự kiện lỗi |
| `Coi_Large.wav` | Kết thúc game, kết thúc trận và đổi bên giữa game quyết định |

Cú đánh phát âm theo **hành động gameplay**, không theo ký tự bàn phím cố định. Đổi phím trong Settings hoặc dùng nút cảm ứng vẫn cho cùng âm thanh. Chỉ phát tiếng chạm khi có contact; cú vung hụt không tạo tiếng đập bóng giả. Nhân vật người chơi và bot dùng chung ánh xạ.

Âm va chạm và bước chân có vị trí 3D, mono, không Doppler. Còi và đồng hồ là âm thông báo 2D để nghe rõ khi camera đổi góc. Pool có 12 voice tại sân + 4 voice thông báo; không tạo/xóa AudioSource khi đánh bóng. Âm thông báo có ưu tiên cao hơn bước chân.

## Chuẩn bị tài nguyên và độ trễ

Chỉ tám file được chọn được đưa vào `Assets/Audio/SFX`. File gốc trong Music được giữ nguyên. Các bản dùng trong game là WAV PCM mono, đã bỏ phần chuẩn bị/im lặng trước sự kiện chính:

| File | Gốc | Phần đầu bỏ | Bản game |
|---|---:|---:|---:|
| smash-normal | 0,300 s | 0 ms | 0,300 s |
| smash-serve | 0,700 s | 266 ms | 0,434 s |
| smash-strong | 0,400 s | 53 ms | 0,347 s |
| Ma_Sat_Giay | 0,290 s | 118 ms | 0,173 s |
| foot-step | 0,800 s | 391 ms | 0,409 s |
| Countdown | 14,000 s | 0 ms | một nhịp 0,920 s |
| Coi | 2,479 s | 632 ms | 0,988 s |
| Coi_Large | 1,816 s | 21 ms | 1,667 s |

Countdown gốc gồm chuỗi nhịp và phần đuôi dài. Dùng một nhịp ngắn phát theo đồng hồ simulation giúp nó dừng đúng lúc giao bóng, lỗi, time-out hoặc tạm dừng; không chạy lệch khi đồng hồ dừng. Không phát dồn các giây bị bỏ qua nếu có một frame chậm.

Importer của cả tám clip đã được đọc lại xác nhận: **PCM, Decompress On Load, Preload Audio Data bật, Load In Background tắt, một kênh**. Tổng dữ liệu PCM của bản game khoảng **496 KB**; bộ nhớ clip được Unity báo trong Editor là **550.962 byte**. Tần số gốc 48 kHz được giữ, riêng Countdown 44,1 kHz. Thư viện `Assets/Resources/Audio/Match SFX.asset` được nạp trước scene đầu tiên.

DSP buffer trên Windows kiểm thử giảm từ **1024 xuống 256 mẫu**, 4 buffer ở 48 kHz. Một block tương ứng khoảng 5,33 ms; tổng dung lượng 4 block khoảng 21,33 ms. Đây là cấu hình đệm, **không phải phép đo độ trễ từ tay bấm đến loa**: hệ điều hành, thiết bị xuất âm và Bluetooth còn thêm độ trễ riêng.

Runtime gọi Play ngay tại sự kiện contact/timer/fault, không đọc file, giải nén, tạo voice mới hay dùng coroutine đợi phát tiếng chạm. Thời gian vợt đến bóng vẫn theo animation/gameplay hiện có.

## Âm lượng và vòng đời

- Thanh **Tổng** tiếp tục điều khiển AudioListener.
- Thanh **Hiệu ứng** đã nối vào SFX; xem trước, lưu và hoàn tác đều cập nhật cả voice đang phát.
- Thanh **Nhạc** vẫn lưu mức; chưa thêm nhạc nền trong yêu cầu này.
- Mở Settings trong đấu bot dừng tiếng đang phát và giữ đồng hồ; tiếp tục không phát lại giây đã nghe.
- Khi đổi scene, SFX cũ dừng. Chỉ một service tồn tại qua các scene; service không tạo AudioListener hay camera thứ hai.
- Chạy dedicated/headless batch không tạo hệ thống xuất âm.

## Các luồng được tích hợp

`MatchActor.ConsumeContact` phát âm contact và có bảo vệ chống phát lặp. `MatchActor.Step` / `RenderNetworkPose` dùng MovementFoley để phát bước theo quãng đường thực đi, không theo phím bị giữ khi đứng yên hoặc teleport.

BotMatch điều khiển countdown/còi trực tiếp theo luật. Multiplayer dùng contact RPC đã có để phát tiếng chạm tại từng client; còi và countdown được server gửi bằng RPC tới clients/host. Các thay đổi âm thanh không sửa luật, quyền quyết định contact hay cách tính điểm. Chưa chạy xác minh âm thanh bằng hai client online qua Internet; độ trễ âm của client xa còn phụ thuộc mạng.

Menu/ModeSelection/Settings dùng chung service và mức âm lượng, nhưng yêu cầu này chưa gán tiếng bấm UI hoặc nhạc nền. Training chưa có luồng thi đấu tương đương BotMatch; các hệ thống mới có thể gọi `SfxPlayer.Play(GameSfx, position)` tại sự kiện gameplay của mình.

## Kiểm tra

- **32/32 kiểm tra SFX trong Play Mode:** thư viện nạp trước lần đánh đầu, đủ tám clip, nguồn âm có mẫu không bằng 0, đúng âm theo intent, không phát khi chỉ vung vợt, contact lặp không phát hai lần, đi/chạy/teleport, 10 nhịp countdown, còi lỗi/hết game/hết trận, thanh âm lượng và tạm dừng.
- **61/61 EditMode tests** hiện có của gameplay/multiplayer đạt.
- Kết quả Editor: `UnityWork/SFX/validation.json`. Bản chuẩn bị: `UnityWork/SFX/preparation.json`.
- **Bản Windows Development build và kiểm tra đạt:** tám nguồn đều có mẫu âm khác 0, DSP 256 mẫu / 4 buffer / 48 kHz; tiến trình tự thoát với mã 0. Bản game tại `My project/Builds/SfxValidation/PickleVerse.exe`.
- Probe Windows dùng cờ opt-in `--pkv-sfx-report=<đường dẫn JSON>`; cờ này không chạy trong phiên chơi bình thường. Kết quả tại `UnityWork/SFX/windows-audio.json`. Mẫu âm được quan sát lần đầu sau khoảng 29–34 ms bằng cách polling theo frame; số này chịu độ phân giải polling và **không đo thời điểm âm thanh đến tai người chơi**.
- Build trả về Succeeded nhưng ghi nhận **11 lỗi biên dịch biến thể shader URP/Lit DX11**, gồm lỗi thiếu bộ nhớ, và cảnh báo Missing Script tại các UI có sẵn. Tám SFX vẫn qua kiểm tra Windows; bản này là bản kiểm tra âm thanh, chưa được xác nhận là bản phát hành đồ họa sạch lỗi. Không sửa shader/UI ngoài phạm vi yêu cầu SFX.
- Chưa đo trên Android/iOS hoặc loa/tai nghe Bluetooth; cần đo thiết bị thật trước khi kết luận độ trễ đầu cuối trên các thiết bị đó.

## Tài liệu và tái tạo

Các script chuẩn bị/kiểm tra nằm trong `My project/Tools`: PrepareMatchSfx.py, MatchSfxSetup.cs và MatchSfxValidation.cs. Import settings và thư viện được tạo bằng API Unity Editor, không sửa metadata nhập âm bằng tay. Chạy lại chuẩn bị từ file gốc rồi chạy Setup khi người dùng cập nhật những WAV này.

- [Unity: Preload Audio Data](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/audioclip/preloadaudiodata): nạp dữ liệu clip trước khi phát.
- [Unity: DSP buffer size](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/audioconfiguration/dspbuffersize): kích thước buffer ảnh hưởng độ trễ xuất âm.
