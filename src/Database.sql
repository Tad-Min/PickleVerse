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
