-- Additive migration; preserves users and existing account data.
CREATE TABLE IF NOT EXISTS password_challenges (
    id CHAR(36) PRIMARY KEY,
    user_id CHAR(36) NOT NULL,
    purpose VARCHAR(10) NOT NULL,
    code_hash CHAR(64) NOT NULL,
    password_stamp CHAR(64) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    expires_at DATETIME(6) NOT NULL,
    attempts INT NOT NULL DEFAULT 0,
    consumed BOOLEAN NOT NULL DEFAULT FALSE,
    INDEX ix_password_challenges_user (user_id, created_at)
);
CREATE TABLE IF NOT EXISTS password_security (
    user_id CHAR(36) PRIMARY KEY,
    changed_at DATETIME(6) NOT NULL
);
