"""Test cases, results, defects and unit-test decision tables for Report 5.

Results are recorded only from real runs:
  EV-01  Unity EditMode run, 11/11/2026, main @ 7f8ee32, 515 tests, 515 passed (Unity CLI, Pipeline test run)
  EV-02  Playtest on the staging server, 10/11/2026 (offline training, signed-in menu, bot match, AI coaching, ranked 1v1 with bot fallback)
  EV-03  UI check of the account screens and the tutorial, 11/11/2026
  EV-04  Match history of the team test accounts: two ranked 1v1 matches on 09/11/2026 21:47 and 21:55
  EV-06  Unity EditMode run, 11/11/2026, main @ 5963501, 516 tests, 516 passed
  EV-07  Unity Play Mode check of the fixed defects D-03, D-04, D-05, D-07 and D-19, 11/11/2026, main @ 5963501
  EV-08  GitHub Actions run 30 (id 43385071385), 13/11/2026 14:20-14:34 (UTC+7), main @ d9200cc: Backend integration checks (12 IntegrationChecks
         groups on PostgreSQL 16 and Redis 7.4), FastAPI tests, admin web type check, dedicated-server build, image publish with smoke test
         and deployment all succeeded; runs 27 and 28 on main @ 1dfefe9 succeeded too. Step results read from the GitHub REST API
         (evidence/ci_runs_20261113.json).
  EV-09  Transport and secret check, 13/11/2026. 11:30: HTTPS on port 443 but port 8080 still plain HTTP and credentials in appsettings.json.
         16:21, after commit be556a1 was deployed: API and admin web only over HTTPS, port 8080 closed, settings files without credentials;
         the removed values are still in the Git history (evidence/staging_check_20261113.md)
  EV-10  Android build of main @ 5963501, 12/11/2026: APK 190.5 MB, IL2CPP ARM64, API 26-36, manifest read with aapt2; not installed on a
         device (evidence/android_build_20261112.md)
  EV-11  GitHub Actions run 52 (id 44028526455), 17/11/2026 13:04-13:20 (UTC+7), main @ 96ff8e9: 15 IntegrationChecks groups (adds
         room-invitations, ranked-doubles and social-content), FastAPI tests, admin web type check, dedicated-server build, publish and
         deployment all succeeded (evidence/ci_runs_20261117.json)
  EV-12  Unity EditMode run, 16/11/2026, on the source committed as bc53722..1b64424, 522 tests, 522 passed
         (evidence/editmode_results_20261116_1b64424.json)
  EV-13  Windows player of 16/11/2026 (source of 1b64424): display modes, window frame and quit checked with Win32 calls
         (evidence/windows_window_test_20261116.md)
  EV-14  Review of main @ 96ff8e9, CI history and staging, 17/11/2026 (evidence/review_20261117.md)
  EV-15  End-to-end run on the Windows build of main @ 96ff8e9 (three player windows on one PC) and the admin web on staging,
         17/11/2026 20:35-21:45 and 18/11/2026 07:32-07:55 (evidence/e2e_run_20261117.md)
  EV-16  Rebuild of the Windows player from main @ 96ff8e9 and retest of the b08f5c7 changes (2v2 room, ranked doubles with
         bot fill, replay), 18/11/2026 08:24-09:02 (evidence/e2e_retest_20261118.md)
  EV-17  Defect fixes 291542a, 0cee928, 95ff4a4 and 9557581 and their retest on Windows builds with the fixes (diagnosis of
         D-29 with a temporary input probe), 18/11/2026 15:20-21:30 (evidence/bugfix_retest_20261118.md)
  EV-18  Unity EditMode run, 25/11/2026, main @ adcb4e2 with the fixes of D-33 and D-34, 730 tests, 730 passed
         (evidence/editmode_results_20261125_adcb4e2.json)
  EV-19  GitHub Actions run 103 (id 45550426329), 25/11/2026 12:12-12:24 (UTC+7), main @ adcb4e2: 16 IntegrationChecks groups (adds
         single-session), dedicated-server build, publish and deployment succeeded; history of the 51 runs since 18/11/2026
         (evidence/ci_runs_20261125.json)
  EV-20  End-to-end run on the Windows build of main @ adcb4e2 with two member accounts, public probes of staging and a read-only pass
         through the admin web, 25/11/2026 13:35-14:20 (evidence/e2e_run_20261125.md)
  EV-21  Court probe of the surrender, 25/11/2026 16:50, Windows player of main @ adcb4e2 with the fixes of D-33 and D-34: the real
         court scene, HUD and NetworkMatch on one Fusion runner without server and account, messages of the other seats injected
         (evidence/surrender_probe_20261125.md)
Every other case is Pending, with the reason in the Note column.
"""

HUY, DAT = "Nguyễn Gia Huy", "Nguyễn Văn Thành Đạt"
D10, D11, D09, D12, D13 = "10/11/2026", "11/11/2026", "09/11/2026", "12/11/2026", "13/11/2026"
D16, D17, D18 = "16/11/2026", "17/11/2026", "18/11/2026"
D25 = "25/11/2026"

SHEETS = [
    ("FE01 Account", "FE-01 Account and onboarding", "FR-01 to FR-05: registration, verification, sign-in, profile, settings"),
    ("FE02 Training", "FE-02 Tutorial, training and offline", "FR-06 to FR-11: tutorial, controls, lessons, drills, offline synchronisation"),
    ("FE03 Bot", "FE-03 Bot practice", "FR-12, FR-13: rules engine and bot match"),
    ("FE04 Online", "FE-04 Online matches", "FR-14 to FR-18: ranked matchmaking, rooms, dedicated play, result and Elo"),
    ("FE05 Coaching", "FE-05 AI coaching", "FR-19: coaching summary and recommendations"),
    ("FE06 Progress", "FE-06 Progress, rewards and shop", "FR-20 to FR-23: XP, missions, streaks, shop and collection"),
    ("FE07 Social", "FE-07 History, social and ranking", "FR-10, FR-24 to FR-26, FR-34, FR-35: progress, history, match detail, replay, friends, leaderboard, feed and messages"),
    ("FE11 Membership", "FE-11 Membership", "FR-36: offer, payment, activation and locks"),
    ("FE08-09 Admin", "FE-08/FE-09 Moderation and administration", "FR-27 to FR-32: reports, appeals, users, content, matches, analytics"),
    ("FE10 Expert", "FE-10 Expert review", "FR-33: expert labelling and dataset export"),
    ("Workflows", "End-to-end workflows W01–W04", "Acceptance scenarios of the Review 2 commitment"),
    ("States", "State transitions SM-01 to SM-14", "State machines of Report 4, §4.2"),
    ("NFR", "Non-functional requirements", "NFR-01 to NFR-12 of Report 3, §4.2"),
]

CASES = []


def C(cid, sheet, fr, level, kind, desc, steps, expected, pre, auto="Manual", status="Pending", date="", tester="", note=""):
    CASES.append(dict(id=cid, sheet=sheet, fr=fr, level=level, kind=kind, desc=desc, steps=steps, expected=expected, pre=pre,
                      auto=auto, status=status, date=date, tester=tester, note=note))


EM = "Unity EditMode – "
PASS_UT = dict(status="Passed", date=D11, tester=HUY)

# ------------------------------------------------------------------ FE-01
C("TC-F01-01", "FE01 Account", "FR-01, FR-02", "System", "Main", "Register a new account and activate it with the e-mail code",
  "1. Open Login and select Đăng ký.\n2. Enter e-mail qa.pkv01@<test mailbox>, password Test@1234 and the same confirmation.\n3. Select Tạo tài khoản.\n4. Open the mailbox and copy the six-digit code.\n5. Enter the code and select Xác thực.\n6. Sign in with the new account.",
  "Step 3 shows MSG07 and the verification page; the e-mail arrives within 1 minute with a six-digit code; step 5 returns to sign-in; step 6 opens the experience question MSG21.",
  "Staging API reachable; an unused test mailbox", note="Not run: no dedicated test mailbox on staging yet. Planned in It-5 (Thuận).")
C("TC-F01-02", "FE01 Account", "FR-02", "System", "Main", "Sign in with a verified account and load the profile",
  "1. Open Login.\n2. Enter the e-mail and password of a verified test account.\n3. Select Đăng nhập.",
  "Loading screen, then the Menu in online mode with name tag (level, rank, Elo) and Tokenpad / Elite Token balances.",
  "Verified team test account", status="Passed", date=D10, tester=HUY, note="EV-02: Menu online with \"Huy – CẤP 1 – Đồng 100\" (screenshot 11).")
C("TC-F01-03", "FE01 Account", "FR-01", "System", "Exception", "Register form rejects an empty e-mail without calling the API",
  "1. Open Login → Đăng ký.\n2. Leave all fields empty.\n3. Select Tạo tài khoản.",
  "No account is created; the inline message MSG02 \"Vui lòng nhập địa chỉ email hợp lệ.\" is shown under the form.",
  "Game client on the Login scene", status="Passed", date=D11, tester=HUY, note="EV-03, screenshot 32.")
C("TC-F01-04", "FE01 Account", "FR-03", "System", "Main", "Reset a forgotten password with the e-mail code",
  "1. Login → Quên mật khẩu.\n2. Enter the e-mail of a verified test account → Gửi mã.\n3. Enter the code, new password Test@5678 and its confirmation.\n4. Sign in with the old password, then with the new one.",
  "Step 2 shows MSG15; the old password is rejected with MSG12; the new password signs in; other sessions of the account are signed out.",
  "Verified test account with a reachable mailbox", note="Not run: needs the test mailbox (backlog PB-04). The Forgot password screen itself was checked in EV-03 (screenshot 31).")
C("TC-F01-05", "FE01 Account", "FR-04", "System", "Rule", "Display name validation and uniqueness",
  "1. Open the profile card and edit the name.\n2. Enter \"ab\" and confirm.\n3. Enter the name of another existing player and confirm.\n4. Enter \"Huy Test 01\" and confirm.",
  "Step 2 shows MSG19, step 3 shows MSG20, step 4 saves the name and the menu name tag shows it.", "Two existing test accounts",
  note="Not run: would change a live team account on staging; to be run with seed accounts (PB-04).")
C("TC-F01-06", "FE01 Account", "FR-05", "Unit", "Rule", "Touch layout settings are migrated once and keep custom positions",
  "Run the touch settings migration tests.", "Old default layouts migrate once to the four-button fan; custom offsets are kept and not applied twice; the run button is independent.", "–",
  auto=EM + "TouchSettingsMigrationTests (3 tests)", note="EV-01", **PASS_UT)
C("TC-F01-07", "FE01 Account", "FR-05", "System", "Main", "Choose the display mode on PC; drag, minimise and quit with the window frame",
  "1. Start the Windows build with -screen-fullscreen 0 -screen-width 1280 -screen-height 720.\n2. Drag the title bar by (+150, +90); select –; restore; select × and press Esc; select × and Thoát game.\n3. Start borderless; Menu → Cài đặt → Khung hình: select Cửa sổ, Toàn màn hình, Không viền, Cửa sổ + 1280 × 720, then Hoàn tác.",
  "Step 1: 1280 × 720 without the Windows caption. Step 2: the window moves by (150, 90), minimises, keeps the frame after restore, the quit dialog closes with Esc and Thoát game ends the process. Step 3: 1600 × 900 centred, 1920 × 1080 exclusive, 1920 × 1080 borderless, 1280 × 720 centred, back to borderless.",
  "Windows 11 PC, Windows build of 16/11/2026; PlayerPrefs exported before the run", status="Passed", date=D16, tester=HUY,
  note="EV-13: all steps as expected; switch times 17–58 ms (windowed, borderless) and 380 ms (exclusive fullscreen). Not checked: Windows 10, a second monitor.")
C("TC-F01-08", "FE01 Account", "FR-05", "Unit", "Rule", "Display settings defaults, validation, window sizes and title-bar safe area",
  "Run the display settings tests.", "Default borderless; old preferences stay borderless; unknown modes and impossible sizes are repaired; 1080p offers 1280 × 720, 1366 × 768 and 1600 × 900; the title bar is removed only from the top of the safe area.", "–",
  auto=EM + "DisplaySettingsTests (7 tests)", status="Passed", date=D16, tester=HUY, note="EV-12")
# ------------------------------------------------------------------ FE-02
C("TC-F02-01", "FE02 Training", "FR-06", "System", "Main", "Tutorial starts at step 1 of 17 and advances step by step",
  "1. Mode Selection → Tutorial / Hướng dẫn cơ bản.\n2. Read step 1 and select Tiếp tục three times.",
  "Header shows CƠ BẢN / 01 - 17 and then 04 - 17; every step shows a title, an explanation and a demonstration on the court.",
  "Signed-in or offline profile", status="Passed", date=D11, tester=HUY, note="EV-03, screenshots 33–34.")
C("TC-F02-02", "FE02 Training", "FR-06", "System", "Rule", "Completed tutorial is saved to the account",
  "1. Finish all 17 steps.\n2. Sign out and sign in again.\n3. Open the Menu.",
  "The tutorial is not forced again and is shown as completed.", "New account that chose Người mới",
  note="Not run: needs a new account (TC-F01-01).")
C("TC-F02-03", "FE02 Training", "FR-11", "Unit", "Rule", "Shot keys decode into the correct shot kind",
  "Run the EditMode tests with key masks J, K, L, J+K, J+L, K+L, three keys, and Backspace or lost focus during a hold.",
  "J→Drive, K→Dink, L→Volley, J+K→Drop, J+L→Smash, K+L→Reset; three keys cancel; Backspace or focus loss never fires a shot.",
  "–", auto=EM + "SinglesMatchTests.BaseAndComboCommitOnRelease (6 cases), ThreeKeysCancelWithoutAnySwing, BackspaceOrLostFocusCannotFireOnRelease", note="EV-01", **PASS_UT)
C("TC-F03-01", "FE02 Training", "FR-07, FR-08", "System", "Main", "Practise the serve drill and read the result",
  "1. Mode Selection → Luyện tập → Giao bóng đúng ô → Bắt đầu.\n2. Hit the eight serves.\n3. Read the result popup.",
  "Result shows hits/8 and percentage, mean timing, practice time, the most frequent error and a tip; fewer than 6 hits gives 0 stars with MSG24 and MSG25.",
  "Profile with the serve lesson unlocked", status="Passed", date=D10, tester=HUY, note="EV-02: 4/8 (50%) → 0 stars, CHƯA ĐẠT MỤC TIÊU (screenshot 07).")
C("TC-F03-02", "FE02 Training", "FR-08", "Unit", "Rule", "Star boundaries of a drill",
  "Run TrainingEngine.Complete with 6/8, 7/8 and 8/8 hits, with 8/8 and mean timing 0.30 s, and with a failed attempt.",
  "6/8 → 1 star, 7/8 → 2 stars, 8/8 with fast timing → 3 stars, 8/8 at 0.30 s → fewer than 3 stars, failed → 0 stars and no currency.",
  "–", auto=EM + "TrainingTests.StarsUseAccuracyAndTiming, SlowTimingCannotReceiveThreeStars, FailedDrillRecordsErrorsAndReceivesNoCurrency", note="EV-01", **PASS_UT)
