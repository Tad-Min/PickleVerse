"""Report 3 §3.2–3.11: functional requirements FR-01 to FR-33."""
import os
from report_lib import SCREENS

FIG = {}  # screenshot file -> figure number, so later FRs can refer to an earlier figure


def shot(r, name, caption, width=14.5):
    if name in FIG:
        return FIG[name]
    n = r.figure(name, caption, width_cm=width, max_height_cm=9.5)
    FIG[name] = n
    return n


def fr(r, code, title, ucs, trigger, desc, shots=(), refs=(), layout=None, fields=None, rules=(), normal=(), exceptions=()):
    r.h4("%s %s" % (code, title))
    r.label("Related use cases", ucs)
    r.label("Function trigger", trigger)
    r.label("Function description", desc)
    para = r.p("**Screen layout**", after=2)
    shown = []
    for name, cap in shots:
        if os.path.exists(os.path.join(SCREENS, name)):
            shown.append(shot(r, name, cap))
    if refs:
        r.p("This function uses the screen already shown in " + ", ".join("Figure %d" % FIG[x] for x in refs if x in FIG) + "; no separate screenshot is repeated here.")
    if layout:
        r.table(["Area", "Elements and behaviour"], layout, widths=[3.6, 12.4], size=8.5,
                caption="%s screen layout" % code)
    r.p("**Function details**", after=2)
    if fields:
        r.table(["Field", "Type", "Required", "Validation and constraints"], fields,
                widths=[3.4, 2.4, 1.6, 8.6], size=8.5, caption="%s data fields" % code)
    if rules:
        r.label("Business rules", rules)
    if normal:
        r.p("Normal flow:", bold=True, after=1)
        r.numbered(normal)
    if exceptions:
        r.p("Alternative and exception flows:", bold=True, after=1)
        r.bullets(exceptions)


def admin_layout(r, name, cap, rows):
    """Real screenshot when it has been captured, otherwise the component layout read from the React source."""
    if os.path.exists(os.path.join(SCREENS, name)):
        return [(name, cap)], None
    return [], rows


