-- ============================================================================
-- PickleVerse - PostgreSQL database schema
-- Ngay thiet ke: 2026-10-14. Tuong thich PostgreSQL 16+.
-- Nguon: PickleVerse_Core_Features.docx, PickleVerse_Capstone_Proposal.md,
--        Assets/Scripts/Entites va cac catalog Training/Progression/Shop hien tai.
--
-- Chay tren DATABASE RONG da tao san, bang pgAdmin Query Tool hoac:
--   psql -X --set ON_ERROR_STOP=1 -d pickleverse -f script/Database.sql
-- Script tao schema pickleverse trong mot transaction; khong DROP du lieu.
-- Day la baseline mot lan. Lan chay sau phai dung migration, khong sua baseline.
-- UUID trong DB; Unity local ID/Authentication ID duoc luu o player_identities.
-- TIMESTAMPTZ cho thoi diem; ngay/tuần nhiem vu dung UTC de khop game hien tai.
-- Ngoai le thanh cong/loi duoc API xu ly; Unity khong ket noi DB truc tiep.
--
-- Cac bang noi N-N chinh:
-- user_roles, lesson_drills, drill_techniques, drill_prerequisites,
-- definition_currency_rewards, period_definitions, player_definition_progress,
-- player_inventory, item_prices, friendships, player_blocks, room_members,
-- match_participants, training_attempts, player_technique_mastery,
-- error_tips, shot_errors, feedback_errors, feedback_tips, recommendation_errors.
-- JSONB chi luu cau hinh/du lieu bien thien; khong thay the FK hay bang noi.
-- ============================================================================

BEGIN;
SET LOCAL TIME ZONE 'UTC';
CREATE SCHEMA IF NOT EXISTS pickleverse;
SET LOCAL search_path = pickleverse, pg_catalog;

-- 1. TAI KHOAN, VAI TRO, HO SO
CREATE TABLE app_users (
    user_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(32) NOT NULL CHECK (username ~ '^[A-Za-z0-9_]{3,32}$'),
    email VARCHAR(254),
    password_hash TEXT,
    status VARCHAR(16) NOT NULL DEFAULT 'active'
        CHECK (status IN ('active', 'suspended', 'banned', 'deleted')),
    email_verified_at TIMESTAMPTZ,
    last_login_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (email IS NULL OR (email = btrim(email) AND position('@' IN email) > 1)),
    CHECK (password_hash IS NULL OR length(password_hash) >= 40),
    CHECK (email_verified_at IS NULL OR email IS NOT NULL)
);
CREATE UNIQUE INDEX uq_users_username_ci ON app_users (lower(username));
CREATE UNIQUE INDEX uq_users_email_ci ON app_users (lower(email)) WHERE email IS NOT NULL;
CREATE INDEX ix_users_status ON app_users (status);
COMMENT ON COLUMN app_users.password_hash IS 'Chi luu bcrypt/Argon2 hash do backend tao; khong luu mat khau goc.';

CREATE TABLE roles (
    role_code VARCHAR(24) PRIMARY KEY,
    display_name VARCHAR(80) NOT NULL
);
CREATE TABLE user_roles (
    user_id UUID NOT NULL REFERENCES app_users(user_id) ON DELETE CASCADE,
    role_code VARCHAR(24) NOT NULL REFERENCES roles(role_code),
    granted_by UUID REFERENCES app_users(user_id),
    granted_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, role_code)
);
CREATE INDEX ix_user_roles_role ON user_roles (role_code, user_id);

CREATE TABLE player_identities (
    user_id UUID NOT NULL REFERENCES app_users(user_id) ON DELETE CASCADE,
    provider VARCHAR(32) NOT NULL CHECK (length(btrim(provider)) > 0),
    external_id VARCHAR(160) NOT NULL CHECK (length(btrim(external_id)) > 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (provider, external_id),
    UNIQUE (user_id, provider)
);
COMMENT ON TABLE player_identities IS 'Anh xa Unity local profile, Unity Authentication hoac nha cung cap dang nhap sang UUID user.';

CREATE TABLE auth_sessions (
    session_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app_users(user_id) ON DELETE CASCADE,
    refresh_token_hash BYTEA NOT NULL UNIQUE CHECK (octet_length(refresh_token_hash) = 32),
    device_label VARCHAR(120),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    revoked_at TIMESTAMPTZ,
    CHECK (expires_at > created_at),
    CHECK (revoked_at IS NULL OR revoked_at >= created_at)
);
CREATE INDEX ix_auth_sessions_user ON auth_sessions (user_id, expires_at);
COMMENT ON COLUMN auth_sessions.refresh_token_hash IS 'SHA-256 cua refresh token ngau nhien; khong luu access/refresh token goc.';

CREATE TABLE player_profiles (
    user_id UUID PRIMARY KEY REFERENCES app_users(user_id) ON DELETE CASCADE,
    display_name VARCHAR(18) NOT NULL CHECK (length(btrim(display_name)) BETWEEN 1 AND 18),
    avatar_code VARCHAR(80) NOT NULL DEFAULT 'initial',
    character_variant VARCHAR(16) NOT NULL DEFAULT 'male'
        CHECK (character_variant IN ('male', 'female')),
    locale VARCHAR(16) NOT NULL DEFAULT 'vi-VN',
    settings JSONB NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(settings) = 'object'),
    profile_version INTEGER NOT NULL DEFAULT 2 CHECK (profile_version > 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
COMMENT ON COLUMN player_profiles.settings IS 'Volume, graphics, FPS, key binding va touch layout; khong chua quan he entity.';

CREATE TABLE player_progression (
    user_id UUID PRIMARY KEY REFERENCES app_users(user_id),
    total_xp BIGINT NOT NULL DEFAULT 0 CHECK (total_xp >= 0),
    active_seconds NUMERIC(18,3) NOT NULL DEFAULT 0 CHECK (active_seconds >= 0),
    ranked_win_streak INTEGER NOT NULL DEFAULT 0 CHECK (ranked_win_streak >= 0),
    ranked_loss_streak INTEGER NOT NULL DEFAULT 0 CHECK (ranked_loss_streak >= 0),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (ranked_win_streak = 0 OR ranked_loss_streak = 0)
);
COMMENT ON TABLE player_progression IS 'XP tong duoc trigger reward_grants cap nhat; level suy ra trong view de tranh lech total_xp/level/level_xp.';

CREATE TABLE player_rating_states (
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    rating_system VARCHAR(16) NOT NULL CHECK (rating_system IN ('elo', 'glicko2')),
    play_mode VARCHAR(16) NOT NULL CHECK (play_mode IN ('singles', 'doubles')),
    rating NUMERIC(12,3) NOT NULL,
    rating_deviation NUMERIC(10,3),
    volatility NUMERIC(12,8),
    reliability_percent NUMERIC(5,2) NOT NULL DEFAULT 0 CHECK (reliability_percent BETWEEN 0 AND 100),
    rated_matches INTEGER NOT NULL DEFAULT 0 CHECK (rated_matches >= 0),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, rating_system, play_mode),
    CHECK ((rating_system = 'elo' AND rating >= 100 AND rating = trunc(rating)
            AND rating_deviation IS NULL AND volatility IS NULL)
        OR (rating_system = 'glicko2' AND rating > 0 AND rating_deviation > 0
            AND volatility > 0 AND rating_deviation IS NOT NULL AND volatility IS NOT NULL))
);
CREATE INDEX ix_rating_leaderboard ON player_rating_states (rating_system, play_mode, rating DESC, user_id);
COMMENT ON TABLE player_rating_states IS 'Unity dung Elo khoi diem 100; scaffold .NET dung Glicko-2 khoi diem 2000. Hai he khong dung chung diem.';

-- 2. VI, SO CAI, COSMETIC VA CUA HANG
CREATE TABLE currencies (
    currency_code VARCHAR(24) PRIMARY KEY,
    display_name VARCHAR(80) NOT NULL,
    is_premium BOOLEAN NOT NULL DEFAULT false
);
CREATE TABLE reward_grants (
    reward_grant_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    grant_key VARCHAR(180) NOT NULL CHECK (length(btrim(grant_key)) > 0),
    source_kind VARCHAR(32) NOT NULL CHECK (source_kind IN
        ('match', 'drill', 'achievement', 'daily_mission', 'weekly_mission',
         'period_bonus', 'daily_streak', 'admin_adjustment')),
    xp_amount BIGINT NOT NULL DEFAULT 0 CHECK (xp_amount >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id, grant_key),
    UNIQUE (reward_grant_id, user_id)
);
CREATE TABLE player_wallets (
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    currency_code VARCHAR(24) NOT NULL REFERENCES currencies(currency_code),
    balance BIGINT NOT NULL DEFAULT 0 CHECK (balance >= 0),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, currency_code)
);
CREATE TABLE wallet_transactions (
    wallet_transaction_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL,
    currency_code VARCHAR(24) NOT NULL,
    request_key VARCHAR(180) NOT NULL CHECK (length(btrim(request_key)) > 0),
    amount BIGINT NOT NULL CHECK (amount <> 0),
    balance_after BIGINT NOT NULL CHECK (balance_after >= 0),
    reason VARCHAR(120) NOT NULL,
    reward_grant_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    FOREIGN KEY (user_id, currency_code) REFERENCES player_wallets(user_id, currency_code),
    FOREIGN KEY (reward_grant_id, user_id) REFERENCES reward_grants(reward_grant_id, user_id),
    UNIQUE (user_id, request_key),
    UNIQUE (wallet_transaction_id, user_id, currency_code),
    CHECK (reward_grant_id IS NULL OR amount > 0)
);
CREATE UNIQUE INDEX uq_reward_currency_once ON wallet_transactions (reward_grant_id, currency_code)
    WHERE reward_grant_id IS NOT NULL;
CREATE INDEX ix_wallet_history ON wallet_transactions (user_id, created_at DESC);
COMMENT ON TABLE wallet_transactions IS 'So cai bat bien. Trigger khoa row vi va ap dung amount; khong UPDATE balance truc tiep trong API.';

CREATE TABLE item_collections (
    collection_code VARCHAR(40) PRIMARY KEY,
    display_name VARCHAR(100) NOT NULL
);
CREATE TABLE cosmetic_items (
    item_code VARCHAR(80) PRIMARY KEY,
    collection_code VARCHAR(40) NOT NULL REFERENCES item_collections(collection_code),
    display_name VARCHAR(120) NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    equipment_slot VARCHAR(24) NOT NULL CHECK (equipment_slot IN ('paddle', 'avatar', 'outfit')),
    unity_asset_key TEXT,
    thumbnail_key TEXT,
    is_default BOOLEAN NOT NULL DEFAULT false,
    is_active BOOLEAN NOT NULL DEFAULT true,
    release_order INTEGER NOT NULL DEFAULT 0 CHECK (release_order >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (item_code, equipment_slot)
);
CREATE UNIQUE INDEX uq_default_item_per_slot ON cosmetic_items (equipment_slot) WHERE is_default;
CREATE INDEX ix_items_collection ON cosmetic_items (collection_code, release_order DESC);
CREATE TABLE item_prices (
    item_code VARCHAR(80) NOT NULL REFERENCES cosmetic_items(item_code),
    currency_code VARCHAR(24) NOT NULL REFERENCES currencies(currency_code),
    price BIGINT NOT NULL CHECK (price > 0),
    PRIMARY KEY (item_code, currency_code)
);
COMMENT ON TABLE item_prices IS 'N-N vat pham/loai tien. Gia 0 trong Unity nghia la khong cho mua, nen khong tao row gia 0.';
CREATE TABLE shop_purchases (
    purchase_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    request_id VARCHAR(160) NOT NULL CHECK (length(btrim(request_id)) > 0),
    item_code VARCHAR(80) NOT NULL REFERENCES cosmetic_items(item_code),
    currency_code VARCHAR(24) NOT NULL REFERENCES currencies(currency_code),
    paid_amount BIGINT NOT NULL CHECK (paid_amount > 0),
    wallet_transaction_id UUID NOT NULL UNIQUE,
    purchased_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    FOREIGN KEY (wallet_transaction_id, user_id, currency_code)
        REFERENCES wallet_transactions(wallet_transaction_id, user_id, currency_code),
    UNIQUE (user_id, request_id),
    UNIQUE (purchase_id, user_id, item_code)
);
CREATE INDEX ix_purchases_user ON shop_purchases (user_id, purchased_at DESC);
CREATE TABLE player_inventory (
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    item_code VARCHAR(80) NOT NULL REFERENCES cosmetic_items(item_code),
    acquisition_kind VARCHAR(24) NOT NULL CHECK (acquisition_kind IN ('default', 'purchase', 'reward', 'admin')),
    purchase_id UUID,
    reward_grant_id UUID,
    acquired_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, item_code),
    FOREIGN KEY (purchase_id, user_id, item_code) REFERENCES shop_purchases(purchase_id, user_id, item_code),
    FOREIGN KEY (reward_grant_id, user_id) REFERENCES reward_grants(reward_grant_id, user_id),
    CHECK ((acquisition_kind = 'purchase' AND purchase_id IS NOT NULL AND reward_grant_id IS NULL)
        OR (acquisition_kind = 'reward' AND reward_grant_id IS NOT NULL AND purchase_id IS NULL)
        OR (acquisition_kind IN ('default', 'admin') AND purchase_id IS NULL AND reward_grant_id IS NULL))
);
CREATE INDEX ix_inventory_item ON player_inventory (item_code, user_id);
CREATE TABLE player_equipment (
    user_id UUID NOT NULL,
    equipment_slot VARCHAR(24) NOT NULL,
    item_code VARCHAR(80) NOT NULL,
    equipped_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, equipment_slot),
    FOREIGN KEY (user_id, item_code) REFERENCES player_inventory(user_id, item_code),
    FOREIGN KEY (item_code, equipment_slot) REFERENCES cosmetic_items(item_code, equipment_slot)
);

