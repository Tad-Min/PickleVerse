# PickleVerse – Phản biện nội dung game và luồng end-to-end (25/11/2026)

Bản chấm: `main @ adcb4e2`, exe Windows cùng commit, staging bản CI run 103.
Cách chấm: chơi thật bằng 2 tài khoản thành viên, đọc trang admin, dò API công khai, đọc code theo từng luồng W01–W04.
Bằng chứng: `Report/evidence/e2e_run_20261125.md` (EV-20), `editmode_results_20261125_adcb4e2.json` (EV-18), `ci_runs_20261125.json` (EV-19), `surrender_probe_20261125.md` (EV-21). Mã D-xx là mã lỗi trong Report 5.

## 1. Đã sửa

| Mã | Lỗi | Đã sửa thế nào |
|---|---|---|
| D-33 | Admin thêm một vật phẩm mã `paddle.…` trên trang CMS (hoặc máy chủ bán vợt mà bản game chưa có) thì **mọi người chơi** không tải được hồ sơ, không vào trận, không lưu kết quả. CMS không có nút xóa vật phẩm nên không tự gỡ được. Nguyên nhân: `OnlinePlayerEconomy.ApplyCatalog` ném lỗi ngay trong sự kiện `ProgressionRuntime.Changed`. | Client bỏ qua vật phẩm không có model (`OnlinePlayerEconomy.Merge`); API không gửi vật phẩm chưa có cấu hình game (`GameRepository.Catalog`). Thêm 2 test Unity và 1 kiểm tra CI. Backend build không lỗi. **Chưa phát hành.** Bản game cũ vẫn dính lỗi, nên chỉ thêm vợt Dragon Ruby vào database sau khi mọi người đã dùng bản mới. |
| D-34 (nặng) | Nút **Rời trận** trên sân xử thua ngay, không hỏi lại. | Nút đổi thành **Đầu hàng**, bấm vào chỉ mở hộp xác nhận. Đánh đơn (hoặc đồng đội là máy): xác nhận là thua. Đánh đôi: hộp hiện cho cả hai, phải cả hai xác nhận trong 20 giây; một người từ chối, rút lại, mất kết nối hoặc hết giờ thì trận tiếp tục và 15 giây sau mới hỏi lại được. Lịch sử ghi "Thua · Đầu hàng". Mã: `Surrender.cs`, `SurrenderPanel.cs`, `NetworkMatch.Surrender.cs`. EditMode 730/730 (20 test mới), thử trên sân thật 36/36 (EV-21). **Chưa phát hành**, chưa thử trên staging. |

## 2. Nặng – chờ bạn quyết

| Mã | Lỗi | Bằng chứng | Đề xuất |
|---|---|---|---|
| D-17 | Mật khẩu SMTP, khóa JWT cũ còn trong lịch sử Git của kho công khai, chưa đổi trên staging | TC-N09 Failed | Đạt đổi khóa trên staging và ghi lại ngày đổi |
| LI-11 | Bản Android không chơi online được: APK dùng giao thức 18, máy chủ 22; chưa từng chạy trên điện thoại | `ProtocolVersion = 22` | Build APK mới, thử trên 1 máy trước Review 3 |
| LI-14 | Gói thành viên thu tiền thật trên staging nhưng chưa có hoàn tiền, điều khoản, hóa đơn, danh sách đơn cho admin. Tường phí nằm trước F07–F10 là các chức năng bắt buộc của BM04; tài khoản mới không thử được W03. | `MembershipService`, không có endpoint admin | Chốt với giảng viên; luôn có sẵn tài khoản thành viên khi bảo vệ; cân nhắc cho chơi thử vài trận đầu |
| NFR | Chưa đạt NFR nào trong 12 mục; chỉ NFR-09 đã đo và không đạt. Còn khoảng 3 tuần. | Report 5 §5.3 | Ưu tiên NFR-02, NFR-03, NFR-05 vì chỉ cần máy của nhóm |

## 3. Trung bình