def write(r):
    # ============================================================ FE-01
    r.h3("3.2 Account and Onboarding (FE-01)")
    fr(r, "FR-01", "Register Account and Verify Email", "UC-01 Register Account, UC-02 Verify Email",
       "Login screen → link \"Đăng ký\" (Register). The verification page also opens after a successful registration, or from the link \"Bạn cần xác thực email?\".",
       "A visitor creates an account with an e-mail address and a password. The client validates the form, the Web API creates the account in the unverified state, stores a hash of a six-digit one-time password (OTP) and queues an e-mail through Gmail SMTP. The visitor enters the OTP to activate the account; after activation the account can sign in.",
       shots=[("29_register.png", "Register screen"), ("32_register_validation.png", "Register screen with an inline validation message"),
              ("30_verify_email.png", "E-mail verification screen")],
       fields=[["Email", "Text (≤254)", "Yes", "Valid e-mail format (MSG01, MSG02); not already registered (MSG06)"],
               ["Password", "Password", "Yes", "6–128 characters (MSG03, MSG04, MSG17); show/hide toggle"],
               ["Confirm password", "Password", "Yes", "Must equal Password (MSG05)"],
               ["Verification code", "Text (6 digits)", "Yes", "Exactly six digits (MSG08); valid for 10 minutes; at most 5 wrong attempts"]],
       rules="BR-01, BR-02, BR-03, BR-04.",
       normal=["The visitor enters e-mail, password and confirmation and selects **Tạo tài khoản**.",
               "The client validates the fields; the API creates an ACTIVE but unverified account with the Player role and a player profile.",
               "The API sends the OTP e-mail and the client shows MSG07 and the verification page.",
               "The visitor enters the six-digit code and selects **Xác thực**.",
               "The API checks the code, marks the e-mail as verified and the client returns to sign-in."],
       exceptions=["E1 – The e-mail is already registered: MSG06; an unverified owner can use **Gửi lại mã xác thực**.",
                   "E2 – Resend is requested within 60 seconds of the previous code: HTTP 429 and MSG10.",
                   "E3 – Wrong or expired code: the attempt is counted; after 5 wrong attempts the code is invalid and a new code must be requested.",
                   "E4 – The e-mail cannot be delivered (SMTP not available): MSG22; an administrator can verify the account manually (FR-29).",
                   "E5 – The e-mail is already verified: the client asks the visitor to sign in."])

    fr(r, "FR-02", "Sign In and Sign Out", "UC-03 Sign In, UC-05 Sign Out",
       "Login screen → **Đăng nhập**; Settings → Overview → **Đăng xuất**. Token refresh runs automatically before the access token expires.",
       "A visitor signs in with e-mail and password. The API verifies the BCrypt hash, issues a JWT access token (2 hours) and a refresh token (14 days, stored as a SHA-256 hash) and returns the profile summary. The client loads the profile, wallet, catalogue and settings on the Loading screen and opens the Menu. A restricted account receives a token limited to the appeal scope and is taken to Decisions and Appeals. Signing out revokes the refresh token on the server and clears the session on the device.",
       shots=[("01_login.png", "Login screen of the game client")],
       fields=[["Email", "Text", "Yes", "Not empty (MSG01), valid format (MSG02)"],
               ["Password", "Password", "Yes", "Not empty (MSG03)"]],
       rules="BR-04, BR-05.",
       normal=["The visitor enters e-mail and password and selects **Đăng nhập**.",
               "The API validates the credentials and returns tokens and the profile; the client shows MSG13 and the Loading screen.",
               "On the first sign-in the client asks for the experience level (FR-04); otherwise it opens the Menu.",
               "To sign out, the player opens Settings → Overview and selects **Đăng xuất**; the client returns to the Login screen."],
       exceptions=["E1 – Wrong e-mail or password: MSG12 (the message does not reveal which field is wrong).",
                   "E2 – The e-mail is not verified: MSG11 and a link to the verification page.",
                   "E3 – The account is suspended or banned: MSG14 and the Decisions and Appeals popup (FR-28).",
                   "E4 – A system bot account tries to sign in: MSG65.",
                   "E5 – The refresh token is expired or revoked: MSG18 and the Login screen.",
                   "A1 – No network and a cached profile exists: the visitor can select **Luyện tập ngoại tuyến** (FR-09)."])

    fr(r, "FR-03", "Reset Password", "UC-04 Reset Password",
       "Login screen → **Quên mật khẩu**.",
       "A visitor who has forgotten the password requests a reset code. To prevent account enumeration the API always answers with the same neutral message; a code is sent only when the address belongs to a verified account. The visitor enters the code and a new password; the API stores the new hash and revokes every refresh token of the account.",
       shots=[("31_forgot_password.png", "Forgot password screen")],
       fields=[["Email", "Text", "Yes", "Valid format (MSG02)"],
               ["Reset code", "Text (6 digits)", "Yes", "Valid for 10 minutes; at most 5 attempts (MSG16)"],
               ["New password / confirmation", "Password", "Yes", "6–128 characters; equal (MSG04, MSG05, MSG17)"]],
       rules="BR-02, BR-03, BR-05.",
       normal=["The visitor enters the e-mail address and selects **Gửi mã**; the client shows MSG15.",
               "The visitor enters the code, the new password and its confirmation.",
               "The API changes the password, revokes all sessions and the client returns to sign-in."],
       exceptions=["E1 – Wrong or expired code: MSG16.", "E2 – Resend within 60 seconds: MSG10."])

    fr(r, "FR-04", "Onboarding and Player Profile", "UC-06 Choose Experience Level, UC-07 Update Player Name, UC-08 Select Character",
       "First sign-in (experience popup); Menu → name tag or profile card edit icon (name); Menu → **Nam** / **Nữ** selector (character).",
       "At the first sign-in the player answers MSG21. **Người mới** (Beginner) opens the tutorial; **Đã thành thạo** (Experienced) goes to the Menu. The answer is saved once. The player can later change the display name and choose the male or female character; the character is used in the menu, training and matches.",
       shots=[("11_menu_online.png", "Menu (lobby) of a signed-in player")],
       fields=[["Experience level", "Choice", "Yes (once)", "Beginner or Experienced; cannot be changed afterwards"],
               ["Display name", "Text", "Yes", "3–18 characters, no control characters, unique (MSG19, MSG20)"],
               ["Character", "Choice", "Yes", "Male or Female; default Male"]],
       rules="BR-06, BR-07, BR-08.",
       normal=["The player answers the experience question; the choice is saved.",
               "The player edits the name and confirms; the API checks length and uniqueness and saves it.",
               "The player selects **Nam** or **Nữ**; the menu character changes immediately and the choice is saved."],
       exceptions=["E1 – Name outside 3–18 characters: MSG19.", "E2 – Name already used: MSG20.",
                   "E3 – The player is in a room, searching or in a match: profile changes are disabled until the activity ends."])

    fr(r, "FR-05", "Configure Game Settings", "UC-09 Configure Game Settings",
       "Menu → **Cài đặt**, or Esc during a match or drill.",
       "The player adjusts audio (music and effects volume), graphics (the quality options of the Graphics tab) and controls (keyboard bindings for PC and the touch layout for phones). Changes are previewed and stored on the device when the player selects **Áp dụng và lưu**.",
       shots=[("24_settings.png", "Settings – Overview tab"), ("25_settings_controls.png", "Settings – Controls tab with key bindings")],
       fields=[["Music / effects volume", "Slider 0–100", "No", "Default from the build"],
               ["Graphics options", "Choice", "No", "Applied after Apply and save"],
               ["Key binding", "Key", "No", "One key per action; Restore defaults resets all (defaults: W/A/S/D move, Left Shift run, J drive and serve, K dink, L volley, U power, I cross)"]],
       rules="BR-17.",
       normal=["The player changes values; **Áp dụng và lưu** becomes active.", "The player saves; settings apply immediately and persist on the device."],
       exceptions=["A1 – **Hoàn tác** restores the last saved values; **Khôi phục mặc định** restores factory values."])

    # ============================================================ FE-02
    r.h3("3.3 Tutorial and Technique Training (FE-02)")
    fr(r, "FR-06", "Complete Tutorial", "UC-10 Complete Tutorial",
       "Automatically after choosing Beginner (FR-04); Mode Selection → **Tutorial / Hướng dẫn cơ bản**.",
       "The tutorial teaches the court, the rules and the controls in 17 short steps (about 2–3 minutes). Each step shows a title, an explanation and a demonstration on the court, for example the court dimensions, a ball that does not cross the net, the two-bounce rule and the non-volley zone. Completion is saved to the profile.",
       shots=[("33_tutorial.png", "Tutorial step 1 – getting to know the court"), ("34_tutorial_step.png", "Tutorial step 4 – fault: the ball does not cross the net")],
       rules="BR-09 to BR-14.",
       normal=["The tutorial opens at step 1.", "The player reads each step and selects **Tiếp tục** (Enter) to continue or **Xem lại** to replay the demonstration.",
               "After step 17 the completion is saved and the Menu opens."],
       exceptions=["A1 – **Để sau** leaves the tutorial; it can be reopened from Mode Selection."])

    fr(r, "FR-07", "Browse Lessons", "UC-11 Browse Lessons",
       "Mode Selection → **Luyện tập**; Training Progress → **Tiếp tục luyện**.",
       "The academy lists the 15 published lessons grouped as Basic, Control, Advanced and Doubles. Each row shows the group, the first-pass reward, the best stars and whether the lesson is unlocked. Selecting a lesson opens its briefing: coach explanation, target, reward and the star rules.",
       shots=[("08_lesson_catalog.png", "Lesson catalogue with group tabs"), ("04_training_entry.png", "Lesson briefing for \"Giao bóng đúng ô\" (serve)")],
       rules="BR-18, BR-19, BR-20.",
       normal=["The player opens the academy; the catalogue loads from the server (or the cached catalogue when offline).", "The player filters by group and selects **Luyện tập** on a lesson.", "The briefing opens; **Bắt đầu** starts the drill (FR-08)."],
       exceptions=["E1 – The lesson is locked: the start button is disabled and MSG23 is shown."])

    fr(r, "FR-08", "Practise Drill", "UC-12 Practise Drill",
       "Lesson briefing → **Bắt đầu**; Drill Result → **Luyện lại**.",
       "The player performs the drill on the training court, for example eight serves into the highlighted service court. For each shot the game measures whether the ball lands in the target area and the timing error between the key release and the ideal contact. In-drill feedback tells the player whether the release was early or late. At the end the client sends the attempt and its telemetry to the API, which validates it, applies the stars and the first-pass reward and returns a receipt.",
       shots=[("05_serve_flight.png", "Practice HUD of the volley drill: coach panel, feedback after a late hit (\"Chưa đúng • Thử lại\") and the shot cue L under the player"), ("07_training_finished.png", "Drill result – target not reached")],
       fields=[["Shots / target", "Integer", "System", "Fixed per lesson (for example 6 of 8 for the serve lesson)"],
               ["Hits in target", "Integer", "System", "Counted by the rules engine"],
               ["Mean timing error", "Seconds", "System", "Absolute difference between release and ideal contact"],
               ["Stars", "0–3", "System", "Computed by BR-18; the best result is kept"]],
       rules="BR-16, BR-17, BR-18, BR-20.",
       normal=["The drill starts and the HUD shows lesson, shot counter and key hints.", "The player hits the required shots; feedback (MSG26, MSG27) appears after each shot.",
               "After the last shot the Drill Result popup shows stars, accuracy, mean timing, time, the most frequent error and a tip.",
               "The API stores the attempt; on the first pass it grants the lesson reward and unlocks the next lesson.",
               "The coaching analysis appears when ready (FR-19)."],
       exceptions=["A1 – Target not reached: MSG24 and MSG25; no currency is lost and **Bài tiếp theo** stays disabled.",
                   "A2 – The device is offline: the attempt is queued for synchronisation (FR-09).",
                   "E1 – The attempt is rejected by the server (for example inconsistent telemetry): receipt REJECTED and no reward."])

    fr(r, "FR-09", "Offline Training and Synchronisation", "UC-13 Practise Drill Offline, UC-14 Synchronise Offline Results",
       "Login → **Luyện tập ngoại tuyến** when a profile is cached; automatic synchronisation when the connection returns, or banner → **Đăng nhập**.",
       "A player who has signed in on the device at least once can train without a connection. The client uses the cached catalogue and profile, stores every finished attempt with a unique request ID in a local queue and shows the offline banner MSG28. When the player is online again, the queue is sent; the API applies each request at most once and answers APPLIED, DUPLICATE or REJECTED. Online-only features (bot and online matches, shop) are disabled offline.",
       shots=[("02_menu_offline.png", "Menu in offline training mode with the offline banner")],
       rules="BR-21.",
       normal=["The player starts offline training from the Login screen.", "Drills work as in FR-08; each result is added to the queue and the banner shows the pending count.",
               "When connected, the client sends the queue; applied items are removed and rewards are added to the profile."],
       exceptions=["A1 – The same request is sent twice: receipt DUPLICATE; no second reward.", "E1 – No cached profile on the device: the offline button is not shown.",
                   "E2 – A queued item is invalid: receipt REJECTED; the item is removed and the player is informed through **Chi tiết**."])

    fr(r, "FR-10", "View Training Progress", "UC-15 View Training Progress",
       "Menu → **Tiến trình**.",
       "The progress popup summarises the player's learning: passed lessons out of 15, the next lesson, the latest post-session feedback, total practice time, the most frequent error and the most retried lesson. For every lesson it shows the stars, the best accuracy and the four most recent accuracy results (\"Gần đây\"), so that the player can see the trend, with a Retry shortcut. Technique mastery is updated from the same attempts. Tabs give access to daily and weekly missions and achievements (FR-21).",
       shots=[("03_progression.png", "Training progress popup")],
       rules="BR-18.",
       normal=["The player opens Progress; the data is loaded from the profile.", "The player selects **Luyện lại** on a lesson to open its briefing."])

    # ============================================================ FE-03
    r.h3("3.4 Core Gameplay and Bot Practice (FE-03)")
    fr(r, "FR-11", "Control the Player and Hit Shots", "UC-12, UC-16, UC-25",
       "Any rally in a drill, bot match or online match.",
       "On PC the player moves with W/A/S/D, runs with Left Shift and hits by holding and releasing shot keys. One of seven shot kinds is chosen from the key combination when the first key is released; U adds power and I aims cross-court. On phones the left joystick moves the player, the CHẠY button is held to run, and the DRIVE, DINK and VOLLEY buttons are held and released; dragging a held button up turns DRIVE into SMASH, DINK into DROP and VOLLEY into RESET, dragging left aims cross-court (CHÉO), dragging right adds power (LỰC) and dragging down cancels (HỦY); the serve uses DRIVE. The paddle contact is accepted only inside the timing window, and the quality of the contact (Perfect, Good, early or late) affects the ball speed and accuracy. The ball flight, bounce and net contact are simulated at 120 Hz. The camera stays behind the player's baseline and keeps the player above the shot cue (PC) or the touch controls (phone).",
       shots=[("19_botmatch_rally.png", "Bot match: the player is serving; the shot cue under the player shows the key combination J + I (cross-court serve)")],
       layout=[["Score panel (top left)", "Mode and difficulty, game number, games and points of each side, serving side and serve timer"],
               ["Rule hint (left)", "What the player must do now, for example \"Bạn giao • giữ rồi thả J\" and the score to call"],
               ["Controls (top right)", "Settings (Esc) and help (F1)"],
               ["Court", "Player character, opponent, ball, landing marker and fault banner"],
               ["Shot cue (bottom centre)", "Key combination and name of the shot being prepared; on phones the joystick and shot buttons take this area"]],
       rules="BR-16, BR-17.",
       normal=["The player moves to the ball and holds a shot key (J, K or L, alone or combined).", "The player releases the key close to the contact moment; the game selects the shot kind and checks the timing.",
               "The ball is hit towards the target computed from the shot kind and modifiers."],
       exceptions=["A1 – Release outside ±65 ms of the ideal contact: no contact (miss) and feedback MSG26 or MSG27.", "A2 – Invalid key combination: the input is ignored until all keys are released."])

    fr(r, "FR-12", "Apply Singles Match Rules", "UC-16, UC-25",
       "Every rally of a bot or online match.",
       "The rules engine decides legal serves, faults, points, games and the match winner according to the official singles rules simplified for the game. In online matches the same engine runs on the dedicated server, which is the only authority for the score.",
       shots=[("18_botmatch_intro.png", "Bot match introduction"), ("20_botmatch_victory.png", "Match result – victory 2–0")],
       rules="BR-09 to BR-15.",
       normal=["The server of the rally serves underhand from behind the baseline into the diagonal service court within 10 seconds.",
               "The return and the third shot must bounce; afterwards volleys are allowed outside the non-volley zone.",
               "When a fault occurs, the rally ends, a fault banner (MSG30) is shown and the point or side-out is applied.",
               "A game ends at 11 points with a 2-point lead; the first side to win two games wins the match (MSG31)."],
       exceptions=["A1 – In the third game the players change ends when a side reaches 6 points.",
                   "E1 – The serve timer expires: fault ServeTimeout and MSG29."])

    fr(r, "FR-13", "Set Up and Play a Bot Match", "UC-16 Play Bot Match",
       "Menu → **Chơi ngay** → Mode Selection → **Đấu với bot** → Bot Match Setup → **Vào sân**.",
       "The player chooses the difficulty, the time of day and the court, then plays a singles match against the computer. Easy, Medium and Hard use fixed reaction and accuracy settings; the adaptive option uses a behaviour-tree bot whose tier follows the player's Elo. After the match the result popup shows the winner, scores and rewards, and the coaching analysis can be opened.",
       shots=[("16_mode_selection.png", "Mode selection"), ("17_bot_setup.png", "Bot match setup")],
       refs=("20_botmatch_victory.png",),
       fields=[["Difficulty", "Choice", "Yes", "Easy, Medium, Hard or Adaptive"], ["Time of day", "Choice", "Yes", "Day or night"], ["Court", "Choice", "Yes", "Professional or outdoor"]],
       rules="BR-10 to BR-16, BR-22, BR-31, BR-32.",
       normal=["The player selects the options and **Vào sân**; the intro cinematic plays.", "The match is played with FR-11 and FR-12.",
               "At the end the API settles the result: rewards and XP are added; Elo does not change in a practice bot match.",
               "The player selects **Chơi lại**, **Chọn chế độ** or **Xem phân tích**."],
       exceptions=["A1 – Ranked Bot match assigned by matchmaking (FR-14): Elo changes according to BR-23.", "E1 – The device is offline: the bot match card is disabled."])

    # ============================================================ FE-04
    r.h3("3.5 Online Matches (FE-04)")
    fr(r, "FR-14", "Find an Online Match", "UC-19 Find Online Match, UC-20 Confirm Match Readiness",
       "Mode Selection → **Đấu online** → Online Lobby → **Đấu đơn 1v1** (ranked singles) or **Đấu đôi 2v2** (quick doubles) → **Tìm trận**.",
       "**Ranked singles:** the player joins the ranked queue; the matchmaking worker pairs two searching players whose Elo is within the pairing window, and if no human opponent is found within 180 seconds a ranked bot opponent is assigned. **Quick doubles:** a player searches alone or the captain of a party of two (formed by room code or invitation) searches for both; the API fills two teams of two from solo players and parties in the same region, without Elo pairing and without a bot; the match is unrated. In both modes every player must accept within 10 seconds. When all players accept, the API allocates the dedicated server and issues signed tickets, and the clients load the multiplayer court.",
       shots=[("28_searching.png", "Searching for a singles opponent with the elapsed time (layout preview rendered in the Unity Editor)")],
       rules="BR-22 to BR-27.",
       normal=["The player selects **Tìm trận**; MSG32 and a timer are shown.", "A pair (or two full teams for doubles) is found; the Ready Check shows MSG33 with a 10-second countdown.",
               "All players accept; the server allocates the match and the court loads (FR-16)."],
       exceptions=["A1 – Ranked singles only: no opponent within 180 seconds: a ranked bot is assigned and the match starts on the dedicated server.",
                   "A2 – Quick doubles: no two full teams are found before the search lease ends: MSG34; there is no bot fallback.",
                   "A3 – The player selects **Hủy** while searching: the ticket is cancelled.",
                   "E1 – A player declines or the countdown ends: the offer closes; the decliner sees MSG35 and the other player returns to the queue.",
                   "E2 – The dedicated server is busy: MSG36 and the request waits for the slot.",
                   "E3 – The player already has a search or an unfinished match: MSG41.", "E4 – Search expired: MSG34."])

    fr(r, "FR-15", "Private Rooms and Invitations", "UC-21 Create Private Room, UC-22 Join Room by Code, UC-23 Invite Friend to Room, UC-24 Respond to Room Invitation",
       "Mode Selection → **Tạo phòng**; Online Lobby → **Mã phòng** + **Tham gia bằng mã**; friend list → invite.",
       "A player creates a singles (2 players) or doubles (up to 4 players) room. Other players join with the room code or accept an invitation sent to an online friend. The collapsible friends column of the lobby lists the friends who can be invited; only friends who are in the lobby receive invitations. The host can change teams, remove a guest and start the match. Custom matches are unrated and give no Elo. A doubles room of two players can also be used as a party for quick doubles (FR-14).",
       shots=[("27_online_lobby.png", "Online lobby – doubles room with the room code and the friends column (layout preview rendered in the Unity Editor with sample friend data)")],
       fields=[["Room code", "Text", "Yes (to join)", "Code of an open room on Photon; wrong code → MSG39"], ["Mode", "Choice", "Yes", "Singles 1v1 or Doubles 2v2"]],
       rules="BR-22, BR-30, BR-38.",
       normal=["The host creates the room; the lobby shows the room code and roster slots.", "Guests join by code or by accepting an invitation.", "The host starts the match; the dedicated server is allocated as in FR-14."],
       exceptions=["E1 – Room full: MSG37.", "E2 – Room closed or the player was removed: MSG38.", "E3 – Invited player is busy or offline: MSG40.",
                   "E4 – Blocked players cannot invite each other (BR-38)."])

    fr(r, "FR-16", "Play an Online Match", "UC-25 Play Online Match",
       "Automatically after the ready check (FR-14) or when the host starts a room (FR-15).",
       "Each client connects to the dedicated server with its signed ticket through Photon Fusion. The server simulates the ball and applies the rules (FR-12); clients send only input and render server state with interpolation. The server sends heartbeats to the API every 8 seconds, records telemetry and the replay, and reports the final result.",
       shots=[("10_multiplayercourt_editmode.png", "Multiplayer court scene")],
       rules="BR-12 to BR-17, BR-28, BR-29.",
       normal=["The court loads and the server starts the match after all players are connected.", "Players play rallies; scores are shown from the server state.", "The server ends the match and the result popup appears (FR-18)."],
       exceptions=["E1 – Photon cannot be reached: MSG45.", "E2 – The connection to the match server is lost: MSG42 and FR-17 starts."])

    fr(r, "FR-17", "Reconnect to or Leave a Match", "UC-26 Reconnect to Match, UC-27 Leave Match",
       "Connection loss during an online match; pause menu → leave.",
       "If a player's connection drops, the client shows a reconnecting overlay and retries for up to 30 seconds with the same identity. The dedicated server pauses the match and keeps the player's slot for 30 seconds. A player who reconnects in time continues with the server score. A player who leaves, or who does not return within 30 seconds, is recorded as having forfeited and the server ends the match as interrupted; interrupted matches change no Elo and give no reward. If the dedicated server itself is lost, its lease expires and the API ends the match as interrupted (BR-28).",
       layout=[["Reconnecting overlay", "Status text and countdown; the court stays visible in the background"], ["Leave dialog", "Warning about the consequence and Confirm / Cancel buttons"]],
       rules="BR-28, BR-29.",
       normal=["The client detects the loss and starts reconnecting.", "The player rejoins with the same ticket identity and control is restored."],
       exceptions=["E1 – Reconnection fails within 30 seconds: MSG43; the result is shown when the server finishes processing.", "A1 – The player confirms leaving: MSG44 and the Menu opens."])

    fr(r, "FR-18", "Record the Match Result and Update Elo", "UC-16, UC-25 (non-screen settlement)",
       "End of a bot match (reported by the client) or of an online match (reported by the dedicated server).",
       "The API settles each match exactly once in a single transaction: it stores the game scores, marks the match FINISHED, applies Elo for ranked matches, grants Tokenpad and XP, and updates missions, achievements and streaks. The result popup and the history (FR-24) show the outcome.",
       refs=("20_botmatch_victory.png",),
       rules="BR-22, BR-23, BR-29, BR-32 to BR-35.",
       normal=["The API receives the result with the match identifier.", "It validates that the match is still PLAYING and that the reporter is allowed to report it.", "It saves scores, Elo, rewards and progress, then returns the summary to the clients."],
       exceptions=["A1 – The result is sent again: the stored result is returned and nothing is applied twice.", "E1 – The match was stopped by an administrator before the result: no Elo or reward (BR-48)."])

    # ============================================================ FE-05
    r.h3("3.6 AI Coaching (FE-05)")
    fr(r, "FR-19", "View Coaching Summary and Drill Recommendations", "UC-17 View Coaching Summary, UC-18 View Drill Recommendation",
       "Drill Result → analysis panel; Match Result → **Xem phân tích**.",
       "The client uploads the shot telemetry of the drill or match (for online matches the dedicated server uploads it). The hybrid AI worker sends the session to the AI coaching service, which detects common errors with rules (for example late contact, dink too high, entering the non-volley zone after a volley), writes a short summary and recommends at most three drills. When the gated tier model is approved, it also estimates the player's tier. The coaching popup shows the counts of shots, contacts and serves, the main errors with their frequency, and the recommended drills.",
       shots=[("21_ai_analysis.png", "Coaching summary after a bot match")],
       rules="BR-50, BR-51.",
       normal=["The player opens the analysis; the client polls the analysis job.", "The summary and recommendations appear when the job is completed.", "**Luyện ngay** opens the recommended lesson briefing."],
       exceptions=["A1 – The analysis is still running: the panel shows \"Đang phân tích…\".", "E1 – Too few shot opportunities: no finding is reported for that error.",
                   "E2 – AI service unavailable: the job is retried by the worker; the player can open the analysis later."])

    # ============================================================ FE-06
    r.h3("3.7 Progress, Rewards and Shop (FE-06)")
    fr(r, "FR-20", "Earn Experience, Levels and Progression Rewards", "UC-15, UC-16 (non-screen progression)",
       "Settlement of a drill (FR-08) or match (FR-18); claiming mission rewards (FR-21).",
       "Players earn XP from matches, lessons and missions. Reaching the XP threshold raises the level, which is shown in the name tag and history card. Two items are granted automatically once: the THOT paddle after 10 passed lessons and the GEMSTONE paddle at level 25.",
       refs=("11_menu_online.png",),
       rules="BR-20, BR-32, BR-33, BR-37.",
       normal=["XP is added by the settlement.", "If XP reaches level × 200, the level increases and the remaining XP carries over.", "Progression grants are checked and recorded in the reward log so that each is given once."])

    fr(r, "FR-21", "Missions, Streak and Achievements", "UC-37 to UC-40",
       "Menu → **Nhiệm vụ**; Progress → **Nhiệm vụ ngày / tuần**.",
       "Seven daily and ten weekly missions track matches, wins, points, valid returns and serves, bot matches and human matches. Completing all daily or weekly missions unlocks a set bonus. The daily streak counts consecutive days with at least one match; it has no reward of its own but is rewarded through the weekly mission for a 3-day streak (350 Tokenpad, 20 XP) and the achievement for a 5-day streak (500 Tokenpad, 30 XP). Ten achievements reward long-term goals such as 5, 25 and 100 wins.",
       shots=[("15_missions.png", "Missions popup with daily streak and reset countdown")],
       rules="BR-34, BR-35.",
       normal=["The player opens Missions; progress and the reset countdown are shown.", "A completed mission shows **NHẬN THƯỞNG** (or **NHẬN THƯỞNG TỔNG** for the set bonus); the player claims it and the reward is added.", "Achievement rewards are claimed in the same way (MSG49)."],
       exceptions=["A1 – A reward is claimed twice: the second claim adds nothing.", "A2 – Missions renew at 07:00 Vietnam time; unclaimed progress of the old cycle is closed."])

    fr(r, "FR-22", "Browse Shop and Purchase Cosmetic", "UC-41 Browse Shop, UC-42 Purchase Cosmetic",
       "Menu → **Cửa hàng**.",
       "The shop lists cosmetic paddles with preview, description and price. An item can have a Tokenpad price, an Elite Token price or both. The purchase debits the wallet and adds the item to the collection in one transaction.",
       shots=[("22_shop.png", "Shop with featured GEMSTONE and THOT paddles")],
       rules="BR-36.",
       normal=["The player selects an item and its currency and confirms the purchase.", "The API checks ownership and balance, debits the price and grants the item.", "The wallet and the collection are refreshed."],
       exceptions=["E1 – Not enough balance: MSG46.", "E2 – Item already owned: MSG47.", "E3 – Transaction failure: no balance is changed."])

    fr(r, "FR-23", "View Collection and Equip Cosmetic", "UC-43 View Collection, UC-44 Equip Cosmetic",
       "Menu → **Bộ sưu tập**.",
       "The collection shows all paddles with their status: in use, owned or not owned (preview only). The player equips an owned paddle, which is then used in matches and replays.",
       shots=[("23_collection.png", "Collection of paddle skins")],
       rules="BR-36.",
       normal=["The player selects an owned paddle and **TRANG BỊ**.", "The API marks it as the only equipped item; the menu character shows it."],
       exceptions=["E1 – The item is not owned: MSG48."])

    # ============================================================ FE-07
    r.h3("3.8 Social, History and Ranking (FE-07)")
    fr(r, "FR-24", "View Match History and Watch Replay", "UC-28 View Match History, UC-29 Watch Match Replay",
       "Menu → **Lịch sử**; history entry → replay button.",
       "The history popup lists the player's last 20 matches in Vietnam time with opponent, mode, result (win, loss or interrupted), game score and Elo change, plus the level card. A match with a stored replay can be watched in the Replay Viewer with pause, seek and speeds from 0.5× to 2×. Only participants can open a replay.",
       shots=[("12_history.png", "Match history with interrupted ranked matches")],
       rules="BR-29, BR-40.",
       normal=["The player opens History; the list loads from the API.", "The player selects a replay; the manifest and chunks are downloaded and played."],
       exceptions=["A1 – No replay stored: the entry shows \"Chưa có replay\".", "E1 – A player who did not take part requests the replay: HTTP 403."])

    fr(r, "FR-25", "Manage Friends and Blocked Players", "UC-32 to UC-36",
       "Menu → friend drawer → **Bạn bè**.",
       "The player searches other players by name or ID, sends friend requests, accepts or declines received requests, removes friends and blocks or unblocks players. Online friends can be invited to a room (FR-15).",
       shots=[("13_social.png", "Friends drawer")],
       rules="BR-38.",
       normal=["The player searches by name or ID and sends a request.", "The receiver accepts or declines it under the requests list.", "The player can remove a friend or block a player from the list."],
       exceptions=["E1 – No match for the search: MSG51.", "E2 – The other player has already sent a request: MSG50.", "E3 – A request to oneself or to a blocked player is rejected."])

    fr(r, "FR-26", "View Leaderboard", "UC-31 View Leaderboard",
       "Menu → friend drawer → **Bảng xếp hạng**.",
       "The leaderboard lists players by Elo in descending order with rank tier, and highlights the player's own position, which can be located with **Vị trí của tôi**.",
       shots=[("14_leaderboard.png", "Elo leaderboard")],
       rules="BR-27, BR-39.",
       normal=["The player opens the leaderboard; the cached list is loaded.", "The player searches by name or jumps to the own position."])

    # ============================================================ FE-08
    r.h3("3.9 Reports, Restrictions and Appeals (FE-08)")
    fr(r, "FR-27", "Report a Player", "UC-30 Report Player",
       "Match History → **Tố cáo** on a finished match.",
       "A player reports an opponent from one of their own matches with a reason. The report is stored as PENDING for administrators (FR-30).",
       refs=("12_history.png",),
       fields=[["Reported player", "Choice", "Yes", "A participant of the match other than the reporter (MSG52)"], ["Reason", "Text (≤254)", "Yes", "Not empty"]],
       rules="BR-41, BR-44.",
       normal=["The player selects **Tố cáo**, the opponent and enters the reason.", "The API stores the report; the button changes to the reported state."],
       exceptions=["E1 – The player has already reported this player in this match: the duplicate is rejected.", "E2 – No eligible player: MSG52."])

    fr(r, "FR-28", "View Sanction Decision and Submit Appeal", "UC-45 View Sanction Decision, UC-46 Submit Appeal",
       "Sign-in of a restricted account (automatic); Menu → **Quyết định & Khiếu nại**.",
       "A restricted player sees each sanction with its type (suspended or banned), reason and expiry, and can submit one appeal per sanction. The appeal status (PENDING, CHECKING, ACCEPTED, DECLINED) and the administrator's decision reason are shown when available.",
       layout=[["Decision list", "Restriction type, reason, start and expiry (\"Không thời hạn\" for an indefinite ban)"],
               ["Appeal form", "Statement field (10–4000 characters) and Send button; hidden when an appeal already exists"],
               ["Appeal status", "Status badge and decision reason"]],
       fields=[["Statement", "Text", "Yes", "10–4000 characters (MSG53)"]],
       rules="BR-42, BR-43, BR-44.",
       normal=["The restricted player opens the popup and reads the decision.", "The player writes the statement and sends it; the status becomes PENDING."],
       exceptions=["E1 – Statement shorter than 10 characters: MSG53.", "E2 – No sanction to appeal: MSG54.", "E3 – An appeal already exists for the sanction: the form is not shown."])

    # ============================================================ FE-09
    r.h3("3.10 Administration Web Application (FE-09)")
    sh, lay = admin_layout(r, "42_admin_users.png", "Users page of the admin web application", [
        ["Header", "Title \"Quản lý Người dùng\", search box (name, e-mail or ID) and status filter (all, ACTIVE, SUSPENDED, BANNED)"],
        ["User table", "Player and e-mail, role, status, Elo, play time, creation date and actions; paging"],
        ["Role dialog", "Player (game client only) or Admin (full portal access)"],
        ["Sanction dialog", "Suspend (expiry required) or ban (expiry optional) with a reason"],
        ["Progress reset dialog", "Groups LEARNING, RANKING, MISSIONS, ECONOMY, impact preview and Reset button"]])
    fr(r, "FR-29", "Admin Sign-in and User Management", "UC-47 to UC-53",
       "Browser → admin web URL → Login; sidebar → **Quản lý người dùng** (/users).",
       "An administrator signs in with an Admin account; other roles are rejected. On the Users page the administrator searches and filters accounts, assigns the Player or Admin role, verifies an e-mail manually when delivery failed, applies or revokes a sanction with a reason, and resets selected progress groups of a player after previewing the impact. Every change is written to the audit log.",
       shots=[("40_admin_login.jpg", "Admin web login page")] + sh, layout=lay,
       fields=[["Search text", "Text", "No", "Matches name, e-mail or ID"], ["Restriction", "Choice", "Yes (sanction)", "SUSPENDED or BANNED"],
               ["Expiry", "Date-time (UTC)", "Suspend: yes", "Must be in the future (MSG56)"], ["Reason", "Text", "Yes", "3–2000 characters (MSG55)"],
               ["Reset groups", "Multi-choice", "Yes (reset)", "At least one of LEARNING, RANKING, MISSIONS, ECONOMY"]],
       rules="BR-42, BR-45, BR-46.",
       normal=["The administrator signs in and opens Users.", "The administrator searches, selects an account and an action.", "The administrator confirms; the API applies the change, writes the audit record and the table refreshes."],
       exceptions=["E1 – Non-admin account at login or on any admin API: HTTP 403.", "E2 – Sanction on the own account or a system account: MSG57.",
                   "E3 – Reset while the player is searching or in a match: MSG58.", "E4 – Invalid reason or expiry: MSG55, MSG56."])

    sh, lay = admin_layout(r, "45_admin_moderation.png", "Moderation page", [
        ["Reports tab", "Filter by status; reporter, reported player, match and evidence; Download match evidence and replay"],
        ["Decision form", "Outcome (accept or decline), optional sanction (suspend with expiry or ban) and decision reason; Save"],
        ["Appeals tab", "Statement, related sanction, decision (accept revokes only that sanction, decline keeps it) and reason"]])
    fr(r, "FR-30", "Review Player Reports and Appeals", "UC-54 Review Player Report, UC-55 Review Appeal, UC-51 Apply Sanction",
       "Sidebar → **Xử lý vi phạm** (/moderation).",
       "The administrator opens a pending report, marks it CHECKING, examines the match evidence and replay, and accepts or declines it with a reason; accepting may apply a sanction to the reported player. Appeals are handled in the same way; accepting an appeal revokes only the sanction it concerns.",
       shots=sh, layout=lay,
       fields=[["Decision", "Choice", "Yes", "ACCEPTED or DECLINED"], ["Decision reason", "Text", "Yes", "3–2000 characters (MSG55)"]],
       rules="BR-42, BR-43, BR-44, BR-45.",
       normal=["The administrator selects a report and reviews evidence.", "The administrator enters the decision and reason and saves.", "The report status changes; a sanction, if chosen, is applied and the player is restricted."],
       exceptions=["E1 – The report or appeal was changed by another administrator: the latest version is reloaded.", "E2 – A non-admin token: MSG63 (HTTP 403)."])

    sh, lay = admin_layout(r, "43_admin_cms.png", "Content management page", [
        ["Lesson manager", "Lesson list; editor with code, name, group, order, target, prerequisite, coach text and technique template; Save draft, Publish saved version, Unpublish"],
        ["Technique references", "Reference values per technique with version; save requires the latest version"],
        ["Shop item form", "Item code, name, type (paddle, outfit, hat), price and currency"],
        ["Mission form", "Name, cycle (daily or weekly), target and reward"]])
    fr(r, "FR-31", "Manage Training Content, Items and Missions", "UC-56 to UC-61",
       "Sidebar → **Nội dung (CMS)** (/cms).",
       "The administrator creates and edits lessons as drafts, publishes a saved draft so that new attempts use it, and unpublishes a lesson to hide it from players; attempts already in progress keep their version. The administrator also maintains technique reference values, adds shop items and creates missions.",
       shots=sh, layout=lay,
       fields=[["Lesson code", "Text", "Yes", "Unique (MSG59)"], ["Target", "Integer", "Yes", "1 ≤ target ≤ number of shots"],
               ["Prerequisite", "Lesson", "No", "Must not create an unlock cycle"], ["Price", "Integer", "Yes (item)", "≥ 0, with a currency"]],
       rules="BR-19, BR-45, BR-47.",
       normal=["The administrator edits a lesson and selects **Lưu bản nháp**.", "The administrator selects **Xuất bản bản đã lưu**; the lesson becomes PUBLISHED.", "**Gỡ xuất bản** sets it to UNPUBLISHED."],
       exceptions=["E1 – Duplicate lesson code: MSG59.", "E2 – The content changed since it was loaded: MSG60.", "E3 – The prerequisite creates a cycle: the save is rejected."])

    sh1, lay1 = admin_layout(r, "41_admin_dashboard.png", "Admin dashboard", None)
    sh2, lay2 = admin_layout(r, "44_admin_matches.png", "Live matches page", None)
    sh3, lay3 = admin_layout(r, "46_admin_analytics.png", "Analytics page", None)
    lay = None
    if not (sh1 or sh2 or sh3):
        lay = [["Dashboard", "Total users, daily active users, matches, online time, pending reports, technique-error heat map, Refresh"],
               ["Matches", "Live matches refreshed every 5 s with kind, players, score (games A : B) and connection; history list; Stop match dialog with reason and warning MSG61"],
               ["Analytics", "From/To dates (Vietnam time); daily players; retention cohorts; popular lessons with pass rate; error heat map; matchmaking health"]]
    fr(r, "FR-32", "Monitor Matches and View Analytics", "UC-62 to UC-66",
       "Sidebar → **Tổng quan (Dashboard)**, **Giám sát trận đấu** (/matches), **Thống kê & AI Telemetry** (/analytics).",
       "The dashboard summarises today's activity and pending work. The Matches page shows live matches with the score and connection state reported by the dedicated server and lets the administrator stop a live match with a reason. The Analytics page reports daily active players, retention cohorts, popular lessons with pass rate (passes divided by started attempts), the technique-error heat map and matchmaking health for a chosen date range.",
       shots=sh1 + sh2 + sh3, layout=lay,
       fields=[["Stop reason", "Text", "Yes", "3–2000 characters"], ["From / To date", "Date", "Yes", "From ≤ To; at most 366 days (MSG62)"]],
       rules="BR-45, BR-48, BR-49.",
       normal=["The administrator opens Matches; the list refreshes every 5 seconds.", "To stop a match the administrator enters a reason and confirms MSG61; the server ends the match as interrupted.",
               "The administrator opens Analytics, sets the dates and reads the reports."],
       exceptions=["E1 – The match already finished: the stop is ignored and the final result is kept.", "E2 – Invalid date range: MSG62."])

    # ============================================================ FE-10
    r.h3("3.11 Expert Review (FE-10)")
    sh, lay = admin_layout(r, "47_admin_expert.png", "Expert review page", [
        ["Session list", "Synchronised real-player sessions with filters; simulated demo sessions are shown separately and read-only"],
        ["Shot evidence", "Per-shot telemetry (timing, contact, landing, net clearance) for the selected session"],
        ["Labels", "Player level (beginner, intermediate, advanced), error labels per code, data group (training, calibration, holdout), basis of the label; Save expert review"]])
    fr(r, "FR-33", "Review Coaching Sessions and Export Dataset", "UC-67 Review Coaching Session, UC-68 Export Labelled Dataset",
       "Sidebar → **Gán nhãn coaching** (/coaching).",
       "An administrator acting as a pickleball expert inspects the shot-level evidence of a real training session, assigns the player level and confirms or rejects each error, and assigns the data group. All sessions of one player must use the same data group. The labelled sessions are exported to calibrate the rule thresholds and to evaluate the tier model against NFR-06.",
       shots=sh, layout=lay,
       fields=[["Level", "Choice", "Yes", "Beginner, intermediate or advanced"], ["Error labels", "Per error code", "At least one", "Present or absent with evidence (MSG64)"],
               ["Data group", "Choice", "Yes", "Training, calibration or holdout; consistent for a player"]],
       rules="BR-45, BR-50.",
       normal=["The administrator selects a session and reviews the evidence.", "The administrator sets the labels and saves the review.", "The administrator exports the labelled dataset."],
       exceptions=["E1 – Missing confirmation, evidence or labels: MSG64.", "E2 – The session already has a newer review: the evidence must be reloaded.", "E3 – The player's sessions are in different data groups: HTTP 409."])