C("TC-F03-03", "FE02 Training", "FR-07", "Unit", "Exception", "Locked lessons cannot be started before their prerequisite",
  "Run the curriculum test that requests advanced lessons for a new player.",
  "Basic lessons are available; advanced lessons require their prerequisite.",
  "–", auto=EM + "TrainingTests.BasicLessonsAreAvailableAndAdvancedLessonsNeedPrerequisites", note="EV-01. The server message MSG23 is covered by IntegrationChecks, not run in this round.", **PASS_UT)
C("TC-F14-01", "FE02 Training", "FR-09", "System", "Main", "Train offline from a cached profile",
  "1. Disconnect the PC from the network.\n2. Open Login and select Luyện tập ngoại tuyến.\n3. Play the serve drill to the end.",
  "Menu opens with the offline banner; the drill can be played; the banner shows 1 item waiting for synchronisation; online-only buttons are disabled.",
  "Profile cached by an earlier online sign-in", status="Passed", date=D10, tester=HUY, note="EV-02, screenshots 02 and 07. Defect D-01 (banner overlaps titles) found during this case.")
C("TC-F14-02", "FE02 Training", "FR-09", "System", "Rule", "Offline attempts are synchronised exactly once after reconnecting",
  "1. Finish two drills offline.\n2. Reconnect and sign in.\n3. Open Progress and the API training history.",
  "Both attempts are applied once (receipts APPLIED), stars and rewards appear once; the banner clears.", "TC-F14-01 done",
  note="Not run: synchronisation after reconnect was not observed in the 10/11 session.")
C("TC-F14-03", "FE02 Training", "FR-09", "Unit", "Exception", "Offline store applies receipts once and rejects foreign or stale data",
  "Run the offline store tests for accepted/duplicate receipts, a new reset epoch and an owner mismatch.",
  "Outbox entries are removed once; a reset epoch does not overlay old progress; another owner's data is rejected.",
  "–", auto=EM + "OfflineTrainingStoreTests.AcceptedOrDuplicateReceiptsRemoveTheOutboxOnce, ANewResetEpochDoesNotOverlayOldProgress, OwnerMismatchCannotRemoveQueuedProgress", note="EV-01", **PASS_UT)
# ------------------------------------------------------------------ FE-03
C("TC-F05-01", "FE03 Bot", "FR-12, FR-13", "System", "Main", "Play a bot match to the end",
  "1. Mode Selection → Đấu với bot.\n2. Choose Khó, Ban đêm, Ngoài trời and select Vào sân.\n3. Play until the match ends.",
  "Best-of-three result is shown; a win gives +100 Tokenpad and +10 XP; Elo does not change for a practice bot match.",
  "Signed-in player", status="Passed", date=D10, tester=HUY, note="EV-02: won 2–0; server recorded BotPractice, +100 Tokenpad, +10 XP, Elo unchanged. Rallies were played by the runtime auto-player, see D-13.")
C("TC-F05-02", "FE03 Bot", "FR-13", "Unit", "Rule", "Adaptive bot tier follows the Elo boundaries",
  "Run the behaviour-tree bot tests at the Elo boundaries 499/500 and 1999/2000.",
  "Tier changes exactly at 500 and 2000; Elo stays fixed during simulation.", "–",
  auto=EM + "BhTreeTests.EloBoundaries, EloRemainsFixedAndSimulationInputIsExposed", note="EV-01", **PASS_UT)
C("TC-F05-03", "FE03 Bot", "FR-12, FR-13", "Unit", "Rule", "Bot obeys the two-bounce and non-volley-zone rules",
  "Run the bot graph tests with a ball before the mandatory bounce and with the bot in the kitchen.",
  "The bot never volleys before the two bounces or from the kitchen.", "–",
  auto=EM + "BhTreeTests.MandatoryTwoBounceAndKitchenRejectAirShots; AdaptiveBotGraphTests.MandatoryBounceAndKitchenExcludeVolleyBranches", note="EV-01", **PASS_UT)
# ------------------------------------------------------------------ FE-05
C("TC-F06-01", "FE05 Coaching", "FR-19", "System", "Main", "Coaching summary after a bot match",
  "1. Finish TC-F05-01.\n2. Select Xem phân tích.",
  "The popup shows counts of shots, contacts and serves, the main errors with frequency and at most three recommended drills with Luyện ngay.",
  "Hybrid coaching enabled on the server", status="Passed", date=D10, tester=HUY, note="EV-02: 86 shots, 71 contacts, 28 serves; recommendation \"Đánh bạt về cuối sân\" (screenshot 21). D-11 found.")
C("TC-F06-02", "FE05 Coaching", "FR-19", "Unit", "Rule", "Quick coaching rules use only valid evidence",
  "Run the quick coaching rule tests.",
  "Standing in the kitchen is a fault only with a volley; assisted timing is not reported; missing data gives no tip.", "–",
  auto=EM + "QuickCoachingRulesTests (3 tests)", note="EV-01", **PASS_UT)
C("TC-F06-03", "FE05 Coaching", "FR-09, FR-19", "System", "Exception", "Offline drill shows a clear analysis status",
  "1. Train offline (TC-F14-01).\n2. Look at the analysis area of the result popup for 2 minutes.",
  "A quick tip is shown and the AI analysis area states that the analysis will run after reconnecting.",
  "Offline profile", status="Failed", date=D10, tester=HUY, note="EV-02: \"Đang phân tích…\" never changes while offline → D-02.")
# ------------------------------------------------------------------ FE-04
C("TC-F07-01", "FE04 Online", "FR-14", "System", "Main", "Ranked search assigns a ranked bot when no human opponent is available",
  "1. Mode Selection → Đấu online → Đấu đơn 1v1 → Tìm trận.\n2. Wait without another player searching.\n3. Accept the ready check.",
  "MSG32 with a timer; after 180 s a ranked bot with Elo = own + 120 is offered with a 10-second ready check; after acceptance the dedicated match starts.",
  "One signed-in player; dedicated server idle", status="Failed", date=D10, tester=HUY,
  note="EV-02: bot (Elo 220) offered about 160 s after the search started (11:00:44 → 11:03:23), earlier than 180 s → D-15; the rest of the flow worked.")
C("TC-F07-02", "FE04 Online", "FR-14", "Unit", "Rule", "Every human must accept; a decline cancels the offer",
  "Run the ready-policy tests for singles and doubles rosters.",
  "All humans must accept and connect; any decline rejects the match and only accepting players re-queue; late acceptances after the deadline cannot approve.", "–",
  auto=EM + "MatchReadyPolicyTests.EveryHumanMustAcceptAndConnect, AnyDeclineCancelsWholeMatchButOnlyAcceptingPlayersRequeue, ExpiredDeadlineCannotApproveEvenWithLateAcceptances", note="EV-01", **PASS_UT)
C("TC-F07-03", "FE04 Online", "FR-14", "Unit", "Rule", "Elo pairing window and direction",
  "Run the pairing-key test with Elo pairs (200, 240), (200, 320) and (200, 321) with and without expansion and 5 losses.",
  "Pairs inside the window and preferred direction match; 200 vs 321 never matches; expanded window is ±120.", "–",
  auto=EM + "ProgressionTests.MutualPairKeysEnforceBothPreferencesAndWindow", note="EV-01", **PASS_UT)
C("TC-F07-04", "FE04 Online", "FR-14, FR-18", "Integration", "Main", "Ranked doubles fills free seats with bots after the deadline and rates the humans",
  "Automated: the check creates players and searches with a 2-second bot deadline: a solo player, a party of two, three solo players and four solo players; it starts the matches, reports the results and reads the rating ledger.",
  "Before the deadline no roster; after it one roster with bots (bot Elo = own + 120); a party stays on one team; available humans take priority over bots; the confirmation waits for a free container; doubles Elo uses the average opposing rating, one ledger row per human; clients older than protocol 20 get no bot.",
  "Disposable PostgreSQL 16 database created from Database.sql; Redis 7.4", auto="IntegrationChecks --check-ranked-doubles (CI validate job)",
  status="Passed", date=D17, tester="GitHub Actions (CI)", note="EV-11 (run 52). The playable 2v2 with four clients on the dedicated server was not run; needs four accounts at the same time (Round 2).")
C("TC-F08-01", "FE04 Online", "FR-15", "System", "Main", "Create a private singles room and join it by code",
  "1. Player A: Mode Selection → Tạo phòng → Đấu đơn 1v1; note the room code.\n2. Player B: Online lobby → enter the code → THAM GIA.\n3. Player A starts the match.",
  "Player B appears in the roster; the match starts on the dedicated server; the result is unrated.", "Two signed-in accounts on two devices",
  note="Not run: needs two devices and accounts at the same time.")
C("TC-F08-02", "FE04 Online", "FR-15", "Unit", "Rule", "Custom room start rules",
  "Run the room service tests.", "A guest cannot start; a custom room can start without readiness; an empty or overfilled team cannot start; automatic doubles still needs four players.", "–",
  auto=EM + "MultiplayerRoomServiceTests.GuestCannotStartCustom, CustomCanStartWithoutReadiness, CustomCannotStartEmptyOrOverfilledTeam, AutomaticDoublesStillRequiresFourPlayers", note="EV-01", **PASS_UT)
C("TC-F08-03", "FE04 Online", "FR-15", "Unit", "Exception", "Blank room code never connects; codes are normalised",
  "Run the room launch request tests.", "A blank code schedules no connection; the shared code is normalised before joining.", "–",
  auto=EM + "RoomLaunchRequestTests.BlankCodeNeverSchedulesConnection, JoinNormalizesSharedCode", note="EV-01. Server message MSG39 for a wrong code is not covered.", **PASS_UT)
C("TC-F08-04", "FE04 Online", "FR-15", "System", "Main", "Guests join a doubles room by code, change team and play an underfilled custom match",
  "1. Player A: Tạo phòng → TẠO PHÒNG 2V2; note the code.\n2. Players B and C: enter the code → THAM GIA.\n3. Player C: ĐỔI ĐỘI.\n4. Player A: Bắt đầu; all accept.\n5. Play to the end, then every player selects VỀ MENU or VỀ PHÒNG CHỜ.",
  "B and C appear in the roster and the team change is shown to all; the 1v2 match runs on the dedicated server; the result is unrated; every player can leave the summary.", "Three signed-in accounts",
  status="Failed", date=D18, tester=HUY,
  note="EV-16 (18/11): steps 1–4 passed (code F0EFFDAB, ĐỔI ĐỘI moved Ghost to team B, \"PHÒNG RIÊNG · 1V2\" on the dedicated server, 0 : 2, \"+0 ELO · Chế độ này không đổi Elo\"). Step 5 failed for the two guests: VỀ MENU, VỀ PHÒNG CHỜ and Xem phân tích did not react for 19 minutes, only closing the game helped (D-29). The host returned normally. The custom win gave no XP (D-30).")
C("TC-F09-01", "FE04 Online", "FR-16, FR-18", "System", "Main", "Two human players complete a ranked 1v1 on the dedicated server",
  "1. Players A and B search ranked 1v1 at the same time.\n2. Both accept the ready check.\n3. Play the match to the end.\n4. Open Match History on both clients.",
  "The match ends with a winner; history shows the result, score and Elo change; a replay is available.", "Two accounts with similar Elo; dedicated server idle",
  status="Failed", date=D09, tester=HUY + ", " + DAT, note="EV-04: two attempts (21:47 and 21:55) ended \"Gián đoạn\", 0:0, +0 Elo, no replay → D-09.")
C("TC-F09-02", "FE04 Online", "FR-12, FR-16", "Unit", "Rule", "Server rules engine: games, end change and net crossing",
  "Run the multiplayer rules tests.", "Best of three ends only after two game wins; the deciding game changes ends at 6 once; the ball must cross the net before contact.", "–",
  auto=EM + "MultiplayerRulesTests.BestOfThreeOnlyEndsAfterTwoGameWins, DecidingGameChangesEndsAtSixOnceWithoutResettingScoresOrServe, BallMustCrossNetEntirelyBeforeContact", note="EV-01", **PASS_UT)
C("TC-F09-03", "FE04 Online", "FR-17", "System", "Exception", "Reconnect within the window; forfeit after it",
  "1. During an online match disconnect player A for 20 s, then reconnect.\n2. In a second match disconnect player A for 60 s.",
  "Case 1: A returns to the same match with the server score. Case 2: MSG43; A forfeits: the opponent wins, a ranked match changes A's Elo as a loss and A receives no Tokenpad or XP (BR-28 v1.5).", "Two accounts; dedicated server idle",
  note="Not run: blocked by D-09.")
C("TC-F10-01", "FE04 Online", "FR-18", "Unit", "Rule", "Elo change uses K = 32 and the floor 100",
  "Run StandardDelta with (400, 400, win), (400, 400, loss), (100, 100, loss), (400, 360, loss).",
  "+16, −16, 0 (floor), −18.", "–", auto=EM + "HybridAiTests.StandardElo (4 cases)", note="EV-01", **PASS_UT)
C("TC-F10-02", "FE04 Online", "FR-14, FR-18", "Unit", "Rule", "Ranked bot rating and accuracy",
  "Run BotElo(100, 4), BotElo(100, 5) and BotAccuracy for Elo 0, 100, 220, 9000, 9100, 100000.",
  "BotElo 220 and 0; accuracy 25, 25, 26, 99, 100, 100.", "–", auto=EM + "ProgressionTests.BotRatingAndSeededDecisions, AccuracyThresholds (6 cases)", note="EV-01", **PASS_UT)
C("TC-F10-03", "FE04 Online", "FR-18", "Unit", "Exception", "Match result transfer is complete and applied once on the client",
  "Run the result transfer and reward idempotency tests.", "Incomplete, conflicting or oversized transfers produce no result; a match reward is applied once after reload.", "–",
  auto=EM + "MatchResultTransferTests.IncompleteConflictingAndOversizedTransfersDoNotProduceAResult; ProfileEconomyTests.PurchaseAndMatchRewardStayIdempotentAfterReload", note="EV-01. Server-side idempotency is TC-S08-03.", **PASS_UT)
# ------------------------------------------------------------------ FE-06
C("TC-F04-01", "FE06 Progress", "FR-22", "Unit", "Exception", "Purchase is refused when the balance is too low or the currency is not accepted",
  "Run the local economy tests.", "Insufficient funds leave wallet and inventory unchanged; GEMSTONE cannot be bought with Tokenpad; an owned item cannot be bought again.", "–",
  auto=EM + "LocalEconomyTests.InsufficientFundsLeaveStateUntouched, GemstoneRejectsTokenpad, OwnedItemCannotBeBoughtWithOtherCurrency", note="EV-01. Server purchase API (MSG46, MSG47) not run.", **PASS_UT)
C("TC-F04-02", "FE06 Progress", "FR-23", "Unit", "Main", "Equip an owned paddle",
  "Run the paddle equipment tests.", "Equipping persists and costs nothing; unowned or unknown paddles cannot be equipped; the default paddle is always owned.", "–",
  auto=EM + "PaddleEquipmentTests.EquipPersistsAndDoesNotSpendMoney, UnownedAndUnknownCannotEquip, DefaultIsAlwaysOwnedWithoutPurchase", note="EV-01", **PASS_UT)
