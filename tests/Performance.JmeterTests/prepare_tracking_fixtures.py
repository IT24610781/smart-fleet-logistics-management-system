"""Create fresh local QA fixtures for TrackingPerformanceTest.jmx (Python stdlib only).

Builds N In-Progress trips, each with its own Driver and Vehicle (a Driver can only hold
one Assigned/In-Progress trip), so the fleet map and the location-write tests have real
data. Same conventions as prepare_trip_fixtures.py: no Admin password or JWT is saved.
"""

import argparse
import json
import os
from pathlib import Path
import secrets
import urllib.error
import urllib.request
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--drivers", type=int, default=10,
                        help="number of In-Progress trips (one Driver + Vehicle each); keep >= qa.threads")
    args = parser.parse_args()
    if args.drivers < 1:
        parser.error("--drivers must be positive")
    admin_password = os.environ.get("QA_ADMIN_PASSWORD")
    if not admin_password:
        parser.error("Set QA_ADMIN_PASSWORD before creating fixtures")

    auth_url = os.environ.get("QA_AUTH_URL", "http://localhost:5100").rstrip("/")
    fleet_url = os.environ.get("QA_FLEET_URL", "http://localhost:5041").rstrip("/")
    trip_url = os.environ.get("QA_TRIP_URL", "http://localhost:5051").rstrip("/")
    password = "QaLoad!" + secrets.token_hex(16)
    capacity = "65432 kg"  # distinct from the Trip and Selenium fixtures

    def request(base, path, body=None, token=None):
        headers = {"Content-Type": "application/json"}
        if token:
            headers["Authorization"] = "Bearer " + token
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(base + path, data=data, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=30) as response:
                text = response.read()
                return json.loads(text) if text else {}
        except urllib.error.HTTPError as error:
            raise SystemExit(f"{path} -> HTTP {error.code}: {error.read().decode(errors='replace')}")

    def user(name):
        return dict(username=name, email=name + "@example.com", password=password,
                    firstName="QA", lastName="Track", contactNumber="0771234567")

    def login(name, secret):
        return request(auth_url, "/api/auth/login", dict(username=name, password=secret))["token"]

    admin = login(os.environ.get("QA_ADMIN_USERNAME", "admin"), admin_password)
    dispatcher = "qatl" + uuid.uuid4().hex[:10]
    request(auth_url, "/api/auth/register/dispatcher", user(dispatcher))
    dispatcher_token = login(dispatcher, password)

    drivers = []
    for i in range(args.drivers):
        name = "qatd" + uuid.uuid4().hex[:10]
        driver = request(auth_url, "/api/users/drivers", user(name), admin)
        vehicle = request(fleet_url, "/api/vehicles",
                          dict(registrationNo="QP" + uuid.uuid4().hex[:12], type="Van", capacity=capacity), admin)
        trip = request(trip_url, "/api/trips",
                       dict(pickupLocation="QA track load " + uuid.uuid4().hex[:8],
                            dropoffLocation="QA track load destination", requiredCapacity=capacity),
                       dispatcher_token)
        request(trip_url, f"/api/trips/{trip['id']}/assign",
                dict(vehicleId=vehicle["id"], driverId=driver["id"]), dispatcher_token)
        request(trip_url, f"/api/trips/{trip['id']}/respond", dict(decision="accept"), login(name, password))
        drivers.append(dict(username=name, driverId=driver["id"], vehicleId=vehicle["id"], tripId=trip["id"]))
        print(f"Prepared In-Progress trip {i + 1}/{args.drivers}", flush=True)

    fixtures = dict(dispatcher=dispatcher, password=password, authUrl=auth_url, drivers=drivers)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(fixtures, indent=2), encoding="utf-8")
    print(f"Fixture file: {args.output.resolve()}")
    print("Records stay in the target databases. Location history grows with every run; use fresh fixtures per run.")


if __name__ == "__main__":
    main()
