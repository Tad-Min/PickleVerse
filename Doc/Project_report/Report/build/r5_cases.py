"""Test cases, results, defects and unit-test decision tables for Report 5.

Results are recorded only from real runs:
  EV-01  Unity EditMode run, 11/11/2026, main @ 7f8ee32, 515 tests, 515 passed (Unity CLI, Pipeline test run)
  EV-02  Playtest on the staging server, 10/11/2026 (offline training, signed-in menu, bot match, AI coaching, ranked 1v1 with bot fallback)
  EV-03  UI check of the account screens and the tutorial, 11/11/2026
  EV-04  Match history of the team test accounts: two ranked 1v1 matches on 09/11/2026 21:47 and 21:55
Every other case is Pending, with the reason in the Note column.
"""

HUY, DAT = "Nguyễn Gia Huy", "Nguyễn Văn Thành Đạt"
D10, D11, D09 = "10/11/2026", "11/11/2026", "09/11/2026"

SHEETS = [
    ("FE01 Account", "FE-01 Account and onboarding", "FR-01 to FR-05: registration, verification, sign-in, profile, settings"),
    ("FE02 Training", "FE-02 Tutorial, training and offline", "FR-06 to FR-11: tutorial, controls, lessons, drills, offline synchronisation"),
    ("FE03 Bot", "FE-03 Bot practice", "FR-12, FR-13: rules engine and bot match"),
    ("FE04 Online", "FE-04 Online matches", "FR-14 to FR-18: ranked matchmaking, rooms, dedicated play, result and Elo"),
    ("FE05 Coaching", "FE-05 AI coaching", "FR-19: coaching summary and recommendations"),
    ("FE06 Progress", "FE-06 Progress, rewards and shop", "FR-20 to FR-23: XP, missions, streaks, shop and collection"),
    ("FE07 Social", "FE-07 History, social and ranking", "FR-10, FR-24 to FR-26: progress, history, replay, friends, leaderboard"),
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
C("TC-F08-01", "FE04 Online", "FR-15", "System", "Main", "Create a private singles room and join it by code",
  "1. Player A: Mode Selection → Tạo phòng → Đấu đơn 1v1; note the room code.\n2. Player B: Online lobby → enter the code → Tham gia bằng mã.\n3. Player A starts the match.",
  "Player B appears in the roster; the match starts on the dedicated server; the result is unrated.", "Two signed-in accounts on two devices",
  note="Not run: needs two devices and accounts at the same time.")
C("TC-F08-02", "FE04 Online", "FR-15", "Unit", "Rule", "Custom room start rules",
  "Run the room service tests.", "A guest cannot start; a custom room can start without readiness; an empty or overfilled team cannot start; automatic doubles still needs four players.", "–",
  auto=EM + "MultiplayerRoomServiceTests.GuestCannotStartCustom, CustomCanStartWithoutReadiness, CustomCannotStartEmptyOrOverfilledTeam, AutomaticDoublesStillRequiresFourPlayers", note="EV-01", **PASS_UT)
C("TC-F08-03", "FE04 Online", "FR-15", "Unit", "Exception", "Blank room code never connects; codes are normalised",
  "Run the room launch request tests.", "A blank code schedules no connection; the shared code is normalised before joining.", "–",
  auto=EM + "RoomLaunchRequestTests.BlankCodeNeverSchedulesConnection, JoinNormalizesSharedCode", note="EV-01. Server message MSG39 for a wrong code is not covered.", **PASS_UT)
C("TC-F09-01", "FE04 Online", "FR-16, FR-18", "System", "Main", "Two human players complete a ranked 1v1 on the dedicated server",
  "1. Players A and B search ranked 1v1 at the same time.\n2. Both accept the ready check.\n3. Play the match to the end.\n4. Open Match History on both clients.",
  "The match ends with a winner; history shows the result, score and Elo change; a replay is available.", "Two accounts with similar Elo; dedicated server idle",
  status="Failed", date=D09, tester=HUY + ", " + DAT, note="EV-04: two attempts (21:47 and 21:55) ended \"Gián đoạn\", 0:0, +0 Elo, no replay → D-09.")
C("TC-F09-02", "FE04 Online", "FR-12, FR-16", "Unit", "Rule", "Server rules engine: games, end change and net crossing",
  "Run the multiplayer rules tests.", "Best of three ends only after two game wins; the deciding game changes ends at 6 once; the ball must cross the net before contact.", "–",
  auto=EM + "MultiplayerRulesTests.BestOfThreeOnlyEndsAfterTwoGameWins, DecidingGameChangesEndsAtSixOnceWithoutResettingScoresOrServe, BallMustCrossNetEntirelyBeforeContact", note="EV-01", **PASS_UT)
C("TC-F09-03", "FE04 Online", "FR-17", "System", "Exception", "Reconnect within the window; interruption after it",
  "1. During an online match disconnect player A for 20 s, then reconnect.\n2. In a second match disconnect player A for 60 s.",
  "Case 1: A returns to the same match with the server score. Case 2: MSG43 and the match ends interrupted without Elo or reward.", "Two accounts; dedicated server idle",
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
  "Finished match with a stored replay", note="Not run: IntegrationChecks need PostgreSQL and restored packages, unavailable on the test machine.")
C("TC-F12-01", "FE07 Social", "FR-25", "System", "Main", "Send and accept a friend request",
  "1. Player A: friend drawer → Tìm bạn → search player B by ID → send.\n2. Player B accepts.", "Both lists show each other as friends.", "Two accounts",
  note="Not run: needs two accounts at the same time.")
C("TC-F12-02", "FE07 Social", "FR-25", "Unit", "Exception", "Invalid friend additions are rejected",
  "Run the social model tests.", "Self, unknown and duplicate additions are rejected; removal is idempotent.", "–",
  auto=EM + "OfflineSocialTests.AddRejectsSelfUnknownAndDuplicatesAndRemoveIsIdempotent", note="EV-01", **PASS_UT)
C("TC-F12-03", "FE07 Social", "FR-26", "Unit", "Rule", "Leaderboard order follows Elo",
  "Run the leaderboard model test.", "The live player is listed once and a change of Elo reorders the list.", "–",
  auto=EM + "OfflineSocialTests.LivePlayerIsIncludedExactlyOnceAndEloChangesReorder", note="EV-01. Screen observed on 10/11 with two players at 100 Elo (screenshot 14).", **PASS_UT)
# ------------------------------------------------------------------ FE-08/09
C("TC-F15-01", "FE08-09 Admin", "FR-29", "System", "Main", "Admin signs in and finds an account",
  "1. Open the admin web and sign in with an Admin account.\n2. Quản lý người dùng → search by e-mail.", "Dashboard opens; the account row shows role, status, Elo and play time.",
  "Admin account on staging", note="Not run in this round.")
C("TC-F15-02", "FE08-09 Admin", "FR-29", "Integration", "Exception", "Role and scope protection of the admin API",
  "1. Call GET /api/Admin/users with a Player token.\n2. Call a game endpoint with a restricted (appeals scope) token.", "Both calls return HTTP 403.",
  "Player and restricted test tokens", note="Not run: IntegrationChecks unavailable on the test machine.")
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
C("TC-W02", "Workflows", "FR-13, FR-18, FR-19", "Acceptance", "Workflow", "W02 Play a bot match and get a tip (owner: Huy, support: Đạt)",
  "1. Play a bot match to the end.\n2. Open the analysis.\n3. Open the recommended drill.", "Result with rewards; coaching summary with at most three drills; the drill briefing opens.",
  "Signed-in player; hybrid coaching enabled", status="Passed", date=D10, tester=HUY, note="EV-02 (TC-F05-01, TC-F06-01).")
C("TC-W03", "Workflows", "FR-14, FR-16, FR-18", "Acceptance", "Workflow", "W03 Find and play an online match (owner: Đạt, support: Huy)",
  "1. Search ranked 1v1.\n2. Accept the offer.\n3. Play on the dedicated server to the result.", "The match runs on the dedicated server and both sides see the final result.",
  "Dedicated server idle", status="Passed", date=D10, tester=HUY, note="EV-02, bot-fallback variant: accept 11:03:23, match started 11:03:46, result 0:2 at 11:06:04. Human-versus-human variant is TC-F09-01 (failed). D-14 found.")
C("TC-W04", "Workflows", "FR-27, FR-28, FR-30", "Acceptance", "Workflow", "W04 Report, sanction and appeal (owner: Kiên, support: Thuận)",
  "1. Player reports an opponent.\n2. Admin accepts and suspends.\n3. Suspended player appeals.\n4. Admin accepts the appeal.", "Account becomes SUSPENDED, then ACTIVE after the accepted appeal; all decisions are audited.",
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
                                 ("PreServe → Timeout at most twice and only before the serve", EM + "SinglesMatchTests.TimeoutLimitedToTwoAndOnlyBeforeServe")]),
    ("S06", "SM-06 Matchmaking search", [("SEARCHING → MATCHED with two human players", None),
                                        ("SEARCHING → BOT with a ready check that still needs the human's confirmation", "obs"),
                                        ("MATCHED → offer declined → accepting player back to SEARCHING", EM + "MatchReadyPolicyTests.AnyDeclineCancelsWholeMatchButOnlyAcceptingPlayersRequeue")]),
    ("S07", "SM-07 Online room phase", [("searching → assigning-bot → ready → loading → playing", "obs"), ("playing → interrupted after the reconnection window", None),
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
              note="Not run: needs IntegrationChecks or seeded staging data.")

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
            "NFR-09": "Not run. Known failures already logged: D-17 (secrets in the repository) and D-18 (HTTP on staging).",
            "NFR-12": "Not run as a formal check; the project references follow the layered rule (checked by reading the .csproj files)."}
