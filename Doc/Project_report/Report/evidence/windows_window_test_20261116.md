# EV-13 – Windows player: display modes, window frame and quit (16/11/2026)

Build: Windows 64-bit player (Mono) built with the Unity CLI from the source of commit 1b64424, on the team PC
(Windows 11, RTX 3050 Laptop GPU, 1920×1080 monitor at 125 % scaling). The checks were run with a PowerShell
script that starts the player, reads the window with the Win32 API (GetWindowRect, GetClientRect, GetWindowLongPtr,
IsIconic), moves the mouse and sends key presses. The player's own PlayerPrefs (registry key of the player) were
exported before the run and imported again afterwards. No sign-in was used; only the Login screen and the offline
Menu were opened.

## 1. Window frame (player started with `-screen-fullscreen 0 -screen-width 1280 -screen-height 720`)

| Step | Result |
|---|---|
| Start in windowed mode | window 1280×720, client 1280×720, no system caption (style 0x940A0000), title "PickleVerse" |
| Drag the title bar by (+150, +90) with the mouse | window moved by exactly (150, 90) |
| Click "–" | window minimised (IsIconic = true) |
| Restore from outside (ShowWindow) | 1280×720 again, still without the system caption |
| Click "×" | quit confirmation "Thoát PickleVerse?" shown |
| Press Esc (key event with scan code) | confirmation closed, game still running |
| Click "×", then "Thoát game" | process exited within 8 s |

A first run sent Esc without a scan code; the Input System ignored it. This was a fault of the test script, not of the game.

## 2. Display modes chosen in Settings → Khung hình (player started borderless; offline Menu → CÀI ĐẶT)

| Choice | Window after the change | Log line of the player |
|---|---|---|
| Cửa sổ | 1600×900, centred at (160, 90), no system caption | `[Display] Windowed 1600x900 in 32 ms` |
| Toàn màn hình | 1920×1080, exclusive (style 0x14000000) | `[Display] ExclusiveFullScreen 1920x1080 in 380 ms` |
| Không viền | 1920×1080 borderless (style 0x94000000) | `[Display] FullScreenWindow 1920x1080 in 58 ms` |
| Cửa sổ, then 1280 × 720 | 1280×720, centred at (320, 180) | `Windowed 1600x900 in 21 ms`, `Windowed 1280x720 in 20 ms` |
| Hoàn tác | back to borderless 1920×1080 | `[Display] FullScreenWindow 1920x1080 in 20 ms` |

An earlier test build produced a 1618×947 window when switching from fullscreen to windowed; it was fixed before
commit bc53722 (the frame now waits two frames and keeps the chosen client size) and the table above is from the
final build.

## 3. Display modes saved before start-up (PlayerPrefs written before each launch)

| Saved mode | Window at start-up |
|---|---|
| Windowed 1366×768 | 1366×768, no system caption, `[Display] Windowed 1366x768 in 329 ms` |
| Fullscreen | 1920×1080 exclusive, `[Display] ExclusiveFullScreen 1920x1080 in 745 ms` |
| Borderless | 1920×1080 borderless, `[Display] FullScreenWindow 1920x1080 in 363 ms` |

## Not checked

Windows 10 (no DWM rounded corners), a second monitor, Android (the display tab is hidden on phones), online play with
this build (it uses protocol 19, while staging runs protocol 20 since b08f5c7 of 17/11/2026; not tried).
