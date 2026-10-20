# Unity và backend: đợt tài khoản online

Ngày kiểm tra: 20/10/2026. Unity `6000.6.2f1`, ASP.NET Core `.NET 10`, PostgreSQL 16, Docker Desktop.

## Phạm vi đã chốt

- Đăng ký -> OTP email Gmail thật -> đăng nhập -> tải/cập nhật hồ sơ -> đăng xuất.
- Bỏ khách/offline trong luồng chạy. Xóa save hồ sơ, replay, ví demo và trạng thái tutorial cũ trên máy phát triển.
- Tạm khóa training, thi đấu, shop, nhiệm vụ, lịch sử và social khi các phần này chưa được nối backend.
- Chuyển Photon và đồng bộ replay/S3 ở đợt tiếp theo; không thay netcode trong đợt này.
- Các thay đổi backend/Compose/OTP và bản sửa phân quyền Admin đều đã được người dùng duyệt trước khi sửa.

## Chạy môi trường dev

Tại thư mục repository:

```powershell
docker compose up -d --build
docker compose ps
```

| Dịch vụ | Địa chỉ trên máy phát triển |
|---|---|
| API | http://localhost:8080 |
| API readiness (kết nối DB) | http://localhost:8080/health/ready |
| Swagger UI dev | http://localhost:8080/swagger |
| OpenAPI dev | http://localhost:8080/openapi/v1.json |
| PostgreSQL Compose | 127.0.0.1:15432 |
| pgAdmin Compose | http://localhost:15050 |
| S3-compatible dev | http://localhost:19000 |

Các cổng 15432/15050/19000 được chọn vì máy đã có container khác dùng 5432/5050/9000. Những container cũ được giữ nguyên. Compose có thể đổi cổng bằng `POSTGRES_PORT`, `PGADMIN_PORT`, `S3_PORT`, `API_PORT` trong `.env`; nếu đổi cổng API cần đổi cấu hình Unity tương ứng.

Compose chỉ bind cổng ra loopback. Volume S3 dev mới là `pickleverse_dev_s3_data`; không dùng chung với volume của container S3 cũ.

Swagger UI chỉ bật khi `ASPNETCORE_ENVIRONMENT=Development`, đọc tài liệu OpenAPI hiện có tại `/openapi/v1.json`. Cấu hình dùng `Swashbuckle.AspNetCore.SwaggerUI` theo [hướng dẫn Microsoft cho .NET 10](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/using-openapi-documents?view=aspnetcore-10.0). Docker dev dùng cổng 8080; cổng 5000 không được publish.

`.env` nằm ngoài Git; không đưa file này vào Unity hoặc chia sẻ build. `.env.example` cung cấp tên biến cần cấu hình:

```dotenv
JWT_KEY=<random secret, at least 32 characters>
SMTP_USERNAME=<sending Gmail address>
SMTP_APP_PASSWORD=<Google App Password>
SMTP_FROM_EMAIL=<same sending address>
```

