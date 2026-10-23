| ID | Category | Location | Finding | Severity (High/Medium/Low) | Evidence |
|---|---|---|---|---|---|
| SR01 | Architecture | Registration vs SDD actual design | Photon Fusion Shared gives a peer state authority. This is not the proposed trusted dedicated simulation and leaves host/peer trust risk. | High | My project/Assets/Scripts/Multiplayer/PhotonSession.cs:116 |
| SR02 | Scope | F05 F14 and N06 | The active progression runtime requires server state. Offline drill execution and exactly-once later synchronization are not demonstrated. | High | My project/Assets/Scripts/Progression/ProgressionRuntime.cs:8 |
| SR03 | Scope | F05 and N06 | Initial-placement ML, telemetry technique classifier, labelled evaluation and validated recommendations are not evidenced. Rule-based feedback does not establish those commitments. | High | My project/Assets/Scripts/LocalMatch/BotMatch.cs:11 |
| SR04 | Design vs code | F15 administrator sign in | The React login sends emailOrUsername/passwordHash and reads role/token, while the API requires Email/Password and returns AccessToken/Profile. The default contracts are incompatible. | High | src/Frontend/src/context/AuthContext.tsx:41 |
| SR05 | Data | F15 user status mutation | Admin mutations serialize enum status as Banned/Active while the SQL constraint uses ACTIVE/BANNED/SUSPENDED; these values can fail the database constraint. | High | src/Backend/Services/Implementations/AdminService.cs:82 |
| SR06 | Flows | F16 content publication | Admin lesson creation omits catalog Code/Config, while the game catalog selects coded configured lessons. CRUD does not yet demonstrate publishing a playable drill. | High | src/Backend/Services/Implementations/AdminService.cs:159 |
| SR07 | Scope | F17 F18 administration | Complete live-match oversight, appeals and operational analytics are not evidenced. ActiveUsersToday currently counts all users rather than dated activity. | High | src/Backend/Services/Implementations/AdminService.cs:353 |
| SR08 | Requirements | N01 through N06 and Report 5 | Current-revision physical-device, network/load, security, availability, offline and ML acceptance results are unavailable. Test source is not execution evidence. | High | src/Database.sql |
| SR09 | Flows | Review 2 baseline and CI | The local review-2 tag and CI execution evidence are absent; the required official tagged self-review and automated-in-CI target cannot be verified. | High | src/Database.sql |
| SR10 | Scope | BM03 and registration baseline | The approved registration revision, official project code and decisions accepting stack/authority deviations are unconfirmed. No scope reduction has been assumed. | Medium | src/Database.sql |
| SR11 | Architecture | Online result validation | Server-owned transactions and matching human reports improve result integrity, but peers still supply simulation outcomes and training measurements. Agreement does not independently prove legal physics. | Medium | src/Backend/Services/Game/OnlineGameService.OnlineMatches.cs:182 |
| SR12 | Scope | Optional account invitation UX | Real friendship APIs and room-code play exist and cover the proposal room-code requirement. Account-targeted invitations are an unverified desired UX extension; confirm whether they should become a separate commitment. | Medium | src/Backend/Services/Game/OnlineSocialService.cs:27 |
| SR13 | Architecture | Deployment secrets and platforms | Fallback authentication configuration, deployed TLS/AES-256 controls, production rollback and iOS build acceptance require deployment evidence; configuration values are intentionally redacted. | Medium | My project/Assets/Scripts/Services/Api/UnityApiTransport.cs:17 |

The 5 most important findings

1. SR01 — Photon Fusion Shared gives a peer state authority. This is not the proposed trusted dedicated simulation and leaves host/peer trust risk.
2. SR02 — The active progression runtime requires server state. Offline drill execution and exactly-once later synchronization are not demonstrated.
3. SR03 — Initial-placement ML, telemetry technique classifier, labelled evaluation and validated recommendations are not evidenced. Rule-based feedback does not establish those commitments.
4. SR04 — The React login sends emailOrUsername/passwordHash and reads role/token, while the API requires Email/Password and returns AccessToken/Profile. The default contracts are incompatible.
5. SR05 — Admin mutations serialize enum status as Banned/Active while the SQL constraint uses ACTIVE/BANNED/SUSPENDED; these values can fail the database constraint.
