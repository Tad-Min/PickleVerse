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
    AuthVersion INT NOT NULL DEFAULT 0 CHECK (AuthVersion >= 0),
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

-- Independent from VerificationEmail: password recovery never verifies an email.
CREATE TABLE PasswordResetChallenges (
    UserId UUID PRIMARY KEY REFERENCES Users(UserId) ON DELETE CASCADE,
    ChallengeId UUID NOT NULL,
    TokenHash VARCHAR(64) NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    ExpiresAt TIMESTAMPTZ NOT NULL,
    FailedAttempts INT NOT NULL DEFAULT 0 CHECK (FailedAttempts >= 0),
    Consumed BOOLEAN NOT NULL DEFAULT FALSE
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
    LearningEpoch BIGINT NOT NULL DEFAULT 0,
    LearningResetAt TIMESTAMPTZ,
    MissionEpoch BIGINT NOT NULL DEFAULT 0,
    EconomyEpoch BIGINT NOT NULL DEFAULT 0,
    ExperienceChoice VARCHAR(20) CHECK (ExperienceChoice IN ('NEW','EXPERIENCED')),
    TutorialCompleted BOOLEAN NOT NULL DEFAULT FALSE,
    TutorialSkipped BOOLEAN NOT NULL DEFAULT FALSE,
    TutorialCheckpoint INT NOT NULL DEFAULT 0 CHECK(TutorialCheckpoint BETWEEN 0 AND 17),
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
    AuthorityMode VARCHAR(16) NOT NULL DEFAULT 'shared' CHECK (AuthorityMode IN ('shared', 'dedicated')),
    ServerHeartbeatAt TIMESTAMPTZ NULL,
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

CREATE TABLE PlayerLoginDays (
    PlayerId UUID NOT NULL REFERENCES Users(UserId) ON DELETE CASCADE,
    Day DATE NOT NULL,
    FirstAt TIMESTAMPTZ NOT NULL,
    LastAt TIMESTAMPTZ NOT NULL,
    PRIMARY KEY(PlayerId,Day)
);
CREATE INDEX PlayerLoginDays_Day_Idx ON PlayerLoginDays(Day,PlayerId);
CREATE TABLE PlayerActivityDays (
    PlayerId UUID NOT NULL REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    Day DATE NOT NULL,
    Seconds BIGINT NOT NULL CHECK(Seconds>=0),
    PRIMARY KEY(PlayerId,Day)
);
CREATE TABLE MatchmakingSearches (
    SearchId UUID PRIMARY KEY,
    PlayerId UUID NOT NULL REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    Mode INT NOT NULL CHECK(Mode IN (1,2)),
    Region VARCHAR(30) NOT NULL,
    Protocol INT NOT NULL,
    State VARCHAR(20) NOT NULL CHECK(State IN ('SEARCHING','MATCHED','BOT','CANCELLED','TIMEOUT')),
    StartedAt TIMESTAMPTZ NOT NULL,
    LastSeenAt TIMESTAMPTZ NOT NULL,
    EndedAt TIMESTAMPTZ,
    WaitMilliseconds BIGINT CHECK(WaitMilliseconds>=0),
    MatchId UUID,
    CancelledAfterAssignment BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE INDEX MatchmakingSearches_Start_Idx ON MatchmakingSearches(StartedAt,State);

CREATE TABLE ReportPlayerInMatch (
    ReporterId UUID NOT NULL,       
    ReportedPlayerId UUID NOT NULL,  
    MatchId UUID NOT NULL,      
    Reason VARCHAR(254) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'PENDING' CHECK (Status IN ('PENDING', 'CHECKING', 'ACCEPTED', 'DECLINED')),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ReviewerId UUID REFERENCES Users(UserId),
    DecisionReason VARCHAR(2000),
    PRIMARY KEY (ReporterId, ReportedPlayerId, MatchId),
    CONSTRAINT CHK_CannotReportSelf CHECK (ReporterId != ReportedPlayerId),
    CONSTRAINT FK_Report_Reporter FOREIGN KEY (ReporterId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_Report_ReportedPlayer FOREIGN KEY (ReportedPlayerId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_Report_GameMatches FOREIGN KEY (MatchId) REFERENCES GameMatches(MatchId) ON DELETE CASCADE
);

CREATE TABLE UserSanctions (
    SanctionId UUID PRIMARY KEY,
    UserId UUID NOT NULL REFERENCES Users(UserId),
    ActorId UUID NOT NULL REFERENCES Users(UserId),
    Restriction VARCHAR(20) NOT NULL CHECK(Restriction IN ('BANNED','SUSPENDED')),
    Reason VARCHAR(2000) NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    ExpiresAt TIMESTAMPTZ,
    RevokedAt TIMESTAMPTZ,
    RevokedBy UUID REFERENCES Users(UserId),
    RevocationReason VARCHAR(2000),
    ReportMatchId UUID REFERENCES GameMatches(MatchId),
    ReporterId UUID REFERENCES Users(UserId),
    CHECK(ExpiresAt IS NULL OR ExpiresAt > CreatedAt),
    CHECK(Restriction <> 'SUSPENDED' OR ExpiresAt IS NOT NULL)
);
CREATE INDEX UserSanctions_User_Idx ON UserSanctions(UserId,CreatedAt DESC);
CREATE TABLE SanctionAppeals (
    AppealId UUID PRIMARY KEY,
    SanctionId UUID NOT NULL UNIQUE REFERENCES UserSanctions(SanctionId),
    UserId UUID NOT NULL REFERENCES Users(UserId),
    Statement VARCHAR(4000) NOT NULL,
    Status VARCHAR(20) NOT NULL CHECK(Status IN ('PENDING','CHECKING','ACCEPTED','DECLINED')),
    CreatedAt TIMESTAMPTZ NOT NULL,
    UpdatedAt TIMESTAMPTZ NOT NULL,
    ReviewerId UUID REFERENCES Users(UserId),
    DecisionReason VARCHAR(2000)
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

CREATE TABLE TechniqueReferences (
    ReferenceId UUID PRIMARY KEY,
    TechniqueId INT NOT NULL REFERENCES Techniques(TechniqueId),
    Version BIGINT NOT NULL CHECK(Version>0),
    Data JSONB NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    UNIQUE(TechniqueId,Version)
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
    PublishStatus VARCHAR(20) NOT NULL DEFAULT 'PUBLISHED' CHECK (PublishStatus IN ('DRAFT','PUBLISHED','UNPUBLISHED')),
    DraftConfig JSONB,
    ContentVersion BIGINT NOT NULL DEFAULT 1 CHECK (ContentVersion > 0),
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
    CatalogVersion VARCHAR(64) NOT NULL DEFAULT '',
    ProgressEpoch BIGINT NOT NULL DEFAULT 0,
    Fingerprint VARCHAR(64) NOT NULL DEFAULT '',
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
INSERT INTO Achievements(AchievementCode,AchievementName,Description,Type,Target,Reward) VALUES ('A.Higher5','Thắng 5 đối thủ xếp hạng Elo cao hơn','Thắng 5 đối thủ xếp hạng Elo cao hơn','HigherRatedWins',5,'{"coins":300,"gems":0,"xp":25}'::jsonb) ON CONFLICT(AchievementCode) DO UPDATE SET AchievementName=EXCLUDED.AchievementName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Play1','Hoàn tất 1 trận','Hoàn tất 1 trận','Daily',1,'{"coins":50,"gems":0,"xp":5}'::jsonb,'{"id":"D.Play1","title":"Hoàn tất 1 trận","scope":"Daily","metric":"Matches","target":1,"reward":{"coins":50,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Win1','Thắng 1 trận','Thắng 1 trận','Daily',1,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Win1","title":"Thắng 1 trận","scope":"Daily","metric":"Wins","target":1,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Point20','Ghi 20 điểm cho đội','Ghi 20 điểm cho đội','Daily',20,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Point20","title":"Ghi 20 điểm cho đội","scope":"Daily","metric":"Points","target":20,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Return30','Trả bóng hợp lệ 30 lần','Trả bóng hợp lệ 30 lần','Daily',30,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Return30","title":"Trả bóng hợp lệ 30 lần","scope":"Daily","metric":"Returns","target":30,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Serve10','Giao bóng hợp lệ 10 lần','Giao bóng hợp lệ 10 lần','Daily',10,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Serve10","title":"Giao bóng hợp lệ 10 lần","scope":"Daily","metric":"Serves","target":10,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.Bot1','Hoàn tất 1 trận với máy','Hoàn tất 1 trận với máy','Daily',1,'{"coins":75,"gems":0,"xp":5}'::jsonb,'{"id":"D.Bot1","title":"Hoàn tất 1 trận với máy","scope":"Daily","metric":"BotMatches","target":1,"reward":{"coins":75,"gems":0,"xp":5}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('D.HumanWin1','Thắng 1 trận tìm đối thủ người thật','Thắng 1 trận tìm đối thủ người thật','Daily',1,'{"coins":125,"gems":0,"xp":10}'::jsonb,'{"id":"D.HumanWin1","title":"Thắng 1 trận tìm đối thủ người thật","scope":"Daily","metric":"HumanWins","target":1,"reward":{"coins":125,"gems":0,"xp":10}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Play10','Hoàn tất 10 trận','Hoàn tất 10 trận','Weekly',10,'{"coins":300,"gems":0,"xp":20}'::jsonb,'{"id":"W.Play10","title":"Hoàn tất 10 trận","scope":"Weekly","metric":"Matches","target":10,"reward":{"coins":300,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Win5','Thắng 5 trận','Thắng 5 trận','Weekly',5,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Win5","title":"Thắng 5 trận","scope":"Weekly","metric":"Wins","target":5,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Point100','Ghi 100 điểm cho đội','Ghi 100 điểm cho đội','Weekly',100,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Point100","title":"Ghi 100 điểm cho đội","scope":"Weekly","metric":"Points","target":100,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Return200','Trả bóng hợp lệ 200 lần','Trả bóng hợp lệ 200 lần','Weekly',200,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Return200","title":"Trả bóng hợp lệ 200 lần","scope":"Weekly","metric":"Returns","target":200,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Serve50','Giao bóng hợp lệ 50 lần','Giao bóng hợp lệ 50 lần','Weekly',50,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Serve50","title":"Giao bóng hợp lệ 50 lần","scope":"Weekly","metric":"Serves","target":50,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Bot5','Hoàn tất 5 trận với máy','Hoàn tất 5 trận với máy','Weekly',5,'{"coins":250,"gems":0,"xp":15}'::jsonb,'{"id":"W.Bot5","title":"Hoàn tất 5 trận với máy","scope":"Weekly","metric":"BotMatches","target":5,"reward":{"coins":250,"gems":0,"xp":15}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Human5','Hoàn tất 5 trận tìm đối thủ người thật','Hoàn tất 5 trận tìm đối thủ người thật','Weekly',5,'{"coins":350,"gems":0,"xp":20}'::jsonb,'{"id":"W.Human5","title":"Hoàn tất 5 trận tìm đối thủ người thật","scope":"Weekly","metric":"HumanMatches","target":5,"reward":{"coins":350,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.RankWin3','Thắng 3 trận xếp hạng 1v1','Thắng 3 trận xếp hạng 1v1','Weekly',3,'{"coins":350,"gems":0,"xp":20}'::jsonb,'{"id":"W.RankWin3","title":"Thắng 3 trận xếp hạng 1v1","scope":"Weekly","metric":"RankedWins","target":3,"reward":{"coins":350,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.DoubleWin2','Thắng 2 trận tìm trận 2v2','Thắng 2 trận tìm trận 2v2','Weekly',2,'{"coins":400,"gems":0,"xp":20}'::jsonb,'{"id":"W.DoubleWin2","title":"Thắng 2 trận tìm trận 2v2","scope":"Weekly","metric":"DoublesWins","target":2,"reward":{"coins":400,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Missions(Code,MissionName,Description,Type,Target,Reward,Config) VALUES ('W.Streak3','Đạt chuỗi thắng 3 trận trong tuần','Đạt chuỗi thắng 3 trận trong tuần','Weekly',3,'{"coins":350,"gems":0,"xp":20}'::jsonb,'{"id":"W.Streak3","title":"Đạt chuỗi thắng 3 trận trong tuần","scope":"Weekly","metric":"BestStreak","target":3,"reward":{"coins":350,"gems":0,"xp":20}}'::jsonb) ON CONFLICT(Code) DO UPDATE SET MissionName=EXCLUDED.MissionName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,Config=EXCLUDED.Config;
INSERT INTO Cosmetics(CurrencyId,ItemCode,CosmeticName,Description,Price,Type,Config) SELECT CurrencyId,'paddle.default','PICKLEVERSE','Vợt mặc định của PickleVerse. Thiết kế gọn gàng, luôn sẵn sàng cho những trận đấu đầu tiên.',0,'Paddle','{"id":"paddle.default","title":"PICKLEVERSE","description":"Vợt mặc định của PickleVerse. Thiết kế gọn gàng, luôn sẵn sàng cho những trận đấu đầu tiên.","collection":"BỘ SƯU TẬP NGUYÊN BẢN","tokenpadPrice":0,"elitePrice":0,"gemstone":false,"releaseOrder":0}'::jsonb FROM Currencies WHERE CurrencyName='TOKENPAD' ON CONFLICT(ItemCode) DO UPDATE SET CurrencyId=EXCLUDED.CurrencyId,CosmeticName=EXCLUDED.CosmeticName,Description=EXCLUDED.Description,Price=EXCLUDED.Price,Type=EXCLUDED.Type,Config=EXCLUDED.Config;
INSERT INTO CosmeticPrices(CosmeticId,CurrencyId,Price) SELECT c.CosmeticId,u.CurrencyId,0 FROM Cosmetics c CROSS JOIN Currencies u WHERE c.ItemCode='paddle.default' AND u.CurrencyName='TOKENPAD' ON CONFLICT(CosmeticId,CurrencyId) DO UPDATE SET Price=EXCLUDED.Price;
INSERT INTO Cosmetics(CurrencyId,ItemCode,CosmeticName,Description,Price,Type,Config) SELECT CurrencyId,'paddle.gemstone','GEMSTONE','Viền đá quý, sắc vàng và mặt carbon. Ánh lấp lánh tinh tế khi xoay dưới ánh sáng.',650,'Paddle','{"id":"paddle.gemstone","title":"GEMSTONE","description":"Viền đá quý, sắc vàng và mặt carbon. Ánh lấp lánh tinh tế khi xoay dưới ánh sáng.","collection":"BỘ SƯU TẬP TINH ANH","tokenpadPrice":0,"elitePrice":650,"gemstone":true,"releaseOrder":2}'::jsonb FROM Currencies WHERE CurrencyName='ELITE TOKEN' ON CONFLICT(ItemCode) DO UPDATE SET CurrencyId=EXCLUDED.CurrencyId,CosmeticName=EXCLUDED.CosmeticName,Description=EXCLUDED.Description,Price=EXCLUDED.Price,Type=EXCLUDED.Type,Config=EXCLUDED.Config;
INSERT INTO CosmeticPrices(CosmeticId,CurrencyId,Price) SELECT c.CosmeticId,u.CurrencyId,650 FROM Cosmetics c CROSS JOIN Currencies u WHERE c.ItemCode='paddle.gemstone' AND u.CurrencyName='ELITE TOKEN' ON CONFLICT(CosmeticId,CurrencyId) DO UPDATE SET Price=EXCLUDED.Price;
INSERT INTO Cosmetics(CurrencyId,ItemCode,CosmeticName,Description,Price,Type,Config) SELECT CurrencyId,'paddle.thot','THOT','Hoa văn nổi bật cho phong cách thi đấu riêng. Chọn Tokenpad hoặc Elite để sở hữu.',900,'Paddle','{"id":"paddle.thot","title":"THOT","description":"Hoa văn nổi bật cho phong cách thi đấu riêng. Chọn Tokenpad hoặc Elite để sở hữu.","collection":"BỘ SƯU TẬP DẤU ẤN","tokenpadPrice":900,"elitePrice":250,"gemstone":false,"releaseOrder":1}'::jsonb FROM Currencies WHERE CurrencyName='TOKENPAD' ON CONFLICT(ItemCode) DO UPDATE SET CurrencyId=EXCLUDED.CurrencyId,CosmeticName=EXCLUDED.CosmeticName,Description=EXCLUDED.Description,Price=EXCLUDED.Price,Type=EXCLUDED.Type,Config=EXCLUDED.Config;
INSERT INTO CosmeticPrices(CosmeticId,CurrencyId,Price) SELECT c.CosmeticId,u.CurrencyId,900 FROM Cosmetics c CROSS JOIN Currencies u WHERE c.ItemCode='paddle.thot' AND u.CurrencyName='TOKENPAD' ON CONFLICT(CosmeticId,CurrencyId) DO UPDATE SET Price=EXCLUDED.Price;
INSERT INTO CosmeticPrices(CosmeticId,CurrencyId,Price) SELECT c.CosmeticId,u.CurrencyId,250 FROM Cosmetics c CROSS JOIN Currencies u WHERE c.ItemCode='paddle.thot' AND u.CurrencyName='ELITE TOKEN' ON CONFLICT(CosmeticId,CurrencyId) DO UPDATE SET Price=EXCLUDED.Price;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Serve','Giao bóng đúng ô') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('serve','Giao bóng đúng ô','Giao dưới tay từ sau đường biên cuối sân. Bóng phải sang ô chéo và vượt cả vạch vùng cấm vô lê.','Serve',6,'{"coins":150,"gems":0,"xp":5}'::jsonb,1,'{"id":"serve","title":"Giao bóng đúng ô","explanation":"Giao dưới tay từ sau đường biên cuối sân. Bóng phải sang ô chéo và vượt cả vạch vùng cấm vô lê.","coaching":"Đứng trong vùng sáng phía sau đường biên cuối sân. Giữ rồi thả Đánh bạt; không bước vào sân trước khi chạm bóng.","skill":"Serve","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[1],"prerequisites":[]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'serve',TechniqueId,'Giao bóng đúng ô','Đứng trong vùng sáng phía sau đường biên cuối sân. Giữ rồi thả Đánh bạt; không bước vào sân trước khi chạm bóng.' FROM Techniques WHERE TechniqueName='Serve' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,6 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='serve' AND d.Code='serve' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Drive','Đánh bạt về cuối sân') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('drive','Đánh bạt về cuối sân','Đón bóng sau một lần nảy, đánh Đánh bạt vào vùng sâu đã đánh dấu.','Drive',6,'{"coins":150,"gems":0,"xp":5}'::jsonb,2,'{"id":"drive","title":"Đánh bạt về cuối sân","explanation":"Đón bóng sau một lần nảy, đánh Đánh bạt vào vùng sâu đã đánh dấu.","coaching":"Chuẩn bị vợt phía trước. Thả phím sớm hơn lúc bóng tới để hoạt ảnh đưa vợt đến điểm tiếp xúc.","skill":"Drive","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[1],"prerequisites":[]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'drive',TechniqueId,'Đánh bạt về cuối sân','Chuẩn bị vợt phía trước. Thả phím sớm hơn lúc bóng tới để hoạt ảnh đưa vợt đến điểm tiếp xúc.' FROM Techniques WHERE TechniqueName='Drive' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,6 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='drive' AND d.Code='drive' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Dink','Bóng ngắn kiểm soát vùng cấm vô lê') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('dink','Bóng ngắn kiểm soát vùng cấm vô lê','Đánh nhẹ sau nảy, đưa bóng vào vùng cấm vô lê đối phương. Được đứng trong vùng cấm vô lê khi đánh bóng đã nảy.','Dink',6,'{"coins":150,"gems":0,"xp":5}'::jsonb,3,'{"id":"dink","title":"Bóng ngắn kiểm soát vùng cấm vô lê","explanation":"Đánh nhẹ sau nảy, đưa bóng vào vùng cấm vô lê đối phương. Được đứng trong vùng cấm vô lê khi đánh bóng đã nảy.","coaching":"Đón bóng trước người. Không dùng lực mạnh; ưu tiên kiểm soát và cung bóng thấp.","skill":"Dink","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[2],"prerequisites":[]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'dink',TechniqueId,'Bóng ngắn kiểm soát vùng cấm vô lê','Đón bóng trước người. Không dùng lực mạnh; ưu tiên kiểm soát và cung bóng thấp.' FROM Techniques WHERE TechniqueName='Dink' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,6 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='dink' AND d.Code='dink' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Volley','Vô lê ngoài vùng cấm vô lê') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('volley','Vô lê ngoài vùng cấm vô lê','Chặn bóng trước khi nảy. Cả hai chân và đà sau cú đánh phải ở ngoài vùng cấm vô lê.','Volley',6,'{"coins":150,"gems":0,"xp":5}'::jsonb,4,'{"id":"volley","title":"Vô lê ngoài vùng cấm vô lê","explanation":"Chặn bóng trước khi nảy. Cả hai chân và đà sau cú đánh phải ở ngoài vùng cấm vô lê.","coaching":"Giữ vị trí sau vạch sáng. Chọn Vô lê khi bóng còn trên không; không lao vào vùng cấm vô lê sau cú đánh.","skill":"Volley","tier":0,"trials":8,"required":6,"reward":150,"xp":5,"masks":[4],"prerequisites":[]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'volley',TechniqueId,'Vô lê ngoài vùng cấm vô lê','Giữ vị trí sau vạch sáng. Chọn Vô lê khi bóng còn trên không; không lao vào vùng cấm vô lê sau cú đánh.' FROM Techniques WHERE TechniqueName='Volley' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,6 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='volley' AND d.Code='volley' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Modifiers','Lực và hướng chéo') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('modifiers','Lực và hướng chéo','Luân phiên Đánh bạt, Bóng ngắn, Vô lê với tăng lực hoặc đánh chéo theo chỉ dẫn từng bóng.','Modifiers',7,'{"coins":225,"gems":0,"xp":7}'::jsonb,5,'{"id":"modifiers","title":"Lực và hướng chéo","explanation":"Luân phiên Đánh bạt, Bóng ngắn, Vô lê với tăng lực hoặc đánh chéo theo chỉ dẫn từng bóng.","coaching":"Giữ modifier cho đến khi thả nút đánh. Quan sát vùng đích đổi bên khi cần đánh chéo.","skill":"Modifiers","tier":1,"trials":10,"required":7,"reward":225,"xp":7,"masks":[9,17,10,18,12,20],"prerequisites":["drive","dink","volley"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'modifiers',TechniqueId,'Lực và hướng chéo','Giữ modifier cho đến khi thả nút đánh. Quan sát vùng đích đổi bên khi cần đánh chéo.' FROM Techniques WHERE TechniqueName='Modifiers' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,7 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='modifiers' AND d.Code='modifiers' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Drop','Third-shot bỏ nhỏ') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('drop','Third-shot bỏ nhỏ','Sau giao và trả giao đã nảy, đánh cú thứ ba mềm vào vùng cấm vô lê để lấy thời gian lên lưới.','Drop',7,'{"coins":225,"gems":0,"xp":7}'::jsonb,6,'{"id":"drop","title":"Third-shot bỏ nhỏ","explanation":"Sau giao và trả giao đã nảy, đánh cú thứ ba mềm vào vùng cấm vô lê để lấy thời gian lên lưới.","coaching":"Ghép Đánh bạt + Bóng ngắn. Đây là cú đánh sau nảy; bóng phải rơi trong vùng sáng gần lưới.","skill":"Drop","tier":1,"trials":10,"required":7,"reward":225,"xp":7,"masks":[3],"prerequisites":["drive","dink"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'drop',TechniqueId,'Third-shot bỏ nhỏ','Ghép Đánh bạt + Bóng ngắn. Đây là cú đánh sau nảy; bóng phải rơi trong vùng sáng gần lưới.' FROM Techniques WHERE TechniqueName='Drop' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,7 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='drop' AND d.Code='drop' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Reset','Hãm bóng bóng nhanh') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('reset','Hãm bóng bóng nhanh','Dùng Hãm bóng để giảm nhịp bóng nhanh, đưa bóng về vùng cấm vô lê thay vì cố đánh mạnh.','Reset',7,'{"coins":225,"gems":0,"xp":7}'::jsonb,7,'{"id":"reset","title":"Hãm bóng bóng nhanh","explanation":"Dùng Hãm bóng để giảm nhịp bóng nhanh, đưa bóng về vùng cấm vô lê thay vì cố đánh mạnh.","coaching":"Ghép Bóng ngắn + Vô lê, tiếp bóng trên không và giữ cả hai chân ngoài vùng cấm vô lê.","skill":"Reset","tier":1,"trials":10,"required":7,"reward":225,"xp":7,"masks":[6],"prerequisites":["dink","volley"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'reset',TechniqueId,'Hãm bóng bóng nhanh','Ghép Bóng ngắn + Vô lê, tiếp bóng trên không và giữ cả hai chân ngoài vùng cấm vô lê.' FROM Techniques WHERE TechniqueName='Reset' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,7 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='reset' AND d.Code='reset' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Smash','Đập bóng bóng bổng') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('smash','Đập bóng bóng bổng','Đợi bóng bổng đủ cao rồi đánh Đập bóng vào vùng sâu. Không vô lê trong vùng cấm vô lê.','Smash',9,'{"coins":300,"gems":0,"xp":10}'::jsonb,8,'{"id":"smash","title":"Đập bóng bóng bổng","explanation":"Đợi bóng bổng đủ cao rồi đánh Đập bóng vào vùng sâu. Không vô lê trong vùng cấm vô lê.","coaching":"Ghép Đánh bạt + Vô lê. Quan sát vòng điểm tiếp bóng trên sân và thả khi chỉ báo thời điểm vào vùng xanh.","skill":"Smash","tier":2,"trials":12,"required":9,"reward":300,"xp":10,"masks":[5],"prerequisites":["volley","modifiers"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'smash',TechniqueId,'Đập bóng bóng bổng','Ghép Đánh bạt + Vô lê. Quan sát vòng điểm tiếp bóng trên sân và thả khi chỉ báo thời điểm vào vùng xanh.' FROM Techniques WHERE TechniqueName='Smash' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,9 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='smash' AND d.Code='smash' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Footwork','Di chuyển và hồi vị') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('footwork','Di chuyển và hồi vị','Đón bóng luân phiên hai bên sân, đánh Đánh bạt rồi trở về vị trí hồi phục.','Footwork',9,'{"coins":300,"gems":0,"xp":10}'::jsonb,9,'{"id":"footwork","title":"Di chuyển và hồi vị","explanation":"Đón bóng luân phiên hai bên sân, đánh Đánh bạt rồi trở về vị trí hồi phục.","coaching":"Di chuyển A/D hoặc cần điều khiển tới vùng đón bóng. Có thể dùng chạy; sau cú đánh quay về vòng hồi vị.","skill":"Footwork","tier":2,"trials":12,"required":9,"reward":300,"xp":10,"masks":[1,17],"prerequisites":["drive","modifiers"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'footwork',TechniqueId,'Di chuyển và hồi vị','Di chuyển A/D hoặc cần điều khiển tới vùng đón bóng. Có thể dùng chạy; sau cú đánh quay về vòng hồi vị.' FROM Techniques WHERE TechniqueName='Footwork' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,9 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='footwork' AND d.Code='footwork' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('Approach','Bỏ nhỏ rồi tiến lên') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('approach','Bỏ nhỏ rồi tiến lên','Đánh bỏ nhỏ từ cuối sân, tiến lên vùng sẵn sàng trước vùng cấm vô lê. Dừng có kiểm soát trước lượt tiếp theo.','Approach',9,'{"coins":300,"gems":0,"xp":10}'::jsonb,10,'{"id":"approach","title":"Bỏ nhỏ rồi tiến lên","explanation":"Đánh bỏ nhỏ từ cuối sân, tiến lên vùng sẵn sàng trước vùng cấm vô lê. Dừng có kiểm soát trước lượt tiếp theo.","coaching":"Ghép Đánh bạt + Bóng ngắn rồi di chuyển W lên vùng sáng. Đừng chạy qua vạch vùng cấm vô lê để đón vô lê.","skill":"Approach","tier":2,"trials":12,"required":9,"reward":300,"xp":10,"masks":[3],"prerequisites":["drop","footwork"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'approach',TechniqueId,'Bỏ nhỏ rồi tiến lên','Ghép Đánh bạt + Bóng ngắn rồi di chuyển W lên vùng sáng. Đừng chạy qua vạch vùng cấm vô lê để đón vô lê.' FROM Techniques WHERE TechniqueName='Approach' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,9 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='approach' AND d.Code='approach' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('DoublesCover','Đánh đôi: giữ khoảng cách') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('doubles-cover','Đánh đôi: giữ khoảng cách','Hai người giữ hàng ngang, dịch chuyển cùng phía bóng. Đánh bóng phần sân của bạn và giữ khe giữa đội nhỏ.','DoublesCover',9,'{"coins":450,"gems":0,"xp":15}'::jsonb,11,'{"id":"doubles-cover","title":"Đánh đôi: giữ khoảng cách","explanation":"Hai người giữ hàng ngang, dịch chuyển cùng phía bóng. Đánh bóng phần sân của bạn và giữ khe giữa đội nhỏ.","coaching":"Đồng đội AI đi cùng bạn. Dịch chuyển ngang, đánh Vô lê theo chỉ dẫn, rồi hồi vị để cả đội giữ hàng ngang.","skill":"DoublesCover","tier":3,"trials":12,"required":9,"reward":450,"xp":15,"masks":[4,20],"prerequisites":["volley","footwork"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'doubles-cover',TechniqueId,'Đánh đôi: giữ khoảng cách','Đồng đội AI đi cùng bạn. Dịch chuyển ngang, đánh Vô lê theo chỉ dẫn, rồi hồi vị để cả đội giữ hàng ngang.' FROM Techniques WHERE TechniqueName='DoublesCover' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,9 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='doubles-cover' AND d.Code='doubles-cover' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Techniques(TechniqueName,Description) VALUES ('DoublesTransition','Đánh đôi: bỏ nhỏ và phối hợp') ON CONFLICT(TechniqueName) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,Type,Target,Reward,SortOrder,Config) VALUES ('doubles-transition','Đánh đôi: bỏ nhỏ và phối hợp','Đội giao chờ trả giao nảy, dùng bỏ nhỏ tiến lên cùng nhau rồi chuyển sang bóng ngắn và vô lê. Có lượt bóng của đồng đội AI.','DoublesTransition',9,'{"coins":450,"gems":0,"xp":15}'::jsonb,12,'{"id":"doubles-transition","title":"Đánh đôi: bỏ nhỏ và phối hợp","explanation":"Đội giao chờ trả giao nảy, dùng bỏ nhỏ tiến lên cùng nhau rồi chuyển sang bóng ngắn và vô lê. Có lượt bóng của đồng đội AI.","coaching":"Theo dấu vị trí của bạn. Sau bỏ nhỏ tiến lên cùng đồng đội; khi hiện ‘Bóng của đồng đội’, giữ hàng ngang và để AI xử lý.","skill":"DoublesTransition","tier":3,"trials":12,"required":9,"reward":450,"xp":15,"masks":[3,2,4],"prerequisites":["approach","doubles-cover","reset"]}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,Type=EXCLUDED.Type,Target=EXCLUDED.Target,Reward=EXCLUDED.Reward,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'doubles-transition',TechniqueId,'Đánh đôi: bỏ nhỏ và phối hợp','Theo dấu vị trí của bạn. Sau bỏ nhỏ tiến lên cùng đồng đội; khi hiện ‘Bóng của đồng đội’, giữ hàng ngang và để AI xử lý.' FROM Techniques WHERE TechniqueName='DoublesTransition' ON CONFLICT(Code) DO UPDATE SET TechniqueId=EXCLUDED.TechniqueId,DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,9 FROM Lessons l CROSS JOIN Drills d WHERE l.Code='doubles-transition' AND d.Code='doubles-transition' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO PlayerCosmetics(PlayerId,CosmeticId,Equipped) SELECT p.UserId,c.CosmeticId,NOT EXISTS(SELECT 1 FROM PlayerCosmetics pc WHERE pc.PlayerId=p.UserId AND pc.Equipped) FROM PlayerProfiles p CROSS JOIN Cosmetics c WHERE c.ItemCode='paddle.default' ON CONFLICT(PlayerId,CosmeticId) DO NOTHING;
INSERT INTO Lessons(Code,LessonName,Description,SortOrder,Type,Target,Config,Reward) VALUES ('serve-alternating','Giao bóng luân phiên hai ô','Luân phiên giao từ hai bên đường cuối sân. Đưa bóng vào đúng ô chéo và ngoài Kitchen.',13,'Serve',7,'{"id":"serve-alternating","title":"Giao bóng luân phiên hai ô","skill":"Serve","tier":1,"masks":[1],"explanation":"Luân phiên giao từ hai bên đường cuối sân. Đưa bóng vào đúng ô chéo và ngoài Kitchen.","coaching":"Quan sát vị trí bắt đầu đổi bên. Giữ chân sau đường cuối sân và giao vào vùng đích.","prerequisites":["serve"],"alternateTargets":true,"trials":10,"required":7,"reward":225,"xp":7,"requiredLevel":1,"targetRadius":0.9,"targetDepth":0,"requireRecovery":false}'::jsonb,'{"coins": 225, "gems": 0, "xp": 7}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config,Reward=EXCLUDED.Reward;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'serve-alternating',TechniqueId,'Giao bóng luân phiên hai ô','Luân phiên giao từ hai bên đường cuối sân. Đưa bóng vào đúng ô chéo và ngoài Kitchen.' FROM Techniques WHERE TechniqueName='Serve' ON CONFLICT(Code) DO UPDATE SET DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,7 FROM Lessons l JOIN Drills d ON d.Code=l.Code WHERE l.Code='serve-alternating' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Lessons(Code,LessonName,Description,SortOrder,Type,Target,Config,Reward) VALUES ('dink-cross','Dink chéo sân','Đánh Dink chéo sân sau một lần nảy, đưa bóng vào vùng Kitchen đã đánh dấu.',14,'Dink',7,'{"id":"dink-cross","title":"Dink chéo sân","skill":"Dink","tier":1,"masks":[18],"explanation":"Đánh Dink chéo sân sau một lần nảy, đưa bóng vào vùng Kitchen đã đánh dấu.","coaching":"Giữ hướng chéo đến khi thả nút. Ưu tiên kiểm soát bóng ngắn và đứng cân bằng.","prerequisites":["dink","modifiers"],"trials":10,"required":7,"reward":225,"xp":7,"requiredLevel":1,"targetRadius":0.9,"targetDepth":0,"alternateTargets":false,"requireRecovery":false}'::jsonb,'{"coins": 225, "gems": 0, "xp": 7}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config,Reward=EXCLUDED.Reward;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'dink-cross',TechniqueId,'Dink chéo sân','Đánh Dink chéo sân sau một lần nảy, đưa bóng vào vùng Kitchen đã đánh dấu.' FROM Techniques WHERE TechniqueName='Dink' ON CONFLICT(Code) DO UPDATE SET DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,7 FROM Lessons l JOIN Drills d ON d.Code=l.Code WHERE l.Code='dink-cross' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
INSERT INTO Lessons(Code,LessonName,Description,SortOrder,Type,Target,Config,Reward) VALUES ('volley-recovery','Volley rồi hồi vị','Đón Volley ngoài Kitchen rồi trở về vòng hồi vị trước lượt tiếp theo.',15,'Volley',9,'{"id":"volley-recovery","title":"Volley rồi hồi vị","skill":"Volley","tier":2,"masks":[4,20],"explanation":"Đón Volley ngoài Kitchen rồi trở về vòng hồi vị trước lượt tiếp theo.","coaching":"Dừng đà sau cú đánh, giữ chân ngoài Kitchen và quay về vòng hồi vị.","prerequisites":["volley","footwork"],"requireRecovery":true,"trials":12,"required":9,"reward":300,"xp":10,"requiredLevel":1,"targetRadius":0.7,"targetDepth":0,"alternateTargets":false}'::jsonb,'{"coins": 300, "gems": 0, "xp": 10}'::jsonb) ON CONFLICT(Code) DO UPDATE SET LessonName=EXCLUDED.LessonName,Description=EXCLUDED.Description,SortOrder=EXCLUDED.SortOrder,Config=EXCLUDED.Config,Reward=EXCLUDED.Reward;
INSERT INTO Drills(Code,TechniqueId,DrillName,Description) SELECT 'volley-recovery',TechniqueId,'Volley rồi hồi vị','Đón Volley ngoài Kitchen rồi trở về vòng hồi vị trước lượt tiếp theo.' FROM Techniques WHERE TechniqueName='Volley' ON CONFLICT(Code) DO UPDATE SET DrillName=EXCLUDED.DrillName,Description=EXCLUDED.Description;
INSERT INTO LessonDrills(LessonId,DrillId,Target) SELECT l.LessonId,d.DrillId,9 FROM Lessons l JOIN Drills d ON d.Code=l.Code WHERE l.Code='volley-recovery' ON CONFLICT(LessonId,DrillId) DO UPDATE SET Target=EXCLUDED.Target;
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
CREATE TABLE ExpertReviews (
    ReviewId UUID PRIMARY KEY,
    BatchId UUID NOT NULL REFERENCES AiTelemetrySessions(BatchId),
    ReviewerId UUID NOT NULL REFERENCES Users(UserId),
    Version BIGINT NOT NULL CHECK(Version>0),
    Data JSONB NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    UNIQUE(BatchId,Version)
);

--#region Coaching demo (only seeded when pickleverse.seed_coaching_demo is on)
CREATE TABLE IF NOT EXISTS CoachingDemoSessions (
    SessionId VARCHAR(64) PRIMARY KEY,
    PlayerCode VARCHAR(48) NOT NULL UNIQUE,
    Tier VARCHAR(16) NOT NULL CHECK(Tier IN ('beginner','intermediate','advanced')),
    Split VARCHAR(16) NOT NULL CHECK(Split IN ('train','calibration','test')),
    GameVersion VARCHAR(80) NOT NULL,
    Shots JSONB NOT NULL CHECK(jsonb_typeof(Shots) = 'array')
);
-- Exact seed 42 dataset from AiService/simulate_training.py.
-- Trial fields: pressed seconds, released seconds, input mask, x, z, timing error, recovered.
DO $coaching_demo$
BEGIN
    IF current_setting('pickleverse.seed_coaching_demo', true) = 'on' THEN
        WITH fixtures(player_code, tier, split, trials) AS (
        VALUES
        ('sim-beginner-01','beginner','train','[[0.217,0.455,1,0.389,-2.419,null,null],[3.792,3.917,4,-0.619,-2.844,0.032,true],[7.233,7.44,4,-0.615,-2.752,0.0632,false],[10.895,11.04,4,1.006,-3.03,0.0417,true],[14.202,14.419,4,0.676,-2.597,-0.3228,true],[17.791,18.01,4,0.796,-2.765,-0.0309,false],[21.243,21.409,1,-0.924,-3.144,null,null],[24.698,24.92,1,-0.297,-2.993,null,null],[28.172,28.442,1,0.326,-2.73,null,null],[31.788,31.934,4,-0.265,-2.312,-0.0608,false],[35.412,35.569,4,-1.029,-3.053,-0.0854,true],[38.682,38.942,4,-0.408,-2.679,-0.1978,false],[42.243,42.453,1,-0.522,-2.757,null,null],[45.925,46.08,4,1.095,-2.84,0.2532,true],[49.189,49.409,1,0.643,-2.936,null,null],[52.634,52.913,4,0.064,-2.332,0.0712,false],[56.373,56.536,4,0.31,-3.277,0.0402,false],[59.731,59.991,4,-0.521,-2.849,0.1959,false],[63.243,63.46,4,-0.764,-2.561,0.3456,false],[66.936,67.056,4,-0.387,-3.379,0.3259,false],[70.202,70.462,1,0.983,-3.306,null,null],[73.755,73.997,1,0.585,-3.259,null,null],[77.333,77.495,4,0.819,-2.935,-0.0712,true],[80.856,81.008,4,-0.414,-2.305,-0.0345,false]]'::jsonb),
        ('sim-beginner-02','beginner','train','[[0.25,0.424,1,0.194,-3.147,null,null],[3.723,3.944,1,-0.596,-2.404,null,null],[7.414,7.572,1,0.372,-3.164,null,null],[10.715,10.926,4,-0.06,-2.537,0.0759,true],[14.291,14.486,4,0.504,-2.659,-0.0455,false],[17.746,17.92,4,0.796,-3.126,0.1007,false],[21.188,21.456,1,-0.125,-2.453,null,null],[24.73,25.01,1,0.739,-2.334,null,null],[28.438,28.585,4,-0.032,-3.165,0.1898,false],[31.634,31.912,4,-0.517,-2.538,-0.1034,false],[35.364,35.599,4,-0.759,-3.074,0.0851,false],[38.776,39.016,4,-0.974,-2.757,-0.2411,true],[42.442,42.592,1,0.209,-2.657,null,null],[45.685,45.947,1,-0.558,-2.746,null,null],[49.311,49.524,4,0.05,-2.372,0.0376,true],[52.86,53.043,1,0.378,-3.07,null,null],[56.331,56.463,4,-0.092,-2.302,0.0992,true],[59.692,59.953,4,0.834,-2.994,0.0412,true],[63.349,63.567,4,1.072,-2.681,0.3096,true],[66.892,67.033,4,-0.846,-3.282,0.0558,false],[70.219,70.454,4,-0.652,-2.702,0.0279,false],[73.881,74.069,1,-0.491,-3.396,null,null],[77.392,77.554,4,0.531,-2.793,0.2099,false],[80.641,80.902,1,0.889,-2.8,null,null]]'::jsonb),
        ('sim-beginner-03','beginner','train','[[0.261,0.43,1,0.878,-2.524,null,null],[3.918,4.072,4,-0.551,-3.287,-0.0918,false],[7.358,7.577,4,-0.76,-2.377,0.3349,false],[10.838,11.076,1,-0.369,-2.376,null,null],[14.31,14.56,4,-0.513,-2.534,-0.2247,true],[17.918,18.074,4,0.696,-2.894,-0.0144,true],[21.232,21.405,1,0.802,-2.336,null,null],[24.772,24.956,4,1.059,-2.81,0.3163,false],[28.274,28.423,4,1.018,-3.108,0.1937,false],[31.761,31.963,4,-0.253,-2.766,0.1709,true],[35.274,35.542,1,0.085,-2.609,null,null],[38.87,39.048,4,-0.946,-2.669,0.0126,false],[42.375,42.544,4,-0.202,-2.957,0.1836,true],[45.655,45.925,4,0.39,-2.407,-0.0184,false],[49.211,49.4,4,0.176,-2.68,-0.008,false],[52.792,52.988,1,0.883,-2.524,null,null],[56.232,56.434,1,0.292,-3.031,null,null],[59.836,60.064,4,-0.606,-3.181,0.1808,false],[63.384,63.57,1,0.285,-3.186,null,null],[66.88,67.039,4,0.343,-3.394,0.09,false],[70.366,70.554,1,-0.713,-2.346,null,null],[73.844,74.004,1,0.766,-2.898,null,null],[77.282,77.56,4,0.21,-2.355,0.2284,false],[80.793,81.001,4,0.874,-2.582,-0.0525,false]]'::jsonb),
        ('sim-beginner-04','beginner','train','[[0.325,0.528,4,0.279,-3.098,0.1682,true],[3.786,3.957,1,0.088,-3.248,null,null],[7.213,7.446,4,-0.959,-2.952,-0.0858,true],[10.771,10.984,4,0.43,-2.458,0.0194,false],[14.3,14.476,1,0.558,-2.461,null,null],[17.851,18.091,4,0.101,-2.736,0.0779,false],[21.177,21.406,4,-0.211,-3.218,0.1594,false],[24.802,24.926,4,-0.233,-2.779,0.2642,true],[28.311,28.492,1,-0.634,-3.04,null,null],[31.812,32.052,4,0.73,-3.123,0.0944,true],[35.124,35.404,4,-0.33,-2.685,0.1002,false],[38.967,39.09,1,-0.765,-3.261,null,null],[42.379,42.534,4,0.439,-2.556,-0.1474,true],[45.883,46.021,4,0.702,-2.339,0.0849,true],[49.352,49.535,4,0.473,-3.316,0.0799,false],[52.781,53.025,1,0.771,-2.74,null,null],[56.179,56.424,4,-0.336,-2.929,-0.0592,true],[59.933,60.07,4,1.014,-2.701,0.1842,false],[63.304,63.541,4,1.024,-3.103,0.1236,false],[66.824,66.987,4,0.774,-2.486,-0.1092,true],[70.382,70.576,1,0.243,-2.983,null,null],[73.757,73.906,4,-0.633,-2.522,-0.1015,false],[77.183,77.455,1,-0.912,-2.608,null,null],[80.768,80.998,4,0.321,-2.86,0.3111,false]]'::jsonb),
        ('sim-beginner-05','beginner','train','[[0.325,0.538,4,-0.059,-2.816,0.1285,false],[3.817,4.049,4,-0.504,-3.123,0.1407,true],[7.357,7.507,4,-0.624,-2.867,0.137,false],[10.93,11.095,4,-0.879,-3.186,0.0847,true],[14.231,14.507,1,0.117,-2.633,null,null],[17.726,17.925,4,0.82,-2.769,0.1621,false],[21.36,21.488,1,0.97,-2.874,null,null],[24.932,25.064,4,0.285,-3.341,0.1822,true],[28.357,28.599,1,0.234,-2.53,null,null],[31.753,31.945,4,-0.126,-2.454,0.2222,false],[35.243,35.461,4,0.528,-2.358,0.098,false],[38.799,38.931,1,-1.094,-2.904,null,null],[42.362,42.519,4,0.455,-2.627,0.1681,false],[45.791,46.037,4,0.275,-2.673,0.2113,false],[49.278,49.53,4,-0.943,-3.217,0.01,true],[52.884,53.05,4,-0.826,-2.642,-0.0359,false],[56.373,56.499,1,-0.15,-3.045,null,null],[59.676,59.95,1,0.739,-2.767,null,null],[63.362,63.59,4,-0.507,-3.356,-0.2578,false],[66.727,66.994,4,-0.701,-2.756,-0.0399,true],[70.243,70.47,4,0.787,-3.037,-0.0546,false],[73.708,73.958,4,0.11,-2.9,0.0213,false],[77.203,77.481,4,0.347,-2.803,0.1896,false],[80.697,80.938,4,0.276,-2.564,0.1245,false]]'::jsonb),
        ('sim-beginner-06','beginner','train','[[0.349,0.488,4,1.041,-2.73,0.0899,false],[3.731,4.01,1,0.908,-2.892,null,null],[7.223,7.423,4,0.477,-2.84,0.1691,true],[10.908,11.067,4,0.113,-2.978,0.2365,false],[14.327,14.573,1,-0.187,-2.372,null,null],[17.837,18.002,4,-0.443,-2.754,0.0027,false],[21.292,21.498,1,-0.341,-2.793,null,null],[24.838,25.009,4,-0.685,-2.633,-0.011,false],[28.176,28.409,4,0.685,-2.975,0.0381,false],[31.865,32.064,4,-1.019,-2.847,-0.1628,false],[35.295,35.488,4,0.489,-2.949,-0.073,false],[38.656,38.931,4,-0.355,-2.638,-0.0826,false],[42.401,42.572,4,0.481,-2.565,-0.1449,false],[45.686,45.907,1,0.926,-2.303,null,null],[49.413,49.549,4,0.294,-2.44,-0.1251,false],[52.742,52.909,4,-0.275,-3.24,0.1615,false],[56.366,56.513,4,-0.926,-2.442,0.0056,false],[59.768,59.929,4,-0.538,-3.39,0.0106,false],[63.435,63.58,4,-0.128,-3.02,-0.0922,false],[66.798,66.95,4,-0.254,-2.868,-0.024,true],[70.235,70.514,4,-0.45,-2.324,0.0209,false],[73.909,74.037,4,0.234,-2.854,-0.0132,false],[77.24,77.457,4,-0.325,-2.7,-0.0741,false],[80.811,81.032,4,0.887,-2.689,-0.0661,true]]'::jsonb),
        ('sim-beginner-07','beginner','calibration','[[0.379,0.546,1,0.544,-3.207,null,null],[3.651,3.926,4,0.068,-2.395,0.1048,false],[7.254,7.451,4,0.674,-2.579,-0.0025,false],[10.67,10.928,4,0.493,-2.322,0.0963,false],[14.314,14.561,4,-1.069,-2.81,-0.1726,false],[17.747,18.017,4,-0.862,-3.143,0.0962,true],[21.311,21.577,4,-0.613,-3.33,0.1777,true],[24.711,24.982,1,-0.43,-2.858,null,null],[28.277,28.419,4,-0.102,-2.662,-0.2487,false],[31.85,32.089,4,-0.76,-2.944,0.1721,false],[35.411,35.59,1,-0.125,-2.354,null,null],[38.841,39.071,1,0.098,-2.324,null,null],[42.322,42.472,4,-0.831,-2.467,0.1334,false],[45.817,46.033,4,-1.053,-2.535,0.0385,false],[49.261,49.414,4,-0.625,-2.443,0.1106,true],[52.81,52.93,4,0.788,-3.241,0.1109,true],[56.41,56.532,1,0.638,-3.138,null,null],[59.837,59.965,1,0.532,-2.821,null,null],[63.305,63.549,4,0.029,-3.28,0.1158,false],[66.844,67.089,1,0.807,-2.826,null,null],[70.362,70.492,4,-0.046,-2.958,-0.0303,false],[73.698,73.915,1,-0.955,-3.097,null,null],[77.355,77.527,4,1.088,-2.816,-0.1195,false],[80.789,81.021,1,0.776,-2.684,null,null]]'::jsonb),
        ('sim-beginner-08','beginner','calibration','[[0.286,0.443,4,-0.354,-2.901,-0.1662,false],[3.693,3.919,4,-0.277,-3.232,0.1116,false],[7.181,7.419,1,0.686,-2.788,null,null],[10.844,11.017,4,-0.831,-3.011,0.0386,false],[14.315,14.55,4,1.03,-2.74,-0.0497,true],[17.894,18.031,1,0.76,-2.996,null,null],[21.304,21.553,4,0.759,-2.328,0.2167,false],[24.899,25.023,4,0.944,-2.488,0.0538,false],[28.341,28.462,4,0.814,-2.777,0.1547,false],[31.803,31.928,4,0.541,-3.163,-0.0522,true],[35.333,35.544,4,-0.913,-3.342,0.1277,true],[38.86,39.024,4,0.356,-2.866,-0.0444,false],[42.258,42.423,4,0.393,-2.865,0.1051,false],[45.693,45.909,4,-1.083,-3.068,0.0828,true],[49.226,49.466,1,-0.713,-2.982,null,null],[52.788,53.041,4,0.674,-3.321,0.1403,false],[56.141,56.408,1,0.797,-2.767,null,null],[59.828,60.015,4,-0.847,-3.377,-0.0542,false],[63.432,63.566,4,0.758,-3.132,0.2962,false],[66.835,67.005,4,-0.353,-3.034,0.1491,true],[70.326,70.502,4,0.5,-2.499,0.2147,false],[73.795,73.947,1,0.225,-2.564,null,null],[77.287,77.531,1,-0.013,-2.57,null,null],[80.784,81.052,4,0.142,-2.701,-0.1417,false]]'::jsonb),
        ('sim-beginner-09','beginner','test','[[0.239,0.43,1,-0.434,-3.098,null,null],[3.741,3.911,4,-0.106,-3.337,0.0603,false],[7.37,7.571,4,-0.082,-2.79,-0.0187,false],[10.829,11.079,4,0.334,-3.046,-0.0501,true],[14.246,14.421,4,0.471,-2.845,0.0445,true],[17.76,17.95,4,0.05,-3.225,-0.0507,false],[21.222,21.468,4,0.324,-3.327,0.1166,true],[24.8,25.036,4,0.344,-2.403,0.1232,false],[28.153,28.428,4,0.437,-2.969,-0.0615,false],[31.908,32.088,4,0.642,-2.505,-0.0993,false],[35.314,35.537,4,-0.169,-3.002,-0.2071,true],[38.664,38.936,1,-0.03,-3.151,null,null],[42.173,42.428,1,-0.877,-2.552,null,null],[45.941,46.067,4,-0.359,-2.557,0.1277,true],[49.317,49.566,4,-0.736,-2.919,0.1352,false],[52.844,53.035,1,-0.473,-2.577,null,null],[56.32,56.49,4,0.679,-2.884,0.1026,false],[59.932,60.097,4,-0.26,-2.82,-0.0877,false],[63.421,63.563,4,-0.55,-2.695,0.1615,true],[66.903,67.069,4,0.579,-3.1,-0.0993,false],[70.158,70.429,4,-0.612,-2.904,0.0121,true],[73.721,74.0,1,-0.275,-3.369,null,null],[77.362,77.586,4,0.641,-3.249,0.0592,true],[80.924,81.066,4,0.452,-2.907,0.0907,true]]'::jsonb),
        ('sim-beginner-10','beginner','test','[[0.331,0.567,4,0.061,-3.278,0.0312,true],[3.791,3.984,4,-0.856,-2.404,0.1795,false],[7.244,7.403,4,-0.784,-2.928,-0.0205,false],[10.757,11.033,1,-0.951,-2.821,null,null],[14.292,14.501,4,-0.241,-2.883,-0.0108,false],[17.973,18.096,1,0.635,-3.021,null,null],[21.304,21.547,4,0.517,-3.034,0.2355,false],[24.741,24.935,4,0.43,-2.705,0.1104,false],[28.22,28.413,4,-0.454,-3.352,0.0735,false],[31.796,32.003,4,-0.543,-2.571,0.1006,true],[35.212,35.471,4,-0.37,-3.263,-0.1453,false],[38.803,39.079,4,-0.008,-2.853,0.2847,false],[42.268,42.504,4,-0.926,-2.737,0.1402,true],[45.747,45.916,4,0.226,-2.931,-0.1033,false],[49.211,49.47,1,-0.324,-2.302,null,null],[52.683,52.955,4,-0.935,-2.699,-0.11,false],[56.374,56.591,1,0.619,-3.362,null,null],[59.734,59.913,4,-0.258,-2.776,0.2576,false],[63.356,63.536,4,0.579,-2.769,-0.0833,false],[66.712,66.95,1,-0.002,-2.974,null,null],[70.35,70.512,1,-0.118,-2.304,null,null],[73.758,73.957,4,-0.83,-2.462,0.0334,false],[77.446,77.58,4,0.4,-2.47,0.0035,true],[80.752,81.008,4,0.466,-2.38,0.1816,false]]'::jsonb),
        ('sim-intermediate-01','intermediate','train','[[0.282,0.424,4,0.639,-3.371,-0.0546,true],[3.766,3.974,4,0.246,-3.305,-0.0764,false],[7.253,7.505,4,-0.938,-2.33,0.3076,true],[10.815,10.99,4,0.832,-2.542,0.0011,false],[14.358,14.487,4,-0.827,-3.232,-0.0051,true],[17.79,17.965,4,0.97,-2.416,0.0638,true],[21.342,21.51,1,0.074,-2.847,null,null],[24.789,24.934,4,0.349,-2.607,-0.0155,true],[28.317,28.569,4,-1.038,-3.35,-0.0305,false],[31.831,32.053,4,-0.004,-2.71,-0.0449,true],[35.342,35.591,4,0.407,-3.073,0.0551,true],[38.78,38.997,4,-0.412,-2.61,0.0431,false],[42.431,42.572,4,-0.285,-2.782,-0.0039,true],[45.784,45.95,1,-0.255,-2.723,null,null],[49.304,49.45,4,-0.38,-2.765,0.1077,true],[52.851,53.053,4,-0.003,-3.061,0.2103,true],[56.417,56.593,4,-0.989,-2.856,0.0577,false],[59.825,60.031,4,0.764,-2.926,0.1209,false],[63.262,63.473,4,-0.672,-2.791,-0.05,true],[66.836,67.001,4,1.076,-2.652,0.1811,true],[70.289,70.559,4,0.561,-3.181,0.1699,true],[73.858,74.0,1,-0.367,-2.879,null,null],[77.289,77.491,4,-0.378,-2.726,0.1512,false],[80.707,80.96,4,0.071,-2.62,0.2269,true]]'::jsonb),
        ('sim-intermediate-02','intermediate','train','[[0.322,0.535,4,0.653,-2.602,-0.0331,false],[3.63,3.905,4,0.622,-2.546,-0.0732,true],[7.215,7.434,4,0.751,-3.237,-0.0235,false],[10.768,10.906,4,-1.058,-3.055,0.0994,true],[14.295,14.555,4,0.519,-3.331,0.1262,true],[17.715,17.941,4,0.056,-3.055,0.1095,true],[21.236,21.471,4,0.315,-2.637,0.1792,true],[24.729,24.938,4,-0.605,-2.33,0.0144,true],[28.365,28.541,4,0.954,-2.525,0.0919,true],[31.743,31.924,4,1.056,-2.5,0.1628,true],[35.283,35.458,4,-0.127,-3.118,-0.0054,true],[38.671,38.94,4,0.432,-3.249,-0.0418,true],[42.312,42.534,4,-0.985,-2.945,-0.0336,false],[45.799,45.92,4,0.111,-2.378,-0.1135,false],[49.221,49.495,4,-0.393,-2.69,0.1295,false],[52.712,52.918,4,0.491,-2.37,0.0725,false],[56.155,56.435,4,-0.227,-2.855,0.0086,false],[59.832,60.092,4,-1.08,-2.775,0.1999,true],[63.399,63.598,4,0.965,-2.464,0.1663,true],[66.793,66.939,1,-0.09,-3.117,null,null],[70.19,70.437,4,0.149,-2.567,0.0993,false],[73.931,74.065,4,0.372,-3.197,0.1645,true],[77.303,77.465,4,-0.582,-2.571,0.0937,false],[80.671,80.902,4,-0.963,-3.27,0.0146,true]]'::jsonb),
        ('sim-intermediate-03','intermediate','train','[[0.409,0.579,4,-0.842,-2.392,0.055,true],[3.882,4.023,4,-0.763,-2.577,-0.0558,true],[7.268,7.494,4,-0.619,-3.128,-0.0425,false],[10.884,11.075,1,0.448,-3.314,null,null],[14.305,14.513,1,0.012,-2.77,null,null],[17.727,17.93,4,-0.844,-3.174,-0.023,true],[21.36,21.562,4,-0.095,-3.336,-0.0109,true],[24.878,25.061,4,0.696,-2.58,-0.0147,true],[28.143,28.413,4,-0.948,-2.373,-0.0054,true],[31.739,31.982,4,1.052,-2.69,-0.2015,true],[35.394,35.574,4,0.402,-2.672,0.1185,true],[38.882,39.031,4,0.082,-2.818,-0.002,true],[42.177,42.405,4,0.098,-2.815,-0.0473,false],[45.786,45.95,4,0.962,-2.602,0.1091,true],[49.43,49.553,4,-0.647,-3.289,0.0943,true],[52.892,53.02,4,0.529,-2.958,0.0106,false],[56.245,56.411,4,0.695,-2.595,0.0577,true],[59.849,60.02,4,-0.436,-3.242,-0.0229,true],[63.151,63.412,4,0.905,-2.711,-0.0394,true],[66.728,66.999,4,0.377,-2.536,-0.0272,true],[70.279,70.475,4,0.769,-3.069,0.0761,false],[73.851,74.061,4,1.029,-2.787,0.0427,true],[77.273,77.529,4,-0.897,-2.603,0.0472,true],[80.738,80.954,4,0.822,-3.193,0.0143,true]]'::jsonb),
        ('sim-intermediate-04','intermediate','train','[[0.323,0.496,4,1.005,-3.383,0.1701,true],[3.941,4.1,4,0.23,-3.175,-0.0808,false],[7.329,7.51,4,0.074,-3.005,-0.0135,true],[10.725,10.92,4,0.747,-2.394,0.0827,true],[14.328,14.483,4,-0.779,-3.113,0.0835,true],[17.749,17.93,4,0.733,-2.851,-0.0463,false],[21.286,21.537,4,1.026,-2.694,-0.0525,false],[24.736,24.934,4,0.917,-2.804,-0.0043,true],[28.157,28.412,1,0.979,-2.665,null,null],[31.798,32.053,4,-0.591,-2.622,0.0819,true],[35.305,35.524,4,-0.037,-2.863,-0.0019,true],[38.805,39.01,1,-0.496,-2.325,null,null],[42.175,42.403,4,0.674,-2.399,0.0194,true],[45.688,45.938,4,-0.512,-2.963,0.014,true],[49.203,49.481,4,-0.603,-2.648,0.056,false],[52.871,53.052,1,0.116,-3.338,null,null],[56.202,56.402,4,-0.145,-2.537,-0.0962,true],[59.867,60.072,1,-1.006,-3.167,null,null],[63.378,63.574,4,-0.998,-3.318,0.1364,true],[66.737,66.907,4,1.015,-2.754,-0.0795,false],[70.411,70.543,4,-0.743,-3.135,0.0307,false],[73.824,73.966,4,1.075,-2.603,-0.0712,true],[77.405,77.595,1,0.745,-3.025,null,null],[80.871,81.054,4,0.602,-3.367,-0.0413,true]]'::jsonb),
        ('sim-intermediate-05','intermediate','train','[[0.282,0.471,4,0.395,-2.673,0.1764,false],[3.898,4.043,1,0.466,-2.703,null,null],[7.411,7.548,4,-1.089,-3.061,0.1404,true],[10.804,10.954,4,-0.113,-2.79,0.0725,true],[14.247,14.419,4,-0.252,-3.079,0.0964,true],[17.652,17.917,4,1.052,-2.771,0.1225,true],[21.33,21.46,4,-0.144,-2.937,0.1512,true],[24.756,24.915,4,0.275,-2.747,0.0989,true],[28.371,28.59,4,0.669,-3.038,0.1206,true],[31.787,32.063,4,-0.801,-3.047,0.1345,true],[35.426,35.593,4,0.429,-2.86,0.0706,true],[38.697,38.948,4,-0.236,-3.275,-0.0055,true],[42.264,42.536,4,-0.084,-2.621,0.0477,true],[45.707,45.958,4,0.65,-2.95,-0.0193,true],[49.286,49.532,4,-0.937,-2.31,0.0874,true],[52.713,52.98,4,0.422,-2.802,0.1049,false],[56.373,56.507,4,0.592,-2.677,0.0185,true],[59.752,60.029,1,0.39,-2.96,null,null],[63.362,63.551,4,-1.077,-3.115,-0.0035,true],[66.832,67.015,4,0.599,-2.753,0.0809,true],[70.332,70.469,1,-0.185,-2.342,null,null],[73.78,73.923,4,-0.414,-2.899,0.11,true],[77.317,77.488,4,-0.439,-2.509,0.17,true],[80.842,81.07,4,-0.738,-2.318,0.0887,true]]'::jsonb),
        ('sim-intermediate-06','intermediate','train','[[0.355,0.512,4,-0.209,-3.197,-0.0133,true],[3.808,4.023,4,-0.245,-2.625,-0.0243,true],[7.42,7.55,4,-0.876,-2.441,0.0631,true],[10.748,10.952,4,0.306,-2.743,0.0988,true],[14.262,14.517,4,0.258,-2.505,0.0202,true],[17.778,17.917,4,-0.428,-3.199,-0.0233,false],[21.36,21.502,4,-0.256,-3.196,-0.0386,true],[24.902,25.1,4,-0.791,-3.054,-0.0494,true],[28.289,28.411,4,-0.8,-2.503,0.1241,true],[31.783,32.037,4,-0.025,-3.309,0.0173,true],[35.388,35.552,4,0.083,-3.215,-0.0818,false],[38.872,39.01,1,0.605,-2.494,null,null],[42.346,42.473,4,0.482,-2.799,0.0701,false],[45.68,45.92,4,-0.445,-2.301,-0.0286,false],[49.244,49.488,4,-0.579,-2.508,0.0589,true],[52.749,52.97,4,-0.735,-3.247,0.0511,true],[56.264,56.47,4,-0.388,-2.626,0.0866,true],[59.675,59.953,4,0.394,-3.295,0.1654,false],[63.458,63.598,4,0.805,-3.125,0.0069,false],[66.838,67.066,4,-0.023,-2.765,0.0283,true],[70.392,70.527,4,0.034,-3.094,0.1144,false],[73.802,73.974,4,-1.095,-2.548,0.0242,true],[77.403,77.533,4,0.076,-3.16,-0.0834,true],[80.689,80.942,4,-0.357,-2.764,-0.0417,true]]'::jsonb),
        ('sim-intermediate-07','intermediate','calibration','[[0.401,0.537,1,0.625,-2.894,null,null],[3.734,3.925,4,-1.099,-2.346,0.0495,true],[7.261,7.53,4,-0.497,-2.68,0.1252,true],[10.828,10.974,4,-0.228,-3.064,-0.0014,false],[14.345,14.541,1,-0.808,-3.151,null,null],[17.805,18.036,1,0.698,-2.313,null,null],[21.353,21.484,4,-0.257,-2.596,-0.0363,true],[24.821,24.963,4,0.602,-2.572,0.1739,true],[28.282,28.506,1,-0.132,-2.605,null,null],[31.84,32.026,4,0.41,-2.454,0.1909,true],[35.206,35.42,4,-0.255,-2.34,-0.0005,true],[38.701,38.917,4,0.237,-2.543,0.0535,false],[42.402,42.57,4,0.039,-2.84,0.0148,true],[45.881,46.08,4,-0.852,-2.85,-0.0471,true],[49.228,49.506,4,0.955,-3.255,0.078,true],[52.764,53.037,4,0.595,-3.382,-0.0589,true],[56.322,56.452,1,0.636,-2.843,null,null],[59.84,60.026,4,0.444,-3.309,-0.0854,true],[63.309,63.462,4,0.678,-2.81,-0.0079,true],[66.798,67.027,4,-0.955,-2.631,-0.0025,true],[70.224,70.417,4,0.239,-3.06,-0.1283,false],[73.815,74.048,1,0.443,-3.22,null,null],[77.346,77.591,4,0.486,-3.216,0.107,true],[80.952,81.077,4,0.676,-3.101,0.1093,true]]'::jsonb),
        ('sim-intermediate-08','intermediate','calibration','[[0.237,0.415,4,-0.001,-2.776,-0.0767,true],[3.739,3.951,1,0.49,-3.149,null,null],[7.244,7.502,1,-0.562,-2.881,null,null],[10.708,10.977,4,0.786,-2.792,0.1245,true],[14.229,14.464,4,-0.503,-3.314,-0.0446,true],[17.851,18.0,4,0.67,-2.32,0.046,true],[21.284,21.456,4,0.117,-3.391,0.0108,true],[24.793,25.013,4,-0.079,-2.749,-0.1101,true],[28.421,28.58,4,-1.086,-2.949,0.0242,true],[31.709,31.969,4,0.992,-3.398,-0.0614,false],[35.263,35.421,4,-0.018,-3.334,-0.1029,false],[38.775,39.042,1,0.874,-2.828,null,null],[42.264,42.54,4,-0.913,-3.295,0.1117,true],[45.74,46.014,4,-0.579,-3.113,0.1222,true],[49.322,49.56,4,-0.4,-3.101,0.0639,false],[52.871,53.017,4,-0.075,-2.953,0.0417,true],[56.424,56.593,4,-0.517,-3.268,0.0815,false],[59.785,60.039,1,-0.379,-3.3,null,null],[63.248,63.45,4,0.39,-3.114,0.1143,false],[66.714,66.906,4,0.546,-3.125,-0.1127,true],[70.125,70.402,4,-0.812,-2.494,0.0508,true],[73.813,74.026,4,-0.531,-2.506,0.046,false],[77.224,77.489,1,0.725,-3.035,null,null],[80.762,80.909,4,0.163,-2.496,0.0226,true]]'::jsonb),
        ('sim-intermediate-09','intermediate','test','[[0.285,0.435,4,-0.484,-2.428,0.0758,true],[3.857,4.024,4,-0.194,-2.794,0.1112,true],[7.236,7.44,4,0.288,-2.518,0.0775,false],[10.921,11.098,4,0.098,-2.867,0.1304,true],[14.281,14.436,4,0.871,-2.544,0.0086,true],[17.855,18.098,4,1.099,-2.329,0.1435,true],[21.434,21.563,4,1.092,-3.159,0.1194,false],[24.805,25.059,4,0.759,-3.206,-0.0587,false],[28.351,28.583,1,0.985,-2.78,null,null],[31.735,32.013,4,0.84,-2.859,-0.0198,true],[35.341,35.498,1,-0.619,-2.821,null,null],[38.748,38.9,4,-0.813,-2.611,0.181,true],[42.137,42.404,4,0.604,-2.469,-0.0158,false],[45.821,46.092,4,-0.23,-3.289,0.0418,true],[49.316,49.59,4,0.328,-3.208,0.0933,false],[52.793,53.039,4,-0.609,-2.752,0.0838,false],[56.153,56.425,4,0.987,-2.969,0.1121,false],[59.877,60.093,1,0.926,-2.336,null,null],[63.174,63.444,4,-0.791,-2.58,0.0709,true],[66.844,67.077,1,0.306,-2.425,null,null],[70.329,70.489,4,-0.951,-3.118,0.2674,true],[73.663,73.9,4,1.032,-2.427,-0.0183,true],[77.162,77.42,4,0.332,-2.644,0.058,true],[80.748,80.915,4,-0.399,-2.353,0.0957,true]]'::jsonb),
        ('sim-intermediate-10','intermediate','test','[[0.282,0.546,4,-0.895,-2.747,-0.0346,true],[3.826,4.088,4,-0.754,-3.289,-0.0259,true],[7.202,7.438,4,0.506,-3.108,0.0196,true],[10.869,11.014,4,-0.306,-2.93,0.0846,true],[14.38,14.532,4,-1.043,-3.212,0.0224,false],[17.764,17.962,4,0.354,-3.3,0.0604,true],[21.258,21.437,4,-0.119,-3.11,-0.0219,true],[24.685,24.912,4,-0.911,-3.022,0.0422,true],[28.385,28.594,4,0.75,-2.5,-0.0505,false],[31.799,31.995,4,-0.926,-2.407,0.0759,false],[35.252,35.5,4,0.391,-2.718,0.1223,true],[38.848,39.097,4,-0.822,-2.934,0.117,false],[42.267,42.487,4,0.735,-3.116,0.1713,true],[45.806,45.978,4,-0.492,-2.896,-0.0579,false],[49.353,49.557,4,-0.177,-2.944,0.0901,false],[52.878,53.088,4,-0.878,-3.085,0.1106,true],[56.288,56.469,4,-0.032,-2.73,0.089,true],[59.896,60.022,4,0.857,-3.035,0.0445,true],[63.285,63.549,4,-1.055,-2.501,0.0106,true],[66.78,67.039,1,-0.804,-2.328,null,null],[70.326,70.489,4,-0.183,-2.69,0.088,true],[73.703,73.942,4,0.571,-2.446,0.0694,true],[77.32,77.462,4,0.773,-2.433,-0.0509,true],[80.685,80.939,4,-0.551,-2.898,0.1324,true]]'::jsonb),
        ('sim-advanced-01','advanced','train','[[0.343,0.565,4,-0.827,-3.366,-0.0431,true],[3.898,4.074,4,-0.74,-2.416,0.0234,true],[7.356,7.538,4,-0.742,-2.771,0.057,true],[10.85,11.036,4,-1.056,-2.542,-0.0255,true],[14.178,14.402,4,0.223,-3.391,-0.0113,true],[17.905,18.093,4,-0.272,-3.016,0.0529,true],[21.241,21.493,4,-0.226,-3.287,-0.0618,true],[24.758,24.975,4,-0.443,-3.281,-0.0141,true],[28.34,28.598,4,-0.525,-2.618,0.0126,true],[31.804,32.053,4,0.665,-2.74,-0.0288,true],[35.311,35.536,4,1.094,-3.115,-0.0406,true],[38.892,39.042,4,0.498,-3.155,0.0066,true],[42.33,42.557,4,0.016,-2.47,0.0282,true],[45.759,45.92,4,0.678,-2.561,-0.0707,true],[49.402,49.536,4,-0.318,-2.581,-0.032,true],[52.696,52.952,4,-0.065,-2.447,0.0473,true],[56.327,56.589,4,0.001,-2.446,-0.0321,true],[59.919,60.071,4,-0.2,-2.774,0.0096,false],[63.257,63.492,4,0.614,-2.864,-0.0252,true],[66.659,66.901,4,-0.729,-2.559,-0.0264,true],[70.335,70.553,4,0.294,-2.956,0.0232,true],[73.818,73.94,4,-0.391,-2.483,-0.0178,true],[77.375,77.535,4,0.426,-3.021,0.0134,true],[80.765,80.933,4,-0.46,-2.599,0.0146,true]]'::jsonb),
        ('sim-advanced-02','advanced','train','[[0.335,0.599,4,-0.31,-3.193,0.0454,false],[3.927,4.084,4,0.5,-3.256,-0.0216,true],[7.347,7.473,4,0.828,-3.119,-0.0634,true],[10.684,10.91,4,0.338,-3.378,-0.0304,true],[14.364,14.509,4,0.429,-2.707,-0.0507,true],[17.869,18.032,4,0.232,-3.249,-0.0089,true],[21.287,21.424,4,-0.663,-3.18,-0.0313,true],[24.772,25.005,4,-0.45,-3.357,-0.0408,false],[28.239,28.466,1,0.895,-2.481,null,null],[31.9,32.034,4,0.026,-2.604,-0.0164,true],[35.173,35.451,4,-0.449,-2.889,0.0006,true],[38.788,38.958,4,0.525,-3.296,-0.0042,true],[42.183,42.409,4,-0.725,-3.007,-0.0621,true],[45.77,45.919,4,-0.218,-2.341,0.0017,true],[49.229,49.477,4,0.327,-2.523,0.024,true],[52.901,53.088,4,0.2,-2.518,0.0183,true],[56.259,56.436,4,-1.037,-2.647,0.0328,true],[59.898,60.084,4,-0.999,-3.112,-0.097,true],[63.298,63.541,4,0.169,-2.778,0.0347,true],[66.87,67.005,4,0.356,-3.127,-0.0242,true],[70.281,70.535,4,0.128,-2.313,0.0304,true],[73.811,74.07,4,-0.935,-2.859,-0.0,true],[77.438,77.594,4,0.315,-2.956,-0.0133,true],[80.798,80.99,4,-0.839,-2.853,0.0217,true]]'::jsonb),
        ('sim-advanced-03','advanced','train','[[0.376,0.511,1,0.229,-2.719,null,null],[3.808,3.961,4,0.377,-2.355,0.0432,true],[7.218,7.411,4,0.132,-2.718,-0.029,true],[10.768,10.923,4,-0.349,-2.487,0.0301,false],[14.226,14.458,4,-0.999,-3.219,0.0568,true],[17.771,17.921,4,0.946,-2.968,0.0644,true],[21.419,21.556,4,-0.188,-2.381,0.0459,true],[24.717,24.99,4,-0.804,-2.851,-0.0183,true],[28.156,28.41,4,-0.038,-2.841,0.0407,true],[31.864,32.046,4,0.221,-2.429,0.0111,true],[35.21,35.477,4,0.577,-3.099,0.112,true],[38.695,38.927,4,0.547,-3.071,0.0004,true],[42.302,42.572,4,0.292,-3.179,0.0009,true],[45.81,46.034,4,1.008,-2.846,0.0011,true],[49.288,49.464,4,-0.043,-2.772,0.0034,true],[52.812,53.068,4,0.678,-2.825,-0.0101,true],[56.277,56.429,4,0.55,-3.223,0.0154,true],[59.797,60.038,4,-1.029,-2.505,0.0474,true],[63.316,63.574,4,0.533,-2.783,0.0263,true],[66.69,66.958,4,-0.247,-2.348,0.0826,false],[70.341,70.463,4,-0.547,-2.717,0.0351,true],[73.858,74.082,4,0.561,-2.799,-0.0479,true],[77.388,77.555,4,-0.71,-2.649,0.0298,true],[80.766,80.975,4,-0.814,-2.758,0.0414,true]]'::jsonb),
        ('sim-advanced-04','advanced','train','[[0.259,0.431,4,-0.988,-2.374,0.0379,true],[3.82,4.032,4,-0.312,-2.538,-0.0205,true],[7.227,7.414,4,0.081,-3.299,0.0122,true],[10.765,10.943,4,-0.619,-2.572,-0.1262,true],[14.369,14.596,1,-0.12,-2.405,null,null],[17.75,18.023,4,0.402,-3.047,-0.0327,false],[21.383,21.589,4,-0.478,-2.398,0.0046,true],[24.725,24.989,4,-0.676,-2.835,-0.0408,false],[28.227,28.433,4,-1.084,-3.328,-0.0392,true],[31.779,31.985,4,0.446,-2.326,-0.0632,true],[35.289,35.529,4,-0.762,-2.895,-0.0176,true],[38.847,39.059,4,-0.599,-3.115,0.0028,true],[42.403,42.526,4,0.381,-2.812,-0.0409,true],[45.885,46.017,4,0.942,-3.283,-0.048,true],[49.218,49.46,4,-0.137,-2.965,-0.0314,true],[52.679,52.927,4,0.101,-2.344,-0.0438,true],[56.288,56.535,4,0.505,-2.99,0.032,true],[59.71,59.925,1,0.082,-2.358,null,null],[63.196,63.461,4,-0.344,-2.946,-0.0241,true],[66.848,67.003,4,0.736,-3.186,0.016,true],[70.291,70.57,4,-0.085,-2.795,0.0468,true],[73.677,73.913,4,-0.028,-3.036,0.0251,true],[77.353,77.524,4,-0.109,-2.722,0.0266,true],[80.839,81.017,4,-0.404,-2.929,0.016,true]]'::jsonb),
        ('sim-advanced-05','advanced','train','[[0.294,0.548,4,-0.805,-2.594,0.0335,true],[3.813,3.961,4,0.235,-2.474,-0.0216,true],[7.266,7.428,4,0.85,-3.316,0.0115,true],[10.721,10.906,4,0.198,-2.403,0.0086,true],[14.313,14.509,4,0.605,-2.998,0.0155,true],[17.756,17.958,4,-0.227,-2.848,0.0648,true],[21.352,21.57,4,-0.735,-2.834,-0.0153,false],[24.794,25.032,4,-0.036,-3.005,0.0127,true],[28.319,28.498,4,-1.006,-3.173,0.0332,true],[31.714,31.991,4,-0.387,-2.626,-0.0127,true],[35.404,35.566,4,0.098,-3.209,-0.0135,true],[38.887,39.067,4,0.896,-2.832,-0.022,true],[42.377,42.574,4,0.229,-2.848,0.019,true],[45.879,46.029,4,-0.303,-2.611,0.0216,true],[49.211,49.446,4,-0.041,-2.873,0.0105,true],[52.692,52.904,4,-0.746,-2.708,0.0256,true],[56.353,56.514,4,-0.704,-2.346,-0.0311,true],[59.879,60.044,4,-0.911,-2.906,-0.0209,false],[63.32,63.573,4,0.222,-2.526,0.015,true],[66.778,66.953,4,-0.102,-2.75,-0.0146,true],[70.325,70.477,4,0.791,-2.845,-0.012,true],[73.869,73.995,4,0.625,-2.846,0.0151,true],[77.279,77.496,4,0.721,-2.413,0.0133,true],[80.769,80.974,4,-0.595,-2.517,-0.0446,true]]'::jsonb),
        ('sim-advanced-06','advanced','train','[[0.317,0.548,4,-0.885,-3.253,0.0171,true],[3.782,3.947,4,0.829,-2.95,0.006,true],[7.395,7.516,4,0.34,-2.643,0.0364,true],[10.748,10.972,4,-0.542,-2.46,-0.0442,true],[14.35,14.536,4,0.624,-2.869,0.0008,true],[17.842,18.011,4,-0.342,-2.624,-0.0207,true],[21.318,21.55,4,-0.744,-3.164,-0.0831,true],[24.736,24.968,4,0.455,-3.223,-0.0111,true],[28.239,28.429,4,-0.959,-3.156,-0.0154,true],[31.63,31.908,4,0.251,-2.827,-0.0259,false],[35.374,35.564,4,-0.656,-2.677,-0.0604,true],[38.741,38.957,1,0.035,-3.145,null,null],[42.276,42.434,1,-1.099,-3.227,null,null],[45.922,46.099,4,-0.231,-2.755,-0.0714,true],[49.165,49.418,4,0.913,-3.017,-0.0209,false],[52.804,52.955,4,0.945,-2.768,-0.0171,true],[56.142,56.414,4,0.666,-2.348,0.0211,true],[59.832,60.063,4,1.025,-2.781,-0.0072,false],[63.278,63.492,4,-1.04,-3.272,-0.073,true],[66.799,67.027,4,-0.594,-3.053,0.026,true],[70.431,70.566,4,0.382,-2.497,-0.0262,true],[73.909,74.086,4,0.867,-3.104,0.0356,false],[77.262,77.5,4,0.82,-3.173,-0.0319,true],[80.805,80.95,4,-0.865,-2.388,-0.0378,true]]'::jsonb),
        ('sim-advanced-07','advanced','calibration','[[0.426,0.553,4,-0.039,-2.717,-0.123,false],[3.796,4.074,4,0.915,-3.092,-0.0165,true],[7.267,7.515,4,-0.048,-2.805,-0.0564,true],[10.703,10.935,4,-0.088,-2.642,-0.0219,true],[14.27,14.442,4,0.248,-3.114,-0.0459,true],[17.823,18.023,4,-0.637,-2.629,-0.0291,true],[21.422,21.571,4,-0.811,-2.767,-0.0192,true],[24.766,24.947,4,-0.779,-2.345,-0.0055,true],[28.361,28.561,4,1.09,-2.466,-0.0769,true],[31.749,31.96,4,-0.224,-2.64,-0.0273,true],[35.342,35.563,4,-0.11,-3.032,-0.0502,false],[38.813,38.952,1,0.808,-2.341,null,null],[42.216,42.44,4,-0.716,-2.542,0.013,true],[45.797,46.035,4,0.857,-2.981,-0.0326,true],[49.26,49.434,4,0.289,-2.771,0.0268,true],[52.757,52.914,4,-0.403,-3.08,-0.0269,true],[56.311,56.489,4,0.191,-2.338,0.0287,true],[59.701,59.937,1,0.356,-2.366,null,null],[63.393,63.54,4,-0.873,-3.33,0.0106,true],[66.846,66.979,4,-0.523,-3.229,-0.0518,true],[70.417,70.577,4,-0.475,-2.711,0.024,true],[73.861,74.033,4,-0.052,-2.328,0.0277,true],[77.179,77.454,4,0.435,-3.205,-0.0618,true],[80.791,81.013,4,-0.419,-3.018,-0.0498,true]]'::jsonb),
        ('sim-advanced-08','advanced','calibration','[[0.304,0.473,4,-1.1,-2.503,-0.0233,true],[3.848,3.969,4,0.929,-2.358,0.0076,true],[7.338,7.459,4,-0.278,-2.947,0.013,true],[10.741,10.979,4,-0.243,-2.984,0.0093,true],[14.304,14.553,4,0.406,-3.087,0.0488,true],[17.805,18.062,4,-0.699,-3.081,-0.0194,true],[21.402,21.525,4,-0.991,-2.812,-0.0245,true],[24.686,24.92,4,0.5,-3.144,0.0361,true],[28.182,28.462,4,-0.08,-2.529,0.0454,true],[31.94,32.069,4,0.606,-3.321,-0.0102,true],[35.423,35.563,4,0.59,-3.126,0.0165,true],[38.864,38.989,4,-0.086,-2.552,-0.0861,true],[42.364,42.536,4,0.284,-3.354,-0.0009,false],[45.782,46.004,4,-0.663,-2.424,0.0283,true],[49.38,49.527,4,-0.493,-3.268,-0.0063,true],[52.657,52.902,4,-0.054,-2.744,0.0018,true],[56.149,56.418,4,1.093,-2.712,0.0232,true],[59.782,60.029,4,-0.823,-3.144,0.0736,true],[63.41,63.561,4,0.416,-3.006,0.0052,true],[66.89,67.08,4,-0.452,-2.633,0.0206,true],[70.293,70.546,4,0.912,-2.79,0.0332,true],[73.769,73.931,4,-0.292,-3.399,-0.0098,true],[77.16,77.404,4,-0.575,-2.607,-0.0138,true],[80.804,81.008,4,-0.005,-2.793,-0.0111,true]]'::jsonb),
        ('sim-advanced-09','advanced','test','[[0.291,0.422,4,0.737,-3.257,0.01,true],[3.666,3.92,4,0.347,-2.822,-0.0233,true],[7.234,7.451,4,-0.944,-2.377,0.0039,false],[10.786,10.916,4,0.128,-2.368,0.0328,true],[14.222,14.474,4,-0.515,-3.355,0.0204,false],[17.856,18.092,4,0.07,-3.192,0.0062,true],[21.301,21.45,4,0.548,-2.943,0.0328,true],[24.731,24.992,4,-0.733,-3.202,0.0242,true],[28.188,28.468,4,-0.125,-3.012,-0.0495,true],[31.785,31.974,4,0.497,-2.731,0.0926,true],[35.175,35.443,4,-0.541,-2.342,-0.0398,true],[38.894,39.097,4,1.086,-3.068,0.0049,true],[42.321,42.444,4,0.708,-2.641,-0.0137,true],[45.959,46.085,4,-0.318,-3.248,0.0374,true],[49.266,49.516,4,-0.898,-2.96,0.0431,true],[52.807,53.059,4,0.417,-2.766,-0.0346,false],[56.282,56.519,4,0.168,-3.314,0.0499,true],[59.804,60.063,4,0.814,-3.151,0.0142,true],[63.276,63.52,1,-0.259,-3.065,null,null],[66.764,66.908,4,0.005,-3.157,0.0474,true],[70.288,70.489,4,-0.611,-3.081,0.0112,true],[73.715,73.926,4,-0.992,-2.961,0.029,true],[77.217,77.426,4,-0.027,-2.683,0.0145,true],[80.864,81.023,4,-0.942,-2.546,-0.0419,true]]'::jsonb),
        ('sim-advanced-10','advanced','test','[[0.358,0.534,4,-0.394,-2.747,0.0752,false],[3.832,4.023,4,-0.779,-3.351,0.0502,true],[7.199,7.44,4,0.776,-3.276,0.1261,false],[10.952,11.074,4,0.426,-3.278,0.0744,true],[14.199,14.405,4,-0.652,-2.824,0.0435,true],[17.805,17.928,4,0.183,-2.67,0.0858,true],[21.188,21.434,4,0.26,-3.342,0.0431,true],[24.728,24.954,4,0.801,-3.379,0.0419,true],[28.349,28.53,4,1.049,-3.29,0.0176,true],[31.744,31.937,4,0.938,-3.376,0.1101,true],[35.323,35.506,4,-0.755,-3.019,0.0164,true],[38.846,39.096,4,0.277,-2.433,0.085,false],[42.354,42.563,1,-0.484,-3.233,null,null],[45.945,46.079,4,-0.259,-2.343,0.0084,true],[49.173,49.448,4,0.901,-3.038,0.0138,true],[52.734,52.941,4,0.66,-2.451,0.0259,true],[56.151,56.427,4,-0.781,-2.374,0.0967,false],[59.691,59.963,4,-0.84,-3.051,0.0867,true],[63.39,63.576,4,-0.125,-2.373,0.007,false],[66.842,67.087,4,-0.865,-3.197,0.0494,true],[70.356,70.513,4,0.412,-3.005,-0.0023,true],[73.778,73.995,4,0.466,-2.989,0.07,true],[77.197,77.439,1,-1.067,-3.103,null,null],[80.705,80.983,4,0.567,-3.327,0.0131,false]]'::jsonb)
        )
        INSERT INTO CoachingDemoSessions(SessionId,PlayerCode,Tier,Split,GameVersion,Shots)
        SELECT player_code || '-training', player_code, tier, split, 'synthetic-demo-v1',
            (SELECT jsonb_agg(jsonb_strip_nulls(jsonb_build_object(
                'shotId', player_code || '-volley-' || lpad(ordinal::text,2,'0'),
                'sequence', ordinal - 1, 'rally', ordinal,
                'kind', 'Volley', 'technique', 'Volley', 'source', 'simulated-training',
                'time', fields->1, 'rawPressedAt', fields->0, 'rawReleasedAt', fields->1,
                'rawInputMask', fields->2, 'rawPosition', jsonb_build_array(fields->3,0.0,fields->4),
                'rawTimingError', fields->5, 'recovered', fields->6,
                'rawTechnique', CASE WHEN fields->>2 = '1' THEN 'Drive' ELSE 'Volley' END,
                'outcome', CASE WHEN fields->>2 = '1' THEN 'WrongInput' ELSE 'Contact' END,
                'assisted', true, 'engineAimed', true
            )) ORDER BY ordinal) FROM jsonb_array_elements(trials) WITH ORDINALITY AS t(fields, ordinal))
        FROM fixtures
        ON CONFLICT (SessionId) DO NOTHING;
    END IF;
END;
$coaching_demo$;
--#endregion Coaching demo

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

CREATE TABLE ContentCatalogs (
    Version VARCHAR(64) PRIMARY KEY,
    Data JSONB NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL
);
CREATE TABLE ProgressRewardGrants (
    PlayerId UUID NOT NULL REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    RewardKey VARCHAR(100) NOT NULL,
    Details JSONB NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    PRIMARY KEY(PlayerId,RewardKey)
);
CREATE TABLE DedicatedOperations (
    OperationId UUID PRIMARY KEY,
    MatchId UUID NOT NULL UNIQUE REFERENCES GameMatches(MatchId),
    LeaseId VARCHAR(64) NOT NULL,
    ActorId UUID NOT NULL,
    Reason VARCHAR(1000) NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    AcknowledgedAt TIMESTAMPTZ
);

CREATE TABLE AdminAudits (
    AuditId UUID PRIMARY KEY,
    ActorId UUID NOT NULL,
    TargetId UUID,
    Action VARCHAR(80) NOT NULL,
    Details JSONB NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL
);
CREATE INDEX AdminAudits_Target_Time_Idx ON AdminAudits(TargetId,CreatedAt DESC);

COMMIT;

-- Version 1 preserves the existing Unity gameplay tolerances. These are not expert-calibrated AI thresholds.
INSERT INTO TechniqueReferences(ReferenceId,TechniqueId,Version,Data,CreatedAt)
SELECT md5('pickleverse.reference.' || TechniqueName)::uuid,TechniqueId,1,
    '{"contactWindowSeconds":0.17,"earlyBufferSeconds":0.23,"recoveryRadius":0.6,"recoveryTimeoutSeconds":4}'::jsonb,CURRENT_TIMESTAMP
FROM Techniques;
UPDATE Lessons l SET Config=l.Config || jsonb_build_object('referenceId',r.ReferenceId::text,'reference',r.Data)
FROM TechniqueReferences r JOIN Techniques t ON t.TechniqueId=r.TechniqueId
WHERE l.Config->>'skill'=t.TechniqueName AND r.Version=1;