C("TC-F04-03", "FE06 Progress", "FR-20", "System", "Rule", "Progression grants THOT after 10 lessons and GEMSTONE at level 25 once",
  "1. With a seeded account that has passed 9 lessons, pass a 10th lesson.\n2. With a seeded account at level 24 and 4,790/4,800 XP, win a bot match.\n3. Repeat both actions.",
  "THOT is added after step 1 and GEMSTONE after step 2; step 3 adds nothing.", "Seed accounts in Database.sql",
  note="Not run: seed accounts not yet prepared.")
C("TC-F13-01", "FE06 Progress", "FR-21", "System", "Main", "Missions popup shows daily missions, streak and reset time",
  "1. Menu → Nhiệm vụ.\n2. Read the daily tab.", "7 daily missions with progress and reward, the set bonus 200 Tokenpad + 20 XP, the daily streak and the reset time 07:00 Vietnam time.",
  "Signed-in player", status="Passed", date=D10, tester=HUY, note="EV-02, screenshot 15.")
C("TC-F13-02", "FE06 Progress", "FR-21", "Unit", "Rule", "Daily streak rules",
  "Run the daily streak tests.", "All match modes count; consecutive days increase the streak; exits and interruptions do not count; a gap restarts the streak.", "–",
  auto=EM + "DailyStreakTests.AllMatchModesCount, ExitsAndInterruptionsDoNotCount, GapExpiresAndNextMatchRestarts", note="EV-01", **PASS_UT)
C("TC-F13-03", "FE06 Progress", "FR-21", "Unit", "Exception", "A reward is claimed only once",
  "Run the claim tests.", "Catalog and achievement rewards can be claimed once in their lifetime; a repeat adds zero.", "–",
  auto=EM + "ProgressionTests.CatalogAndAchievementLifetimeClaims", note="EV-01", **PASS_UT)
# ------------------------------------------------------------------ FE-07
C("TC-F11-01", "FE07 Social", "FR-10", "System", "Main", "Training progress popup",
  "1. Menu → Tiến trình.", "Passed lessons out of 15, next lesson, feedback, practice time, most frequent error and per-lesson results.",
  "Profile with at least one attempt", status="Passed", date=D10, tester=HUY, note="EV-02, screenshot 03.")
C("TC-F11-02", "FE07 Social", "FR-24", "System", "Main", "Match history shows recent matches in Vietnam time",
  "1. Menu → Lịch sử.", "The last matches with opponent, mode, date-time (Vietnam), result, score, Elo change and Report/Replay buttons.",
  "Account with ranked matches", status="Passed", date=D10, tester=HUY, note="EV-02, screenshot 12.")
C("TC-F11-03", "FE07 Social", "FR-24", "Integration", "Exception", "A player outside the match cannot read its replay",
  "1. As player C request GET /api/OnlineReplays/{match} of a match between A and B.", "HTTP 403; no replay data returned.",
  "Finished match with a stored replay", note="Not run: the shared-replay checks are not part of the CI validate job (EV-08) and the test machine has no PostgreSQL/Redis for them.")
C("TC-F11-04", "FE07 Social", "FR-24", "Unit", "Main", "Match detail shows the score of every game for both teams and every player",
  "Run the match detail score tests (teams of one and two players, team A starting on either side).", "Both team columns list every player name and the scores of each game are shown on the correct side.", "–",
  auto=EM + "MatchDetailScoreTests.ScoreColumnsRenderEveryPlayerName (3 cases)", note="Not run: the test was added on 17/11/2026 (b08f5c7), after the last EditMode run EV-12.")
C("TC-F11-05", "FE07 Social", "FR-24", "System", "Main", "Watch the replay of an online match",
  "1. Lịch sử → Xem replay on a finished ranked doubles match.\n2. Select 2×, then TẠM DỪNG.",
  "The replay shows the court, the score and a timeline with 0.5×, 1× and 2×; the ball keeps its normal size; 2× plays faster and TẠM DỪNG stops the timeline.", "Finished online match with a stored replay",
  status="Passed", date=D18, tester=HUY,
  note="EV-16 (18/11): replay of the ranked doubles match (04:00); ball drawn at its normal size after the fix of b08f5c7; 2× advanced 00:38 → 00:43 in about 3 s; TẠM DỪNG held 00:45.")
C("TC-F12-01", "FE07 Social", "FR-25", "System", "Main", "Send and accept a friend request",
  "1. Player A: friend drawer → Tìm bạn → search player B by ID → send.\n2. Player B accepts.", "Both lists show each other as friends.", "Two accounts",
  note="Not run: needs two accounts at the same time.")
C("TC-F12-02", "FE07 Social", "FR-25", "Unit", "Exception", "Invalid friend additions are rejected",
  "Run the social model tests.", "Self, unknown and duplicate additions are rejected; removal is idempotent.", "–",
  auto=EM + "OfflineSocialTests.AddRejectsSelfUnknownAndDuplicatesAndRemoveIsIdempotent", note="EV-01", **PASS_UT)
C("TC-F12-03", "FE07 Social", "FR-26", "Unit", "Rule", "Leaderboard order follows Elo",
  "Run the leaderboard model test.", "The live player is listed once and a change of Elo reorders the list.", "–",
  auto=EM + "OfflineSocialTests.LivePlayerIsIncludedExactlyOnceAndEloChangesReorder", note="EV-01. Screen observed on 10/11 with two players at 100 Elo (screenshot 14).", **PASS_UT)
# FE-07 social feed and messages (Review 1, added 17/11/2026). IntegrationChecks --check-social-content in run 52 (EV-11).
SC = dict(auto="IntegrationChecks --check-social-content (CI validate job)", status="Passed", date=D17, tester="GitHub Actions (CI)",
          pre="Disposable PostgreSQL 16 database created from Database.sql; Redis 7.4; an author, a friend, an excluded friend and a stranger")
C("TC-F19-01", "FE07 Social", "FR-34", "Integration", "Rule", "Post visibility follows the audience, the exclusions and blocks",
  "Automated: the author saves the same post as community, private, friends and friends_except (excluding one friend); then blocks the friend.",
  "Community: visible to a stranger. Private: author only. Friends: the friend sees it, the stranger does not. Friends except: the excluded friend does not see it and cannot like it (404). After the block the friend no longer sees it.",
  **SC, note="EV-11 (run 52).")
C("TC-F19-02", "FE07 Social", "FR-34", "Integration", "Rule", "Likes are counted once and comments can be added and removed",
  "Automated: two parallel like requests of the same player; a friend comments; the author deletes the comment.",
  "One like row; the comment is listed for the author; after deletion the list is empty.", **SC, note="EV-11 (run 52).")
C("TC-F19-03", "FE07 Social", "FR-35", "Integration", "Rule", "Direct messages: friends only, retries stored once, unread counter, block",
  "Automated: the author sends the same message twice with one client message ID to an offline friend; the friend reads; a stranger opens the conversation; the author blocks the friend and sends again.",
  "One message and one notification; unread 1, then 0 after the read cursor; the stranger gets 404; after the block a new message gets 403 and the old message stays readable.", **SC, note="EV-11 (run 52).")
C("TC-F19-04", "FE07 Social", "FR-35", "Integration", "Rule", "Whispers need an online recipient, work between non-friends and stop after a block",
  "Automated: whisper to a stranger who is not in the lobby, then after the stranger's presence is registered; whisper to the blocked friend.",
  "404 while the stranger is offline; delivered once when online; 403 to the blocked player.", **SC, note="EV-11 (run 52).")
C("TC-F19-05", "FE07 Social", "FR-34", "System", "Main", "Publish a post with an MP4 training video from the Windows build and play it on another client",
  "1. Player A (Windows build): Menu → BẢNG TIN → Viết bài; text, audience Bạn bè; Chọn MP4 (H.264, 30 s); Đăng bài.\n2. Player B (friend): BẢNG TIN → Video; Thích; Bình luận.",
  "The post appears for B with the video; the video plays; like and comment counters increase for A.", "Two signed-in friends on two devices; an MP4 test file",
  note="Not run. Code review (EV-14) found that the video picker exists only in the Unity Editor, so step 1 is expected to fail on the Windows and Android builds (D-22).")
C("TC-F19-06", "FE07 Social", "FR-35", "System", "Main", "Two players exchange direct messages and whispers in the lobby",
  "1. Friends A and B in the Menu: A → TIN NHẮN → NHẮN TIN to B; B reads.\n2. A → friend drawer → THÌ THẦM to player C who is not a friend and is in the lobby.",
  "B sees the message and the unread counter; after opening it the counter is 0. C receives the whisper at once.", "Three signed-in accounts on two or three devices",
  note="Not run: needs three accounts at the same time (Round 2).")
# ------------------------------------------------------------------ FE-08/09
C("TC-F15-01", "FE08-09 Admin", "FR-29", "System", "Main", "Admin signs in and finds an account",
  "1. Open the admin web and sign in with an Admin account.\n2. Quản lý người dùng → search by e-mail.", "Dashboard opens; the account row shows role, status, Elo and play time.",
  "Admin account on staging", note="Not run in this round.")
C("TC-F15-02", "FE08-09 Admin", "FR-29", "Integration", "Exception", "Role and scope protection of the admin API",
  "1. Call GET /api/Admin/users with a Player token.\n2. Call a game endpoint with a restricted (appeals scope) token.", "Both calls return HTTP 403.",
  "Player and restricted test tokens", note="Not run as written: the admin authorisation check (--check-admin-authorization) is not part of the CI validate job.")
C("TC-F15-03", "FE08-09 Admin", "FR-29", "System", "Rule", "Progress reset needs a preview and is refused during a search",
  "1. Start a ranked search with player P.\n2. Admin opens Progress Reset for P and confirms.\n3. Cancel the search and repeat.", "Step 2 shows MSG58; step 3 resets only the selected groups and writes an audit record.",
  "Admin and player accounts", note="Not run in this round.")
C("TC-F16-01", "FE08-09 Admin", "FR-31", "System", "Main", "Save a lesson draft and publish it",
  "1. CMS → open a lesson → change the coach text → Lưu bản nháp.\n2. Xuất bản bản đã lưu.\n3. Restart the game client.", "Lesson status PUBLISHED; the client loads the new catalogue version and shows the new text.",
  "Admin account", note="Not run in this round.")
C("TC-F16-02", "FE08-09 Admin", "FR-31", "Unit", "Rule", "An attempt keeps its own lesson definition after publishing",
  "Run the catalogue version tests.", "An active attempt keeps the definition it started with; other sessions are unchanged.", "–",
  auto=EM + "TrainingTests.AttemptUsesItsOwnCatalogWithoutChangingOtherSessions; OfflineTrainingStoreTests.ActiveAttemptRetainsItsOwnDefinitionAfterCatalogChanges", note="EV-01", **PASS_UT)
C("TC-F16-03", "FE08-09 Admin", "FR-31", "System", "Exception", "Publishing a stale version is refused",
  "1. Two admins open the same lesson.\n2. Admin 1 saves.\n3. Admin 2 saves without reloading.", "Admin 2 sees MSG60 and nothing is overwritten.", "Two admin sessions",
  note="Not run in this round.")
C("TC-F17-01", "FE08-09 Admin", "FR-27, FR-30", "System", "Main", "Report an opponent and decide the report",
  "1. Player: Lịch sử → Tố cáo on a finished match → reason \"Cố ý rời trận\" → send.\n2. Admin: Xử lý vi phạm → open the report → accept with reason.", "Report goes PENDING → CHECKING → ACCEPTED with the reason saved.",
  "Finished match between two accounts", note="Not run: blocked by D-09 (no finished human match).")
C("TC-F17-02", "FE08-09 Admin", "FR-32", "System", "Main", "Stop a live dedicated match",
  "1. Start an online match.\n2. Admin: Giám sát trận đấu → Dừng trận có lý do → enter a reason → confirm MSG61.", "The match ends as interrupted on both clients; no Elo or reward.",
  "Live dedicated match", note="Not run in this round.")
C("TC-F17-03", "FE08-09 Admin", "FR-28", "System", "Exception", "Appeal validation",
  "1. Restricted player opens Quyết định & Khiếu nại.\n2. Send a 5-character statement.\n3. Send a valid statement, then try to send another.", "Step 2 shows MSG53; step 3 creates one PENDING appeal; a second appeal is not possible.",
  "Suspended test account", note="Not run in this round.")
C("TC-F18-01", "FE08-09 Admin", "FR-32", "System", "Main", "Analytics for a seeded date range",
  "1. Load the analytics seed.\n2. Thống kê → set the seeded dates.", "Daily players, retention, popular lessons, error heat map and matchmaking health equal the seeded values.", "Analytics seed",
  note="Not run: analytics seed not yet prepared.")
C("TC-F18-02", "FE08-09 Admin", "FR-32", "System", "Exception", "Analytics range longer than 366 days",
  "1. Set From = 01/01/2025 and To = 11/11/2026.", "MSG62; no request is executed.", "Admin account", note="Not run in this round.")
C("TC-F18-03", "FE08-09 Admin", "FR-32", "System", "Main", "Dashboard summary",
  "1. Sign in to the admin web.", "Total users, daily active users, matches, online time and pending reports are shown.", "Admin account", note="Not run in this round.")
# ------------------------------------------------------------------ FE-10
C("TC-X01-01", "FE10 Expert", "FR-33", "System", "Main", "Save an expert review of a training session",
  "1. Gán nhãn coaching → select a synchronised real session.\n2. Set level, error labels, data group and basis → save.", "The review is saved and the session shows the labels.",
  "Real player session with telemetry", note="Not run: no real labelled sessions yet (risk 8).")
C("TC-X01-02", "FE10 Expert", "FR-33", "System", "Exception", "Incomplete expert review is refused",
  "1. Save a review without any label.", "MSG64; nothing is saved.", "Admin account", note="Not run in this round.")

# ------------------------------------------------------------------ Workflows
C("TC-W01", "Workflows", "FR-01, FR-06, FR-08, FR-09", "Acceptance", "Workflow", "W01 Learn and sync a drill (owner: Thuận, support: Huy)",
  "1. New player registers and chooses Người mới.\n2. Finishes the tutorial.\n3. Goes offline and passes the serve drill.\n4. Reconnects.",
  "Tutorial completion, offline stars and the first-pass reward are on the server exactly once.", "Test mailbox, network switch",
  note="Not run end to end; steps 3 partly covered by TC-F14-01.")
C("TC-W02", "Workflows", "FR-13, FR-18, FR-19", "Acceptance", "Workflow", "W02 Play a bot and get a tip (owner: Huy, support: Đạt)",
  "1. Play a bot match to the end.\n2. Open the analysis.\n3. Open the recommended drill.", "Result with rewards; coaching summary with at most three drills; the drill briefing opens.",
  "Signed-in player; hybrid coaching enabled", status="Passed", date=D10, tester=HUY, note="EV-02 (TC-F05-01, TC-F06-01).")
