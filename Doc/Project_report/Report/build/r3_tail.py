"""Report 3 §4–5 and appendices: NFRs, business rules, common requirements, messages, glossary, traceability."""

NFR = [
    ("NFR-01", "Usability", "A new player can learn the game without outside help.", "At least 8 of 10 first-time testers complete the 17-step tutorial in 5 minutes or less and then pass the first lesson (serve) within three attempts, without help from the team.", "Moderated usability test with 10 students who have not played PickleVerse (Report 5, TC-N01)"),
    ("NFR-02", "Performance – network", "Online play is responsive for players in Vietnam.", "Round-trip time between the client and the staging game server is at most 100 ms at the 95th percentile over 10 online matches on home broadband in Ho Chi Minh City.", "Fusion RTT statistics logged by the client (TC-N02)"),
    ("NFR-03", "Performance – frame rate", "Matches are smooth on mid-range devices.", "Average frame rate ≥ 60 FPS (1% low ≥ 45 FPS) during a bot match on a reference PC (Intel Core i5 10th generation, GTX 1650, 16 GB RAM, 1920×1080, default quality); average ≥ 30 FPS on a reference Android phone (Snapdragon 7-series, 6 GB RAM).", "Unity Profiler capture of a full bot match on each device (TC-N03)"),
    ("NFR-04", "Performance – capacity", "The Web API supports the target player population.", "With 2,000 concurrent virtual users performing a read-mostly mix (profile, missions, leaderboard, history, catalogue) the API keeps the 95th-percentile response time ≤ 500 ms and the error rate < 1% for 10 minutes. The release capacity of online matches equals the number of dedicated server slots (one in the staging deployment, LI-07 in Report 1).", "k6 load test against staging (TC-N04)"),
    ("NFR-05", "Performance – AI latency", "Coaching feedback arrives quickly.", "The AI coaching service returns the analysis of one drill or match in ≤ 1 second at the 95th percentile once the telemetry has been received; the coaching popup shows the result within 5 seconds of the end of the drill when online.", "Timing of 50 analysis requests from the worker log (TC-N05)"),
    ("NFR-06", "Accuracy – AI quality", "Coaching findings are trustworthy.", "Each rule-based finding reaches precision ≥ 0.80 and recall ≥ 0.70 on the expert-labelled holdout sessions. The tier model is shown to players only when it is approved: real expert data, at least 10 holdout players per tier, macro-F1 ≥ 0.70 and at least 0.05 above the majority baseline, prediction confidence ≥ 0.65 and at least 20 shot opportunities.", "Calibration and training reports of the AI service (TC-N06)"),
    ("NFR-07", "Reliability – availability", "Online services are available when players want to play.", "The API health endpoint /health/ready is available ≥ 99.5% of the time measured every 5 minutes by an external probe over the 14 days before the final defense (at most 100 minutes of downtime).", "Uptime probe report (TC-N07)"),
    ("NFR-08", "Reliability – data integrity and recovery", "No result or reward is lost or duplicated.", "Every match result, training receipt and purchase is applied exactly once under retries; after a container failure the service is restored from Docker Hub images within 1 hour (MTTR) with at most 24 hours of data loss from the daily database backup. No open Critical defect is allowed at release (Critical = loss of player data or inability to sign in or play).", "Retry tests on settlement and sync APIs; restore drill (TC-N08)"),
    ("NFR-09", "Security", "Accounts and match results are protected.", "Passwords are stored with BCrypt; access tokens are signed JWTs valid for 2 hours and refresh tokens are stored as SHA-256 hashes; OTPs and dedicated-server tickets are protected with HMAC; authentication endpoints are rate-limited; every admin endpoint requires the Admin role; secrets are supplied by environment variables and never committed; production traffic uses HTTPS (TLS 1.2+); match outcomes are decided only by the dedicated server.", "Security checklist, API negative tests and secret scan (TC-N09)"),
    ("NFR-10", "Compatibility", "The game runs on the target platforms.", "The client runs on Windows 10/11 64-bit and on Android 8.0 (API 26) or later on ARM64, at 16:9 and 20:9 aspect ratios with keyboard or touch input; the admin web supports the latest Chrome and Edge at 1366×768 or larger.", "Installation and smoke test on each platform (TC-N10)"),
    ("NFR-11", "Availability offline", "Training is possible without a connection.", "After one online sign-in, all published lessons can be practised offline, and 100% of offline attempts are synchronised exactly once (no duplicate reward) when the device reconnects.", "Offline–online test with 20 queued attempts (TC-N11)"),
    ("NFR-12", "Maintainability and privacy", "The system can be changed safely and protects personal data.", "The backend keeps the layered dependency rule (API → Services → Repositories → Entities) with no reverse reference; core rule classes are covered by automated unit tests that run before release; other players can see only the display name, player ID, rank and Elo, never the e-mail address, which is visible only to administrators.", "Architecture check of project references, test run report and API response review (TC-N12)"),
]

