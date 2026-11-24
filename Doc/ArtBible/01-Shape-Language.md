# 01 — Shape Language

> Organic curves · controlled bevels · clean silhouettes · consistent proportions

## Nguyên tắc

1. **Không có cạnh dao.** Mọi cạnh lồi nhìn thấy được trong camera gameplay (5–15 m) phải có bevel bắt sáng.
   Cạnh sắc chỉ được phép ở chi tiết < 1 cm hoặc khi chức năng yêu cầu (vạch sân, mép lưới).
2. **Hình khối đọc được từ silhouette.** Ở 15 m, khi tô đen toàn bộ, người chơi phải nhận ra vật thể
   (ghế, thùng đá, cột đèn, nhân vật). Ưu tiên 1 khối lớn + 1–2 khối phụ; chi tiết vi mô để cho vật liệu.
3. **Đường cong có chủ đích.** Ống thép uốn, nan gỗ võng nhẹ, thùng nhựa thuôn đáy: đường cong đến từ cách vật
   được sản xuất (uốn, đúc, tiện), không phải trang trí ngẫu nhiên.
4. **Tỷ lệ nhất quán theo người thật** (mét), riêng nhân vật giữ tỷ lệ cách điệu của bộ model hiện có
   (đầu lớn ~1:5) — đồng bộ với video Login.
5. **Hình học của luật chơi là bất khả xâm phạm.** Mặt sân, vạch, lưới, collider giữ nguyên từng đỉnh
   (`courtside_kit.py` kiểm tra bằng assert).

## Thông số Hard Surface (ghế, rào, cột, thiết bị, kiến trúc)

| Hạng mục | Giá trị chuẩn |
|---|---|
| Bevel | 1–3 % cạnh ngắn nhất của chi tiết; tối thiểu 8 mm ở vật 1 m (nan ghế 12 mm, thùng đá 28–45 mm) |
| Segments | 2–4 (4 cho đồ đúc nhựa, 2–3 cho gỗ/thép) · profile 0.6 |
| Giới hạn | Bevel theo góc 30°, *Harden Normals* bật |
| Pháp tuyến | Weighted Normal (Face Area, weight 50, keep sharp) + Smooth by Angle 30° |
| Ống / thanh tròn | Curve bevel, 5 đoạn/nhịp, 2 vòng bevel (≈ 0,4k tam giác mỗi khung ống) |
| Boolean | Chỉ boolean sạch (không n-gon chéo, không đỉnh lơ lửng); áp modifier trước khi xuất |
| Pivot | Đáy giữa vật (đặt lên mặt đất) |

## Thông số Organic (nhân vật, cây, đá, đất, vải)

| Hạng mục | Giá trị chuẩn |
|---|---|
| Hình dạng | Subdivision / lathe (vật tròn xoay), không mặt phẳng lớn không có độ cong |
| Shading | Smooth toàn bộ; không split normal trên bề mặt hữu cơ |
| Vải | Lưới con ≥ 6×8 ô, võng nhẹ + gợn biên độ ≤ 3 mm (khăn) |
| Cây | Thẻ lá alpha + pháp tuyến hình vòm tán (giữ quy trình Sunset District) |
| Nhân vật | Giữ model hiện có; LOD1 cho khán giả ở khoảng cách gameplay |

## Ngân sách tam giác (LOD0)

| Loại | Ngân sách | Ví dụ thực tế |
|---|---:|---|
| Đạo cụ cạnh sân (ghế + thùng + chai + khăn, một bên) | ≤ 3k | 2,8k (`courtside_kit.py`) |
| Đạo cụ nhỏ (chai, nắp) | ≤ 300 | chai 252, nắp 160 |
| Nhân vật người chơi | ≤ 25k | 19,6k |
| Khán giả | LOD0 ≤ 25k chỉ cận cảnh; LOD1 ≤ 7k | 24k / 6,7k |

## Bộ đạo cụ hữu cơ đã làm (Giai đoạn 2)

`ArtSource/ArtDirection/courtside_kit.py` thay tại chỗ các khối hộp 8–24 đỉnh của hai sân thi đấu:

- **Ghế cạnh sân:** 3 nan gỗ dầu bo cạnh, tựa 2 nan nghiêng 12°, khung ống thép sơn tĩnh điện uốn liền, chân đế.
- **Thùng đá:** thân nhựa đúc bo 45 mm thuôn đáy, nắp bo 28 mm, hai quai ống.
- **Chai nước:** tiện tròn (vai, cổ), nắp ren.
- **Khăn:** tấm vải mềm võng nhẹ trên mặt ghế.

Giữ nguyên tên object, vật liệu, màu đỉnh (bảng màu cũ) và tỷ lệ UV; chế độ `verify` xuất lại nguồn chưa sửa
cho ra GLB **trùng byte** với bản đang dùng, nên mọi khác biệt trong GLB mới đều do bộ đạo cụ.
Ảnh duyệt Blender: `BlenderWork/ArtDirection/preview/` (gitignored).

## Checklist tài sản mới

- [ ] Silhouette đọc được ở 15 m
- [ ] Mọi cạnh lồi nhìn thấy có bevel + weighted normal
- [ ] Không N-gon/đỉnh trùng; pivot ở đáy
- [ ] Đúng ngân sách tam giác; có LOD nếu > 5k và xuất hiện nhiều lần
- [ ] Không chạm hình học/collider của luật chơi