C("TC-W03", "Workflows", "FR-14, FR-16, FR-18", "Acceptance", "Workflow", "W03 Play online and save the result (owner: Đạt, support: Huy)",
  "1. Search ranked 1v1.\n2. Accept the offer.\n3. Play on the dedicated server to the result.", "The match runs on the dedicated server and both sides see the final result.",
  "Dedicated server idle", status="Passed", date=D10, tester=HUY, note="EV-02, bot-fallback variant: accept 11:03:23, match started 11:03:46, result 0:2 at 11:06:04. Human-versus-human variant is TC-F09-01, which failed in Round 1 (D-09). D-14 found.")
C("TC-W04", "Workflows", "FR-27, FR-28, FR-30, FR-31", "Acceptance", "Workflow", "W04 Review a report and publish a lesson (owner: Kiên, support: Thuận)",
  "1. Player reports an opponent.\n2. Admin accepts and suspends.\n3. Suspended player appeals.\n4. Admin accepts the appeal.\n5. Admin edits a lesson draft and publishes it.\n6. A player restarts the client and opens the lesson.", "Account becomes SUSPENDED, then ACTIVE after the accepted appeal; all decisions are audited; the published lesson text appears in the client.",
  "Two players and one admin", note="Not run: blocked by D-09.")

# ------------------------------------------------------------------ State transitions
S = [
    ("S01", "SM-01 Account status", [("ACTIVE → SUSPENDED when an admin applies a suspension", None), ("SUSPENDED → ACTIVE when the expiry passes (expiry worker)", None), ("BANNED → ACTIVE when the appeal is accepted", None)]),
    ("S02", "SM-02 E-mail code", [("Pending → Verified with the correct code", None), ("Pending → Expired after 10 minutes", None), ("Pending → Locked after 5 wrong codes", None)]),
    ("S03", "SM-03 Training attempt", [("Started → Applied with stars; first-pass reward once", EM + "TrainingTests.ReviewStillImprovesStarsAndLogsMistakesWithoutRewardsAfterReload"),
                                      ("Started → Applied as failed; errors logged, no currency", EM + "TrainingTests.FailedDrillRecordsErrorsAndReceivesNoCurrency"),
                                      ("Applied → Duplicate when the same request is sent again", EM + "OfflineTrainingStoreTests.AcceptedOrDuplicateReceiptsRemoveTheOutboxOnce")]),
    ("S04", "SM-04 Offline queue item", [("Queued → kept from backup when the primary file is corrupt", EM + "OfflineTrainingStoreTests.CorruptPrimaryFallsBackToTheLastValidBackup"),
                                        ("Sending → ignored when a delayed response is older than the snapshot", EM + "OfflineTrainingStoreTests.DelayedSyncResponseCannotOverwriteANewerSnapshot"),
                                        ("Queued → Applied after reconnecting", None)]),
    ("S05", "SM-05 Match phase", [("Rally → BetweenGames only at 11 with a 2-point lead (11–10 continues, 12–10 ends the game)", EM + "SinglesMatchTests.ElevenTenDoesNotWinAndTwelveTenDoes"),
                                 ("Rally → end change once at 6 in the deciding game", EM + "SinglesMatchTests.DecidingGameChangesEndsAtSixOnlyOnce"),
                                 ("Settling → MatchOver only after the second game win", EM + "SinglesMatchTests.MatchRequiresTwoGameWins")]),
    ("S06", "SM-06 Matchmaking search", [("SEARCHING → MATCHED with two human players", None),
                                        ("SEARCHING → BOT with a ready check that still needs the human's confirmation", "obs"),
                                        ("MATCHED → offer declined → accepting player back to SEARCHING", EM + "MatchReadyPolicyTests.AnyDeclineCancelsWholeMatchButOnlyAcceptingPlayersRequeue")]),
    ("S07", "SM-07 Online room phase", [("searching → assigning-bot → ready → loading → playing", "obs"), ("playing → finished with a forfeit after the reconnection window (BR-28 v1.5)", None),
                                       ("Failed connection still allows leaving and retrying", EM + "RoomOperationGateTests.FailedConnectionStillAllowsLeavingAndRetry")]),
    ("S08", "SM-08 Game match", [("PREPARING → PLAYING → FINISHED with the result saved once", None), ("PLAYING → interrupted by an admin stop", None), ("FINISHED → a repeated result is ignored", None)]),
    ("S09", "SM-09 Dedicated lease", [("queued → starting → ready, then players admitted", "obs2"), ("finishing → released after the result", None), ("ready → released when heartbeats stop (lease expiry)", None)]),
    ("S10", "SM-10 Lesson publication", [("DRAFT → PUBLISHED", None), ("PUBLISHED → UNPUBLISHED", None), ("Publish refused when the version changed", None)]),
    ("S11", "SM-11 Player report", [("PENDING → CHECKING", None), ("CHECKING → ACCEPTED with a sanction", None), ("CHECKING → DECLINED", None)]),
    ("S12", "SM-12 Sanction appeal", [("PENDING → ACCEPTED and the sanction is revoked", None), ("PENDING → DECLINED and the sanction stays", None), ("A second appeal for the same sanction is refused", None)]),
    ("S13", "SM-13 AI analysis job", [("pending → processing → completed", "obs3"), ("processing → failed after the retries", None),
                                     ("Duplicate delivery is ignored and cannot cross accounts", EM + "NetworkCoachingTests.AuthoritativeDeliveryDeduplicatesAndCannotCrossAccountOrSession")]),
    ("S14", "SM-14 Friend request", [("PENDING → ACCEPTED", None), ("PENDING → DECLINED", None), ("Friendship removed when one player blocks the other", None)]),
]
OBS = {"obs": ("EV-02: search → bot offer → accept → dedicated match on 10/11 (11:00–11:06).", D10),
       "obs2": ("EV-02: client showed \"Đang chuẩn bị máy chủ thi đấu…\" and the match started about 23 s after acceptance.", D10),
       "obs3": ("EV-02: the analysis of the 10/11 bot match was completed by the server and shown in the client.", D10)}
for sid, title, items in S:
    for k, (tr, auto) in enumerate(items, 1):
        cid = "TC-%s-%02d" % (sid, k)
        if auto and auto.startswith(EM):
            C(cid, "States", title, "Unit", "State", tr, "Run the automated test.", "The transition happens exactly as described; no other state changes.", "–", auto=auto, note="EV-01", **PASS_UT)
        elif auto in OBS:
            C(cid, "States", title, "System", "State", tr, "Drive the scenario in the game client and observe the state shown by the client and the API.",
              "The transition happens exactly as described.", "Staging server", status="Passed", date=OBS[auto][1], tester=HUY, note=OBS[auto][0])
        else:
            C(cid, "States", title, "Integration", "State", tr, "Set up the source state with the seed data, trigger the event through the API, then read the entity.",
              "The entity is in the target state and the change is recorded; invalid events leave the state unchanged.", "Seeded staging or local database",
              note="Not run: not covered by the IntegrationChecks groups of the CI validate job (EV-08, EV-11); needs a new check or seeded staging data.")

# ------------------------------------------------------------------ Changes of 18–25/11/2026
PASS_25 = dict(status="Passed", date=D25, tester=HUY)
CI_25 = dict(status="Passed", date=D25, tester="GitHub Actions (CI)")
C("TC-F01-09", "FE01 Account", "FR-02", "Integration", "Rule", "Signing in on a second device ends the first session",
  "Automated: sign in twice with the same account, use the first access and refresh token, sign out with an old token, sign in four times in parallel.",
  "The first access and refresh token are refused; other accounts are not affected; a delayed sign-out of the replaced device cannot end the new session; after parallel sign-ins exactly one session is valid (BR-62).", "Fresh database",
  auto="IntegrationChecks --check-single-session (CI validate job)", note="EV-19.", **CI_25)
C("TC-F01-10", "FE01 Account", "FR-02", "System", "Exception", "The replaced device returns to the login screen",
  "1. Sign in on PC 1.\n2. Sign in with the same account on PC 2.\n3. Watch PC 1.", "PC 1 shows MSG84 on the Login screen within a few seconds; PC 2 keeps working.", "One account, two devices",
  note="Not run: one device per account needs a second device or a second password entry by the account owner.")
C("TC-F06-04", "FE05 Coaching", "FR-19", "System", "Main", "Analysis after a bot match",
  "1. Play a bot match (Easy) to the end.\n2. Wait on the result screen.\n3. Select Xem phân tích AI.",
  "The button changes to \"Xem phân tích AI\" without reopening the screen; the panel shows the number of shots, clean shots, rate and faults, every fault with one piece of advice and the recommended drills.", "Signed-in player, staging",
  note="EV-20 (25/11, 13:51): 28 shots, 19 clean (68%), 9 faults (Đánh hụt 7/28, Bóng nảy hai lần 2/28), drills \"Giao bóng đúng ô\" and \"Đánh bạt về cuối sân\". The figures are those of the engine; D-37 questions two of them.", **PASS_25)
C("TC-F07-05", "FE04 Online", "FR-14", "System", "Exception", "Nobody accepts the ready check",
  "1. Two members search ranked 1v1.\n2. Let the ready check expire on both clients.",
  "Both clients show the match-found intro and the countdown, then return to the lobby with a message; no Elo changes.", "Two member accounts with similar Elo",
  note="EV-20 (25/11): Elo 114 and 117 matched after about 31 s; \"Lượt xác nhận đã kết thúc. Bạn có thể tìm trận lại.\"; Elo unchanged.", **PASS_25)
C("TC-F07-06", "FE04 Online", "FR-14", "Unit", "Rule", "The intro never shortens the time to decide",
  "Run the ready-check overlay and choreography tests.", "The buttons wait for the intro when the server grants the grace, and the intro ends within three seconds of its start; the decision time stays 10 s.", "–",
  auto=EM + "MatchReadyOverlayRulesTests.ControlsWaitForTheIntroWhenTheServerGrantsGrace, ControlsNeverEatIntoDecisionTime; MatchFoundChoreographyTests.HitsBulletTimeAndLockHappenInOrderWithinThreeSeconds", note="EV-18", **PASS_25)
C("TC-F08-05", "FE04 Online", "FR-15", "System", "Main", "Two players swap positions in a private room",
  "1. Host and guest are in a 1v1 room.\n2. The host selects the position button of the guest and confirms.\n3. The guest selects Đồng ý.", "Both clients show the swapped teams (BR-63).", "Two member accounts",
  note="EV-20 (25/11): request and answer dialogs shown, seats swapped on both clients.", **PASS_25)
C("TC-F08-06", "FE04 Online", "FR-15", "System", "Exception", "The host leaves a private room",
  "1. Host and guest are in a room.\n2. The host selects CHỌN CHẾ ĐỘ.", "The guest becomes the host and the room stays open with the same code.", "Two member accounts",
  note="EV-20 (25/11): \"Bạn là chủ phòng.\", same room code.", **PASS_25)
C("TC-F09-04", "FE04 Online", "FR-12, FR-16", "Unit", "Rule", "Referee calls from the point of view of the reader",
  "Run the rally call tests.", "Own fault: red MẤT LƯỢT; opponent fault: green call that follows side-out scoring; doubles names the team and the second server; the text stays still for about 1.5 s.", "–",
  auto=EM + "RallyCallTests (5 tests)", note="EV-18", **PASS_25)
C("TC-F09-05", "FE04 Online", "FR-12, FR-16", "System", "Main", "The same fault is announced differently to both players",
  "1. Two players are in an online match.\n2. Each of them lets the serve timer expire once.", "The player at fault sees the red call, the opponent the green call, with the right consequence (MSG30).", "Two member accounts",
  note="EV-20 (25/11): four serve time-outs, calls correct on both clients (screenshots/50).", **PASS_25)
C("TC-F09-06", "FE04 Online", "FR-17", "System", "Exception", "Giving up a running singles match asks for a confirmation",
  "1. In an online singles match select Đầu hàng.\n2. Select TIẾP TỤC ĐẤU.\n3. Select Đầu hàng again and confirm with ĐẦU HÀNG.",
  "Step 1 opens a box with the consequence and ends nothing; step 2 closes it; step 3 ends the match, the player loses (Thua · Đầu hàng, no reward), the opponent wins and both see the summary (BR-66).", "Online match in progress",
  note="Failed in EV-20 (25/11): the former button Rời trận forfeited the match at once (D-34). Fixed on 25/11; the three steps passed in the court probe EV-21 (one PC, no server). Retest on staging with the next release.")
C("TC-F09-07", "FE04 Online", "FR-16, FR-18", "Integration", "Rule", "The result of a dedicated match is published with its sealed replay",
  "Automated: upload replay batches, complete the match before and after the recording is sealed, repeat batches.", "The result is withheld until the replay is complete; repeated batches are stored once; the sealed replay has every chunk.", "Fresh database, replay storage of the test",
  auto="IntegrationChecks --check-dedicated (CI validate job)", note="EV-19.", **CI_25)
C("TC-F09-08", "FE04 Online", "FR-17", "Unit", "Rule", "Surrender requests follow the rules of a team",
  "Run the surrender tests (server rules and the box of every player on simulated clients).",
  "Singles needs one confirmation, doubles both; the box opens for both partners at once and never for the opponents; refusal, withdrawal, timeout, waiting time, disconnected partner and silent server are handled; bots and strangers cannot answer; the result is labelled Thua · Đầu hàng.", "–",
  auto=EM + "SurrenderTests (20 tests)", note="EV-18; server rules and box of D-34.", **PASS_25)
C("TC-F09-09", "FE04 Online", "FR-17", "System", "Main", "Two partners surrender a doubles match together",
  "1. In a 2v2 match partner A selects Đầu hàng.\n2. B refuses.\n3. After 15 s A asks again; A confirms, B confirms.",
  "Step 1 shows the box to A and B, nothing to the opponents; step 2 tells A and the match goes on; step 3 ends the match, A and B lose, the opponents win (BR-66).", "Four accounts, or a party of two against ranked bots",
  note="Not run on staging: the match server with the request is not released yet. The steps passed in the court probe EV-21 with the partner's messages injected.")
C("TC-F09-10", "FE04 Online", "FR-17, FR-18", "Integration", "Rule", "A surrendered dedicated match is stored as a rated loss without reward",
  "Automated: complete a dedicated doubles match with the surrender result of both partners, then with another reason text.",
  "Both partners: loss, Elo change, unchanged Tokenpad, label Thua · Đầu hàng; another text is stored as the general forfeit reason.", "Fresh database",
  note="Not run: the check was added to the group ranked-doubles on 25/11/2026 and runs in the pipeline with the next push.")
C("TC-F11-06", "FE07 Social", "FR-24", "System", "Main", "The replay of an online match is available at once",
  "1. Finish an online match.\n2. Lịch sử → Xem replay of this match.", "The replay opens directly after the match and plays with pause, seek and speed.", "Two member accounts",
  note="EV-20 (25/11): replay of the private-room match of 13:57 opened and played; the court shown is not the court of the match (D-35).", **PASS_25)
