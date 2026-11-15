# Sunset District — nguồn dựng bối cảnh chiều tà (15/11/2026)

Toàn bộ hình học và texture được sinh trong dự án bằng Python + Blender 5.1.1; không dùng gói tải về.
Các file `.blend` ở đây là nguồn chỉnh sửa; Unity chỉ dùng GLB/PNG trong `My project/Assets`.
Hai file gốc `../ProfessionalStadium.blend` và `../PickleballCourt_LowPoly.blend` **không bị ghi đè**:
bản đã nâng cấp nằm ở `*_Sunset.blend`.

> `ArtSource/` đang nằm trong `.gitignore`. File mới ở thư mục này chỉ vào Git khi thêm bằng `git add -f`.

## Quy ước

- Mét; Blender Z hướng lên. Các script dựng trong "hệ mặt bằng" (x = Unity x, y = Unity z) rồi xoay 180°
  quanh Z trước khi xuất, khớp quy ước cũ của dự án (Unity x = −Blender X, Unity z = −Blender Y).
- Màu bề mặt nằm trong vertex colour `Col` (sRGB, mã hóa gamma 2, shader bình phương lại); texture chỉ
  mang chi tiết vật liệu (hạt acrylic, nhựa đường, gạch lát, thớ gỗ...). Nhờ vậy nhiều vật thể dùng
  chung một vật liệu: 24 vật liệu cũ của sân Professional còn 8, 17 của LowPoly còn 9.
- Lá cây: vertex colour R = độ uốn theo gió, G = pha, B = che khuất, A = sắc độ.
- Mặt trời chiều tà: cao 21°, Euler Unity (21, 62, 0) — phải khớp `MatchEnvironmentLighting.GoldenHourSun`
  vì mặt nạ bóng được nướng theo đúng hướng này.

## Thứ tự dựng lại

```
python sunset_textures.py                       # 17 texture thủ tục + foliage_atlas.json
blender --background --python build_trees.py    # SunsetTrees.blend/.glb (4 loài × LOD0-3, 2 loại bụi)
python sunset_textures.py tree_impostors        # atlas billboard LOD3 từ ảnh render của bước trên
blender --background --python build_district.py # SunsetDistrict.blend/.glb + district_layout.json
blender --background --python upgrade_courts.py # *_Sunset.blend, 2 GLB sân, VenueFixtures.glb
blender --background --python bake_sunmask.py -- LowPoly 1024
blender --background --python bake_sunmask.py -- Professional 1024
blender --background --python preview_render.py -- <thư mục ảnh> LowPoly final   # ảnh duyệt (Eevee)
```

Sau đó trong Unity (Edit mode, qua `unity command script eval`):
`SunsetDistrictInstaller.CaptureGeometry("before")` (một lần, trên bản sân cũ) → `SunsetDistrictInstaller.Install()`
(tự chạy `ValidateGeometry()` so mặt sân, lưới, điểm vào sân, giới hạn camera, đèn, collider, script).
`SunsetDistrictProbe` chụp ảnh, kiểm kê và đo FPS trước/sau; ghi vào `BlenderWork/SunsetDistrict20261115/` (gitignored).

## Thành phần

| File | Nội dung |
|---|---|
| `sd_common.py` | Bộ dựng lưới (ống, dải, tường, hộp), vật liệu Blender đọc được rõ ràng, xuất GLB nén màu 8 bit, bỏ ảnh nhúng |
| `build_trees.py` | Xà cừ tán tròn, sấu/long não tán cao, phượng vĩ (lá kép + chùm hoa), cọ; thẻ lá alpha có pháp tuyến hình vòm tán; LOD0/1/2 + billboard chéo |
| `build_district.py` | Vành đai đường bo cong 2 làn có vỉa, vạch, vạch qua đường; quảng trường lát gạch; 4 sân pickleball lân cận có rào và lưới; hồ, giàn hoa vòm, đài phun, ki-ốt cà phê; đèn đường cần cổ thiên nga, đèn công viên hoa văn xoắn; ghế gang uốn S; nhà phố (phía nắng thấp, phía đối diện cao), đường chân trời |
| `upgrade_courts.py` | Gom vật liệu, chiếu UV theo mét, giữ nguyên từng đỉnh mặt sân và lưới (có assert); cột đèn hình đàn lia + đầu đèn LED có lá tản nhiệt |
| `bake_sunmask.py` | Cycles, chỉ nắng trực tiếp: bóng của nhà, cây (alpha), rào, khán đài lên mặt đất → mặt nạ 1024² cho 224 m |
| `assemble.py`, `preview_render.py` | Ghép bối cảnh + cây + sân như trong Unity để nướng và render duyệt |
