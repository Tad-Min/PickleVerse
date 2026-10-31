BEGIN;

-- Kích hoạt extension pgcrypto để sinh UUID ngẫu nhiên
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

--#region Authentication-Users
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
    Token VARCHAR(128) NOT NULL,
    FailedAttempts INT NOT NULL DEFAULT 0,
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
    Revision BIGINT NOT NULL DEFAULT 0,
    TutorialCompleted BOOLEAN NOT NULL DEFAULT FALSE,
    TutorialSkipped BOOLEAN NOT NULL DEFAULT FALSE,
    DailyStreakEnabledAt TIMESTAMPTZ NULL,
    RankWinStreak INT NOT NULL DEFAULT 0,
    RankLossStreak INT NOT NULL DEFAULT 0,
    BestWinStreak INT NOT NULL DEFAULT 0,
    LifetimeMatches BIGINT NOT NULL DEFAULT 0,
    LifetimePoints BIGINT NOT NULL DEFAULT 0,
    LifetimeReturns BIGINT NOT NULL DEFAULT 0,
    LifetimeServes BIGINT NOT NULL DEFAULT 0,
    LifetimeBotMatches BIGINT NOT NULL DEFAULT 0,
    LifetimeHumanMatches BIGINT NOT NULL DEFAULT 0,
    LifetimeHumanWins BIGINT NOT NULL DEFAULT 0,
    LifetimeRankedWins BIGINT NOT NULL DEFAULT 0,
    LifetimeDoublesWins BIGINT NOT NULL DEFAULT 0,
    LifetimeHigherRatedWins BIGINT NOT NULL DEFAULT 0,
    CONSTRAINT FK_PlayerProfiles_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);
--#endregion

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
CREATE SEQUENCE GameMatches_History_Seq;
CREATE TABLE GameMatches (
    MatchId UUID PRIMARY KEY,
    Kind VARCHAR(32) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'PREPARING' CHECK(Status IN ('PREPARING','PLAYING','FINISHED','CANCELLED')),
    RatingPolicyVersion VARCHAR(64) NOT NULL DEFAULT 'legacy-v1',
    Mode SMALLINT NOT NULL DEFAULT 1 CHECK (Mode IN (1,2)),
    PhotonSession VARCHAR(100),
    SearchId UUID,
    Region VARCHAR(30),
    Night BOOLEAN NOT NULL DEFAULT FALSE,
    Venue SMALLINT NOT NULL DEFAULT 0 CHECK (Venue IN (0,1)),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    StartedAt TIMESTAMPTZ NULL,
    EndedAt TIMESTAMPTZ NULL,
    Sequence BIGINT UNIQUE,
    CONSTRAINT CHK_GameMatches_Finished CHECK (Status <> 'FINISHED' OR (EndedAt IS NOT NULL AND Sequence IS NOT NULL))
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
    CONSTRAINT FK_Report_GameMatches FOREIGN KEY (MatchId) REFERENCES GameMatches(MatchId) ON DELETE CASCADE
);