C("TC-F04-04", "FE06 Progress", "FR-22", "Unit", "Exception", "Shop items without a model in this build do not stop the client",
  "Run the catalogue merge tests.", "Unknown, empty and missing item codes are skipped; known items take title and price from the server; items that are not sold are hidden.", "–",
  auto=EM + "CatalogMergeTests (2 tests)", note="EV-18; verifies the client part of D-33.", **PASS_25)
C("TC-F04-05", "FE06 Progress", "FR-22, FR-31", "Integration", "Exception", "An item added in the CMS without game configuration is not sent to the clients",
  "Automated: add a cosmetic with the code paddle.cms-… and the default configuration, read the game catalogue.", "The catalogue contains no item without an id; the other items are unchanged.", "Fresh database",
  note="Not run: the check was added to the group content with the fix of D-33 on 25/11/2026 and runs in the pipeline with the next push.")
C("TC-F16-04", "FE08-09 Admin", "FR-31", "Integration", "Main", "A lesson holds several drills that are published together",
  "Automated: create a lesson, add two drills, publish, edit a draft, publish again, unpublish.", "Drafts are absent from the player catalogue; after publishing both drills appear in order; attempts keep their snapshot.", "Fresh database",
  auto="IntegrationChecks --check-content (CI validate job)", note="EV-19.", **CI_25)
C("TC-F17-04", "FE08-09 Admin", "FR-30", "Unit", "Exception", "A replay link opens only its own replay",
  "Run the replay link tests.", "Other protocols, extended or ambiguous links are refused; the view token reads one match without account authentication; expired or disposed tokens cannot read.", "–",
  auto=EM + "WebReplayTests (6 tests); WindowsReplayBridgeTests (2 tests)", note="EV-18", **PASS_25)
C("TC-F17-05", "FE08-09 Admin", "FR-30", "System", "Main", "Open the replay of a reported match from the admin web",
  "1. Moderation → Xem replay on a report.\n2. Confirm the browser dialog.\n3. Use the same link again.", "The game opens the Replay Viewer of this match without changing the signed-in account; the second use shows MSG85.", "Admin account, Windows game installed",
  note="Not run. The report row shows Xem replay (EV-20); the server part is in IntegrationChecks --check-replay-launch, which is not in the pipeline (D-49).")
C("TC-F20-01", "FE11 Membership", "FR-36", "System", "Main", "The membership panel of a member",
  "1. Menu → THÀNH VIÊN.", "Price and bonus from the server, the three benefits, \"Gói thành viên đang hoạt động.\" and the disabled button ĐÃ KÍCH HOẠT.", "Member account",
  note="EV-20 (25/11): 49.000 đ, +3.000 Elite Token, ĐÃ KÍCH HOẠT (screenshots/48).", **PASS_25)
C("TC-F20-02", "FE11 Membership", "FR-36", "System", "Exception", "A player without membership meets the locks",
  "1. Sign in with an account without membership.\n2. Select Đấu online, Tạo phòng and the bot level Tự động.", "The cards carry a lock; each choice opens the membership panel with MSG79; Easy, Medium, Hard, training and the shop stay usable.", "Account without membership",
  note="Not run: no account without membership was signed in during EV-20.")
C("TC-F20-03", "FE11 Membership", "FR-36", "System", "Main", "Buy the membership",
  "1. MUA NGAY; pay 49,000 VND on the PayOS page.\n2. Return to the game.", "MSG83, +3,000 Elite Token once, the locks disappear without a restart; the order is PAID.", "Account without membership; a payment with real money by its owner",
  note="Not run: PayOS moves real money on staging (Report 1, LI-14); the account owner must pay.")
C("TC-F20-04", "FE11 Membership", "FR-36", "System", "Exception", "An unsigned payment notification is refused",
  "POST /api/payments/payos/webhook with an empty body and no signature.", "HTTP 403, nothing is stored or activated.", "Staging",
  note="EV-20 (25/11, 13:40): 403 \"Chữ ký PayOS không hợp lệ.\"", **PASS_25)
C("TC-F20-05", "FE11 Membership", "FR-36", "Integration", "Rule", "Order, signature, amount, partial payment and permissions",
  "Automated: public PayOS signature vector, forged webhook, wrong amount, partial and late payment, two parallel checkouts, member and non-member calls to rooms, matches and the adaptive bot.",
  "BR-58 to BR-60 hold; the Elite Token is credited once; every locked action of a non-member is refused with MSG79.", "Fresh database and Redis",
  note="Not run in the pipeline: the group membership of IntegrationChecks exists but is not in the validate job (D-49), and no local database was available on 25/11/2026.")

# ------------------------------------------------------------------ NFR
NFR = [("NFR-01", "Usability test with 10 first-time students", "≥ 8 of 10 finish the tutorial in ≤ 5 min and pass the serve lesson within 3 attempts"),
       ("NFR-02", "RTT over 10 online matches from Ho Chi Minh City", "p95 RTT ≤ 100 ms"),
       ("NFR-03", "Profiler capture on the reference PC and Android phone", "PC ≥ 60 FPS average, 1% low ≥ 45; phone ≥ 30 FPS"),
       ("NFR-04", "k6 load test with 2,000 virtual users for 10 minutes", "p95 ≤ 500 ms and errors < 1%"),
       ("NFR-05", "Timing of 50 analysis requests", "p95 ≤ 1 s server processing"),
       ("NFR-06", "Holdout evaluation against expert labels", "precision ≥ 0.80, recall ≥ 0.70; model gate conditions"),
       ("NFR-07", "Uptime probe every 5 minutes for 14 days", "≥ 99.5% availability"),
       ("NFR-08", "Retry and restore drill", "no duplicate or lost result; restore ≤ 1 h"),
       ("NFR-09", "Security checklist, negative API tests and secret scan", "all items satisfied; no secret in the repository"),
       ("NFR-10", "Install and smoke test on Windows 10/11 and Android 8+", "client starts, signs in and plays a drill on each platform"),
       ("NFR-11", "20 offline attempts then reconnect", "100% synchronised exactly once"),
       ("NFR-12", "Reference check, test report and API response review", "no reverse dependency; tests pass; e-mail never exposed to players")]
NFR_NOTE = {"NFR-03": "Not run on the reference devices. The Unity Editor showed 51–79 FPS during the 10/11 bot match (not a valid measurement).",
            "NFR-09": "Not run. Known failures already logged: D-17 (secrets in the Git history of the public repository) and D-18 (HTTP on staging, fixed).",
            "NFR-10": "Not run on a device. EV-10: an Android APK was built from main @ 5963501 on 12/11/2026 (190.5 MB, IL2CPP, ARM64, Android 8.0+) and another from 22690ad on 14/11/2026; neither has been installed on a phone (Report 1, LI-11). Both use protocol 18, while the servers use protocol 20 since 17/11/2026, so a new APK has to be built from main @ 96ff8e9 for the device test. Windows was used in EV-02 and EV-13.",
            "NFR-12": "Not run as a formal check; the project references follow the layered rule (checked by reading the .csproj files)."}
for i, (nid, how, crit) in enumerate(NFR, 1):
    C("TC-N%02d" % i, "NFR", nid, "System", "NFR", how, "Follow the verification method of %s in Report 3." % nid, crit, "Release candidate on staging and reference devices",
      note=NFR_NOTE.get(nid, "Not run: planned in It-5 / It-6."))

DEFECTS = [
    ("D-01", "Offline banner overlaps the titles of Progress, the training HUD and the lesson list", "UI", "Low", "TC-F14-01", D10, "Thuận"),
    ("D-02", "Offline: the analysis area stays at \"Đang phân tích…\" forever", "Functional", "Medium", "TC-F06-03", D10, "Đạt"),
    ("D-03", "Key-hint box (shot cue) covers the player character in training, bot and online matches", "UI", "Low", "TC-F03-01", D10, "Huy"),
    ("D-04", "\"Quyết định & Khiếu nại\" button overlaps the PLAY button and the profile panel", "UI", "Medium", "TC-F01-02", D10, "Thuận"),
    ("D-05", "Fourth bot difficulty shows the technical name \"BhTree\"", "UI text", "Low", "TC-F05-01", D10, "Huy"),
    ("D-06", "Bot card says \"Không cần kết nối mạng\" but bot matches are disabled offline", "UI text", "Low", "TC-F14-01", D10, "Thuận"),
    ("D-07", "Male/female choice is not applied in bot match, training and tutorial (always the female character)", "Functional", "Medium", "TC-F05-01", D10, "Huy"),
    ("D-08", "New players start at Elo 100 while the admin reset-Elo default is 2000", "Business rule", "Medium", "TC-F10-01", D10, "Đạt"),
    ("D-09", "Ranked 1v1 between two humans ended \"Gián đoạn\" twice, without replay", "Functional", "High", "TC-F09-01", D09, "Đạt"),
    ("D-10", "\"Xem phân tích\" button on the match result is oversized and covers the centre of the screen", "UI", "Low", "TC-F05-01", D10, "Thuận"),
    ("D-11", "Coaching popup mixes session-wide (2/86) and per-technique (1/16) figures", "Usability", "Low", "TC-F06-01", D10, "Đạt"),
    ("D-12", "Elite Token has no earning source, so GEMSTONE can only be granted at level 25", "Requirement", "Medium", "TC-F04-01", D10, "Kiên"),
    ("D-13", "Hard bot is beaten 2–0 by the auto-player with short rallies; balance not validated", "Balance", "Medium", "TC-F05-01", D10, "Huy"),
    ("D-14", "After a normal ranked result the lobby shows \"Mất kết nối Photon.\"", "UI message", "Low", "TC-W03", D10, "Đạt"),
    ("D-15", "Ranked bot offered about 160 s after the search started instead of 180 s", "Functional", "Low", "TC-F07-01", D10, "Đạt"),
    ("D-16", "Sync-UnityTests.ps1 fails to parse in Windows PowerShell 5.1 (UTF-8 file without BOM)", "Tooling", "Medium", "Unit test run", D11, "Thuận"),
    ("D-17", "Development credentials committed in appsettings.json and a fallback JWT key in Program.cs", "Security", "High", "TC-N09", D11, "Đạt"),
    ("D-18", "Staging API and admin web are served over HTTP instead of HTTPS", "Security", "High", "TC-N09", D11, "Đạt"),
    ("D-19", "Settings tile is pushed below the screen after the Decisions and Appeals tile was added to the menu", "UI", "Low", "TC-F01-02", D11, "Huy"),
    ("D-20", "Level 25 needs 60,000 XP in total (n × 200 per level) while a match gives 5–10 XP, so the GEMSTONE paddle (BR-37) is effectively unreachable", "Requirement", "Medium", "TC-F04-03", D12, "Kiên"),
    ("D-21", "The Android APK declares usesCleartextTraffic=false (manifest written by the App UI package) while BackendApiSettings used http://tad-min.io.vn:8080, so API calls from an Android phone are expected to be refused", "Configuration", "High", "TC-N10", D12, "Huy"),
    ("D-22", "The MP4 upload of the social feed is compiled only for the Unity Editor; the Windows and Android builds show \"Tải video hiện khả dụng trong Unity Editor.\", so players cannot share training videos as Review 1 asked", "Functional", "High", "TC-F19-05", D17, "Đạt"),
    ("D-23", "The deploy job of the pipeline is skipped when the FastAPI and admin web checks are skipped, so nine passing runs of 13–16/11/2026 did not deploy and staging kept the release of 15/11/2026", "DevOps", "Medium", "CI history (EV-14)", D17, "Đạt"),
    ("D-24", "The merge 96ff8e9 lists \"Unity.TextMeshPro\" twice in PickleVerse.FrontEnd.Tests.asmdef, so the EditMode test assemblies do not compile on main and a Windows build with the synced tests stops with \"Scripts have compiler errors\"", "Build", "Medium", "Windows build (EV-15)", D17, "Đạt"),
    ("D-25", "Every admin web page shows \"Server API: http://localhost:8080\" on staging (text hard-coded in AdminLayout.tsx), although the requests go to https://tad-min.io.vn/api", "UI text", "Low", "TC-F15-01", D17, "Kiên"),
    ("D-26", "Expert review: with no label the save button is only disabled and MSG64 is not shown, so the reviewer is not told what is missing", "Usability", "Low", "TC-X01-02", D17, "Kiên"),
    ("D-27", "Reporting a player from the match detail fails: the client sends the match id without hyphens, the API answers \"The JSON value could not be converted to System.Guid\" and this English text is shown to the player; no report can be sent from the game", "Functional", "High", "TC-F17-01", D17, "Đạt"),
    ("D-28", "A player who does not return within the 30-second reconnection window is stored as a forfeit loss (\"Thua · Bỏ trận\") and the opponent wins, while BR-28, FR-17 and TC-F09-03 say that the match ends as interrupted without Elo or reward (the Elo effect was not observable because the player was at the 100 floor)", "Requirement", "Medium", "TC-F09-03", D18, "Đạt"),
    ("D-29", "After a custom doubles room match the guests stay on the match summary: VỀ PHÒNG CHỜ, VỀ MENU and Xem phân tích do not react (19 minutes observed) and only closing the game helps; the host returns normally", "Functional", "High", "TC-F08-04", D18, "Đạt"),
    ("D-30", "Custom room matches give no XP (MatchResult.Eligible excludes custom matches), while BR-32 says that every finished match gives 10 XP for a win and 5 XP for a loss", "Requirement", "Low", "TC-F08-04", D18, "Kiên"),
    ("D-31", "The referee banner of the bot match shows the technical fault codes \"PermanentObject\" and \"DoubleHit\" instead of a Vietnamese text", "UI text", "Low", "TC-F05-01", D18, "Huy"),
    ("D-32", "After an appeal is accepted, Làm mới in Quyết định và khiếu nại returns to the login screen that still shows the old text \"Tài khoản đang bị hạn chế. Bạn có thể xem quyết định và gửi khiếu nại.\"; signing in again works", "UI message", "Low", "TC-W04", D18, "Thuận"),
    ("D-33", "A shop item that the client build does not know stops the game for every player: OnlinePlayerEconomy.ApplyCatalog throws inside ProgressionRuntime.Changed, so the profile load and every command fail. One item with a code starting with \"paddle.\" added in the CMS (UC-60), or an item of a newer build, is enough; the CMS cannot delete it", "Functional", "Critical", "TC-F04-04", D25, "Huy"),
    ("D-34", "The court button Rời trận forfeits a running online match at once; the confirmation of FR-17 is shown only when leaving through the settings, so a mis-click costs Elo in a ranked match", "Functional", "High", "TC-F09-06", D25, "Đạt"),
    ("D-35", "The Replay Viewer shows the old stadium court, not the court, light and crowd of the match that was played", "Functional", "Medium", "TC-F11-06", D25, "Huy"),
    ("D-36", "Analytics: \"Hủy sau ghép\" counts every search whose ticket is closed after the match (37 of 37 matched searches on staging), so the figure says that all matches were cancelled", "Functional", "Medium", "TC-F18-01", D25, "Đạt"),
    ("D-37", "Coaching figures mislead: serve time-outs are counted as shots, and a swing in a rally that is lost by a rule fault (ball past the player to the fence, body hit) counts as a clean shot – 68% clean shots in a 0–2 loss without a point", "Business rule", "Medium", "TC-F06-04", D25, "Đạt"),
    ("D-38", "Match history names the opponent of a bot match with the internal account name (player9034cf523f53) instead of Bot", "UI text", "Low", "TC-F11-02", D25, "Huy"),
    ("D-39", "Private room: most player cards show a silhouette; the portrait appears only for the local player in slot B and changes with the swap", "UI", "Low", "TC-F08-05", D25, "Huy"),
    ("D-40", "The friends column keeps the button MỜI for a friend who already is in the room", "UI", "Low", "TC-F08-01", D25, "Đạt"),
    ("D-41", "Private room: when the ready check expires the guest reads \"Một người đã hủy tìm trận.\" although nobody searched", "UI text", "Low", "TC-F08-01", D25, "Đạt"),
    ("D-42", "Singles online HUD shows the doubles serving order (\"NGƯỜI 1 · LƯỢT 2\")", "UI text", "Low", "TC-F09-05", D25, "Đạt"),
    ("D-43", "Shop detail: the footer text is cut at 960 × 540 and the Gemstone paddle has no description of its ball effect", "UI", "Low", "TC-F04-01", D25, "Huy"),
    ("D-44", "A ball that the receiver does not return is announced as \"Bóng chạm vật cố định ngoài sân\" when it reaches the fence before the second bounce; a beginner cannot see what went wrong", "Usability", "Low", "TC-F05-01", D25, "Huy"),
    ("D-45", "Admin match history shows the match ID, the kind in English and the games only: no players, no winner and no reason (0 : 0 for forfeits and stops), and it cannot be searched", "Usability", "Medium", "TC-F18-03", D25, "Kiên"),
    ("D-46", "BR-24 pairs two waiting players with different Elo only after 30 seconds, because the window of the lower one extends upwards and that of the higher one does not extend downwards; average wait 19.6 s and P95 34.9 s with two players online", "Business rule", "Medium", "TC-F07-03", D25, "Đạt"),
    ("D-47", "A match of two inactive players goes on until the server limit of two hours and then ends as interrupted: the serve passes every 10 seconds, no score changes and the game-server container stays occupied", "Functional", "Medium", "TC-F09-05", D25, "Đạt"),
    ("D-48", "The Dragon Ruby paddle is in the build and in Database.sql but not on sale on staging, because the deployment does not change the database (Report 2, risk 17)", "DevOps", "Medium", "TC-F04-01", D25, "Đạt"),
    ("D-49", "The IntegrationChecks groups membership and replay-launch are not run by the pipeline, so the payment path has no automatic regression test", "DevOps", "Medium", "CI history (EV-19)", D25, "Đạt"),
]
# Defects fixed in code after Round 1: commit on main and verification (EV-07). They are closed only after the Round 2 retest on staging.
DEFECT_FIX = {
    "D-03": "5963501",
    "D-04": "31a7c2e",
    "D-05": "d2fc5be",
    "D-07": "6d1ed37",
    "D-19": "9dd994c",
    "D-18": "be556a1",
    "D-21": "be556a1",
    "D-20": "b08f5c7",
    "D-16": "291542a", "D-23": "291542a", "D-24": "291542a",
    "D-08": "0cee928", "D-25": "0cee928", "D-26": "0cee928",
    "D-11": "95ff4a4",
    "D-02": "9557581", "D-06": "9557581", "D-10": "9557581, 329810f", "D-14": "9557581", "D-22": "9557581",
    "D-27": "9557581", "D-29": "9557581", "D-31": "9557581",
    "D-33": "fix of 25/11/2026", "D-34": "fix of 25/11/2026",
}
# How each fix was checked (shown with the defect list).
DEFECT_CHECK = {
    "D-18": "Verified on staging on 13/11/2026 16:21 (EV-09): API and admin web answer only over HTTPS, port 80 redirects and port 8080 is closed",
    "D-21": "The client address is https://tad-min.io.vn since be556a1, so cleartext HTTP is no longer needed; not yet verified with a new Android build on a device",
    "D-20": "XP per level changed to level × 10 by b08f5c7 (17/11/2026); level 25 now needs 3,000 XP in total. The new EditMode test EligibleWinGrantsTenXpAtEveryLevel has not been run yet",
    "D-03": "Checked in Play Mode (EV-07); on the Windows build of 96ff8e9 (EV-15) the key-hint box stays below the player in training and online matches; the bot match was not retested",
    "D-14": "EV-17: after a room match and after a ranked 1v1 the lobby shows \"Trận đã kết thúc.\"; in these runs the dedicated server did not leave while the players read the summary, so the branch for a planned server stop was checked by code review only",
    "D-23": "The new condition was checked by review; the runs of 18/11/2026 ran every check and deployed, so the skipped-check case has not occurred since the fix",
    "D-31": "Vietnamese names added in BotMatch.FaultText; no banner with these faults occurred in the bot matches of EV-17, so the fix was checked by code review; in EV-20 (25/11) the fault PermanentObject was shown as \"Bóng chạm vật cố định ngoài sân\", DoubleHit has not occurred",
    "D-34": "The court button is now Đầu hàng with a confirmation box; in doubles both partners have to confirm within 20 s (BR-66). Checked by 20 EditMode tests (SurrenderTests, EV-18) and by the court probe EV-21 (36 checks); the database check in ranked-doubles runs with the next push, the retest on staging (TC-F09-06, TC-F09-09) with the next release. Leaving in the settings keeps its own dialog",
    "D-33": "The client skips items without a model (OnlinePlayerEconomy.Merge; EditMode tests CatalogMergeTests, EV-18) and the API withholds items without game configuration (GameRepository.Catalog; new check in the group content, runs with the next push). Builds older than the fix still fail when the server sells an item they do not know, so a new item must be added to the database only after every player has the new build",
}


