# Mô hình Bài học – Bài tập (22/11/2026)

Một **bài học** chứa nhiều **bài tập**. Bài tập là đơn vị người chơi luyện và mang toàn bộ cấu hình (`TrainingDrill`).

## Schema (`src/Database.sql`)

- `Lessons`: nhóm bài tập. Gồm `Code`, `LessonName`, `Description`, `SortOrder`, `PublishStatus`, `ContentVersion`. Không còn `Type`, `Target`, `Config`, `DraftConfig`, `Reward`.
- `Drills`: thêm `Config` (bản đã xuất bản, NULL nếu chưa xuất bản) và `DraftConfig` (bản đang soạn). `Code` là mã mà game dùng để nhận diện bài luyện.
- `LessonDrills`: thêm `SortOrder` (thứ tự bài tập trong bài học). Khóa ngoại của `PlayerLessonDrills` giữ nguyên.
- Seed: 15 bài học cũ được chuyển thành 15 bài học, mỗi bài chứa một bài tập cùng mã, nên tiến độ và mã bài luyện không đổi.

## Giao thức với game

Danh mục gửi cho Unity vẫn là danh sách phẳng `lessons: TrainingDrill[]`, nay lấy từ `Drills.Config` của các bài học đang xuất bản, theo thứ tự bài học rồi thứ tự bài tập. Client Unity không cần sửa; 42 kiểm tra hợp đồng không dùng database đều đạt.

## API quản trị (`api/Admin/cms/lessons`)

| Phương thức | Đường dẫn | Ý nghĩa |
| --- | --- | --- |
| GET | `/` | Danh sách bài học kèm bài tập |
| POST | `/` | Tạo bài học |
| PUT | `/{id}` | Sửa tên, mô tả, thứ tự bài học |
| POST | `/{id}/drills` | Thêm bài tập |
| PUT | `/{id}/drills/{drillId}` | Sửa bài tập (bản nháp) |
| DELETE | `/{id}/drills/{drillId}?expectedVersion=` | Xóa bài tập chưa có dữ liệu người chơi |
| POST | `/{id}/publish`, `/{id}/unpublish` | Xuất bản hoặc gỡ cả bài học |

Mọi thao tác dùng `ExpectedVersion` của bài học (`ContentVersion`) để tránh ghi đè. Xuất bản áp dụng đồng thời mọi bài tập đang soạn của bài học; bài học phải có ít nhất một bài tập.

## Triển khai

Đổi schema nên **database phải được tạo lại từ `src/Database.sql`** (môi trường dev được phép reset theo `AGENTS.md`). Triển khai backend mới lên database cũ sẽ lỗi vì thiếu cột `drills.config`, `drills.draftconfig`, `lessondrills.sortorder`.
