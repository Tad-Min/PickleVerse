# 04 — Environment Language

> Layered composition · environmental storytelling · subtle atmosphere · reusable modular assets

## Luật vàng

**Khu thi đấu luôn rõ.** Không hạt, vệt sáng, sương hay vật trang trí nào được nằm trong khối không gian của sân
(|x| ≤ 4,5 m và |z| ≤ 9 m, cao ≤ 6 m) hoặc che bóng, nhân vật, vạch. Các prefab khí quyển được dựng ngoài khối này.

## Bốn lớp chi tiết

| Lớp | Nội dung | Ví dụ trong game |
|---|---|---|
| **Primary** | Hình khối quyết định bối cảnh | sân, khán đài, mái màng, nhà phố, hàng cây |
| **Secondary** | Đồ nội thất có chức năng | ghế cạnh sân, thùng đá, cột đèn pha, bảng quảng cáo, camera truyền hình |
| **Tertiary** | Đồ kể chuyện | chai nước, khăn trên ghế, chậu cây, cờ, băng rôn "VÀO SÂN. TỎA SÁNG" |
| **Micro** | Bề mặt & chuyển động nhỏ | vết mòn/vệt cao su trên sân, hạt cát acrylic, bụi nắng, lá rơi, lá cây có gió |

Mọi chi tiết phải trả lời "ai dùng nó, khi nào": khăn và chai ở đúng ghế cầu thủ, vết mòn ở đúng chỗ đứng giao bóng
và vạch bếp, lá rơi từ đúng vành đai cây.

## Khí quyển theo Look (`Assets/Prefabs/ArtDirection/`)

Do `ArtDirectionRuntime` sinh vào scene theo Look (preset Trung bình và Cao), dựng lại bằng
`PickleVerse/Art Direction/Rebuild atmosphere`.

| Prefab | Thành phần | Ngân sách |
|---|---|---|
| `Atmosphere_SunsetDistrict` | bụi nắng ấm trên hai khán đài và sau đường biên xa (3 × 40–70 hạt); lá rơi từ các cạnh hộp 40×5×52 m quanh sân (45 hạt, 2 loại lá) | ≤ 225 hạt, 1 draw/hệ |
| `Atmosphere_AcademyArena` | 8 vệt skylight nghiêng theo hướng key, trên mép trong khán đài; bụi trong vệt (2 × 90 hạt) | ≤ 180 hạt + 8 quad |

Hạt mờ dần theo trọng số đêm (`AtmosphereNightResponse`); vệt sáng tắt ban đêm, mờ khi nhìn sượt hoặc khi camera lại gần.
Volumetric fog **không** dùng (không cần cho góc camera gameplay, tốn GPU).

## Bộ modular

| Nhóm | Tài sản | Nguồn |
|---|---|---|
| Floors | mặt sân acrylic (feature `_COURT`), nền lát, bê tông | `M_Court_Acrylic`, `M_Stone_Concrete` |
| Walls / barriers | bảng chắn sơn, rào lưới | bộ Metal / Fabric |
| Fences | rào thép bọc nhựa (dither xa) | `SD_ChainLink` |
| Lighting | cột đèn pha thon (VenueFixtures), đèn LED (`M_Emissive_LED`) | Sunset District |
| Decorations | ghế, thùng đá, chai, khăn (courtside kit), cờ/băng rôn (gió) | `courtside_kit.py`, bộ Fabric |

## Bóng đổ, phản xạ, decal

- Bóng rào/cột/khán đài lên mặt đất: mặt nạ nướng (Sunset District) `min()` với bóng realtime → người chơi và bóng vẫn đổ bóng thật.
- Phản xạ: bầu trời qua môi trường (DynamicGI.UpdateEnvironment) + fresnel của BRDF; không thêm reflection probe (đo được không đáng chi phí với góc camera trên cao).
- "Decal" mặt sân làm trong shader (`_COURT`), không thêm draw call.
- Gió: lá cây (Sunset Foliage), cờ satin và lá cây trong đấu trường (`_WIND`), gió chung `_PV_WindParams` theo Look.
