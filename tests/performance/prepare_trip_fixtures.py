"""Create fresh local QA fixtures for TripDispatchPerformanceTest.jmx (Python stdlib)."""

import argparse
import json
import os
from pathlib import Path
import secrets
import urllib.request
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--threads", type=int, default=10)
    parser.add_argument("--loops", type=int, default=5)
    args = parser.parse_args()
    if args.threads < 1 or args.loops < 1:
        parser.error("threads and loops must be positive")
    admin_password = os.environ.get("QA_ADMIN_PASSWORD")
    if not admin_password:
        parser.error("Set QA_ADMIN_PASSWORD before creating fixtures")

    auth_url = os.environ.get("QA_AUTH_URL", "http://localhost:5100").rstrip("/")
    fleet_url = os.environ.get("QA_FLEET_URL", "http://localhost:5041").rstrip("/")
    trip_url = os.environ.get("QA_TRIP_URL", "http://localhost:5051").rstrip("/")
    password = "QaLoad!" + secrets.token_hex(16)
    capacity = "54321 kg"  # Separate from passenger-based Selenium fixtures.

    def request(base, path, body=None, token=None):
        headers = {"Content-Type": "application/json"}
        if token:
            headers["Authorization"] = "Bearer " + token
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(base + path, data=data, headers=headers)
        with urllib.request.urlopen(req, timeout=30) as response:
            return json.loads(response.read())

    def user(name):
        return dict(username=name, email=name + "@example.com", password=password,
                    firstName="QA", lastName="Load", contactNumber="0771234567")

    def login(name, secret):
        return request(auth_url, "/api/auth/login", dict(username=name, password=secret))["token"]

    admin = login(os.environ.get("QA_ADMIN_USERNAME", "admin"), admin_password)
    dispatcher = "qal" + uuid.uuid4().hex[:10]
    request(auth_url, "/api/auth/register/dispatcher", user(dispatcher))
    dispatcher_token = login(dispatcher, password)
    driver_name = "qald" + uuid.uuid4().hex[:10]
    driver = request(auth_url, "/api/users/drivers", user(driver_name), admin)

    def vehicle():
        return request(fleet_url, "/api/vehicles",
                       dict(registrationNo="QL" + uuid.uuid4().hex[:12],
                            type="Van", capacity=capacity), admin)

    def trip():
        return request(trip_url, "/api/trips",
                       dict(pickupLocation="QA load " + uuid.uuid4().hex[:8],
                            dropoffLocation="QA load destination", requiredCapacity=capacity),
                       dispatcher_token)

    # Only the generated fixture password is saved; no Admin password or JWT is exported.
    fixtures = dict(dispatcher=dispatcher, driver=driver_name, driverId=driver["id"],
                    password=password, authUrl=auth_url, fleetUrl=fleet_url, tripUrl=trip_url,
                    requiredCapacity=capacity, threads=args.threads, loops=args.loops,
                    suggest=trip()["id"])
    samples = args.threads * args.loops
    for operation in ("assign", "respond", "lifecycle", "mixed"):
        entries = []
        for _ in range(samples):
            fleet_vehicle = vehicle()
            entry = dict(vehicleId=fleet_vehicle["id"], driverId=driver["id"])
            if operation != "mixed":
                created_trip = trip()
                entry["tripId"] = created_trip["id"]
                if operation in ("respond", "lifecycle"):
                    request(trip_url, "/api/trips/" + created_trip["id"] + "/assign",
                            dict(vehicleId=entry["vehicleId"], driverId=entry["driverId"]),
                            dispatcher_token)
            entries.append(entry)
        fixtures[operation] = entries
        print(f"Prepared {operation}: {samples} fixtures", flush=True)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(fixtures, indent=2), encoding="utf-8")
    print(f"Fixture file: {args.output.resolve()}")
    print("Generated records remain in the target databases; each JMeter run needs fresh fixtures.")


if __name__ == "__main__":
    main()
