# MultiplayerCourt: cinematic, kết quả và xác nhận trận

Ngày cập nhật: 14/10/2026.

## Sửa lỗi xác nhận trên hai máy — 14/10/2026

Luồng trước đây khởi tạo kết nối thi đấu ngay trong `CreateOrJoinSessionAsync`. Máy khách phải kết nối xong mới nhận được session để hiện UI; nút Chấp nhận lại chờ đủ kết nối mới được bật. Log của dự án ghi `Failed to create allocation`, làm máy khách không tới được bước xác nhận còn host chờ kết nối.

Đã chuyển sang tạo phiên chờ bằng `WithNetworkHandler` trước, chưa tạo Relay hoặc khởi động NGO. Cả hai phía có thể xác nhận ngay sau khi tham gia phiên chờ, kể cả khi chưa có kết nối thi đấu hoặc host chưa công bố đồng hồ. Host bắt đầu đồng hồ khi đủ thành viên của roster trong session. Chỉ khi mọi người chấp nhận mới gọi `IHostSessionNetwork.StartRelayNetworkAsync`; đủ kết nối rồi mới tải sân.

Vùng từ assignment của Matchmaker không được dùng trực tiếp làm vùng Relay nữa. Vùng Relay được chọn từ số đo của toàn bộ người chơi, hoặc chọn tự động nếu thiếu dữ liệu. Nếu dịch vụ trả lỗi vùng không tồn tại, thử chọn vùng tự động; lỗi quyền truy cập vẫn được báo rõ.

Protocol tăng lên **8** để tách khỏi các build dùng luồng cũ. Cả hai máy cần pull code mới và build lại cùng phiên bản. Các queue dùng SameProtocol dạng Equality giữa các vé không cần khóa lại một số phiên bản cụ thể.

Kiểm chứng sửa lỗi: 23 kiểm tra thực thi MultiplayerApp với adapter Sessions; thử hai registry dịch vụ, hai tài khoản Authentication độc lập cùng tạo/tham gia một phiên riêng, cả hai xác nhận rồi kết nối thật qua Relay với `SessionNetworkHandler` của game: 9/9 kiểm tra đạt. Có thử tạo/tham gia đồng thời. Đây là hai phía trong một Editor, chưa phải hai máy vật lý hoặc tìm trận công khai.

Bộ EditMode sau sửa lỗi: **329/329** bài đạt, bao gồm chọn đúng vùng Relay, thử lại khi vùng không tồn tại, không che lỗi quyền truy cập và không khởi tạo kết nối cho trận đã hủy.

## Luồng chơi

1. Tìm đủ người và kết nối vào phiên chờ. Hiện **Đã tìm thấy trận**, chế độ 1v1/2v2, số người xác nhận và thời hạn 20 giây.
2. Chọn **Chấp nhận** hoặc **Hủy**. Mọi người phải xác nhận trước khi host tải MultiplayerCourt. Đã chấp nhận thì chờ quyết định chung; bot dự phòng không cần xác nhận.
3. Nếu một người hủy/hết giờ/mất kết nối trước khi xác nhận xong, trận không khởi chạy. Người đã chấp nhận tự tìm tiếp; người hủy hoặc chưa xác nhận dừng tìm. Với nhóm 2v2, tách nhóm cũ và tìm tiếp bằng vé cá nhân để đồng đội đã chấp nhận không bị người hủy kéo ra khỏi hàng chờ.
4. Khi tất cả máy tải sân, camera giới thiệu sân, quay đội của người chơi bước vào sân rồi quay đội đối thủ. 2v2 quay cả hai đồng đội cùng lúc. Giao bóng, điều khiển và tính tiến trình chỉ bắt đầu khi cinematic hoàn tất.
5. Cuối trận, chờ dòng thông báo **Kết thúc trận đấu** và toàn bộ thông báo đang xếp hàng chạy hết. Camera chuyển tới nhân vật của người chơi, hoặc cả hai người trong đội 2v2.
6. Thắng: chữ **Thắng** có sắc vàng và ánh sáng quét; nữ nhảy Win, nam SuperSaiyan rồi Clap. Thua: chữ **Thua** màu xám, màn hình tối nhẹ và phản ứng thua.
7. Chọn **Tiếp tục** để mở thống kê trận, Elo trước/sau, chênh lệch Elo, rank và XP. Dữ liệu lấy từ kết quả đã lưu, dùng cơ chế chống cộng trùng của dự án.

## Ánh sáng

