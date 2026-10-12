# Bộ luyện tập PickleVerse

Cập nhật ngày 12/10/2026. Scene: `My project/Assets/Scenes/Training.unity`.

## Luồng sử dụng

- Đại sảnh → Chơi ngay → Luyện tập → chọn bài → đọc lời HLV → Bắt đầu.
- Camera giới thiệu sân và vùng mục tiêu, sau đó trở về góc điều khiển trong lúc thực hành.
- Bài có bóng cấp, tiếp xúc vợt thực, vùng đích, giới hạn số lượt và đánh giá kết thúc; không hoàn thành chỉ bằng bấm nút Tiếp tục.
- Đại sảnh → Tiến trình → Luyện tập hiển thị số bài đạt, tỷ lệ chính xác, sao, thời gian, lỗi phổ biến, số lượt và số lần làm lại.
- Nút Tiến trình ở nav dưới mở trực tiếp bảng luyện tập; nút Nhiệm vụ phía trên giữ phần nhiệm vụ ngày/tuần. Menu được giữ tạm ngưng khi đi qua Training để tránh tải lại đại sảnh lúc quay về. Chi tiết kiểm tra hiệu năng: `PickleVerse_Training_Return_Performance.md`.
- Tiếp tục luyện ưu tiên bài đang dở; nếu không có, chọn bài tiếp theo đã mở khóa. Khi quay lại bài đang dở, các lượt đã đánh được giữ; bóng của lượt hiện tại được cấp lại an toàn.

## Các bài và thưởng

| Nhóm | Bài | Thao tác mặc định | Đạt | Tokenpad / XP |
|---|---|---|---|---|
| Cơ bản | Giao bóng đúng ô | J, chân sau baseline | 6/8 | 150 / 5 |
| Cơ bản | Drive về cuối sân | J sau nảy | 6/8 | 150 / 5 |
| Cơ bản | Dink kiểm soát Kitchen | K sau nảy | 6/8 | 150 / 5 |
| Cơ bản | Volley ngoài Kitchen | L trước nảy | 6/8 | 150 / 5 |
| Kiểm soát | Lực và hướng chéo | J/K/L + U hoặc I, theo HUD | 7/10 | 225 / 7 |
| Kiểm soát | Third-shot drop | J+K sau nảy | 7/10 | 225 / 7 |
| Kiểm soát | Reset bóng nhanh | K+L trên không | 7/10 | 225 / 7 |
| Nâng cao | Smash bóng bổng | J+L | 9/12 | 300 / 10 |
| Nâng cao | Di chuyển và hồi vị | A/D, chạy, J hoặc J+I | 9/12 | 300 / 10 |
| Nâng cao | Drop rồi tiến lên | J+K, W lên vùng sẵn sàng | 9/12 | 300 / 10 |
| Đánh đôi | Giữ khoảng cách | Di chuyển ngang, L hoặc L+I, hồi vị | 9/12 | 450 / 15 |
| Đánh đôi | Drop và phối hợp | J+K, K, L; tiến lên cùng AI; lượt bóng của AI | 9/12 | 450 / 15 |

Bốn bài cơ bản mở sẵn. Các bài sau có tiền đề kỹ thuật, được hiển thị trong danh sách khi chưa mở khóa. Phím và tổ hợp trên HUD sử dụng cấu hình điều khiển hiện tại; ví dụ K+U thực hiện cú tăng tốc/Drive theo bộ đọc input đang dùng trong trận. Có joystick và nút cảm ứng dùng lại bố cục trong Cài đặt.

## Tiêu chí và luật