-- 3. NOI DUNG HOC, DRILL, ONBOARDING VA KET QUA LUYEN TAP
CREATE TABLE techniques (
    technique_code VARCHAR(40) PRIMARY KEY,
    display_name VARCHAR(120) NOT NULL,
    description TEXT NOT NULL DEFAULT ''
);
CREATE TABLE lessons (
    lesson_code VARCHAR(80) PRIMARY KEY,
    title VARCHAR(160) NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    tier SMALLINT NOT NULL CHECK (tier BETWEEN 0 AND 3),
    sort_order INTEGER NOT NULL CHECK (sort_order >= 0),
    content_version INTEGER NOT NULL DEFAULT 1 CHECK (content_version > 0),
    is_published BOOLEAN NOT NULL DEFAULT false,
    created_by UUID REFERENCES app_users(user_id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE drills (
    drill_code VARCHAR(80) PRIMARY KEY,
    title VARCHAR(160) NOT NULL,
    explanation TEXT NOT NULL,
    coaching_text TEXT NOT NULL,
    tier SMALLINT NOT NULL CHECK (tier BETWEEN 0 AND 3),
    trial_count SMALLINT NOT NULL CHECK (trial_count > 0),
    required_successes SMALLINT NOT NULL CHECK (required_successes > 0 AND required_successes <= trial_count),
    first_completion_xp INTEGER NOT NULL DEFAULT 0 CHECK (first_completion_xp >= 0),
    first_completion_tokenpad BIGINT NOT NULL DEFAULT 0 CHECK (first_completion_tokenpad >= 0),
    configuration JSONB NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(configuration) = 'object'),
    content_version INTEGER NOT NULL DEFAULT 1 CHECK (content_version > 0),
    is_published BOOLEAN NOT NULL DEFAULT false,
    created_by UUID REFERENCES app_users(user_id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE lesson_drills (
    lesson_code VARCHAR(80) NOT NULL REFERENCES lessons(lesson_code),
    drill_code VARCHAR(80) NOT NULL REFERENCES drills(drill_code),
    sort_order INTEGER NOT NULL CHECK (sort_order >= 0),
    PRIMARY KEY (lesson_code, drill_code),
    UNIQUE (lesson_code, sort_order)
);
CREATE INDEX ix_lesson_drills_drill ON lesson_drills (drill_code);
CREATE TABLE drill_techniques (
    drill_code VARCHAR(80) NOT NULL REFERENCES drills(drill_code),
    technique_code VARCHAR(40) NOT NULL REFERENCES techniques(technique_code),
    is_primary BOOLEAN NOT NULL DEFAULT false,
    PRIMARY KEY (drill_code, technique_code)
);
CREATE UNIQUE INDEX uq_drill_primary_technique ON drill_techniques (drill_code) WHERE is_primary;
CREATE INDEX ix_drill_techniques_technique ON drill_techniques (technique_code);
CREATE TABLE drill_prerequisites (
    drill_code VARCHAR(80) NOT NULL REFERENCES drills(drill_code),
    prerequisite_drill_code VARCHAR(80) NOT NULL REFERENCES drills(drill_code),
    required_stars SMALLINT NOT NULL DEFAULT 1 CHECK (required_stars BETWEEN 1 AND 3),
    PRIMARY KEY (drill_code, prerequisite_drill_code),
    CHECK (drill_code <> prerequisite_drill_code)
);
CREATE INDEX ix_drill_prerequisites_reverse ON drill_prerequisites (prerequisite_drill_code);
CREATE TABLE drill_input_steps (
    drill_code VARCHAR(80) NOT NULL REFERENCES drills(drill_code),
    step_number SMALLINT NOT NULL CHECK (step_number > 0),
    input_mask SMALLINT NOT NULL CHECK (input_mask BETWEEN 1 AND 31),
    PRIMARY KEY (drill_code, step_number)
);
COMMENT ON COLUMN drill_input_steps.input_mask IS 'Unity: Drive=1, Dink=2, Volley=4, Power=8, Cross=16; cac step lap lai qua trial.';
CREATE TABLE drill_cosmetic_rewards (
    drill_code VARCHAR(80) NOT NULL REFERENCES drills(drill_code),
    item_code VARCHAR(80) NOT NULL REFERENCES cosmetic_items(item_code),
    required_stars SMALLINT NOT NULL DEFAULT 1 CHECK (required_stars BETWEEN 1 AND 3),
    PRIMARY KEY (drill_code, item_code)
);
CREATE INDEX ix_drill_cosmetic_rewards_item ON drill_cosmetic_rewards (item_code);
COMMENT ON TABLE drill_cosmetic_rewards IS 'N-N drill/cosmetic. Catalog rong vi game chua co chinh sach mo khoa cosmetic tu drill.';

CREATE TABLE training_attempts (
    attempt_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    client_attempt_id VARCHAR(100) NOT NULL CHECK (length(btrim(client_attempt_id)) > 0),
    drill_code VARCHAR(80) NOT NULL REFERENCES drills(drill_code),
    drill_version INTEGER NOT NULL CHECK (drill_version > 0),
    status VARCHAR(16) NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'paused', 'completed', 'abandoned')),
    is_review BOOLEAN NOT NULL DEFAULT false,
    total_trials SMALLINT NOT NULL CHECK (total_trials > 0),
    required_successes SMALLINT NOT NULL CHECK (required_successes BETWEEN 1 AND total_trials),
    completed_trials SMALLINT NOT NULL DEFAULT 0 CHECK (completed_trials BETWEEN 0 AND total_trials),
    successes SMALLINT NOT NULL DEFAULT 0 CHECK (successes BETWEEN 0 AND completed_trials),
    contacts INTEGER NOT NULL DEFAULT 0 CHECK (contacts >= 0),
    duration_seconds NUMERIC(14,3) NOT NULL DEFAULT 0 CHECK (duration_seconds >= 0),
    timing_total_seconds NUMERIC(14,6) NOT NULL DEFAULT 0 CHECK (timing_total_seconds >= 0),
    accuracy NUMERIC GENERATED ALWAYS AS
        (CASE WHEN completed_trials = 0 THEN 0 ELSE successes::numeric / completed_trials END) STORED,
    average_timing_seconds NUMERIC GENERATED ALWAYS AS
        (CASE WHEN contacts = 0 THEN 1 ELSE timing_total_seconds / contacts END) STORED,
    stars SMALLINT GENERATED ALWAYS AS
        (CASE WHEN status <> 'completed' OR successes < required_successes THEN 0
              WHEN successes::numeric / total_trials >= 0.90 AND contacts > 0
                   AND timing_total_seconds / nullif(contacts,0) <= 0.16 THEN 3
              WHEN successes::numeric / total_trials >= 0.80 THEN 2 ELSE 1 END) STORED,
    checkpoint JSONB NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(checkpoint) = 'object'),
    started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    ended_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id, client_attempt_id),
    UNIQUE (attempt_id, user_id),
    UNIQUE (attempt_id, user_id, drill_code),
    CHECK ((status IN ('completed', 'abandoned')) = (ended_at IS NOT NULL)),
    CHECK (ended_at IS NULL OR ended_at >= started_at),
    CHECK (status <> 'completed' OR completed_trials = total_trials)
);
CREATE UNIQUE INDEX uq_one_pending_training_attempt ON training_attempts (user_id) WHERE status IN ('active', 'paused');
CREATE INDEX ix_training_user_drill ON training_attempts (user_id, drill_code, started_at DESC);
CREATE INDEX ix_training_drill_analytics ON training_attempts (drill_code, started_at DESC);
COMMENT ON TABLE training_attempts IS 'Bang lien ket user/drill co lich su nhieu lan tap. Snapshot muc tieu khong doi khi catalog duoc sua.';
CREATE TABLE drill_reward_claims (
    user_id UUID NOT NULL,
    drill_code VARCHAR(80) NOT NULL,
    attempt_id UUID NOT NULL UNIQUE,
    reward_grant_id UUID NOT NULL UNIQUE,
    claimed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, drill_code),
    FOREIGN KEY (attempt_id, user_id, drill_code) REFERENCES training_attempts(attempt_id, user_id, drill_code),
    FOREIGN KEY (reward_grant_id, user_id) REFERENCES reward_grants(reward_grant_id, user_id)
);
COMMENT ON TABLE drill_reward_claims IS 'Moi user chi nhan thuong hoan thanh moi drill mot lan; on lai van luu stars/accuracy.';
CREATE TABLE player_technique_mastery (
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    technique_code VARCHAR(40) NOT NULL REFERENCES techniques(technique_code),
    mastery_percent NUMERIC(5,2) NOT NULL DEFAULT 0 CHECK (mastery_percent BETWEEN 0 AND 100),
    sample_count BIGINT NOT NULL DEFAULT 0 CHECK (sample_count >= 0),
    evaluator_version VARCHAR(80) NOT NULL,
    evaluated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, technique_code)
);
CREATE INDEX ix_mastery_technique ON player_technique_mastery (technique_code, mastery_percent);
CREATE TABLE player_tutorial_progress (
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    tutorial_code VARCHAR(80) NOT NULL DEFAULT 'basic',
    content_version INTEGER NOT NULL DEFAULT 1 CHECK (content_version > 0),
    last_step INTEGER NOT NULL DEFAULT 0 CHECK (last_step >= 0),
    status VARCHAR(16) NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'completed', 'skipped')),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, tutorial_code)
);
CREATE TABLE onboarding_assessments (
    assessment_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    self_assessed_tier VARCHAR(24) CHECK (self_assessed_tier IN ('beginner', 'casual', 'intermediate', 'advanced')),
    answers JSONB NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(answers) = 'object'),
    rating_system VARCHAR(16) NOT NULL DEFAULT 'elo' CHECK (rating_system IN ('elo', 'glicko2')),
    estimated_rating NUMERIC(12,3) CHECK (estimated_rating > 0),
    classifier_version VARCHAR(80),
    completed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ix_assessment_user ON onboarding_assessments (user_id, completed_at DESC);

