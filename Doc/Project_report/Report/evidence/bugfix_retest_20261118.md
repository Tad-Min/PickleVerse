# EV-17 – Defect fixes and retest (18/11/2026, afternoon)

## Scope

The user asked for every open defect of Report 5 v1.6 to be fixed, workflow-blocking defects first, and for the
end-to-end workflow to be run again. The fixes were made in the report worktree on top of main @ 96ff8e9.

## Code changes

| Defect | Change | Where |
|---|---|---|
| D-27 | The report overlay converts the match id to the hyphenated Guid form before calling `api/Matches/report-targets` and `api/Matches/report` | `FrontEnd/ModerationOverlay.cs` |
| D-14 | When the dedicated server leaves after a finished match the client returns with "Trận đã kết thúc." instead of "Mất kết nối Photon." | `Multiplayer/MultiplayerApp.cs` |
| D-02 | Offline result screens show "HLV • Đăng nhập để xem phân tích" and an offline explanation instead of a permanent "Đang phân tích…" | `LocalMatch/CoachingSessionView.cs` |
| D-22 | Windows players open the native "Open" dialog (comdlg32, own STA thread) to pick an MP4; Android still shows that video upload is not supported there | `FrontEnd/WindowsFilePicker.cs`, `FrontEnd/SocialLobbyMenu.Feed.cs` |
| D-24 | Duplicate `Unity.TextMeshPro` reference removed | `src/Testing/Unity/EditMode/FrontEnd/PickleVerse.FrontEnd.Tests.asmdef` |
| D-16 | UTF-8 byte-order mark added; Windows PowerShell 5.1 parses and runs the script | `src/Testing/Sync-UnityTests.ps1` |
| D-08 | The ranking reset sets Elo to `EloPolicy.Minimum` (100), the start value of BR-23; admin web texts and the integration check follow | `ProgressResetService.cs`, `AdminController.cs`, `AdminService.cs`, `IAdminService.cs`, admin web `adminApi.ts`, `mockAdminApi.ts`, `ProgressResetDialog.tsx`, `Users.tsx`, `ProgressResetChecks.cs` |
| D-25 | The header shows the API origin actually used by the build | admin web `api/client.ts`, `AdminLayout.tsx` |
| D-26 | MSG64 is shown in red under the expert review form while the review is incomplete | admin web `ExpertReview.tsx` |
| D-11 | The summary says the figures are for this session; each recommendation names the technique and says that its figures cover the recent sessions | `src/AiService/app/engine.py` |
| D-23 | The deploy job runs when `deployed` and `publish` succeeded, even if an API, AI or web check was skipped | `.github/workflows/dockerhub.yml` |
| D-28, D-30 | Requirements aligned with the implemented rules: a forfeit is a loss (BR-28, FR-17); custom room matches give no XP or Tokenpad (BR-32) | Report 3 generators |

## Automated checks

| Check | Result |
|---|---|
| `dotnet build src/Backend/PickleVerse.slnx -c Release` and IntegrationChecks build | 0 warnings, 0 errors |
| IntegrationChecks `--check-api-contract` | 37 checks passed (no database) |
| IntegrationChecks `--check-client-security` | 19 checks passed |
| Admin web `tsc --noEmit` | no errors |
| AI service `pytest src/Testing/AiService` | 24 passed |
| Database checks (progress reset, moderation, …) | not run locally (no Docker); they run in the CI validate job |

## Environment incident

The first Windows build at 14:42 ran together with `npm ci` and `dotnet build`; Windows reported a low virtual memory
condition caused by Unity.exe, several programs crashed and afterwards every PickleVerse player (also the build of the
morning) stopped at start-up after "Using Windows.Gaming.Input". The second build alone succeeded. After a restart of
Windows the players started normally.

## D-29 diagnosis (build `PickleVerse-fix1` with a temporary input probe, 15:20–15:35)

The probe (`-pkvProbe`, local test builds only, not part of the code changes) logged the EventSystem, the raycast hits
and the multiplayer flow flags on every click. A = huy created a 2v2 room (code 0363A73B), B = Jahwi and C = Ghost
joined, C changed team, the 1v2 match was played to 0 : 2 (0–11, 3–11) on the dedicated server and all three opened the
summary (`summary.visible=True`, the buttons interactable, `busy=False`).

When A pressed VỀ MENU, the host closed the party. On both guests the client logged
`flow state=WaitingRoom notice="Chủ phòng đã đóng nhóm." room=False matchSession=False`: `ClosePartyAsync` had left
the match, Fusion had unloaded the court and only `FusionSceneManager_TempEmptyScene` was loaded, with
`es.current=null es.all=[]`. No camera renders that scene, so the last frame of the summary stayed on screen; with no
EventSystem no button, Enter or Esc could work, while the window frame (which polls the mouse) and the quit prompt
(which creates its own EventSystem) still reacted. This is exactly the EV-16 observation.

