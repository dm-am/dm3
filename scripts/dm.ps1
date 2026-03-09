# DM3 Management Script (Windows PowerShell)
# Usage: .\scripts\dm.ps1 <command>
# Commands: start, stop, reset, seed, status, logs

param(
    [Parameter(Position=0)]
    [ValidateSet('start', 'stop', 'reset', 'seed', 'status', 'logs', 'help')]
    [string]$Command = 'help',

    [Parameter(Position=1)]
    [string]$Service = ''
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$DockerDir = Join-Path $ProjectRoot "docker"

function Show-Help {
    Write-Host @"
DM3 Management Script

Usage: .\scripts\dm.ps1 <command> [service]

Commands:
  start     Start all Docker services (infrastructure + API)
  stop      Stop all Docker services
  reset     Stop, clear databases, and restart
  seed      Run seed scripts (requires running API)
  status    Show status of all services
  logs      Show logs (optionally for specific service)
  help      Show this help

Examples:
  .\scripts\dm.ps1 start          # Start everything
  .\scripts\dm.ps1 stop           # Stop everything
  .\scripts\dm.ps1 reset          # Fresh start with clean DB
  .\scripts\dm.ps1 seed           # Populate test data
  .\scripts\dm.ps1 logs dm-api    # Show API logs
  .\scripts\dm.ps1 status         # Check what's running

Environment:
  Docker Compose: $DockerDir\docker-compose.yml
  API: http://localhost:5000
  Frontend: http://localhost:5173 (run separately with npm)
"@
}

function Wait-ForHealthy {
    param([string]$Container, [int]$TimeoutSeconds = 120)

    $elapsed = 0
    $interval = 3

    while ($elapsed -lt $TimeoutSeconds) {
        $status = docker inspect --format='{{.State.Health.Status}}' $Container 2>$null
        if ($status -eq "healthy") {
            return $true
        }
        Write-Host "  Waiting for $Container... ($elapsed s)" -ForegroundColor Gray
        Start-Sleep -Seconds $interval
        $elapsed += $interval
    }
    return $false
}

function Start-Services {
    Write-Host "Starting DM3 services..." -ForegroundColor Cyan

    # Check .env file
    $envFile = Join-Path $DockerDir ".env"
    if (-not (Test-Path $envFile)) {
        Write-Host "Creating .env file from template..." -ForegroundColor Yellow
        Copy-Item (Join-Path $DockerDir ".env.example") $envFile
        Write-Host "Please edit docker/.env and set secure passwords!" -ForegroundColor Yellow
    }

    Push-Location $DockerDir
    try {
        # Build and start services
        Write-Host "Building containers..." -ForegroundColor Gray
        & docker compose build --quiet
        Write-Host "Starting containers..." -ForegroundColor Gray
        & docker compose up -d

        # Wait for RabbitMQ (slowest to start, ~20-30 sec)
        Write-Host "`nRabbitMQ starting (this takes ~20-30 seconds)..." -ForegroundColor Yellow
        if (-not (Wait-ForHealthy "dm-rmq" 180)) {
            Write-Host "Warning: RabbitMQ health check timeout" -ForegroundColor Red
        }

        # Ensure all containers are started (some may have failed on first pass due to dependencies)
        & docker compose up -d

        Write-Host "`nServices started!" -ForegroundColor Green
        Write-Host "API/Swagger: http://localhost:5000"
        Write-Host "MailHog: http://localhost:8025"
        Write-Host "MinIO: http://localhost:9001"
        Write-Host "RabbitMQ: http://localhost:15672"
        Write-Host "`nNote: Frontend runs separately - cd src/DM.Web.Client && npm run dev"
    } finally {
        Pop-Location
    }
}

function Stop-Services {
    Write-Host "Stopping DM3 services..." -ForegroundColor Cyan

    Push-Location $DockerDir
    try {
        docker compose down
        Write-Host "Services stopped." -ForegroundColor Green
    } finally {
        Pop-Location
    }
}

function Reset-Services {
    Write-Host "Resetting DM3 (stop, clear DB, restart)..." -ForegroundColor Cyan

    # Stop services
    Stop-Services

    # Clear volumes
    Write-Host "`nClearing database volumes..." -ForegroundColor Yellow
    Push-Location $DockerDir
    try {
        docker compose down -v
    } finally {
        Pop-Location
    }

    Write-Host "Database volumes cleared." -ForegroundColor Green

    # Restart
    Write-Host "`nRestarting services..." -ForegroundColor Cyan
    Start-Services
}

function Invoke-Seed {
    Write-Host "Seeding test data..." -ForegroundColor Cyan

    # Check if API is running
    $curlResult = curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5000/v1/boards" 2>$null
    if ($curlResult -ne "200") {
        Write-Host "Error: API is not available at http://localhost:5000 (status: $curlResult)" -ForegroundColor Red
        Write-Host "Start services first: .\scripts\dm.ps1 start"
        exit 1
    }

    # Call seed endpoint directly
    $response = curl.exe -s -X POST "http://localhost:5000/v1/moderation/seed" -H "Content-Type: application/json"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to call seed endpoint" -ForegroundColor Red
        exit 1
    }

    # Parse JSON response
    $result = $response | ConvertFrom-Json

    Write-Host ""
    Write-Host "Created: $($result.created)" -ForegroundColor Green
    Write-Host "Skipped: $($result.skipped) (already exist)" -ForegroundColor Yellow

    if ($result.createdLogins -and $result.createdLogins.Count -gt 0) {
        Write-Host ""
        Write-Host "Created users:" -ForegroundColor Green
        foreach ($login in $result.createdLogins) {
            Write-Host "  + $login"
        }
    }

    Write-Host ""
    Write-Host "Password: Test123!" -ForegroundColor Cyan
    Write-Host "All users are newbies (0 posts)"
}

function Show-Status {
    Write-Host "DM3 Service Status" -ForegroundColor Cyan
    Write-Host "==================`n"

    docker ps --format "table {{.Names}}`t{{.Status}}`t{{.Ports}}" --filter "name=dm-"

    Write-Host ""
}

function Show-Logs {
    if ($Service) {
        docker logs $Service --tail 100 -f
    } else {
        Push-Location $DockerDir
        try {
            docker compose logs --tail 50 -f
        } finally {
            Pop-Location
        }
    }
}

# Main
switch ($Command) {
    'start'  { Start-Services }
    'stop'   { Stop-Services }
    'reset'  { Reset-Services }
    'seed'   { Invoke-Seed }
    'status' { Show-Status }
    'logs'   { Show-Logs }
    'help'   { Show-Help }
    default  { Show-Help }
}