- Cú hợp lệ phải đúng kỹ thuật, contact của animation gặp bóng và bóng rơi trong vùng đích. Tính đích từ cú đánh thật, không cấp thành công khi chỉ chọn đúng phím.
- Timing được đo từ lúc tiếp xúc bóng tới mốc bóng đến trong quỹ đạo cấp. Giao bóng tính sai lệch so với mốc tiếp xúc của animation giao dưới tay.
- 1 sao: đủ số cú đạt; 2 sao: tỷ lệ từ 80%; 3 sao: tỷ lệ từ 90% và timing trung bình không quá 0,16 giây.
- Bài sau nảy dùng bóng cấp có nảy. Volley/reset/smash dùng tình huống sau hai lần nảy bắt buộc; bài third-shot drop khởi tạo trạng thái sau giao và trả giao để tiếp xúc là cú thứ ba.
- Chân giao bóng dùng kiểm tra baseline/ô giao của `SinglesRules`. Giao chéo phải vượt Kitchen. Volley trong Kitchen và đà sau volley vào Kitchen đều bị tính lỗi.
- Lưới được kiểm tra theo đoạn bóng quét qua mặt phẳng lưới. Đường biên và điểm rơi dùng kích thước mét của game: sân 6,096 × 13,4112 m; Kitchen sâu 2,1336 m mỗi phía.
- Footwork yêu cầu trở về vòng hồi vị. Approach yêu cầu tiến lên vùng sẵn sàng. Bài đôi yêu cầu đứng cùng hàng, giữ khoảng cách, có đồng đội AI và hai đối thủ luân phiên cấp bóng. Các khoảng cách đội hình là tiêu chí chiến thuật của drill, không phải điều luật xử lỗi pickleball.

## Lưu dữ liệu và thưởng

Dữ liệu luyện tập nằm trong `PlayerProfile.training`, cùng hồ sơ đang dùng cho Tiến trình và cửa hàng. `LocalPlayerStore` ghi hồ sơ bằng transaction và thay file nguyên tử.

Chỉ lần đầu hoàn thành mỗi bài mới được cộng Tokenpad và XP theo bảng; không tăng Elite Token, Elo hoặc thống kê thắng/thua trận. Bài đã đạt đổi nút thành **Ôn lại**, không thưởng thêm Tokenpad hay XP, nhưng vẫn cập nhật sao và thống kê. Lần đầu chưa đạt vẫn có thể luyện lại để nhận thưởng khi đạt. Hồ sơ cũ có sao hoặc lần hoàn thành cũng được coi là đã nhận thưởng; không thu hồi tiền trước đây. Mỗi lượt có ID riêng; mở lại kết quả hay gọi hoàn thành lần nữa không cộng tiền hai lần.

Nhạc nền phát ở danh sách bài và phần xem hướng dẫn; dừng khi bắt đầu thực hành, giữ nguyên lựa chọn bài nhạc và trạng thái tạm dừng của người dùng. Trở về danh sách sẽ tiếp tục nhạc. Training không hiện widget đĩa nhạc.

Tiếng chạm vợt dùng cùng `MatchActor.ConsumeContact` và thư viện SFX của BotMatch: giao bóng, đánh thường/chéo và tăng lực. Không phát còi cho lỗi chọn phím, timing, vùng đích hoặc hồi vị; còi dành cho lỗi luật. Mỗi lượt thành công hoặc chưa đúng hiện chữ lướt ở giữa màn hình cùng lời động viên. Chờ thông báo chạy hết rồi mới cấp lượt bóng tiếp theo; các bài hồi vị/đánh đôi được đánh giá sau khi kiểm tra vị trí cuối lượt.

Mỗi bài lưu số lượt, số lần làm lại, số lần hoàn thành, sao tốt nhất, độ chính xác, thời gian, tần suất từng lỗi và 8 kết quả chính xác gần nhất. Lượt đang dở lưu checkpoint sau mỗi lượt bóng và định kỳ khi thực hành. Dữ liệu gắn với ID hồ sơ, không dùng một ví PlayerPrefs riêng cho luyện tập.

Phản hồi HLV hiện là hội thoại chữ trên HUD và gợi ý theo lỗi trong game. Phối hợp đánh đôi là bài luyện offline với AI; kiến trúc này chưa thay thế huấn luyện nhiều người thật hoặc mô hình AI coaching của backend.