# Defects with a state other than Open or Fixed: (state, date, explanation).
DEFECT_STATE = {
    "D-22": ("Partly fixed", D18, "Commit 9557581 adds the native Windows open-file dialog; in EV-17 an MP4 was uploaded, played and deleted from the Windows build. The Android build still cannot pick a video and says that the upload is available on Windows"),
    "D-12": ("As designed", D18, "GEMSTONE is granted at level 25, which is reachable since the XP curve of b08f5c7 (D-20). Since 22/11/2026 Elite Token is credited with the membership (3,000 tokens, BR-58); a player without membership still has no source, as stated in Report 1 LI-02"),
    "D-17": ("Partly fixed", D13, "Commit be556a1 emptied the credential values in appsettings.json and removed the fallback JWT key from Program.cs (the API now refuses to start without a key of at least 32 bytes). The old values are still readable in the Git history of the public repository, so they must be rotated on staging and the rotation confirmed"),
}


# Defects closed after a successful retest: (date, how the retest was done).
DEFECT_CLOSED = {
    "D-04": (D18, "Retested on the Windows build of 96ff8e9 at 960 × 540 (EV-15): the KHIẾU NẠI tile sits in the bottom bar and covers neither CHƠI NGAY nor the profile panel"),
    "D-09": (D17, "Retested in EV-15: a ranked 1v1 between two human accounts was matched in about 30 s, played on the dedicated server to 2 : 0 and saved for both players with the Elo change (+16) and a replay; the fixing commit was not identified"),
    "D-15": (D18, "Retested in EV-15: with no other player searching, the ranked bot was offered 3 min 01 s after the search started (07:42:06 → 07:45:07)"),
    "D-19": (D18, "Retested in EV-15: the CÀI ĐẶT tile is fully visible in the bottom bar at 960 × 540"),
    "D-20": (D18, "Retested in EV-15: the level bar shows 10 XP for level 1 and 20 XP for level 2 (level × 10)"),
    "D-01": (D18, "Retested in EV-17 on the Windows build: the offline banner sits below the profile and currency plates in the menu, does not cover the Progress title and docks at the bottom centre in the lesson (fixed by 404b2cd)"),
    "D-02": (D18, "Retested in EV-17: an offline result shows \"HLV • Đăng nhập để xem phân tích\" and the details explain that the analysis needs a connection"),
    "D-03": (D18, "Retested in EV-17: in the bot match the key-hint box \"Đánh bạt · J\" stays below the player"),
    "D-05": (D18, "Retested in EV-17: the fourth bot level reads \"Tự động\""),
    "D-06": (D18, "Retested in EV-17 on the final build: the bot setup card says \"Cần đăng nhập\""),
    "D-07": (D18, "Retested in EV-17: the chosen female character is used in the bot match and in training; the tutorial uses the same setting and was not opened"),
    "D-08": (D18, "Retested in EV-17 on staging (0cee928 deployed): the ranking reset preview says \"Elo về 100 (mức khởi đầu của người chơi mới)\"; the CI progress-reset check expects EloPolicy.Minimum"),
    "D-10": (D18, "Retested in EV-17 on the final build: \"Xem phân tích\" is a third button of the same size on the CHƠI LẠI / CHỌN CHẾ ĐỘ row"),
    "D-11": (D18, "Retested in EV-17 on staging (95ff4a4 deployed): \"Cần cải thiện trong phiên này: Đánh hụt 8/32 lần quan sát\" and the drill \"Đánh hụt khi đánh bạt: 101/127 lần quan sát trong các phiên gần đây\""),
    "D-16": (D18, "Retested in EV-17: Sync-UnityTests.ps1 ran in Windows PowerShell 5.1 and copied the tests"),
    "D-24": (D18, "Retested in EV-17: the Windows builds with the synchronised EditMode tests compiled (291542a)"),
    "D-25": (D18, "Retested in EV-17 on staging: the header shows \"Server API: https://tad-min.io.vn\""),
    "D-26": (D18, "Retested in EV-17 on staging: MSG64 appears in red under the incomplete expert review"),
    "D-27": (D18, "Retested in EV-17: two reports were sent from the match detail and reached the admin web; one was declined, the other accepted with a suspension"),
    "D-28": (D18, "Resolved by requirement change: Report 3 v1.5 states the implemented forfeit rule in BR-28 and FR-17; TC-F09-03 passed against it in EV-17"),
    "D-29": (D18, "Retested in EV-17: after the host closed the party the guests kept the summary, then returned to the room or to the menu normally"),
    "D-30": (D18, "Resolved by requirement change: Report 3 v1.5 BR-32 states that custom room matches give no XP or Tokenpad"),
    "D-23": (D25, "Retested in EV-19: run 103 skipped the FastAPI and admin web checks and its deploy job succeeded; the same in the other passing runs since 18/11/2026"),
    "D-14": (D25, "Retested in EV-20: after the opponent left, the dedicated server stopped while the player read the outcome and the summary; the summary stayed and the room was reached normally"),
}


def defect_status(did):
    if did in DEFECT_CLOSED:
        return "Closed"
    if did in DEFECT_STATE:
        return DEFECT_STATE[did][0]
    return "Fixed" if did in DEFECT_FIX else "Open"


def defect_open(did):
    """True while the defect still needs work (Open or Partly fixed)."""
    return defect_status(did) in ("Open", "Partly fixed")

