# Khôi phục sân cho BotMatch

Nguồn sân: `My project/Assets/Models/court/Court.glb`.
Scene đã lưu: `My project/Assets/Scenes/BotMatch.unity`.

Sau khi pull, object `Arena/Court` vẫn tồn tại nhưng 6 mesh của sân và lưới tham chiếu GUID cũ. Đã thay bằng model hiện tại và lưu prefab `Assets/LocalMatchContent/Court/PlayableCourt.prefab`.

| Thành phần | Giá trị áp dụng |
|---|---:|
| Chiều rộng sân, đo đến mép ngoài vạch | 6,096 m / 20 ft |
| Chiều dài sân, đo đến mép ngoài vạch | 13,4112 m / 44 ft |
| Kitchen mỗi bên tính từ lưới | 2,1336 m / 7 ft |
| Bề rộng vạch trắng | 0,0508 m / 2 in |
| Đỉnh lưới ở giữa | 0,8636 m / 34 in |
| Đỉnh lưới tại biên dọc | 0,9144 m / 36 in |
| Khoảng cách giữa hai mặt trong cột lưới | 6,7056 m / 22 ft |
| Mặt sân tính theo game | Y = 0 m |

Kích thước đối chiếu `Doc/Luat_thi_dau_Pickleball.md`, mục 2.A–2.C và [USA Pickleball](https://usapickleball.org/what-is-pickleball/).

Model nguồn dùng đơn vị mét và làm tròn một số số đo. Đã căn mặt sân, tạo vạch đúng kích thước, giữ hình lưới nguồn và thêm mép trắng có độ võng khớp `Court.NetHeight`. Đường trung tâm chỉ chạy từ Kitchen đến baseline. Vạch Kitchen nằm trong vùng Kitchen; mép ngoài vạch biên khớp giới hạn IN/OUT của Rules Engine.

Scene dùng prefab có tham chiếu mesh và material hiện tại. Builder tạo sân qua `BotMatchCourtBuilder`, đọc model tại đường dẫn mới và tái áp dụng kích thước khi dựng lại scene. Trong Unity có menu **PickleVerse → Restore court in BotMatch** để khôi phục riêng sân mà giữ các object còn lại.

Phạm vi lưới trong phép kiểm tra bóng bay qua lưới dùng `Court.NetHalfSpan` để khớp phần hiển thị. Bóng và NVZ vẫn dùng hệ thống luật hiện tại. Model sân không thêm camera, light hoặc collider PhysX trùng với hệ thống mô phỏng bóng.

Kiểm chứng đã thực hiện: đo geometry trong Unity, mở lại scene để xác nhận mesh/material đều hợp lệ, 29/29 test luật và điều khiển, 12/12 kiểm tra contact các combo, bot giao bóng vào Rally trên frame Play Mode thật. Backup scene, báo cáo đo và hình Game View nằm trong `UnityWork/CourtRestore`.
