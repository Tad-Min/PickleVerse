# Multiplayer PickleVerse — PlayMultiplayer

## Chạy bản mới

Windows cập nhật lúc **23:36 ngày 09/10/2026**, build `build_0b24d1be48fa`: sửa NullReferenceException ở nút Về phòng khi NetworkMatch đã despawn. Bảng điểm vẫn chỉ hiện tỉ số và đã bỏ TechniqueBar. Sau mỗi lần sửa scene/script phải build lại bản Windows; đóng phiên game cũ rồi mở lại. Thay cả thư mục build khi chuyển bản mới sang máy khác.

1. Trong Unity, mở `Assets/Scenes/UI/PlayMultiplayer.unity` rồi bấm Play.
2. Hoặc chạy `Builds/Multiplayer/PickleVerse.exe`. Khi gửi cho bạn bè, gửi **toàn bộ folder Multiplayer**, gồm exe, `PickleVerse_Data` và các DLL; mọi máy dùng cùng bản build.
3. Mỗi phiên dùng danh tính đăng nhập riêng. Mặc định lấy PID; có thể chạy `PickleVerse.exe -pkvProfile Player1`, phiên thứ hai dùng `Player2`.
4. Nhập tên ở `TxtPlayerName`. Bỏ trống tạo 4 chữ cái. Tên là bí danh hiển thị; Authentication ID riêng giữ nguyên để kết nối lại. Không cho tên trùng trong cùng phòng.

## UI mới

Giao diện PlayMultiplayer đã được chỉnh theo ảnh tham chiếu ngày 09/10/2026: bảng nhập/chia sẻ mã phòng bên trái, thẻ người chơi bên phải, nền xanh tối và viền mint. Cả 1Vs1, 2Vs2 và Custom dùng cùng phong cách. Canvas co theo độ phân giải, giữ khung nội dung ở giữa; đã kiểm tra 1280×720, 1024×768, 1920×1080 và 2560×1080.

Artwork UI nằm tại `Assets/FrontEndContent/Multiplayer`. Ảnh nhân vật hiện tại là hình trang trí lấy từ prefab MaleActor; avatar cá nhân chưa triển khai. Ghế trống hiện bóng người và “Chờ người chơi”. BtnInvite vẫn là placeholder chưa triển khai.

Ảnh xem trước: `Docs/MultiplayerScreenshots/PlayMultiplayer-2Vs2-redesign.png` và `PlayMultiplayer-Custom-redesign.png`. Kiểm tra bố cục và nút: `Docs/PlayMultiplayerRedesignValidation.json`, `Docs/PlayMultiplayerRedesignLiveValidation.json`. Bản scene/presenter trước khi chỉnh nằm tại `Temp/PlayMultiplayerRestyle`.

- Vào scene mở 1Vs1. BtnPlay là **Tìm trận**; sau khi bấm trở thành **Hủy**.
- Đồng hồ tăng `00:00`…`00:05`; gửi tìm thực sự khi đạt `00:06`. Hủy được suốt lúc chờ, tìm và kết nối. Giới hạn tìm 120 giây từ lần gửi đầu tiên.
- Btn2Vs2 tạo phòng nhóm tối đa 2 người. TxtRoomId/BtnCopy hiện mã; bạn nhập mã trong TxtJoinID và bấm BtnJoin. Chủ nhóm tìm, cả hai hủy được. Hai bạn luôn cùng đội.
- Btn1Vs1 từ chủ nhóm giải tán nhóm, mời khách ra và mở phòng solo. Khách chọn 1Vs1 thì rời nhóm của mình.
- BtnCustom tạo phòng 4 chỗ, mặc định dùng đội tối đa 2 người. Host ở A1, khách tự phân đội cân bằng. BtnSwap đổi đội mình; host có BtnKick. Custom cho phép **1vs1, 1vs2, 2vs1, 2vs2**, mỗi đội phải có người. **Không dùng Ready**; chỉ host bấm Bắt đầu. Khách thấy Chờ chủ phòng.
- ImgIcon và BtnInvite để dành phát triển sau.
- Hết trận bấm Về phòng; nhóm được giữ. Host bấm Game tiếp giữa các game; tự tiếp tục sau 120 giây.

