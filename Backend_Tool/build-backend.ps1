$ErrorActionPreference = 'Stop'

$backendRoot = $PSScriptRoot
$project = Join-Path $backendRoot 'Backend_Tool.csproj'
$dll = Join-Path $backendRoot 'bin\Debug\net8.0\Backend_Tool.dll'
$port = 5225

# Stop only the process currently listening on this application's API port.
$listener = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) {
    $process = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue
    if ($process -and $process.ProcessName -eq 'dotnet') {
        Stop-Process -Id $process.Id -Force
        Start-Sleep -Seconds 1
    }
}

dotnet build $project --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed. The API was not restarted.' }

Start-Process -FilePath 'C:\Program Files\dotnet\dotnet.exe' `
    -ArgumentList @($dll, '--urls', "http://127.0.0.1:$port") `
    -WorkingDirectory (Split-Path $dll) -WindowStyle Hidden

$ready = $null
for ($attempt = 0; $attempt -lt 15 -and -not $ready; $attempt++) {
    Start-Sleep -Seconds 1
    $ready = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
}
if (-not $ready) { throw "Backend did not start on port $port." }
Write-Host "Backend built and started on http://127.0.0.1:$port"
