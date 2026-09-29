CREATE DATABASE IF NOT EXISTS fleet_identity_db;

USE fleet_identity_db;

CREATE TABLE users (
    id CHAR(36) NOT NULL,
    username VARCHAR(50) NOT NULL,
    email VARCHAR(255) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,

    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    contact_number VARCHAR(20) NOT NULL,

    role VARCHAR(50) NOT NULL
        CHECK (role IN ('Admin', 'Dispatcher', 'Driver')),

    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (id),

    UNIQUE KEY uq_users_username (username),
    UNIQUE KEY uq_users_email (email)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4;
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