BR = [
    # Account
    ("BR-01", "Account", "An e-mail address can be used by only one account."),
    ("BR-02", "Account", "A password has 6 to 128 characters and is stored only as a BCrypt hash."),
    ("BR-03", "Account", "A verification or reset code has six digits, is valid for 10 minutes, can be re-sent only after 60 seconds and allows at most 5 wrong attempts."),
    ("BR-04", "Account", "Only an account with a verified e-mail can sign in. System bot accounts can never sign in. A suspended or banned account can sign in only with the appeal scope."),
    ("BR-05", "Account", "An access token is valid for 2 hours and a refresh token for 14 days; a refresh token is replaced each time it is used. Resetting the password or signing out revokes the refresh tokens concerned."),
    ("BR-06", "Account", "A display name has 3 to 18 characters, contains no control characters and is unique among players."),
    ("BR-07", "Account", "The experience level (Beginner or Experienced) is chosen once, at the first sign-in."),
    ("BR-08", "Account", "The display name and character cannot be changed while the player is in a room, searching for a match or playing a match."),
    # Gameplay
    ("BR-09", "Gameplay", "The court is 13.41 m × 6.10 m; the non-volley zone extends 2.13 m from the net on each side. A ball touching a line is in."),
    ("BR-10", "Gameplay", "A singles match is the best of three games. A game is won by the first side to reach 11 points with a lead of at least 2. Only the serving side scores a point; a fault by the server gives the serve to the opponent (side-out)."),
    ("BR-11", "Gameplay", "The serve is hit underhand, with both feet behind the baseline, into the diagonal service court and beyond the non-volley zone, within 10 seconds after the server is ready. The server serves from the right court when the server's score is even and from the left when it is odd."),
    ("BR-12", "Gameplay", "Two-bounce rule: the return of serve and the third shot must bounce once before they are hit."),
    ("BR-13", "Gameplay", "A volley may not be hit while standing in the non-volley zone, and the player's momentum after a volley may not carry them into the zone."),
    ("BR-14", "Gameplay", "The rally ends with a fault of one of 17 types: serve foot, serve motion, serve timeout, short serve, serve to the wrong court, missed serve, out, ball into own court, two-bounce rule, double bounce, double hit, kitchen volley, kitchen momentum, net touch, body hit, net plane and permanent object."),
    ("BR-15", "Gameplay", "In the third game the players change ends when a side first reaches 6 points. Time-outs are not offered in release 1.0."),
    ("BR-16", "Gameplay", "A paddle contact is accepted only when the key is released within ±65 ms of the ideal contact time. A contact is Perfect when the contact error is ≤ 0.145 m and the timing error is within ±30 ms."),
    ("BR-17", "Gameplay", "Shot keys: J drive (and serve), K dink, L volley, J+K drop, J+L smash, K+L reset; U adds power and I aims cross-court. The shot is decided once, when the first base key is released."),
    # Training
    ("BR-18", "Training", "Drill score: 0 stars below the target; 1 star when the target is reached; 2 stars at ≥ 80% hits; 3 stars at ≥ 90% hits with a mean timing error ≤ 0.16 s. The best result of each lesson is kept."),
    ("BR-19", "Training", "A lesson is unlocked when its prerequisite lesson has been passed; a locked lesson cannot be started. Prerequisites cannot form a cycle."),
    ("BR-20", "Training", "The first pass of a lesson grants 150, 225, 300 or 450 Tokenpad and 5, 7, 10 or 15 XP according to the lesson group; later passes and failed attempts grant no currency and cost nothing."),
    ("BR-21", "Training", "Offline training is allowed only for a profile cached by a previous online sign-in. Each attempt carries a unique request ID and is applied at most once (receipt APPLIED, DUPLICATE or REJECTED)."),
    # Match
    ("BR-22", "Match", "Match kinds are Bot Practice, Ranked Bot, Ranked Singles, Matched Doubles and Custom. Only Ranked Singles and Ranked Bot change Elo."),
    ("BR-23", "Match", "A new player starts at 100 Elo, which is also the minimum. After a ranked match Elo changes by round(32 × (S − E)), where S is 1 for a win and 0 for a loss and E = 1 / (1 + 10^((R_opponent − R_own)/400)); the result is never below 100."),
    ("BR-24", "Match", "Ranked pairing uses a window of 120 Elo. During the first 30 seconds the window extends only upward (only downward for a player with 5 or more losses); after 30 seconds it extends 120 in both directions."),
    ("BR-25", "Match", "When a match is found, every player must accept within 10 seconds; a decline or a timeout cancels the offer."),
    ("BR-26", "Match", "Ranked singles only: if no human opponent is found within 180 seconds, a ranked bot is assigned with Elo = own + 120 (own − 100 for a player with 5 or more losses). Quick doubles has no bot fallback; its search ends when the search lease expires."),
    ("BR-27", "Match", "Rank tiers by Elo: Đồng < 500, Bạc < 1000, Vàng < 2000, Bạch Kim < 3500, Kim Cương < 5000, Cao Thủ < 7000, Huyền Thoại ≥ 7000."),
    ("BR-28", "Match", "A disconnected client tries to reconnect for 30 seconds, and the dedicated server keeps the player's slot for 30 seconds while the match is paused; a player who does not return is recorded as having forfeited and the match ends as interrupted. Clients send a heartbeat to the API every 8 seconds; in shared-mode matches (local development only) the API interrupts the match when a player has sent no heartbeat for 35 seconds. If the dedicated server stops renewing its lease, the lease monitor ends the match as interrupted."),
    ("BR-29", "Match", "A match result is saved exactly once. An interrupted or stopped match gives no Elo and no reward."),
    ("BR-30", "Match", "A singles room holds 2 players and a doubles room up to 4. The host starts a custom match and may start a doubles room before all four slots are filled; custom matches are unrated."),
    ("BR-31", "Match", "Bot levels Easy, Medium and Hard use fixed reaction, accuracy and error settings. The adaptive bot selects its tier from the player's Elo: below 500 beginner, below 2000 intermediate, otherwise advanced."),
    # Economy
    ("BR-32", "Economy", "Match rewards: bot match win 100 / loss 50 Tokenpad; online match win 150 / loss 75 Tokenpad; every finished match gives 10 XP for a win and 5 XP for a loss."),
    ("BR-33", "Economy", "Reaching level n+1 requires n × 200 XP at level n; surplus XP carries over."),
    ("BR-34", "Economy", "There are 7 daily and 10 weekly missions. Daily missions renew at 07:00 Vietnam time (UTC+7) and weekly missions at the start of each mission week. Completing all daily missions gives 200 Tokenpad + 20 XP and all weekly missions 1,000 Tokenpad + 50 XP. The daily streak grows by one for each day with at least one finished match; streaks are rewarded by the weekly mission \"3-day streak\" (350 Tokenpad + 20 XP) and the achievement \"5-day streak\" (500 Tokenpad + 30 XP)."),
    ("BR-35", "Economy", "Each mission, mission set and achievement reward can be claimed only once."),
    ("BR-36", "Economy", "A purchase requires enough balance in the chosen currency and is refused if the item is already owned; the debit and the grant are one transaction. Exactly one paddle is equipped at a time. Elite Token has no earning source in release 1.0."),
    ("BR-37", "Economy", "The THOT paddle is granted once after 10 passed lessons and the GEMSTONE paddle once at level 25, unless already owned."),
    # Social
    ("BR-38", "Social", "A player cannot send a friend request to themself, send a duplicate request, or send one to a player who has already sent them a request. Blocking removes the friendship and prevents friend requests and room invitations in both directions."),
    ("BR-39", "Social", "The leaderboard is ordered by Elo descending and is cached for 15 seconds."),
    ("BR-40", "Social", "A match history and its replay can be read only by the players who took part and by administrators."),
    # Moderation
    ("BR-41", "Moderation", "A player can report another participant of their own finished match once per match; self-reporting is not allowed; the reason has at most 254 characters."),
    ("BR-42", "Moderation", "A sanction is SUSPENDED (expiry required and in the future) or BANNED (expiry optional). The reason has 3 to 2,000 characters. An administrator cannot sanction their own account or a system account. An expired suspension ends automatically."),
    ("BR-43", "Moderation", "A restricted player can submit one appeal per sanction, with a statement of 10 to 4,000 characters. Accepting an appeal revokes only the sanction it concerns."),
    ("BR-44", "Moderation", "Reports and appeals move from PENDING to CHECKING to ACCEPTED or DECLINED; a decision requires a reason."),
    # Admin
    ("BR-45", "Administration", "Only the Admin role can use the administration application and API (otherwise HTTP 403). Every administrative change is recorded in the audit log with actor, action, target and time."),
    ("BR-46", "Administration", "Progress reset works on the groups LEARNING, RANKING, MISSIONS and ECONOMY after an impact preview, and is refused while the player is searching or in a match. It keeps the account, friends, match history, decisions and reward log; rewards already granted are not granted again."),
    ("BR-47", "Administration", "A lesson moves DRAFT → PUBLISHED → UNPUBLISHED (and can be published again). Publishing applies to new attempts only. A save is refused when the content changed after it was loaded."),
    ("BR-48", "Administration", "Stopping a live match requires a reason; when the stop is recorded before the final result the match is interrupted without Elo or reward."),
    ("BR-49", "Administration", "Analytics dates are calendar days in Vietnam time; the start date is not after the end date and a range covers at most 366 days."),
    # AI
    ("BR-50", "AI coaching", "A coaching finding is reported only when the session contains at least 5 opportunities for that error; at most 3 drills are recommended; the tier estimate is shown only under the approval conditions of NFR-06."),
    ("BR-51", "AI coaching", "Telemetry and replays of dedicated-server matches are submitted only by the dedicated server; a client can submit telemetry only for its own drills and bot matches."),
]

