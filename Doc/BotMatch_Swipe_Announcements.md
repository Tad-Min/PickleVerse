# Thông báo chữ lướt trong trận đấu bot

Cập nhật ngày 09/10/2026. Scene: `My project/Assets/Scenes/BotMatch.unity`.

## Luồng mới

Trận vẫn thắng hai trong ba game. Mỗi game cần ít nhất 11 điểm và cách hai; 11–0 và 11–3 đều là thắng một game, 11–10 chưa kết thúc, 12–10 mới đạt cách hai.

Sau rally kết thúc game đầu, hệ thống lần lượt hiển thị ở giữa màn hình:

1. **OUT** hoặc **FAULT / LỖI**, kèm nguyên nhân và bên phạm lỗi.
2. **GAME 1 KẾT THÚC**, kèm điểm 11–0/11–3 và số game thắng 1–0.
3. **ĐỔI BÊN**, kèm `CHANGE ENDS` và game tiếp theo.

Khi thông báo Đổi bên bắt đầu, hai nhân vật chuyển sang đầu sân đối diện, camera theo phía mới, điểm game về 0–0 và số game thắng vẫn là 1–0. Lượt giao đầu thay đổi theo Rules.NextGame. Đồng hồ giao bóng chỉ chạy khi thông báo đã kết thúc; người chơi không cần nhấn Tiếp tục.

Thời lượng danh định: lỗi 1,2 giây, kết thúc game 1,6 giây, đổi bên 1,4 giây; tổng khoảng 4,2 giây. Chữ đi từ trái vào giữa, giữ ngắn để đọc rồi ra phải. Một frame bị treo hoặc bước tiếp tục sau pause Editor không làm toàn bộ chuỗi bị bỏ qua.

Ở game quyết định, mốc 6 điểm cũng dùng Đổi bên, giữ nguyên điểm, số game thắng và quota time-out.

Khi một bên thắng đủ hai game, thông báo **GAME, MATCH!** hiển thị người thắng và số game thắng 2–0 hoặc 2–1. Sau đó chỉ có hai nút nhỏ ở cuối màn hình: **Chơi lại** và **Chọn chế độ**. Không còn hộp lớn dùng để thông báo kết thúc game/trận. Bảng pause, time-out và mở scene trực tiếp vẫn được giữ cho các thao tác tương ứng.

## Thông báo lỗi

Các lỗi kết thúc rally được đưa vào hàng đợi chữ lướt, thay cho dòng lỗi ở thanh trên. Nguyên nhân được chốt sau bước xử lý đà volley, để thông báo và điểm không dùng kết quả tạm thời. Khi đang giao/đánh bình thường, thanh trên vẫn có hướng dẫn tình huống.

Tổ hợp phím không hợp lệ cũng có thông báo trung tâm. Thông báo không chặn raycast hay thao tác chơi. Pause giữ thời gian/chuỗi thông báo; chơi lại xóa toàn bộ hàng đợi để không đổi bên nhầm ở trận mới.

## Phân biệt thuật ngữ

**Game** là từng ván; **Match** là cả trận; **Change Ends** là đổi đầu sân. Không dùng “Game Over” để mô tả tỷ số số game 1–0. Luật đổi bên và đổi lượt giao đầu giữa game được đối chiếu với mục 21.F.2 của [USA Pickleball Official Rulebook 2026](https://usapickleball.org/docs/rules/USAP-Official-Rulebook.pdf). Thời gian chuyển nhanh là lựa chọn trình bày của trận bot, nằm trong giới hạn nghỉ giữa game của luật, không phải thời lượng nghỉ bắt buộc của thi đấu ngoài đời.

## Triển khai và kiểm tra

- `MatchAnnouncements.cs`: hàng đợi, chuyển động chữ, màu, pause, hủy và callback đổi bên.
- `BotMatch.cs`: gửi thông báo từ kết quả rally, giữ nguyên Rules Engine, tự chuyển game và đổi bên.
- `MatchHud.cs`: ẩn hộp giữa game/kết quả, giữ scoreboard và hai nút nhỏ sau trận.
- `Editor/MatchAnnouncementsSetup.cs`: dựng UI qua Unity Editor API; builder BotMatch cũng gọi cấu hình này để tạo lại scene vẫn có thông báo.

**29/29 kiểm thử luật hiện hữu đạt**. **38/38 kiểm tra luồng/UI đạt** với trạng thái điểm được thiết lập để tái hiện: 11–0, 11–3, bot thắng game, 11–10/12–10, thắng/thua cả trận, đổi bên game quyết định, camera, đồng hồ giao, pause, chơi lại, trượt trái/giữa/phải và glyph tiếng Việt. Những ca này dùng cùng CommitRally/StepSimulation/callback của gameplay, không phải một trận chơi tay hoàn chỉnh.

Đã kiểm tra chuỗi chạy theo Update thật: tự sang game 2, số game thắng 1–0, đổi phía sau khoảng **4,28 giây**, không nhấn Tiếp tục. Ảnh game kết thúc/đổi bên/lỗi/thắng trận/nút kết quả được đối chiếu ở 16:9 và 4:3; không có chữ tràn ở mẫu kiểm tra. Chưa nghiệm thu trên điện thoại thật.

Backup, báo cáo và ảnh nằm trong `UnityWork/MatchAnnouncements/Before`, `validation.json`, `automatic-transition.json`, `Images/`. Helper tái hiện ở `My project/Tools/MatchAnnouncementsValidation.cs` không nằm trong runtime Assets.
