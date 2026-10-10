param(
    [string]$ResourceGroup = 'csp-project',
    [string]$ServerOverride = ''
)

$ErrorActionPreference = 'Stop'
$previousMysqlPassword = [Environment]::GetEnvironmentVariable('MYSQL_PWD', 'Process')

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI is required.'
}
if (-not (Get-Command mysql -ErrorAction SilentlyContinue)) {
    throw 'The MySQL command-line client is required.'
}

$checks = @(
    @{ App = 'smartfleet-auth-demo'; Secret = 'auth-db'; Database = 'fleet_identity_db'; Tables = @('users', 'password_challenges', 'password_security') },
    @{ App = 'smartfleet-fleet-demo'; Secret = 'fleet-db'; Database = 'fleet_vehicle_db'; Tables = @('vehicles') },
    @{ App = 'smartfleet-trip-demo'; Secret = 'trip-db'; Database = 'trip_dispatch_db'; Tables = @('trips', 'driver_account_guards'); Columns = @(@{ Table = 'trips'; Name = 'created_by_dispatcher_id' }) },
    @{ App = 'smartfleet-tracking-demo'; Secret = 'tracking-db'; Database = 'tracking_telemetry_db'; Tables = @('trip_location_updates') }
)

foreach ($check in $checks) {
    $secret = $null
    $builder = $null
    try {
        $secretJson = az containerapp secret show -n $check.App -g $ResourceGroup --secret-name $check.Secret -o json
        if ($LASTEXITCODE -ne 0) { throw "Could not read secret for $($check.App)." }
        $secret = $secretJson | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace([string]$secret.value)) {
            throw "The database secret for $($check.App) is empty."
        }

        $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
        # PowerShell's assignment to this property can create a dictionary entry
        # named ConnectionString instead of invoking the .NET property setter.
        [System.Data.Common.DbConnectionStringBuilder].GetProperty('ConnectionString').SetValue($builder, [string]$secret.value)
        $hostName = if ([string]::IsNullOrWhiteSpace($ServerOverride)) {
            [string]$builder['Server']
        } else {
            $ServerOverride
        }
        $database = [string]$builder['Database']
        $userName = [string]$builder['User ID']
        $password = [string]$builder['Password']
        $port = if ($builder.ContainsKey('Port')) { [string]$builder['Port'] } else { '3306' }
        if ($database -ne $check.Database -or [string]::IsNullOrWhiteSpace($hostName) -or
            [string]::IsNullOrWhiteSpace($userName) -or [string]::IsNullOrWhiteSpace($password)) {
            throw "Incomplete or unexpected database connection for $($check.App)."
        }

        $env:MYSQL_PWD = $password
        $tables = @(mysql --host=$hostName --port=$port --user=$userName --ssl-mode=REQUIRED --database=$database --batch --skip-column-names --execute='SHOW TABLES')
        if ($LASTEXITCODE -ne 0) { throw "Database query failed for $($check.App)." }
        $missing = @($check.Tables | Where-Object { $_ -notin $tables })
        if ($missing.Count -gt 0) { throw "Missing tables in ${database}: $($missing -join ', ')." }
        foreach ($column in $check.Columns) {
            $sql = "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '$($column.Table)' AND COLUMN_NAME = '$($column.Name)'"
            $found = @(mysql --host=$hostName --port=$port --user=$userName --ssl-mode=REQUIRED --database=$database --batch --skip-column-names --execute=$sql)
            if ($LASTEXITCODE -ne 0 -or $column.Name -notin $found) {
                throw "Missing migration column $($column.Table).$($column.Name) in $database."
            }
        }
        Write-Output "$($check.App): $database reachable over TLS on $hostName; expected tables present."
    }
    finally {
        if ($null -eq $previousMysqlPassword) {
            Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
        }
        else {
            $env:MYSQL_PWD = $previousMysqlPassword
        }
        Remove-Variable secretJson, secret, builder, hostName, database, userName, password, port, tables, missing, sql, found -ErrorAction SilentlyContinue
    }
}