MSG = [
    ("MSG01", "In red, under the text box", "E-mail field is empty", "Vui lòng nhập email."),
    ("MSG02", "In red, under the text box", "E-mail format is invalid", "Vui lòng nhập địa chỉ email hợp lệ."),
    ("MSG03", "In red, under the text box", "Password field is empty", "Vui lòng nhập mật khẩu."),
    ("MSG04", "In red, under the text box", "Password shorter than 6 characters", "Mật khẩu cần ít nhất 6 ký tự."),
    ("MSG05", "In red, under the text box", "Confirmation differs from the password", "Mật khẩu xác nhận chưa khớp."),
    ("MSG06", "In red, under the form", "E-mail already registered", "Email đã được đăng ký. Nếu chưa xác thực, hãy dùng chức năng gửi lại mã."),
    ("MSG07", "In line, information", "Registration succeeded", "Đã đăng ký. Hãy nhập mã được gửi tới email để kích hoạt tài khoản."),
    ("MSG08", "In red, under the text box", "Code is not six digits", "Mã xác thực gồm 6 chữ số."),
    ("MSG09", "In line, information", "A new code was sent", "Mã xác thực đã được gửi. Hãy kiểm tra cả thư rác."),
    ("MSG10", "In red, under the form", "Code re-sent within 60 seconds (HTTP 429)", "Vui lòng chờ ít nhất 60 giây trước khi gửi lại mã."),
    ("MSG11", "In red, under the form", "Sign-in with an unverified e-mail", "Email chưa được xác thực. Hãy nhập OTP hoặc yêu cầu gửi lại mã."),
    ("MSG12", "In red, under the form", "Wrong e-mail or password", "Email hoặc mật khẩu không chính xác."),
    ("MSG13", "Toast message", "Sign-in succeeded", "Đăng nhập thành công."),
    ("MSG14", "Popup", "Sign-in of a suspended or banned account", "Tài khoản đang bị hạn chế. Bạn có thể xem quyết định và gửi khiếu nại."),
    ("MSG15", "In line, information", "Password reset requested (same text whether or not the e-mail exists)", "Nếu email thuộc tài khoản đã xác thực, mã đặt lại mật khẩu sẽ được gửi. Hãy kiểm tra cả thư rác."),
    ("MSG16", "In red, under the form", "Reset code wrong or expired", "Mã đặt lại mật khẩu không hợp lệ hoặc đã hết hạn."),
    ("MSG17", "In red, under the form", "Password rule rejected by the API", "Mật khẩu cần từ 6 đến 128 ký tự và xác nhận phải khớp."),
    ("MSG18", "Popup", "Refresh token expired or revoked", "Phiên đăng nhập không hợp lệ hoặc đã hết hạn."),
    ("MSG19", "In red, under the text box", "Display name length invalid", "Tên người chơi cần từ 3 đến 18 ký tự."),
    ("MSG20", "In red, under the text box", "Display name already used", "Tên người chơi đã được sử dụng. Hãy chọn tên khác."),
    ("MSG21", "Popup question", "First sign-in experience choice", "Bạn đã quen với pickleball chưa? – Người mới sẽ được hướng dẫn luật và điều khiển. Nếu đã thành thạo, bạn có thể vào sảnh ngay."),
    ("MSG22", "In red, under the form", "Verification e-mail could not be sent", "Chưa gửi được email xác thực. Vui lòng thử gửi lại mã sau ít nhất 60 giây."),
    ("MSG23", "In line", "Starting a locked lesson", "Bài tập chưa mở khóa."),
    ("MSG24", "Popup title", "Drill target not reached", "CHƯA ĐẠT MỤC TIÊU"),
    ("MSG25", "In line", "Drill failed", "Không mất Tokenpad. Luyện lại để đạt {target} cú."),
    ("MSG26", "HUD feedback", "Key released late", "Bạn thả nút hơi muộn. Hãy chuẩn bị vợt và thả nút sớm hơn."),
    ("MSG27", "HUD feedback", "Key released early", "Bạn thả nút hơi sớm. Hãy đợi bóng tiến gần hơn."),
    ("MSG28", "Banner", "Offline training mode", "Đang luyện tập ngoại tuyến · {n} mục chờ đồng bộ"),
    ("MSG29", "HUD banner", "Serve timer expired", "Bạn đã hết 10 giây giao bóng. Hãy chuẩn bị và giao bóng trước khi hết thời gian."),
    ("MSG30", "HUD banner", "Rally ended by a fault (examples)", "Lỗi • Bóng ra ngoài / Lỗi • Nảy hai lần / Lỗi • Vô lê trong vùng cấm vô lê / Lỗi • Bóng không qua lưới / Lỗi chân giao bóng"),
    ("MSG31", "Result title", "Player wins the match", "Bạn thắng trận!"),
    ("MSG32", "In line", "Searching for an opponent", "Đang tìm đối thủ trực tuyến…"),
    ("MSG33", "Popup", "Match found, ready check", "Đã tìm thấy trận • Xác nhận để tham gia."),
    ("MSG34", "In line", "Search expired", "Hết thời gian tìm trận. Hãy thử lại."),
    ("MSG35", "In line", "Player declined the match", "Bạn đã hủy tham gia. Bấm Tìm trận để tìm lại."),
    ("MSG36", "In line", "Dedicated server slot is busy", "Máy chủ đang bận • Đang chờ lượt thi đấu…"),
    ("MSG37", "In line", "Room is full", "Phòng đã đủ người."),
    ("MSG38", "In line", "Room closed or player removed", "Phòng đã đóng hoặc bạn đã được mời ra."),
    ("MSG39", "In line", "Joining a room failed (for example wrong code)", "Không tham gia được phòng. Vui lòng thử lại."),
    ("MSG40", "In line", "Invited player cannot receive invitations", "Người chơi chưa sẵn sàng nhận lời mời."),
    ("MSG41", "In line", "Player already searching or in an unfinished match", "Người chơi đang tìm trận hoặc có trận chưa hoàn tất."),
    ("MSG42", "Overlay", "Connection to the match server lost", "Mất kết nối máy chủ thi đấu."),
    ("MSG43", "Popup", "Reconnection failed", "Không thể kết nối lại trận. Kết quả sẽ được cập nhật khi máy chủ xử lý xong."),
    ("MSG44", "Toast message", "Player left the match", "Bạn đã rời trận."),
    ("MSG45", "Popup", "Photon Cloud not reachable", "Không kết nối được Photon. Vui lòng kiểm tra mạng và thử lại."),
    ("MSG46", "Popup", "Balance too low for a purchase", "Số dư tiền tệ không đủ để mua vật phẩm này."),
    ("MSG47", "Popup", "Item already owned", "Bạn đã sở hữu vật phẩm này rồi."),
    ("MSG48", "Popup", "Equipping an item that is not owned", "Bạn chưa sở hữu vật phẩm này."),
    ("MSG49", "Toast message", "Achievement reward claimed", "Mở khóa thành công danh hiệu và phần thưởng: {reward}!"),
    ("MSG50", "In line", "The other player already sent a request", "Người chơi đã gửi lời mời cho bạn. Hãy chấp nhận hoặc từ chối."),
    ("MSG51", "In line", "No search result", "Không tìm thấy tên hoặc ID phù hợp."),
    ("MSG52", "In line", "No player can be reported", "Không có người chơi phù hợp để tố cáo"),
    ("MSG53", "In red, under the text box", "Appeal statement too short", "Nội dung khiếu nại cần ít nhất 10 ký tự."),
    ("MSG54", "In line", "No decision to appeal", "Không có quyết định xử lý để khiếu nại."),
    ("MSG55", "In red, under the text box", "Reason length invalid (admin)", "Lý do phải có từ 3 đến {max} ký tự."),
    ("MSG56", "In red, under the text box", "Suspension without a future expiry", "Tạm ngưng cần thời hạn trong tương lai; khóa có thể không có thời hạn."),
    ("MSG57", "Toast message", "Admin tries to sanction own account", "Không thể tự khóa tài khoản quản trị."),
    ("MSG58", "In line, in the dialog", "Reset while player searching or in a match", "Người chơi đang tìm trận hoặc có trận chưa hoàn tất. Chưa thể reset."),
    ("MSG59", "In red, under the text box", "Lesson code already exists", "Mã bài học đã tồn tại."),
    ("MSG60", "Toast message", "Content changed since it was loaded", "Nội dung đã thay đổi. Hãy tải lại trước khi lưu."),
    ("MSG61", "Warning in the dialog", "Confirming a match stop", "Trận sẽ bị gián đoạn và không tính Elo/thưởng nếu lệnh dừng được ghi trước kết quả cuối."),
    ("MSG62", "In red, under the date fields", "Invalid analytics range", "Khoảng ngày thống kê không hợp lệ. / Biểu đồ hỗ trợ tối đa 366 ngày mỗi lần xem."),
    ("MSG63", "Toast message", "Non-admin calls a moderation function (HTTP 403)", "Bạn không có quyền xử lý vi phạm."),
    ("MSG64", "In red, under the form", "Expert review incomplete", "Hãy xác nhận phiên người thật, người đánh giá, bằng chứng và ít nhất một nhãn."),
    ("MSG65", "In red, under the form", "System account sign-in attempt", "Tài khoản hệ thống không được đăng nhập."),
]

