$ErrorActionPreference = 'Stop'
$toolRoot = Split-Path -Parent $PSScriptRoot
$backendPath = Join-Path $toolRoot 'Backend_Tool'
$backendProject = Join-Path $backendPath 'Backend_Tool.csproj'

$portReady = Test-NetConnection -ComputerName '127.0.0.1' -Port 5225 -InformationLevel Quiet
$running = Get-CimInstance Win32_Process -Filter "Name = 'Backend_Tool.exe'" -ErrorAction SilentlyContinue
if (-not $portReady -and -not $running) {
  Start-Process -FilePath 'dotnet' -ArgumentList 'run', '--project', $backendProject, '--no-launch-profile', '--', '--urls', 'http://127.0.0.1:5225' -WorkingDirectory $backendPath -WindowStyle Hidden
}

$ready = $false
for ($attempt = 0; $attempt -lt 30; $attempt++) {
  if (Test-NetConnection -ComputerName '127.0.0.1' -Port 5225 -InformationLevel Quiet) { $ready = $true; break }
  Start-Sleep -Seconds 1
}
if (-not $ready) { throw 'Backend did not start on http://127.0.0.1:5225.' }

pnpm exec ng serve
