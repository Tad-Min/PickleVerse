-- Idempotent upgrade: also safe to run manually against an existing dev database.
BEGIN;
ALTER TABLE verificationemail ALTER COLUMN token TYPE VARCHAR(128);
ALTER TABLE verificationemail ADD COLUMN IF NOT EXISTS failedattempts INT NOT NULL DEFAULT 0;
INSERT INTO roles (rolename, description) VALUES
    ('Player', 'Nguoi choi PickleVerse'),
    ('Admin', 'Quan tri vien PickleVerse')
ON CONFLICT (rolename) DO NOTHING;
COMMIT;