## Cấu trúc để mở rộng

- `Assets/Scripts/Progression/TrainingModels.cs`: danh sách bài, tiền đề, phần thưởng, scoring, dữ liệu phiên và lịch sử; không phụ thuộc scene.
- `Assets/Scripts/LocalMatch/TrainingDirector.cs`: bóng, contact, luật, input, camera, hồi vị và AI phối hợp.
- `Assets/Scripts/LocalMatch/TrainingHud.cs` và `TrainingStars.cs`: danh sách, hướng dẫn, HUD, đánh giá và sao vector.
- `Assets/Scripts/FrontEnd/PlayerProgressionMenu.Training.cs`: bảng tiến trình, lỗi và tiếp tục luyện.
- `Tools/TrainingSceneSetup.cs`: dựng scene qua API Editor; sao chép Arena, ánh sáng, camera, model và bóng từ BotMatch. Không sửa model/sân nguồn.

Có thể thêm bài sử dụng một kỹ thuật có sẵn bằng thêm định nghĩa trong curriculum; danh sách và bảng tiến trình tự tạo các hàng. Để thêm kỹ thuật hoàn toàn mới, bổ sung quy tắc thực hành trong director. Dữ liệu serializable và engine chấm điểm riêng là chỗ nối với kho nội dung/backend về sau.

## Nghiên cứu đối chiếu

Tài liệu dự án đã đối chiếu: `PickleVerse_Doc_Analysis.md`, `PickleVerse_Core_Features_Status_Audit.md`, `PickleVerse_Capstone_Proposal.md`, `Luat_thi_dau_Pickleball.md`. Audit ngày 08/10 mô tả Training cũ thiếu script; triển khai này xây vòng bài tập trên code và hồ sơ hiện tại sau các cập nhật mới của dự án.

Hướng dẫn kỹ thuật được đối chiếu với các nguồn chính thức:

- [USA Pickleball — Basic Terms](https://usapickleball.org/blog-category/pickleball-basic-terms/): dink, drop và cách tiến lên sau drop.
- [USA Pickleball — Rules](https://usapickleball.org/rules/) và [Rules Summary](https://www.usapickleball.org/docs/rules/USAP-Rules-Summary.pdf): giao bóng, hai lần nảy, Kitchen và đà volley.
- [USA Pickleball — Doubles Strategy](https://usapickleball.org/pickleball-skills/level-three/pickleball-doubles-strategy-tips/): phối hợp vị trí, kiểm soát Kitchen và hướng đánh trong đội.

## Kiểm tra

- 230 EditMode tests đạt, gồm mở khóa, sao, thưởng lần đầu, ôn lại không thưởng, dữ liệu cũ, checkpoint JSON, chính sách nhạc Training và giữ nguyên Elite/Elo.
- Cả 12 bài đã được chạy bằng input giả lập, animation và bóng thật trong Play Mode tới trạng thái đánh giá, trên hồ sơ QA riêng.
- 17 kiểm tra luồng vào từ chọn mode, đọc hướng dẫn, lỗi đánh hụt, luyện lại, thưởng thật, tiếp tục checkpoint và UI Tiến trình đạt.
- Kiểm tra bổ sung trong `Tools/TrainingPolishValidation.cs`: âm thanh thực của các cú giao, Drive, tổ hợp lực/chéo; nhạc khi chọn bài và khi luyện; ôn lại không cộng tiền/XP; lời động viên lướt giữa màn hình, tạm dừng và chờ thông báo trước bóng mới. Báo cáo và ảnh tại `UnityWork/TrainingPolish/`.
- Ảnh QA và báo cáo: `UnityWork/Training/`. Test khôi phục hồ sơ người chơi và xóa các hồ sơ QA của chính nó.

Kiểm tra trên Unity Editor/Windows; phần điều khiển cảm ứng sử dụng hệ thống hiện có, chưa là nghiệm thu trên thiết bị Android/iOS thật.
