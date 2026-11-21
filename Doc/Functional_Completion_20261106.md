# Hoàn thiện chức năng — 06/11/2026

Kế hoạch đã được người dùng duyệt trong chat. Baseline: `30aba28`.
Bản đối chiếu các thay đổi có sẵn: `artifacts/functional-completion-20261106/preexisting.patch`.

## Quyết định cố định

- API → Services, Entities; Services → Repositories, Entities; Repositories → Entities.
- Photon Fusion Shared cho phòng chờ, Unity headless Dedicated cho trận đấu. Không tự fallback trận sang Shared.
- Npgsql EF Core/IUnitOfWork; schema/seed chỉ từ src/Database.sql; được reset đúng development database.
- Offline sau lần đăng nhập đầu; đồng bộ theo lượt, server xác nhận thưởng; không lặp thưởng sau reset.
- 15 bài: thêm giao bóng luân phiên, dink chéo và volley hồi vị. 10 bài nhận THOT, cấp 25 nhận GEMSTONE.
- Onboarding Người mới → Tutorial, Đã thành thạo → Menu. ML ba mức không đổi Elo.
- UI game tiếng Việt, giữ tên kỹ thuật. Client thử trong Editor; Linux cập nhật cuối.
- Dữ liệu người thật/chuyên gia do người dùng bố trí; không giả nghiệm thu AI bằng dữ liệu tổng hợp.

## Theo dõi

- [ ] Phase 1: enum/hợp đồng, lỗi nền, môi trường local và Dedicated.
- [ ] Phase 2: CMS có phiên bản, 15 bài, phần thưởng cosmetic.
- [ ] Phase 3: onboarding, offline, reset theo nhóm.
- [ ] Phase 4: xử lý vi phạm, tố cáo, khiếu nại.
- [ ] Phase 5: live monitoring, analytics.
- [ ] Phase 6: telemetry gốc, dữ liệu/gán nhãn, coaching/model.
- [ ] Phase 7: hồi quy và triển khai Linux sau các cổng nghiệm thu.

## Cổng dữ liệu AI

Model: holdout độc lập ≥ 10 người/mức, macro-F1 ≥ 0,70, hơn majority baseline ≥ 0,05.
Inference: ≥ 20 cơ hội đúng ngữ cảnh, confidence ≥ 0,65.
Coaching: precision ≥ 80%, recall ≥ 70% cho từng lỗi, chuyên gia duyệt ngưỡng/lời khuyên.
Chưa có dữ liệu người thật: chưa bật model hoặc triển khai Linux cuối.

## Mã và kiểm thử đã bổ sung (chưa thay cho nghiệm thu Editor/E2E)

- CMS draft/edit/publish/unpublish, snapshot danh mục bất biến, 15 bài và grant cosmetic một lần.
- Onboarding, hàng đợi offline bền theo tài khoản/môi trường, receipt từng lượt, kiểm tra epoch và fingerprint.
- Reset có preview/revision/request ID, chia bốn nhóm; giữ lịch sử thưởng, ngăn AI job cũ phục hồi profile.
- Quyết định ban/suspend có thời hạn/lý do/audit, thu hồi token; phiên `appeals` bị chặn khỏi API chơi; khiếu nại có kết luận bất biến.
- Tố cáo chống tự tố cáo/bot/ghi đè; review cùng giao dịch với sanction và lệnh dừng Dedicated.
- Lệnh dừng bền, heartbeat chuyển lệnh, ack gắn lease; kết quả đến muộn không thay trạng thái gián đoạn.
- Dashboard live tách lịch sử; bổ sung heartbeat, điểm, reconnect và trạng thái lệnh dừng.
- DAU từ login (không refresh), retention theo ngày Việt Nam, số liệu không giới hạn 1.000 bản ghi, bài phổ biến, heatmap và journal ghép trận.
- Nhật ký thời gian theo ngày dùng cùng cơ chế hợp nhất khoảng trùng và phục hồi commit của tổng thời gian.
- Sửa lỗi converter JSON mặc định ghi đè converter enum chuẩn; kiểm tra cả cấu hình JSON dùng thực tế.
- Màn admin xem bằng chứng cú đánh, lưu đánh giá có phiên bản và xuất dataset với checksum. Pipeline tách train, hiệu chỉnh và holdout theo người chơi. Cách thu phiên thật và duyệt nhãn nằm trong [hướng dẫn thu dữ liệu](AI_Expert_Review.md).

Bằng chứng trong `artifacts/functional-completion-20261106/`:

- Backend và ảnh Docker API/AI đã build. Kiểm tra admission/coaching chạy 18 ca đạt. Lượt kiểm tra mới nhất trên PostgreSQL đạt schema 51, enum 202, CMS 21, sync/onboarding 18, Dedicated 26, hybrid 36 và gán nhãn 8. Kiểm tra hợp đồng API đạt 34 ca.
- Hồi quy PostgreSQL/Redis trên bản hiện tại đạt auth 132, matchmaking 28, app activity 31, app/ranked 22, online game 84, online multiplayer 34, reset 21, moderation/auth/appeal 19 và analytics 17.
- Unity EditMode đạt 513/513. PlayMode có 0 bài kiểm thử được đăng ký. Web admin đã qua `npm run type-check` và `npm run build`.
- Python đạt 22/22, gồm xác thực FastAPI và kiểm tra artifact coaching sai checksum/ngữ cảnh.

Lượt chạy đầu với các ca CMS tham chiếu mới bị xét duyệt tự động từ chối do hết hạn mức. Lượt chạy được duyệt sau đó đã qua các ca CMS, schema, Dedicated, hybrid và expert review trên bản mới. Stack local đã cập nhật từ `src/Database.sql` và API/AI mới. Backup trước lần nạp schema này nằm trong `artifacts/functional-completion-20261106/before-reset-20261106-105212.dump`. API trả sẵn sàng, web admin local mở được và HTTP đã xác nhận Player bị chặn, Admin xem được màn gán nhãn.

Các phase vẫn để mở. Cần nghiệm thu UI/gameplay trong Editor, 1v1/2v2 Dedicated và các ca kết nối lại/dừng trận. Đã chuẩn bị tài khoản và mở Unity Editor cho phiên thu thử. Trong khoảng theo dõi đầu tiên chưa có phiên người thật đồng bộ. ML và hiệu chỉnh Dink chưa bật. Chưa triển khai Linux.
