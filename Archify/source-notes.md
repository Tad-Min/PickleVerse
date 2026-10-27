# PickleVerse runtime architecture — source notes

This diagram describes the inspected working tree on 2026-10-27 (Asia/Saigon). It does not assert that these services are deployed or running.

Original repository: https://github.com/Tad-Min/PickleVerse.git
Original HEAD: 087769927cdd38a5e4907ceb3cbabc60fc2e0baa
Local evidence snapshot: e97aa908b3098211289e2e0c1ed1ec3678ffa658

The source-snapshot folder is an independent, local-only evidence repository containing selected source files, including uncommitted changes. Its commit is a snapshot identifier, not a commit published to the original remote. The original repository index and history were not changed. No .env files or runtime service keys were copied.

Scope: eight runtime components; one highlighted backend lookup path. SMTP, the in-process AI dispatcher, and detailed state transitions are described in cards. pgAdmin and deployment-only tools are omitted.

Key source conclusions:
- Unity uses REST/JWT for backend requests and Photon Fusion Shared mode for online sessions.
- The browser is the React administration app. API admin routes require the Admin role; browser mock mode can bypass backend calls.
- Redis owns transient matchmaking/presence/invitations and disposable leaderboard ranking snapshots.
- PostgreSQL remains authoritative and is used on normal requests too; on RedisException, leaderboard ranking falls back to SQL. Matchmaking does not have SQL fallback.
- The hosted AI worker leases PostgreSQL jobs, calls the internal FastAPI service with X-AI-Key when enabled, then stores validated analysis in PostgreSQL.
- AI ingest/dispatch/coaching/model flags default off in Compose; the actual ignored .env was not inspected.
- Docker Compose is a local development topology. The bridge is a deployment boundary, not a network ACL: several backing services also have loopback host port mappings. External TLS termination is not defined here.

Working-tree changes present before diagram authoring:
```text
 M .dockerignore
 M .env.example
 M .gitignore
 M "My project/Assets/Resources/BackendApiSettings.asset"
 M "My project/Assets/Resources/BhTree/Bot Behavior.asset"
 M "My project/Assets/Scripts/Entites/Progression/ProgressionModels.cs"
 M "My project/Assets/Scripts/FrontEnd/PlayerProgressionMenu.Training.cs"
 M "My project/Assets/Scripts/LocalMatch/BhTreeBot.cs"
 M "My project/Assets/Scripts/LocalMatch/BotMatch.Progression.cs"
 M "My project/Assets/Scripts/LocalMatch/BotMatch.cs"
 M "My project/Assets/Scripts/LocalMatch/MatchActor.cs"
 M "My project/Assets/Scripts/LocalMatch/TrainingDirector.cs"
 M "My project/Assets/Scripts/Multiplayer/NetworkMatch.Bots.cs"
 M "My project/Assets/Scripts/Multiplayer/NetworkMatch.Photon.cs"
 M "My project/Assets/Scripts/Multiplayer/NetworkMatch.Progression.cs"
 M "My project/Assets/Scripts/Multiplayer/NetworkMatch.cs"
 M "My project/Assets/Scripts/Multiplayer/PhotonCommandChannel.cs"
 M "My project/Assets/Scripts/Progression/BotContactChance.cs"
 M "My project/Assets/Scripts/Progression/EloPolicy.cs"
 M "My project/Assets/Scripts/Progression/OnlineGameModels.cs"
 M "My project/Assets/Scripts/Progression/ProgressionEngine.cs"
 M "My project/Assets/Scripts/Services/Api/UnityApiTransport.cs"
 M "My project/Assets/Tests/Progression/BotContactChanceTests.cs"
 M "My project/Assets/Tests/Progression/ProgressionTests.cs"
 M README.md
 M docker-compose.yml
 M skills-lock.json
 M src/Backend/API/Controllers/AiCoachingController.cs
 M src/Backend/API/Program.cs
 M src/Backend/API/appsettings.json
 M src/Backend/IntegrationChecks/IntegrationChecks.csproj
 M src/Backend/IntegrationChecks/Program.cs
 M src/Backend/IntegrationChecks/TestSettings.cs
 M src/Backend/Services/Configuration/ServerSettings.cs
 M src/Backend/Services/Game/GameModels.cs
 M src/Backend/Services/Game/OnlineGameService.Matches.cs
 M src/Backend/Services/Game/OnlineGameService.OnlineMatches.cs
 M src/Backend/Services/Game/RedisGameState.cs
 M src/Backend/Services/Services.csproj
 M src/Database.sql
?? Doc/Hybrid_AI_Implementation.md
?? "My project/Assets/Scripts/LocalMatch/ActorTelemetry.cs"
?? "My project/Assets/Scripts/LocalMatch/ActorTelemetry.cs.meta"
?? "My project/Assets/Scripts/LocalMatch/AdaptiveBotContext.cs"
?? "My project/Assets/Scripts/LocalMatch/AdaptiveBotContext.cs.meta"
?? "My project/Assets/Scripts/Multiplayer/NetworkMatch.Coaching.cs"
?? "My project/Assets/Scripts/Multiplayer/NetworkMatch.Coaching.cs.meta"
?? "My project/Assets/Scripts/Progression/CoachingRuntime.cs"
?? "My project/Assets/Scripts/Progression/CoachingRuntime.cs.meta"
?? "My project/Assets/Scripts/Progression/HybridAiModels.cs"
?? "My project/Assets/Scripts/Progression/HybridAiModels.cs.meta"
?? "My project/Assets/Tests/Hybrid.meta"
?? "My project/Assets/Tests/Hybrid/"
?? "My project/Assets/Tests/Progression/HybridAiEditorVerification.cs"
?? "My project/Assets/Tests/Progression/HybridAiEditorVerification.cs.meta"
?? "My project/Assets/Tests/Progression/HybridAiTests.cs"
?? "My project/Assets/Tests/Progression/HybridAiTests.cs.meta"
?? contracts/
?? src/AiService/
?? src/Backend/API/Workers/
?? src/Backend/IntegrationChecks/HybridChecks.cs
?? src/Backend/IntegrationChecks/HybridHttpChecks.cs
?? src/Backend/Services/Configuration/HybridAiSettings.cs
?? src/Backend/Services/Game/HybridAiMetrics.cs
?? src/Backend/Services/Game/HybridCoachingService.cs
?? src/Backend/Services/Game/HybridJson.cs
```

