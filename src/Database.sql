-- Kích hoạt extension pgcrypto để sinh UUID ngẫu nhiên
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================================
-- 1. AUTHENTICATION & USERS
-- ============================================================================
CREATE TABLE Roles (
    RoleId SERIAL PRIMARY KEY,
    RoleName VARCHAR(32) NOT NULL UNIQUE,
    Description VARCHAR(254) NOT NULL
);

CREATE TABLE Users (
    UserId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Email VARCHAR(254) NOT NULL UNIQUE,
    PasswordHash VARCHAR(254) NOT NULL,
    RoleId INT NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE' CHECK (Status IN ('ACTIVE', 'BANNED', 'SUSPENDED')),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

CREATE TABLE RefreshTokens (
    TokenId SERIAL PRIMARY KEY,
    UserId UUID NOT NULL,
    TokenHash VARCHAR(254) NOT NULL UNIQUE,
    ExpiresAt TIMESTAMPTZ NOT NULL,
    IsRevoked BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);

CREATE TABLE VerificationEmail (
    VerificationEmailId SERIAL PRIMARY KEY,
    UserId UUID NOT NULL,
    Email VARCHAR(254) NOT NULL,
    Token VARCHAR(10) NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ExpiresAt TIMESTAMPTZ NOT NULL,
    IsVerified BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT FK_VerificationEmail_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);

CREATE TABLE PlayerProfiles (
    UserId UUID PRIMARY KEY,
    Avatar VARCHAR(80) NOT NULL DEFAULT 'initial',
    PlayerName VARCHAR(18) NOT NULL UNIQUE,
    Elo INT NOT NULL DEFAULT 100 CHECK (Elo >= 100),
    IsMale BOOLEAN NOT NULL DEFAULT TRUE,
    TimePlayedSeconds BIGINT NOT NULL DEFAULT 0,
    Level INT NOT NULL DEFAULT 1 CHECK (Level >= 1),
    Exp INT NOT NULL DEFAULT 0 CHECK (Exp >= 0),
    WinStreak INT NOT NULL DEFAULT 0 CHECK (WinStreak >= 0),
    DayStreak INT NOT NULL DEFAULT 0 CHECK (DayStreak >= 0),
    WinCount INT NOT NULL DEFAULT 0 CHECK (WinCount >= 0),
    LoseCount INT NOT NULL DEFAULT 0 CHECK (LoseCount >= 0),
    CONSTRAINT FK_PlayerProfiles_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);

--#region Social-Friendships
CREATE TABLE FriendRequests (
    RequestId SERIAL PRIMARY KEY,
    SenderId UUID NOT NULL,
    ReceiverId UUID NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'PENDING' CHECK (Status IN ('PENDING', 'ACCEPTED', 'DECLINED')),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    RespondedAt TIMESTAMPTZ NULL,
    CONSTRAINT CHK_FriendRequests_Self CHECK (SenderId != ReceiverId),
    CONSTRAINT UQ_Sender_Receiver UNIQUE (SenderId, ReceiverId),
    CONSTRAINT FK_FriendRequests_Sender FOREIGN KEY (SenderId) REFERENCES Users(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_FriendRequests_Receiver FOREIGN KEY (ReceiverId) REFERENCES Users(UserId) ON DELETE CASCADE
);

CREATE TABLE Friendships (
    UserId1 UUID NOT NULL,
    UserId2 UUID NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (UserId1, UserId2),
    CONSTRAINT CHK_Friendship_Order CHECK (UserId1 < UserId2),
    CONSTRAINT FK_Friendships_User1 FOREIGN KEY (UserId1) REFERENCES Users(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_Friendships_User2 FOREIGN KEY (UserId2) REFERENCES Users(UserId) ON DELETE CASCADE
);

CREATE TABLE BlockedUsers (
    UserId UUID NOT NULL,
    BlockedUserId UUID NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (UserId, BlockedUserId),
    CONSTRAINT CHK_Block_Self CHECK (UserId != BlockedUserId),
    CONSTRAINT FK_BlockedUsers_User FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_BlockedUsers_Blocked FOREIGN KEY (BlockedUserId) REFERENCES Users(UserId) ON DELETE CASCADE
);
--#endregion

--#region Match-History
CREATE TABLE MatchHistory (
    MatchId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    MatchType VARCHAR(25) NOT NULL DEFAULT '1Vs1',
    TeamAScore INT NOT NULL DEFAULT 0,
    TeamBScore INT NOT NULL DEFAULT 0
);

CREATE TABLE PlayerMatchHistory (
    PlayerId UUID NOT NULL,
    MatchId UUID NOT NULL,
    Status VARCHAR(25) NOT NULL DEFAULT 'WIN',
    IsTeamA BOOLEAN NOT NULL DEFAULT TRUE,
    ReceivedElo INT NOT NULL DEFAULT 0,
    PRIMARY KEY (PlayerId, MatchId),
    CONSTRAINT FK_PlayerMatchHistory_PlayerProfiles FOREIGN KEY (PlayerId) REFERENCES PlayerProfiles(UserId),
    CONSTRAINT FK_PlayerMatchHistory_MatchHistory FOREIGN KEY (MatchId) REFERENCES MatchHistory(MatchId) ON DELETE CASCADE
);

CREATE TABLE ReportPlayerInMatch (
    ReporterId UUID NOT NULL,       
    ReportedPlayerId UUID NOT NULL,  
    MatchId UUID NOT NULL,      
    Reason VARCHAR(254) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'PENDING' CHECK (Status IN ('PENDING', 'CHECKING', 'ACCEPTED', 'DECLINED')),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (ReporterId, ReportedPlayerId, MatchId),
    CONSTRAINT CHK_CannotReportSelf CHECK (ReporterId != ReportedPlayerId),
    CONSTRAINT FK_Report_Reporter FOREIGN KEY (ReporterId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_Report_ReportedPlayer FOREIGN KEY (ReportedPlayerId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_Report_Match FOREIGN KEY (MatchId) REFERENCES MatchHistory(MatchId) ON DELETE CASCADE
);

CREATE TABLE MatchHistoryReplay (
    MatchId UUID PRIMARY KEY,
    ReplayUrl VARCHAR(1000) NOT NULL,
    CONSTRAINT FK_MatchHistoryReplay_MatchHistory FOREIGN KEY (MatchId) REFERENCES MatchHistory(MatchId) ON DELETE CASCADE
);
--#endregion

--#region Economy-Cosmetics
CREATE TABLE Currencies (
    CurrencyId SERIAL PRIMARY KEY,
    CurrencyName VARCHAR(254) NOT NULL UNIQUE,
    Description VARCHAR(254) NOT NULL
);

CREATE TABLE PlayerCurrencies (
    PlayerId UUID NOT NULL,
    CurrencyId INT NOT NULL,
    Amount INT NOT NULL DEFAULT 0 CHECK (Amount >= 0),
    PRIMARY KEY (PlayerId, CurrencyId),
    CONSTRAINT FK_PlayerCurrencies_PlayerProfiles FOREIGN KEY (PlayerId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_PlayerCurrencies_Currencies FOREIGN KEY (CurrencyId) REFERENCES Currencies(CurrencyId)
);

CREATE TABLE Cosmetics (
    CosmeticId SERIAL PRIMARY KEY,
    CurrencyId INT NOT NULL,
    ItemCode VARCHAR(50) NOT NULL UNIQUE,
    CosmeticName VARCHAR(254) NOT NULL,
    Description VARCHAR(254) NOT NULL,
    Price INT NOT NULL DEFAULT 99999 CHECK (Price >= 0),
    Type VARCHAR(25) NOT NULL,
    CONSTRAINT FK_Cosmetics_Currencies FOREIGN KEY (CurrencyId) REFERENCES Currencies(CurrencyId)
);

CREATE TABLE PlayerCosmetics (
    PlayerId UUID NOT NULL,
    CosmeticId INT NOT NULL,
    Equipped BOOLEAN NOT NULL DEFAULT FALSE,
    PRIMARY KEY (PlayerId, CosmeticId),
    CONSTRAINT FK_PlayerCosmetics_PlayerProfiles FOREIGN KEY (PlayerId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_PlayerCosmetics_Cosmetics FOREIGN KEY (CosmeticId) REFERENCES Cosmetics(CosmeticId)
);
--#endregion

--#region Achievements-Missions
CREATE TABLE Achievements (
    AchievementId SERIAL PRIMARY KEY,
    AchievementCode VARCHAR(50) NOT NULL UNIQUE,
    AchievementName VARCHAR(254) NOT NULL,
    Description VARCHAR(254) NOT NULL,
    Type VARCHAR(25) NOT NULL,
    Target INT NOT NULL,
    Reward JSONB NOT NULL DEFAULT '{"Exp": 0, "Currency": 0}'::jsonb
);

CREATE TABLE PlayerAchievements (
    PlayerId UUID NOT NULL,
    AchievementId INT NOT NULL,
    Progress INT NOT NULL DEFAULT 0,
    IsClaimed BOOLEAN NOT NULL DEFAULT FALSE,
    ClaimedAt TIMESTAMPTZ NULL,
    PRIMARY KEY (PlayerId, AchievementId),
    CONSTRAINT FK_PlayerAchievements_PlayerProfiles FOREIGN KEY (PlayerId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_PlayerAchievements_Achievements FOREIGN KEY (AchievementId) REFERENCES Achievements(AchievementId)
);

CREATE TABLE Missions (
    MissionId SERIAL PRIMARY KEY,
    MissionName VARCHAR(254) NOT NULL,
    Description VARCHAR(254) NOT NULL,
    Type VARCHAR(25) NOT NULL,
    Target INT NOT NULL,
    Reward JSONB NOT NULL DEFAULT '{"Exp": 0, "Currency": 0}'::jsonb
);

CREATE TABLE PlayerMissions (
    PlayerId UUID NOT NULL,
    MissionId INT NOT NULL,
    Progress INT NOT NULL DEFAULT 0,
    IsClaimed BOOLEAN NOT NULL DEFAULT FALSE,
    ClaimedAt TIMESTAMPTZ NULL,
    PRIMARY KEY (PlayerId, MissionId),
    CONSTRAINT FK_PlayerMissions_PlayerProfiles FOREIGN KEY (PlayerId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_PlayerMissions_Missions FOREIGN KEY (MissionId) REFERENCES Missions(MissionId)
);
--#endregion

--#region Training & AI Coaching
CREATE TABLE Techniques (
    TechniqueId SERIAL PRIMARY KEY,
    TechniqueName VARCHAR(254) NOT NULL UNIQUE,
    Description VARCHAR(254) NOT NULL
);

CREATE TABLE Lessons (
    LessonId SERIAL PRIMARY KEY,
    LessonName VARCHAR(254) NOT NULL,
    Description VARCHAR(254) NOT NULL,
    Type VARCHAR(25) NOT NULL,
    Target INT NOT NULL,
    Reward JSONB NOT NULL DEFAULT '{"Exp": 0, "Currency": 0}'::jsonb
);

CREATE TABLE Drills (
    DrillId SERIAL PRIMARY KEY,
    TechniqueId INT NOT NULL,
    DrillName VARCHAR(254) NOT NULL,
    Description VARCHAR(254) NOT NULL,
    CONSTRAINT FK_Drills_Techniques FOREIGN KEY (TechniqueId) REFERENCES Techniques(TechniqueId) ON DELETE CASCADE
);

CREATE TABLE LessonDrills (
    LessonId INT NOT NULL,
    DrillId INT NOT NULL,
    Target INT NOT NULL,
    PRIMARY KEY (LessonId, DrillId),
    CONSTRAINT FK_LessonDrills_Lessons FOREIGN KEY (LessonId) REFERENCES Lessons(LessonId) ON DELETE CASCADE,
    CONSTRAINT FK_LessonDrills_Drills FOREIGN KEY (DrillId) REFERENCES Drills(DrillId) ON DELETE CASCADE
);

CREATE TABLE PlayerTechniqueMastery (
    PlayerId UUID NOT NULL,
    TechniqueId INT NOT NULL,
    TotalAttempts INT NOT NULL DEFAULT 0,
    TotalErrors INT NOT NULL DEFAULT 0,
    MasteryScore SMALLINT GENERATED ALWAYS AS (
        CASE 
            WHEN TotalAttempts = 0 THEN 0 
            ELSE CAST(((TotalAttempts - TotalErrors) * 100) / TotalAttempts AS SMALLINT)
        END
    ) STORED,
    LastUpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (PlayerId, TechniqueId),
    CONSTRAINT FK_PlayerTechniqueMastery_PlayerProfiles FOREIGN KEY (PlayerId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_PlayerTechniqueMastery_Techniques FOREIGN KEY (TechniqueId) REFERENCES Techniques(TechniqueId) ON DELETE CASCADE
);

CREATE TABLE PlayerTechniquesErrors (
    ErrorLogId SERIAL PRIMARY KEY,
    PlayerId UUID NOT NULL,
    TechniqueId INT NOT NULL,
    MatchId UUID NULL,
    LessonId INT NULL,
    ErrorType VARCHAR(50) NOT NULL,
    CoachingTip TEXT NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_PlayerTechniquesErrors_PlayerProfiles FOREIGN KEY (PlayerId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_PlayerTechniquesErrors_Techniques FOREIGN KEY (TechniqueId) REFERENCES Techniques(TechniqueId) ON DELETE CASCADE,
    CONSTRAINT FK_PlayerTechniquesErrors_MatchHistory FOREIGN KEY (MatchId) REFERENCES MatchHistory(MatchId) ON DELETE SET NULL,
    CONSTRAINT FK_PlayerTechniquesErrors_Lessons FOREIGN KEY (LessonId) REFERENCES Lessons(LessonId) ON DELETE SET NULL
);

CREATE TABLE PlayerLessonDrills (
    PlayerId UUID NOT NULL,
    LessonId INT NOT NULL,
    DrillId INT NOT NULL,
    Score INT NOT NULL DEFAULT 0,
    IsClaimedReward BOOLEAN NOT NULL DEFAULT FALSE,
    PRIMARY KEY (PlayerId, LessonId, DrillId),
    CONSTRAINT FK_PlayerLessonDrills_PlayerProfiles FOREIGN KEY (PlayerId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_PlayerLessonDrills_Lessons FOREIGN KEY (LessonId) REFERENCES Lessons(LessonId) ON DELETE CASCADE,
    CONSTRAINT FK_PlayerLessonDrills_Drills FOREIGN KEY (DrillId) REFERENCES Drills(DrillId) ON DELETE CASCADE
);
--#endregion

-- BEGIN ONLINE GAME SCHEMA
-- Additive online-game migration. Existing player data is retained.
BEGIN;
ALTER TABLE lessons ADD COLUMN IF NOT EXISTS code VARCHAR(50);
ALTER TABLE lessons ADD COLUMN IF NOT EXISTS sortorder INT NOT NULL DEFAULT 0;
ALTER TABLE lessons ADD COLUMN IF NOT EXISTS config JSONB NOT NULL DEFAULT '{}'::jsonb;
CREATE UNIQUE INDEX IF NOT EXISTS lessons_code_key ON lessons(code);
ALTER TABLE drills ADD COLUMN IF NOT EXISTS code VARCHAR(50);
CREATE UNIQUE INDEX IF NOT EXISTS drills_code_key ON drills(code);
ALTER TABLE missions ADD COLUMN IF NOT EXISTS code VARCHAR(50);
ALTER TABLE missions ADD COLUMN IF NOT EXISTS config JSONB NOT NULL DEFAULT '{}'::jsonb;
CREATE UNIQUE INDEX IF NOT EXISTS missions_code_key ON missions(code);
ALTER TABLE cosmetics ADD COLUMN IF NOT EXISTS config JSONB NOT NULL DEFAULT '{}'::jsonb;
CREATE TABLE IF NOT EXISTS cosmeticprices (
    cosmeticid INT NOT NULL REFERENCES cosmetics(cosmeticid) ON DELETE CASCADE,
    currencyid INT NOT NULL REFERENCES currencies(currencyid),
    price INT NOT NULL CHECK(price >= 0),
    PRIMARY KEY(cosmeticid, currencyid)
);
CREATE TABLE IF NOT EXISTS gamesettings (
    key VARCHAR(80) PRIMARY KEY, value JSONB NOT NULL
);
CREATE TABLE IF NOT EXISTS gameprogress (
    playerid UUID PRIMARY KEY REFERENCES playerprofiles(userid) ON DELETE CASCADE,
    data JSONB NOT NULL DEFAULT '{}'::jsonb,
    revision BIGINT NOT NULL DEFAULT 0,
    updatedat TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS gamecommands (
    playerid UUID NOT NULL REFERENCES playerprofiles(userid) ON DELETE CASCADE,
    commandid VARCHAR(64) NOT NULL,
    kind VARCHAR(40) NOT NULL,
    fingerprint VARCHAR(64) NOT NULL,
    result JSONB NOT NULL,
    createdat TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY(playerid,commandid)
);
CREATE TABLE IF NOT EXISTS trainingsessions (
    attemptid UUID PRIMARY KEY,
    playerid UUID NOT NULL REFERENCES playerprofiles(userid) ON DELETE CASCADE,
    lessonid INT NOT NULL REFERENCES lessons(lessonid),
    drillid INT NOT NULL REFERENCES drills(drillid),
    data JSONB NOT NULL,
    completed BOOLEAN NOT NULL DEFAULT FALSE,
    createdat TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updatedat TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(lessonid,drillid) REFERENCES lessondrills(lessonid,drillid)
);
CREATE INDEX IF NOT EXISTS trainingsessions_player_time_idx ON trainingsessions(playerid,createdat DESC);
CREATE TABLE IF NOT EXISTS gamematches (
    matchid UUID PRIMARY KEY,
    ownerid UUID REFERENCES playerprofiles(userid) ON DELETE SET NULL,
    kind VARCHAR(32) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'STARTED' CHECK(status IN ('STARTED','FINISHED','CANCELLED')),
    data JSONB NOT NULL,
    createdat TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updatedat TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
ALTER TABLE gamematches DROP CONSTRAINT IF EXISTS gamematches_ownerid_fkey;
ALTER TABLE gamematches ADD CONSTRAINT gamematches_ownerid_fkey FOREIGN KEY(ownerid) REFERENCES playerprofiles(userid) ON DELETE SET NULL;
CREATE TABLE IF NOT EXISTS gamematchplayers (
    matchid UUID NOT NULL REFERENCES gamematches(matchid) ON DELETE CASCADE,
    playerid UUID NOT NULL REFERENCES playerprofiles(userid) ON DELETE CASCADE,
    team SMALLINT NOT NULL CHECK(team IN (0,1)),
    accepted BOOLEAN NOT NULL DEFAULT FALSE,
    report JSONB,
    fingerprint VARCHAR(64),
    lastseenat TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY(matchid,playerid)
);
CREATE INDEX IF NOT EXISTS gamematchplayers_player_idx ON gamematchplayers(playerid,matchid);
ALTER TABLE playermissions ADD COLUMN IF NOT EXISTS periodkey VARCHAR(8) NOT NULL DEFAULT '';
CREATE TABLE IF NOT EXISTS currencytransactions (
    transactionid BIGSERIAL PRIMARY KEY,
    playerid UUID NOT NULL REFERENCES playerprofiles(userid) ON DELETE CASCADE,
    currencyid INT NOT NULL REFERENCES currencies(currencyid),
    commandid VARCHAR(64) NOT NULL,
    delta BIGINT NOT NULL,
    balanceafter BIGINT NOT NULL CHECK(balanceafter >= 0),
    reason VARCHAR(80) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(playerid,currencyid,commandid)
);
CREATE TABLE IF NOT EXISTS onlinereplays (
    matchid UUID NOT NULL REFERENCES gamematches(matchid) ON DELETE CASCADE,
    playerid UUID NOT NULL REFERENCES playerprofiles(userid) ON DELETE CASCADE,
    objectkey VARCHAR(500) NOT NULL,
    metadata JSONB NOT NULL,
    updatedat TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY(matchid,playerid)
);
CREATE TABLE IF NOT EXISTS onlinereplaychunks (
    matchid UUID NOT NULL,
    playerid UUID NOT NULL,
    chunkindex INT NOT NULL CHECK(chunkindex>=0 AND chunkindex<10000),
    objectkey VARCHAR(500) NOT NULL,
    contenthash VARCHAR(64) NOT NULL,
    metadata JSONB NOT NULL,
    PRIMARY KEY(matchid,playerid,chunkindex),
    FOREIGN KEY(matchid,playerid) REFERENCES onlinereplays(matchid,playerid) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS verificationemail_user_recent_idx ON verificationemail(userid,createdat DESC,verificationemailid DESC);
CREATE INDEX IF NOT EXISTS playerprofiles_leaderboard_idx ON playerprofiles(elo DESC,wincount DESC);
CREATE INDEX IF NOT EXISTS playermatchhistory_match_idx ON playermatchhistory(matchid,playerid);
CREATE INDEX IF NOT EXISTS playertechniqueserrors_player_recent_idx ON playertechniqueserrors(playerid,createdat DESC);
COMMIT;

-- END ONLINE GAME SCHEMA

-- BEGIN UNITY GAME CONTENT SEED
-- Exported from Unity Resources/Progression Config, Shop/Shop Catalog and TrainingCurriculum.
BEGIN;
INSERT INTO currencies(currencyname,description) VALUES ('TOKENPAD','TOKENPAD') ON CONFLICT(currencyname) DO NOTHING;
INSERT INTO currencies(currencyname,description) VALUES ('ELITE TOKEN','ELITE TOKEN') ON CONFLICT(currencyname) DO NOTHING;
INSERT INTO achievements(achievementcode,achievementname,description,type,target,reward) VALUES ('A.Win5','Thắng 5 trận','Thắng 5 trận','Wins',5,'{"coins":100,"gems":0,"xp":10}'::jsonb) ON CONFLICT(achievementcode) DO UPDATE SET achievementname=EXCLUDED.achievementname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward;
INSERT INTO achievements(achievementcode,achievementname,description,type,target,reward) VALUES ('A.Win25','Thắng 25 trận','Thắng 25 trận','Wins',25,'{"coins":300,"gems":0,"xp":25}'::jsonb) ON CONFLICT(achievementcode) DO UPDATE SET achievementname=EXCLUDED.achievementname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward;
INSERT INTO achievements(achievementcode,achievementname,description,type,target,reward) VALUES ('A.Win100','Thắng 100 trận','Thắng 100 trận','Wins',100,'{"coins":1000,"gems":0,"xp":50}'::jsonb) ON CONFLICT(achievementcode) DO UPDATE SET achievementname=EXCLUDED.achievementname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward;
INSERT INTO achievements(achievementcode,achievementname,description,type,target,reward) VALUES ('A.Play10','Hoàn tất 10 trận','Hoàn tất 10 trận','Matches',10,'{"coins":100,"gems":0,"xp":10}'::jsonb) ON CONFLICT(achievementcode) DO UPDATE SET achievementname=EXCLUDED.achievementname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward;
INSERT INTO achievements(achievementcode,achievementname,description,type,target,reward) VALUES ('A.Play50','Hoàn tất 50 trận','Hoàn tất 50 trận','Matches',50,'{"coins":400,"gems":0,"xp":25}'::jsonb) ON CONFLICT(achievementcode) DO UPDATE SET achievementname=EXCLUDED.achievementname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward;
INSERT INTO achievements(achievementcode,achievementname,description,type,target,reward) VALUES ('A.Play200','Hoàn tất 200 trận','Hoàn tất 200 trận','Matches',200,'{"coins":1500,"gems":0,"xp":75}'::jsonb) ON CONFLICT(achievementcode) DO UPDATE SET achievementname=EXCLUDED.achievementname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward;
INSERT INTO achievements(achievementcode,achievementname,description,type,target,reward) VALUES ('A.Point100','Ghi 100 điểm cho đội','Ghi 100 điểm cho đội','Points',100,'{"coins":200,"gems":0,"xp":15}'::jsonb) ON CONFLICT(achievementcode) DO UPDATE SET achievementname=EXCLUDED.achievementname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward;
INSERT INTO achievements(achievementcode,achievementname,description,type,target,reward) VALUES ('A.Point500','Ghi 500 điểm cho đội','Ghi 500 điểm cho đội','Points',500,'{"coins":600,"gems":0,"xp":35}'::jsonb) ON CONFLICT(achievementcode) DO UPDATE SET achievementname=EXCLUDED.achievementname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward;
INSERT INTO achievements(achievementcode,achievementname,description,type,target,reward) VALUES ('A.Streak5','Đạt chuỗi thắng 5 trận','Đạt chuỗi thắng 5 trận','BestStreak',5,'{"coins":500,"gems":0,"xp":30}'::jsonb) ON CONFLICT(achievementcode) DO UPDATE SET achievementname=EXCLUDED.achievementname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward;
INSERT INTO achievements(achievementcode,achievementname,description,type,target,reward) VALUES ('A.Higher5','Thắng 5 đối thủ rank Elo cao hơn','Thắng 5 đối thủ rank Elo cao hơn','HigherRatedWins',5,'{"coins":300,"gems":0,"xp":25}'::jsonb) ON CONFLICT(achievementcode) DO UPDATE SET achievementname=EXCLUDED.achievementname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('D.Play1','Hoàn tất 1 trận','Hoàn tất 1 trận','Daily',1,'{"coins":50,"gems":0,"xp":5}'::jsonb,'{"id":"D.Play1","title":"Hoàn tất 1 trận","scope":"Daily","metric":"Matches","target":1,"reward":{"coins":50,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('D.Win1','Thắng 1 trận','Thắng 1 trận','Daily',1,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Win1","title":"Thắng 1 trận","scope":"Daily","metric":"Wins","target":1,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('D.Point20','Ghi 20 điểm cho đội','Ghi 20 điểm cho đội','Daily',20,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Point20","title":"Ghi 20 điểm cho đội","scope":"Daily","metric":"Points","target":20,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('D.Return30','Trả bóng hợp lệ 30 lần','Trả bóng hợp lệ 30 lần','Daily',30,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Return30","title":"Trả bóng hợp lệ 30 lần","scope":"Daily","metric":"Returns","target":30,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('D.Serve10','Giao bóng hợp lệ 10 lần','Giao bóng hợp lệ 10 lần','Daily',10,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Serve10","title":"Giao bóng hợp lệ 10 lần","scope":"Daily","metric":"Serves","target":10,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('D.Bot1','Hoàn tất 1 trận với bot','Hoàn tất 1 trận với bot','Daily',1,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Bot1","title":"Hoàn tất 1 trận với bot","scope":"Daily","metric":"BotMatches","target":1,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('D.HumanWin1','Thắng 1 trận tìm đối thủ người thật','Thắng 1 trận tìm đối thủ người thật','Daily',1,'{"coins":125,"gems":0,"xp":10}'::jsonb,'{"id":"D.HumanWin1","title":"Thắng 1 trận tìm đối thủ người thật","scope":"Daily","metric":"HumanWins","target":1,"reward":{"coins":125,"gems":0,"xp":10}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('W.Play10','Hoàn tất 10 trận','Hoàn tất 10 trận','Weekly',10,'{"coins":300,"gems":0,"xp":20}'::jsonb,'{"id":"W.Play10","title":"Hoàn tất 10 trận","scope":"Weekly","metric":"Matches","target":10,"reward":{"coins":300,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('W.Win5','Thắng 5 trận','Thắng 5 trận','Weekly',5,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Win5","title":"Thắng 5 trận","scope":"Weekly","metric":"Wins","target":5,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('W.Point100','Ghi 100 điểm cho đội','Ghi 100 điểm cho đội','Weekly',100,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Point100","title":"Ghi 100 điểm cho đội","scope":"Weekly","metric":"Points","target":100,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('W.Return200','Trả bóng hợp lệ 200 lần','Trả bóng hợp lệ 200 lần','Weekly',200,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Return200","title":"Trả bóng hợp lệ 200 lần","scope":"Weekly","metric":"Returns","target":200,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('W.Serve50','Giao bóng hợp lệ 50 lần','Giao bóng hợp lệ 50 lần','Weekly',50,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Serve50","title":"Giao bóng hợp lệ 50 lần","scope":"Weekly","metric":"Serves","target":50,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('W.Bot5','Hoàn tất 5 trận với bot','Hoàn tất 5 trận với bot','Weekly',5,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Bot5","title":"Hoàn tất 5 trận với bot","scope":"Weekly","metric":"BotMatches","target":5,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('W.Human5','Hoàn tất 5 trận tìm đối thủ người thật','Hoàn tất 5 trận tìm đối thủ người thật','Weekly',5,'{"coins":350,"gems":0,"xp":20}'::jsonb,'{"id":"W.Human5","title":"Hoàn tất 5 trận tìm đối thủ người thật","scope":"Weekly","metric":"HumanMatches","target":5,"reward":{"coins":350,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('W.RankWin3','Thắng 3 trận rank 1v1','Thắng 3 trận rank 1v1','Weekly',3,'{"coins":350,"gems":0,"xp":20}'::jsonb,'{"id":"W.RankWin3","title":"Thắng 3 trận rank 1v1","scope":"Weekly","metric":"RankedWins","target":3,"reward":{"coins":350,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('W.DoubleWin2','Thắng 2 trận tìm trận 2v2','Thắng 2 trận tìm trận 2v2','Weekly',2,'{"coins":400,"gems":0,"xp":20}'::jsonb,'{"id":"W.DoubleWin2","title":"Thắng 2 trận tìm trận 2v2","scope":"Weekly","metric":"DoublesWins","target":2,"reward":{"coins":400,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO missions(code,missionname,description,type,target,reward,config) VALUES ('W.Streak3','Đạt chuỗi thắng 3 trận trong tuần','Đạt chuỗi thắng 3 trận trong tuần','Weekly',3,'{"coins":350,"gems":0,"xp":20}'::jsonb,'{"id":"W.Streak3","title":"Đạt chuỗi thắng 3 trận trong tuần","scope":"Weekly","metric":"BestStreak","target":3,"reward":{"coins":350,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(code) DO UPDATE SET missionname=EXCLUDED.missionname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,config=EXCLUDED.config;
INSERT INTO cosmetics(currencyid,itemcode,cosmeticname,description,price,type,config) SELECT currencyid,'paddle.default','PICKLEVERSE','Vợt mặc định của PickleVerse. Thiết kế gọn gàng, luôn sẵn sàng cho những trận đấu đầu tiên.',0,'Paddle','{"id":"paddle.default","title":"PICKLEVERSE","description":"Vợt mặc định của PickleVerse. Thiết kế gọn gàng, luôn sẵn sàng cho những trận đấu đầu tiên.","collection":"ORIGINAL COLLECTION","tokenpadPrice":0,"elitePrice":0,"gemstone":false,"releaseOrder":0}'::jsonb FROM currencies WHERE currencyname='TOKENPAD' ON CONFLICT(itemcode) DO UPDATE SET currencyid=EXCLUDED.currencyid,cosmeticname=EXCLUDED.cosmeticname,description=EXCLUDED.description,price=EXCLUDED.price,type=EXCLUDED.type,config=EXCLUDED.config;
INSERT INTO cosmeticprices(cosmeticid,currencyid,price) SELECT c.cosmeticid,u.currencyid,0 FROM cosmetics c CROSS JOIN currencies u WHERE c.itemcode='paddle.default' AND u.currencyname='TOKENPAD' ON CONFLICT(cosmeticid,currencyid) DO UPDATE SET price=EXCLUDED.price;
INSERT INTO cosmetics(currencyid,itemcode,cosmeticname,description,price,type,config) SELECT currencyid,'paddle.gemstone','GEMSTONE','Viền đá quý, sắc vàng và mặt carbon. Ánh lấp lánh tinh tế khi xoay dưới ánh sáng.',650,'Paddle','{"id":"paddle.gemstone","title":"GEMSTONE","description":"Viền đá quý, sắc vàng và mặt carbon. Ánh lấp lánh tinh tế khi xoay dưới ánh sáng.","collection":"ELITE COLLECTION","tokenpadPrice":0,"elitePrice":650,"gemstone":true,"releaseOrder":2}'::jsonb FROM currencies WHERE currencyname='ELITE TOKEN' ON CONFLICT(itemcode) DO UPDATE SET currencyid=EXCLUDED.currencyid,cosmeticname=EXCLUDED.cosmeticname,description=EXCLUDED.description,price=EXCLUDED.price,type=EXCLUDED.type,config=EXCLUDED.config;
INSERT INTO cosmeticprices(cosmeticid,currencyid,price) SELECT c.cosmeticid,u.currencyid,650 FROM cosmetics c CROSS JOIN currencies u WHERE c.itemcode='paddle.gemstone' AND u.currencyname='ELITE TOKEN' ON CONFLICT(cosmeticid,currencyid) DO UPDATE SET price=EXCLUDED.price;
INSERT INTO cosmetics(currencyid,itemcode,cosmeticname,description,price,type,config) SELECT currencyid,'paddle.thot','THOT','Hoa văn nổi bật cho phong cách thi đấu riêng. Chọn Tokenpad hoặc Elite để sở hữu.',900,'Paddle','{"id":"paddle.thot","title":"THOT","description":"Hoa văn nổi bật cho phong cách thi đấu riêng. Chọn Tokenpad hoặc Elite để sở hữu.","collection":"SIGNATURE COLLECTION","tokenpadPrice":900,"elitePrice":250,"gemstone":false,"releaseOrder":1}'::jsonb FROM currencies WHERE currencyname='TOKENPAD' ON CONFLICT(itemcode) DO UPDATE SET currencyid=EXCLUDED.currencyid,cosmeticname=EXCLUDED.cosmeticname,description=EXCLUDED.description,price=EXCLUDED.price,type=EXCLUDED.type,config=EXCLUDED.config;
INSERT INTO cosmeticprices(cosmeticid,currencyid,price) SELECT c.cosmeticid,u.currencyid,900 FROM cosmetics c CROSS JOIN currencies u WHERE c.itemcode='paddle.thot' AND u.currencyname='TOKENPAD' ON CONFLICT(cosmeticid,currencyid) DO UPDATE SET price=EXCLUDED.price;
INSERT INTO cosmeticprices(cosmeticid,currencyid,price) SELECT c.cosmeticid,u.currencyid,250 FROM cosmetics c CROSS JOIN currencies u WHERE c.itemcode='paddle.thot' AND u.currencyname='ELITE TOKEN' ON CONFLICT(cosmeticid,currencyid) DO UPDATE SET price=EXCLUDED.price;
INSERT INTO techniques(techniquename,description) VALUES ('Serve','Giao bóng đúng ô') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('serve','Giao bóng đúng ô','Giao dưới tay từ sau baseline. Bóng phải sang ô chéo và vượt cả vạch Kitchen.','Serve',6,'{"xp":5,"gems":0,"coins":150}'::jsonb,1,'{"id":"serve","title":"Giao bóng đúng ô","explanation":"Giao dưới tay từ sau baseline. Bóng phải sang ô chéo và vượt cả vạch Kitchen.","coaching":"Đứng trong vùng sáng phía sau baseline. Giữ rồi thả Drive; không bước vào sân trước khi chạm bóng.","skill":"Serve","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[1],"prerequisites":[]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'serve',techniqueid,'Giao bóng đúng ô','Đứng trong vùng sáng phía sau baseline. Giữ rồi thả Drive; không bước vào sân trước khi chạm bóng.' FROM techniques WHERE techniquename='Serve' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,6 FROM lessons l CROSS JOIN drills d WHERE l.code='serve' AND d.code='serve' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO techniques(techniquename,description) VALUES ('Drive','Drive về cuối sân') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('drive','Drive về cuối sân','Đón bóng sau một lần nảy, đánh Drive vào vùng sâu đã đánh dấu.','Drive',6,'{"xp":5,"gems":0,"coins":150}'::jsonb,2,'{"id":"drive","title":"Drive về cuối sân","explanation":"Đón bóng sau một lần nảy, đánh Drive vào vùng sâu đã đánh dấu.","coaching":"Chuẩn bị vợt phía trước. Thả phím sớm hơn lúc bóng tới để animation đưa vợt đến điểm tiếp xúc.","skill":"Drive","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[1],"prerequisites":[]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'drive',techniqueid,'Drive về cuối sân','Chuẩn bị vợt phía trước. Thả phím sớm hơn lúc bóng tới để animation đưa vợt đến điểm tiếp xúc.' FROM techniques WHERE techniquename='Drive' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,6 FROM lessons l CROSS JOIN drills d WHERE l.code='drive' AND d.code='drive' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO techniques(techniquename,description) VALUES ('Dink','Dink kiểm soát Kitchen') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('dink','Dink kiểm soát Kitchen','Đánh nhẹ sau nảy, đưa bóng vào Kitchen đối phương. Được đứng trong Kitchen khi đánh bóng đã nảy.','Dink',6,'{"xp":5,"gems":0,"coins":150}'::jsonb,3,'{"id":"dink","title":"Dink kiểm soát Kitchen","explanation":"Đánh nhẹ sau nảy, đưa bóng vào Kitchen đối phương. Được đứng trong Kitchen khi đánh bóng đã nảy.","coaching":"Đón bóng trước người. Không dùng lực mạnh; ưu tiên kiểm soát và cung bóng thấp.","skill":"Dink","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[2],"prerequisites":[]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'dink',techniqueid,'Dink kiểm soát Kitchen','Đón bóng trước người. Không dùng lực mạnh; ưu tiên kiểm soát và cung bóng thấp.' FROM techniques WHERE techniquename='Dink' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,6 FROM lessons l CROSS JOIN drills d WHERE l.code='dink' AND d.code='dink' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO techniques(techniquename,description) VALUES ('Volley','Volley ngoài Kitchen') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('volley','Volley ngoài Kitchen','Chặn bóng trước khi nảy. Cả hai chân và đà sau cú đánh phải ở ngoài Kitchen.','Volley',6,'{"xp":5,"gems":0,"coins":150}'::jsonb,4,'{"id":"volley","title":"Volley ngoài Kitchen","explanation":"Chặn bóng trước khi nảy. Cả hai chân và đà sau cú đánh phải ở ngoài Kitchen.","coaching":"Giữ vị trí sau vạch sáng. Chọn Volley khi bóng còn trên không; không lao vào Kitchen sau cú đánh.","skill":"Volley","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[4],"prerequisites":[]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'volley',techniqueid,'Volley ngoài Kitchen','Giữ vị trí sau vạch sáng. Chọn Volley khi bóng còn trên không; không lao vào Kitchen sau cú đánh.' FROM techniques WHERE techniquename='Volley' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,6 FROM lessons l CROSS JOIN drills d WHERE l.code='volley' AND d.code='volley' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO techniques(techniquename,description) VALUES ('Modifiers','Lực và hướng chéo') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('modifiers','Lực và hướng chéo','Luân phiên Drive, Dink, Volley với tăng lực hoặc đánh chéo theo chỉ dẫn từng bóng.','Modifiers',7,'{"xp":7,"gems":0,"coins":225}'::jsonb,5,'{"id":"modifiers","title":"Lực và hướng chéo","explanation":"Luân phiên Drive, Dink, Volley với tăng lực hoặc đánh chéo theo chỉ dẫn từng bóng.","coaching":"Giữ modifier cho đến khi thả nút đánh. Quan sát vùng đích đổi bên khi cần đánh chéo.","skill":"Modifiers","tier":1,"trials":10,"required":7,"reward":225,"xp":7,"masks":[9,17,10,18,12,20],"prerequisites":["drive","dink","volley"]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'modifiers',techniqueid,'Lực và hướng chéo','Giữ modifier cho đến khi thả nút đánh. Quan sát vùng đích đổi bên khi cần đánh chéo.' FROM techniques WHERE techniquename='Modifiers' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,7 FROM lessons l CROSS JOIN drills d WHERE l.code='modifiers' AND d.code='modifiers' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO techniques(techniquename,description) VALUES ('Drop','Third-shot drop') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('drop','Third-shot drop','Sau giao và trả giao đã nảy, đánh cú thứ ba mềm vào Kitchen để lấy thời gian lên lưới.','Drop',7,'{"xp":7,"gems":0,"coins":225}'::jsonb,6,'{"id":"drop","title":"Third-shot drop","explanation":"Sau giao và trả giao đã nảy, đánh cú thứ ba mềm vào Kitchen để lấy thời gian lên lưới.","coaching":"Ghép Drive + Dink. Đây là cú đánh sau nảy; bóng phải rơi trong vùng sáng gần lưới.","skill":"Drop","tier":1,"trials":10,"required":7,"reward":225,"xp":7,"masks":[3],"prerequisites":["drive","dink"]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'drop',techniqueid,'Third-shot drop','Ghép Drive + Dink. Đây là cú đánh sau nảy; bóng phải rơi trong vùng sáng gần lưới.' FROM techniques WHERE techniquename='Drop' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,7 FROM lessons l CROSS JOIN drills d WHERE l.code='drop' AND d.code='drop' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO techniques(techniquename,description) VALUES ('Reset','Reset bóng nhanh') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('reset','Reset bóng nhanh','Dùng Reset để giảm nhịp bóng nhanh, đưa bóng về Kitchen thay vì cố đánh mạnh.','Reset',7,'{"xp":7,"gems":0,"coins":225}'::jsonb,7,'{"id":"reset","title":"Reset bóng nhanh","explanation":"Dùng Reset để giảm nhịp bóng nhanh, đưa bóng về Kitchen thay vì cố đánh mạnh.","coaching":"Ghép Dink + Volley, tiếp bóng trên không và giữ cả hai chân ngoài Kitchen.","skill":"Reset","tier":1,"trials":10,"required":7,"reward":225,"xp":7,"masks":[6],"prerequisites":["dink","volley"]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'reset',techniqueid,'Reset bóng nhanh','Ghép Dink + Volley, tiếp bóng trên không và giữ cả hai chân ngoài Kitchen.' FROM techniques WHERE techniquename='Reset' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,7 FROM lessons l CROSS JOIN drills d WHERE l.code='reset' AND d.code='reset' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO techniques(techniquename,description) VALUES ('Smash','Smash bóng bổng') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('smash','Smash bóng bổng','Đợi bóng bổng đủ cao rồi đánh Smash vào vùng sâu. Không volley trong Kitchen.','Smash',9,'{"xp":10,"gems":0,"coins":300}'::jsonb,8,'{"id":"smash","title":"Smash bóng bổng","explanation":"Đợi bóng bổng đủ cao rồi đánh Smash vào vùng sâu. Không volley trong Kitchen.","coaching":"Ghép Drive + Volley. Quan sát vòng điểm tiếp bóng trên sân và thả khi chỉ báo timing vào vùng xanh.","skill":"Smash","tier":2,"trials":12,"required":9,"reward":300,"xp":10,"masks":[5],"prerequisites":["volley","modifiers"]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'smash',techniqueid,'Smash bóng bổng','Ghép Drive + Volley. Quan sát vòng điểm tiếp bóng trên sân và thả khi chỉ báo timing vào vùng xanh.' FROM techniques WHERE techniquename='Smash' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,9 FROM lessons l CROSS JOIN drills d WHERE l.code='smash' AND d.code='smash' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO techniques(techniquename,description) VALUES ('Footwork','Di chuyển và hồi vị') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('footwork','Di chuyển và hồi vị','Đón bóng luân phiên hai bên sân, đánh Drive rồi trở về vị trí hồi phục.','Footwork',9,'{"xp":10,"gems":0,"coins":300}'::jsonb,9,'{"id":"footwork","title":"Di chuyển và hồi vị","explanation":"Đón bóng luân phiên hai bên sân, đánh Drive rồi trở về vị trí hồi phục.","coaching":"Di chuyển A/D hoặc joystick tới vùng đón bóng. Có thể dùng chạy; sau cú đánh quay về vòng hồi vị.","skill":"Footwork","tier":2,"trials":12,"required":9,"reward":300,"xp":10,"masks":[1,17],"prerequisites":["drive","modifiers"]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'footwork',techniqueid,'Di chuyển và hồi vị','Di chuyển A/D hoặc joystick tới vùng đón bóng. Có thể dùng chạy; sau cú đánh quay về vòng hồi vị.' FROM techniques WHERE techniquename='Footwork' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,9 FROM lessons l CROSS JOIN drills d WHERE l.code='footwork' AND d.code='footwork' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO techniques(techniquename,description) VALUES ('Approach','Drop rồi tiến lên') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('approach','Drop rồi tiến lên','Đánh drop từ cuối sân, tiến lên vùng sẵn sàng trước Kitchen. Dừng có kiểm soát trước lượt tiếp theo.','Approach',9,'{"xp":10,"gems":0,"coins":300}'::jsonb,10,'{"id":"approach","title":"Drop rồi tiến lên","explanation":"Đánh drop từ cuối sân, tiến lên vùng sẵn sàng trước Kitchen. Dừng có kiểm soát trước lượt tiếp theo.","coaching":"Ghép Drive + Dink rồi di chuyển W lên vùng sáng. Đừng chạy qua vạch Kitchen để đón volley.","skill":"Approach","tier":2,"trials":12,"required":9,"reward":300,"xp":10,"masks":[3],"prerequisites":["drop","footwork"]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'approach',techniqueid,'Drop rồi tiến lên','Ghép Drive + Dink rồi di chuyển W lên vùng sáng. Đừng chạy qua vạch Kitchen để đón volley.' FROM techniques WHERE techniquename='Approach' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,9 FROM lessons l CROSS JOIN drills d WHERE l.code='approach' AND d.code='approach' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO techniques(techniquename,description) VALUES ('DoublesCover','Đánh đôi: giữ khoảng cách') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('doubles-cover','Đánh đôi: giữ khoảng cách','Hai người giữ hàng ngang, dịch chuyển cùng phía bóng. Đánh bóng phần sân của bạn và giữ khe giữa đội nhỏ.','DoublesCover',9,'{"xp":15,"gems":0,"coins":450}'::jsonb,11,'{"id":"doubles-cover","title":"Đánh đôi: giữ khoảng cách","explanation":"Hai người giữ hàng ngang, dịch chuyển cùng phía bóng. Đánh bóng phần sân của bạn và giữ khe giữa đội nhỏ.","coaching":"Đồng đội AI đi cùng bạn. Dịch chuyển ngang, đánh Volley theo chỉ dẫn, rồi hồi vị để cả đội giữ hàng ngang.","skill":"DoublesCover","tier":3,"trials":12,"required":9,"reward":450,"xp":15,"masks":[4,20],"prerequisites":["volley","footwork"]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'doubles-cover',techniqueid,'Đánh đôi: giữ khoảng cách','Đồng đội AI đi cùng bạn. Dịch chuyển ngang, đánh Volley theo chỉ dẫn, rồi hồi vị để cả đội giữ hàng ngang.' FROM techniques WHERE techniquename='DoublesCover' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,9 FROM lessons l CROSS JOIN drills d WHERE l.code='doubles-cover' AND d.code='doubles-cover' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO techniques(techniquename,description) VALUES ('DoublesTransition','Đánh đôi: drop và phối hợp') ON CONFLICT(techniquename) DO NOTHING;
INSERT INTO lessons(code,lessonname,description,type,target,reward,sortorder,config) VALUES ('doubles-transition','Đánh đôi: drop và phối hợp','Đội giao chờ trả giao nảy, dùng drop tiến lên cùng nhau rồi chuyển sang dink và volley. Có lượt bóng của đồng đội AI.','DoublesTransition',9,'{"xp":15,"gems":0,"coins":450}'::jsonb,12,'{"id":"doubles-transition","title":"Đánh đôi: drop và phối hợp","explanation":"Đội giao chờ trả giao nảy, dùng drop tiến lên cùng nhau rồi chuyển sang dink và volley. Có lượt bóng của đồng đội AI.","coaching":"Theo dấu vị trí của bạn. Sau drop tiến lên cùng đồng đội; khi hiện ‘Bóng của đồng đội’, giữ hàng ngang và để AI xử lý.","skill":"DoublesTransition","tier":3,"trials":12,"required":9,"reward":450,"xp":15,"masks":[3,2,4],"prerequisites":["approach","doubles-cover","reset"]}'::jsonb) ON CONFLICT(code) DO UPDATE SET lessonname=EXCLUDED.lessonname,description=EXCLUDED.description,type=EXCLUDED.type,target=EXCLUDED.target,reward=EXCLUDED.reward,sortorder=EXCLUDED.sortorder,config=EXCLUDED.config;
INSERT INTO drills(code,techniqueid,drillname,description) SELECT 'doubles-transition',techniqueid,'Đánh đôi: drop và phối hợp','Theo dấu vị trí của bạn. Sau drop tiến lên cùng đồng đội; khi hiện ‘Bóng của đồng đội’, giữ hàng ngang và để AI xử lý.' FROM techniques WHERE techniquename='DoublesTransition' ON CONFLICT(code) DO UPDATE SET techniqueid=EXCLUDED.techniqueid,drillname=EXCLUDED.drillname,description=EXCLUDED.description;
INSERT INTO lessondrills(lessonid,drillid,target) SELECT l.lessonid,d.drillid,9 FROM lessons l CROSS JOIN drills d WHERE l.code='doubles-transition' AND d.code='doubles-transition' ON CONFLICT(lessonid,drillid) DO UPDATE SET target=EXCLUDED.target;
INSERT INTO gamesettings(key,value) VALUES ('progression','{"lossXp":5,"matchRewards":{"botWinReward":100,"botLossReward":50,"onlineWinReward":150,"onlineLossReward":75},"weeklyBonus":{"coins":1000,"gems":0,"xp":50},"winXp":10,"dailyBonus":{"coins":200,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(key) DO UPDATE SET value=EXCLUDED.value;
INSERT INTO playercosmetics(playerid,cosmeticid,equipped) SELECT p.userid,c.cosmeticid,NOT EXISTS(SELECT 1 FROM playercosmetics pc WHERE pc.playerid=p.userid AND pc.equipped) FROM playerprofiles p CROSS JOIN cosmetics c WHERE c.itemcode='paddle.default' ON CONFLICT(playerid,cosmeticid) DO NOTHING;
COMMIT;
-- END UNITY GAME CONTENT SEED
