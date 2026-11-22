# test_bluetooth.ps1
$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  PowerShell Bluetooth Enumeration & Tests   " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 1. Load WinRT Types
Add-Type -AssemblyName System.Runtime.WindowsRuntime

$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | ? {
    $_.Name -eq 'AsTask' -and
    $_.GetParameters().Count -eq 1 -and
    $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
})[0]

function Await($WinRtTask, $ResultType) {
    $netTask = $asTaskGeneric.MakeGenericMethod($ResultType).Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    return $netTask.Result
}

[Windows.Devices.Bluetooth.BluetoothAdapter, Windows.Devices.Bluetooth, ContentType = WindowsRuntime] | Out-Null
[Windows.Devices.Bluetooth.BluetoothDevice, Windows.Devices.Bluetooth, ContentType = WindowsRuntime] | Out-Null
[Windows.Devices.Enumeration.DeviceInformation, Windows.Devices.Enumeration, ContentType = WindowsRuntime] | Out-Null

# 2. Query Default Bluetooth Adapter
Write-Host "`n1. Querying BluetoothAdapter.GetDefaultAsync()..." -ForegroundColor Yellow
$adapterOp = [Windows.Devices.Bluetooth.BluetoothAdapter]::GetDefaultAsync()
$adapter = Await $adapterOp ([Windows.Devices.Bluetooth.BluetoothAdapter])

if ($null -ne $adapter) {
    Write-Host "  [OK] Adapter DeviceId: $($adapter.DeviceId)" -ForegroundColor Green
    Write-Host "  [OK] Adapter BluetoothAddress: $([Convert]::ToString($adapter.BluetoothAddress, 16).ToUpper())" -ForegroundColor Green
    Write-Host "  [OK] IsClassicSupported: $($adapter.IsClassicSupported)" -ForegroundColor Green
    Write-Host "  [OK] IsLowEnergySupported: $($adapter.IsLowEnergySupported)" -ForegroundColor Green
    Write-Host "  [OK] IsPeripheralRoleSupported: $($adapter.IsPeripheralRoleSupported)" -ForegroundColor Green
    Write-Host "  [OK] IsCentralRoleSupported: $($adapter.IsCentralRoleSupported)" -ForegroundColor Green
} else {
    Write-Host "  [WARN] No default BluetoothAdapter found on host." -ForegroundColor Yellow
}

# 3. Query Paired Devices
Write-Host "`n2. Querying Paired Devices via DeviceInformation.FindAllAsync()..." -ForegroundColor Yellow
$selector = [Windows.Devices.Bluetooth.BluetoothDevice]::GetDeviceSelectorFromPairingState($true)
$reqProps = [string[]]@("System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected")
$findOp = [Windows.Devices.Enumeration.DeviceInformation]::FindAllAsync($selector, $reqProps)
$devices = Await $findOp ([Windows.Devices.Enumeration.DeviceInformationCollection])

Write-Host "  Paired Devices Found: $($devices.Count)" -ForegroundColor Green

foreach ($dev in $devices) {
    Write-Host "  -------------------------------------------------" -ForegroundColor Gray
    Write-Host "  Name: '$($dev.Name)'" -ForegroundColor White
    Write-Host "  ID: $($dev.Id)" -ForegroundColor Gray
    
    $isConn = $false
    try {
        $val = $dev.Properties["System.Devices.Aep.IsConnected"]
        if ($null -ne $val) { $isConn = [bool]$val }
    } catch {}
    Write-Host "  Connection Status: $(if ($isConn) { 'Connected' } else { 'Disconnected' })" -ForegroundColor $(if ($isConn) { 'Green' } else { 'Yellow' })
    
    try {
        $addr = $dev.Properties["System.Devices.Aep.DeviceAddress"]
        if ($null -ne $addr) {
            Write-Host "  MAC Address: $addr" -ForegroundColor Gray
        }
    } catch {}
}

# 4. Preferences Persistence Test
Write-Host "`n3. Testing Preferences Persistence (%LOCALAPPDATA%\BTController\widget_devices.json)..." -ForegroundColor Yellow
$localAppData = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)
$settingsDir = Join-Path $localAppData "BTController"
$settingsPath = Join-Path $settingsDir "widget_devices.json"

if (-not (Test-Path $settingsDir)) {
    New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
}

$sampleDeviceIds = @()
foreach ($dev in $devices) {
    $sampleDeviceIds += $dev.Id
}
if ($sampleDeviceIds.Count -eq 0) {
    $sampleDeviceIds = @("mock-dev-1", "mock-dev-2")
}

$jsonOut = $sampleDeviceIds | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText($settingsPath, $jsonOut)
Write-Host "  [OK] Saved pinned devices list to $settingsPath" -ForegroundColor Green

$readBack = [System.IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
Write-Host "  [OK] Reloaded $($readBack.Count) items from $($settingsPath):" -ForegroundColor Green
foreach ($item in $readBack) {
    Write-Host "    - $item" -ForegroundColor Gray
}

Write-Host "`n=============================================" -ForegroundColor Cyan
Write-Host "  All PowerShell Bluetooth Tests Completed!  " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
