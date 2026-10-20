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
