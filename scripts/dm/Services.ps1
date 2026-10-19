# The start, stop and reset commands of scripts/dm.ps1, which dot-sources
# this file.

function Start-Infrastructure {
    # The list is declared before the step that reports it, so the failure line cannot carry
    # a hand-typed total.
    $infraServices = @(
        @{ Name = "dm-pg"; Label = "Postgres" },
        @{ Name = "dm-seaweedfs"; Label = "SeaweedFS" },
        @{ Name = "dm-storage"; Label = "Storage" },
        @{ Name = "dm-loki"; Label = "Loki" },
        @{ Name = "dm-rmq"; Label = "RabbitMQ" }
    )

    # alertmanager comes up with prometheus: started apart, prometheus evaluates its rules
    # into nothing.
    if (-not (Invoke-Step -Label "Starting" -Command $script:DockerPath -Arguments "compose up -d postgres rabbitmq storage seaweedfs mailpit jaeger loki prometheus alertmanager grafana")) {
        Write-Step -Failed -Label "Infrastructure" -Result "start failed"
        exit 1
    }

    if (-not (Wait-ServicesHealthy -Label "Infrastructure" -Services $infraServices -TimeoutSeconds 120)) {
        Write-Host "    Logs: powershell -File .\scripts\dm.ps1 logs" -ForegroundColor DarkGray
        exit 1
    }

    # The bucket and its accounts come from storage-start.sh on every start of seaweedfs.
}

function Start-Migration {
    & $script:DockerPath compose up -d migration 2>&1 | Out-Null
    $migrationOk = Wait-SimpleStep -Label "Migration" -TimeoutSeconds 30 -Check {
        $status = & $script:DockerPath inspect dm-migration --format='{{.State.Status}}' 2>$null
        if ($status -eq "exited") {
            $exitCode = & $script:DockerPath inspect dm-migration --format='{{.State.ExitCode}}' 2>$null
            return $exitCode -eq "0"
        }
        return $false
    }
    if (-not $migrationOk) { exit 1 }
}

function Start-Applications {
    $appServices = @(
        @{ Name = "dm-api"; Label = "API" },
        @{ Name = "dm-mail-worker"; Label = "Mail" },
        @{ Name = "dm-notification-worker"; Label = "Notify" }
    )

    & $script:DockerPath compose up -d dm-mail-worker dm-notification-worker dmapi 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Step -Failed -Label "Applications" -Result "start failed"
        exit 1
    }

    if (-not (Wait-ServicesHealthy -Label "Applications" -Services $appServices -TimeoutSeconds 90)) {
        Write-Host "    Logs: powershell -File .\scripts\dm.ps1 logs" -ForegroundColor DarkGray
        exit 1
    }
}

function Show-Ready {
    Write-Host ""
    Write-Host "Ready!" -ForegroundColor Green
    Write-Host ""
    $addresses = Get-Addresses
    foreach ($name in $addresses.Keys) {
        Write-Host ("  {0,-11}{1}" -f $name, $addresses[$name])
    }
    Write-Host ""
    Write-Host "  Test data:    powershell -File .\scripts\dm.ps1 seed"
    # --prefix instead of "cd ... && npm run dev": Windows PowerShell rejects && as a
    # statement separator.
    Write-Host "  Run frontend: npm --prefix `"$ProjectRoot\src\DM.Web.Client`" run dev"
    Write-Host ""
}

function Start-Services {
    param([switch]$SkipHeader)

    if (-not $SkipHeader) {
        Write-Host ""
        Write-Host "DM3 Start" -ForegroundColor Cyan
        Write-Host ""
    }

    Push-Location $DockerDir
    try {
        if (-not (Invoke-Step -Label "Building" -Command $script:DockerPath -Arguments "compose build --quiet")) {
            exit 1
        }

        Start-Infrastructure
        Start-Migration
        Start-Applications
        Show-Ready
    } finally {
        Pop-Location
    }
}

function Stop-Services {
    Write-Host ""
    Write-Host "DM3 Stop" -ForegroundColor Cyan
    Write-Host ""

    Push-Location $DockerDir
    try {
        if (-not (Invoke-Step -Label "Stopping" -Command $script:DockerPath -Arguments "compose down --remove-orphans")) {
            exit 1
        }
        Write-Host ""
    } finally {
        Pop-Location
    }
}

function Reset-Services {
    Write-Host ""
    Write-Host "DM3 Reset" -ForegroundColor Cyan
    Write-Host ""

    # --remove-orphans: a service deleted from the compose file leaves its container behind,
    # which "down" walks past, and a reset that leaves containers behind is not one.
    Push-Location $DockerDir
    try {
        if (-not (Invoke-Step -Label "Clearing" -Command $script:DockerPath -Arguments "compose down -v --remove-orphans")) {
            exit 1
        }
    } finally {
        Pop-Location
    }

    # Continue with start (no separate header)
    Start-Services -SkipHeader
}