# ------------------------------------------------------------------ Unit test decision tables
# Each function: code, class, function, requirement, loc, conditions [(group, value)], confirms [(group, value)],
# cases [(utcid label, type N/A/B, [condition indexes], [confirm indexes], test name)]
UNIT = [
    dict(code="UT-01", cls="TrainingEngine", fn="Complete(player, attempt)", req="FR-08, BR-18", loc=138, file="Progression/TrainingModels.cs",
         cond=[("Precondition", "Serve drill: 8 shots, target 6"), ("Successes", "6"), ("Successes", "7"), ("Successes", "8"), ("Successes", "< 6 (failed attempt)"), ("Mean timing", "fast (≤ 0.16 s)"), ("Mean timing", "0.30 s")],
         conf=[("Return (stars)", "0"), ("Return (stars)", "1"), ("Return (stars)", "2"), ("Return (stars)", "3"), ("Return (stars)", "< 3"), ("State", "No currency granted, errors logged")],
         cases=[("N", [0, 1, 5], [1], "StarsUseAccuracyAndTiming(6,1)"), ("N", [0, 2, 5], [2], "StarsUseAccuracyAndTiming(7,2)"), ("B", [0, 3, 5], [3], "StarsUseAccuracyAndTiming(8,3)"),
                ("B", [0, 3, 6], [4], "SlowTimingCannotReceiveThreeStars"), ("A", [0, 4], [0, 5], "FailedDrillRecordsErrorsAndReceivesNoCurrency")]),
    dict(code="UT-02", cls="EloPolicy", fn="StandardDelta(own, opponent, won)", req="FR-18, BR-23", loc=102, file="Progression/EloPolicy.cs",
         cond=[("own", "400"), ("own", "100"), ("opponent", "400"), ("opponent", "360"), ("opponent", "100"), ("won", "true"), ("won", "false")],
         conf=[("Return", "+16"), ("Return", "−16"), ("Return", "0 (floor 100)"), ("Return", "−18")],
         cases=[("N", [0, 2, 5], [0], "StandardElo(400,400,true,16)"), ("N", [0, 2, 6], [1], "StandardElo(400,400,false,-16)"), ("B", [1, 4, 6], [2], "StandardElo(100,100,false,0)"), ("N", [0, 3, 6], [3], "StandardElo(400,360,false,-18)")]),
    dict(code="UT-03", cls="EloPolicy", fn="Rank(elo)", req="FR-26, BR-27", loc=102, file="Progression/EloPolicy.cs",
         cond=[("elo", v) for v in ["100", "499", "500", "999", "1000", "1999", "2000", "3499", "3500", "4999", "5000", "6999", "7000"]],
         conf=[("Return", v) for v in ["Đồng", "Bạc", "Vàng", "Bạch Kim", "Kim Cương", "Cao Thủ", "Huyền Thoại"]],
         cases=[("B" if i not in (0,) else "N", [i], [r], "RankBoundaries(%s)" % v) for i, (v, r) in enumerate(zip(["100", "499", "500", "999", "1000", "1999", "2000", "3499", "3500", "4999", "5000", "6999", "7000"], [0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6]))]),
    dict(code="UT-04", cls="EloPolicy", fn="PairingKeys(elo, losses, expanded)", req="FR-14, BR-24", loc=102, file="Progression/EloPolicy.cs",
         cond=[("Pair", "200 vs 240, no losses, not expanded"), ("Pair", "200 vs 240 (5 losses), not expanded"), ("Pair", "200 (5 losses) vs 240, not expanded"), ("Pair", "200 vs 320, expanded"), ("Pair", "200 vs 321, expanded"), ("Pair", "100 (5 losses) vs 100")],
         conf=[("Return (pair fits)", "true"), ("Return (pair fits)", "false")],
         cases=[("N", [0], [1], "MutualPairKeysEnforceBothPreferencesAndWindow #1"), ("N", [1], [0], "… #2"), ("N", [2], [1], "… #3"), ("B", [3], [0], "… #4"), ("B", [4], [1], "… #5"), ("B", [5], [0], "… #6")]),
    dict(code="UT-05", cls="EloPolicy", fn="BotAccuracy(elo) / BotElo(own, losses)", req="FR-14, BR-26", loc=102, file="Progression/EloPolicy.cs",
         cond=[("elo", "0"), ("elo", "100"), ("elo", "220"), ("elo", "9000"), ("elo", "9100"), ("elo", "100000"), ("BotElo input", "own 100, losses 4"), ("BotElo input", "own 100, losses 5")],
         conf=[("Return (accuracy %)", "25"), ("Return (accuracy %)", "26"), ("Return (accuracy %)", "99"), ("Return (accuracy %)", "100"), ("Return (bot Elo)", "220"), ("Return (bot Elo)", "0")],
         cases=[("A", [0], [0], "AccuracyThresholds(0,25)"), ("B", [1], [0], "AccuracyThresholds(100,25)"), ("N", [2], [1], "AccuracyThresholds(220,26)"), ("N", [3], [2], "AccuracyThresholds(9000,99)"),
                ("B", [4], [3], "AccuracyThresholds(9100,100)"), ("A", [5], [3], "AccuracyThresholds(100000,100)"), ("N", [6], [4], "BotRatingAndSeededDecisions"), ("B", [7], [5], "BotRatingAndSeededDecisions")]),
    dict(code="UT-06", cls="SinglesRules", fn="CommitRally() / NextGame()", req="FR-12, BR-10, BR-15", loc=170, file="LocalMatch/Rules/SinglesRules.cs",
         cond=[("Score", "11–10 in a game"), ("Score", "12–10 in a game"), ("Games", "one game won"), ("Deciding game", "a side reaches 6"), ("Ball", "lands on the baseline or sideline")],
         conf=[("Return", "game continues"), ("Return", "game won"), ("Return", "match not over"), ("State", "ends changed once"), ("Return", "ball in")],
         cases=[("B", [0], [0], "ElevenTenDoesNotWinAndTwelveTenDoes"), ("B", [1], [1], "ElevenTenDoesNotWinAndTwelveTenDoes"), ("N", [2], [2], "MatchRequiresTwoGameWins"),
                ("N", [3], [3], "DecidingGameChangesEndsAtSixOnlyOnce"), ("B", [4], [4], "BaselineAndSidelineAreIn")]),
    dict(code="UT-07", cls="ShotInput", fn="Tick(mask, cancel, serve, out intent)", req="FR-11, BR-17", loc=142, file="LocalMatch/ShotInput.cs",
         cond=[("Keys released", "J"), ("Keys released", "K"), ("Keys released", "L"), ("Keys released", "J+K"), ("Keys released", "J+L"), ("Keys released", "K+L"), ("Keys", "J+K+L held"), ("Event", "Backspace or focus lost while holding")],
         conf=[("Return (shot)", "Drive"), ("Return (shot)", "Dink"), ("Return (shot)", "Volley"), ("Return (shot)", "Drop"), ("Return (shot)", "Smash"), ("Return (shot)", "Reset"), ("Return", "no swing")],
         cases=[("N", [0], [0], "BaseAndComboCommitOnRelease(1)"), ("N", [1], [1], "BaseAndComboCommitOnRelease(2)"), ("N", [2], [2], "BaseAndComboCommitOnRelease(4)"),
                ("N", [3], [3], "BaseAndComboCommitOnRelease(3)"), ("N", [4], [4], "BaseAndComboCommitOnRelease(5)"), ("N", [5], [5], "BaseAndComboCommitOnRelease(6)"),
                ("A", [6], [6], "ThreeKeysCancelWithoutAnySwing"), ("A", [7], [6], "BackspaceOrLostFocusCannotFireOnRelease")]),
    dict(code="UT-08", cls="MatchReadyPolicy", fn="Evaluate(roster, decisions, expired, allConnected)", req="FR-14, BR-25", loc=28, file="Entites/Multiplayer/MatchReadyPolicy.cs",
         cond=[("Decisions", "all humans accepted and connected"), ("Decisions", "one player declined"), ("Deadline", "passed before the last acceptance"), ("Roster", "invalid"), ("Roster", "acceptance from another roster"), ("Bot fallback", "human has not confirmed")],
         conf=[("Return", "Approved"), ("Return", "Rejected"), ("Return", "Pending / not approved"), ("State", "only accepting players re-queue")],
         cases=[("N", [0], [0], "EveryHumanMustAcceptAndConnect"), ("A", [1], [1, 3], "AnyDeclineCancelsWholeMatchButOnlyAcceptingPlayersRequeue"), ("B", [2], [2], "ExpiredDeadlineCannotApproveEvenWithLateAcceptances"),
                ("A", [3], [2], "InvalidRosterCannotStart"), ("A", [4], [2], "AcceptanceFromDifferentRosterCannotApprove"), ("N", [5], [2], "BotFallbackStillNeedsHumanConfirmation")]),
    dict(code="UT-09", cls="LocalPlayerEconomy", fn="PurchaseAsync(itemId, currency, requestId)", req="FR-22, BR-36", loc=162, file="Shop/PlayerEconomy.cs",
         cond=[("Balance", "below the price"), ("Currency", "Tokenpad for GEMSTONE"), ("Ownership", "item already owned"), ("Item", "unknown item or currency"), ("Save", "persistence fails")],
         conf=[("Return", "PurchaseResult not successful (status NotAllowed / insufficient)"), ("State", "wallet and inventory unchanged")],
         cases=[("A", [0], [0, 1], "InsufficientFundsLeaveStateUntouched"), ("A", [1], [0, 1], "GemstoneRejectsTokenpad"), ("A", [2], [0, 1], "OwnedItemCannotBeBoughtWithOtherCurrency"),
                ("A", [3], [0, 1], "InvalidItemAndCurrencyCannotSpend"), ("A", [4], [0, 1], "FailedSaveDoesNotMutateMemoryState")]),
    dict(code="UT-10", cls="OfflineTrainingStore", fn="Apply receipts / Load", req="FR-09, BR-21", loc=219, file="Progression/OfflineTrainingStore.cs",
         cond=[("Receipt", "APPLIED or DUPLICATE"), ("Epoch", "server reset epoch is newer"), ("File", "primary file corrupt"), ("Response", "delayed and older than the snapshot"), ("Owner", "different player")],
         conf=[("State", "outbox entry removed once"), ("State", "old progress not overlaid"), ("Return", "last valid backup loaded"), ("State", "newer snapshot kept"), ("Return", "data rejected")],
         cases=[("N", [0], [0], "AcceptedOrDuplicateReceiptsRemoveTheOutboxOnce"), ("B", [1], [1], "ANewResetEpochDoesNotOverlayOldProgress"), ("A", [2], [2], "CorruptPrimaryFallsBackToTheLastValidBackup"),
                ("A", [3], [3], "DelayedSyncResponseCannotOverwriteANewerSnapshot"), ("A", [4], [4], "OwnerMismatchCannotRemoveQueuedProgress")]),
]


# ------------------------------------------------------------------ Results of 13/11/2026 (EV-08, EV-09)
# The CI validate job runs twelve IntegrationChecks groups against PostgreSQL and Redis. A case is marked Passed only when the
# assertions of a check cover its expected result; otherwise the case stays Pending and the note says what the check covers.
CI_TESTER = "GitHub Actions (CI)"
IC = "IntegrationChecks --check-%s (CI validate job)"
CI_PASSED = {
    "TC-F04-03": ("content", "ten passed lessons grant THOT exactly once; level 24 grants nothing and level 25 grants GEMSTONE; repeated reads add nothing and both milestone receipts are stored.",
                  "Automated: the check seeds a player, records 10 passed lessons, sets level 24 and then 25 in the database and reads the game state after each change and once more."),
    "TC-F18-01": ("analytics", "daily players, D1/D7/D30 retention, popular lessons, error heat map and matchmaking health equal the seeded values, including the Vietnam day boundary.",
                  "Automated: the check seeds logins, sessions, lessons, errors and queue events, then requests the analytics report for the seeded dates from the service used by the admin web."),
    "TC-X01-01": ("expert-review", "the review is stored with the authenticated reviewer, version 1 and its shot labels; a player cannot confirm labels and a stale version is refused. A fixture session is used, not a real labelled session (risk 8).",
                  "Automated: the check opens a fixture training session, saves a review with level, shot labels, data group and basis, and reads it back."),
    "TC-S01-01": ("moderation", "after the suspension the old access and refresh tokens are rejected and a new sign-in receives only the appeals scope.", None),
    "TC-S01-02": ("moderation", "six minutes after a five-minute suspension the sign-in returns a game session again and the sanction row is kept. The expiry is evaluated at sign-in; the background expiry worker is not exercised.", None),
    "TC-S04-03": ("training-sync", "the server applies a queued offline attempt (receipt APPLIED), answers DUPLICATE to a repeat and applies a parallel submission from two devices once.", None),
    "TC-S08-01": ("dedicated", "the dedicated server completes a singles match; the result, the reward and one rating-ledger row per player are stored once.", None),
    "TC-S08-02": ("dedicated", "an admin stop ends the playing match as interrupted, writes no Elo ledger and blocks a late normal result.", None),
    "TC-S08-03": ("dedicated", "two simultaneous completions of the same match store one result (150 Tokenpad, one win).", None),
    "TC-S10-01": ("content", "a new lesson is stored as DRAFT and is absent from the player catalogue; after publishing it appears there.", None),
    "TC-S10-02": ("content", "unpublishing sets UNPUBLISHED and a new attempt of the lesson is refused, while an attempt already in progress finishes on its snapshot.", None),
    "TC-S12-01": ("moderation", "accepting the appeal revokes only the linked sanction; the decision cannot be changed afterwards.", None),
    "TC-S12-03": ("moderation", "a second appeal for the same sanction is refused with HTTP 409 and a concurrent retry creates one case.", None),
    "TC-S13-02": ("hybrid", "with the AI service failing, the job stops at ten attempts in state failed and the cached coaching is kept.", None),
}
CI_PARTLY = {
    "TC-F14-02": "the server applies each offline attempt once and returns DUPLICATE for a repeat (--check-training-sync); the client banner and screens were not checked.",
    "TC-F15-02": "a restricted (appeals) session is refused with HTTP 403 on unmarked endpoints (--check-moderation) and a player cannot run admin operations at service level; the call to /api/Admin/users with a Player token was not made.",
    "TC-F15-03": "preview, group selection, audit record and the refusal during an active match pass (--check-progress-reset); the refusal during a ranked search and the admin web were not checked.",
    "TC-F16-01": "a published lesson enters the player catalogue and a draft does not (--check-content); the admin web and the client restart were not checked.",
    "TC-F16-03": "a save with a stale version is rejected with HTTP 409 at service level (--check-content); the admin web message MSG60 was not checked.",
    "TC-F17-01": "accepting a report stores the decision, the reviewer and the sanction in one transaction (--check-moderation); sending the report from the game and the CHECKING step were not checked.",
    "TC-F17-02": "a stop command ends the dedicated match as interrupted without an Elo ledger (--check-dedicated); the admin web and the two clients were not checked.",
    "TC-F17-03": "one appeal per sanction and a second appeal refused with HTTP 409 (--check-moderation); the 5-character validation and the game screen were not checked.",
    "TC-F18-03": "the summary totals are computed correctly from seeded data (--check-analytics); the dashboard screen was not checked.",
    "TC-X01-02": "a review without the evidence confirmation is refused (--check-expert-review); the empty-label case and MSG64 were not checked.",
    "TC-S01-03": "an accepted appeal revokes its sanction and a revocation restores the game sign-in (--check-moderation), but not as one BANNED → ACTIVE transition.",
    "TC-S09-03": "a match whose server heartbeat stops is ended as interrupted (--check-dedicated); the release of the lease is not asserted.",
    "TC-S10-03": "a save with a stale version is rejected (--check-content); a stale publish is not asserted.",
    "TC-S11-02": "a report is accepted with a sanction from its initial state (--check-moderation); the CHECKING source state is not used.",
    "TC-N11": "exactly-once synchronisation holds for single, repeated and parallel submissions (--check-training-sync); the 20-attempt run of the method was not executed.",
    "TC-N12": "the layering rule is checked automatically (--check-api-contract: no EF Core, PostgreSQL or Redis dependency in Services, no repository types in API) and the automated suites pass (EV-06, EV-08); the review of e-mail exposure in the API responses was not done.",
}
_BY_ID = {c["id"]: c for c in CASES}
for _cid, (_chk, _what, _steps) in CI_PASSED.items():
    _c = _BY_ID[_cid]
    assert _c["status"] == "Pending", _cid
    _c.update(status="Passed", date=D13, tester=CI_TESTER, auto=IC % _chk, note="EV-08: " + _what)
    if _steps:
        _c.update(level="Integration", steps=_steps, pre="Disposable PostgreSQL 16 database created from Database.sql; Redis 7.4")
for _cid, _what in CI_PARTLY.items():
    _c = _BY_ID[_cid]
    assert _c["status"] == "Pending", _cid
    _base = _c["note"] if _cid[:4] in ("TC-F", "TC-X") else ("Not run as specified." if _cid.startswith("TC-N") else "Not passed yet.")
    _c["note"] = _base.rstrip() + " Partly covered by EV-08: " + _what
_BY_ID["TC-N09"].update(status="Failed", date=D13, tester=HUY,
                        note="EV-09 (13/11/2026). First check at 11:30: port 8080 still answered over plain HTTP and appsettings.json held the development credentials, so the case "
                             "failed. Re-check at 16:21 after commit be556a1 was deployed: the API and the admin web answer only over HTTPS (Let's Encrypt, TLS 1.3), port 8080 is closed and the "
                             "settings files on main @ d9200cc contain no credential (D-18 fixed). The removed values are still readable in the Git history and their rotation is not "
                             "confirmed (D-17 partly fixed), so the case stays failed. The remaining checklist items and the negative API tests were not completed.")