Fix (`MultiplayerApp.ClosePartyAsync`): a guest who is on the summary of a finished match only leaves the closed party
and keeps the summary until they choose; in every other case where a match was left, the room scene is loaded again
(`ShowRoomSceneAsync`, also used by `ReturnToRoomAsync`).

## Results

| Case | Steps | Result |
|---|---|---|
| TC-F17-01 (D-27) | A: Lịch sử → the room match of 08:41 → "!" next to Jahwi → statement → Gửi tố cáo (15:33) | Report targets loaded ("[Moderation] Loaded report targets"), the report was accepted: "Đã gửi tố cáo. Ban quản trị sẽ xem xét." → **Passed**; D-27 fixed |
| TC-F19-05 (D-22) | A: Bảng tin → Viết bài → audience "Chỉ mình tôi" → Chọn MP4 → the Windows "Chọn video MP4" dialog (filter Video MP4) → a 0.2 MB sample clip of the project → Đăng bài | Upload finished ("pkv-clip.mp4"), the post "Chỉ mình tôi · 18/11 15:37" shows "Xem video" and the clip plays; the post was deleted afterwards → **Passed** on Windows; D-22 fixed for Windows (Android still unsupported) |
| D-29 | see the diagnosis above | Reproduced with the probe; fix built as `PickleVerse-fix2` |

## Retest on `PickleVerse-fix2` (18/11/2026, 19:40–20:15)

The three player windows of 15:41 had been closed during a pause, so the user signed in again (A = huy, B = Jahwi,
C = Ghost) and to the admin web.

| Case | Steps | Result |
|---|---|---|
| TC-F17-01 (admin part) | Admin web → Xử lý vi phạm → the report "huy → Jahwi, c0ad0268-bff7-…, kiem tra D27 …" (Chờ xử lý) → Xem xét → Bác tố cáo with a reason → Lưu quyết định | "Đã ghi kết luận và lịch sử xử lý."; no sanction (test data closed) |
| TC-F08-04 (D-29) | A: Tạo phòng → 2v2 (code 07D62BC5); B and C join; C ĐỔI ĐỘI; A starts; all accept; played to 2 : 0 for A; all open the summary; A presses VỀ MENU first | Both guests stay on the summary: probe `flow state=Results notice="Chủ phòng đã đóng nhóm." room=False matchSession=True`. B then pressed VỀ PHÒNG CHỜ → doubles lobby with "Trận đã kết thúc."; C pressed VỀ MENU → main menu → **Passed**; D-29 fixed |
| TC-F09-01 / D-14 | B and C: Đấu đơn 1v1 → Tìm trận at 19:49:01; matched and accepted at 19:49:36; played on the dedicated server to 2 : 1 for Ghost; both stayed on the summary for 3 minutes, then B pressed VỀ PHÒNG CHỜ and C VỀ MENU | Ghost +15 Elo (116 → 131, expected round(32 × (1 − 0.523)) = 15), XP 20/30; Jahwi 100 → 100 (floor), +5 XP. The lobby showed "Trận đã kết thúc.", not "Mất kết nối Photon." The dedicated server did not leave during the 3 minutes, so the planned-stop branch of the D-14 fix was checked by code review only |
| TC-F05-01 (D-03, D-05, D-07, D-10) | A: CHƠI NGAY → Đấu với bot → Vừa → VÀO SÂN; played with J until the end (0 : 2) | The fourth level reads "Tự động" (D-05 fixed); huy's female choice is used on court (D-07 fixed for the bot match); the key-hint box ("Đánh bạt · J") stays below the player (D-03 fixed for the bot match). **D-10 still present**: "Xem phân tích" was a large block over the court and the outcome title. **D-06 still present**: the setup card says "Không cần kết nối mạng" (the text is in the scene, not in code). New: the referee banner showed the technical code "PermanentObject" (D-31) |
| TC-F14-01 / D-01, D-02 | C: Cài đặt → Đăng xuất → Luyện tập ngoại tuyến (A and B closed first, because three players on one PC share the offline store) → Tiến trình → Tiếp tục luyện → "Đánh bạt về cuối sân" → J until the end | Menu banner below the profile and currency plates; Progress title not covered; in the lesson the banner docks at the bottom centre (D-01 fixed by 404b2cd). Result "Chưa đạt mục tiêu" shows "HLV • Đăng nhập để xem phân tích" and the details explain that the analysis needs a connection (D-02 fixed). The failed attempt (no reward) waits to sync into huy's account |

Fixes added after this retest: D-10 (the analysis button becomes a third button on the CHƠI LẠI / CHỌN CHẾ ĐỘ row,
`MatchHud.cs`), D-06 (setup card text "Cần đăng nhập", `ModeSelection.unity`) and D-31 (Vietnamese names for
`PermanentObject` and `DoubleHit`, `BotMatch.FaultText`). Final build without the probe: `PickleVerse-fix3`.

## Push, pipeline and staging (20:14–20:50)

