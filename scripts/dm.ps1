# DM3 Management Script (Windows PowerShell)
# Usage: powershell -File .\scripts\dm.ps1 <command>
# Commands: start, stop, reset, seed, e2e, status, logs
# A fresh stand is reset, then seed, then e2e: e2e does not seed, it only recreates the API.

param(
    [Parameter(Position=0)]
    [ValidateSet('start', 'stop', 'reset', 'seed', 'e2e', 'status', 'logs', 'help')]
    [string]$Command = 'help',

    [Parameter(Position=1)]
    [string]$Service = ''
)

$ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$DockerDir = Join-Path $ProjectRoot "docker"

# Find docker executable
$script:DockerPath = "docker"
if (-not (Get-Command "docker" -ErrorAction SilentlyContinue)) {
    $dockerDesktopPath = "C:\Program Files\Docker\Docker\resources\bin\docker.exe"
    if (Test-Path $dockerDesktopPath) {
        $script:DockerPath = $dockerDesktopPath
    } else {
        Write-Host "Docker not found. Install Docker Desktop or add docker to PATH." -ForegroundColor Red
        exit 1
    }
}

# Check Docker daemon is running
$null = & $script:DockerPath info 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker daemon not running. Start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Each command lives in a part under scripts/dm that defines functions and the values they
# share. The checks above that stop the script stay here: exit at the top of a dot-sourced
# file ends that file, not this one.
. (Join-Path $PSScriptRoot "dm\Environment.ps1")
. (Join-Path $PSScriptRoot "dm\Steps.ps1")
. (Join-Path $PSScriptRoot "dm\Services.ps1")
. (Join-Path $PSScriptRoot "dm\Seed.ps1")
. (Join-Path $PSScriptRoot "dm\EndToEnd.ps1")
. (Join-Path $PSScriptRoot "dm\Status.ps1")

function Show-Help {
    $addressLines = (Get-Addresses).GetEnumerator() | ForEach-Object { "  {0,-11}{1}" -f $_.Key, $_.Value }
    Write-Host @"
DM3 Management Script

Usage: powershell -File .\scripts\dm.ps1 <command> [service]

Commands:
  start     Start all Docker services (infrastructure + API)
  stop      Stop all Docker services
  reset     Stop, clear databases, and restart
  seed      Run seed scripts (requires running API)
  e2e       Recreate the API the way the e2e suite needs it: rate limiting off
            (it does not seed: a fresh stand is reset, then seed, then e2e)
  status    Show status of all services, docker and host processes alike
  logs      Show logs (optionally for specific service)
  help      Show this help

Examples:
  powershell -File .\scripts\dm.ps1 start          # Start everything
  powershell -File .\scripts\dm.ps1 stop           # Stop everything
  powershell -File .\scripts\dm.ps1 reset          # Fresh start with clean DB
  powershell -File .\scripts\dm.ps1 seed           # Populate test data
  powershell -File .\scripts\dm.ps1 e2e            # Stand for the e2e suite and the pre-push hook
  powershell -File .\scripts\dm.ps1 logs dm-api    # Show API logs
  powershell -File .\scripts\dm.ps1 status         # Check what's running

Docker Compose: $DockerDir\docker-compose.yml

Addresses (scripts\dm\addresses.txt, the frontend runs separately with npm):
$($addressLines -join "`n")
"@
}

# Main
# Every command but help gets the same preparation, the read-only ones too: compose
# interpolates docker/.env before it will so much as list containers.
if ($Command -ne 'help') { Initialize-Environment }

try {
    switch ($Command) {
        'start'  { Start-Services }
        'stop'   { Stop-Services }
        'reset'  { Reset-Services }
        'seed'   { Invoke-Seed }
        'e2e'    { Start-EndToEndStand }
        'status' { Show-Status }
        'logs'   { Show-Logs }
        'help'   { Show-Help }
    }
} finally {
    # Ctrl+C skips the step line that would show the cursor again.
    try { [Console]::CursorVisible = $true } catch {}
}