# ------------------------------------------------------------------ Results of 17–18/11/2026 (EV-15)
# End-to-end run on the Windows build of main @ 96ff8e9 and the admin web. The status is the latest result; the note keeps the earlier one.
E = "EV-15 (%s): "
E2E = {
    "TC-F01-05": ("Passed", D17, E % "17/11" + "\"ab\" → MSG19, the name of another player → MSG20, \"Jahwi01\" saved with \"Đã lưu tên người chơi\" and shown in the menu; the name was changed back afterwards. Run with a test account instead of the seed accounts."),
    "TC-F07-01": ("Passed", D18, E % "18/11" + "search 07:42:06, offer 07:45:07 (3 min 01 s) with the 10-second ready check, match on the dedicated server against \"player3ec8bb2dbe02 (Bot)\" from 07:45:09. The bot Elo is not shown in the client. Round 1 (EV-02, 10/11/2026) failed: offer after about 160 s (D-15, now closed)."),
    "TC-F08-01": ("Passed", D17, E % "17/11" + "room 1v1 with code 679E7858; the second player joined by code; ready check 10 s; match on the dedicated server (admin: PLAYING · dedicated, both connected); stopped by the admin for TC-F17-02, so the result is \"Gián đoạn\", +0 Elo. Three player windows on one PC were used instead of two devices."),
    "TC-F09-01": ("Passed", D17, E % "17/11" + "Ghost and Jahwi searched at 21:24:43 and were matched with each other; the match was played on the dedicated server to 2 : 0 (11–0, 11–0); histories show \"Thắng · 2 : 0 ván · +16 Elo · Xem replay\" and \"Thua · 0 : 2 ván · +0 Elo\" (floor 100). Round 1 (EV-04, 09/11/2026) failed: both attempts \"Gián đoạn\" (D-09, now closed). After the match both lobbies still show \"Mất kết nối Photon.\" (D-14)."),
    "TC-F09-03": ("Failed", D18, E % "18/11" + "the player process was suspended (no frames, no network). 20 s: the admin kept \"Đã kết nối\" and the player continued in the same match with the server score – passed. 65 s: the match ended 41 s after the suspension and the client showed MSG43, but the result is a forfeit loss \"Thua · Bỏ trận\" and the opponent won, not an interrupted match (D-28). Tokenpad unchanged; the level bar rose by 5 XP for an unknown reason (open question in EV-15)."),
    "TC-F11-04": ("Pending", "", "Not run: the test was added on 17/11/2026 (b08f5c7), after the last EditMode run EV-12, and the EditMode assemblies on main @ 96ff8e9 do not compile (D-24). The same screen was checked by hand in EV-15 (17/11): the score of each game for both teams and every player name, with the change of ends."),
    "TC-F12-01": ("Passed", D17, E % "17/11" + "A: friend drawer → TÌM BẠN → KẾT BẠN on Ghost (\"Đã gửi lời mời kết bạn.\"); B: ĐỒNG Ý; both lists show each other with XÓA BẠN and NHẮN TIN. Searched by name, not by ID."),
    "TC-F14-02": ("Pending", "", "Not run as written (two drills). EV-15: one offline drill on 17/11 (7/8, 2 stars, \"1 mục chờ đồng bộ\") was applied once after the next sign-in on 18/11 (Progress: 1/15 passed, 2/3 stars, 88 %; banner gone); the API training history was not opened. Partly covered by EV-08: the server applies each offline attempt once and returns DUPLICATE for a repeat (--check-training-sync)."),
    "TC-F15-01": ("Passed", D17, E % "17/11" + "the user signed in with the Admin account; the dashboard opened; search by e-mail and the status filters return the matching rows with role, status, Elo, play time and creation date (GET /api/Admin/users). The page header shows a wrong API address (D-25)."),
    "TC-F15-03": ("Passed", D18, E % "18/11" + "preview while the player was searching showed the current values and MSG58 with the confirm button disabled; after the match the reset of the Học tập group only returned \"Đã đặt lại các nhóm tiến trình đã chọn. Nhật ký thưởng được giữ nguyên.\" and a new preview showed level 1 with the wallet unchanged. The audit record was not opened. The Ranking group text says \"Elo về 2000\" (D-08)."),
    "TC-F16-01": ("Passed", D18, E % "17–18/11" + "draft with \"[E2E 17/11]\" appended to the coach text of \"Giao bóng đúng ô\" saved (version 2), published on 18/11 (\"Đã xuất bản bài học.\", version 3); after a client restart the lesson card shows the new text. The original text was published again afterwards (version 5)."),
    "TC-F16-03": ("Passed", D17, E % "17/11" + "two tabs of the same lesson; tab 1 saved (version 2); tab 2 saved without reloading → PUT 409 and MSG60 \"Nội dung đã thay đổi. Hãy tải lại trước khi lưu.\"; nothing overwritten. One admin account in two tabs was used instead of two administrators."),
    "TC-F17-01": ("Failed", D17, E % "17/11" + "after the ranked match the loser opened Lịch sử → match detail → \"!\" next to the opponent and sent a reason; the API refused it and the dialog showed \"The JSON value could not be converted to System.Guid. Path: $.matchId\" (D-27). No report was created, so the admin steps could not be run. Partly covered by EV-08: accepting a report stores the decision, the reviewer and the sanction in one transaction (--check-moderation)."),
    "TC-F17-02": ("Passed", D17, E % "17/11" + "Giám sát trận đấu showed the live room match (score, both connected); Dừng trận có lý do showed MSG61; the stop was confirmed by the dedicated server 6 s later; both clients left the court and both histories show \"Gián đoạn · 0 : 0 ván · +0 Elo\". The clients only show \"Mất kết nối Photon.\" instead of saying that an administrator stopped the match (D-14)."),
    "TC-F17-03": ("Passed", D18, E % "18/11" + "the banned test account saw \"Quyết định và khiếu nại\" at sign-in; a 5-character statement gave \"Vui lòng trình bày ít nhất 10 ký tự.\" (MSG53 wording differs) and nothing was sent; a valid statement created one appeal \"Đang chờ\" and the send box disappeared, so a second appeal is not possible."),
    "TC-F18-02": ("Passed", D17, E % "17/11" + "From 01/01/2025, To 11/11/2026 → \"Biểu đồ hỗ trợ tối đa 366 ngày mỗi lần xem.\" (MSG62) under the dates and no report data loaded. The range is checked by the API (GET …/analytics/report → 400), so a request is sent; the expected result \"no request is executed\" was too strict and was corrected on 18/11/2026 (FR-32 does not require a check in the browser).", dict(expected="MSG62 under the dates; no report data is loaded.")),
    "TC-F18-03": ("Passed", D17, E % "17/11" + "dashboard after sign-in: total users, daily active users, matches, online time (4.5 h) and pending reports (0) shown. Also covered by EV-08 (--check-analytics)."),
    "TC-F19-05": ("Failed", D17, E % "17/11" + "Chọn MP4 in the Windows build shows \"Tải video hiện khả dụng trong Unity Editor.\" – no video can be chosen (D-22 confirmed). The rest of the flow was run with a text post: audience Bạn bè, the friend saw it, liked and commented, the author saw \"Thích · 1\" and \"Bình luận · 1\", and a non-friend did not see it. The test post was deleted on 18/11."),
    "TC-F19-06": ("Passed", D17, E % "17/11" + "A → NHẮN TIN to friend B; B's inbox showed the thread with \"1 chưa đọc\", which disappeared after opening it; A → THÌ THẦM to C (not a friend, in the lobby); C received the whisper at once. The unread count is shown only inside the inbox, not on the menu."),
    "TC-X01-02": ("Failed", D17, E % "17/11" + "with no label and no confirmation the button Lưu đánh giá chuyên gia stays disabled, so nothing is saved, but MSG64 is not shown (D-26, Low). Partly covered by EV-08: a review without the evidence confirmation is refused (--check-expert-review)."),
    "TC-W01": ("Pending", "", "Not run end to end: registration and the tutorial need a test mailbox (PB-04). Steps 3–4 were run in EV-15: an offline serve drill on 17/11 was applied once after signing in on 18/11 (see TC-F14-02)."),
    "TC-W04": ("Failed", D18, E % "17–18/11" + "step 1 failed (D-27: the report cannot be sent from the game), so step 2 was replaced by a ban from Quản lý người dùng. Steps 3–6 passed: the banned player appealed, the admin set the appeal to CHECKING and accepted it, the ban shows \"Đã thu hồi\" and the account is ACTIVE; the lesson draft was published and the restarted client showed the new text. Decisions are kept in Lịch sử quyết định xử lý."),
    "TC-S01-03": ("Passed", D18, E % "18/11" + "the ban of 17/11 was revoked by accepting the appeal; Quản lý người dùng shows the account ACTIVE and the decision history keeps the ban with the revocation reason. A new game sign-in after the revocation was not tried. Also covered by EV-08 (--check-moderation)."),
    "TC-S06-01": ("Passed", D17, E % "17/11" + "two human players searched ranked 1v1 at the same time and were matched with each other within about 30 s (see TC-F09-01)."),
    "TC-S07-02": ("Failed", D18, E % "18/11" + "after 65 s without the client the match left the active list and the client showed MSG43, but the match is stored as a forfeit loss, not as interrupted (D-28; see TC-F09-03)."),
    "TC-S10-03": ("Passed", D17, E % "17/11" + "the stale tab of TC-F16-03 tried Xuất bản bản đã lưu → POST …/publish 409 and MSG60; the published version stayed unchanged."),
    "TC-S11-01": ("Pending", "", "Not run for a player report: no report could be created from the game (D-27). The same PENDING → CHECKING step was run for an appeal in EV-15 (18/11)."),
    "TC-S14-01": ("Passed", D17, E % "17/11" + "friend request accepted; both friend lists show each other (see TC-F12-01)."),
    "TC-S14-02": ("Passed", D17, E % "17/11" + "C declined the request of A; C's list stayed empty and A saw KẾT BẠN again."),
    "TC-N10": ("Pending", "", "Not run on Android. Windows 11: the build of main @ 96ff8e9 started, the user signed in, a drill and online matches were played in EV-15 (17–18/11/2026); Windows 10 was not tried. EV-10: Android APKs were built on 12/11 and 14/11/2026 but use protocol 18 and were never installed on a phone (Report 1, LI-11)."),
}
for _cid, _v in E2E.items():
    _c = _BY_ID[_cid]
    _c.update(status=_v[0], date=_v[1], tester=HUY if _v[0] != "Pending" else "", note=_v[2])
    if len(_v) > 3:
        _c.update(**_v[3])
_BY_ID["TC-W03"]["note"] += " Human-versus-human variant passed on 17/11/2026 in EV-15 (TC-F09-01)."
_BY_ID["TC-F07-04"]["note"] = ("EV-11 (run 52). System part in EV-16 (18/11/2026): a solo player searched ranked doubles in the Windows build; after 3 min 02 s one bot partner and two "
                               "bot opponents were offered with \"3 / 4 ĐÃ XÁC NHẬN\", the match ran on the dedicated server and the human lost 11 Elo (137 → 126, opponents "
                               "rated 257 = own + 120), the bots were not rated. The four-human variant was not run.")
_BY_ID["TC-S12-01"]["note"] += " Also seen on the admin web in EV-15 (18/11/2026): the accepted appeal shows the ban as \"Đã thu hồi\"."
E17 = "EV-17 (18/11): "
EV17 = {
    "TC-F06-03": ("Passed", E17 + "after a failed offline lesson the result shows \"HLV • Đăng nhập để xem phân tích\" and the details say that the analysis needs a connection and follows the synchronisation (D-02 fixed)."),
    "TC-F08-04": ("Passed", E17 + "room 07D62BC5: B and C joined, C changed team, 2 : 0 for A on the dedicated server; A pressed VỀ MENU first and both guests kept the summary (\"Chủ phòng đã đóng nhóm.\"); B returned to the doubles lobby with \"Trận đã kết thúc.\" and C to the menu (D-29 fixed). The EV-16 failure is kept in Report 5 v1.6."),
    "TC-F09-03": ("Passed", E17 + "case 1 as in EV-15 (reconnected within 20 s). Case 2 against BR-28 v1.5: huy was suspended for 65 s; MSG43 in the lobby; history \"Thua · Bỏ trận · −17 Elo\" (126 → 109); Tokenpad 1,050 and XP 22/50 unchanged; the opponent received +17 Elo and 10 XP. The +5 XP seen in EV-15 did not recur."),
    "TC-S07-02": ("Passed", E17 + "after 65 s without the client the match ended with a forfeit loss for that player and a win for the opponent (see TC-F09-03), as BR-28 v1.5 requires."),
    "TC-F17-01": ("Passed", E17 + "the reports \"huy → Jahwi\" and \"Ghost → Jahwi\" were sent from Lịch sử → match detail → \"!\" (\"Đã gửi tố cáo. Ban quản trị sẽ xem xét.\") and appeared in Xử lý vi phạm; the second went PENDING → CHECKING → ACCEPTED with the reason saved (D-27 fixed)."),
    "TC-F19-05": ("Passed", E17 + "Windows build: Chọn MP4 opened the Windows dialog \"Chọn video MP4\"; a 0.2 MB clip uploaded, the post (Chỉ mình tôi) showed Xem video and the clip played; the test post was deleted (D-22 fixed for Windows). Not run on Android."),
    "TC-X01-02": ("Passed", E17 + "on staging after 0cee928: with no label and no confirmation MSG64 is shown in red under the form and Lưu đánh giá chuyên gia stays disabled (D-26 fixed)."),
    "TC-W04": ("Passed", E17 + "steps 1–4: Ghost reported Jahwi from the match detail; the admin set CHECKING, then accepted with a 30-minute suspension; Jahwi was signed out, saw the decision after signing in and appealed; the admin accepted the appeal, the suspension shows \"Đã thu hồi\" and Jahwi is ACTIVE. Steps 5–6 passed in EV-15. D-32 (old restriction text on the login screen after Làm mới) found."),
    "TC-S11-01": ("Passed", E17 + "the report of Ghost was saved as CHECKING with a reason and moved to the Đang xem xét filter."),
    "TC-S11-02": ("Passed", E17 + "the CHECKING report was accepted with Tạm ngưng until 21:29; the decision appears in Lịch sử quyết định xử lý and the player was signed out."),
}
for _cid, (_st, _note) in EV17.items():
    _c = _BY_ID[_cid]
    _c.update(status=_st, date=D18, tester=HUY, note=_note)
_BY_ID["TC-F05-01"]["note"] += " EV-17 (18/11/2026): bot match on the Windows build – \"Tự động\" label, female character, key hint below the player, card text \"Cần đăng nhập\" and the analysis button on the result row (D-03, D-05, D-06, D-07, D-10 closed); D-31 found and fixed."
_BY_ID["TC-F14-01"]["note"] += " EV-17 (18/11/2026): the offline banner no longer covers the titles (D-01 closed)."
_BY_ID["TC-F15-01"]["note"] += " EV-17: the header shows the real API origin (D-25 closed)."
_BY_ID["TC-F06-01"]["note"] += " EV-17: the summary and the recommendations now name their scope (D-11 closed)."
_BY_ID["TC-W03"]["note"] += " EV-17: after a normal ranked result the lobby shows \"Trận đã kết thúc.\" (D-14 fixed)."


E20 = "EV-20 (25/11): "
_BY_ID["TC-F11-04"].update(status="Passed", date=D25, tester=HUY, note="EV-18: the three cases pass (the test assemblies compile since 291542a). Checked by hand in EV-15 (17/11).")
_BY_ID["TC-W02"]["note"] += " " + E20 + "a bot match was played to the end on the Windows build and its analysis was shown (TC-F06-04)."
_BY_ID["TC-F08-01"]["note"] += " " + E20 + "room 1v1 by invitation, ready check, dedicated match and summary with two accounts."
_BY_ID["TC-F15-01"]["note"] += " " + E20 + "dashboard and users page after the redesign of 20–22/11/2026."
_BY_ID["TC-F18-01"]["note"] += " " + E20 + "the report loads on staging; the figure Hủy sau ghép is wrong (D-36)."


def stats():
    from collections import Counter
    by_status = Counter(c["status"] for c in CASES)
    by_level = Counter(c["level"] for c in CASES)
    auto = sum(1 for c in CASES if c["auto"] != "Manual")
    return by_status, by_level, auto