| Mã | Lỗi | Bằng chứng / vị trí |
|---|---|---|
| D-35 | Xem replay hiện sân vận động cũ, không phải sân đã đấu | Chơi thật; scene Replay chưa theo đợt đồ họa 24/11 |
| D-36 | Thống kê "Hủy sau ghép" đếm cả trận đã đấu xong (37/37) | `GameRepository.MatchmakingHistory`: vé bị đóng sau trận cũng tính là hủy |
| D-37 | "Cú chuẩn" gây hiểu lầm: thua 0–2 không ăn điểm nào vẫn 68% cú chuẩn; hết giờ giao bóng bị tính là một cú đánh | EV-20; engine AI không xét lỗi luật ngoài tiêu chí |
| D-45 | Lịch sử trận của admin chỉ có mã trận, loại tiếng Anh và tỷ số ván; không có người chơi, người thắng, lý do; không tìm kiếm được | Trang /matches |
| D-46 | 30 giây đầu hai người khác Elo không bao giờ được ghép (BR-24), dù chỉ có hai người đang tìm; chờ trung bình 19,6 giây | `EloPolicy.PairingKeys`; đo thật 31 giây (Elo 114 và 117) |
| D-47 | Hai người bỏ máy: quyền giao bóng đổi qua lại mãi tới giới hạn 2 giờ, chiếm một máy chủ trận | EV-20, `DedicatedWorker` (7200 giây) |
| D-48 | Vợt Dragon Ruby có trong game nhưng không bán trên staging; triển khai không cập nhật database | Cửa hàng staging; `ssh-deploy.sh` |
| D-49 | Kiểm tra thanh toán (`membership`) và xem replay của admin (`replay-launch`) có sẵn nhưng CI không chạy | `.github/workflows/dockerhub.yml:154` |
| D-13 | Bot Khó chưa được kiểm chứng độ khó với người chơi thật | Mở từ Round 1 |
| D-22 | Android chưa chọn được video để đăng | Mở từ Round 2 |
| – | Mỗi máy chơi hỏi phiên đăng nhập 3 giây/lần; 2.000 người là khoảng 670 lượt/giây, mỗi lượt truy vấn database – ảnh hưởng NFR-04 | `AccountRuntime.Update`, `IsAuthVersionValidAsync` |
| – | Tạo đơn thanh toán và lưu kết quả trận gọi dịch vụ ngoài (PayOS, S3) khi đang giữ giao dịch database | `MembershipService.Checkout`, `OnlineGameService.FinishOnline` |
| – | Form "Đăng bán vật phẩm" nhận cả quần áo, mũ nhưng game chỉ hiển thị vợt có model; thêm xong không thấy gì | CMS, `GameRepository.Catalog` |
| – | Một người trong nhóm chưa mua gói thì cả nhóm nhận "Hãy mua gói thành viên…", không nói ai thiếu | `GameMatchesController.MemberAction` |
| – | Phần "AI" đang là luật tính sẵn; mô hình phân hạng chưa bật vì chưa có phiên nào được gán nhãn (trang gán nhãn có 4 phiên, đều "Chờ gán nhãn") | Trang /coaching, NFR-06 |
| – | Đóng góp mã nguồn lệch: Huy 236, Đạt 131, Kiên 10, Thuận 2 commit | `git shortlog` |

## 4. Nhẹ

| Mã | Lỗi |
|---|---|
| D-38 | Lịch sử trận bot hiện tên tài khoản nội bộ `player9034cf523f53` thay vì Bot |
| D-39 | Phòng riêng: ảnh đại diện phần lớn là bóng người; ảnh chỉ hiện ở ô đội B của chính mình |
| D-40 | Bạn đã ở trong phòng vẫn còn nút MỜI |
| D-41 | Hết giờ xác nhận trong phòng riêng báo "Một người đã hủy tìm trận." dù không ai tìm trận |
| D-42 | Đấu đơn hiện thứ tự giao bóng của đánh đôi "NGƯỜI 1 · LƯỢT 2" |
| D-43 | Cửa hàng: dòng chú thích bị cắt ở 960 × 540; Gemstone không có mô tả hiệu ứng |
| D-44 | Bóng không trả được báo "Bóng chạm vật cố định ngoài sân", người mới không hiểu |
| D-32 | Sau khi khiếu nại được chấp nhận, màn đăng nhập còn dòng chữ hạn chế cũ |
| – | Nút THÀNH VIÊN ở 960 × 540 chỉ vẽ nửa biểu tượng; chữ "KẾT QUẢ CỦA BẠN" chìm vào nền cây |
| – | Tài liệu API (`/swagger`) mở công khai trên staging |
| – | Đĩa nhạc còn chữ "ON THE RUN / ECHO ELECTRONIC" của bài khác; 5 bài mới ghi thể loại tạm "NCS Release" |

## 5. Luồng đã chạy đúng trong lần chấm này

Gói thành viên (màn của người đã mua), trận bot và phân tích AI, mời bạn vào phòng, đổi vị trí, chuyển chủ phòng, xác nhận trận, trận online trên máy chủ riêng, thông báo lỗi theo từng phía, xử thắng khi đối thủ rời trận, tổng kết, phân tích AI trận online, tìm trận xếp hạng tới màn xác nhận, lịch sử, replay, 7 trang admin.

## 6. Chưa kiểm được

Thanh toán thật; tài khoản chưa mua gói; một tài khoản trên hai thiết bị; trận xếp hạng đánh đến hết; đánh đôi; admin bấm "Xem replay" để mở game; Android; nhóm kiểm tra `membership` (máy này không có Docker/PostgreSQL).

## 7. Câu hội đồng dễ hỏi

1. *Vì sao chức năng bắt buộc lại phải trả tiền?* – Phần học vẫn miễn phí, phí bù chi phí máy chủ trận (Report 1 §4); cần giảng viên xác nhận trước.
2. *Thu tiền thật thì hoàn tiền, trẻ vị thành niên, giấy phép game online xử lý thế nào?* – Chưa có câu trả lời; LI-14 ghi là hạn chế. Nhóm cần tìm hiểu quy định về game online và thanh toán trước buổi bảo vệ.
3. *AI ở đâu?* – Luật có ngưỡng, hiệu chỉnh được theo nhãn chuyên gia; mô hình học máy có cổng chất lượng và đang tắt (NFR-06). Nên gán nhãn tối thiểu vài chục phiên để có số đo thật.
4. *Máy chủ chịu được bao nhiêu trận?* – Mỗi container một trận, mặc định 1 container (LI-07); chưa đo tải.
5. *Ai làm phần nào?* – Report 2 §4 và bảng RACI; số commit lệch nên mỗi người phải tự trình bày được luồng của mình.
