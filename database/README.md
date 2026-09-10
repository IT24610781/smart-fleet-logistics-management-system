# Database

This directory contains database-related documentation and scripts for the Smart Fleet & Logistics Management System.

## Database Technology

MySQL

## Data Ownership

Each backend service is responsible for the data belonging to its own functional domain.

## Repository Policy

The actual database instance and database credentials are not stored in this repository.

Only database-related source files such as schema definitions, migrations, documentation, and safe sample data should be committed.

## Planned Database Domains

- Authentication and user data
- Fleet and vehicle data
- Dispatch and trip data
- Tracking and telemetry data