-- 4. THANH TUU, NHIEM VU NGAY/TUAN VA STREAK
CREATE TABLE progress_definitions (
    definition_code VARCHAR(80) PRIMARY KEY,
    title VARCHAR(180) NOT NULL,
    scope VARCHAR(16) NOT NULL CHECK (scope IN ('achievement', 'daily', 'weekly')),
    metric_code VARCHAR(40) NOT NULL CHECK (metric_code IN
        ('matches', 'wins', 'points', 'returns', 'serves', 'bot_matches', 'human_matches',
         'human_wins', 'ranked_wins', 'doubles_wins', 'best_win_streak', 'higher_rated_wins')),
    target_value BIGINT NOT NULL CHECK (target_value > 0),
    reward_xp INTEGER NOT NULL DEFAULT 0 CHECK (reward_xp >= 0),
    badge_asset_key TEXT,
    is_active BOOLEAN NOT NULL DEFAULT true,
    content_version INTEGER NOT NULL DEFAULT 1 CHECK (content_version > 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (definition_code, scope)
);
CREATE TABLE definition_currency_rewards (
    definition_code VARCHAR(80) NOT NULL REFERENCES progress_definitions(definition_code),
    currency_code VARCHAR(24) NOT NULL REFERENCES currencies(currency_code),
    amount BIGINT NOT NULL CHECK (amount > 0),
    PRIMARY KEY (definition_code, currency_code)
);
CREATE TABLE definition_cosmetic_rewards (
    definition_code VARCHAR(80) NOT NULL REFERENCES progress_definitions(definition_code),
    item_code VARCHAR(80) NOT NULL REFERENCES cosmetic_items(item_code),
    PRIMARY KEY (definition_code, item_code)
);
CREATE INDEX ix_definition_cosmetic_rewards_item ON definition_cosmetic_rewards (item_code);
CREATE TABLE progress_periods (
    period_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    scope VARCHAR(16) NOT NULL CHECK (scope IN ('achievement', 'daily', 'weekly')),
    start_date DATE NOT NULL,
    end_date DATE,
    bonus_xp INTEGER NOT NULL DEFAULT 0 CHECK (bonus_xp >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (scope, start_date),
    UNIQUE (period_id, scope),
    CHECK ((scope = 'achievement' AND start_date = DATE '1970-01-01' AND end_date IS NULL AND bonus_xp = 0)
        OR (scope = 'daily' AND end_date IS NOT NULL AND end_date = start_date + 1)
        OR (scope = 'weekly' AND end_date IS NOT NULL AND end_date = start_date + 7 AND extract(isodow FROM start_date) = 1))
);
CREATE TABLE period_definitions (
    period_id UUID NOT NULL,
    definition_code VARCHAR(80) NOT NULL,
    scope VARCHAR(16) NOT NULL,
    target_snapshot BIGINT NOT NULL CHECK (target_snapshot > 0),
    reward_xp_snapshot INTEGER NOT NULL CHECK (reward_xp_snapshot >= 0),
    PRIMARY KEY (period_id, definition_code),
    FOREIGN KEY (period_id, scope) REFERENCES progress_periods(period_id, scope),
    FOREIGN KEY (definition_code, scope) REFERENCES progress_definitions(definition_code, scope)
);
CREATE INDEX ix_period_definitions_definition ON period_definitions (definition_code);
CREATE TABLE period_definition_rewards (
    period_id UUID NOT NULL,
    definition_code VARCHAR(80) NOT NULL,
    currency_code VARCHAR(24) NOT NULL REFERENCES currencies(currency_code),
    amount BIGINT NOT NULL CHECK (amount > 0),
    PRIMARY KEY (period_id, definition_code, currency_code),
    FOREIGN KEY (period_id, definition_code) REFERENCES period_definitions(period_id, definition_code)
);
CREATE TABLE period_definition_cosmetics (
    period_id UUID NOT NULL,
    definition_code VARCHAR(80) NOT NULL,
    item_code VARCHAR(80) NOT NULL REFERENCES cosmetic_items(item_code),
    PRIMARY KEY (period_id, definition_code, item_code),
    FOREIGN KEY (period_id, definition_code) REFERENCES period_definitions(period_id, definition_code)
);
CREATE INDEX ix_period_definition_cosmetics_item ON period_definition_cosmetics (item_code);
CREATE TABLE period_bonus_rewards (
    period_id UUID NOT NULL REFERENCES progress_periods(period_id),
    currency_code VARCHAR(24) NOT NULL REFERENCES currencies(currency_code),
    amount BIGINT NOT NULL CHECK (amount > 0),
    PRIMARY KEY (period_id, currency_code)
);
CREATE TABLE player_definition_progress (
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    period_id UUID NOT NULL,
    definition_code VARCHAR(80) NOT NULL,
    progress_value BIGINT NOT NULL DEFAULT 0 CHECK (progress_value >= 0),
    completed_at TIMESTAMPTZ,
    claimed_at TIMESTAMPTZ,
    reward_grant_id UUID UNIQUE,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, period_id, definition_code),
    FOREIGN KEY (period_id, definition_code) REFERENCES period_definitions(period_id, definition_code),
    FOREIGN KEY (reward_grant_id, user_id) REFERENCES reward_grants(reward_grant_id, user_id),
    CHECK ((claimed_at IS NULL) = (reward_grant_id IS NULL)),
    CHECK (claimed_at IS NULL OR (completed_at IS NOT NULL AND claimed_at >= completed_at))
);
CREATE INDEX ix_definition_progress_period ON player_definition_progress (period_id, definition_code);
CREATE TABLE player_period_bonus_claims (
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    period_id UUID NOT NULL REFERENCES progress_periods(period_id),
    reward_grant_id UUID NOT NULL UNIQUE,
    claimed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, period_id),
    FOREIGN KEY (reward_grant_id, user_id) REFERENCES reward_grants(reward_grant_id, user_id)
);
CREATE TABLE player_daily_activity (
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    activity_date DATE NOT NULL,
    completed_matches INTEGER NOT NULL DEFAULT 0 CHECK (completed_matches >= 0),
    completed_drills INTEGER NOT NULL DEFAULT 0 CHECK (completed_drills >= 0),
    active_seconds NUMERIC(14,3) NOT NULL DEFAULT 0 CHECK (active_seconds >= 0),
    PRIMARY KEY (user_id, activity_date)
);
COMMENT ON TABLE player_daily_activity IS 'activity_date theo UTC. Streak hien tai chi tinh ngay co tran hoan tat, khong tinh bo tran/gián doan.';
CREATE TABLE streak_reward_definitions (
    streak_reward_code VARCHAR(80) PRIMARY KEY,
    consecutive_days SMALLINT NOT NULL CHECK (consecutive_days > 0),
    xp_amount INTEGER NOT NULL DEFAULT 0 CHECK (xp_amount >= 0),
    tokenpad_amount BIGINT NOT NULL DEFAULT 0 CHECK (tokenpad_amount >= 0),
    is_active BOOLEAN NOT NULL DEFAULT true
);
CREATE TABLE player_streak_reward_claims (
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    streak_reward_code VARCHAR(80) NOT NULL REFERENCES streak_reward_definitions(streak_reward_code),
    streak_start_date DATE NOT NULL,
    qualifying_date DATE NOT NULL,
    reward_grant_id UUID NOT NULL UNIQUE,
    claimed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, streak_reward_code, streak_start_date),
    FOREIGN KEY (reward_grant_id, user_id) REFERENCES reward_grants(reward_grant_id, user_id),
    CHECK (qualifying_date >= streak_start_date)
);

-- 5. BAN BE, PHONG VA LOI MOI
CREATE TABLE friendships (
    user_low_id UUID NOT NULL REFERENCES app_users(user_id),
    user_high_id UUID NOT NULL REFERENCES app_users(user_id),
    requested_by UUID NOT NULL REFERENCES app_users(user_id),
    status VARCHAR(16) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'accepted', 'declined', 'removed')),
    requested_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    responded_at TIMESTAMPTZ,
    PRIMARY KEY (user_low_id, user_high_id),
    CHECK (user_low_id < user_high_id),
    CHECK (requested_by IN (user_low_id, user_high_id)),
    CHECK (status = 'pending' OR responded_at IS NOT NULL),
    CHECK (responded_at IS NULL OR responded_at >= requested_at)
);
CREATE INDEX ix_friendships_high ON friendships (user_high_id, status);
COMMENT ON TABLE friendships IS 'Bang noi tu lien ket N-N users/users; luu moi cap mot row, sap UUID tang dan de tranh A-B va B-A trung.';
CREATE TABLE player_blocks (
    blocker_user_id UUID NOT NULL REFERENCES app_users(user_id),
    blocked_user_id UUID NOT NULL REFERENCES app_users(user_id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (blocker_user_id, blocked_user_id),
    CHECK (blocker_user_id <> blocked_user_id)
);
CREATE INDEX ix_blocks_target ON player_blocks (blocked_user_id);
CREATE TABLE rooms (
    room_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    room_code VARCHAR(16) NOT NULL UNIQUE CHECK (room_code ~ '^[A-Z0-9]{4,16}$'),
    external_session_id VARCHAR(160) UNIQUE,
    host_user_id UUID NOT NULL REFERENCES app_users(user_id),
    play_mode VARCHAR(16) NOT NULL CHECK (play_mode IN ('singles', 'doubles')),
    room_kind VARCHAR(16) NOT NULL CHECK (room_kind IN ('custom', 'quick_match')),
    status VARCHAR(16) NOT NULL DEFAULT 'waiting' CHECK (status IN ('waiting', 'searching', 'in_match', 'closed')),
    region VARCHAR(40),
    protocol_version INTEGER NOT NULL DEFAULT 6 CHECK (protocol_version > 0),
    revision INTEGER NOT NULL DEFAULT 0 CHECK (revision >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    closed_at TIMESTAMPTZ,
    CHECK ((status = 'closed') = (closed_at IS NOT NULL)),
    CHECK (closed_at IS NULL OR closed_at >= created_at)
);
CREATE INDEX ix_rooms_host ON rooms (host_user_id, created_at DESC);
CREATE TABLE room_members (
    room_id UUID NOT NULL REFERENCES rooms(room_id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    team_number SMALLINT CHECK (team_number IN (0, 1)),
    team_seat SMALLINT CHECK (team_seat BETWEEN 1 AND 2),
    group_key VARCHAR(100),
    is_ready BOOLEAN NOT NULL DEFAULT false,
    joined_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    left_at TIMESTAMPTZ,
    PRIMARY KEY (room_id, user_id),
    CHECK ((team_number IS NULL) = (team_seat IS NULL)),
    CHECK (left_at IS NULL OR left_at >= joined_at)
);
CREATE UNIQUE INDEX uq_active_room_seat ON room_members (room_id, team_number, team_seat) WHERE left_at IS NULL;
CREATE INDEX ix_room_members_user ON room_members (user_id, joined_at DESC);
CREATE TABLE room_invitations (
    invitation_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    room_id UUID NOT NULL,
    inviter_user_id UUID NOT NULL,
    invitee_user_id UUID NOT NULL REFERENCES app_users(user_id),
    status VARCHAR(16) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'accepted', 'declined', 'cancelled', 'expired')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    responded_at TIMESTAMPTZ,
    FOREIGN KEY (room_id, inviter_user_id) REFERENCES room_members(room_id, user_id),
    CHECK (inviter_user_id <> invitee_user_id),
    CHECK (expires_at > created_at),
    CHECK (responded_at IS NULL OR responded_at >= created_at)
);
CREATE UNIQUE INDEX uq_pending_room_invite ON room_invitations (room_id, invitee_user_id) WHERE status = 'pending';
CREATE INDEX ix_invitations_invitee ON room_invitations (invitee_user_id, status, created_at DESC);
COMMENT ON TABLE rooms IS 'Luu metadata/lich su phong. Hang cho matchmaking va state bong realtime nam o Unity Services/Redis, khong ghi PostgreSQL moi frame.';

-- 6. BOT, TRAN DAU, DOI HINH, TY SO VA LICH SU RATING
CREATE TABLE bot_profiles (
    bot_profile_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bot_code VARCHAR(80) NOT NULL UNIQUE,
    display_name VARCHAR(80) NOT NULL,
    difficulty SMALLINT NOT NULL CHECK (difficulty BETWEEN 0 AND 2),
    behavior_version VARCHAR(80) NOT NULL DEFAULT 'unity-rule-v1',
    configuration JSONB NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(configuration) = 'object'),
    is_active BOOLEAN NOT NULL DEFAULT true
);
CREATE TABLE matches (
    match_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    external_match_id VARCHAR(120) UNIQUE,
    room_id UUID REFERENCES rooms(room_id),
    match_kind VARCHAR(24) NOT NULL CHECK (match_kind IN
        ('bot_practice', 'ranked_singles', 'ranked_bot', 'matched_doubles', 'custom')),
    play_mode VARCHAR(16) NOT NULL CHECK (play_mode IN ('singles', 'doubles')),
    status VARCHAR(16) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'running', 'completed', 'interrupted', 'cancelled')),
    region VARCHAR(40),
    is_night BOOLEAN NOT NULL DEFAULT false,
    target_points SMALLINT NOT NULL DEFAULT 11 CHECK (target_points > 0),
    best_of_games SMALLINT NOT NULL DEFAULT 3 CHECK (best_of_games IN (1, 3, 5)),
    winner_team_number SMALLINT CHECK (winner_team_number IN (0, 1)),
    end_reason VARCHAR(160),
    started_at TIMESTAMPTZ,
    ended_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK ((status IN ('completed', 'interrupted', 'cancelled')) = (ended_at IS NOT NULL)),
    CHECK ((status = 'completed') = (winner_team_number IS NOT NULL)),
    CHECK (status NOT IN ('running', 'completed', 'interrupted') OR started_at IS NOT NULL),
    CHECK (ended_at IS NULL OR started_at IS NULL OR ended_at >= started_at),
    CHECK (match_kind NOT IN ('bot_practice', 'ranked_singles', 'ranked_bot') OR play_mode = 'singles'),
    CHECK (match_kind <> 'matched_doubles' OR play_mode = 'doubles')
);
CREATE INDEX ix_matches_started ON matches (started_at DESC);
CREATE INDEX ix_matches_room ON matches (room_id);
CREATE INDEX ix_live_matches ON matches (status, region) WHERE status IN ('pending', 'running');
CREATE TABLE match_teams (
    match_id UUID NOT NULL REFERENCES matches(match_id) ON DELETE CASCADE,
    team_number SMALLINT NOT NULL CHECK (team_number IN (0, 1)),
    display_name VARCHAR(100),
    PRIMARY KEY (match_id, team_number)
);
ALTER TABLE matches ADD CONSTRAINT fk_match_winner_team
    FOREIGN KEY (match_id, winner_team_number) REFERENCES match_teams(match_id, team_number)
    DEFERRABLE INITIALLY DEFERRED;