CREATE TABLE MatchHistoryReplay (
    MatchId UUID PRIMARY KEY,
    ObjectKey VARCHAR(500) NOT NULL,
    CONSTRAINT FK_MatchHistoryReplay_GameMatches FOREIGN KEY (MatchId) REFERENCES GameMatches(MatchId) ON DELETE CASCADE
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
    Config JSONB NOT NULL DEFAULT '{}'::jsonb,
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
    Reward JSONB NOT NULL DEFAULT '{"Exp": 0, "Currency": 0}'::jsonb,
    Code VARCHAR(50),
    Config JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE PlayerMissions (
    PlayerId UUID NOT NULL,
    MissionId INT NOT NULL,
    Progress INT NOT NULL DEFAULT 0,
    IsClaimed BOOLEAN NOT NULL DEFAULT FALSE,
    ClaimedAt TIMESTAMPTZ NULL,
    PeriodKey VARCHAR(8) NOT NULL DEFAULT '',
    CurrentStreak INT NOT NULL DEFAULT 0,
    PRIMARY KEY (PlayerId, MissionId, PeriodKey),
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
    Code VARCHAR(50),
    SortOrder INT NOT NULL DEFAULT 0,
    Type VARCHAR(25) NOT NULL,
    Target INT NOT NULL,
    Config JSONB NOT NULL DEFAULT '{}'::jsonb,
    Reward JSONB NOT NULL DEFAULT '{"Exp": 0, "Currency": 0}'::jsonb
);

CREATE TABLE Drills (
    DrillId SERIAL PRIMARY KEY,
    TechniqueId INT NOT NULL,
    DrillName VARCHAR(254) NOT NULL,
    Description VARCHAR(254) NOT NULL,
    Code VARCHAR(50),
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
    CONSTRAINT FK_PlayerTechniquesErrors_GameMatches FOREIGN KEY (MatchId) REFERENCES GameMatches(MatchId) ON DELETE SET NULL,
    CONSTRAINT FK_PlayerTechniquesErrors_Lessons FOREIGN KEY (LessonId) REFERENCES Lessons(LessonId) ON DELETE SET NULL
);

CREATE TABLE PlayerLessonDrills (
    PlayerId UUID NOT NULL,
    LessonId INT NOT NULL,
    DrillId INT NOT NULL,
    Score INT NOT NULL DEFAULT 0,
    IsClaimedReward BOOLEAN NOT NULL DEFAULT FALSE,
    Attempts INT NOT NULL DEFAULT 0,
    Retries INT NOT NULL DEFAULT 0,
    Completions INT NOT NULL DEFAULT 0,
    Trials INT NOT NULL DEFAULT 0,
    Successes INT NOT NULL DEFAULT 0,
    Seconds DOUBLE PRECISION NOT NULL DEFAULT 0,
    BestAccuracy DOUBLE PRECISION NOT NULL DEFAULT 0,
    Errors JSONB NOT NULL DEFAULT '[]'::jsonb,
    PRIMARY KEY (PlayerId, LessonId, DrillId),
    CONSTRAINT FK_PlayerLessonDrills_PlayerProfiles FOREIGN KEY (PlayerId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_PlayerLessonDrills_LessonDrills FOREIGN KEY (LessonId, DrillId) REFERENCES LessonDrills(LessonId, DrillId) ON DELETE CASCADE
);
--#endregion

--#region Online Game Services
CREATE UNIQUE INDEX IF NOT EXISTS Lessons_Code_Key ON Lessons(Code);
CREATE UNIQUE INDEX IF NOT EXISTS Drills_Code_Key ON Drills(Code);
CREATE UNIQUE INDEX IF NOT EXISTS Missions_Code_Key ON Missions(Code);
CREATE TABLE IF NOT EXISTS CosmeticPrices (
    CosmeticId INT NOT NULL REFERENCES Cosmetics(CosmeticId) ON DELETE CASCADE,
    CurrencyId INT NOT NULL REFERENCES Currencies(CurrencyId),
    Price INT NOT NULL CHECK(Price >= 0),
    PRIMARY KEY(CosmeticId, CurrencyId)
);
CREATE TABLE IF NOT EXISTS GameCommands (
    PlayerId UUID NOT NULL REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CommandId VARCHAR(64) NOT NULL,
    Kind VARCHAR(40) NOT NULL,
    Fingerprint VARCHAR(64) NOT NULL,
    Result JSONB NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY(PlayerId,CommandId)
);
CREATE TABLE IF NOT EXISTS TrainingSessions (
    AttemptId UUID PRIMARY KEY,
    Sequence BIGINT GENERATED ALWAYS AS IDENTITY,
    PlayerId UUID NOT NULL REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    LessonId INT NOT NULL REFERENCES Lessons(LessonId),
    DrillId INT NOT NULL REFERENCES Drills(DrillId),
    Data JSONB NOT NULL,
    Completed BOOLEAN NOT NULL DEFAULT FALSE,
    IsCurrent BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(LessonId,DrillId) REFERENCES LessonDrills(LessonId,DrillId)
);
CREATE UNIQUE INDEX IF NOT EXISTS TrainingSessions_Current_Idx ON TrainingSessions(PlayerId) WHERE IsCurrent;
CREATE INDEX IF NOT EXISTS TrainingSessions_Player_Time_Idx ON TrainingSessions(PlayerId,CreatedAt DESC);
-- App activity session counters/coverage live in Redis; only the total is persisted in PlayerProfiles.

CREATE TABLE MatchScore (
    MatchId UUID NOT NULL REFERENCES GameMatches(MatchId) ON DELETE CASCADE,
    GameNumber SMALLINT NOT NULL CHECK (GameNumber BETWEEN 1 AND 3),
    TeamAScore INT NOT NULL CHECK (TeamAScore BETWEEN 0 AND 100000),
    TeamBScore INT NOT NULL CHECK (TeamBScore BETWEEN 0 AND 100000),
    Completed BOOLEAN NOT NULL DEFAULT FALSE,
    PRIMARY KEY (MatchId, GameNumber)
);
CREATE TABLE IF NOT EXISTS GameMatchPlayers (
    MatchId UUID NOT NULL REFERENCES GameMatches(MatchId) ON DELETE CASCADE,
    PlayerId UUID NOT NULL REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    Team SMALLINT NOT NULL CHECK(Team IN (0,1)),
    EloBefore INT NOT NULL DEFAULT 100 CHECK (EloBefore >= 100),
    GroupId TEXT,
    RosterIndex SMALLINT NOT NULL DEFAULT 0 CHECK (RosterIndex BETWEEN 0 AND 3),
    IsReplayWriter BOOLEAN NOT NULL DEFAULT FALSE,
    ValidReturns INT NOT NULL DEFAULT 0 CHECK (ValidReturns BETWEEN 0 AND 1000000),
    ValidServes INT NOT NULL DEFAULT 0 CHECK (ValidServes BETWEEN 0 AND 1000000),
    EndReason VARCHAR(500) NOT NULL DEFAULT '',
    ReportedAt TIMESTAMPTZ,
    LastSeenAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Outcome VARCHAR(16) NULL CHECK (Outcome IN ('WIN','LOSE','INTERRUPTED')),
    LocalForfeit BOOLEAN NOT NULL DEFAULT FALSE,
    FinalizedAt TIMESTAMPTZ NULL,
    CONSTRAINT CHK_GameMatchPlayers_Finalized CHECK (
        (FinalizedAt IS NULL AND Outcome IS NULL) OR
        (FinalizedAt IS NOT NULL AND Outcome IS NOT NULL)),
    PRIMARY KEY(MatchId,PlayerId)
);
CREATE INDEX IF NOT EXISTS GameMatchPlayers_Player_Idx ON GameMatchPlayers(PlayerId,MatchId);
CREATE UNIQUE INDEX GameMatchPlayers_ReplayWriter_Idx ON GameMatchPlayers(MatchId) WHERE IsReplayWriter;
CREATE TABLE IF NOT EXISTS CurrencyTransactions (
    TransactionId BIGSERIAL PRIMARY KEY,
    PlayerId UUID NOT NULL REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CurrencyId INT NOT NULL REFERENCES Currencies(CurrencyId),
    CommandId VARCHAR(64) NOT NULL,
    Delta BIGINT NOT NULL,
    BalanceAfter BIGINT NOT NULL CHECK(BalanceAfter >= 0),
    Reason VARCHAR(80) NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(PlayerId,CurrencyId,CommandId)
);
CREATE INDEX IF NOT EXISTS VerificationEmail_User_Recent_Idx ON VerificationEmail(UserId,CreatedAt DESC,VerificationEmailId DESC);
CREATE INDEX IF NOT EXISTS PlayerProfiles_Leaderboard_Idx ON PlayerProfiles(Elo DESC,WinCount DESC);
CREATE INDEX GameMatches_History_Idx ON GameMatches(EndedAt DESC,Sequence DESC) WHERE Status='FINISHED';
CREATE INDEX GameMatchPlayers_History_Idx ON GameMatchPlayers(PlayerId,FinalizedAt DESC,MatchId) WHERE FinalizedAt IS NOT NULL;
CREATE INDEX IF NOT EXISTS PlayerTechniquesErrors_Player_Recent_Idx ON PlayerTechniquesErrors(PlayerId,CreatedAt DESC);
--#endregion

-- BEGIN INITIAL DATA
INSERT INTO Roles (RoleName, Description) VALUES
    ('Player', 'Nguoi choi PickleVerse'),
    ('Admin', 'Quan tri vien PickleVerse')
ON CONFLICT (RoleName) DO NOTHING;

-- Seed the fixed identities shared with Unity/API RankedBotAccounts before the Unity content seed.
INSERT INTO Users(UserId,Email,PasswordHash,RoleId)
SELECT id, 'rank-bot-'||slot||'@players.pickleverse.invalid', '!virtual-account-login-disabled', r.RoleId
FROM (VALUES
 (1,'3ec8bb2d-be02-444c-9e5d-e28f61a91a56'::uuid), (2,'144b9001-fa77-4116-b7e1-e73e7b28ac63'::uuid),
 (3,'a1851f31-a754-464b-a32d-498d5d6fb7ef'::uuid), (4,'8517f877-4476-4199-b8e0-8760de323acb'::uuid),
 (5,'87c71481-088d-4356-9b01-038316fc2863'::uuid), (6,'30b4f6ca-65f8-4a53-8521-fc38c5224560'::uuid),
 (7,'64a7c55b-33a6-434b-b191-e25067af69e1'::uuid), (8,'9034cf52-3f53-47e2-a043-265d3eed515b'::uuid),
 (9,'09da8e83-2ab6-4fe6-a55a-7d6dfcab469a'::uuid), (10,'ace8686c-10d5-4d63-a8d1-cd10a1980afa'::uuid)
) AS bots(slot,id) CROSS JOIN Roles r WHERE r.RoleName='Player'
ON CONFLICT(UserId) DO NOTHING;
INSERT INTO PlayerProfiles(UserId,PlayerName,IsMale)
SELECT UserId,'player'||substring(replace(UserId::text,'-',''),1,12), (row_number() OVER(ORDER BY UserId)%2=0)
FROM Users WHERE Email LIKE 'rank-bot-%@players.pickleverse.invalid' AND PasswordHash='!virtual-account-login-disabled'
ON CONFLICT(UserId) DO NOTHING;

-- Fail initialization if any required virtual account lacks its Player role or profile.
DO $$
BEGIN
    IF (
        SELECT count(*)
        FROM Users u
        JOIN Roles r ON r.RoleId=u.RoleId
        JOIN PlayerProfiles p ON p.UserId=u.UserId
        WHERE r.RoleName='Player'
          AND u.PasswordHash='!virtual-account-login-disabled'
          AND u.Email IN (
              SELECT 'rank-bot-'||slot||'@players.pickleverse.invalid'
              FROM generate_series(1,10) AS slots(slot)
          )
    ) <> 10 THEN
        RAISE EXCEPTION 'Database initialization requires all 10 virtual Player accounts and profiles.';
    END IF;
END;
$$;

-- BEGIN UNITY GAME CONTENT SEED
-- Exported from Unity Resources/Progression Config, Shop/Shop Catalog and TrainingCurriculum.
INSERT INTO Currencies(CurrencyName,Description) VALUES ('TOKENPAD','TOKENPAD') ON CONFLICT(CurrencyName) DO NOTHING;
INSERT INTO Currencies(CurrencyName,Description) VALUES ('ELITE TOKEN','ELITE TOKEN') ON CONFLICT(CurrencyName) DO NOTHING;
INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ('A.Win5','Thắng 5 trận','Thắng 5 trận','Wins',5,'{"coins":100,"gems":0,"xp":10}'::jsonb) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;
INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ('A.Win25','Thắng 25 trận','Thắng 25 trận','Wins',25,'{"coins":300,"gems":0,"xp":25}'::jsonb) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;
INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ('A.Win100','Thắng 100 trận','Thắng 100 trận','Wins',100,'{"coins":1000,"gems":0,"xp":50}'::jsonb) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;
INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ('A.Play10','Hoàn tất 10 trận','Hoàn tất 10 trận','Matches',10,'{"coins":100,"gems":0,"xp":10}'::jsonb) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;
INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ('A.Play50','Hoàn tất 50 trận','Hoàn tất 50 trận','Matches',50,'{"coins":400,"gems":0,"xp":25}'::jsonb) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;
INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ('A.Play200','Hoàn tất 200 trận','Hoàn tất 200 trận','Matches',200,'{"coins":1500,"gems":0,"xp":75}'::jsonb) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;
INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ('A.Point100','Ghi 100 điểm cho đội','Ghi 100 điểm cho đội','Points',100,'{"coins":200,"gems":0,"xp":15}'::jsonb) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;
INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ('A.Point500','Ghi 500 điểm cho đội','Ghi 500 điểm cho đội','Points',500,'{"coins":600,"gems":0,"xp":35}'::jsonb) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;
INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ('A.Streak5','Đạt chuỗi thắng 5 trận','Đạt chuỗi thắng 5 trận','BestStreak',5,'{"coins":500,"gems":0,"xp":30}'::jsonb) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;
INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ('A.Higher5','Thắng 5 đối thủ rank Elo cao hơn','Thắng 5 đối thủ rank Elo cao hơn','HigherRatedWins',5,'{"coins":300,"gems":0,"xp":25}'::jsonb) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Play1','Hoàn tất 1 trận','Hoàn tất 1 trận','Daily',1,'{"coins":50,"gems":0,"xp":5}'::jsonb,'{"id":"D.Play1","title":"Hoàn tất 1 trận","scope":"Daily","metric":"Matches","target":1,"reward":{"coins":50,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Win1','Thắng 1 trận','Thắng 1 trận','Daily',1,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Win1","title":"Thắng 1 trận","scope":"Daily","metric":"Wins","target":1,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Point20','Ghi 20 điểm cho đội','Ghi 20 điểm cho đội','Daily',20,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Point20","title":"Ghi 20 điểm cho đội","scope":"Daily","metric":"Points","target":20,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Return30','Trả bóng hợp lệ 30 lần','Trả bóng hợp lệ 30 lần','Daily',30,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Return30","title":"Trả bóng hợp lệ 30 lần","scope":"Daily","metric":"Returns","target":30,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Serve10','Giao bóng hợp lệ 10 lần','Giao bóng hợp lệ 10 lần','Daily',10,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Serve10","title":"Giao bóng hợp lệ 10 lần","scope":"Daily","metric":"Serves","target":10,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Bot1','Hoàn tất 1 trận với bot','Hoàn tất 1 trận với bot','Daily',1,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Bot1","title":"Hoàn tất 1 trận với bot","scope":"Daily","metric":"BotMatches","target":1,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.HumanWin1','Thắng 1 trận tìm đối thủ người thật','Thắng 1 trận tìm đối thủ người thật','Daily',1,'{"coins":125,"gems":0,"xp":10}'::jsonb,'{"id":"D.HumanWin1","title":"Thắng 1 trận tìm đối thủ người thật","scope":"Daily","metric":"HumanWins","target":1,"reward":{"coins":125,"gems":0,"xp":10}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Play10','Hoàn tất 10 trận','Hoàn tất 10 trận','Weekly',10,'{"coins":300,"gems":0,"xp":20}'::jsonb,'{"id":"W.Play10","title":"Hoàn tất 10 trận","scope":"Weekly","metric":"Matches","target":10,"reward":{"coins":300,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Win5','Thắng 5 trận','Thắng 5 trận','Weekly',5,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Win5","title":"Thắng 5 trận","scope":"Weekly","metric":"Wins","target":5,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Point100','Ghi 100 điểm cho đội','Ghi 100 điểm cho đội','Weekly',100,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Point100","title":"Ghi 100 điểm cho đội","scope":"Weekly","metric":"Points","target":100,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Return200','Trả bóng hợp lệ 200 lần','Trả bóng hợp lệ 200 lần','Weekly',200,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Return200","title":"Trả bóng hợp lệ 200 lần","scope":"Weekly","metric":"Returns","target":200,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Serve50','Giao bóng hợp lệ 50 lần','Giao bóng hợp lệ 50 lần','Weekly',50,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Serve50","title":"Giao bóng hợp lệ 50 lần","scope":"Weekly","metric":"Serves","target":50,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Bot5','Hoàn tất 5 trận với bot','Hoàn tất 5 trận với bot','Weekly',5,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Bot5","title":"Hoàn tất 5 trận với bot","scope":"Weekly","metric":"BotMatches","target":5,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Human5','Hoàn tất 5 trận tìm đối thủ người thật','Hoàn tất 5 trận tìm đối thủ người thật','Weekly',5,'{"coins":350,"gems":0,"xp":20}'::jsonb,'{"id":"W.Human5","title":"Hoàn tất 5 trận tìm đối thủ người thật","scope":"Weekly","metric":"HumanMatches","target":5,"reward":{"coins":350,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.RankWin3','Thắng 3 trận rank 1v1','Thắng 3 trận rank 1v1','Weekly',3,'{"coins":350,"gems":0,"xp":20}'::jsonb,'{"id":"W.RankWin3","title":"Thắng 3 trận rank 1v1","scope":"Weekly","metric":"RankedWins","target":3,"reward":{"coins":350,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.DoubleWin2','Thắng 2 trận tìm trận 2v2','Thắng 2 trận tìm trận 2v2','Weekly',2,'{"coins":400,"gems":0,"xp":20}'::jsonb,'{"id":"W.DoubleWin2","title":"Thắng 2 trận tìm trận 2v2","scope":"Weekly","metric":"DoublesWins","target":2,"reward":{"coins":400,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Streak3','Đạt chuỗi thắng 3 trận trong tuần','Đạt chuỗi thắng 3 trận trong tuần','Weekly',3,'{"coins":350,"gems":0,"xp":20}'::jsonb,'{"id":"W.Streak3","title":"Đạt chuỗi thắng 3 trận trong tuần","scope":"Weekly","metric":"BestStreak","target":3,"reward":{"coins":350,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Cosmetics(CurrencyId,ItemCode,CosmeticName,Description,Price,Type,Config) SELECT CurrencyId,'paddle.default','PICKLEVERSE','Vợt mặc định của PickleVerse. Thiết kế gọn gàng, luôn sẵn sàng cho những trận đấu đầu tiên.',0,'Paddle','{"id":"paddle.default","title":"PICKLEVERSE","description":"Vợt mặc định của PickleVerse. Thiết kế gọn gàng, luôn sẵn sàng cho những trận đấu đầu tiên.","collection":"ORIGINAL COLLECTION","tokenpadPrice":0,"elitePrice":0,"gemstone":false,"releaseOrder":0}'::jsonb FROM Currencies WHERE CurrencyName='TOKENPAD' ON CONFLICT(ItemCode) DO UPDATE SET CurrencyId=EXCLUDED.CurrencyId,CosmeticName=EXCLUDED.CosmeticName,Description=EXCLUDED.Description,Price=EXCLUDED.Price,Type=EXCLUDED.Type,Config=EXCLUDED.Config;
INSERT INTO CosmeticPrices(CosmeticId,CurrencyId,Price) SELECT c.CosmeticId,u.CurrencyId,0 FROM Cosmetics c CROSS JOIN Currencies u WHERE c.ItemCode='paddle.default' AND u.CurrencyName='TOKENPAD' ON CONFLICT(CosmeticId,CurrencyId) DO UPDATE SET Price=EXCLUDED.Price;
INSERT INTO Cosmetics(CurrencyId,ItemCode,CosmeticName,Description,Price,Type,Config) SELECT CurrencyId,'paddle.gemstone','GEMSTONE','Viền đá quý, sắc vàng và mặt carbon. Ánh lấp lánh tinh tế khi xoay dưới ánh sáng.',650,'Paddle','{"id":"paddle.gemstone","title":"GEMSTONE","description":"Viền đá quý, sắc vàng và mặt carbon. Ánh lấp lánh tinh tế khi xoay dưới ánh sáng.","collection":"ELITE COLLECTION","tokenpadPrice":0,"elitePrice":650,"gemstone":true,"releaseOrder":2}'::jsonb FROM Currencies WHERE CurrencyName='ELITE TOKEN' ON CONFLICT(ItemCode) DO UPDATE SET CurrencyId=EXCLUDED.CurrencyId,CosmeticName=EXCLUDED.CosmeticName,Description=EXCLUDED.Description,Price=EXCLUDED.Price,Type=EXCLUDED.Type,Config=EXCLUDED.Config;
INSERT INTO CosmeticPrices(CosmeticId,CurrencyId,Price) SELECT c.CosmeticId,u.CurrencyId,650 FROM Cosmetics c CROSS JOIN Currencies u WHERE c.ItemCode='paddle.gemstone' AND u.CurrencyName='ELITE TOKEN' ON CONFLICT(CosmeticId,CurrencyId) DO UPDATE SET Price=EXCLUDED.Price;
INSERT INTO Cosmetics(CurrencyId,ItemCode,CosmeticName,Description,Price,Type,Config) SELECT CurrencyId,'paddle.thot','THOT','Hoa văn nổi bật cho phong cách thi đấu riêng. Chọn Tokenpad hoặc Elite để sở hữu.',900,'Paddle','{"id":"paddle.thot","title":"THOT","description":"Hoa văn nổi bật cho phong cách thi đấu riêng. Chọn Tokenpad hoặc Elite để sở hữu.","collection":"SIGNATURE COLLECTION","tokenpadPrice":900,"elitePrice":250,"gemstone":false,"releaseOrder":1}'::jsonb FROM Currencies WHERE CurrencyName='TOKENPAD' ON CONFLICT(ItemCode) DO UPDATE SET CurrencyId=EXCLUDED.CurrencyId,CosmeticName=EXCLUDED.CosmeticName,Description=EXCLUDED.Description,Price=EXCLUDED.Price,Type=EXCLUDED.Type,Config=EXCLUDED.Config;
INSERT INTO CosmeticPrices(CosmeticId,CurrencyId,Price) SELECT c.CosmeticId,u.CurrencyId,900 FROM Cosmetics c CROSS JOIN Currencies u WHERE c.ItemCode='paddle.thot' AND u.CurrencyName='TOKENPAD' ON CONFLICT(CosmeticId,CurrencyId) DO UPDATE SET Price=EXCLUDED.Price;
INSERT INTO CosmeticPrices(CosmeticId,CurrencyId,Price) SELECT c.CosmeticId,u.CurrencyId,250 FROM Cosmetics c CROSS JOIN Currencies u WHERE c.ItemCode='paddle.thot' AND u.CurrencyName='ELITE TOKEN' ON CONFLICT(CosmeticId,CurrencyId) DO UPDATE SET Price=EXCLUDED.Price;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Serve','Giao bóng đúng ô') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('serve','Giao bóng đúng ô','Giao dưới tay từ sau baseline. Bóng phải sang ô chéo và vượt cả vạch Kitchen.','Serve',6,'{"coins":150,"gems":0,"xp":5}'::jsonb,1,'{"id":"serve","title":"Giao bóng đúng ô","explanation":"Giao dưới tay từ sau baseline. Bóng phải sang ô chéo và vượt cả vạch Kitchen.","coaching":"Đứng trong vùng sáng phía sau baseline. Giữ rồi thả Drive; không bước vào sân trước khi chạm bóng.","skill":"Serve","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[1],"prerequisites":[]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'serve',TechniqueId,'Giao bóng đúng ô','Đứng trong vùng sáng phía sau baseline. Giữ rồi thả Drive; không bước vào sân trước khi chạm bóng.' FROM Techniques WHERE TechniqueName='Serve' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,6 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='serve' AND d.Code='serve' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Drive','Drive về cuối sân') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('drive','Drive về cuối sân','Đón bóng sau một lần nảy, đánh Drive vào vùng sâu đã đánh dấu.','Drive',6,'{"coins":150,"gems":0,"xp":5}'::jsonb,2,'{"id":"drive","title":"Drive về cuối sân","explanation":"Đón bóng sau một lần nảy, đánh Drive vào vùng sâu đã đánh dấu.","coaching":"Chuẩn bị vợt phía trước. Thả phím sớm hơn lúc bóng tới để animation đưa vợt đến điểm tiếp xúc.","skill":"Drive","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[1],"prerequisites":[]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'drive',TechniqueId,'Drive về cuối sân','Chuẩn bị vợt phía trước. Thả phím sớm hơn lúc bóng tới để animation đưa vợt đến điểm tiếp xúc.' FROM Techniques WHERE TechniqueName='Drive' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,6 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='drive' AND d.Code='drive' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Dink','Dink kiểm soát Kitchen') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('dink','Dink kiểm soát Kitchen','Đánh nhẹ sau nảy, đưa bóng vào Kitchen đối phương. Được đứng trong Kitchen khi đánh bóng đã nảy.','Dink',6,'{"coins":150,"gems":0,"xp":5}'::jsonb,3,'{"id":"dink","title":"Dink kiểm soát Kitchen","explanation":"Đánh nhẹ sau nảy, đưa bóng vào Kitchen đối phương. Được đứng trong Kitchen khi đánh bóng đã nảy.","coaching":"Đón bóng trước người. Không dùng lực mạnh; ưu tiên kiểm soát và cung bóng thấp.","skill":"Dink","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[2],"prerequisites":[]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'dink',TechniqueId,'Dink kiểm soát Kitchen','Đón bóng trước người. Không dùng lực mạnh; ưu tiên kiểm soát và cung bóng thấp.' FROM Techniques WHERE TechniqueName='Dink' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,6 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='dink' AND d.Code='dink' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Volley','Volley ngoài Kitchen') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('volley','Volley ngoài Kitchen','Chặn bóng trước khi nảy. Cả hai chân và đà sau cú đánh phải ở ngoài Kitchen.','Volley',6,'{"coins":150,"gems":0,"xp":5}'::jsonb,4,'{"id":"volley","title":"Volley ngoài Kitchen","explanation":"Chặn bóng trước khi nảy. Cả hai chân và đà sau cú đánh phải ở ngoài Kitchen.","coaching":"Giữ vị trí sau vạch sáng. Chọn Volley khi bóng còn trên không; không lao vào Kitchen sau cú đánh.","skill":"Volley","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[4],"prerequisites":[]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'volley',TechniqueId,'Volley ngoài Kitchen','Giữ vị trí sau vạch sáng. Chọn Volley khi bóng còn trên không; không lao vào Kitchen sau cú đánh.' FROM Techniques WHERE TechniqueName='Volley' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,6 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='volley' AND d.Code='volley' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Modifiers','Lực và hướng chéo') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('modifiers','Lực và hướng chéo','Luân phiên Drive, Dink, Volley với tăng lực hoặc đánh chéo theo chỉ dẫn từng bóng.','Modifiers',7,'{"coins":225,"gems":0,"xp":7}'::jsonb,5,'{"id":"modifiers","title":"Lực và hướng chéo","explanation":"Luân phiên Drive, Dink, Volley với tăng lực hoặc đánh chéo theo chỉ dẫn từng bóng.","coaching":"Giữ modifier cho đến khi thả nút đánh. Quan sát vùng đích đổi bên khi cần đánh chéo.","skill":"Modifiers","tier":1,"trials":10,"required":7,"reward":225,"xp":7,"masks":[9,17,10,18,12,20],"prerequisites":["drive","dink","volley"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'modifiers',TechniqueId,'Lực và hướng chéo','Giữ modifier cho đến khi thả nút đánh. Quan sát vùng đích đổi bên khi cần đánh chéo.' FROM Techniques WHERE TechniqueName='Modifiers' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,7 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='modifiers' AND d.Code='modifiers' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Drop','Third-shot drop') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('drop','Third-shot drop','Sau giao và trả giao đã nảy, đánh cú thứ ba mềm vào Kitchen để lấy thời gian lên lưới.','Drop',7,'{"coins":225,"gems":0,"xp":7}'::jsonb,6,'{"id":"drop","title":"Third-shot drop","explanation":"Sau giao và trả giao đã nảy, đánh cú thứ ba mềm vào Kitchen để lấy thời gian lên lưới.","coaching":"Ghép Drive + Dink. Đây là cú đánh sau nảy; bóng phải rơi trong vùng sáng gần lưới.","skill":"Drop","tier":1,"trials":10,"required":7,"reward":225,"xp":7,"masks":[3],"prerequisites":["drive","dink"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'drop',TechniqueId,'Third-shot drop','Ghép Drive + Dink. Đây là cú đánh sau nảy; bóng phải rơi trong vùng sáng gần lưới.' FROM Techniques WHERE TechniqueName='Drop' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,7 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='drop' AND d.Code='drop' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Reset','Reset bóng nhanh') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('reset','Reset bóng nhanh','Dùng Reset để giảm nhịp bóng nhanh, đưa bóng về Kitchen thay vì cố đánh mạnh.','Reset',7,'{"coins":225,"gems":0,"xp":7}'::jsonb,7,'{"id":"reset","title":"Reset bóng nhanh","explanation":"Dùng Reset để giảm nhịp bóng nhanh, đưa bóng về Kitchen thay vì cố đánh mạnh.","coaching":"Ghép Dink + Volley, tiếp bóng trên không và giữ cả hai chân ngoài Kitchen.","skill":"Reset","tier":1,"trials":10,"required":7,"reward":225,"xp":7,"masks":[6],"prerequisites":["dink","volley"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'reset',TechniqueId,'Reset bóng nhanh','Ghép Dink + Volley, tiếp bóng trên không và giữ cả hai chân ngoài Kitchen.' FROM Techniques WHERE TechniqueName='Reset' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,7 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='reset' AND d.Code='reset' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Smash','Smash bóng bổng') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('smash','Smash bóng bổng','Đợi bóng bổng đủ cao rồi đánh Smash vào vùng sâu. Không volley trong Kitchen.','Smash',9,'{"coins":300,"gems":0,"xp":10}'::jsonb,8,'{"id":"smash","title":"Smash bóng bổng","explanation":"Đợi bóng bổng đủ cao rồi đánh Smash vào vùng sâu. Không volley trong Kitchen.","coaching":"Ghép Drive + Volley. Quan sát vòng điểm tiếp bóng trên sân và thả khi chỉ báo timing vào vùng xanh.","skill":"Smash","tier":2,"trials":12,"required":9,"reward":300,"xp":10,"masks":[5],"prerequisites":["volley","modifiers"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'smash',TechniqueId,'Smash bóng bổng','Ghép Drive + Volley. Quan sát vòng điểm tiếp bóng trên sân và thả khi chỉ báo timing vào vùng xanh.' FROM Techniques WHERE TechniqueName='Smash' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,9 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='smash' AND d.Code='smash' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Footwork','Di chuyển và hồi vị') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('footwork','Di chuyển và hồi vị','Đón bóng luân phiên hai bên sân, đánh Drive rồi trở về vị trí hồi phục.','Footwork',9,'{"coins":300,"gems":0,"xp":10}'::jsonb,9,'{"id":"footwork","title":"Di chuyển và hồi vị","explanation":"Đón bóng luân phiên hai bên sân, đánh Drive rồi trở về vị trí hồi phục.","coaching":"Di chuyển A/D hoặc joystick tới vùng đón bóng. Có thể dùng chạy; sau cú đánh quay về vòng hồi vị.","skill":"Footwork","tier":2,"trials":12,"required":9,"reward":300,"xp":10,"masks":[1,17],"prerequisites":["drive","modifiers"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'footwork',TechniqueId,'Di chuyển và hồi vị','Di chuyển A/D hoặc joystick tới vùng đón bóng. Có thể dùng chạy; sau cú đánh quay về vòng hồi vị.' FROM Techniques WHERE TechniqueName='Footwork' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,9 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='footwork' AND d.Code='footwork' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Approach','Drop rồi tiến lên') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('approach','Drop rồi tiến lên','Đánh drop từ cuối sân, tiến lên vùng sẵn sàng trước Kitchen. Dừng có kiểm soát trước lượt tiếp theo.','Approach',9,'{"coins":300,"gems":0,"xp":10}'::jsonb,10,'{"id":"approach","title":"Drop rồi tiến lên","explanation":"Đánh drop từ cuối sân, tiến lên vùng sẵn sàng trước Kitchen. Dừng có kiểm soát trước lượt tiếp theo.","coaching":"Ghép Drive + Dink rồi di chuyển W lên vùng sáng. Đừng chạy qua vạch Kitchen để đón volley.","skill":"Approach","tier":2,"trials":12,"required":9,"reward":300,"xp":10,"masks":[3],"prerequisites":["drop","footwork"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'approach',TechniqueId,'Drop rồi tiến lên','Ghép Drive + Dink rồi di chuyển W lên vùng sáng. Đừng chạy qua vạch Kitchen để đón volley.' FROM Techniques WHERE TechniqueName='Approach' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,9 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='approach' AND d.Code='approach' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('DoublesCover','Đánh đôi: giữ khoảng cách') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('doubles-cover','Đánh đôi: giữ khoảng cách','Hai người giữ hàng ngang, dịch chuyển cùng phía bóng. Đánh bóng phần sân của bạn và giữ khe giữa đội nhỏ.','DoublesCover',9,'{"coins":450,"gems":0,"xp":15}'::jsonb,11,'{"id":"doubles-cover","title":"Đánh đôi: giữ khoảng cách","explanation":"Hai người giữ hàng ngang, dịch chuyển cùng phía bóng. Đánh bóng phần sân của bạn và giữ khe giữa đội nhỏ.","coaching":"Đồng đội AI đi cùng bạn. Dịch chuyển ngang, đánh Volley theo chỉ dẫn, rồi hồi vị để cả đội giữ hàng ngang.","skill":"DoublesCover","tier":3,"trials":12,"required":9,"reward":450,"xp":15,"masks":[4,20],"prerequisites":["volley","footwork"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'doubles-cover',TechniqueId,'Đánh đôi: giữ khoảng cách','Đồng đội AI đi cùng bạn. Dịch chuyển ngang, đánh Volley theo chỉ dẫn, rồi hồi vị để cả đội giữ hàng ngang.' FROM Techniques WHERE TechniqueName='DoublesCover' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,9 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='doubles-cover' AND d.Code='doubles-cover' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('DoublesTransition','Đánh đôi: drop và phối hợp') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('doubles-transition','Đánh đôi: drop và phối hợp','Đội giao chờ trả giao nảy, dùng drop tiến lên cùng nhau rồi chuyển sang dink và volley. Có lượt bóng của đồng đội AI.','DoublesTransition',9,'{"coins":450,"gems":0,"xp":15}'::jsonb,12,'{"id":"doubles-transition","title":"Đánh đôi: drop và phối hợp","explanation":"Đội giao chờ trả giao nảy, dùng drop tiến lên cùng nhau rồi chuyển sang dink và volley. Có lượt bóng của đồng đội AI.","coaching":"Theo dấu vị trí của bạn. Sau drop tiến lên cùng đồng đội; khi hiện ‘Bóng của đồng đội’, giữ hàng ngang và để AI xử lý.","skill":"DoublesTransition","tier":3,"trials":12,"required":9,"reward":450,"xp":15,"masks":[3,2,4],"prerequisites":["approach","doubles-cover","reset"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'doubles-transition',TechniqueId,'Đánh đôi: drop và phối hợp','Theo dấu vị trí của bạn. Sau drop tiến lên cùng đồng đội; khi hiện ‘Bóng của đồng đội’, giữ hàng ngang và để AI xử lý.' FROM Techniques WHERE TechniqueName='DoublesTransition' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,9 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='doubles-transition' AND d.Code='doubles-transition' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO PlayerCosmetics(PlayerId,CosmeticId,Equipped) SELECT p.UserId,c.CosmeticId,NOT EXISTS(SELECT 1 FROM PlayerCosmetics pc WHERE pc.PlayerId=p.UserId AND pc.Equipped) FROM PlayerProfiles p CROSS JOIN Cosmetics c WHERE c.ItemCode='paddle.default' ON CONFLICT(PlayerId,CosmeticId) DO NOTHING;
-- END UNITY GAME CONTENT SEED
-- END INITIAL DATA

-- Hybrid AI durable queue and rating ledger.
CREATE TABLE IF NOT EXISTS ratingledger (
    playerid UUID NOT NULL REFERENCES playerprofiles(userid) ON DELETE CASCADE,
    matchid UUID NOT NULL REFERENCES GameMatches(matchid) ON DELETE CASCADE,
    beforeelo INT NOT NULL, afterelo INT NOT NULL, opponentelo INT NOT NULL,
    delta INT NOT NULL, policyversion TEXT NOT NULL, createdat TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY(playerid,matchid), CHECK(afterelo >= 100), CHECK(afterelo-beforeelo=delta)
);
CREATE TABLE IF NOT EXISTS aitelemetrysessions (
    batchid UUID PRIMARY KEY, playerid UUID NOT NULL REFERENCES playerprofiles(userid) ON DELETE CASCADE,
    sessionid UUID NOT NULL, sessiontype TEXT NOT NULL CHECK(sessiontype IN ('match','training')),
    partcount INT NOT NULL CHECK(partcount BETWEEN 1 AND 100), metadata JSONB NOT NULL,
    createdat TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(playerid,sessionid,sessiontype)
);
CREATE TABLE IF NOT EXISTS aitelemetryparts (
    batchid UUID NOT NULL REFERENCES aitelemetrysessions(batchid) ON DELETE CASCADE,
    partindex INT NOT NULL CHECK(partindex>=0), fingerprint TEXT NOT NULL, shots JSONB NOT NULL,
    PRIMARY KEY(batchid,partindex)
);
CREATE TABLE IF NOT EXISTS aianalysisjobs (
    batchid UUID PRIMARY KEY REFERENCES aitelemetrysessions(batchid) ON DELETE CASCADE,
    state TEXT NOT NULL DEFAULT 'pending' CHECK(state IN ('pending','processing','completed','failed')),
    attempts INT NOT NULL DEFAULT 0, availableat TIMESTAMPTZ NOT NULL DEFAULT now(),
    leasetoken UUID, leaseuntil TIMESTAMPTZ, request JSONB,
    ruleversion TEXT NOT NULL DEFAULT 'rules-v1', lasterror TEXT,
    updatedat TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ai_jobs_pending_idx ON aianalysisjobs(availableat) WHERE state IN ('pending','processing');
CREATE TABLE IF NOT EXISTS aicoachingsessions (
    batchid UUID PRIMARY KEY, playerid UUID NOT NULL REFERENCES playerprofiles(userid) ON DELETE CASCADE,
    sessionid UUID NOT NULL, sessiontype TEXT NOT NULL, result JSONB NOT NULL,
    createdat TIMESTAMPTZ NOT NULL DEFAULT now(), UNIQUE(playerid,sessionid,sessiontype)
);
CREATE INDEX IF NOT EXISTS ai_coaching_recent_idx ON aicoachingsessions(playerid,createdat DESC);
CREATE TABLE IF NOT EXISTS aicoachingprofiles (
    playerid UUID PRIMARY KEY REFERENCES playerprofiles(userid) ON DELETE CASCADE,
    batchid UUID NOT NULL, result JSONB NOT NULL, updatedat TIMESTAMPTZ NOT NULL DEFAULT now()
);

COMMIT;
