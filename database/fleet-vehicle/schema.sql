CREATE DATABASE IF NOT EXISTS fleet_vehicle_db;

USE fleet_vehicle_db;

CREATE TABLE vehicles (
    id CHAR(36) NOT NULL,
    registration_no VARCHAR(20) NOT NULL,
    type VARCHAR(50) NOT NULL,
    capacity INT NOT NULL,

    status VARCHAR(50) NOT NULL DEFAULT 'Available'
        CHECK (status IN ('Available', 'On Trip', 'Under Maintenance')),

    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (id),

    UNIQUE KEY uq_vehicles_registration_no (registration_no)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4;
