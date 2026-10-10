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
    $template.containers[0].resources.Remove('ephemeralStorage')
    $template.containers[0].Remove('imageType')
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

    $saved = az containerapp show -n $app -g $ResourceGroup --query 'properties.template.containers[0].probes' -o json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $saved.Count -ne 3) { throw "Probe verification failed for $app" }
    Write-Output "$app has HTTP startup, liveness, and readiness probes."
}