CREATE TABLE match_participants (
    participant_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id UUID NOT NULL,
    team_number SMALLINT NOT NULL,
    team_seat SMALLINT NOT NULL CHECK (team_seat BETWEEN 1 AND 2),
    user_id UUID REFERENCES app_users(user_id),
    bot_profile_id UUID REFERENCES bot_profiles(bot_profile_id),
    actor_external_id VARCHAR(160) NOT NULL,
    display_name_snapshot VARCHAR(80) NOT NULL,
    rating_snapshot NUMERIC(12,3) NOT NULL DEFAULT 100 CHECK (rating_snapshot >= 0),
    paddle_item_code VARCHAR(80) REFERENCES cosmetic_items(item_code),
    outcome VARCHAR(16) CHECK (outcome IN ('won', 'lost', 'interrupted')),
    is_forfeit BOOLEAN NOT NULL DEFAULT false,
    points_scored INTEGER NOT NULL DEFAULT 0 CHECK (points_scored >= 0),
    valid_returns INTEGER NOT NULL DEFAULT 0 CHECK (valid_returns >= 0),
    valid_serves INTEGER NOT NULL DEFAULT 0 CHECK (valid_serves >= 0),
    reward_grant_id UUID UNIQUE,
    result_applied_at TIMESTAMPTZ,
    FOREIGN KEY (match_id, team_number) REFERENCES match_teams(match_id, team_number),
    FOREIGN KEY (reward_grant_id, user_id) REFERENCES reward_grants(reward_grant_id, user_id),
    UNIQUE (match_id, team_number, team_seat),
    UNIQUE (match_id, actor_external_id),
    UNIQUE (match_id, user_id),
    UNIQUE (match_id, participant_id),
    CHECK (num_nonnulls(user_id, bot_profile_id) = 1),
    CHECK (user_id IS NOT NULL OR (reward_grant_id IS NULL AND result_applied_at IS NULL)),
    CHECK (reward_grant_id IS NULL OR result_applied_at IS NOT NULL),
    CHECK (NOT is_forfeit OR outcome IS NOT DISTINCT FROM 'lost')
);
CREATE INDEX ix_participants_user_history ON match_participants (user_id, match_id) WHERE user_id IS NOT NULL;
CREATE INDEX ix_participants_bot ON match_participants (bot_profile_id) WHERE bot_profile_id IS NOT NULL;
CREATE TABLE match_games (
    match_id UUID NOT NULL REFERENCES matches(match_id),
    game_number SMALLINT NOT NULL CHECK (game_number > 0),
    team_0_score INTEGER NOT NULL DEFAULT 0 CHECK (team_0_score >= 0),
    team_1_score INTEGER NOT NULL DEFAULT 0 CHECK (team_1_score >= 0),
    is_completed BOOLEAN NOT NULL DEFAULT false,
    winner_team_number SMALLINT GENERATED ALWAYS AS
        (CASE WHEN NOT is_completed THEN NULL WHEN team_0_score > team_1_score THEN 0 ELSE 1 END) STORED,
    PRIMARY KEY (match_id, game_number),
    CHECK (NOT is_completed OR team_0_score <> team_1_score)
);
CREATE TABLE rating_events (
    rating_event_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL,
    rating_system VARCHAR(16) NOT NULL,
    play_mode VARCHAR(16) NOT NULL,
    match_id UUID,
    event_key VARCHAR(160) NOT NULL,
    rating_before NUMERIC(12,3) NOT NULL,
    rating_after NUMERIC(12,3) NOT NULL,
    rating_delta NUMERIC GENERATED ALWAYS AS (rating_after - rating_before) STORED,
    deviation_after NUMERIC(10,3),
    volatility_after NUMERIC(12,8),
    reliability_after NUMERIC(5,2) NOT NULL DEFAULT 0 CHECK (reliability_after BETWEEN 0 AND 100),
    reason VARCHAR(120) NOT NULL,
    algorithm_version VARCHAR(80) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    FOREIGN KEY (user_id, rating_system, play_mode) REFERENCES player_rating_states(user_id, rating_system, play_mode),
    FOREIGN KEY (match_id, user_id) REFERENCES match_participants(match_id, user_id),
    UNIQUE (user_id, rating_system, play_mode, event_key),
    UNIQUE (match_id, user_id, rating_system, play_mode),
    CHECK ((rating_system = 'elo' AND rating_before >= 100 AND rating_after >= 100
                AND rating_before = trunc(rating_before) AND rating_after = trunc(rating_after)
                AND deviation_after IS NULL AND volatility_after IS NULL)
        OR (rating_system = 'glicko2' AND rating_before > 0 AND rating_after > 0
                AND deviation_after IS NOT NULL AND deviation_after > 0
                AND volatility_after IS NOT NULL AND volatility_after > 0))
);
CREATE INDEX ix_rating_events_history ON rating_events (user_id, created_at DESC);

-- 7. TELEMETRY, LOI KY THUAT, FEEDBACK VA GOI Y DRILL
CREATE TABLE technique_errors (
    error_code VARCHAR(40) PRIMARY KEY,
    display_name VARCHAR(120) NOT NULL,
    description TEXT NOT NULL DEFAULT ''
);
CREATE TABLE coaching_tips (
    tip_code VARCHAR(80) PRIMARY KEY,
    locale VARCHAR(16) NOT NULL DEFAULT 'vi-VN',
    tip_text TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true
);
CREATE TABLE error_tips (
    error_code VARCHAR(40) NOT NULL REFERENCES technique_errors(error_code),
    tip_code VARCHAR(80) NOT NULL REFERENCES coaching_tips(tip_code),
    priority SMALLINT NOT NULL DEFAULT 1 CHECK (priority > 0),
    PRIMARY KEY (error_code, tip_code)
);
CREATE INDEX ix_error_tips_tip ON error_tips (tip_code);
CREATE TABLE training_attempt_errors (
    attempt_id UUID NOT NULL REFERENCES training_attempts(attempt_id),
    error_code VARCHAR(40) NOT NULL REFERENCES technique_errors(error_code),
    occurrences INTEGER NOT NULL CHECK (occurrences > 0),
    PRIMARY KEY (attempt_id, error_code)
);
CREATE INDEX ix_training_errors_type ON training_attempt_errors (error_code, attempt_id);
CREATE TABLE shot_telemetry (
    shot_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    match_id UUID,
    participant_id UUID,
    attempt_id UUID REFERENCES training_attempts(attempt_id),
    trial_number SMALLINT,
    shot_sequence INTEGER NOT NULL CHECK (shot_sequence > 0),
    technique_code VARCHAR(40) NOT NULL REFERENCES techniques(technique_code),
    occurred_at TIMESTAMPTZ NOT NULL,
    elapsed_seconds NUMERIC(14,6) NOT NULL CHECK (elapsed_seconds >= 0),
    timing_error_seconds NUMERIC(10,6),
    contact_x NUMERIC(10,4), contact_y NUMERIC(10,4), contact_z NUMERIC(10,4),
    actor_x NUMERIC(10,4), actor_z NUMERIC(10,4),
    landing_x NUMERIC(10,4), landing_z NUMERIC(10,4),
    ball_peak_height NUMERIC(10,4) CHECK (ball_peak_height >= 0),
    is_cross_court BOOLEAN NOT NULL DEFAULT false,
    is_power BOOLEAN NOT NULL DEFAULT false,
    bounce_count SMALLINT CHECK (bounce_count >= 0),
    was_in_kitchen BOOLEAN NOT NULL DEFAULT false,
    hit_target BOOLEAN,
    result_code VARCHAR(24) NOT NULL CHECK (result_code IN ('valid', 'miss', 'net', 'out', 'rule_fault', 'pending')),
    FOREIGN KEY (match_id, participant_id) REFERENCES match_participants(match_id, participant_id),
    UNIQUE (match_id, participant_id, shot_sequence),
    UNIQUE (attempt_id, shot_sequence),
    CHECK ((match_id IS NOT NULL AND participant_id IS NOT NULL AND attempt_id IS NULL AND trial_number IS NULL)
        OR (match_id IS NULL AND participant_id IS NULL AND attempt_id IS NOT NULL AND trial_number > 0 AND trial_number IS NOT NULL))
);
CREATE INDEX ix_shots_attempt ON shot_telemetry (attempt_id, trial_number) WHERE attempt_id IS NOT NULL;
CREATE INDEX ix_shots_time ON shot_telemetry USING BRIN (occurred_at);
COMMENT ON TABLE shot_telemetry IS 'Moi cu danh mot row; toa do met, timing giay. Khong luu input/pose moi frame. Dat chinh sach retention theo nhu cau analytics.';
CREATE TABLE shot_errors (
    shot_id BIGINT NOT NULL REFERENCES shot_telemetry(shot_id) ON DELETE CASCADE,
    error_code VARCHAR(40) NOT NULL REFERENCES technique_errors(error_code),
    confidence NUMERIC(5,4) NOT NULL DEFAULT 1 CHECK (confidence BETWEEN 0 AND 1),
    PRIMARY KEY (shot_id, error_code)
);
CREATE INDEX ix_shot_errors_type ON shot_errors (error_code);
CREATE TABLE coaching_feedback (
    feedback_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    match_id UUID,
    attempt_id UUID,
    summary TEXT NOT NULL,
    evaluator_kind VARCHAR(16) NOT NULL CHECK (evaluator_kind IN ('rule', 'classifier', 'hybrid')),
    evaluator_version VARCHAR(80) NOT NULL,
    processing_ms INTEGER NOT NULL CHECK (processing_ms >= 0),
    generated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    FOREIGN KEY (match_id, user_id) REFERENCES match_participants(match_id, user_id),
    FOREIGN KEY (attempt_id, user_id) REFERENCES training_attempts(attempt_id, user_id),
    UNIQUE (feedback_id, user_id),
    CHECK (num_nonnulls(match_id, attempt_id) = 1)
);
CREATE INDEX ix_feedback_user ON coaching_feedback (user_id, generated_at DESC);
CREATE INDEX ix_feedback_attempt ON coaching_feedback (attempt_id);
CREATE INDEX ix_feedback_match ON coaching_feedback (match_id);
CREATE TABLE feedback_errors (
    feedback_id UUID NOT NULL REFERENCES coaching_feedback(feedback_id) ON DELETE CASCADE,
    error_code VARCHAR(40) NOT NULL REFERENCES technique_errors(error_code),
    occurrences INTEGER NOT NULL CHECK (occurrences > 0),
    PRIMARY KEY (feedback_id, error_code)
);
CREATE INDEX ix_feedback_errors_type ON feedback_errors (error_code);
CREATE TABLE feedback_tips (
    feedback_id UUID NOT NULL REFERENCES coaching_feedback(feedback_id) ON DELETE CASCADE,
    tip_code VARCHAR(80) NOT NULL REFERENCES coaching_tips(tip_code),
    tip_text_snapshot TEXT NOT NULL,
    sort_order SMALLINT NOT NULL CHECK (sort_order > 0),
    PRIMARY KEY (feedback_id, tip_code),
    UNIQUE (feedback_id, sort_order)
);
CREATE INDEX ix_feedback_tips_tip ON feedback_tips (tip_code);
CREATE TABLE drill_recommendations (
    recommendation_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES app_users(user_id),
    drill_code VARCHAR(80) NOT NULL REFERENCES drills(drill_code),
    feedback_id UUID,
    priority SMALLINT NOT NULL CHECK (priority > 0),
    reason TEXT NOT NULL,
    recommender_version VARCHAR(80) NOT NULL,
    status VARCHAR(16) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'accepted', 'completed', 'dismissed')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    FOREIGN KEY (feedback_id, user_id) REFERENCES coaching_feedback(feedback_id, user_id)
);
CREATE INDEX ix_recommendations_user ON drill_recommendations (user_id, status, priority);
CREATE INDEX ix_recommendations_drill ON drill_recommendations (drill_code);
CREATE INDEX ix_recommendations_feedback ON drill_recommendations (feedback_id);
CREATE TABLE recommendation_errors (
    recommendation_id UUID NOT NULL REFERENCES drill_recommendations(recommendation_id) ON DELETE CASCADE,
    error_code VARCHAR(40) NOT NULL REFERENCES technique_errors(error_code),
    PRIMARY KEY (recommendation_id, error_code)
);
CREATE INDEX ix_recommendation_errors_type ON recommendation_errors (error_code);

