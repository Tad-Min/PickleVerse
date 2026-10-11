# Tách 4 GLB dance thành clip cho nam/nữ

Đã tạo 6 animation theo danh sách nhân vật, mỗi animation có một bản ở `LocalMatchContent/Clips` và một bản ở `Animation/Characters`, tổng cộng 12 `.anim`.

| Nhân vật | Nguồn GLB | Tên clip | Thời lượng gốc |
|---|---|---|---:|
| Nam | đồng_ý_vỗ_tay | Dance_Clap | 13,000 s |
| Nam | hóa_supersaigian | Dance_SuperSaiyan | 1,083 s |
| Nam | nhảy_suốt_đêm | Dance_AllNight | 8,208 s |
| Nữ | đồng_ý_vỗ_tay | Dance_Clap | 13,000 s |
| Nữ | nhảy_boom | Dance_Boom | 7,167 s |
| Nữ | nhảy_suốt_đêm | Dance_AllNight | 8,208 s |

Đường dẫn:

- `My project/Assets/LocalMatchContent/Clips/male/PKV_Dance_Clap.anim`
- `My project/Assets/LocalMatchContent/Clips/male/PKV_Dance_SuperSaiyan.anim`
- `My project/Assets/LocalMatchContent/Clips/male/PKV_Dance_AllNight.anim`
- `My project/Assets/LocalMatchContent/Clips/female/PKV_Dance_Clap.anim`
- `My project/Assets/LocalMatchContent/Clips/female/PKV_Dance_Boom.anim`
- `My project/Assets/LocalMatchContent/Clips/female/PKV_Dance_AllNight.anim`
- `My project/Assets/Animation/Characters/Male/Dance_Clap.anim`
- `My project/Assets/Animation/Characters/Male/Dance_SuperSaiyan.anim`
- `My project/Assets/Animation/Characters/Male/Dance_AllNight.anim`
- `My project/Assets/Animation/Characters/Female/Dance_Clap.anim`
- `My project/Assets/Animation/Characters/Female/Dance_Boom.anim`
- `My project/Assets/Animation/Characters/Female/Dance_AllNight.anim`

Bản LocalMatch là Legacy clip để dùng với hệ thống SampleAnimation hiện tại; bản Characters là Generic clip để dùng với Animator. Bone path dùng cùng rig hiện có. Đã chuyển rest rotation và tỷ lệ translation sang rig đích, đặc biệt với nữ. Các khớp ngón tay bổ sung của rig đích có track tĩnh để không giữ pose ngón tay từ clip gameplay trước đó.

Giữ thời lượng, frame rate 60 của source và toàn bộ chuyển động. Không áp dụng bảng duration cũ cho 4 animation mới. Thử giảm keyframe đã được hủy vì kiểm tra cho thấy lệch động tác; các clip cuối giữ nguyên dữ liệu chuyển động trước phép thử đó. Clip lưu theo định dạng asset text của project.

Kiểm chứng 81 thời điểm trên mỗi animation: hai bản clip cho cùng pose; không kéo giãn xương; giá trị pose hữu hạn; mọi bone path tìm thấy trên model đích. Đã xem contact sheet 6 hàng × 4 thời điểm. Animation không còn phụ thuộc GLB nguồn.

Sau kiểm chứng và kiểm tra reference, đã xóa `My project/Assets/animaiton_dance` cùng meta, loại bỏ 94.391.052 byte GLB. Dữ liệu chuyển động thuần và hash nguồn được lưu ở `UnityWork/DanceImport/SourceMotion`, không giữ mesh/texture GLB.

Ảnh/báo cáo: `UnityWork/DanceImport/Images/contact-sheet.png`, `extraction.json`, `validation.json`, `deletion.json`.

**Đã dọn thay đổi ngoài phạm vi:** sau khi người dùng cho phép, đã khôi phục đúng 67 asset/cấu hình cũ về phiên bản Git hiện tại. Kiểm tra diff sau khôi phục không còn thay đổi trên các file tracked. Giữ nguyên 12 clip mới và việc xóa thư mục nguồn. Danh sách audit: `UnityWork/DanceImport/unexpected-serialization-changes.json`.
