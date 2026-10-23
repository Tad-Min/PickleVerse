# Kết quả chuẩn hóa thời lượng animation

Ngày: 07/10/2026. Nguồn quy định: `Doc/duration_animation.md`.

Đã lưu đủ **20 clip nữ và 20 clip nam** đúng duration. Đồng thời cập nhật **38 clip gameplay** mà scene BotMatch tham chiếu và **5 model GLB có Idle nhúng**. Các clip hiện hữu được ghi đè tại đường dẫn cũ, giữ GUID và tham chiếu scene/Animator.

## Thời lượng hoàn tất

| Clip theo tài liệu | Duration | Nữ | Nam |
|---|---:|:---:|:---:|
| `PKV_Idle_Ready` | 2.0 s | ✓ | ✓ |
| `PKV_Walk_Forward` | 1.0 s | ✓ | ✓ |
| `PKV_Run_Forward` | 0.8 s | ✓ | ✓ |
| `PKV_Jump` | 0.8 s | ✓ | ✓ |
| `PKV_Overhead_Smash` | 1.0 s | ✓ | ✓ |
| `PKV_Serve_FH_Underhand` | 1.5 s | ✓ | ✓ |
| `PKV_Groundstroke_FH` | 1.0 s | ✓ | ✓ |
| `PKV_Groundstroke_BH` | 1.0 s | ✓ | ✓ |
| `PKV_Dink_FH` | 0.8 s | ✓ | ✓ |
| `PKV_Dink_BH` | 0.8 s | ✓ | ✓ |
| `PKV_Volley_FH` | 0.6 s | ✓ | ✓ |
| `PKV_Volley_BH` | 0.6 s | ✓ | ✓ |
| `PKV_ThirdShotDrop_FH` | 1.2 s | ✓ | ✓ |
| `PKV_ThirdShotDrop_BH` | 1.2 s | ✓ | ✓ |
| `PKV_Shuffle_Left` | 0.8 s | ✓ | ✓ |
| `PKV_Shuffle_Right` | 0.8 s | ✓ | ✓ |
| `PKV_Backpedal` | 1.0 s | ✓ | ✓ |
| `PKV_SplitStep` | 0.5 s | ✓ | ✓ |
| `PKV_NVZ_MomentumRecovery` | 0.8 s | ✓ | ✓ |
| `PKV_Fault_Reaction` | 1.0 s | ✓ | ✓ |

Trong `Assets/Animation/Characters/Female` và `Male`, tên file bỏ tiền tố `PKV_`, ví dụ `Volley_FH.anim`, `Jump.anim`. Bộ `Assets/LocalMatchContent/Clips/female` và `male` giữ tiền tố `PKV_`.

`Jump.anim` nữ bị thiếu sau quá trình tối ưu asset trước đó. Đã khôi phục chuyển động gốc từ `My project/Assets/Models/Character/female/Jump.glb` ở commit `3fc68e7` và tạo asset 0,8 s. Jump vẫn không được đưa vào scene đấu bot, theo lựa chọn gameplay trước đây.

## Cách chỉnh

Clip gốc có keyframe khoảng 0,041667–2,041667 s; bộ BotMatch có 0–2 s. Đã đưa keyframe đầu về 0 và co giãn toàn bộ timeline theo công thức `(t - start) × target / (end - start)`, đồng thời điều chỉnh tangent theo hệ số nghịch đảo. Không cắt mất phần chuẩn bị, contact, follow-through hoặc recovery.

Giá trị keyframe, đường cong động tác, rig, skin weights, ngón tay và socket cầm vợt được giữ. Idle nhúng trong model cũng chuẩn hóa 0–2 s; toàn bộ dữ liệu nhị phân mesh, rig, texture và giá trị chuyển động được xác nhận không thay đổi. Action phụ ngắn trong GLB không nằm trong danh sách duration và được giữ nguyên.

Playback SplitStep dùng độ dài clip 0,5 s thay cho giá trị cứng 0,45 s; vẫn cho phép di chuyển chủ động ngắt SplitStep. Timing contact của các cú đánh giữ nguyên theo profile đã căn vợt. Builder đọc duration từ tài liệu khi tạo lại clip, tránh sinh lại asset 2 s. Builder lấy danh sách từ ClipData vì thư mục GLB từng animation đã được tối ưu và chuyển thành `.anim`.

Idle vẫn dài 2 s vì đây là vòng thở/chờ đúng quy định. Hệ thống giữ–ghép–thả phím vẫn chốt cú đánh khi thả nút gốc; thời gian chuẩn bị trước contact là một phần động tác đánh.

## Kiểm chứng

- Unity 6000.6.2f1 đọc lại 78 asset `.anim`: length và stopTime đều khớp tài liệu, sai số cho phép 0,00001 s.
- Toàn bộ giá trị keyframe và GUID của clip hiện hữu được giữ; so sánh đường cong tại 41 pha trên từng curve đạt sai số tối đa 7.15255737e-07.
- 12 tổ hợp đánh bóng qua parser → animation → contact: đều pass.
- Phát bóng trên frame Play Mode thật: bấm nhanh J, giữ/thả J và bot đều vào Rally, không có lỗi giao bóng.
- SplitStep còn hoạt động trước 0,5 s, kết thúc sau mốc 0,5 s, và phản ứng ngay với di chuyển: pass.
- Tạo lại Volley nữ, Serve nam, Idle nữ và Jump nữ qua pipeline: cả 4 clip giữ đúng duration.
- 7 clip ngoài danh sách duration giữ nguyên nội dung so với đầu phiên.

Backup và dữ liệu kiểm chứng: `UnityWork/AnimationDuration/backup`, `retime-report.json`, `embedded-idle-report.json`, `embedded-unity-audit.json`, `QA/`, `result.json`. Thư mục UnityWork được Git ignore theo cấu hình hiện tại của dự án.

Trong Unity có thể chạy lại **PickleVerse → Animations → Apply documented durations** ở Edit Mode. Không cần dựng lại scene BotMatch để dùng các clip đã cập nhật.

Unity tạo cache atlas/glyph trong `Match Vietnamese Font.asset` khi mở scene và chạy thử. Cache được giữ lại; kiểm duyệt tự động đã từ chối bước xóa vì nằm ngoài phạm vi duration và có thể ảnh hưởng hiển thị chữ. Không chỉnh cấu hình font.
