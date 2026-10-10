param([string]$ResourceGroup = 'csp-project')

$ErrorActionPreference = 'Stop'
$run = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$auth = 'https://smartfleet-auth-demo.icystone-b3019ce0.malaysiawest.azurecontainerapps.io'
$fleet = 'https://smartfleet-fleet-demo.icystone-b3019ce0.malaysiawest.azurecontainerapps.io'
$trip = 'https://smartfleet-trip-demo.icystone-b3019ce0.malaysiawest.azurecontainerapps.io'
$tracking = 'https://smartfleet-tracking-demo.icystone-b3019ce0.malaysiawest.azurecontainerapps.io'

function CallApi($method, $url, $body, $token) {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(40)
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($method), $url)
    if ($token) { $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token) }
    if ($null -ne $body) {
        $request.Content = [System.Net.Http.StringContent]::new(($body | ConvertTo-Json -Depth 8 -Compress), [Text.Encoding]::UTF8, 'application/json')
    }
    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if ([int]$response.StatusCode -lt 200 -or [int]$response.StatusCode -ge 300) {
            throw "API request failed with HTTP $([int]$response.StatusCode): $content"
        }
        if ([string]::IsNullOrWhiteSpace($content)) { return $null }
        return ($content | ConvertFrom-Json)
    } finally {
        $request.Dispose()
        $client.Dispose()
    }
}

function New-TestPassword {
    'Qa' + ([Guid]::NewGuid().ToString('N')) + '!7'
}

$adminSecret = az containerapp secret show -n smartfleet-auth-demo -g $ResourceGroup --secret-name bootstrap-admin-password -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($adminSecret.value)) { throw 'Could not retrieve the Admin secret.' }
$adminLogin = CallApi Post "$auth/api/auth/login" @{ username = 'admin'; password = $adminSecret.value } $null
$adminSecret = $null
Write-Output 'Admin login: OK'

$dispatcherName = "qd_$run"
$dispatcherPassword = New-TestPassword
$dispatcher = CallApi Post "$auth/api/auth/register/dispatcher" @{
    username = $dispatcherName
    email = "$dispatcherName@example.invalid"
    password = $dispatcherPassword
    firstName = 'QA'
    lastName = 'Dispatcher'
    contactNumber = '0771234567'
} $null
$dispatcherLogin = CallApi Post "$auth/api/auth/login" @{ username = $dispatcherName; password = $dispatcherPassword } $null
$dispatcherPassword = $null
Write-Output "Dispatcher created: $($dispatcher.id)"

$driverName = "qr_$run"
$driverPassword = New-TestPassword
$driver = CallApi Post "$auth/api/users/drivers" @{
    username = $driverName
    email = "$driverName@example.invalid"
    password = $driverPassword
    firstName = 'QA'
    lastName = 'Driver'
    contactNumber = '0771234568'
} $adminLogin.token
$driverLogin = CallApi Post "$auth/api/auth/login" @{ username = $driverName; password = $driverPassword } $null
$driverPassword = $null
Write-Output "Driver created: $($driver.id)"

$vehicle = CallApi Post "$fleet/api/vehicles" @{
    registrationNo = "QA-$run"
    type = 'Van'
    capacity = '10 units'
} $adminLogin.token
Write-Output "Vehicle created: $($vehicle.id)"

$newTrip = CallApi Post "$trip/api/trips" @{
    pickupLocation = 'QA pickup'
    dropoffLocation = 'QA dropoff'
    requiredCapacity = '1 unit'
} $dispatcherLogin.token
Write-Output "Trip created: $($newTrip.id)"

$assigned = CallApi Post "$trip/api/trips/$($newTrip.id)/assign" @{
    vehicleId = $vehicle.id
    driverId = $driver.id
} $dispatcherLogin.token
Write-Output "Trip assigned: $($assigned.status)"

$accepted = CallApi Post "$trip/api/trips/$($newTrip.id)/respond" @{ decision = 'accept' } $driverLogin.token
Write-Output "Driver response: $($accepted.status)"

$location = CallApi Post "$tracking/api/tracking/locations" @{
    tripId = $newTrip.id
    latitude = 6.927079
    longitude = 79.861244
    observedAt = [DateTimeOffset]::UtcNow.ToString('o')
} $driverLogin.token
Write-Output "Location submitted: $($location.id)"

$history = CallApi Get "$tracking/api/tracking/trips/$($newTrip.id)/locations" $null $dispatcherLogin.token
Write-Output "Location history entries: $(@($history).Count)"

$record = [ordered]@{
    runUtc = [DateTimeOffset]::UtcNow.ToString('o')
    dispatcherId = $dispatcher.id
    driverId = $driver.id
    vehicleId = $vehicle.id
    tripId = $newTrip.id
    tripStatus = $accepted.status
    locationId = $location.id
    historyCount = @($history).Count
}
$record | ConvertTo-Json | Set-Content -Path 'outputs/azure-e2e.json'