MultiplayerCourt dùng PlayableStadium, sky, sun và cấu hình fixture của BotMatch. Đã đồng bộ shadow strength/bias của sun, tham chiếu camera/sân, đồng thời buộc áp lại trạng thái đèn khi chọn môi trường để cache không giữ trạng thái cũ. Host truyền lựa chọn ngày/đêm trong roster; máy khác áp dụng lựa chọn này.

Ban đêm: 8 nguồn sáng Y chiếu sân, 16 nguồn trên mái và 8 nguồn nhẹ chiếu khán đài. Màu trắng và mức sáng giữ cùng cấu hình BotMatch. Ban ngày: sun và đèn Y; tắt nguồn trên mái và khán đài.

## Quy tắc Elo hiện có

1v1 xếp hạng và bot dự phòng xếp hạng có thay đổi Elo. 2v2 ghép trận và phòng custom hiện không đổi Elo theo `MatchResult.Ranked` của dự án; bảng kết quả hiển thị `+0 ELO` và giải thích chế độ không đổi Elo. Thay đổi này không sửa chính sách xếp hạng thành một hệ thống Elo 2v2 mới.

## Tham khảo UX

LoL sử dụng ready check với lựa chọn chấp nhận/từ chối. Nguồn: [Riot, Patch 14.17](https://www.leagueoflegends.com/en-us/news/game-updates/patch-14-17-notes/).

CS:GO dùng popup chấp nhận trận và đã sửa việc popup tự đóng khi nhấn Esc. Nguồn: [Valve, Release Notes 12/07/2018](https://blog.counter-strike.net/2018/07/20633/).

Thiết kế áp dụng cho PickleVerse: một hộp thoại tập trung, nút chính lớn, nút hủy riêng, đồng hồ và tiến độ xác nhận rõ ràng. Thời hạn 20 giây và cách tìm tiếp là lựa chọn của dự án theo yêu cầu người dùng.

## Kiểm chứng

- EditMode: 313/313 bài vượt qua, bao gồm chính sách xác nhận 1v1/2v2, từ chối, hết giờ và bot dự phòng.
- Play Mode với NGO loopback: 52 kiểm tra vượt qua cho nam/nữ, thắng/thua, 1v1/2v2; chặn cú đánh trong intro; khung hình; chờ hết thông báo; raycast nút Tiếp tục và đọc Elo đã lưu. Đồng đội/đối thủ trong kiểm tra này là nhân vật bot mô phỏng, chưa phải nhiều máy thật.
- 11 kiểm tra thực thi MultiplayerApp với adapter Sessions trong bộ nhớ: xác nhận đủ bốn người, hủy trận, tự tìm tiếp, dừng người hủy và tương tác UI.
- 27 kiểm tra ánh sáng: so sánh bốn scene, đèn ngày/đêm và roster host ghi đè lựa chọn khác của guest.
- Chưa xác minh end-to-end Matchmaker/Relay giữa nhiều máy thật. Cần dùng cùng bản game mới ở mọi máy; protocol đã tăng từ 6 lên 7 do thêm trạng thái cinematic đồng bộ.

Các công cụ lặp lại kiểm tra nằm trong `My project/Tools/MultiplayerPresentationValidation.cs`, `MatchReadyIntegrationValidation.cs`, `NightLightingValidation.cs`. Báo cáo và ảnh kiểm tra nằm trong thư mục cục bộ `UnityWork/MultiplayerPresentation`.

## Lịch sử cấu hình Cloud ngày 13/10/2026

Đã sửa cả hai file `PickleVerse_1v1.mmq` và `PickleVerse_2v2.mmq`: rule `SameProtocol` ghép các vé có cùng phiên bản, thay vì khóa queue vào protocol 6. Công cụ `MultiplayerCloudSetup.WriteConfigs` cũng tạo lại đúng rule này.

Đã thử triển khai vào môi trường **pickleverse-test**, nhưng việc lấy quyền dịch vụ từ phiên đăng nhập Unity Editor trả lỗi **401**. Cấu hình mới chưa được đưa lên Cloud. Queue 1v1 đang triển khai trước đó có thể vẫn lọc protocol 6, nên bản protocol 7 cần cập nhật queue trước khi thử tìm trận thật.

Thao tác còn cần người dùng thực hiện: đăng nhập Unity Editor bằng tài khoản có quyền dự án, chọn **PickleVerse → Multiplayer → Deploy Test Configuration**, kiểm tra trạng thái Deployed rồi build lại game cùng phiên bản cho mọi máy. Đây là lỗi xác thực Unity Cloud, không phải lỗi biên dịch hoặc lỗi phê duyệt của Codex.