Gmail dùng `smtp.gmail.com:587` với STARTTLS. App Password yêu cầu cấu hình tài khoản Google phù hợp; xem [Google App Password](https://support.google.com/accounts/answer/185833). Credential đã được cấu hình tại máy và SMTP đã chấp nhận email OTP thử; kết quả này không thay cho việc kiểm tra hộp thư/spam của người nhận.

Database mới tự chạy `src/Database.sql` và `src/DevAuthSetup.sql`. Với volume DB đã tồn tại, init script không tự chạy lại: áp dụng `DevAuthSetup.sql` vào đúng DB trước khi chạy backend mới. Script nâng độ dài cột OTP, thêm bộ đếm nhập sai và seed role bằng thao tác idempotent, không xóa dữ liệu.

## Cấu trúc Unity

| Vị trí trong `My project/Assets/Scripts` | Vai trò |
|---|---|
| `Services/Api/AccountModels.cs` | DTO tương ứng hợp đồng REST của backend |
| `Services/Api/ApiClient.cs` | JSON camelCase, phân tích lỗi API/ProblemDetails; không đưa stack trace vào UI |
| `Services/Api/UnityApiTransport.cs` | UnityWebRequest bất đồng bộ, timeout, cancellation và Bearer header |
| `Services/Utilities/ApiUrl.cs` | Kiểm tra URL; HTTP chỉ được chấp nhận trên loopback trong Editor/development |
| `Services/AccountService.cs` | Đăng ký, OTP, đăng nhập, refresh, logout và hồ sơ |
| `Services/AccountRuntime.cs` | Vòng đời service và cấu hình dùng chung qua scene |
| `FrontEnd/AccountScreen.cs` | UI tiếng Việt theo font và bảng màu lobby; chặn truy cập khi chưa đăng nhập |
| `Progression/ProgressionRuntime.cs` | Chiếu hồ sơ server cho các binding menu cũ, không mở/ghi file player local |

Asset cấu hình: `My project/Assets/Resources/BackendApiSettings.asset`. Base URL hiện là `http://localhost:8080`, không kèm `/api`. Dùng HTTPS cho bản release; bản Windows hiện tại là development để gọi Docker dev qua HTTP loopback.

Token và mật khẩu không được lưu xuống PlayerPrefs hoặc file. Mỗi lần chạy lại game cần đăng nhập. Trong phiên, service thực hiện một refresh dùng chung khi nhiều request đồng thời nhận 401; request được thử lại tối đa một lần sau refresh. Không tự thử lại giao dịch ghi khi mất mạng.

Ví dụ dùng service từ Unity main thread:

```csharp
using PickleVerse.Services;
using PickleVerse.Services.Api;

var account = AccountRuntime.Ensure().Account;
await account.LoginAsync(email, password, cancellationToken);
var profile = await account.ReloadProfileAsync(cancellationToken);
await account.UpdateProfileAsync(new UpdateProfileRequest
{
    PlayerName = newName,
    IsMale = true
}, cancellationToken);
await account.LogoutAsync(cancellationToken);
```

## Hợp đồng API đã nối

| Method | Route |
|---|---|
| POST | `/api/Auth/register` |
| POST | `/api/Auth/verify-email` |
| POST | `/api/Auth/resend-otp` |
| POST | `/api/Auth/login` |
| POST | `/api/Auth/refresh-token` |
| POST | `/api/Auth/logout` |
| GET | `/api/Profiles/me` |
| PUT | `/api/Profiles/me` |

Đăng ký chỉ gửi bốn trường sau; không gửi `playerName`:

```json
{
  "email": "ban@example.com",
  "password": "ExamplePass!23",
  "confirmPassword": "ExamplePass!23",
  "isMale": true
}
```

Backend bắt buộc xác nhận mật khẩu khớp, tự sinh tên mặc định `player` + 12 ký tự hex ngẫu nhiên, ví dụ `playera4f8b2c9d103` (18 ký tự). Database giữ ràng buộc tên duy nhất; server thử sinh lại nếu gặp trùng tên. Form Unity không yêu cầu nhập tên khi đăng ký; sau đăng nhập vẫn đổi tên ở hồ sơ như trước. Tên và hồ sơ của tài khoản đã có được giữ nguyên.

Xác nhận mật khẩu dùng [CompareAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.dataannotations.compareattribute?view=net-10.0); hậu tố tên dùng [RandomNumberGenerator.GetHexString](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.gethexstring?view=net-10.0) của .NET.

OTP có 6 chữ số, hạn 10 phút, tối đa 5 lần nhập sai cho một mã, chờ 60 giây trước khi gửi lại. Mã được gắn với tài khoản và lưu dưới dạng HMAC; token refresh được lưu dưới dạng hash và xoay khi sử dụng. Access token hạn 2 giờ, refresh token hạn 14 ngày. API auth có giới hạn 20 yêu cầu/phút/IP trong một cửa sổ cố định.

## Đối chiếu hai tài liệu đề tài

| Nhóm tính năng | Source hiện có | Trạng thái kết nối trong đợt này |
|---|---|---|
| Tài khoản/hồ sơ | Trước đây Unity dùng player local; backend có REST/JWT | Đã nối tài khoản online, OTP Gmail, hồ sơ server |
| Tutorial, training và bot | Có các script tutorial, training, chấm sao, bot và scene tương ứng | Giữ source, khóa lối vào; chưa kết luận chất lượng gameplay qua kiểm thử của đợt auth này |
| Tiến trình, nhiệm vụ, shop, social | Có engine/model/UI local và backend controller tương ứng | Không dùng save/fixture local làm dữ liệu thật; đợi adapter và mapping API |
| Multiplayer | Manifest/source hiện dùng NGO và Unity Services | Chưa chuyển Photon; cần chốt sản phẩm/topology ở đợt sau |
| Replay | Unity có recorder/repository local; backend có storage/replay API | Save cũ đã xóa; chưa nối upload/download từ Unity |
| Admin | Có controller quản trị | Sửa role guard và kiểm thử 401/403/200; không xây admin web trong đợt này |

Source thực tế dùng .NET và uGUI; proposal còn nêu lựa chọn backend Java/Python và UI Toolkit. Việc tích hợp hiện tại đi theo stack/source đang có và yêu cầu mới của người dùng.

## Xác minh

- Build backend thành công, không lỗi/cảnh báo ở lượt build xác minh.
- 43 kiểm tra auth với PostgreSQL thật: hợp đồng đăng ký bốn trường, xác nhận mật khẩu, tên mặc định ngẫu nhiên (kể cả thử lại khi trùng tên), giới tính, email chưa xác thực, OTP sai/hết hạn/dùng lại, cooldown, khóa mã, gửi lại, refresh xoay/vô hiệu hóa, logout, tài khoản tạm khóa và request đồng thời. Database test riêng được xóa sau kiểm tra.
- Một luồng kiểm tra có gửi OTP thật qua Gmail SMTP, xác thực và đăng nhập trong DB test riêng. Ngoài ra, đã gọi `POST /api/Auth/register` tới chính API trong Docker: nhận HTTP 200 sau khi Gmail SMTP chấp nhận email OTP; tài khoản test vừa tạo được xóa ngay sau đó. Các case còn lại dùng mail adapter thu mã trong bộ nhớ, không gửi hàng loạt thư.
- 14 NUnit test Unity service đã qua: refresh đồng thời, clear phiên khi login đang chờ, request/logout của phiên cũ không xóa phiên mới, logout sau refresh và URL/ProblemDetails.
- Lượt kiểm tra đăng ký mới: Swagger có đúng `email`, `password`, `confirmPassword`, `isMale`; Docker API trả 400 cho xác nhận sai/thiếu và mật khẩu quá ngắn. Editor xác nhận ô tên được ẩn khi đăng ký, nút đăng ký báo mật khẩu không khớp và lựa chọn giới tính hoạt động. Báo cáo service mới: `artifacts/verification/unity-registration-tests.json`.
- Bản Windows đã cập nhật theo hợp đồng đăng ký mới, build 0 lỗi và 1 cảnh báo Pipeline như mô tả bên dưới. Smoke-test đăng nhập/hồ sơ/đăng xuất thành công: `artifacts/verification/windows-registration-smoke.json`; tài khoản fixture đã xóa. Ảnh form mới: `artifacts/verification/unity-registration-contract.png`.
- Editor Play Mode: chứng minh frame đang tăng; nhập vào input/bấm button thật để login và lưu tên/giới tính. Đọc lại từ REST xác nhận thay đổi đã lưu server. Không tạo lại thư mục LocalPlayers/Replays.
- HTTP: `/health/ready` -> 200, hồ sơ chưa đăng nhập -> 401, gọi auth lặp lại -> 429.
- Swagger UI: Docker API đã rebuild; `/swagger`, HTML, CSS/JavaScript và `/openapi/v1.json` đều trả 200. Trình duyệt đã hiển thị danh sách endpoint và schema; ảnh tại `artifacts/verification/swagger-ui.png`.
- Phân quyền Admin: chưa đăng nhập -> 401, Player -> 403, Admin -> 200; sử dụng fixture tạm và khôi phục role sau kiểm tra.
- Windows development: build cuối thành công với 0 lỗi; executable chạy ở batch mode, xác nhận Development flag, login, cập nhật hồ sơ, logout và không tạo save local đều thành công. Kết quả cuối: `artifacts/verification/windows-account-final-v3.json`.

Build cuối còn một cảnh báo của package Pipeline: chưa có RuntimePipelineConfig, nên tính năng điều khiển Player từ xa của package này không bật. API/đăng nhập không phụ thuộc tính năng đó. Một lượt build sau Play Mode từng gặp cache URP chứa tham chiếu runtime đã bị hủy; cache build đã được làm mới bằng API Dispose của package và lượt build cuối không còn lỗi. Không sửa source package hay renderer của dự án.

Báo cáo máy đọc nằm trong `artifacts/verification/`. Bản Windows nằm trong `artifacts/WindowsDev/`; chạy executable ngay trong thư mục đó cùng thư mục Data/DLL đi kèm. Build đợt này chỉ gồm Menu và Settings theo phạm vi tài khoản online.

Các tài khoản fixture và file credential kiểm thử đã được dọn. Mở Menu trong Unity, nhấn Play rồi đăng ký tài khoản thực để sử dụng. Ảnh UI đã kiểm tra nằm trong `artifacts/unity/`.

Chạy lại kiểm thử backend (mặc định không gửi thư):

```powershell
$env:PKV_TEST_POSTGRES = 'Host=127.0.0.1;Port=15432;Database=postgres;Username=sa;Password=12345'
dotnet run --project src/Backend/IntegrationChecks/IntegrationChecks.csproj
```

Chỉ bật `PKV_RUN_SMTP_TEST=1` khi chủ động muốn gửi một OTP tới Gmail gửi đang cấu hình. Không đưa output chứa token hoặc credential vào log.

Phát hiện replay/S3 dành cho đợt sau được ghi trong `Doc/Backend_Followup_Findings.md`.
