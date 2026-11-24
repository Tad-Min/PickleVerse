# 06 — Rendering & Performance

> High Quality, Performance Optimized · "Visual Quality per Millisecond"

## Mục tiêu

| Thiết bị | Mục tiêu |
|---|---|
| PC phổ thông (tham chiếu: laptop RTX 3050 4 GB, i5-11400H) | ≥ 60 FPS ổn định ở 1080p, preset Cao (game giới hạn 60 FPS mặc định) |
| PC mạnh | ≥ 120 FPS (bỏ giới hạn khung hình) |
| Mobile | 30–60 FPS: preset Thấp tự áp (không bloom/vignette/DoF/SSAO/khí quyển), 1 cascade, render scale theo `MobileRenderPolicy` |

## Quy trình: đo trước, tối ưu sau

1. Xác định **CPU- hay GPU-bound** bằng FrameTimingManager (CPU main thread vs GPU frame time) và bộ đếm Rendering
   (Triangles, SetPass) — probe `PerfProbe` (Editor) và `PkvBench` (bản build, cờ `-pkvBench`), không suy đoán.
2. Đo số tam giác/khung theo từng renderer để tìm nguồn (bộ đếm không phụ thuộc nhiệt độ GPU).
3. Chỉ giữ tối ưu có số đo; ghi số trước/sau vào bảng dưới.
4. So sánh FPS chỉ giữa các lần chạy cùng điều kiện nhiệt: laptop RTX 3050 hạ xung xuống 210 MHz khi đạt 88 °C — các
   số Editor đo lúc GPU nóng bị loại; số bản build đo xen kẽ trước/sau, nghỉ giữa các lần.

## Kết luận đo

- Mọi cảnh đều **GPU-bound** (GPU time ≈ frame time; CPU main thread 1,3–1,6 ms trên bản build).
- Nguồn hình học lớn nhất: lượt DepthNormals của SSAO cũ (vẽ lại toàn cảnh), bóng 4 cascade, khán giả skinned ở LOD0
  (24k tam giác/người, 4 bone) và khung mái thép của đấu trường đổ bóng.
- Chi phí điểm ảnh của tính năng tùy chọn trong uber shader (noise, world detail, court, edge) đo được ≈ 0,2 ms tổng — giữ.
- Hậu kỳ (tonemap + grading LUT + bloom nửa độ phân giải + vignette) ≈ 0,75 ms; SSAO after-opaque ≈ 0,7 ms (Editor, cùng điều kiện).

## Tối ưu đã áp dụng (có số đo bộ đếm tam giác)

| Thay đổi | Cảnh | Tam giác/khung trước → sau |
|---|---|---:|
| SSAO: DepthNormals prepass → after-opaque từ depth (nửa độ phân giải) | Training | 4,22M → 3,32M |
| Bóng PC: 4 → 2 cascade (split 0,33) | Training | 3,32M → 2,56M |
| Khán giả: LOD0 chỉ trong ~7,5 m, LOD1 + skin 2 bone | BotMatch | 1,16M → 0,73M |
| Tổng hợp (Editor, góc gameplay) | BotMatch | 2,48M → 0,73M |
| Mái màng vải không đổ bóng cứng (đúng vật lý, bớt caster) | Training/Tutorial/Replay | — |

Đã có sẵn và giữ: SRP Batcher (uber shader tương thích), GPU instancing cho cây/foliage, static batching sân,
LOD cây 0–3 + billboard, mipmap + anisotropic 4–8 cho texture chuẩn, **mipmap streaming** bật trên texture chuẩn,
ASTC 6×6 cho Android. Không bật GPU Resident Drawer (giảm CPU, trong khi game GPU-bound) và Occlusion Culling
(sân mở, khán đài thấp: lợi ích nhỏ so với thời gian bake) — xem lại nếu có cảnh kín.

## Số đo bản build Windows (1920×1080, D3D12, preset PC, VSync tắt, 600 khung/cảnh)

_Điền sau lần đo xen kẽ trước/sau — xem mục "Kết quả" bên dưới._

## Ngân sách cho tài sản mới

| Hạng mục | Ngân sách |
|---|---|
| Tam giác hiển thị (góc gameplay) | ≤ 1,0M (PC), ≤ 0,35M (mobile) |
| Shadow caster | chỉ vật trong 30 m và cao < 6 m; vật mờ/xa tắt cast |
| Vật liệu mới | biến thể của 5 bộ chuẩn; không shader mới |
| Hạt khí quyển | ≤ 250 hạt/scene, không trong khối sân |
| Texture | ≤ 1024² (PC), ≤ 512² (Android), mipmap bật |
