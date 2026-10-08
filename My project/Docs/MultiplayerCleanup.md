# Rà soát script multiplayer — 09/10/2026

## Kết quả

Đã xóa **14 script** cũ hoặc trùng, kèm `.meta` của các script thuộc Assets. Trước khi xóa đã kiểm tra tham chiếu từ C#, scene, prefab, cấu hình và công cụ Editor.

Các file trước khi sửa/xóa được sao lưu theo đường dẫn gốc trong `Temp/MultiplayerCleanup-20261009/`. Thư mục Temp dùng cho khôi phục trong workspace hiện tại, không đưa vào bản build.

## Script đã xóa

| File | Lý do |
|---|---|
| `Assets/Scripts/Multiplayer/CourtSurface.cs` | Marker va chạm PhysX cũ; gameplay dùng `BallFlight` và luật dùng chung với BotMatch. Đã gỡ 17 component khỏi sân trước khi xóa. |
| `Assets/Scripts/Multiplayer/MultiplayerBallContact.cs` | Chuyển tiếp va chạm tới callback không còn xử lý; không còn component sử dụng. |
| `Assets/Scripts/Multiplayer/ShotAnimationLibrary.cs` | Thư viện Animator cũ; `MatchActor` đang dùng clip và thời điểm đánh của BotMatch. |
| `Assets/Scripts/Multiplayer/MultiplayerTestUI.cs` | UI test cũ đã được thay bằng Canvas PlayMultiplayer và `PlayMultiplayerPresenter`. |
| `Assets/Editor/MultiplayerSetup.cs` | Công cụ tạo UI/sân/animation theo kiến trúc cũ; đã có `PlayMultiplayerIntegration`. |
| `Tools/ReplaceMultiplayerSimulation.ps1` | Script chuyển đổi source một lần, không dùng trong gameplay hiện tại. |
| `Tools/PlayMultiplayerUiProbe.cs` | Bản trùng của công cụ được giữ trong Assets/Editor. |
| `Tools/PlayMultiplayerNetworkProbe.cs` | Bản trùng của công cụ được giữ trong Assets/Editor. |
| `Tools/MultiplayerRoomProbe.cs` | Probe phòng của UI test cũ. |
| `Tools/MultiplayerInputProbe.cs` | Probe input dựa trên Animator cũ. |
| `Tools/MultiplayerFinalValidation.cs` | Tham chiếu cấu hình, model và scene cũ. |
| `Tools/MultiplayerEditorCourtProbe.cs` | Probe sân theo luồng Ready cũ. |
| `Tools/MultiplayerCourtVisualFix.cs` | Công cụ sửa hình ảnh sân cũ một lần; sân hiện tại lấy từ BotMatch. |
| `Tools/MultiplayerAnimationProbe.cs` | Probe clip theo Animator cũ. |

## Tham chiếu và code đã dọn

- Xóa scene `Assets/Scenes/UI/MultiplayerTest.unity` và asset `Assets/Settings/Multiplayer/ShotAnimations.asset` dành cho hệ thống cũ.
- Gỡ scene test cũ khỏi cấu hình build, chuẩn hóa Build Profile Windows theo các scene hiện có.
- Gỡ các field model/Animator/paddle/animation cũ khỏi `MultiplayerSettings` và asset cấu hình. Prefab nhân vật/bóng BotMatch vẫn là nguồn gameplay.
- Gỡ callback va chạm rỗng, dictionary cooldown không dùng và property Animator không còn được gọi.
- Gỡ `OpenRoomOnLoad`, `CanChangeMode` và thao tác `ReadyAsync` không còn được UI sử dụng. Giữ `OpenDefaultRoomOnLoad` cho luồng mở phòng hiện tại.
- Gỡ `ShotChord`/`ModifierMode` cũ và 10 test của implementation đó. Input thực tế dùng `LocalMatch.ShotInput`, có các test tổ hợp/nhả phím/hủy input riêng.
- Gỡ field nhãn Ready không dùng của presenter. Cấu trúc dữ liệu session còn các field Ready để giữ tương thích dữ liệu; custom hiện không yêu cầu Ready.

## Script đang dùng

Folder `Assets/Scripts/Multiplayer` còn **24 file C#**:

| Nhóm | File |
|---|---|
| Interface và service | `MultiplayerInterfaces`, `PlayerIdentityService`, `RoomService`, `MatchmakingService`, `MultiplayerSceneConfiguration` |
| UI phòng | `PlayMultiplayerPresenter`, `RoomViewModel`, `MenuPlayMultiplayer` |
| SDK và kết nối | `MultiplayerApp` và hai partial Room/Matchmaking, `SessionNetworkHandler`, `GroupMatchmaker`, `MultiplayerSettings` |
| Gameplay | `BotMatchGameplayService`, `NetworkPlayer`, `NetworkMatch`, `PickleballRules`, `MultiplayerTypes` |
| Scene sân | `CourtBootstrap`, `CourtCamera`, `MultiplayerCourtUI` |
| Kiểm thử nhiều phiên | `MultiplayerSmokeRunner`, `MultiplayerSmokeChecks` — chỉ chạy khi truyền tham số `-pkvCase` |

Giữ 5 công cụ Editor: integration BotMatch, cấu hình cloud, kiểm tra build, probe UI và probe network. Đây là các entry point Editor hoặc công cụ test cần thiết, dù không được gắn vào scene.

## Kiểm chứng sau khi dọn

- Unity biên dịch thành công, **0 lỗi**.
- **61/61 EditMode test đạt**: phòng, đồng hồ tìm trận, luật, BO3 và input/gameplay dùng chung với BotMatch.
- **0 Missing Script** trong PlayMultiplayer, MultiplayerCourt và cả 5 prefab thuộc Prefabs/Multiplayer. Chi tiết: `Docs/MultiplayerCleanupValidation.json`.
- PlayMultiplayer còn presenter, MultiplayerCourt còn bootstrap; các prefab nhân vật/bóng trong settings đều hợp lệ.
- Không còn tham chiếu tới các class, scene hoặc cấu hình vừa xóa trong source/asset đang dùng.
- Build Windows thành công, **0 lỗi**; kết quả tại `Docs/PlayMultiplayerBuildResult.json`.
- Build có 542 cảnh báo: 43 cảnh báo script thiếu ở scene Menu, 498 cảnh báo shader và 1 cảnh báo cấu hình Pipeline Editor. Các cảnh báo này nằm ngoài hai scene multiplayer đã kiểm tra; scene Menu còn cần xử lý các tham chiếu UI thiếu.

Các test kết nối nhiều phiên được ghi ở báo cáo Windows trước đó; lần dọn này kiểm tra lại biên dịch, luật/input, asset và build.
