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

# Prepares docker/.env through docker/scripts/init-env.sh, the one script that
# owns that file. It is a shell script because the server installer is one too,
# and Git for Windows ships the interpreter for it - the same one this repository
# already requires for its git hooks and for check-vulnerable-packages.sh. If it
# is not on the machine, the command to run is printed rather than guessed at:
# half-preparing the file is what produced a .env compose refused to interpolate.
function Invoke-EnvironmentInit {
    $bash = $null
    $onPath = Get-Command "bash" -ErrorAction SilentlyContinue
    if ($onPath) { $bash = $onPath.Source }
    if (-not $bash) {
        $gitBash = Join-Path $env:ProgramFiles "Git\bin\bash.exe"
        if (Test-Path $gitBash) { $bash = $gitBash }
    }

    if (-not $bash) {
        Write-Host "bash not found; docker/.env is prepared by a shell script." -ForegroundColor Red
        Write-Host "  Install Git for Windows, or run: bash docker/scripts/init-env.sh local" -ForegroundColor Yellow
        return $false
    }

    $script = Join-Path $DockerDir "scripts/init-env.sh"
    & $bash $script local
    if ($LASTEXITCODE -ne 0) {
        Write-Host "docker/scripts/init-env.sh failed with exit code $LASTEXITCODE" -ForegroundColor Red
        return $false
    }

    return $true
}

# Check Docker daemon is running
$dockerCheck = & $script:DockerPath info 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker daemon not running. Start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Environment check: docker compose refuses to do anything at all when a
# required variable has no value, and the message it prints names the first
# service it failed to interpolate rather than the file to edit. Worse, the
# animated wrappers below cut a failure down to sixty characters, so the reason
# does not survive to the screen. The list of names comes from .env.example
# rather than from a copy here: a variable added to the stack is added there in
# the same commit, and a second list would be one more thing to forget.
function Assert-Environment {
    $envFile = Join-Path $DockerDir ".env"
    $exampleFile = Join-Path $DockerDir ".env.example"

    if (-not (Test-Path $envFile)) {
        Write-Host "  docker/.env missing. Copy it from docker/.env.example and fill in the values." -ForegroundColor Red
        Write-Host ""
        exit 1
    }
    if (-not (Test-Path $exampleFile)) { return }

    $names = { param($path)
        Get-Content $path |
            Where-Object { $_ -match '^[A-Z][A-Z0-9_]*=' } |
            ForEach-Object { ($_ -split '=', 2)[0] }
    }

    $declared = & $names $envFile
    $expected = & $names $exampleFile
    $missing = $expected | Where-Object { $declared -notcontains $_ }

    if ($missing.Count -gt 0) {
        Write-Host "  docker/.env is missing $($missing.Count) variable(s) the stack needs:" -ForegroundColor Red
        foreach ($name in $missing) {
            $sample = (Get-Content $exampleFile | Where-Object { $_ -match "^$name=" } | Select-Object -First 1)
            Write-Host "    $sample" -ForegroundColor DarkGray
        }
        Write-Host ""
        Write-Host "  They are documented in docker/.env.example. Copy the lines above and set your own values." -ForegroundColor DarkGray
        Write-Host ""
        exit 1
    }
}

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

$script:LabelWidth = 18
$script:DotChars = @('.', '..', '...')

function Write-AnimatedStep {
    param([string]$Label, [int]$Current, [int]$Total, [string]$Detail = "", [int]$DotFrame = 0)

    try { [Console]::CursorVisible = $false } catch {}
    $dots = $script:DotChars[$DotFrame % 3].PadRight(3)
    $pad = " " * ($script:LabelWidth - $Label.Length)
    $text = if ($Detail) { "  [ ] $Label$pad[$Current/$Total]  $Detail  $dots" } else { "  [ ] $Label$pad[$Current/$Total]  $dots" }
    Write-Host "`r$text" -NoNewline
}