-- 8. REPLAY: CHI LUU METADATA, FILE CHUNK O LOCAL/OBJECT STORAGE
CREATE TABLE match_replays (
    replay_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id UUID NOT NULL,
    owner_user_id UUID NOT NULL,
    format_version INTEGER NOT NULL DEFAULT 1 CHECK (format_version > 0),
    sample_rate SMALLINT NOT NULL DEFAULT 30 CHECK (sample_rate BETWEEN 1 AND 120),
    duration_seconds NUMERIC(14,3) NOT NULL DEFAULT 0 CHECK (duration_seconds >= 0),
    is_complete BOOLEAN NOT NULL DEFAULT false,
    storage_prefix TEXT NOT NULL CHECK (length(btrim(storage_prefix)) > 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    FOREIGN KEY (match_id, owner_user_id) REFERENCES match_participants(match_id, user_id),
    UNIQUE (match_id, owner_user_id)
);
CREATE INDEX ix_replays_owner ON match_replays (owner_user_id, created_at DESC);
CREATE TABLE replay_chunks (
    replay_id UUID NOT NULL REFERENCES match_replays(replay_id) ON DELETE CASCADE,
    chunk_number INTEGER NOT NULL CHECK (chunk_number >= 0),
    storage_key TEXT NOT NULL,
    start_seconds NUMERIC(14,6) NOT NULL CHECK (start_seconds >= 0),
    end_seconds NUMERIC(14,6) NOT NULL CHECK (end_seconds >= start_seconds),
    size_bytes BIGINT CHECK (size_bytes > 0),
    sha256 BYTEA CHECK (octet_length(sha256) = 32),
    PRIMARY KEY (replay_id, chunk_number),
    UNIQUE (replay_id, storage_key)
);
COMMENT ON TABLE replay_chunks IS 'Khong luu mang ReplayFrame/bones vao quan he SQL; luu .json.gz va checksum trong object storage.';

-- 9. BAO CAO, QUAN TRI VA AUDIT
CREATE TABLE player_reports (
    report_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reporter_user_id UUID NOT NULL REFERENCES app_users(user_id),
    reported_user_id UUID NOT NULL REFERENCES app_users(user_id),
    match_id UUID,
    reason_code VARCHAR(24) NOT NULL CHECK (reason_code IN ('cheating', 'abuse', 'disconnect', 'other')),
    description TEXT NOT NULL,
    status VARCHAR(16) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'reviewing', 'resolved', 'dismissed')),
    reviewed_by UUID REFERENCES app_users(user_id),
    reviewed_at TIMESTAMPTZ,
    resolution TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    FOREIGN KEY (match_id, reported_user_id) REFERENCES match_participants(match_id, user_id),
    CHECK (reporter_user_id <> reported_user_id),
    CHECK (status NOT IN ('resolved', 'dismissed') OR (reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL))
);
CREATE INDEX ix_reports_queue ON player_reports (status, created_at);
CREATE INDEX ix_reports_target ON player_reports (reported_user_id, created_at DESC);
CREATE TABLE moderation_actions (
    action_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    target_user_id UUID NOT NULL REFERENCES app_users(user_id),
    actor_user_id UUID NOT NULL REFERENCES app_users(user_id),
    report_id UUID REFERENCES player_reports(report_id),
    action_code VARCHAR(24) NOT NULL CHECK (action_code IN ('warning', 'suspend', 'ban', 'unban', 'reset_progress', 'restore_progress')),
    reason TEXT NOT NULL,
    effective_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ,
    CHECK (expires_at IS NULL OR expires_at > effective_at)
);
CREATE INDEX ix_moderation_target ON moderation_actions (target_user_id, effective_at DESC);
CREATE TABLE audit_logs (
    audit_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    actor_user_id UUID REFERENCES app_users(user_id),
    action_code VARCHAR(80) NOT NULL,
    entity_type VARCHAR(80) NOT NULL,
    entity_key VARCHAR(180) NOT NULL,
    request_id VARCHAR(160),
    details JSONB NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(details) = 'object'),
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ix_audit_entity ON audit_logs (entity_type, entity_key, occurred_at DESC);
CREATE INDEX ix_audit_actor ON audit_logs (actor_user_id, occurred_at DESC);
COMMENT ON COLUMN audit_logs.details IS 'Snapshot thay doi phuc vu audit; khong ghi password, token hoac bi mat vao log.';

-- 10. SEED TU GAME HIEN TAI; KHONG TAO USER/ADMIN MAT KHAU MAU
INSERT INTO roles VALUES ('player', 'Người chơi'), ('admin', 'Quản trị viên'), ('moderator', 'Điều hành');
INSERT INTO currencies VALUES ('TOKENPAD', 'Tokenpad', false), ('ELITE_TOKEN', 'Elite Token', true);
INSERT INTO item_collections VALUES
    ('original', 'ORIGINAL COLLECTION'), ('signature', 'SIGNATURE COLLECTION'), ('elite', 'ELITE COLLECTION');
INSERT INTO cosmetic_items (item_code, collection_code, display_name, description, equipment_slot, is_default, release_order) VALUES
    ('paddle.default', 'original', 'PICKLEVERSE', 'Vợt mặc định của PickleVerse.', 'paddle', true, 0),
    ('paddle.thot', 'signature', 'THOT', 'Hoa văn nổi bật cho phong cách thi đấu riêng.', 'paddle', false, 1),
    ('paddle.gemstone', 'elite', 'GEMSTONE', 'Viền đá quý, sắc vàng và mặt carbon.', 'paddle', false, 2);
INSERT INTO item_prices VALUES ('paddle.thot', 'TOKENPAD', 900), ('paddle.thot', 'ELITE_TOKEN', 250), ('paddle.gemstone', 'ELITE_TOKEN', 650);
INSERT INTO techniques (technique_code, display_name) VALUES
    ('serve', 'Giao bóng'), ('drive', 'Drive'), ('dink', 'Dink'), ('volley', 'Volley'),
    ('modifiers', 'Lực và hướng chéo'), ('drop', 'Third-shot drop'), ('reset', 'Reset'),
    ('smash', 'Smash'), ('footwork', 'Di chuyển và hồi vị'), ('approach', 'Tiến lên sau drop'),
    ('doubles-cover', 'Giữ khoảng cách đánh đôi'), ('doubles-transition', 'Phối hợp đánh đôi');
INSERT INTO drills (drill_code, title, explanation, coaching_text, tier, trial_count, required_successes, first_completion_xp, first_completion_tokenpad, is_published) VALUES
    ('serve', 'Giao bóng đúng ô', 'Giao dưới tay từ sau baseline sang ô chéo, vượt vạch Kitchen.', 'Giữ chân sau baseline; giữ rồi thả Drive, không bước vào sân trước tiếp xúc.', 0, 8, 6, 5, 150, true),
    ('drive', 'Drive về cuối sân', 'Đón bóng sau một lần nảy và đánh vào vùng sâu.', 'Chuẩn bị vợt trước người; thả phím sớm hơn lúc bóng tới.', 0, 8, 6, 5, 150, true),
    ('dink', 'Dink kiểm soát Kitchen', 'Đánh nhẹ sau nảy vào Kitchen đối phương.', 'Ưu tiên cung bóng thấp và kiểm soát lực.', 0, 8, 6, 5, 150, true),
    ('volley', 'Volley ngoài Kitchen', 'Chặn trước nảy; chân và đà sau đánh ở ngoài Kitchen.', 'Giữ vị trí sau vạch; không lao vào Kitchen sau volley.', 0, 8, 6, 5, 150, true),
    ('modifiers', 'Lực và hướng chéo', 'Luân phiên cú đánh tăng lực hoặc chéo theo chỉ dẫn.', 'Giữ modifier đến lúc thả nút đánh; quan sát vùng đích.', 1, 10, 7, 7, 225, true),
    ('drop', 'Third-shot drop', 'Đánh cú thứ ba mềm vào Kitchen để có thời gian lên lưới.', 'Ghép Drive + Dink; đánh sau nảy vào vùng gần lưới.', 1, 10, 7, 7, 225, true),
    ('reset', 'Reset bóng nhanh', 'Giảm nhịp bóng nhanh, đưa bóng về Kitchen.', 'Ghép Dink + Volley, tiếp bóng trên không ngoài Kitchen.', 1, 10, 7, 7, 225, true),
    ('smash', 'Smash bóng bổng', 'Đánh bóng đủ cao vào vùng sâu.', 'Ghép Drive + Volley; thả khi timing vào vùng xanh.', 2, 12, 9, 10, 300, true),
    ('footwork', 'Di chuyển và hồi vị', 'Đón hai bên, đánh Drive rồi về vị trí hồi phục.', 'Di chuyển tới vùng đón bóng; sau đánh quay về vòng hồi vị.', 2, 12, 9, 10, 300, true),
    ('approach', 'Drop rồi tiến lên', 'Drop từ cuối sân rồi tiến lên trước Kitchen.', 'Ghép Drive + Dink rồi tiến lên và dừng trước Kitchen.', 2, 12, 9, 10, 300, true),
    ('doubles-cover', 'Đánh đôi: giữ khoảng cách', 'Giữ hàng ngang, dịch chuyển cùng phía bóng.', 'Volley theo chỉ dẫn, hồi vị và giữ cùng hàng với đồng đội.', 3, 12, 9, 15, 450, true),
    ('doubles-transition', 'Đánh đôi: drop và phối hợp', 'Drop tiến lên cùng nhau, chuyển dink/volley và nhường lượt AI.', 'Giữ vị trí và đội hình; để AI đánh khi đến lượt đồng đội.', 3, 12, 9, 15, 450, true);
