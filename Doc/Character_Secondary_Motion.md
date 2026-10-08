# Vật lý tóc và váy nhân vật

Đã áp dụng chuyển động phụ cho nhân vật nữ và nam trong BotMatch; đồng thời gắn cùng hệ thống vào bước tạo nhân vật của Multiplayer. Khi Play, các vùng này phản ứng với chuyển động của nhân vật và tư thế animation theo quán tính, lực hồi và lực cản.

## Cách hoạt động

- **Đuôi ngựa nữ:** chuỗi 4 khớp bổ sung, cố định phần sát dây buộc và tăng ảnh hưởng dần về ngọn tóc. Tóc trễ lại khi chạy, lắc khi đổi hướng và giảm dao động khi dừng. Giới hạn mỗi khớp 22°; có vùng đẩy ra khỏi đầu và thân.
- **Tóc ngắn nam:** 8 cụm khớp nhỏ quanh đầu, chân tóc giữ ổn định và phần ngọn rung nhẹ. Giới hạn 5°, lực hồi và lực cản cao hơn đuôi ngựa.
- **Váy nữ:** 8 vùng quanh gấu váy, mỗi vùng có 2 khớp mô phỏng. Kết quả mô phỏng điều khiển 16 blendshape theo hai hướng ngang, ảnh hưởng tăng dần từ eo xuống gấu. Skinning theo chân của váy gốc vẫn được giữ; chuyển động phụ khoảng tối đa 2,55 cm mỗi hướng tại gấu. Có vùng đẩy ra khỏi đùi khi di chuyển.

Đây là mô phỏng spring bone và biến dạng phụ theo vùng. Ràng buộc va chạm là các vùng cầu/capsule xấp xỉ quanh cơ thể; không phải mô phỏng vải từng sợi hay kiểm tra xuyên nhau giữa mọi tam giác. Chuyển động được tính sau animation, có chia bước thời gian và hiệu chỉnh vận tốc khi thời gian frame thay đổi. Khi nhân vật dịch chuyển xa hoặc xoay đột ngột, trạng thái mô phỏng được đặt lại.

## Asset và vị trí chỉnh

| Thành phần | Đường dẫn trong Unity project |
|---|---|
| Mô phỏng runtime | `Assets/Scripts/CharacterMotion/CharacterSecondaryMotion.cs` |
| Cấu trúc thiết lập | `Assets/Scripts/CharacterMotion/CharacterMotionProfile.cs` |
| Thiết lập nữ | `Assets/Resources/CharacterMotion/FemaleMotion.asset` |
| Thiết lập nam | `Assets/Resources/CharacterMotion/MaleMotion.asset` |
| Mesh dùng trong game | `Assets/CharacterMotion/FemaleMotionMesh.asset`, `MaleMotionMesh.asset` |
| Tạo lại asset trong Editor | `Assets/Scripts/LocalMatch/Editor/SecondaryMotionSetup.cs` |

Trong từng profile, tăng `stiffness` để tóc/váy hồi nhanh hơn, tăng `damping` để giảm rung, chỉnh `maximumAngle` để giới hạn độ lắc và `gravityScale` để thay đổi ảnh hưởng trọng lực. Bật/tắt mô phỏng bằng `simulate` trên component `CharacterSecondaryMotion` của CharacterVisual.

Mesh runtime giữ topology và UV của model; thêm khớp/weight ở tóc và blendshape ở váy. Rig nữ có 79 vị trí bone, rig nam có 67, bao gồm 59 bone gốc. Component được gắn ở `MatchActor.Initialize()` và `NetworkPlayer.BuildVisual()`.

Để tạo lại asset, dùng menu **PickleVerse → Add hair and skirt physics** khi mở BotMatch và đã dừng Play. Công cụ đọc các kế hoạch vùng/khớp trong `UnityWork/SecondaryMotion/*_plan.json`; cần giữ thư mục này hoặc tạo lại kế hoạch bằng các script Python đi kèm trước khi chạy công cụ.

## Kiểm tra đã thực hiện