With the user's approval the code was committed under the user's identity as 291542a (CI and test tooling), 0cee928
(admin web and backend), 95ff4a4 (AI service) and 9557581 (game) and pushed to main one commit at a time. GitHub
Actions run 44311727336 (291542a) passed every job and deployed; the runs of 0cee928 and 95ff4a4 were superseded by the
workflow's concurrency group; run 44311790341 (9557581) passed the backend integration checks with PostgreSQL and Redis
(including the new progress-reset expectation), the FastAPI tests, the admin web type check and the Linux dedicated
server build, and deployed the release. Because every check ran in both runs, the skipped-check case of D-23 was not
exercised; the new condition was checked by review.

## Final retest on staging and on `PickleVerse-fix3` (20:50–21:35)

Accounts this time: A = Ghost, B = Jahwi, C = huy (signed in by the user); admin web in the built-in browser.

| Case | Steps | Result |
|---|---|---|
| TC-F15-01 (D-25) | Admin web header | "Server API: https://tad-min.io.vn" → D-25 fixed |
| TC-X01-02 (D-26) | Gán nhãn coaching → session "Jahwi · Giao bóng đúng ô" → no label, no confirmation | MSG64 "Hãy xác nhận phiên người thật, người đánh giá, bằng chứng và ít nhất một nhãn." in red (rgb 239, 68, 68) under the form, save disabled → **Passed**; D-26 fixed |
| TC-F10-01 / D-08 | Quản lý người dùng → Ghost → Đặt lại tiến trình → Xếp hạng → Xem trước tác động (not executed) | Dialog "Xếp hạng: Elo về 100 (mức khởi đầu) …"; server preview "Elo về 100 (mức khởi đầu của người chơi mới), chuỗi xếp hạng về 0 …"; cancelled, Ghost unchanged (131) → D-08 fixed |
| TC-F09-03 / TC-S07-02 (BR-28 v1.5) | B (Jahwi, 100) and C (huy, 126): ranked 1v1, matched at 20:54:18; C suspended for 65 s at 20:55:22 | C returned to the lobby with MSG43 "Không thể kết nối lại trận. Kết quả sẽ được cập nhật khi máy chủ xử lý xong."; C's history "Thua · Bỏ trận · 0 : 0 ván · −17 Elo" (126 → 109, expected round(32 × (0 − 0.537)) = −17); Tokenpad stayed 1,050 and XP stayed 22/50 (no reward for the forfeit); B "+17 ELO 100 → 117", +10 XP → **Passed** under BR-28 v1.5 |
| TC-W04 steps 1–4, TC-S11-01, TC-S11-02 | A (Ghost): Lịch sử → ranked match of 19:53 → "!" next to Jahwi → Gửi tố cáo; admin: Xem xét → Đang xem xét (saved), then Chấp nhận tố cáo + Áp dụng hạn chế tài khoản → Tạm ngưng until 21:29; B (Jahwi) signed in again → Quyết định và khiếu nại → Gửi khiếu nại; admin: Xem xét khiếu nại → Chấp nhận, thu hồi quyết định liên quan | Report "Ghost → Jahwi, b383a1c4-…" PENDING → CHECKING → ACCEPTED; Jahwi was signed out at once and after signing in saw "Tạm ngưng · Đang hiệu lực" with the reason and dates; the appeal was stored as "Khiếu nại: Đang chờ"; after the admin accepted it the sanction shows "Đã thu hồi" and Quản lý người dùng shows Jahwi ACTIVE → **Passed** (steps 5–6 passed in EV-15). Observation: after Làm mới the client returned to the login screen with the old text "Tài khoản đang bị hạn chế. Bạn có thể xem quyết định và gửi khiếu nại." (D-32, Low) |
| TC-F05-01 (D-06, D-10, D-11, D-31) | A (Ghost): Đấu với bot → Vừa → VÀO SÂN → J until 0 : 2 → Xem phân tích | Setup card "11 điểm, cách 2 · Cần đăng nhập" (D-06 fixed). The analysis button now sits right of CHỌN CHẾ ĐỘ and no longer covers the court, but was still about 100 px high because the scene's action bar (620 × 240) is larger than the editor script's; it now takes the height of the CHỌN CHẾ ĐỘ button (final build `PickleVerse-fix4`). The coaching text says "Cần cải thiện trong phiên này: Đánh hụt 8/32 lần quan sát." and the drill "Đánh hụt khi đánh bạt: 101/127 lần quan sát trong các phiên gần đây" (D-11 fixed). No referee banner with PermanentObject or DoubleHit occurred; D-31 is checked by review |

## Test data left on staging

- New matches in the histories of huy, Jahwi and Ghost (two 2v2 rooms, two ranked 1v1, two bot matches, one forfeit).
  Elo now: huy 109, Jahwi 117, Ghost 131.
- Two reports: huy → Jahwi (declined) and Ghost → Jahwi (accepted with a 30-minute suspension that was revoked after the
  accepted appeal). The decision history keeps both decisions.
- The test post with the sample video was deleted. One failed offline attempt of huy (no reward) was synchronised.
