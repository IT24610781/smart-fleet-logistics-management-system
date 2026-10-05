CREATE DATABASE IF NOT EXISTS tracking_telemetry_db;

USE tracking_telemetry_db;

CREATE TABLE trip_location_updates (
    id CHAR(36) NOT NULL,

    trip_id    CHAR(36) NOT NULL,
    driver_id  CHAR(36) NOT NULL,
    vehicle_id CHAR(36) NOT NULL,

    latitude  DECIMAL(9,6) NOT NULL,
    longitude DECIMAL(9,6) NOT NULL,

    observed_at DATETIME NOT NULL,
    received_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (id),

    KEY idx_locations_trip_id (trip_id),
    KEY idx_locations_vehicle_id (vehicle_id),
    KEY idx_locations_driver_id (driver_id)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4;