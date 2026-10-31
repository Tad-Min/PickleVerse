# Phiên hoạt động trong Redis — 31/10/2026

Cập nhật sau cùng: chuẩn hóa GameMatchPlayers còn 15 cột, schema 38 bảng/267 cột/53 FK. Luồng hoạt động Redis không đổi. Xem [Rà soát GameMatchPlayers](GameMatchPlayers_Normalization_20261031.md); những số 276/257 cột dưới đây thuộc các đợt trước.

Đợt tiếp theo đã tách JSON Data/Report của trận thành cột, schema hiện tại 38 bảng/276 cột/53 FK. Các số 257 cột và kết quả kiểm tra dưới đây ghi riêng đợt chuyển phiên hoạt động; luồng Redis không đổi. Xem [Relational Match Columns](Relational_Match_Columns_20261031.md).

Theo yêu cầu, đã bỏ `AppActivitySessions`. PostgreSQL chỉ giữ tổng thời gian hoạt động trong `PlayerProfiles.TimePlayedSeconds`; không thêm bảng hoặc cột thay thế. Schema còn 38 bảng, 257 cột và 53 FK. Nguồn khởi tạo vẫn là `src/Database.sql`, không tạo migration.

## Luồng hiện tại

- Unity mở phiên trước đăng nhập và gửi checkpoint mỗi 15 giây, trước đăng xuất và khi thoát bình thường. Giữ nguyên hai API `api/Game/activity/open` và `api/Game/activity/checkpoint` cùng request/response hiện có.
- Redis giữ `pickleverse:activity:session:<UUID>` cho thời điểm mở và tài khoản sở hữu. `pickleverse:activity:player:<UUID>` giữ các bộ đếm phiên, hợp các khoảng thời gian đã cộng và biên nhận cập nhật tổng. Key dùng TTL 8 ngày; một phiên hỗ trợ tối đa 7 ngày. Các khoảng và phiên quá thời hạn được dọn khi cập nhật.
- Backend khóa dòng `PlayerProfiles` bằng Npgsql EF Core, chỉ cộng khoảng thời gian chưa được tính. Retry, checkpoint đến sai thứ tự và nhiều cửa sổ không cộng trùng; thời gian gửi lên bị giới hạn theo đồng hồ server. Redis gắn mỗi phiên với đúng một tài khoản bằng compare-and-set.
- Trước khi commit PostgreSQL, backend ghi vào Redis tổng trước/sau cùng trạng thái phiên dự kiến. Nếu SQL rollback, request sau khôi phục phần đã chuẩn bị. Nếu SQL commit nhưng response mất, request sau nhận ra tổng đã lưu và không cộng lần hai. Redis không nhận được ghi xác nhận thì transaction SQL không commit.
- Lịch sử, kết quả trận và các DTO tiến trình giữ nguyên. `Game/state` đọc tổng từ `PlayerProfiles`; hoàn tất trận không cộng lại thời lượng trận.

## Hết hạn và mất trạng thái Redis

Phiên không còn hoặc trạng thái người chơi đã mất trả HTTP 410. Unity tạo UUID mới và bắt đầu đếm từ thời điểm hiện tại; không gửi lại toàn bộ thời gian phiên cũ. Generation của Redis ngăn một phiên đã cộng được tính lại từ đầu khi mất riêng key người chơi. Nếu Redis phục hồi snapshot cũ không khớp tổng SQL, backend đổi generation và lấy tổng SQL làm mốc. Các opening cũ trước lúc mất trạng thái không được backfill khoảng thời gian không còn coverage; có thể mất phần chưa đồng bộ nhưng không cộng lại tổng đã lưu. Luồng thử mở lại giới hạn một lần mỗi lượt gửi.

Tổng đã commit trong PostgreSQL vẫn còn khi Redis khởi động lại hoặc mất key. Phần chưa commit hoặc chưa gửi có thể mất nếu Redis mất toàn bộ trạng thái. Redis development hiện có volume, AOF `everysec` và `noeviction`; AOF không bảo đảm giữ mọi cập nhật trong giây cuối khi máy gặp sự cố. Không dùng Redis làm nguồn tổng thời gian bền vững.

## Code và kiểm tra

`RedisAppActivity.cs` quản lý Redis; `OnlineGameService.Activity.cs` cập nhật tổng qua EF Core; `AppActivityRuntime.cs` xử lý phiên hết hạn trên Unity. Không có entity EF cho phiên hoạt động.

Bộ `--check-activity-redis` có 31 kiểm tra với PostgreSQL và Redis thật: cạnh tranh request, cửa sổ chồng nhau, đăng nhập muộn, quyền sở hữu, hết hạn/mất key, snapshot Redis cũ, SQL rollback sau khi Redis ghi và retry sau commit. Các kiểm tra dùng database và prefix Redis riêng, tự dọn sau khi chạy. Kết quả triển khai/build/hồi quy nằm trong `artifacts/redis-activity-checks-20261031/`.

Conceptual Diagram được tạo lại từ SQL, không vẽ attribute hoặc thêm entity Redis. Không tuyên bố đã chạy lại gameplay nhiều Unity client.

Kết quả cuối: 206 assertions trong 6 bộ kiểm tra (31 activity Redis, 22 app/rank, 81 online game, 25 relational progress, 28 Redis và 19 unified matches); 9 kiểm tra HTTP trên image đã triển khai; backend và Unity FrontEnd cùng các assembly phụ thuộc build thành công. Database development đã tạo lại từ SQL, API readiness 200. Đã kiểm tra source hash, XML, JavaScript và render sơ đồ; xem trực tiếp PNG CD-06. Fixture PostgreSQL/Redis được dọn sau kiểm tra. Xem `artifacts/redis-activity-checks-20261031/summary.json`.
