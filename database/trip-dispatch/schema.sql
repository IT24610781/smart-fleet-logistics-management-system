CREATE DATABASE IF NOT EXISTS trip_dispatch_db;

USE trip_dispatch_db;

CREATE TABLE trips (
    id CHAR(36) NOT NULL,

    pickup_location  VARCHAR(255) NOT NULL,
    dropoff_location VARCHAR(255) NOT NULL,

    required_capacity VARCHAR(50) NOT NULL,

    status VARCHAR(50) NOT NULL DEFAULT 'Requested'
        CHECK (status IN ('Requested', 'Assigned', 'In-Progress', 'Completed', 'Cancelled')),


    assigned_vehicle_id CHAR(36) NULL,
    assigned_driver_id CHAR(36) NULL,

    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (id),

    KEY idx_trips_status (status),
    KEY idx_trips_assigned_vehicle_id (assigned_vehicle_id),
    KEY idx_trips_assigned_driver_id (assigned_driver_id)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4;