- Biên dịch Unity không có lỗi.
- 29/29 bài kiểm tra LocalMatch và 25/25 bài kiểm tra Multiplayer qua.
- 12/12 trường hợp combo tiếp xúc bóng hợp lệ; bài kiểm tra nhấn J giao bóng đi vào Rally, không có lỗi luật.
- Chạy mô phỏng qua 1.371 frame PlayerLoop thực với animation chạy, đổi hướng, phanh và đứng lại; tóc và váy có phản ứng.
- Kiểm tra cả model nữ/nam chuẩn bằng 1.000 bước mỗi model, luân phiên thời gian frame tương ứng 30/60/144 FPS, dừng chuyển động và dịch chuyển đột ngột. Không có tọa độ NaN/Infinity; góc lắc nằm trong giới hạn. Với tư thế giữ cố định, tóc ngắn về trạng thái nghỉ, đuôi ngựa và váy còn độ lệch nhỏ do trọng lực/ràng buộc cơ thể.
- Đối chiếu ảnh render và vùng skinning; phần cơ thể ngoài tóc không đổi weight. Bản váy giữ skinning gốc.

Kết quả, ảnh kiểm tra và bản sao scene trước thay đổi ở `UnityWork/SecondaryMotion`. Multiplayer đã kiểm tra khả năng gắn rig trên cả hai model và bộ test code; chưa thực hiện trận online qua dịch vụ mạng trong lần thay đổi này.

## Sửa ngoại hình nữ và lỗi ngọn tóc trong cinematic

Atlas nữ được chỉnh bằng Blender ở độ phân giải 4096 × 4096. Vùng UV dây vai/cổ áo được vá những texel màu da lẫn vào vải và các khe nhỏ bằng màu áo lân cận, giữ bố cục trang phục gốc. Tóc được làm đều màu, loại mảng màu áo ở ngọn và thêm chi tiết nhẹ. Normal thân trên/đầu được làm mượt qua các điểm trùng vị trí để giảm vệt tam giác.

Ba file đã cập nhật atlas và normal: `Assets/Models/Characters/Female.glb`, `Assets/LocalMatchContent/Models/Female.glb`, `Assets/Materials/Player/Female.glb`. Đối chiếu các accessor ngoài NORMAL, node, skin và animation với bản sao trước sửa xác nhận: vị trí đỉnh, UV, topology, rig, weight nguồn và chuyển động được giữ. Atlas 4K tốn bộ nhớ texture hơn bản runtime 1K trước đó; chưa đo hiệu năng trên điện thoại trong lần sửa này.

Mái có thêm lớp mesh nhỏ từ Blender: `Assets/CharacterMotion/FemaleFringe.glb`, chuyển sang không gian bone Head trong `FemaleFringeHead.asset`. Profile nữ giữ mesh/vật liệu này; component gắn dưới `mixamorig:Head`, tên `PKV_SmoothFringe`, để che khe ở mái và đi theo animation đầu. Mesh có 697 đỉnh, 1.280 tam giác, không có collider, camera hoặc light.

Nguyên nhân đuôi tóc bị dính: chọn weight theo màu bỏ sót điểm có màu áo/màu sáng trên tóc. Các điểm đó vẫn chịu weight cơ thể/cánh tay, còn điểm bên cạnh theo chuỗi tóc. Đã chọn đủ thể tích tóc theo hình học, đồng bộ điểm trùng ở seam UV và thay ảnh hưởng cánh tay bằng Head + khớp tóc. Vùng đuôi ngựa có 4.783 đỉnh.

Kiểm tra 240 tư thế đi vào sân trên 15.082 cạnh tóc: không có cạnh kéo dài quá 3 lần; tỷ lệ cực đại khoảng 2,18, so với cạnh gần 17 lần ở tư thế lỗi trước sửa. Chạy intro qua 2.999 frame PlayerLoop thật: thấy cả hai cảnh vào sân, chuyển về PreServe thành công; tóc giữ giới hạn khớp 22°. Bản Blender, backup, báo cáo và ảnh nằm trong `BlenderWork/FemaleAppearanceRepair`.