TRACE = [
    ("F01", "Account and first steps", "FR-01, FR-02, FR-03, FR-04", "UC-01 to UC-08", "W01", "Trần Nguyễn Hiếu Thuận"),
    ("F02", "Rules and controls", "FR-05, FR-06, FR-11", "UC-09, UC-10", "W01", "Trần Nguyễn Hiếu Thuận"),
    ("F03", "Guided drills", "FR-07, FR-08", "UC-11, UC-12", "W01", "Nguyễn Gia Huy"),
    ("F04", "Levels and items", "FR-20, FR-23", "UC-43, UC-44", "W01", "Nguyễn Văn Thành Đạt"),
    ("F05", "Adaptive bot", "FR-12, FR-13", "UC-16", "W02", "Nguyễn Gia Huy"),
    ("F06", "Coaching and tier", "FR-19", "UC-17, UC-18", "W02", "Nguyễn Văn Thành Đạt"),
    ("F07", "Quick match", "FR-14", "UC-19, UC-20", "W03", "Nguyễn Văn Thành Đạt"),
    ("F08", "Private rooms", "FR-15", "UC-21 to UC-24", "W03", "Nguyễn Văn Thành Đạt"),
    ("F09", "Dedicated play", "FR-16, FR-17", "UC-25 to UC-27", "W03", "Nguyễn Văn Thành Đạt"),
    ("F10", "Result and Elo", "FR-18", "UC-25", "W03", "Nguyễn Văn Thành Đạt"),
    ("F11", "Progress and replay", "FR-10, FR-21, FR-24", "UC-15, UC-28, UC-29, UC-39", "W02", "Nguyễn Gia Huy"),
    ("F12", "Friends and ranks", "FR-25, FR-26", "UC-31 to UC-36", "W03", "Trần Nguyễn Hiếu Thuận"),
    ("F13", "Daily and streak rewards", "FR-21", "UC-37 to UC-40", "W01", "Nguyễn Gia Huy"),
    ("F14", "Offline practice", "FR-09", "UC-13, UC-14", "W01", "Trần Nguyễn Hiếu Thuận"),
    ("F15", "Admin and accounts", "FR-29", "UC-47 to UC-53", "W04", "Huỳnh Trung Kiên"),
    ("F16", "Lesson publishing", "FR-31", "UC-56 to UC-61", "W04", "Huỳnh Trung Kiên"),
    ("F17", "Match care and reports", "FR-27, FR-28, FR-30, FR-32", "UC-30, UC-45, UC-46, UC-54, UC-55, UC-62 to UC-64", "W04", "Huỳnh Trung Kiên"),
    ("F18", "Admin charts", "FR-32", "UC-65, UC-66", "W04", "Huỳnh Trung Kiên"),
    ("–", "Shop (not in the Review 2 list)", "FR-22", "UC-41, UC-42", "–", "–"),
    ("–", "Expert review (addition to the proposal)", "FR-33", "UC-67, UC-68", "–", "–"),
]