function Write-CompletedStep {
    param([string]$Label, [int]$Total, [string]$Detail = "")

    try { [Console]::CursorVisible = $true } catch {}
    $pad = " " * ($script:LabelWidth - $Label.Length)
    $text = if ($Detail) { "  [_] $Label$pad[$Total/$Total]  $Detail      " } else { "  [_] $Label$pad[$Total/$Total]      " }

    Write-Host "`r  [" -NoNewline
    Write-Host "+" -ForegroundColor Green -NoNewline
    Write-Host $text.Substring(4)
}

function Write-FailedStep {
    param([string]$Label, [int]$Current, [int]$Total, [string]$Detail = "")

    try { [Console]::CursorVisible = $true } catch {}
    $pad = " " * ($script:LabelWidth - $Label.Length)
    $text = if ($Detail) { "  [_] $Label$pad[$Current/$Total]  $Detail      " } else { "  [_] $Label$pad[$Current/$Total]      " }

    Write-Host "`r  [" -NoNewline
    Write-Host "-" -ForegroundColor Red -NoNewline
    Write-Host $text.Substring(4)
}

function Wait-ServicesHealthy {
    param(
        [string]$Label,
        [array]$Services,
        [int]$TimeoutSeconds = 120
    )

    $total = $Services.Count
    $pending = [System.Collections.Generic.HashSet[string]]::new()
    $readyNames = [System.Collections.Generic.List[string]]::new()
    foreach ($svc in $Services) {
        $pending.Add($svc.Name) | Out-Null
    }

    $elapsed = 0
    $frame = 0

    while ($pending.Count -gt 0 -and $elapsed -lt $TimeoutSeconds) {
        foreach ($svc in $Services) {
            if (-not $pending.Contains($svc.Name)) { continue }
            $status = & $script:DockerPath inspect $svc.Name --format='{{.State.Health.Status}}' 2>$null
            if ($status -eq "healthy") {
                $pending.Remove($svc.Name) | Out-Null
                $readyNames.Add($svc.Label) | Out-Null
            }
        }

        $detail = ($readyNames -join " > ")
        Write-AnimatedStep -Label $Label -Current $readyNames.Count -Total $total -Detail $detail -DotFrame $frame

        if ($pending.Count -gt 0) {
            Start-Sleep -Milliseconds 400
            $elapsed += 0.4
            $frame++
        }
    }

    $detail = ($readyNames -join " > ")
    if ($pending.Count -eq 0) {
        Write-CompletedStep -Label $Label -Total $total -Detail $detail
        return $true
    } else {
        $failedNames = ($Services | Where-Object { $pending.Contains($_.Name) } | ForEach-Object { $_.Label }) -join ", "
        Write-FailedStep -Label $Label -Current $readyNames.Count -Total $total -Detail "failed: $failedNames"
        return $false
    }
}

function Wait-SimpleStep {
    param(
        [string]$Label,
        [scriptblock]$Check,
        [int]$TimeoutSeconds = 30
    )

    $elapsed = 0
    $frame = 0

    while ($elapsed -lt $TimeoutSeconds) {
        Write-AnimatedStep -Label $Label -Current 0 -Total 1 -DotFrame $frame

        $result = & $Check
        if ($result) {
            Write-CompletedStep -Label $Label -Total 1
            return $true
        }

        Start-Sleep -Milliseconds 500
        $elapsed += 0.5
        $frame++
    }

    Write-FailedStep -Label $Label -Current 0 -Total 1 -Detail "timeout after ${TimeoutSeconds}s"
    return $false
}

