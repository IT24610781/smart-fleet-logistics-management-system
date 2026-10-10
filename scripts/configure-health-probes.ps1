param([string]$ResourceGroup = 'csp-project')

$ErrorActionPreference = 'Stop'
$apps = @('smartfleet-auth-demo', 'smartfleet-fleet-demo', 'smartfleet-trip-demo', 'smartfleet-tracking-demo')
$probes = @(
    @{ type = 'Startup'; httpGet = @{ path = '/health/live'; port = 8080 }; initialDelaySeconds = 2; periodSeconds = 5; timeoutSeconds = 2; failureThreshold = 24 },
    @{ type = 'Liveness'; httpGet = @{ path = '/health/live'; port = 8080 }; initialDelaySeconds = 5; periodSeconds = 10; timeoutSeconds = 2; failureThreshold = 3 },
    @{ type = 'Readiness'; httpGet = @{ path = '/health/ready'; port = 8080 }; initialDelaySeconds = 5; periodSeconds = 10; timeoutSeconds = 5; failureThreshold = 6 }
)

foreach ($app in $apps) {
    $readyUrl = "https://$app.icystone-b3019ce0.malaysiawest.azurecontainerapps.io/health/ready"
    $response = Invoke-WebRequest -Uri $readyUrl -TimeoutSec 15 -SkipHttpErrorCheck
    if ($response.StatusCode -ne 200) { throw "$app readiness endpoint returned HTTP $($response.StatusCode); stopping before changing probes" }

    $current = az containerapp show -n $app -g $ResourceGroup -o json | ConvertFrom-Json -AsHashtable
    if ($LASTEXITCODE -ne 0) { throw "Could not read $app" }
    $template = $current.properties.template
    if ($template.containers.Count -ne 1) { throw "$app has unexpected container count" }
    $existing = @($template.containers[0].probes)
    if ($existing.Count -eq 3 -and
        @($existing | Where-Object { $_.type -eq 'Startup' -and $_.httpGet.path -eq '/health/live' }).Count -eq 1 -and
        @($existing | Where-Object { $_.type -eq 'Liveness' -and $_.httpGet.path -eq '/health/live' }).Count -eq 1 -and
        @($existing | Where-Object { $_.type -eq 'Readiness' -and $_.httpGet.path -eq '/health/ready' }).Count -eq 1) {
        Write-Output "$app already has the expected HTTP probes."
        continue
    }
    foreach ($key in @($template.Keys)) {
        if ($null -eq $template[$key]) { [void]$template.Remove($key) }
    }
    foreach ($key in @($template.scale.Keys)) {
        if ($null -eq $template.scale[$key]) { [void]$template.scale.Remove($key) }
    }
    [void]$template.containers[0].resources.Remove('ephemeralStorage')
    [void]$template.containers[0].Remove('imageType')
    $template.containers[0].probes = $probes
    $body = @{ properties = @{ template = $template } } | ConvertTo-Json -Depth 30
    $path = Join-Path $env:TEMP "smartfleet-probes-$app.json"
    try {
        [IO.File]::WriteAllText($path, $body)
        $uri = "https://management.azure.com$($current.id)?api-version=2025-07-01"
        az rest --method patch --uri $uri --body "@$path" --query 'properties.latestRevisionName' -o tsv
        if ($LASTEXITCODE -ne 0) { throw "Probe update failed for $app" }
    }
    finally { Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue }

    $saved = $null
    for ($attempt = 0; $attempt -lt 12; $attempt++) {
        $saved = @(az containerapp show -n $app -g $ResourceGroup --query 'properties.template.containers[0].probes' -o json | ConvertFrom-Json)
        if ($LASTEXITCODE -eq 0 -and $saved.Count -eq 3) { break }
        Start-Sleep -Seconds 5
    }
    if ($saved.Count -ne 3) { throw "Probe verification failed for $app" }
    $revision = az containerapp show -n $app -g $ResourceGroup --query 'properties.latestRevisionName' -o tsv
    $running = $false
    for ($attempt = 0; $attempt -lt 24; $attempt++) {
        $state = az containerapp revision show -n $app -g $ResourceGroup --revision $revision --query 'properties.runningState' -o tsv
        if ($state -like 'Running*') { $running = $true; break }
        if ($state -eq 'Failed') { break }
        Start-Sleep -Seconds 5
    }
    if (-not $running) { throw "$app revision $revision did not become healthy: $state" }
    Write-Output "$app revision $revision has HTTP startup, liveness, and readiness probes."
}
