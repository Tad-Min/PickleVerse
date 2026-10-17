-- Kích hoạt extension pgcrypto để sinh UUID ngẫu nhiên (PostgreSQL)
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
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
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

-- ============================================================================
-- 2. MATCH & HISTORY
-- ============================================================================
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

CREATE TABLE MatchHistoryReplay (
    MatchId UUID PRIMARY KEY,
    ReplayData TEXT NOT NULL,
    CONSTRAINT FK_MatchHistoryReplay_MatchHistory FOREIGN KEY (MatchId) REFERENCES MatchHistory(MatchId) ON DELETE CASCADE
);

-- ============================================================================
-- 3. ECONOMY & COSMETICS
-- ============================================================================
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

-- ============================================================================
-- 4. ACHIEVEMENTS & MISSIONS
-- ============================================================================
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

-- ============================================================================
-- 5. TRAINING SYSTEM (LESSONS & DRILLS)
-- ============================================================================
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
    DrillName VARCHAR(254) NOT NULL,
    Description VARCHAR(254) NOT NULL
);

CREATE TABLE LessonDrills (
    LessonId INT NOT NULL,
    DrillId INT NOT NULL,
    [Target] INT NOT NULL,
    PRIMARY KEY (LessonId, DrillId),
    CONSTRAINT FK_LessonDrills_Lessons FOREIGN KEY (LessonId) REFERENCES Lessons(LessonId) ON DELETE CASCADE,
    CONSTRAINT FK_LessonDrills_Drills FOREIGN KEY (DrillId) REFERENCES Drills(DrillId) ON DELETE CASCADE
);

CREATE TABLE PlayerLessonDrills (
    PlayerId UUID NOT NULL,
    LessonId INT NOT NULL,
    DrillId INT NOT NULL,
    Score INT NOT NULL DEFAULT 0 CHECK (Score >= 0), 
    IsClaimed BOOLEAN NOT NULL DEFAULT FALSE,
    ClaimedAt TIMESTAMPTZ NULL,
    PRIMARY KEY (PlayerId, DrillId),
    CONSTRAINT FK_PlayerLessons_PlayerProfiles FOREIGN KEY (PlayerId) REFERENCES PlayerProfiles(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_PlayerLessons_Drills FOREIGN KEY (DrillId) REFERENCES Drills(DrillId)
);