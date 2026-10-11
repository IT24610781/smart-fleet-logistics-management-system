CREATE DATABASE IF NOT EXISTS trip_dispatch_db;

USE trip_dispatch_db;

CREATE TABLE trips (
    id CHAR(36) NOT NULL,

    pickup_location  VARCHAR(255) NOT NULL,
    pickup_lat  DECIMAL(9,6) NULL,
    pickup_lng  DECIMAL(9,6) NULL,
    dropoff_location VARCHAR(255) NOT NULL,
    dropoff_lat DECIMAL(9,6) NULL,
    dropoff_lng DECIMAL(9,6) NULL,

    required_capacity VARCHAR(50) NOT NULL,
    created_by_dispatcher_id CHAR(36) NULL,

    status VARCHAR(50) NOT NULL DEFAULT 'Requested'
        CHECK (status IN ('Requested', 'Assigned', 'In-Progress', 'Completed', 'Cancelled')),

    assigned_vehicle_id CHAR(36) NULL,
    assigned_driver_id CHAR(36) NULL,
    rejected_by_driver_id CHAR(36) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (id),

    KEY idx_trips_status (status),
    KEY idx_trips_assigned_vehicle_id (assigned_vehicle_id),
    KEY idx_trips_assigned_driver_id (assigned_driver_id),
    KEY idx_trips_rejected_by_driver_id (rejected_by_driver_id)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS driver_account_guards (
    driver_id CHAR(36) NOT NULL PRIMARY KEY,
    assignment_blocked BOOLEAN NOT NULL DEFAULT FALSE,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