function Invoke-WithAnimation {
    param(
        [string]$Label,
        [string]$Command,
        [string]$Arguments
    )

    # Start process in background
    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = $Command
    $pinfo.Arguments = $Arguments
    $pinfo.WorkingDirectory = (Get-Location).Path
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $pinfo

    try {
        $process.Start() | Out-Null
    } catch {
        Write-FailedStep -Label $Label -Current 0 -Total 1 -Detail "Failed to start: $_"
        return $false
    }

    # Both pipes are drained while the process is still running, not after it.
    # A redirected pipe holds tens of kilobytes; the writer blocks once it is
    # full, and a wait loop that reads nothing until HasExited turns that into a
    # hang with no output and no exit — which is what "docker compose --build"
    # did every time, because a build writes more than the buffer holds.
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()

    $frame = 0
    while (-not $process.HasExited) {
        Write-AnimatedStep -Label $Label -Current 0 -Total 1 -DotFrame $frame
        Start-Sleep -Milliseconds 400
        $frame++
    }

    $process.WaitForExit()
    $stderr = $stdoutTask.Result + "`n" + $stderrTask.Result

    if ($process.ExitCode -eq 0) {
        Write-CompletedStep -Label $Label -Total 1
        return $true
    } else {
        # Extract first meaningful error line
        $errorLine = ($stderr -split "`n" | Where-Object { $_.Trim() -ne "" } | Select-Object -First 1)
        if ($errorLine) {
            $errorLine = $errorLine.Trim()
            # Truncate if too long
            if ($errorLine.Length -gt 60) { $errorLine = $errorLine.Substring(0, 57) + "..." }
        } else {
            $errorLine = "exit code $($process.ExitCode)"
        }
        Write-FailedStep -Label $Label -Current 0 -Total 1 -Detail $errorLine
        return $false
    }
}

function Start-Services {
    param([switch]$SkipHeader)

    if (-not $SkipHeader) {
        Write-Host ""
        Write-Host "DM3 Start" -ForegroundColor Cyan
        Write-Host ""
    }

    # docker/.env is prepared by the one script that owns it, the same call
    # scripts/dm.sh makes. This used to be a second implementation of the same
    # two steps - copy the template, generate the encryption key - and the two
    # drifted the moment the generator learned anything the copy did not: the
    # server credentials, and later the topping up of a file created by hand.
    # Two implementations of "prepare the environment" is how the documented
    # first run came to die on interpolation on one platform and not the other.
    if (-not (Invoke-EnvironmentInit)) { exit 1 }

    Push-Location $DockerDir
    try {
        # Building (with animation)
        if (-not (Invoke-WithAnimation -Label "Building" -Command $script:DockerPath -Arguments "compose build --quiet")) {
            exit 1
        }

        # Infrastructure - start containers (with animation)
        # alertmanager comes up with prometheus: started apart, prometheus
        # evaluates its rules into nothing, which is the state the deployment
        # spent its life in and the one a developer would never notice.
        if (-not (Invoke-WithAnimation -Label "Starting" -Command $script:DockerPath -Arguments "compose up -d postgres mongo rabbitmq minio imgproxy mailhog jaeger loki prometheus alertmanager grafana")) {
            Write-FailedStep -Label "Infrastructure" -Current 0 -Total 5
            exit 1
        }

        $infraServices = @(
            @{ Name = "dm-pg"; Label = "Postgres" },
            @{ Name = "dm-mongo"; Label = "Mongo" },
            @{ Name = "dm-minio"; Label = "MinIO" },
            @{ Name = "dm-imgproxy"; Label = "imgproxy" },
            @{ Name = "dm-loki"; Label = "Loki" },
            @{ Name = "dm-rmq"; Label = "RabbitMQ" }
        )
        if (-not (Wait-ServicesHealthy -Label "Infrastructure" -Services $infraServices -TimeoutSeconds 120)) {
            Write-Host "    Logs: .\scripts\dm.ps1 logs" -ForegroundColor DarkGray
            exit 1
        }

        # MinIO bucket создается автоматически API при старте
        # (StorageBucketInitializer) — ручной mc-init не нужен.

        # Migration
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

        # Applications - start containers
        & $script:DockerPath compose up -d dm-mail-worker dm-notification-worker dmapi 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-FailedStep -Label "Applications" -Current 0 -Total 4
            exit 1
        }

        $appServices = @(
            @{ Name = "dm-api"; Label = "API" },
            @{ Name = "dm-mail-worker"; Label = "Mail" },
            @{ Name = "dm-notification-worker"; Label = "Notify" }
        )
        if (-not (Wait-ServicesHealthy -Label "Applications" -Services $appServices -TimeoutSeconds 90)) {
            Write-Host "    Logs: .\scripts\dm.ps1 logs" -ForegroundColor DarkGray
            exit 1
        }

        # Success
        Write-Host ""
        Write-Host "Ready!" -ForegroundColor Green
        Write-Host ""
        Write-Host "  API        http://localhost:5000"
        Write-Host "  RabbitMQ   http://localhost:15672" -ForegroundColor DarkGray
        Write-Host "  MinIO      http://localhost:9001" -ForegroundColor DarkGray
        Write-Host "  MailHog    http://localhost:8025" -ForegroundColor DarkGray
        Write-Host "  Grafana    http://localhost:3000" -ForegroundColor DarkGray
        Write-Host "  Prometheus http://localhost:9090" -ForegroundColor DarkGray
        Write-Host "  Jaeger     http://localhost:16686" -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "  Test data: " -ForegroundColor DarkGray -NoNewline
        Write-Host ".\scripts\dm.ps1 seed"
        Write-Host "  Frontend:  " -ForegroundColor DarkGray -NoNewline
        Write-Host "cd `"$ProjectRoot\src\DM.Web.Client`" && npm run dev"
        Write-Host ""
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
        if (-not (Invoke-WithAnimation -Label "Stopping" -Command $script:DockerPath -Arguments "compose down")) {
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

    Push-Location $DockerDir
    try {
        if (-not (Invoke-WithAnimation -Label "Clearing" -Command $script:DockerPath -Arguments "compose down -v")) {
            exit 1
        }
    } finally {
        Pop-Location
    }

    # Continue with start (no separate header)
    Start-Services -SkipHeader
}

function Invoke-WithAnimationAndOutput {
    param(
        [string]$Label,
        [string]$Command,
        [string]$Arguments
    )

    # Start process in background, capture output
    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = $Command
    $pinfo.Arguments = $Arguments
    $pinfo.WorkingDirectory = (Get-Location).Path
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $pinfo

    try {
        $process.Start() | Out-Null
    } catch {
        Write-FailedStep -Label $Label -Current 0 -Total 1 -Detail "Failed to start: $_"
        return $null
    }

    # Both pipes are drained while the process is still running, not after it.
    # A redirected pipe holds tens of kilobytes; the writer blocks once it is
    # full, and a wait loop that reads nothing until HasExited turns that into a
    # hang with no output and no exit — which is what "docker compose --build"
    # did every time, because a build writes more than the buffer holds.
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()

    $frame = 0
    while (-not $process.HasExited) {
        Write-AnimatedStep -Label $Label -Current 0 -Total 1 -DotFrame $frame
        Start-Sleep -Milliseconds 400
        $frame++
    }

    $process.WaitForExit()
    $output = $stdoutTask.Result
    $stderr = $stderrTask.Result

    if ($process.ExitCode -eq 0) {
        return $output
    } else {
        # Extract first meaningful error line
        $errorLine = ($stderr -split "`n" | Where-Object { $_.Trim() -ne "" } | Select-Object -First 1)
        if ($errorLine) {
            $errorLine = $errorLine.Trim()
            if ($errorLine.Length -gt 60) { $errorLine = $errorLine.Substring(0, 57) + "..." }
        } else {
            $errorLine = "exit code $($process.ExitCode)"
        }
        Write-FailedStep -Label $Label -Current 0 -Total 1 -Detail $errorLine
        return $null
    }
}

