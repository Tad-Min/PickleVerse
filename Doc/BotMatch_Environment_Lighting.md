# Sky và ánh sáng BotMatch

Khi vào BotMatch, bảng trước trận có hai nút **Ban ngày / Ban đêm**. Người chơi chọn bối cảnh rồi nhấn **Vào sân / Enter** để chạy cinematic. Mặc định là ban ngày; khi chơi lại, bảng chọn mở lại và giữ lựa chọn vừa dùng để người chơi có thể đổi. Bối cảnh giữ suốt trận, qua các rally và game; không có lựa chọn ngẫu nhiên, chu kỳ ngày/đêm hoặc nút đổi sky trong trận.

- **Ban đêm:** `AllSky_Night_MoonBurst Equirect.png`, tắt Match Sun, bật bốn spotlight ở LampHousing trên các cột cao 5,45 m. Đèn hướng vào sân, dùng màu trắng hơi lạnh, bóng đổ mềm trên PC. Ánh sáng môi trường xanh tối giữ bối cảnh bên ngoài sân có cảm giác ban đêm.
- **Trời sáng:** `Epic_BlueSunset_EquiRect_flat.png`, bật ánh mặt trời màu ấm nhẹ với góc 28°, tắt cả bốn spotlight và emission mặt đèn. Ambient trời xanh và bóng đổ mặt trời làm sân, nhân vật, khán đài rõ hơn. Dùng đúng panorama được chỉ định, với sắc trời xanh và ánh nắng thấp của ảnh.

Hai skybox dùng shader Skybox/Panoramic, panorama 360° và texture nhập tối đa 4096 px. File PNG nguồn được giữ nguyên. Camera game dùng Skybox thay nền màu đơn.

`Assets/Scripts/LocalMatch/MatchEnvironmentLighting.cs` áp dụng `selectedNight` và cập nhật khi người chơi chọn bối cảnh trong bảng trước trận. `MatchHud.SelectDay()/SelectNight()` chỉ cho đổi ở trạng thái Intro; `BotMatch.RestartWithIntro()` mở lại bảng trước trận. `Assets/Scripts/LocalMatch/Editor/MatchEnvironmentSetup.cs` tạo/lưu material, light, nút UI và cấu hình scene bằng Unity Editor API. Các material nằm ở `Assets/LocalMatchContent/Lighting`.

Bốn đèn ở x = ±5,70 m, z = ±9,50 m. Spot range 24 m, góc ngoài 95°, góc trong 62°, hướng vào phần giữa sân. URP PC/Mobile cho phép bốn additional lights trên mỗi object và bật additional light shadows. PC dùng soft shadows; cấu hình mobile giữ chất lượng bóng hiện có. Chưa đo hiệu năng trên thiết bị điện thoại.

Pack sky được dọn sau khi hai skybox đã hoạt động: chỉ giữ hai PNG được chỉ định và thông tin license/README. Các panorama còn lại, scene/demo Godot, model demo và hình giới thiệu không dùng được xóa. Báo cáo dung lượng và danh sách xóa lưu ở `UnityWork/MatchLighting/sky-cleanup.json`; ảnh kiểm tra lưu ngoài Assets trong cùng thư mục.

Đã xóa 53 file không dùng, giảm khoảng 225,74 MiB file nguồn. Đã kiểm tra callback của hai nút, chọn đúng sky/light, cinematic bắt đầu theo lựa chọn, chặn đổi trong trận và mở lại bảng chọn khi chơi lại. Báo cáo UI ở `UnityWork/EnvironmentSelection/validation.json`. Đã đối chiếu ảnh gameplay cho cả hai bối cảnh; bộ test LocalMatch có 29 bài.