INSERT INTO lessons (lesson_code, title, description, tier, sort_order, is_published)
SELECT drill_code, title, explanation, tier,
       row_number() OVER (ORDER BY CASE drill_code
        WHEN 'serve' THEN 1 WHEN 'drive' THEN 2 WHEN 'dink' THEN 3 WHEN 'volley' THEN 4
        WHEN 'modifiers' THEN 5 WHEN 'drop' THEN 6 WHEN 'reset' THEN 7 WHEN 'smash' THEN 8
        WHEN 'footwork' THEN 9 WHEN 'approach' THEN 10 WHEN 'doubles-cover' THEN 11 ELSE 12 END), true
FROM drills;
INSERT INTO lesson_drills SELECT lesson_code, lesson_code, 1 FROM lessons;
INSERT INTO drill_techniques SELECT drill_code, drill_code, true FROM drills;
INSERT INTO drill_prerequisites (drill_code, prerequisite_drill_code) VALUES
    ('modifiers', 'drive'), ('modifiers', 'dink'), ('modifiers', 'volley'),
    ('drop', 'drive'), ('drop', 'dink'), ('reset', 'dink'), ('reset', 'volley'),
    ('smash', 'volley'), ('smash', 'modifiers'), ('footwork', 'drive'), ('footwork', 'modifiers'),
    ('approach', 'drop'), ('approach', 'footwork'), ('doubles-cover', 'volley'), ('doubles-cover', 'footwork'),
    ('doubles-transition', 'approach'), ('doubles-transition', 'doubles-cover'), ('doubles-transition', 'reset');
INSERT INTO drill_input_steps VALUES
    ('serve',1,1), ('drive',1,1), ('dink',1,2), ('volley',1,4),
    ('modifiers',1,9), ('modifiers',2,17), ('modifiers',3,10), ('modifiers',4,18), ('modifiers',5,12), ('modifiers',6,20),
    ('drop',1,3), ('reset',1,6), ('smash',1,5), ('footwork',1,1), ('footwork',2,17),
    ('approach',1,3), ('doubles-cover',1,4), ('doubles-cover',2,20),
    ('doubles-transition',1,3), ('doubles-transition',2,2), ('doubles-transition',3,4);
INSERT INTO technique_errors (error_code, display_name) VALUES
    ('wrong_input','Sai phím hoặc kỹ thuật'), ('early','Đánh quá sớm'), ('late','Đánh hụt / quá muộn'),
    ('kitchen','Volley trong Kitchen'), ('momentum','Đà volley vào Kitchen'), ('net','Bóng chạm lưới'),
    ('out','Bóng ngoài sân'), ('target','Chưa trúng vùng đích'), ('serve_feet','Chân giao bóng sai'),
    ('movement','Chưa hồi vị / tiến lên'), ('spacing','Khoảng cách đội hình'), ('dink_high','Dink quá cao');
INSERT INTO coaching_tips (tip_code, tip_text) VALUES
    ('tip.wrong_input','Giữ đúng tổ hợp trên HUD; giữ modifier đến lúc thả nút đánh.'),
    ('tip.early','Đợi bóng nảy nếu bài yêu cầu; chọn thời điểm để vợt tới đúng lúc.'),
    ('tip.late','Di chuyển vào vòng đón bóng và thả sớm hơn thời điểm tiếp xúc.'),
    ('tip.kitchen','Lùi cả hai chân ra ngoài vạch Kitchen trước khi volley.'),
    ('tip.momentum','Dừng lại sau volley, đừng để đà đưa chân vào Kitchen.'),
    ('tip.net','Tiếp bóng phía trước người và đúng độ cao của kỹ thuật.'),
    ('tip.out','Giữ bóng trong đường biên; ưu tiên kiểm soát hơn lực.'),
    ('tip.target','Đón bóng đúng vị trí và chọn hướng thẳng hoặc chéo theo vùng sáng.'),
    ('tip.serve_feet','Giữ chân sau baseline, giữa đường giữa sân và biên dọc khi giao.'),
    ('tip.movement','Sau cú đánh, di chuyển về vùng hồi vị được đánh dấu.'),
    ('tip.spacing','Giữ cùng hàng với đồng đội, khoảng cách hợp lý và khe giữa nhỏ.'),
    ('tip.dink_high','Giảm lực và giữ cung bóng dink thấp để đối thủ khó tấn công.');
INSERT INTO error_tips (error_code, tip_code) SELECT error_code, 'tip.' || error_code FROM technique_errors;
INSERT INTO bot_profiles (bot_code, display_name, difficulty, configuration) VALUES
    ('bot.easy','Bot Dễ',0,'{"reaction_seconds":0.28,"contact_error":0.30}'),
    ('bot.medium','Bot Vừa',1,'{"reaction_seconds":0.15,"contact_error":0.16}'),
    ('bot.hard','Bot Khó',2,'{"reaction_seconds":0.08,"contact_error":0.08}');
INSERT INTO progress_definitions (definition_code, title, scope, metric_code, target_value, reward_xp) VALUES
    ('A.Win5','Thắng 5 trận','achievement','wins',5,10),
    ('A.Win25','Thắng 25 trận','achievement','wins',25,25),
    ('A.Win100','Thắng 100 trận','achievement','wins',100,50),
    ('A.Play10','Hoàn tất 10 trận','achievement','matches',10,10),
    ('A.Play50','Hoàn tất 50 trận','achievement','matches',50,25),
    ('A.Play200','Hoàn tất 200 trận','achievement','matches',200,75),
    ('A.Point100','Ghi 100 điểm cho đội','achievement','points',100,15),
    ('A.Point500','Ghi 500 điểm cho đội','achievement','points',500,35),
    ('A.Streak5','Đạt chuỗi thắng 5 trận','achievement','best_win_streak',5,30),
    ('A.Higher5','Thắng 5 đối thủ Elo cao hơn','achievement','higher_rated_wins',5,25),
    ('D.Play1','Hoàn tất 1 trận','daily','matches',1,5),
    ('D.Win1','Thắng 1 trận','daily','wins',1,5),
    ('D.Point20','Ghi 20 điểm cho đội','daily','points',20,5),
    ('D.Return30','Trả bóng hợp lệ 30 lần','daily','returns',30,5),
    ('D.Serve10','Giao bóng hợp lệ 10 lần','daily','serves',10,5),
    ('D.Bot1','Hoàn tất 1 trận với bot','daily','bot_matches',1,5),
    ('D.HumanWin1','Thắng 1 trận tìm đối thủ người thật','daily','human_wins',1,10),
    ('W.Play10','Hoàn tất 10 trận','weekly','matches',10,20),
    ('W.Win5','Thắng 5 trận','weekly','wins',5,15),
    ('W.Point100','Ghi 100 điểm cho đội','weekly','points',100,15),
    ('W.Return200','Trả bóng hợp lệ 200 lần','weekly','returns',200,15),
    ('W.Serve50','Giao bóng hợp lệ 50 lần','weekly','serves',50,15),
    ('W.Bot5','Hoàn tất 5 trận với bot','weekly','bot_matches',5,15),
    ('W.Human5','Hoàn tất 5 trận tìm đối thủ người thật','weekly','human_matches',5,20),
    ('W.RankWin3','Thắng 3 trận rank 1v1','weekly','ranked_wins',3,20),
    ('W.DoubleWin2','Thắng 2 trận tìm trận 2v2','weekly','doubles_wins',2,20),
    ('W.Streak3','Đạt chuỗi thắng 3 trận trong tuần','weekly','best_win_streak',3,20);
INSERT INTO definition_currency_rewards (definition_code, currency_code, amount) VALUES
    ('A.Win5','TOKENPAD',100), ('A.Win25','TOKENPAD',300), ('A.Win100','TOKENPAD',1000),
    ('A.Play10','TOKENPAD',100), ('A.Play50','TOKENPAD',400), ('A.Play200','TOKENPAD',1500),
    ('A.Point100','TOKENPAD',200), ('A.Point500','TOKENPAD',600), ('A.Streak5','TOKENPAD',500), ('A.Higher5','TOKENPAD',300),
    ('D.Play1','TOKENPAD',50), ('D.Win1','TOKENPAD',75), ('D.Point20','TOKENPAD',75),
    ('D.Return30','TOKENPAD',75), ('D.Serve10','TOKENPAD',75), ('D.Bot1','TOKENPAD',75), ('D.HumanWin1','TOKENPAD',125),
    ('W.Play10','TOKENPAD',300), ('W.Win5','TOKENPAD',250), ('W.Point100','TOKENPAD',250),
    ('W.Return200','TOKENPAD',250), ('W.Serve50','TOKENPAD',250), ('W.Bot5','TOKENPAD',250),
    ('W.Human5','TOKENPAD',350), ('W.RankWin3','TOKENPAD',350), ('W.DoubleWin2','TOKENPAD',400), ('W.Streak3','TOKENPAD',350);
-- Game hien tai khong cap ELITE_TOKEN tu gameplay; khong seed phan thuong premium.
-- Streak reward chua co chinh sach thuong trong game: de catalog rong, BA them sau.
INSERT INTO progress_periods (scope, start_date) VALUES ('achievement', DATE '1970-01-01');
INSERT INTO period_definitions (period_id, definition_code, scope, target_snapshot, reward_xp_snapshot)
SELECT p.period_id, d.definition_code, d.scope, d.target_value, d.reward_xp
FROM progress_periods p JOIN progress_definitions d ON d.scope = p.scope;
INSERT INTO period_definition_rewards
SELECT pd.period_id, r.definition_code, r.currency_code, r.amount
FROM period_definitions pd JOIN definition_currency_rewards r USING (definition_code);
INSERT INTO period_definition_cosmetics
SELECT pd.period_id,r.definition_code,r.item_code
FROM period_definitions pd JOIN definition_cosmetic_rewards r USING (definition_code);

-- 11. TRIGGER VA HAM GIAO DICH
CREATE FUNCTION touch_updated_at() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN NEW.updated_at := now(); RETURN NEW; END;
$$;
DO $$
DECLARE table_name TEXT;
BEGIN
    FOREACH table_name IN ARRAY ARRAY['app_users','player_profiles','player_progression','player_rating_states',
        'cosmetic_items','lessons','drills','training_attempts','progress_definitions','player_definition_progress']
    LOOP
        EXECUTE format('CREATE TRIGGER trg_touch_updated_at BEFORE UPDATE ON pickleverse.%I FOR EACH ROW EXECUTE FUNCTION pickleverse.touch_updated_at()', table_name);
    END LOOP;
END;
$$;

CREATE FUNCTION initialize_player() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
BEGIN
    INSERT INTO player_profiles (user_id, display_name) VALUES (NEW.user_id, left(NEW.username,18));
    INSERT INTO player_progression (user_id) VALUES (NEW.user_id);
    INSERT INTO player_rating_states (user_id, rating_system, play_mode, rating)
        VALUES (NEW.user_id,'elo','singles',100);
    INSERT INTO user_roles (user_id, role_code) VALUES (NEW.user_id,'player');
    INSERT INTO player_wallets (user_id, currency_code) SELECT NEW.user_id, currency_code FROM currencies;
    INSERT INTO player_inventory (user_id,item_code,acquisition_kind)
        SELECT NEW.user_id,item_code,'default' FROM cosmetic_items WHERE is_default;
    INSERT INTO player_equipment (user_id,equipment_slot,item_code)
        SELECT NEW.user_id,equipment_slot,item_code FROM cosmetic_items WHERE is_default;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_initialize_player AFTER INSERT ON app_users FOR EACH ROW EXECUTE FUNCTION initialize_player();