function Get-SeedSummary {
    param(
        [string]$Output,
        [string]$Prefix
    )

    # The seeder prints exactly one "<prefix>: ..." summary line per command,
    # already filtered down to what it actually created
    $line = ($Output -split "`n" | Where-Object { $_ -match "^${Prefix}:" } | Select-Object -First 1)
    if ($line) {
        return ($line -replace "^${Prefix}:\s*", "").Trim()
    }
    return "done"
}

function Invoke-Seed {
    Write-Host ""
    Write-Host "DM3 Seed" -ForegroundColor Cyan
    Write-Host ""

    # Check if API is running
    $curlResult = curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5000/v1/games/tags" 2>$null
    if ($curlResult -ne "200") {
        Write-Host "  API not available. Run first: .\scripts\dm.ps1 start" -ForegroundColor Red
        Write-Host ""
        exit 1
    }

    # Seeding runs in its own container (tools profile), not over HTTP.
    # --build keeps the image in step with the seed data in the sources.
    Push-Location $DockerDir
    try {
        # Users (with animation)
        $response = Invoke-WithAnimationAndOutput -Label "Users" -Command $script:DockerPath -Arguments "compose run --rm -T --build seeder users"
        if ($null -eq $response) { exit 1 }
        Write-CompletedStep -Label "Users" -Total 1 -Detail (Get-SeedSummary -Output $response -Prefix "users")

        # Content (with animation)
        $compResponse = Invoke-WithAnimationAndOutput -Label "Content" -Command $script:DockerPath -Arguments "compose run --rm -T --build seeder content"
        if ($null -eq $compResponse) { exit 1 }
        Write-CompletedStep -Label "Content" -Total 1 -Detail (Get-SeedSummary -Output $compResponse -Prefix "content")
    } finally {
        Pop-Location
    }

    # Restart API (with animation)
    if (-not (Invoke-WithAnimation -Label "Restart API" -Command $script:DockerPath -Arguments "restart dm-api")) {
        exit 1
    }

    # Wait for API to be ready
    $apiReady = Wait-SimpleStep -Label "API Ready" -TimeoutSeconds 30 -Check {
        $checkResult = curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5000/v1/games/tags" 2>$null
        return $checkResult -eq "200"
    }
    if (-not $apiReady) { exit 1 }

    Write-Host ""
    Write-Host "Ready!" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Password:  " -ForegroundColor DarkGray -NoNewline
    Write-Host "Test123!"
    Write-Host "  Accounts:  " -ForegroundColor DarkGray -NoNewline
    Write-Host "SolohinLex, TestModerator, TestUser, Player_One..."
    Write-Host ""
}

