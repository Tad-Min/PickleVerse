# Merge Menu và màn ăn mừng — 11/10/2026

Repository đang merge là `D:/Repository/PickleVerse`, HEAD `db01756`, nhánh incoming `b23fadc`. Đợt merge trước đã được commit; lần pull này có hai conflict mới ở `Assets/Scenes/UI/Menu.unity` và `Assets/Scripts/LocalMatch/BotMatch.cs`.

Menu được hợp nhất bằng UnityYAMLMerge từ ba stage Git rồi mở và lưu qua Unity Editor. Giữ Profile, Achievements, Missions, Career, ví TOKENPAD/ELITE TOKEN và Shop, đồng thời nhận model nam, nút chọn nam/nữ và các animation của nhánh incoming. Conflict còn lại sau semantic merge chỉ là màu chữ nút Career; giữ màu mint của Menu hiện tại. Scene có 653 GameObject, không thiếu script.

BotMatch giữ cả ghi nhận tiến trình và màn ăn mừng. Khi chơi lại, kết thúc tiến trình của trận cũ, hủy màn ăn mừng, tạo ID thưởng mới và khởi tạo trận tiếp theo. Quy tắc không cộng trùng XP, lịch sử và TOKENPAD vẫn được giữ. Sửa tên asset `PKV_Dance_Clap` để loại warning tên `(Clone)` không khớp tên file. Phép kiểm tra font trong `BotMatchVictoryValidation` xét cả font dự phòng mà TMP dùng khi hiển thị.

Unity JSON/YAML attributes trong `.gitattributes` được viết trực tiếp vì project nằm trong thư mục con của repository. Git đã được cấu hình local driver `unityyamlmerge` với Unity 6000.6.2f1. Đã xác minh scene/prefab dùng đúng driver: merge không xung đột trả mã 0; hai phía sửa khác nhau cùng thuộc tính trả mã 2 để Git giữ trạng thái cần giải quyết.

Driver là cấu hình local của máy này. Trên máy khác cần cấu hình `merge.unityyamlmerge.driver` trỏ tới UnityYAMLMerge của Unity Editor đã cài. Git vẫn có warning từ macro LFS cũ trong thư mục con; lần sửa này giữ các thiết lập LFS hiện có.

Kết quả kiểm tra: 180 test logic (44 Progression, 44 LocalMatch, 60 Multiplayer, 32 Shop), 32 kiểm tra chọn nhân vật, 23 kiểm tra Menu/Profile/Missions/Career/Shop và 82 kiểm tra thắng/thua, camera ăn mừng, replay/chọn chế độ — tổng cộng 317 mục đạt. Các trận mô phỏng chạy trên player riêng; ID, ví, Elo, XP và lịch sử của player thật đã được xác nhận nguyên vẹn và phục hồi.

Windows Development build thành công, 0 error, 1 warning Pipeline do chưa bật cấu hình runtime điều khiển Player. Bản build: `Builds/ProgressionWindows/PickleVerse.exe`. Báo cáo chi tiết: `Docs/Progression/MergeAgainTests.json`, `Docs/Progression/MergeAgainBuild.json`, `Docs/Progression/MergedMenuValidation.json`.

Bản sao conflict ban đầu và ba stage Git: `Docs/MergeRecovery/20261011-2325/` (được gitignore). Các file giải quyết conflict đã stage, chưa commit/push. Cần tạo merge commit trước lần pull tiếp theo.