CREATE FUNCTION forbid_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Lich su % bat bien; tao giao dich bu tru thay vi UPDATE/DELETE.', TG_TABLE_NAME USING ERRCODE = '55000'; END;
$$;
CREATE TRIGGER trg_rewards_immutable BEFORE UPDATE OR DELETE ON reward_grants FOR EACH ROW EXECUTE FUNCTION forbid_history_mutation();
CREATE TRIGGER trg_wallet_immutable BEFORE UPDATE OR DELETE ON wallet_transactions FOR EACH ROW EXECUTE FUNCTION forbid_history_mutation();
CREATE TRIGGER trg_rating_immutable BEFORE UPDATE OR DELETE ON rating_events FOR EACH ROW EXECUTE FUNCTION forbid_history_mutation();
CREATE TRIGGER trg_purchase_immutable BEFORE UPDATE OR DELETE ON shop_purchases FOR EACH ROW EXECUTE FUNCTION forbid_history_mutation();
CREATE TRIGGER trg_audit_immutable BEFORE UPDATE OR DELETE ON audit_logs FOR EACH ROW EXECUTE FUNCTION forbid_history_mutation();

CREATE FUNCTION apply_reward_xp() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
BEGIN
    UPDATE player_progression SET total_xp = total_xp + NEW.xp_amount WHERE user_id = NEW.user_id;
    IF NOT FOUND THEN RAISE EXCEPTION 'Chua khoi tao progression cua user %.', NEW.user_id; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_apply_reward_xp AFTER INSERT ON reward_grants FOR EACH ROW EXECUTE FUNCTION apply_reward_xp();
CREATE FUNCTION apply_wallet_transaction() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
BEGIN
    -- Khoa user truoc vi: request_key dung chung giua cac currency.
    -- Raise thay vi ON CONFLICT DO NOTHING de BEFORE trigger khong cap tien lan hai.
    PERFORM 1 FROM app_users WHERE user_id = NEW.user_id FOR UPDATE;
    IF EXISTS (SELECT 1 FROM wallet_transactions WHERE user_id = NEW.user_id AND request_key = NEW.request_key)
       OR EXISTS (SELECT 1 FROM wallet_transactions WHERE wallet_transaction_id = NEW.wallet_transaction_id)
       OR (NEW.reward_grant_id IS NOT NULL AND EXISTS (SELECT 1 FROM wallet_transactions
            WHERE reward_grant_id = NEW.reward_grant_id AND currency_code = NEW.currency_code)) THEN
        RAISE EXCEPTION 'Giao dich vi da duoc ghi; doc giao dich cu thay vi cap lai.' USING ERRCODE = '23505';
    END IF;
    UPDATE player_wallets SET balance = balance + NEW.amount, updated_at = now()
    WHERE user_id = NEW.user_id AND currency_code = NEW.currency_code AND balance::numeric + NEW.amount >= 0
    RETURNING balance INTO NEW.balance_after;
    IF NOT FOUND THEN RAISE EXCEPTION 'Vi khong ton tai hoac so du khong du.' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_apply_wallet_transaction BEFORE INSERT ON wallet_transactions FOR EACH ROW EXECUTE FUNCTION apply_wallet_transaction();

CREATE FUNCTION apply_rating_event() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
BEGIN
    UPDATE player_rating_states
    SET rating = NEW.rating_after, rating_deviation = NEW.deviation_after, volatility = NEW.volatility_after,
        reliability_percent = NEW.reliability_after, rated_matches = rated_matches + CASE WHEN NEW.match_id IS NULL THEN 0 ELSE 1 END
    WHERE user_id = NEW.user_id AND rating_system = NEW.rating_system AND play_mode = NEW.play_mode AND rating = NEW.rating_before;
    IF NOT FOUND THEN RAISE EXCEPTION 'Rating da thay doi hoac chua khoi tao; tai lai va tinh lai giao dich.' USING ERRCODE = '40001'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_apply_rating_event AFTER INSERT ON rating_events FOR EACH ROW EXECUTE FUNCTION apply_rating_event();

CREATE FUNCTION validate_drill_prerequisite() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
BEGIN
    -- Catalog nho: serialize thay doi do thi de tranh hai admin tao chu trinh dong thoi.
    PERFORM pg_advisory_xact_lock(728314001);
    IF EXISTS (WITH RECURSIVE ancestors(code) AS (
        SELECT NEW.prerequisite_drill_code
        UNION
        SELECT dp.prerequisite_drill_code FROM drill_prerequisites dp JOIN ancestors a ON dp.drill_code = a.code
    ) SELECT 1 FROM ancestors WHERE code = NEW.drill_code) THEN
        RAISE EXCEPTION 'Dieu kien drill tao chu trinh.' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_drill_no_cycle BEFORE INSERT OR UPDATE ON drill_prerequisites FOR EACH ROW EXECUTE FUNCTION validate_drill_prerequisite();

CREATE FUNCTION validate_drill_reward() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM training_attempts WHERE attempt_id = NEW.attempt_id AND user_id = NEW.user_id
        AND drill_code = NEW.drill_code AND status = 'completed' AND stars > 0 AND NOT is_review) THEN
        RAISE EXCEPTION 'Chi lan dau dat drill moi duoc nhan thuong.' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_drill_reward_eligibility BEFORE INSERT ON drill_reward_claims FOR EACH ROW EXECUTE FUNCTION validate_drill_reward();

CREATE FUNCTION validate_match_roster() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
DECLARE target_id UUID; game_row matches%ROWTYPE; max_seats INTEGER; humans INTEGER; bots INTEGER; count0 INTEGER; count1 INTEGER;
BEGIN
    IF TG_OP = 'DELETE' THEN target_id := OLD.match_id; ELSE target_id := NEW.match_id; END IF;
    SELECT * INTO game_row FROM matches WHERE match_id = target_id FOR UPDATE;
    IF NOT FOUND THEN RETURN NULL; END IF;
    max_seats := CASE WHEN game_row.play_mode = 'singles' THEN 1 ELSE 2 END;
    SELECT count(*) FILTER (WHERE user_id IS NOT NULL), count(*) FILTER (WHERE bot_profile_id IS NOT NULL),
           count(*) FILTER (WHERE team_number = 0), count(*) FILTER (WHERE team_number = 1)
    INTO humans,bots,count0,count1 FROM match_participants WHERE match_id = target_id;
    IF count0 > max_seats OR count1 > max_seats OR EXISTS
        (SELECT 1 FROM match_participants WHERE match_id = target_id AND team_seat > max_seats) THEN
        RAISE EXCEPTION 'Doi hinh vuot so ghe cua che do %.',game_row.play_mode USING ERRCODE = '23514';
    END IF;
    IF (bots > 0 AND game_row.match_kind NOT IN ('bot_practice','ranked_bot'))
       OR (bots > 1) THEN RAISE EXCEPTION 'Bot chi hop le trong tran singles voi bot.' USING ERRCODE = '23514'; END IF;
    IF game_row.status IN ('running','completed') THEN
        IF count0 < 1 OR count1 < 1 OR (game_row.match_kind <> 'custom' AND (count0 <> max_seats OR count1 <> max_seats)) THEN
            RAISE EXCEPTION 'Tran dang chay/hoan tat phai du doi hinh.' USING ERRCODE = '23514';
        END IF;
        IF game_row.match_kind IN ('bot_practice','ranked_bot') AND (bots <> 1 OR humans <> 1) THEN
            RAISE EXCEPTION 'Tran bot phai co mot user va mot bot.' USING ERRCODE = '23514'; END IF;
    END IF;
    IF game_row.status = 'completed' AND EXISTS (SELECT 1 FROM match_participants WHERE match_id = target_id
        AND outcome IS DISTINCT FROM CASE WHEN team_number = game_row.winner_team_number THEN 'won' ELSE 'lost' END) THEN
        RAISE EXCEPTION 'Ket qua participant phai khop doi thang.' USING ERRCODE = '23514'; END IF;
    RETURN NULL;
END;
$$;
CREATE CONSTRAINT TRIGGER trg_match_roster AFTER INSERT OR UPDATE ON matches DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION validate_match_roster();
CREATE CONSTRAINT TRIGGER trg_participant_roster AFTER INSERT OR UPDATE OR DELETE ON match_participants DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION validate_match_roster();

CREATE FUNCTION validate_room_member() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
DECLARE room_mode VARCHAR(16); room_status VARCHAR(16); capacity INTEGER;
BEGIN
    SELECT play_mode,status INTO room_mode,room_status FROM rooms WHERE room_id = NEW.room_id FOR UPDATE;
    capacity := CASE WHEN room_mode = 'singles' THEN 2 ELSE 4 END;
    IF NEW.left_at IS NULL AND (room_status = 'closed' OR NEW.team_seat > capacity / 2 OR
        (SELECT count(*) FROM room_members WHERE room_id = NEW.room_id AND left_at IS NULL AND user_id <> NEW.user_id) >= capacity) THEN
        RAISE EXCEPTION 'Phong da dong hoac het ghe.' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_room_capacity BEFORE INSERT OR UPDATE ON room_members FOR EACH ROW EXECUTE FUNCTION validate_room_member();

CREATE FUNCTION purchase_cosmetic(p_user_id UUID, p_item_code VARCHAR, p_currency_code VARCHAR, p_request_id VARCHAR)
RETURNS UUID LANGUAGE plpgsql SET search_path = pickleverse, pg_catalog AS $$
DECLARE existing shop_purchases%ROWTYPE; cost BIGINT; new_purchase_id UUID; new_transaction_id UUID;
BEGIN
    IF p_request_id IS NULL OR length(btrim(p_request_id)) = 0 THEN RAISE EXCEPTION 'request_id bat buoc.' USING ERRCODE = '22023'; END IF;
    -- Serialize mua cua mot user, bao gom retry dong thoi va hai loai tien.
    PERFORM 1 FROM app_users WHERE user_id = p_user_id AND status = 'active' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Tai khoan khong hoat dong.' USING ERRCODE = '23514'; END IF;
    SELECT * INTO existing FROM shop_purchases WHERE user_id = p_user_id AND request_id = p_request_id;
    IF FOUND THEN
        IF existing.item_code IS DISTINCT FROM p_item_code OR existing.currency_code IS DISTINCT FROM p_currency_code THEN
            RAISE EXCEPTION 'request_id da dung voi noi dung khac.' USING ERRCODE = '22023'; END IF;
        RETURN existing.purchase_id;
    END IF;
    SELECT ip.price INTO cost FROM item_prices ip JOIN cosmetic_items ci USING (item_code)
    WHERE ip.item_code = p_item_code AND ip.currency_code = p_currency_code AND ci.is_active AND NOT ci.is_default
    FOR SHARE OF ip,ci;
    IF NOT FOUND THEN RAISE EXCEPTION 'Vat pham khong co gia ban hop le.' USING ERRCODE = '23514'; END IF;
    IF EXISTS (SELECT 1 FROM player_inventory WHERE user_id = p_user_id AND item_code = p_item_code) THEN
        RAISE EXCEPTION 'User da so huu vat pham.' USING ERRCODE = '23505'; END IF;
    INSERT INTO wallet_transactions (user_id,currency_code,request_key,amount,balance_after,reason)
    VALUES (p_user_id,p_currency_code,'purchase:' || p_request_id,-cost,0,'cosmetic_purchase') RETURNING wallet_transaction_id INTO new_transaction_id;
    INSERT INTO shop_purchases (user_id,request_id,item_code,currency_code,paid_amount,wallet_transaction_id)
    VALUES (p_user_id,p_request_id,p_item_code,p_currency_code,cost,new_transaction_id) RETURNING purchase_id INTO new_purchase_id;
    INSERT INTO player_inventory (user_id,item_code,acquisition_kind,purchase_id) VALUES (p_user_id,p_item_code,'purchase',new_purchase_id);
    RETURN new_purchase_id;
END;
$$;
COMMENT ON FUNCTION purchase_cosmetic(UUID,VARCHAR,VARCHAR,VARCHAR) IS 'Mua atomic, retry cung request_id khong tru tien hai lan; inventory va so cai rollback cung nhau khi loi.';

CREATE FUNCTION validate_definition_progress() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
DECLARE required_value BIGINT;
BEGIN
    SELECT target_snapshot INTO required_value FROM period_definitions
        WHERE period_id = NEW.period_id AND definition_code = NEW.definition_code;
    IF NEW.completed_at IS NOT NULL AND NEW.progress_value < required_value THEN
        RAISE EXCEPTION 'Nhiem vu/thanh tuu chua dat muc tieu.' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_definition_progress BEFORE INSERT OR UPDATE ON player_definition_progress
    FOR EACH ROW EXECUTE FUNCTION validate_definition_progress();

