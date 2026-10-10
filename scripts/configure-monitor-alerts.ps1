param([string]$ResourceGroup = 'csp-project')

$ErrorActionPreference = 'Stop'
$workspace = az monitor log-analytics workspace show -g $ResourceGroup -n workspacecspproject8bdc --query id -o tsv
$action = az monitor action-group show -g $ResourceGroup -n smartfleet-ops-email --query id -o tsv
$subscription = az account show --query id -o tsv
if ($LASTEXITCODE -ne 0 -or -not $workspace -or -not $action -or -not $subscription) { throw 'Azure resources unavailable' }

$query = "ContainerAppConsoleLogs_CL | where ContainerAppName_s startswith 'smartfleet-' | where Log_s has_any ('Unhandled exception', 'ERROR', 'Error') | summarize ErrorCount=count() by ContainerAppName_s | where ErrorCount >= 5"
$rule = @{
    location = 'malaysiawest'
    kind = 'LogAlert'
    properties = @{
        displayName = 'Smart Fleet repeated application errors'
        description = 'Five or more application errors from one Container App in 15 minutes'
        severity = 2
        enabled = $true
        evaluationFrequency = 'PT15M'
        windowSize = 'PT15M'
        scopes = @($workspace)
        criteria = @{ allOf = @(@{ query = $query; timeAggregation = 'Count'; operator = 'GreaterThan'; threshold = 0 }) }
        actions = @{ actionGroups = @($action) }
        autoMitigate = $true
    }
}
$path = Join-Path $env:TEMP 'smartfleet-error-alert.json'
try {
    [IO.File]::WriteAllText($path, ($rule | ConvertTo-Json -Depth 12))
    $uri = "https://management.azure.com/subscriptions/$subscription/resourceGroups/$ResourceGroup/providers/Microsoft.Insights/scheduledQueryRules/smartfleet-repeated-app-errors?api-version=2023-12-01"
    az rest --method put --uri $uri --body "@$path" -o json
    if ($LASTEXITCODE -ne 0) { throw 'Log alert creation failed' }
}
finally { Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue }
