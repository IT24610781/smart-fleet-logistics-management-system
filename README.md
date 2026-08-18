# Smart Fleet & Logistics Management System

A microservice-based web application designed to help organizations manage fleet operations, vehicles, drivers, trips, dispatching, vehicle tracking, telemetry, and operational reporting through a centralized platform.

## Overview

The Smart Fleet & Logistics Management System provides a centralized platform for managing fleet and logistics operations.

The system is designed using a microservice-based architecture with a React frontend and ASP.NET backend services. MySQL is used for persistent data storage, while Apache Kafka is planned for vehicle telemetry and event-driven communication.

## Key Features

- User authentication and role-based access control
- User and driver management
- Fleet and vehicle management
- Vehicle registration and maintenance information
- Trip and dispatch management
- Intelligent vehicle assignment
- Vehicle tracking and telemetry
- Operational dashboards
- Dynamic operational reports
- Notification and alert functionality

## Architecture

The system consists of a React frontend and independently deployable backend services.

### Backend Services

- **Auth & User Service** – authentication, authorization, and user management
- **Fleet & Vehicle Service** – vehicle and fleet management
- **Dispatch & Trip Service** – dispatch, trip management, and operational reporting
- **Tracking & Telemetry Service** – vehicle location and telemetry processing

Apache Kafka is planned for event-driven vehicle telemetry communication.

## Technology Stack

| Layer | Technology |
|---|---|
| Frontend | React |
| Backend | ASP.NET Web API |
| Database | MySQL |
| Data Access | ADO.NET |
| Messaging | Apache Kafka |
| Testing | Unit Testing, Integration Testing, Selenium, JMeter |
| CI/CD | GitHub Actions |
| Containerization | Docker |
| Cloud Platform | Microsoft Azure |
| Version Control | Git & GitHub |

## Project Structure

```text
frontend/       React web application
services/       Backend microservices
database/       Database schemas, scripts, and documentation
tests/          Testing resources
docs/           Project documentation
deployment/     Deployment and containerization resources
.github/        GitHub Actions workflows
