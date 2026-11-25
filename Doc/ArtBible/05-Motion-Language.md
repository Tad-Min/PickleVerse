# 05 — Motion Language

> Fluid · natural acceleration · responsive transitions · consistent physics

**Ranh giới:** chuyển động trình bày được phép tinh chỉnh; thời điểm chạm bóng, luật, mạng và đồng bộ thì **không**.
Ở PickleVerse, tư thế đánh quyết định vị trí mặt vợt lúc chạm bóng, nên clip đánh, pha chạm (`contactPhase`) và IK vợt
thuộc về gameplay.

## Kiến trúc hiện có (đã kiểm tra, giữ nguyên)

| Yêu cầu Art Bible | Cách PickleVerse làm | Đánh giá |
|---|---|---|
| Animator states Idle/Walk/Run/Sprint/Hit/Victory | Bộ lấy mẫu clip tất định trong `MatchActor`: `PKV_Idle_Ready`, `Walk_Forward`, `Run_Forward` (sprint = cùng clip, tốc độ pha ×1,6), `Shuffle_Left/Right`, `Backpedal`, `SplitStep`, 9 cú đánh (Serve, Dink, Drive, Drop, Volley FH/BH, Overhead Smash), `Fault_Reaction`, `Dance_Win`, `NVZ_MomentumRecovery`. Game không có nhảy/né theo luật | Đủ trạng thái; tất định — cần cho chạm bóng và replay |
| Blend Tree theo tốc độ | Pha bước tiến theo `vận tốc / tốc độ clip` (kẹp 0,25–1,6) → độ dài sải khớp tốc độ | Chống trượt chân (foot sliding) tương đương blend tree 1D |
| Crossfade | `ActorPoseMixer.Begin`: crossfade toàn thân SmoothStep 0,10 s; cú đánh vào ≤ 0,055 s (xong trước khung chạm hợp lệ sớm nhất) | Mượt và phản hồi nhanh |
| Layers + Avatar mask | `BeginLegs/ApplyLegs`: lớp chân (hông + hai đùi) chạy bước trong khi thân trên giữ cú đánh, giữ nguyên hướng cột sống | Tương đương layer có avatar mask |
| IK | `AlignPaddle` (IK hai đốt vai–khuỷu–cổ tay đưa mặt vợt tới điểm chạm, tắt dần 0,12 s), `OverheadPose`, `ServeGrip` | IK tay cầm vợt và tư thế đánh |
| Secondary motion | `CharacterSecondaryMotion` (lò xo tóc, váy) trong `LateUpdate` | Follow-through tự nhiên |
| Anticipation / follow-through | Có trong clip đánh (backswing → contact → follow-through), pha hậu chạm kéo dài theo `Intent.Duration` | Giữ |

## Vòng cập nhật (không có xung đột Animator ↔ Rigidbody)

- **Bóng (BotMatch, Training, Tutorial):** tích phân giải tích `BallFlight.Integrate` trong `Update` theo `dt` thực → mượt ở mọi FPS, không dùng Rigidbody.
- **Bóng (mạng):** mô phỏng trong `FixedUpdateNetwork` (Fusion), hiển thị trong `LateUpdate` bằng nội suy snapshot (`from/to/alpha`), ngoại suy ≤ 80 ms khi thiếu snapshot → tương đương *Interpolate*.
- **Nhân vật:** lấy mẫu clip trong `Update`; lò xo phụ trong `LateUpdate`; camera trong `LateUpdate` sau cùng.
- **Camera:** `CourtFollowView.Apply` damping hàm mũ `1 − e^(−3·dt)` với `unscaledDeltaTime` (độc lập frame-rate), luôn giữ chân người chơi trên đường an toàn HUD.
- Physics `Fixed Timestep` 0,02 s chỉ phục vụ collider tĩnh; không vật thể trình bày nào phụ thuộc nó.

## Chuẩn chuyển động

| Loại | Đường cong | Thời lượng |
|---|---|---|
| Chuyển trạng thái nhân vật | SmoothStep (ease in-out) | 0,10 s (đi/đứng), ≤ 0,055 s (vào cú đánh) |
| Camera bám | Damping hàm mũ, k = 3 | — |
| Look điện ảnh (vignette intro/chiến thắng) | MoveTowards tuyến tính trên trọng số Volume | 0,35 s, không rò sang scene sau |
| DoF cảnh cận (đi vào sân, chân dung chiến thắng) | Khoảng mờ lùi/tiến theo cấp số nhân, SmoothStep | 0,5 s; tắt ngay khi kết thúc cinematic, cảnh rộng luôn nét |
| Trọng số đêm (Menu) | SmoothStep giữa các pha ngày/hoàng hôn/đêm/bình minh | theo chu kỳ |
| Môi trường | Gió nhiều lớp (sway 1,3 Hz + 2,9 Hz + flutter 7 Hz, gust 0,43 Hz); bụi/lá theo noise | liên tục |
| UI | `UiButtonMotion`, `UiPanelReveal`, `UiModalPresentation` (có sẵn) | 0,12–0,3 s |

Không dùng curve tuyến tính cho chuyển động vật thể/nhân vật; tuyến tính chỉ cho trọng số trộn hiệu ứng.

## Đã làm trong lần nâng cấp này

- Intro trận và màn chiến thắng trộn Look điện ảnh trong 0,35 s; DoF Gaussian 8 → 30 m chỉ ở cảnh cận (`SetCinematic(true, closeUp)`), tự tắt khi kết thúc/đổi scene.
- Build dòng lệnh (`-nographics`) vẫn giữ shader DoF nhờ `DepthOfFieldVariantKeeper`.
- Chuyển động môi trường: lá/cờ satin trong đấu trường có gió (`_WIND`), bụi nắng trôi, lá rơi xoay, tất cả độc lập với gameplay.

## Cố ý không làm (lý do)

- **Chuyển sang Animator Controller:** sẽ thay thời điểm/vị trí mặt vợt lúc chạm bóng và dữ liệu replay → vi phạm "không đổi gameplay".
- **Xoáy/nén hình cầu cho bóng:** `ReplayRecorder` ghi rotation/scale của transform bóng; thay đổi sẽ đổi dữ liệu replay.
  Muốn làm cần tách mesh bóng thành node con không bị ghi — để cho một thay đổi riêng có review.
- **Foot IK bám đất:** chân hiện bám theo pha sải; IK chân cần raycast mỗi khung cho 2–4 nhân vật mạng — chưa có số đo chứng minh lợi ích.
