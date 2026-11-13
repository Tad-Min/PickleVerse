# Kiểm thử PickleVerse

Các ca dùng S3 thật và dọn fixture Unity đọc `S3_ACCESS_KEY` và `S3_SECRET_KEY` từ môi trường tiến trình. Dùng credential của stack dev đang chạy. `PKV_TEST_S3_URL` có thể ghi đè địa chỉ mặc định `http://127.0.0.1:19000`.

Mã kiểm thử hiện được nhóm theo môi trường chạy. Các assembly, dependency và nhóm ca hiện có vẫn được dùng để kiểm tra hồi quy.

```text
src/Testing/
  Backend/                 IntegrationChecks của .NET
  AiService/               pytest cho FastAPI, coaching và model
  Unity/EditMode/          Các assembly NUnit và file .meta của Unity
  Sync-UnityTests.ps1      Đồng bộ test Unity trước khi chạy
```

## Cách chạy hiện tại

Từ thư mục repository, chạy ca hợp đồng ticket và coaching cục bộ hoặc dùng script tích hợp với PostgreSQL development đã xác minh.

```powershell
dotnet run --project src/Testing/Backend -- --check-client-security
./scripts/Invoke-FunctionalChecks.ps1 -Build -Check schema,api-contract,training-sync,dedicated,expert-review
```

Các ca tích hợp tạo database tạm từ `src/Database.sql`. Các cờ cấp tài khoản fixture như `-ProvisionCoachingPilot` dùng riêng khi cần chuẩn bị kiểm thử thủ công. Không đưa các cờ đó vào lượt chạy hồi quy thông thường.

Chạy `python -m pytest -q` từ `src/AiService` như trước. `pyproject.toml` trỏ pytest sang test ở `src/Testing/AiService`. Fixture hợp đồng dùng chung vẫn nằm trong `contracts/` để Unity, API và FastAPI đối chiếu cùng định dạng.

Chạy `./src/Testing/Sync-UnityTests.ps1` sau khi clone và sau khi sửa test Unity. Script đồng bộ mã và GUID `.meta` sang `My project/Assets/PickleVerse.Testing.Generated`. Thư mục này là bản sinh tự động, đã loại khỏi Git. Mở **Window → General → Test Runner → EditMode** trong Unity Editor để chạy. Thay đổi test ở `src/Testing/Unity/EditMode` rồi đồng bộ lại. Các script smoke và probe Editor hiện có vẫn dùng các điểm chạy cũ.

Web admin hiện có `npm run type-check` và `npm run build` trong `src/Frontend`. Có thể bổ sung bộ test trình duyệt sau khi thống nhất phạm vi E2E.

## Các kiến trúc có thể áp dụng

| Kiến trúc | Bố trí | Phù hợp khi | Chi phí vận hành |
| --- | --- | --- | --- |
| Theo runtime | Mỗi thư mục Unity, Backend, AiService và Frontend dùng runner riêng. | Cần chạy nhanh phần vừa sửa, bám công cụ hiện có. | Cần một nơi ghi rõ các ca đi qua nhiều dịch vụ. |
| Theo cấp độ | Chia `Unit`, `Contract`, `Integration` và `E2E` ở cấp đầu tiên. | CI phân quyền và chạy theo từng cổng kiểm thử. | Mỗi cấp chứa nhiều ngôn ngữ và phải điều phối nhiều runner. |
| Kết hợp | Giữ mã theo runtime, định nghĩa các cổng và kịch bản xuyên hệ thống ở cấp repository. | Game có Unity, API, PostgreSQL, Redis, web admin, FastAPI và Photon Fusion. | Cần thống nhất ai sở hữu fixture và kịch bản chung. |

## Đề xuất cho PickleVerse

Áp dụng **kiến trúc kết hợp**. Giữ bố trí mã hiện tại trong `src/Testing` và tách việc chạy thành các cổng theo rủi ro.

1. **Kiểm tra nhanh khi sửa code.** Unity EditMode cho luật điểm, training và offline store. .NET cho ticket và hợp đồng API. pytest cho rules, telemetry, ngưỡng và kiểm tra artifact. TypeScript cho kiểu dữ liệu và build admin.
2. **Tích hợp với hạ tầng thật trước khi hợp nhất.** Dùng PostgreSQL tạm từ `src/Database.sql`, namespace Redis riêng và FastAPI test local. Ưu tiên sync lặp, reset theo thế hệ, phần thưởng, publish CMS, ban/appeal và tranh chấp kết thúc trận. Đối chiếu enum và JSON giữa API, Unity và web admin.
3. **E2E trước bản phát hành.** Giữ một tập nhỏ các hành trình có giá trị cao. Ví dụ Unity vào phòng Shared rồi chuyển sang Dedicated 1v1/2v2, mất kết nối và quay lại, admin dừng trận, hoàn thành bài offline rồi đồng bộ sau khi mở game lại. Dùng stack và tài khoản test riêng, giữ log API, Dedicated và Unity theo cùng mã trận hoặc mã lượt.
4. **Nghiệm thu AI độc lập.** Dữ liệu mô phỏng kiểm tra đường ống và cổng phục vụ. Đánh giá chất lượng phân loại và coaching bằng tập người thật có nhãn chuyên gia theo các ngưỡng đã chốt. Lưu báo cáo và checksum cùng artifact.

Khi cần thêm công cụ, ưu tiên PlayMode cho vòng đời scene và UI Unity, cùng một vài ca E2E trình duyệt cho CMS và moderation. Giữ hạ tầng có chi phí cao như Photon Dedicated cho cổng phát hành hoặc lượt chạy chủ động.
