# 02 — Material Language

> Stylized PBR · controlled roughness · subtle surface details · consistent response

## Một shader cho mọi bề mặt: `PickleVerse/Stylized Lit`

`Assets/Shaders/ArtDirection/StylizedLit.shader` (+ `PVStylizedLighting.hlsl`, `PVCommon.hlsl`), tương thích SRP Batcher
(mọi thuộc tính vật liệu nằm trong `UnityPerMaterial` ở mọi pass). Các pass: ForwardLit, ShadowCaster, DepthOnly,
DepthNormals, Meta (bake). Tên thuộc tính là tập cha của Sunset Surface nên vật liệu cũ chuyển sang không mất giá trị.

| Tính năng (Art Bible yêu cầu) | Cách làm | Bật bằng |
|---|---|---|
| Stylized Lit | GGX của URP + wrap diffuse (`_Wrap` 0,18–0,45) + bóng lạnh theo Look | luôn bật |
| Fresnel Rim | rim ấm `#FFD39C`, mạnh nhất ở cạnh hướng về key, 25 % còn lại trong bóng | `_RimStrength` |
| Edge highlight | mask B + độ cong màn hình của pháp tuyến hình học | `_EdgeHighlight` |
| Gradient | tối/lạnh dần về chân vật theo trục Y object (grounding) | `_GradientStrength` |
| World-Space Texture | triplanar `T_WorldDetail` theo mét, phá lặp trên mặt lớn | `_WORLD_DETAIL` |
| Procedural Noise | fBm 3 octave thế giới: biến thiên giá trị ±% và độ nhám | `_NoiseAlbedo`, `_NoiseSmoothness` |
| Wet Surface | `_PV_Wetness` toàn cục × `_WetResponse`: tối albedo xốp, bóng hơn, vũng nước phẳng | `_WETNESS` |
| Wind Deformation | lắc lớp (sway + flutter + gust), trọng số màu đỉnh R × chiều cao | `_WIND` |
| Mặt sân | xem dưới | `_COURT` |

Shader phụ dùng chung ngôn ngữ: `PickleVerse/FX/Light Shaft`, `PickleVerse/FX/Atmosphere Particle`; foliage giữ
`Sunset Foliage` (wrap + xuyên sáng ngược nắng). Shader "anh hùng" của Shop (Gemstone, Coin) và match-found
(Stage Lit/Energy/Trail/UI Waves) được giữ vì là vật trưng bày có ánh sáng riêng.

## Mặt sân có chiều sâu mà không thêm polygon (`_COURT`)

Tính trong không gian thế giới, tâm sân (0,0), nửa rộng 3,048 m, nửa dài 6,7056 m, bếp 2,1336 m — khớp `PickleVerse.Rules.Court`
(test `CourtFeatureMatchesTheRulesCourt`).

- **Base color theo vùng:** bếp (NVZ) đậm/lạnh hơn ~5 %; vùng chạy quanh sân được tô màu teal của bảng màu.
- **Vết mòn:** sau đường biên cuối (chỗ giao/đỡ giao), dải sát vạch bếp (dink), làn giữa; phá bằng fBm → mặt mòn sáng hơn 7 %, bóng hơn.
- **Vệt cao su:** vệt kéo dài theo hướng di chuyển, tối 16 %, nhám hơn.
- **Roughness khác nhau giữa các khu:** bếp +0,03, mòn +0,14, vệt −0,12, ngoài sân −0,05.
- **Normal sần nhỏ:** hạt cát acrylic thủ tục (không texture), tự tắt khi texel nhỏ hơn pixel (không nhấp nháy).
- **Fresnel:** phản xạ môi trường theo góc sượt từ BRDF chung.

## Năm bộ vật liệu chuẩn

Texture sinh bằng `ArtSource/ArtDirection/make_material_sets.py` (tileable, ~1 m/ô theo UV mét của venue):
`T_<Bộ>_BaseColor` (chỉ độ sáng/chi tiết — **sắc độ lấy từ palette** qua màu đỉnh hoặc `_BaseColor`), `T_<Bộ>_Normal`,
`T_<Bộ>_Mask` (R metallic, G AO, B cạnh/chi tiết, A smoothness), `T_<Bộ>_Emission` cho bộ có phần phát sáng.

| Bộ | Kích thước | Đặc trưng | Smoothness hiệu dụng | Dùng cho |
|---|---|---|---|---|
| **Court** (acrylic thể thao) | 1024² | hạt cát + vệt lăn sơn ±3 % | 0,38–0,55 | mặt sân, vùng chạy, vạch, nền phụ |
| **Metal** (sơn tĩnh điện / thép trần) | 512² | vỏ cam, xước chải, mẻ cạnh ở mask B, LED panel | 0,55–0,74; metallic 0 (sơn) / 0,85–0,9 (inox) | cột, rào, khung, đèn |
| **Wood** (gỗ dầu) | 512² | thớ thẳng, vòng năm, lỗ mạch ở AO | 0,45–0,55 | ghế, kiosk |
| **Stone** (bê tông, gạch) | 1024² | cốt liệu, rỗ, loang | 0,30–0,50 | nền, tường, bồn cây |
| **Fabric** (vải / polymer) | 512² | dệt trơn, ánh satin, dải đèn biển | 0,30–0,62 | băng rôn, cờ, lưới, ghế nhựa |

Vật liệu tham chiếu (bắt đầu từ đây khi tạo tài sản mới): `Assets/Materials/ArtDirection/M_*.mat`.
Preset **Character** (không phải một bộ texture): wrap 0,38, rim 0,32, edge 0,04 — dùng cho người chơi, khán giả, tóc.

## Quy tắc

1. **Độ nhiễu thấp:** biến thiên giá trị texture ≤ ±6 %; không ảnh chụp, không bẩn ngẫu nhiên.
2. **Roughness có kiểm soát:** smoothness bị kẹp `[0,04 ; 0,88]`; không bề mặt nào gương trừ nước.
3. **Tương phản vừa đủ để phân biệt chất liệu:** gỗ ấm–nhám, thép lạnh–bóng, vải mềm–wrap cao, sân mịn–bóng nhẹ.
4. **Không tạo shader mới** khi uber shader đã có tính năng; không dùng URP Lit/Simple Lit cho tài sản 3D.
5. **Màu đến từ palette**, không từ texture.

## Công cụ & kiểm tra

- `PickleVerse/Art Direction/Apply material standards` — chuyển vật liệu theo bảng Spec (tái chạy được).
- `PickleVerse/Art Direction/Remap imported materials` — vật liệu glTF nhập từ GLB → vật liệu chuẩn, remap trên scene/prefab.
- `PickleVerse/Art Direction/Audit materials` — liệt kê vật liệu lệch chuẩn. Hiện chỉ còn prefab cũ không được tham chiếu
  (`Prefabs/Player`, `Prefabs/Environment/PickleballCourt|Paddle|Ball`, `LocalMatchContent/Court/PlayableCourt`) và 2 marker
  gameplay (`IncomingLanding`, `ShotAim`) giữ nguyên để dễ đọc.