for i, (nid, how, crit) in enumerate(NFR, 1):
    C("TC-N%02d" % i, "NFR", nid, "System", "NFR", how, "Follow the verification method of %s in Report 3." % nid, crit, "Release candidate on staging and reference devices",
      note=NFR_NOTE.get(nid, "Not run: planned in It-5 / It-6."))

DEFECTS = [
    ("D-01", "Offline banner overlaps the titles of Progress, the training HUD and the lesson list", "UI", "Low", "TC-F14-01", D10, "Thuận"),
    ("D-02", "Offline: the analysis area stays at \"Đang phân tích…\" forever", "Functional", "Medium", "TC-F06-03", D10, "Đạt"),
    ("D-03", "Key-hint box of the training HUD covers the player character", "UI", "Low", "TC-F03-01", D10, "Huy"),
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
]

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
    dict(code="UT-06", cls="SinglesRules", fn="CommitRally() / NextGame() / TakeTimeout()", req="FR-12, BR-10, BR-15", loc=178, file="LocalMatch/Rules/SinglesRules.cs",
         cond=[("Score", "11–10 in a game"), ("Score", "12–10 in a game"), ("Games", "one game won"), ("Deciding game", "a side reaches 6"), ("Time-outs", "third request, or after the serve"), ("Ball", "lands on the baseline or sideline")],
         conf=[("Return", "game continues"), ("Return", "game won"), ("Return", "match not over"), ("State", "ends changed once"), ("Return", "time-out refused"), ("Return", "ball in")],
         cases=[("B", [0], [0], "ElevenTenDoesNotWinAndTwelveTenDoes"), ("B", [1], [1], "ElevenTenDoesNotWinAndTwelveTenDoes"), ("N", [2], [2], "MatchRequiresTwoGameWins"),
                ("N", [3], [3], "DecidingGameChangesEndsAtSixOnlyOnce"), ("A", [4], [4], "TimeoutLimitedToTwoAndOnlyBeforeServe"), ("B", [5], [5], "BaselineAndSidelineAreIn")]),
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


def stats():
    from collections import Counter
    by_status = Counter(c["status"] for c in CASES)
    by_level = Counter(c["level"] for c in CASES)
    auto = sum(1 for c in CASES if c["auto"] != "Manual")
    return by_status, by_level, auto
