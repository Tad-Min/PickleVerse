# EV-21 – Court probe of the surrender (D-34), 25/11/2026 16:50

Purpose: check "Đầu hàng" on the real court before it is released, because staging still runs adcb4e2 without it.

Set-up
- Windows player built from main @ adcb4e2 with the fixes of D-33 and D-34 (Unity 6000.6.2f1). The build also contained a temporary probe that starts only with a command-line argument; it is not part of the repository and not part of the release.
- The probe opens the scene MultiplayerCourt with NetworkMatch, NetworkPlayer, MultiplayerCourtUI and SurrenderPanel as they are in the game, on one Fusion runner in GameMode.Single. The local player is "Huy". The messages of the other seats (Gia Bảo, Đạt, Kiên) are injected into NetworkMatch with their peer number, the way Fusion delivers them.
- No account was signed in and no request reached the API.

Not covered: the Photon connection, the dedicated server, the API and database (TC-F09-10), real accounts (TC-F09-06, TC-F09-09).

Result: 36 of 36 checks passed.

| # | Check |
|---|---|
| 1 | the court spawned the match |
| 2 | every seat has its actor |
| 3 | the match started after the entrance |
| 4 | button label: Đầu hàng |
| 5 | the button is on the court |
| 6 | pressing the button ends nothing |
| 7 | the player sees the box |
| 8 | the server opened the request of the team |
| 9 | TIẾP TỤC ĐẤU closes the request |
| 10 | the team waits before the next request |
| 11 | the partner's request opens the card |
| 12 | the card does not lock the controls |
| 13 | one confirmation does not end the match |
| 14 | KHÔNG ĐẦU HÀNG keeps the match running |
| 15 | the opponents cannot answer for this team |
| 16 | the player waits for the partner |
| 17 | refusal is explained: Gia Bảo không đồng ý đầu hàng. Trận đấu tiếp tục. |
| 18 | an unanswered request expires on the server |
| 19 | expiry is explained: Hết thời gian xác nhận đầu hàng. Trận đấu tiếp tục. |
| 20 | still one confirmation missing |
| 21 | both confirmed: the match ends and the team loses |
| 22 | the announcement names the players |
| 23 | the box is gone |
| 24 | both partners carry the loss |
| 25 | the result carries the surrender reason |
| 26 | history label |
| 27 | the opponents win |
| 28 | the court spawned the match |
| 29 | every seat has its actor |
| 30 | the match started after the entrance |
| 31 | D-34: pressing the button alone does not forfeit |
| 32 | the player sees the box |
| 33 | TIẾP TỤC ĐẤU closes the box |
| 34 | one confirmation ends a singles match |
| 35 | the announcement names the player |
| 36 | the surrendering player loses, the opponent wins |

Values read from the screen
- box: ĐẦU HÀNG? | Gia Bảo cũng phải xác nhận. Khi cả hai đồng ý, trận kết thúc và đội bạn bị xử thua. Bạn không nhận thưởng và có thể bị trừ Elo. | Tự hủy sau 20 giây
- card: ĐỒNG ĐỘI MUỐN ĐẦU HÀNG | Gia Bảo đề nghị đầu hàng. Khi cả hai đồng ý, trận kết thúc và đội bạn bị xử thua. Bạn không nhận thưởng và có thể bị trừ Elo. | Tự hủy sau 20 giây
- status: Đội B thắng trận 0 : 0 ván — điểm ván cuối 0 : 0 / Huy và Gia Bảo đã đầu hàng
- box: ĐẦU HÀNG? | Trận kết thúc ngay và bạn bị xử thua. Bạn không nhận thưởng và có thể bị trừ Elo.
- status: Đội B thắng trận 0 : 0 ván — điểm ván cuối 0 : 0 / Huy đã đầu hàng

Notes
- The run was repeated after a small layout change (distance between the buttons, accent lines inside the bevelled corners); this is the run after the change. Screenshots: screenshots/52_surrender_ask.png, 53_surrender_partner.png.
- One run in between was disturbed by a real mouse that pressed Đầu hàng and ĐẦU HÀNG in the window. The match then ended when the partner's confirmation was injected, as BR-66 requires; the probe reported its own later steps as failed. The probe ignores the mouse since then.