function Show-Status {
    Write-Host ""
    Write-Host "DM3 Status" -ForegroundColor Cyan
    Write-Host ""

    $containers = & $script:DockerPath ps -a --format "{{.Names}}|{{.Status}}|{{.Ports}}" --filter "name=dm-" | Sort-Object

    if (-not $containers) {
        Write-Host "  No containers running." -ForegroundColor Yellow
        Write-Host "  Run: .\scripts\dm.ps1 start" -ForegroundColor DarkGray
        Write-Host ""
        return
    }

    # Group services
    $infra = @("pg", "mongo", "rmq", "minio", "imgproxy")
    $apps = @("api", "mail-worker", "notification-worker", "migration")
    $tools = @("mailhog", "grafana", "prometheus", "alertmanager", "jaeger")

    $all = @{}
    foreach ($line in $containers) {
        $parts = $line -split '\|'
        $name = $parts[0] -replace '^dm-', ''
        $status = $parts[1]
        $ports = if ($parts.Length -gt 2) { $parts[2] } else { "" }
        $port = ""
        if ($ports -match '0\.0\.0\.0:(\d+)->') { $port = $matches[1] }

        if ($status -match "Up.*healthy" -or $status -match "Exited \(0\)") {
            $icon = "+"; $color = "Green"
        } elseif ($status -match "Up") {
            $icon = "~"; $color = "Yellow"
        } else {
            $icon = "-"; $color = "Red"
        }

        $all[$name] = @{ Icon = $icon; Color = $color; Port = $port }
    }

    function Write-ServiceGroup {
        param([string]$Title, [array]$Services)
        Write-Host "  $Title" -ForegroundColor DarkGray
        foreach ($svc in $Services) {
            if ($all.ContainsKey($svc)) {
                $s = $all[$svc]
                Write-Host "    [$($s.Icon)] " -ForegroundColor $s.Color -NoNewline
                Write-Host ("{0,-20}" -f $svc) -NoNewline
                if ($s.Port) { Write-Host ":$($s.Port)" -ForegroundColor DarkGray } else { Write-Host "" }
            }
        }
    }

    Write-ServiceGroup "Infrastructure:" $infra
    Write-ServiceGroup "Applications:" $apps
    Write-ServiceGroup "Dev Tools:" $tools
    Write-Host ""
}

function Show-Logs {
    if ($Service) {
        & $script:DockerPath logs $Service --tail 100 -f
    } else {
        Push-Location $DockerDir
        try {
            & $script:DockerPath compose logs --tail 50 -f
        } finally {
            Pop-Location
        }
    }
}

# Main
if ($Command -ne 'help') { Assert-Environment }

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
