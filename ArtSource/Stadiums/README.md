# Nguồn sân PickleVerse

Cập nhật cây/cỏ ngày 26/10/2026: xem [NaturalPark_README.md](NaturalPark_README.md).
Hai sân dùng chung bộ mesh cây có cành/lá và cỏ chuyển động trong
`NaturalVegetation.blend`; các nguồn sân đầy đủ đã được cập nhật tương ứng.

Hai file Blender 5.1 này là nguồn chỉnh sửa đã đóng gói texture. Chúng nằm ngoài `Assets`, vì vậy Unity không đưa file `.blend`, camera render hay đèn xem trước vào game.

- `ProfessionalStadium.blend`: sân chuyên nghiệp, bảng PICKLEVERSE/cổ vũ tiếng Việt, bối cảnh cây và cỏ.
- `PickleballCourt_LowPoly.blend`: sân ngoài trời, ghế gỗ, dụng cụ cạnh sân, cây và thảm cỏ.

Quy ước: Blender Z hướng lên, đơn vị mét; xuất glTF Y hướng lên. Sân chuyên nghiệp giữ cao độ đế trong file nguồn. Prefab Unity căn mặt sân về `y = 0`; không dùng trực tiếp model thay cho prefab khi chạy game.

Model chạy game:

- `My project/Assets/Models/Stadium_Enhanced/ProfessionalStadium.glb`
- `My project/Assets/Models/Stadium_Enhanced/PickleballCourt_LowPoly.glb`
- Prefab và đèn/camera/điểm vào sân: `My project/Assets/Resources/Courts/Professional.prefab`, `LowPoly.prefab`.
- Năm texture dùng chung: `My project/Assets/LocalMatchContent/Stadiums/RefinedTextures/`.

Khi xuất lại: chỉ chọn mesh và parent cần thiết, giữ parent `Gate_Hinge_OPEN_ROTATE_LOCAL_Z` của cửa sân ngoài trời; không xuất camera, đèn xem trước hoặc animation. Sử dụng Mix node RGBA/Multiply với Factor 1 và màu ở input B để glTF giữ hệ số màu, theo [hướng dẫn glTF của Blender 5.1](https://docs.blender.org/manual/en/5.1/addons/import_export/scene_gltf2.html). Kiểm tra màu sau khi nhập Unity thay vì chỉ xem bản render Blender.

Các công cụ Editor chỉ chạy khi được gọi, không được đóng gói vào game:

- `VenueRefinementInstaller`: nhập và gắn model/vật liệu, bảo toàn kích thước sân và bốn đèn trắng.
- `VenueRefinementSceneChecks`: kiểm tra liên kết scene và chụp ảnh ngày/đêm.
- `VenueRefinementRenderProbe`: so sánh render có bốn nhân vật, desktop và cấu hình mobile.
- `VenueRefinementPresentationChecks`: kiểm tra trình diễn cục bộ của Menu/BotMatch, cảnh vào sân, bóng và camera kết quả.

Log, ảnh kiểm tra và bản gốc tạm được đặt trong thư mục Git đã bỏ qua `BlenderWork/VenueRefinement20261025/`. Không đưa thư mục đó vào bản build. Xem báo cáo `Doc/Stadium_Refinement_20261025.md` để biết chính xác phạm vi và giới hạn kiểm thử.

Các script Python đi kèm lưu công thức tạo texture/dựng sân. Muốn chạy lại từ đầu cần chuẩn bị bản gốc của hai GLB từ commit `8e06f7c` vào `BlenderWork/VenueRefinement20261025/Original/` và giữ manifest trong thư mục làm việc. Không dùng GLB đã nâng cấp làm đầu vào lặp lại. Hai `.blend` là nguồn chỉnh sửa trực tiếp và đã có ảnh đóng gói, không cần dựng lại bằng script.

Phép so sánh dùng các bản sao baseline trong một thư mục Editor tạm, giữ nguyên asset chạy game trong suốt quá trình đo. Các bản sao này đã được xóa sau khi hoàn tất. Nguồn đèn pha trong prefab giới hạn một nguồn bóng đổ mềm trên desktop; vị trí bốn đèn và màu trắng được giữ nguyên.
