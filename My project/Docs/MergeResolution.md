# Merge và tiền tệ — 11/10/2026

Đợt merge tiếp theo (Menu/chọn nhân vật/màn ăn mừng): [báo cáo mới](MergeResolution-20261011.md).

- Repository đang merge: `D:/Repository/PickleVerse`. Cả 8 conflict đã được giải quyết và stage; chưa commit hoặc push.
- Giữ Profile, Achievements, Missions, Career, bot rank sau 6 phút, màn hình tổng kết và Shop/Collection/skin vợt.
- TOKENPAD dùng chung số dư Coin với Shop. ELITE TOKEN thay Gem, không nhận từ gameplay, nhiệm vụ hoặc thành tựu. Player mới có cả hai số dư bằng 0.
- Theo lựa chọn của người dùng, loại Gem miễn phí của phiên bản tiền tệ cũ và ELITE demo. Giữ TOKENPAD, bộ sưu tập và tiến trình; dữ liệu đã chuyển sang phiên bản tiền tệ mới không bị reset lại. Dự án chưa có hệ thống nạp thật.
- Ví và bộ sưu tập lưu trong JSON của từng player với cùng cơ chế lưu nguyên tử/khóa phiên. Ví Shop demo toàn máy chỉ được nhập vào một player; giữ dữ liệu cũ trong PlayerPrefs để dự phòng.
- Protocol 6 đã được triển khai cho queue 1v1 tại `pickleverse-test`; cả hai máy cần bản build mới.

Kiểm tra: 180 test logic đạt; 113 kiểm tra Profile/Missions/Career, 23 kiểm tra Shop tabs, 14 kiểm tra giao diện 3D Shop, 13 kiểm tra luồng ví và 25 kiểm tra tổng kết rank đạt. Kiểm tra NGO/skin vợt có 11 mục đạt. Các phép thử dùng player/ví riêng và phục hồi player thật.

Build Windows Development: Succeeded, 0 error, 1 warning. Warning còn lại thuộc plugin Pipeline: không cấu hình runtime điều khiển Player, nên chức năng đó không được bật trong build. File: `Builds/ProgressionWindows/PickleVerse.exe`.

Bản dự phòng nguyên trạng conflict và cả ba stage Git nằm ở `Docs/MergeRecovery/20261011-1519/` và được gitignore. Menu được hợp nhất bằng UnityYAMLMerge rồi kiểm tra/lưu qua Unity Editor. Font tiếng Việt được bổ sung glyph và giữ các typeface đã cấu hình.
