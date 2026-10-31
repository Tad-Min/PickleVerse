# Backend phân tầng và ApiResponse — 31/10/2026

Áp dụng kiến trúc người dùng xác nhận, tham khảo cách tổ chức tại `D:/Repository/SmashHub/BESmashClub`. Giữ bốn project hiện có:

```text
Repositories -> Entities
Services     -> Entities, Repositories
API          -> Entities, Services
```

## Trách nhiệm và cách viết service

- `Entities/Models`: model EF cho các bảng đã có trong `src/Database.sql`.
- `Entities/DTOs`: DTO nghiệp vụ và `Common/ApiResponse.cs`.
- `Entities/Contracts`, `Entities/Game`: model phòng, tiến trình, telemetry, replay và các request/response của game.
- `Repositories`: DbContext, Fluent API, repository, Redis, khóa PostgreSQL và persistence.
- `Services/Interfaces`, `Services/Implementations`: hợp đồng và xử lý nghiệp vụ; service lấy repository qua `IUnitOfWork`.
- `Services/Rules`: các quy tắc C# thuần do backend quản lý; không biên dịch source từ Unity Assets.
- `API`: controller gọi interface service; đăng ký nghiệp vụ qua `AddPickleVerseServices`. `Program.cs` trực tiếp bind section `Redis` từ `builder.Configuration`, kiểm tra giá trị và đăng ký singleton `IConnectionMultiplexer`.

`RedisSettings` ở `Entities/Configuration` chỉ chứa giá trị cấu hình và validation, không đọc `IConfiguration`. Redis cache/repository nhận settings và kết nối qua DI; `Repositories/DependencyInjection.cs` không tự đọc cấu hình hoặc tạo kết nối Redis. `API` vẫn chỉ có Project Reference tới `Entities` và `Services`; package `StackExchange.Redis` được tham chiếu trực tiếp để cấu hình kết nối trong composition root.

`IUnitOfWork.Game` dùng cùng scoped `PickleVerseContext` với các repository hiện có. Các thao tác trận đấu, Elo, điểm, số dư, thưởng và biên nhận chống lặp tiếp tục dùng chung transaction. Không thêm `SaveChanges` tách rời làm mất tính nguyên tử chỉ để giống một CRUD đơn lẻ.

Ví dụ quy ước service:

```csharp
public sealed class ExampleService : IExampleService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExampleService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PlayerProfileResponse> ReadAsync(Guid playerId)
    {
        var player = await _unitOfWork.Profiles.GetByUserIdAsync(playerId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ.");
        // Ánh xạ model sang DTO theo chức năng cụ thể.
        return Map(player);
    }
}
```

Mẫu SmashClub `GenericRepository.CreateAsync` tự gọi `SaveChangesAsync`; vì vậy `CourtId` đã có trước lần đọc lại. PickleVerse giữ ranh giới lưu hiện có và transaction cho từng use case. Không đưa `GetContext()` ra service để truy vấn EF trực tiếp.

## Hợp đồng HTTP

Response JSON thành công:

```json
{"success":true,"message":"Success","data":{"...":"DTO của endpoint"}}
```

Response JSON lỗi:

```json
{"success":false,"message":"Không tìm thấy hồ sơ."}
```

`code` được giữ khi kết quả cũ có mã lỗi; lỗi validation có thêm `errors`. `ApiResponseFilter` thống nhất các action JSON và tránh bọc hai lần nếu controller đã trả `ApiResponse`. `GlobalExceptionHandler` xử lý exception tập trung:

| Trường hợp | HTTP |
|---|---:|
| `KeyNotFoundException` | 404 |
| `UnauthorizedAccessException` khi đã xác thực nhưng thiếu quyền | 403 |
| Chưa xác thực / token không hợp lệ | 401 |
| `ArgumentException`, validation request | 400 |
| `AppException` | Theo status nghiệp vụ |
| Lỗi nội bộ không dự kiến | 500, không trả stack trace / chi tiết exception |
| Dependency tạm thời không sẵn sàng | 503 |

Các file download giữ nội dung và media type. OpenAPI mô tả envelope cho response JSON. `/health/ready` là endpoint hạ tầng, tiếp tục trả status readiness.

Unity `ApiClient` và Axios interceptor của frontend đã đọc `data`; giữ thông điệp và mã lỗi. Client ngoài repository cần cập nhật theo hợp đồng này. Frontend không xóa phiên đăng nhập chỉ vì gặp 403.

## Persistence và kiểm chứng

Đã bổ sung mapping cho các bảng/cột có sẵn nhưng trước đó chưa được EF quản lý, gồm catalog, currency transactions, command receipts, training và Hybrid AI. Không thêm bảng/cột hoặc migration, không reset database development trong lượt này. SQL đặc thù PostgreSQL như row/advisory locks, sequence, upsert và atomic `SKIP LOCKED` nằm trong repository.

Đạt 613 assertions qua 19 bộ kiểm tra, gồm 39 đối chiếu tên bảng/cột EF với schema tạo từ `Database.sql`, 33 kiểm tra HTTP/kiến trúc/client Unity thực tế, tài khoản/OTP/token, retry/concurrency trận đấu, relational progress, Redis, Hybrid/load và HTTP/S3 replay. Tổng là số assertions đã chạy; một số bộ có phạm vi giao nhau.

Backend Release và frontend type-check/build đạt. Vite có cảnh báo bundle lớn; chưa chạy gameplay/UI Unity nhiều client trong lượt này. Client Unity C# thuần được biên dịch và kiểm tra trực tiếp trong IntegrationChecks. Chỉ project kiểm thử được link file client này; ba project backend production không link Unity Assets.

Bằng chứng: `artifacts/backend-layering/summary.json`, các log kiểm tra, `release-build.log`, `api-contract.log`. Các suite dùng database `pkv_auth_checks_<uuid>`, Redis key và object S3 riêng, tự dọn sau khi chạy; email được capture, không gửi SMTP thật.

Đã build image và cập nhật riêng `pickleverse_api`. Đạt thêm 11 HTTP checks trên container đang chạy: readiness, Swagger/OpenAPI, DTO envelope, validation 400, xác thực 401, profile/route 404; test controller không xuất hiện trong API production. Kết quả ở `artifacts/backend-layering/live-smoke.json`. Container AI fixture đã xóa; số database `pkv_auth_checks_%` còn lại sau kiểm tra là 0.
