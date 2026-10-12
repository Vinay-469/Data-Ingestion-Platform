$runtimeRoot = $env:CODEX_RUNTIME_ROOT
if ([string]::IsNullOrWhiteSpace($runtimeRoot)) {
  $runtimeRoot = Join-Path $PSScriptRoot '.runtime'
}
$nodeBin = Join-Path $runtimeRoot 'node\bin'
$pnpm = Join-Path $runtimeRoot 'bin\fallback\pnpm.cmd'

if (-not (Test-Path -LiteralPath $nodeBin)) {
  throw "Bundled Node.js runtime was not found at $nodeBin"
}

$env:Path = "$nodeBin;$runtimeRoot\bin\fallback;$env:Path"

Write-Host "Using Node.js: $(& (Join-Path $nodeBin 'node.exe') --version)"
& $pnpm install
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $pnpm start
