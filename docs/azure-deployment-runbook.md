# Azure team demo deployment runbook

Last verified: 2026-10-10. Subscription: `69a878de-cd36-47b9-8805-4a6419e4f6c1`; resource group: `csp-project`; region: Malaysia West. This is a short-lived team demo. Never copy secret values from Azure into this file or a terminal transcript.

## Live resources

| Component | Azure resource | Public URL or ownership |
| --- | --- | --- |
| Auth API | `smartfleet-auth-demo` | `https://smartfleet-auth-demo.icystone-b3019ce0.malaysiawest.azurecontainerapps.io` |
| Fleet API | `smartfleet-fleet-demo` | `https://smartfleet-fleet-demo.icystone-b3019ce0.malaysiawest.azurecontainerapps.io` |
| Trip API | `smartfleet-trip-demo` | `https://smartfleet-trip-demo.icystone-b3019ce0.malaysiawest.azurecontainerapps.io` |
| Tracking API | `smartfleet-tracking-demo` | `https://smartfleet-tracking-demo.icystone-b3019ce0.malaysiawest.azurecontainerapps.io` |
| Frontend | `smartfleet-frontend-it24610781` (App Service) | `https://smartfleet-frontend-it24610781.azurewebsites.net` |
| Container Apps environment | `smartfleet-v2-env` | Workload profiles environment |
| Registry | `smartfleetacr` | `smartfleetacr-fxgpf2gudpaag4bb.azurecr.io` |
| Registry pull identity | `smartfleet-acr-pull` | User-assigned identity with `AcrPull` on the registry |
| MySQL | `smartfleet-mysql-demo` | Flexible Server, TLS required, seven-day backup retention |
| Event Hubs | `smartfleet-events-it24610781` | Standard tier, `vehicle.status.events` hub |
| Log Analytics | `workspacecspproject8bdc` | Container console logs and telemetry |

MySQL databases belong to exactly one service each: Auth uses `fleet_identity_db`, Fleet uses `fleet_vehicle_db`, Trip uses `trip_dispatch_db`, and Tracking uses `tracking_telemetry_db`. Application connection strings are Container App secrets, referenced by `ConnectionStrings__DefaultConnection`. The frontend build uses `frontend/.env.production` for all four Azure API URLs. The production browser should call those URLs, not `localhost`.

The Container Apps use the `smartfleet-acr-pull` managed identity for image pulls. GitHub Actions uses the `smartfleet-github-actions` federated identity. Its repository secrets are `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID`; no Azure password is required by the workflow. The workflow in `.github/workflows/build.yml` runs checks on pull requests and feature branches and deploys only after a successful push to `dev`. `main` does not deploy these shared apps. The four API images are tagged with the full Git commit SHA. The frontend is hosted on App Service because this student subscription did not permit the planned Static Web Apps resource.

## Deploy and verify

1. Merge a reviewed, green pull request into `dev`. GitHub Actions builds and tests all four APIs and the frontend, pushes the SHA-tagged images, updates each Container App, waits for a running revision, deploys the frontend, and checks `/login`.
2. Verify each app and its image/revision:

   ```powershell
   foreach ($app in 'smartfleet-auth-demo','smartfleet-fleet-demo','smartfleet-trip-demo','smartfleet-tracking-demo') {
       az containerapp show -n $app -g csp-project --query '{name:name,image:properties.template.containers[0].image,revision:properties.latestRevisionName}' -o json
       az containerapp revision list -n $app -g csp-project --query '[].{name:name,active:properties.active,running:properties.runningState,replicas:properties.replicas}' -o table
   }
   ```

3. Run `./scripts/test-azure-workflow.ps1`. It fetches the Admin password at runtime and creates temporary QA accounts and records. It tests login, vehicle creation, Trip assignment, Driver acceptance, a location update, and location history. It prints IDs only; do not paste any secret output. The run writes `outputs/azure-e2e.json` locally.
4. Check `https://smartfleet-frontend-it24610781.azurewebsites.net/login` and browser Network requests. Verify HTTPS API URLs and that Admin, Dispatcher, and Driver screens load. The four APIs should answer CORS preflight from the hosted frontend origin and reject an unrelated origin.
5. Confirm Fleet publishes to Event Hubs by inspecting the `IncomingMessages` metric for `vehicle.status.events`. If asynchronous delivery is missing, inspect Fleet console logs before retrying.
6. Run `./scripts/verify-azure-databases.ps1` from PowerShell with Azure CLI and the MySQL client installed. It reads each Container App's database secret at runtime, connects over TLS, and checks expected tables without printing credentials or changing schema. For an isolated restore, pass `-ServerOverride <restored-server>.mysql.database.azure.com` to validate the copy with the same service users.