CREATE FUNCTION validate_period_bonus() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM progress_periods WHERE period_id = NEW.period_id AND scope IN ('daily','weekly'))
        OR NOT EXISTS (SELECT 1 FROM period_definitions WHERE period_id = NEW.period_id)
        OR EXISTS (SELECT 1 FROM period_definitions pd LEFT JOIN player_definition_progress pp
            ON pp.period_id = pd.period_id AND pp.definition_code = pd.definition_code AND pp.user_id = NEW.user_id
            WHERE pd.period_id = NEW.period_id AND pp.claimed_at IS NULL) THEN
        RAISE EXCEPTION 'Chi nhan bonus sau khi nhan tat ca nhiem vu cua ky.' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_period_bonus BEFORE INSERT ON player_period_bonus_claims FOR EACH ROW EXECUTE FUNCTION validate_period_bonus();

CREATE FUNCTION validate_streak_claim() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
DECLARE required_days INTEGER; recorded_days INTEGER;
BEGIN
    SELECT consecutive_days INTO required_days FROM streak_reward_definitions
        WHERE streak_reward_code = NEW.streak_reward_code AND is_active;
    SELECT count(*) INTO recorded_days FROM player_daily_activity WHERE user_id = NEW.user_id
        AND activity_date BETWEEN NEW.streak_start_date AND NEW.qualifying_date AND completed_matches > 0;
    IF required_days IS NULL OR NEW.qualifying_date - NEW.streak_start_date + 1 < required_days
        OR recorded_days <> NEW.qualifying_date - NEW.streak_start_date + 1 THEN
        RAISE EXCEPTION 'Chuoi ngay cho phan thuong khong hop le.' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_streak_claim BEFORE INSERT ON player_streak_reward_claims FOR EACH ROW EXECUTE FUNCTION validate_streak_claim();

CREATE FUNCTION validate_reward_source() RETURNS trigger LANGUAGE plpgsql
SET search_path = pickleverse, pg_catalog AS $$
DECLARE linked BOOLEAN := false;
BEGIN
    CASE NEW.source_kind
    WHEN 'match' THEN
        SELECT EXISTS (SELECT 1 FROM match_participants mp JOIN matches m USING (match_id)
            WHERE mp.reward_grant_id = NEW.reward_grant_id AND mp.user_id = NEW.user_id
            AND m.status = 'completed' AND m.match_kind <> 'custom' AND mp.result_applied_at IS NOT NULL) INTO linked;
    WHEN 'drill' THEN
        SELECT EXISTS (SELECT 1 FROM drill_reward_claims WHERE reward_grant_id = NEW.reward_grant_id AND user_id = NEW.user_id) INTO linked;
    WHEN 'achievement', 'daily_mission', 'weekly_mission' THEN
        SELECT EXISTS (SELECT 1 FROM player_definition_progress pp JOIN progress_periods p USING (period_id)
            WHERE pp.reward_grant_id = NEW.reward_grant_id AND pp.user_id = NEW.user_id AND pp.claimed_at IS NOT NULL
            AND p.scope = CASE NEW.source_kind WHEN 'daily_mission' THEN 'daily' WHEN 'weekly_mission' THEN 'weekly' ELSE 'achievement' END) INTO linked;
    WHEN 'period_bonus' THEN
        SELECT EXISTS (SELECT 1 FROM player_period_bonus_claims WHERE reward_grant_id = NEW.reward_grant_id AND user_id = NEW.user_id) INTO linked;
    WHEN 'daily_streak' THEN
        SELECT EXISTS (SELECT 1 FROM player_streak_reward_claims WHERE reward_grant_id = NEW.reward_grant_id AND user_id = NEW.user_id) INTO linked;
    WHEN 'admin_adjustment' THEN linked := true;
    END CASE;
    IF NOT linked THEN RAISE EXCEPTION 'Phan thuong % chua lien ket ket qua/claim hop le.',NEW.source_kind USING ERRCODE = '23514'; END IF;
    RETURN NULL;
END;
$$;
CREATE CONSTRAINT TRIGGER trg_reward_source AFTER INSERT ON reward_grants
    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION validate_reward_source();

CREATE FUNCTION open_progress_period(p_scope VARCHAR, p_date DATE) RETURNS UUID
LANGUAGE plpgsql SET search_path = pickleverse, pg_catalog AS $$
DECLARE period_start DATE; period_end DATE; result_id UUID;
BEGIN
    IF p_scope NOT IN ('daily','weekly') OR p_scope IS NULL OR p_date IS NULL THEN
        RAISE EXCEPTION 'Chi tao ky daily/weekly voi ngay UTC hop le.' USING ERRCODE = '22023'; END IF;
    period_start := CASE WHEN p_scope = 'weekly' THEN p_date - (extract(isodow FROM p_date)::integer - 1) ELSE p_date END;
    period_end := period_start + CASE WHEN p_scope = 'weekly' THEN 7 ELSE 1 END;
    PERFORM pg_advisory_xact_lock(hashtextextended('pickleverse.period.' || p_scope || '.' || period_start::text,0));
    SELECT period_id INTO result_id FROM progress_periods WHERE scope = p_scope AND start_date = period_start;
    IF FOUND THEN RETURN result_id; END IF;
    INSERT INTO progress_periods (scope,start_date,end_date,bonus_xp)
        VALUES (p_scope,period_start,period_end,CASE WHEN p_scope = 'daily' THEN 20 ELSE 50 END) RETURNING period_id INTO result_id;
    INSERT INTO period_definitions (period_id,definition_code,scope,target_snapshot,reward_xp_snapshot)
        SELECT result_id,definition_code,scope,target_value,reward_xp FROM progress_definitions WHERE scope = p_scope AND is_active;
    INSERT INTO period_definition_rewards SELECT result_id,r.definition_code,r.currency_code,r.amount
        FROM definition_currency_rewards r JOIN period_definitions pd ON pd.definition_code = r.definition_code AND pd.period_id = result_id;
    INSERT INTO period_definition_cosmetics SELECT result_id,r.definition_code,r.item_code
        FROM definition_cosmetic_rewards r JOIN period_definitions pd ON pd.definition_code = r.definition_code AND pd.period_id = result_id;
    INSERT INTO period_bonus_rewards VALUES (result_id,'TOKENPAD',CASE WHEN p_scope = 'daily' THEN 200 ELSE 1000 END);
    RETURN result_id;
END;
$$;

-- 12. VIEW CHO UNITY DASHBOARD, BXH, LICH SU VA TIEN TRINH
CREATE VIEW v_player_levels AS
WITH levels AS (
    SELECT user_id,total_xp, floor((1 + sqrt(1 + 0.8 * total_xp::numeric)) / 2)::bigint AS player_level
    FROM player_progression
)
SELECT user_id,total_xp,player_level,
       total_xp - 5 * player_level * (player_level - 1) AS level_xp,
       player_level * 10 AS next_level_xp FROM levels;

CREATE VIEW v_elo_leaderboard AS
SELECT r.user_id,p.display_name,r.play_mode,r.rating::integer AS elo,
       CASE WHEN r.rating < 500 THEN 'Đồng' WHEN r.rating < 1000 THEN 'Bạc' WHEN r.rating < 2000 THEN 'Vàng'
            WHEN r.rating < 3500 THEN 'Bạch Kim' WHEN r.rating < 5000 THEN 'Kim Cương'
            WHEN r.rating < 7000 THEN 'Cao Thủ' ELSE 'Huyền Thoại' END AS rank_name,
       row_number() OVER (PARTITION BY r.play_mode ORDER BY r.rating DESC,r.user_id) AS position
FROM player_rating_states r JOIN player_profiles p USING (user_id) JOIN app_users u USING (user_id)
WHERE r.rating_system = 'elo' AND u.status = 'active';

CREATE VIEW v_player_match_history AS
SELECT mp.user_id,m.match_id,m.external_match_id,m.match_kind,m.play_mode,m.status,m.started_at,m.ended_at,
       mp.participant_id,mp.team_number,mp.outcome,mp.is_forfeit,mp.points_scored,mp.valid_returns,mp.valid_serves,
       row_number() OVER (PARTITION BY mp.user_id ORDER BY m.ended_at DESC NULLS LAST,m.match_id) AS history_number
FROM match_participants mp JOIN matches m USING (match_id)
WHERE mp.user_id IS NOT NULL AND m.status IN ('completed','interrupted');
COMMENT ON VIEW v_player_match_history IS 'Unity giu 20 tran: SELECT ... WHERE user_id = :id AND history_number <= 20. DB giu lich su day du.';

CREATE VIEW v_player_drill_progress AS
SELECT user_id,drill_code,count(*) AS attempts,greatest(count(*) - 1,0) AS retries,
       count(*) FILTER (WHERE status = 'completed' AND stars > 0) AS completions,
       max(stars) AS best_stars,coalesce(max(accuracy) FILTER (WHERE status = 'completed'),0) AS best_accuracy,
       coalesce(sum(completed_trials),0) AS trials,coalesce(sum(successes),0) AS successes,
       sum(duration_seconds) AS practice_seconds,max(started_at) AS last_attempt_at
FROM training_attempts GROUP BY user_id,drill_code;
CREATE VIEW v_player_accuracy_trends AS
SELECT user_id,drill_code,attempt_id,ended_at,accuracy,average_timing_seconds,stars,
       row_number() OVER (PARTITION BY user_id,drill_code ORDER BY ended_at DESC,attempt_id) AS recent_number
FROM training_attempts WHERE status = 'completed';

CREATE VIEW v_player_daily_streaks AS
WITH activity AS (
    SELECT user_id,activity_date,activity_date - row_number() OVER (PARTITION BY user_id ORDER BY activity_date)::integer AS island
    FROM player_daily_activity WHERE completed_matches > 0
), runs AS (
    SELECT user_id,island,min(activity_date) AS start_date,max(activity_date) AS end_date,count(*) AS streak_days
    FROM activity GROUP BY user_id,island
), summaries AS (
    SELECT user_id,max(streak_days) AS best_streak,max(end_date) AS last_activity_date FROM runs GROUP BY user_id
)
SELECT u.user_id,coalesce(s.best_streak,0) AS best_streak,s.last_activity_date,
       CASE WHEN s.last_activity_date >= (now() AT TIME ZONE 'UTC')::date - 1 THEN latest.streak_days ELSE 0 END AS current_streak
FROM app_users u LEFT JOIN summaries s USING (user_id)
LEFT JOIN runs latest ON latest.user_id = u.user_id AND latest.end_date = s.last_activity_date;

-- 13. VAN HANH VA RANH GIOI NGHIEP VU
-- Hoan tat tran/drill, rating_event, reward_grant, wallet credit, claim,
-- player_definition_progress va daily_activity phai ghi CUNG transaction backend.
-- UNIQUE/FK/trigger bao ve ghi trung; backend van phai tinh luat/XP/Elo tu du lieu tin cay.
-- Hoan tat drill: stars > 0, insert drill_reward_claims truoc khi COMMIT phan thuong.
-- Nhiem vu: check progress_value >= target_snapshot truoc khi nhan; bonus chi nhan
-- khi tat ca period_definitions cua ky da dat/nhan theo chinh sach game.
-- Moi user co UUID; bot khong co tai khoan, profile, wallet hay reward.
-- FK RESTRICT mac dinh giu lich su; xoa tai khoan bang status='deleted' va an danh PII.
-- API chi duoc GRANT quyen can thiet. Khong dung superuser/owner lam runtime login.
-- Role trong user_roles la RBAC cua ung dung, KHONG phai PostgreSQL login role.
-- TLS, ma hoa volume/backup, PITR/WAL, secret va retention do DevOps cau hinh ngoai DDL.
-- Index BRIN cho telemetry; chi partition theo thang khi co so lieu tai thuc te.
-- Replay file va telemetry can cleanup theo retention; khong prune lich su vi/phần thuong.
REVOKE ALL ON SCHEMA pickleverse FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA pickleverse FROM PUBLIC;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA pickleverse FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA pickleverse FROM PUBLIC;

COMMIT;