Đã sửa lỗi nút Về phòng đọc `NetworkMatch.Instance.BetweenGames` sau khi NetworkMatch bị despawn. UI cập nhật nhãn/nút cả khi đối tượng trận không còn tồn tại; chỉ host của trận còn spawned được bấm Game tiếp. Đã kiểm tra nút Game tiếp sang game 2 và bấm Về phòng sau despawn trên Editor host cùng client Windows; kết quả tại `Docs/CourtReturnNullFixVerification-20261009.json`.

## Gameplay từ BotMatch

Multiplayer dùng prefab nhân vật, clip, grip/IK vợt, thời điểm chạm/thả bóng, chuyển động, camera và sân từ BotMatch. `MatchMovement`, `ShotTrajectory`, `ShotInput`, `MatchActor`, `BallFlight` được dùng chung. BotMatch giữ AI riêng; multiplayer nhận input của người sở hữu và mô phỏng luật/bóng tại host.

| Phím | Hành động |
|---|---|
| WASD / mũi tên | Di chuyển theo hướng sân của mình |
| Shift / Space | Chạy |
| J | Drive / giao bóng khi tới lượt |
| K | Dink |
| L | Volley |
| J+K | Drop |
| J+L | Smash |
| K+L | Reset volley |
| U + phím đánh | Đánh mạnh như BotMatch |
| I + phím đánh | Đánh chéo |
| Backspace | Hủy tổ hợp đang chọn |

Giữ tổ hợp rồi nhả phím đánh. Dùng cách giữ U/I của BotMatch; tối đa hai phím, tổ hợp sai không đánh. Forehand/backhand theo vị trí bóng. Đánh hụt không tự trả bóng.

**Thắng trận khi thắng 2/3 game**. Mỗi game 11 điểm, cách biệt 2, chỉ đội giao ghi điểm. Đổi sân giữa các game; game thứ 3 đổi sân một lần khi đội đầu tiên đạt 6 điểm. Luật giao/nhận, hai lần nảy, kitchen/đà volley và người giao thứ hai áp dụng theo số người thực tế; đội chỉ một người không gọi người thứ hai.

## Kiến trúc để rà soát

Đợt dọn script ngày 09/10/2026: [danh sách đã xóa, lý do và kết quả kiểm tra](MultiplayerCleanup.md). Scene MultiplayerTest cũ đã được gỡ; dùng PlayMultiplayer để test phòng.

| Lớp | File / trách nhiệm |
|---|---|
| View | `UI/PlayMultiplayerPresenter.cs`: binding Canvas và button, render trạng thái |
| Chính sách UI | `UI/RoomViewModel.cs`: điều kiện host bắt đầu, nhãn BtnPlay |
| Hợp đồng | `Services/MultiplayerInterfaces.cs`: identity/state/room/matchmaking/backend |
| Identity | `Services/PlayerIdentityService.cs`: profile, đăng nhập, tên ngẫu nhiên |
| Room | `Services/RoomService.cs`: mở/đổi phòng, nhập mã, đổi đội, kick, start |
| Matchmaking | `Services/MatchmakingService.cs`: find/cancel; cancel không bị Busy chặn |
| SDK adapter | `MultiplayerApp.Room.cs`, `.Matchmaking.cs`: room/ticket; `MultiplayerApp.cs`: network và vòng đời |
| Gameplay | `BotMatchGameplayService.cs`, `NetworkPlayer.cs`, `NetworkMatch.cs`, `PickleballRules.cs` |
| Build | `Services/MultiplayerSceneConfiguration.cs`, `Assets/Editor/MultiplayerBuildValidation.cs` |