On 2026-10-10, Fleet's Event Hubs path was checked end to end. An Admin created QA vehicle `478e16af-4b78-4ba9-bceb-c76969df7888` and changed its status to `Under Maintenance` through the Fleet API. A separate Kafka consumer read the matching `QA-EH-` status event from `vehicle.status.events` at partition 0, offset 7. The namespace `IncomingMessages` metric rose to 2 at 15:38 UTC (one direct diagnostic event and one Fleet status event). The QA vehicle was deleted after verification. Fleet logs also contain periodic `1/1 brokers are down` messages; this successful delivery proves the connection works, but that warning should be monitored for recurrence. The database verification script passed for all four service users and databases, including Trip's additive `created_by_dispatcher_id` column, on the same date.

The Tracking service needs `FleetService__BaseUrl`, `TripDispatchService__BaseUrl`, and `AuthService__BaseUrl` in its Container App environment. A code update that adds a required setting must be paired with an Azure configuration update before deploying the new image.

The Auth service uses Brevo for password verification email. `PasswordEmail__ApiKey` references the Container App secret `password-email-api-key`; `PasswordEmail__SenderEmail` is `it24610781@my.sliit.lk`. Do not store the API key in Git or paste it into deployment logs. On 2026-10-10, a single test through `POST /api/auth/password/forgot` to a temporary QA Driver account was recorded as `delivered` by Brevo. The QA account was then deactivated; no password was changed.

## Rollback

Before deploying, record each current image and revision. `outputs/rollback-before-6f92283.json` is a local example; do not assume it is still current. If a new revision fails, return the affected app to the last known-good image:

```powershell
az containerapp update -n <app-name> -g csp-project --image <known-good-full-image> -o none
az containerapp revision list -n <app-name> -g csp-project --query '[].{name:name,active:properties.active,running:properties.runningState,replicas:properties.replicas}' -o table
```

Then repeat the relevant API smoke test. Use backward-compatible database migrations so the previous image can still read the schema. In single-revision mode, updating the image creates a new revision; changing traffic alone will not repair a broken single active revision. Do not delete the previous image from ACR until rollback is no longer needed.

This procedure was exercised on Tracking on 2026-10-10: an intentionally missing image produced an unhealthy `ImagePullBackOff` revision, the previous healthy revision kept serving the endpoint, and restoring the full-SHA image produced a healthy replacement revision. The live workflow test passed again after recovery.

For database recovery, first review the MySQL Flexible Server's latest restorable time and seven-day retention. Restore into a **new** server at a chosen point in time, validate data there, and plan a controlled connection-string cutover. A restore drill creates billable resources, so schedule and budget it explicitly. On 2026-10-10, a point-in-time restore into `smartfleet-restore-drill-20261010` reached `Ready`; all four service database connections and expected tables passed the read-only verifier with `-ServerOverride`. Both temporary restore resources created during the drill were then deleted, and only production `smartfleet-mysql-demo` remained. No application cutover was performed.

## Monitoring and cost

- Azure Portal: `csp-project` > each Container App > Revisions and replicas for status; Monitoring > Logs for console history. `ContainerAppConsoleLogs_CL` contains app stack traces. If the real-time log stream says no replica is running, use historical logs.
- Event Hubs namespace > `vehicle.status.events` > Metrics > `IncomingMessages` for publication evidence.
- Azure budget `smartfleet-student-monthly`: USD 25 per month; actual-spend alerts at 80% and 100% to the signed-in Azure email. Alerts do not stop spending. Review Cost Management after new resources or tests.
- MySQL Flexible Server: TLS is required; backup retention is seven days. Check firewall/network access before diagnosing a client connection timeout as a password issue.

## Current limitations and cleanup

The old Express environment `smartfleet-demo-env`, old `martfleet-auth-demo` app, and the two `smart-fleet-vehicle-*` App Services were retired on 2026-10-10 after the team confirmed they were no longer needed. The two old Web Apps had zero requests in the preceding seven days, and the repository contained no references to the old resource names. The shared App Service plan remains for the current frontend. The new demo uses `smartfleet-v2-env` and the four `smartfleet-*-demo` apps above.

The scripted live workflow and hosted Admin, Dispatcher, and Driver browser login tests have passed. The reviewed deployment in PR #55 and its CI run deployed the Trip API's structured 503 handling for Fleet HTTP errors, timeouts, and invalid vehicle lists; its fault cases passed in automated tests, but a live dependency fault-injection test has not been run. A restore drill verified the backup copy, but an actual production connection-string cutover has not been rehearsed. The known Driver rejection status mismatch remains unresolved: the backend returns `Requested` with the assignment cleared while the Sprint 3 workbook expects `Rejected`. The team must confirm the intended requirement before changing either behavior or the workbook.