def write(r):
    r.h2("4. Non-Functional Requirements")
    r.h3("4.1 External Interfaces")
    r.h4("4.1.1 User Interfaces")
    r.bullets([
        "The game interface is in Vietnamese with the project's Vietnamese fonts; it scales for 16:9 (1920×1080, 1280×720) and 20:9 phone screens; every screen can be operated with mouse and keyboard on PC and with touch on Android; Esc closes overlays and opens Settings, and Enter confirms.",
        "In-match information (score, serving side, timers, fault banners) is placed at the screen edges so that it does not cover the court.",
        "The administration web application is a responsive single-page application for desktop browsers with a fixed sidebar, tables with paging and confirmation dialogs for every destructive action.",
    ])
    r.h4("4.1.2 Hardware Interfaces")
    r.bullets(["PC: keyboard and mouse; game controller input is not supported in release 1.0.",
               "Android: multi-touch screen for the virtual joystick and shot buttons.",
               "Server: Linux host with Docker; the dedicated game-server container is limited to 2 CPUs and 2 GB RAM by default."])
    r.h4("4.1.3 Software Interfaces")
    r.table(["Interface", "Description"], [
        ["Web API (REST/JSON)", "129 endpoints under /api (Report 4, §3.9), JSON bodies wrapped in the ApiResponse<T> envelope {success, message, data}; error responses carry {success, message, code, errors}; JWT bearer authentication; Swagger/OpenAPI documentation"],
        ["AI coaching service", "Internal HTTP API POST /v1/analyze-session protected by the X-AI-Key header; called only by the Web API worker"],
        ["Dedicated server ↔ Web API", "HTTP calls for lease, heartbeat, telemetry, replay and result, authenticated with a service key; match tickets signed with HMAC-SHA256"],
        ["Photon Fusion 2.1.3", "Real-time session service (protocol version 18) used by clients and the dedicated server"],
        ["PostgreSQL 16 / Redis 7.4", "Persistent storage through Npgsql Entity Framework Core; Redis for matchmaking queue, leases, presence and caches"],
        ["S3-compatible storage", "Replay manifests and chunks through the S3 API"],
    ], widths=[4.0, 12.0], size=9, caption="Software interfaces")
    r.h4("4.1.4 Communication Interfaces")
    r.bullets(["Client and admin web to Web API: HTTP/1.1 with JSON over TLS in production (port 443); the staging environment serves the API and the admin web over HTTPS on port 443 since 13/11/2026 (HTTP on port 8080 until then).",
               "Game traffic: UDP through Photon Cloud and to the dedicated server on UDP port 3669.",
               "E-mail: SMTP with STARTTLS on port 587 to Gmail.",
               "All timestamps are exchanged in UTC (ISO 8601) and displayed in Vietnam time (UTC+7)."])

    r.h3("4.2 Quality Attributes")
    r.p("Each quality attribute below is measurable and is verified by the test case named in the last column (Report 5).")
    groups = [("4.2.1 Usability", ["NFR-01"]), ("4.2.2 Reliability", ["NFR-07", "NFR-08", "NFR-11"]),
              ("4.2.3 Performance", ["NFR-02", "NFR-03", "NFR-04", "NFR-05"]), ("4.2.4 Accuracy of AI Coaching", ["NFR-06"]),
              ("4.2.5 Security", ["NFR-09"]), ("4.2.6 Compatibility", ["NFR-10"]), ("4.2.7 Maintainability and Privacy", ["NFR-12"])]
    by = {n[0]: n for n in NFR}
    for title, ids in groups:
        r.h4(title)
        r.table(["ID", "Requirement", "Measure and acceptance criterion", "Verification"],
                [[by[i][0], by[i][2], by[i][3], by[i][4]] for i in ids],
                widths=[1.5, 3.6, 7.6, 3.3], size=8.5, first_col_bold=True, caption="%s requirements" % title.split(" ", 1)[1])

    r.h2("5. Requirement Appendix")
    r.h3("5.1 Business Rules")
    r.table(["ID", "Group", "Rule Definition"], [list(b) for b in BR], widths=[1.4, 2.2, 12.4], size=8.5, first_col_bold=True, caption="Business rules")
    r.h3("5.2 Common Requirements")
    r.bullets([
        "Language: all player-facing and administrator-facing text is Vietnamese; documents and source code identifiers are English.",
        "Date and time: stored in UTC and shown in Vietnam time (dd/MM/yyyy HH:mm); daily cycles (missions, streaks, analytics days) start at 07:00 or 00:00 Vietnam time as stated in the related rule.",
        "API responses: every JSON response uses the ApiResponse envelope; HTTP 400 validation, 401 not signed in, 403 forbidden, 404 not found, 409 conflict, 429 too many requests, 503 dependency unavailable; HTTP 500 responses never expose internal details.",
        "Idempotency: commands that change progress, wallet or match state carry a client request ID; repeating a request returns the first result.",
        "Concurrency: administrative edits use a version check and ask the user to reload when the data changed.",
        "Lists: account lists are paged (20 rows per page by default in the API); the game history shows the last 20 matches.",
        "Logging: the API, workers and dedicated server write structured logs without passwords, tokens or codes.",
    ])
    r.h3("5.3 Application Messages List")
    r.table(["#", "Message code", "Message Type", "Context", "Content"],
            [[str(i + 1), a, b, c, d] for i, (a, b, c, d) in enumerate(MSG)],
            widths=[0.8, 1.7, 2.8, 4.4, 6.3], size=8.5, caption="Application messages")
    r.h3("5.4 Definitions and Acronyms")
    r.table(["Term", "Definition"], [
        ["Dink", "A soft shot that drops into the opponent's non-volley zone"],
        ["Drill", "A practice exercise with a fixed number of shots and a target"],
        ["Elo", "A rating system in which the rating change depends on the expected result against the opponent"],
        ["Kitchen / NVZ", "Non-volley zone: the area within 2.13 m of the net where volleys are not allowed"],
        ["Rally", "The exchange of shots from the serve until a fault"],
        ["Side-out", "Loss of serve to the opponent after a fault by the serving side"],
        ["Telemetry", "Shot-level data (timing, contact, landing, position) recorded by the game"],
        ["Tokenpad / Elite Token", "In-game currencies; Tokenpad is earned by playing"],
        ["Dedicated server", "A headless Unity server that runs an online match with authority over the result"],
        ["Photon Fusion", "Third-party networking library and cloud used for real-time sessions"],
        ["API, JWT, OTP, HMAC", "Application programming interface; JSON Web Token; one-time password; hash-based message authentication code"],
        ["BR, FR, NFR, UC, MSG", "Business rule; functional requirement; non-functional requirement; use case; message"],
    ], widths=[3.6, 12.4], size=9, first_col_bold=True, caption="Definitions and acronyms")
    r.h3("5.5 Appendix A – Traceability to the Review 2 Commitment")
    r.p("The table below links the 18 features committed in the Review 2 form BM04 (07/11/2026) to the use cases and functional requirements of this specification, with the workflow and the member accountable for each feature at the reviews. Report 5 links each requirement to its test cases.")
    r.table(["Review 2 feature", "Name", "Functional requirements", "Use cases", "Workflow", "Owner (BM04)"], [list(t) for t in TRACE],
            widths=[1.6, 3.4, 3.2, 3.4, 1.6, 2.8], size=8.5, caption="Traceability from Review 2 features to requirements and owners")
    r.h3("5.6 References")
    r.references([
        "USA Pickleball, \"Official Rulebook 2024.\" https://usapickleball.org/what-is-pickleball/official-rules/ (accessed 11 Nov. 2026).",
        "K. E. Wiegers and J. Beatty, Software Requirements, 3rd ed. Microsoft Press, 2013.",
        "Photon Engine, \"Fusion 2 Documentation.\" https://doc.photonengine.com/fusion/current (accessed 11 Nov. 2026).",
        "GFA26SE111, \"Report 1 – Project Introduction\" and \"Report 4 – Software Design Document,\" PickleVerse, 2026.",
    ])