## Kết nối và lỗi Scene Hash

Sau khi pull trên máy mới, Unity có thể dùng **Global Scene List** thay vì profile riêng đang được chọn trên máy phát triển. Đã sửa ngày 09/10/2026: danh sách chung có cả `PlayMultiplayer` và `MultiplayerCourt`; `MultiplayerSceneSetup` tự đăng ký sau khi import và kiểm tra profile đang dùng trước Play. Các scene khác giữ nguyên thứ tự và trạng thái bật/tắt. [Unity: Global Scene List](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/editorbuildsettings/globalscenes).

Nếu gặp “Scene chưa được bật trong Build Profile” khi test Editor: dừng Play → **PickleVerse → Multiplayer → Configure scenes for testing** → mở `Assets/Scenes/UI/PlayMultiplayer.unity` → Play lại. Không cần build Windows chỉ để test bằng Editor. Máy khác cần pull cả bản sửa script Editor và `ProjectSettings/EditorBuildSettings.asset`, các scene cùng file `.meta`. Nếu chưa có menu này, vào **File → Build Profiles → Scene List** và thêm/bật hai scene multiplayer trong danh sách đang dùng.

Find, Join và StartCustom kiểm tra scene trước khi đổi trạng thái hoặc gửi yêu cầu mạng, tránh dừng tìm bất ngờ sau 6 giây. Kết quả thử cấu hình máy mới, scene bị tắt, profile riêng và runtime nằm tại `Docs/MultiplayerSceneSetupVerification-20261009.json`.

Profile `Assets/Settings/BuildProfiles/MultiplayerWindows.asset` phải chứa cả `PlayMultiplayer` và `MultiplayerCourt`, cùng những scene đang được NGO đồng bộ. Editor và mọi Player cần cùng danh sách scene/bản build. Scene Hash lỗi khi client không có scene host gửi trong bảng build của nó. Đã thêm kiểm tra trước kết nối và trước build; protocol phiên bản 2 ngăn bản cũ tham gia.

Services dùng môi trường `pickleverse-test`, hai queue `PickleVerse-1v1` / `PickleVerse-2v2`, Relay nên có thể kết nối khác mạng khi Internet/UGS hoạt động. QoS giới hạn 150 ms, không tự nới. Test nhiều phiên trên một máy chưa chứng minh kết nối giữa hai máy vật lý.

Host rời kết thúc phiên. Khách mất kết nối: tạm dừng tối đa 30 giây, kết nối lại cùng danh tính; quá hạn đội đó thua. Chủ động rời cũng bị xử thua. Chưa có host migration, xếp hạng hoặc lịch sử lâu dài.

## Kiểm chứng

- Kiểm tra mới nhất 09/10/2026: [báo cáo đầy đủ, lỗi đã sửa và giới hạn kiểm thử](MultiplayerVerification-20261009.md). 14/14 kịch bản Windows, 61/61 EditMode; có kiểm tra input host/client, timeout và khách hủy trước khi host nhận sự kiện.
- EditMode: `Docs/PlayMultiplayerTestResults.json`.
- Binding UI: `Docs/PlayMultiplayerUiTest.json`.
- Windows/Relay: `Docs/PlayMultiplayerWindowsTestResults.md`.
- Công cụ opt-in: `Tools/RunMultiplayerSmoke.ps1`, `Tools/StopMultiplayerSmoke.ps1`. Runner chỉ chạy khi có `-pkvCase`.
- `Assets/Editor/PlayMultiplayerIntegration.cs` có menu Integrate PlayMultiplayer and BotMatch để tái tạo binding/prefab/sân sau thay đổi BotMatch. Bản scene ban đầu sao lưu trong Temp/MultiplayerUpgrade; đừng dùng file này để thay đổi thiết kế sau khi đã tinh chỉnh mà chưa xem lại tác động.
