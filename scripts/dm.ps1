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
# Git for Windows first, and by path rather than by name.
#
# "bash" on PATH is C:\Windows\System32\bash.exe on every machine with the WSL
# feature enabled, and that is a launcher for a Linux distribution rather than a
# shell: with no distribution installed it answers
# "execvpe(/bin/bash) failed: No such file or directory" and the caller is left
# with a message about a file that has nothing to do with this repository. It
# wins over Git for Windows whenever the session's PATH lists System32 first,
# which a plain cmd.exe does.
function Resolve-Bash {
    $candidates = @(
        (Join-Path $env:ProgramFiles "Git\bin\bash.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Git\bin\bash.exe")
    )
    if (${env:ProgramFiles(x86)}) {
        $candidates += (Join-Path ${env:ProgramFiles(x86)} "Git\bin\bash.exe")
    }

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) { return $candidate }
    }

    # Anything else on PATH, as long as it is not one of the two launchers that
    # only pretend to be one.
    $onPath = Get-Command "bash" -ErrorAction SilentlyContinue
    if ($onPath -and $onPath.Source -notmatch '\\(System32|WindowsApps)\\') {
        return $onPath.Source
    }

    return $null
}

function Invoke-EnvironmentInit {
    $bash = Resolve-Bash

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
        Write-Host "  docker/.env is still missing after docker/scripts/init-env.sh ran." -ForegroundColor Red
        Write-Host "  Run it by hand to see why: bash docker/scripts/init-env.sh local" -ForegroundColor DarkGray
        Write-Host ""
        exit 1
    }
    if (-not (Test-Path $exampleFile)) { return }

    # Any name a shell would accept, not upper case only. The pattern used to
    # start at [A-Z] and stop at [A-Z0-9_], which excluded the one variable that
    # matters most - DM_CryptoConfiguration__KeyBase64 - so the file could be
    # missing exactly the value compose dies on and this check passed it.
    $names = { param($path)
        Get-Content $path |
            Where-Object { $_ -match '^[A-Za-z_][A-Za-z0-9_]*=' } |
            ForEach-Object { ($_ -split '=', 2)[0] }
    }

    $declared = & $names $envFile
    $expected = & $names $exampleFile
    $missing = @($expected | Where-Object { $declared -notcontains $_ })

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

    # Presence is not enough. Compose marks the values it cannot start without
    # as ${NAME:?...}, and an empty value fails that interpolation exactly as
    # hard as a missing line - which is the state a hand-made .env arrives in,
    # because .env.example ships the encryption key blank on purpose. The names
    # are read out of the compose files rather than listed here: the file that
    # declares the requirement is the one that should carry it.
    $required = @(
        Get-ChildItem -Path $DockerDir -Filter "docker-compose*.yml" |
            Select-String -Pattern '\$\{([A-Za-z_][A-Za-z0-9_]*):\?' -AllMatches |
            ForEach-Object { $_.Matches } |
            ForEach-Object { $_.Groups[1].Value } |
            Sort-Object -Unique
    )

    $values = @{}
    foreach ($line in Get-Content $envFile) {
        if ($line -match '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { $values[$matches[1]] = $matches[2].Trim() }
    }

    $blank = @($required | Where-Object { $values.ContainsKey($_) -and -not $values[$_] })
    if ($blank.Count -gt 0) {
        Write-Host "  docker/.env declares $($blank.Count) variable(s) with no value, and the stack cannot start without them:" -ForegroundColor Red
        foreach ($name in $blank) {
            Write-Host "    $name=" -ForegroundColor DarkGray
        }
        Write-Host ""
        Write-Host "  docker/scripts/init-env.sh fills the generated ones. The rest are yours to set." -ForegroundColor DarkGray
        Write-Host ""
        exit 1
    }
}

# The environment is prepared and then checked, in that order and once per run.
# The check used to run first and on its own, so a clone with no docker/.env was
# told to copy the template by hand while the script that owns that file sat
# unused two functions below - the documented first run, dead on Windows only.
$script:EnvironmentReady = $false
function Initialize-Environment {
    if ($script:EnvironmentReady) { return }
    if (-not (Invoke-EnvironmentInit)) { exit 1 }
    Assert-Environment
    $script:EnvironmentReady = $true
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
  status    Show status of all services, docker and host processes alike
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

# Why a failure gets more than one line: the step line is a fixed width and the
# reason a container refuses to start is not. This used to take the first
# non-empty line of stdout and stderr concatenated in that order, cut to sixty
# characters - so a failed build reported its first progress line, truncated,
# and the actual error was discarded. Stderr leads because that is where every
# tool here writes its diagnosis, and stdout is the fallback for the ones that
# do not.
function Write-ProcessFailure {
    param([string]$StdOut, [string]$StdErr, [int]$MaxLines = 20)

    $lines = @($StdErr -split "`n" | Where-Object { $_.Trim() -ne "" })
    if ($lines.Count -eq 0) {
        $lines = @($StdOut -split "`n" | Where-Object { $_.Trim() -ne "" })
    }
    if ($lines.Count -eq 0) { return }

    if ($lines.Count -gt $MaxLines) {
        Write-Host "      ... $($lines.Count - $MaxLines) earlier line(s) omitted" -ForegroundColor DarkGray
        $lines = $lines[-$MaxLines..-1]
    }
    foreach ($line in $lines) {
        Write-Host "      $($line.TrimEnd())" -ForegroundColor DarkGray
    }
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

    if ($process.ExitCode -eq 0) {
        Write-CompletedStep -Label $Label -Total 1
        return $true
    }

    Write-FailedStep -Label $Label -Current 0 -Total 1 -Detail "exit code $($process.ExitCode)"
    Write-ProcessFailure -StdOut $stdoutTask.Result -StdErr $stderrTask.Result
    return $false
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
        # Building (with animation)
        if (-not (Invoke-WithAnimation -Label "Building" -Command $script:DockerPath -Arguments "compose build --quiet")) {
            exit 1
        }

        # The list is declared before the step that reports it: the failure line
        # used to carry a hand-typed total of five against six services, so a
        # stack that started nothing at all said one of them had made it.
        $infraServices = @(
            @{ Name = "dm-pg"; Label = "Postgres" },
            @{ Name = "dm-mongo"; Label = "Mongo" },
            @{ Name = "dm-minio"; Label = "MinIO" },
            @{ Name = "dm-imgproxy"; Label = "imgproxy" },
            @{ Name = "dm-loki"; Label = "Loki" },
            @{ Name = "dm-rmq"; Label = "RabbitMQ" }
        )

        # Infrastructure - start containers (with animation)
        # alertmanager comes up with prometheus: started apart, prometheus
        # evaluates its rules into nothing, which is the state the deployment
        # spent its life in and the one a developer would never notice.
        if (-not (Invoke-WithAnimation -Label "Starting" -Command $script:DockerPath -Arguments "compose up -d postgres mongo rabbitmq minio imgproxy mailhog jaeger loki prometheus alertmanager grafana")) {
            Write-FailedStep -Label "Infrastructure" -Current 0 -Total $infraServices.Count
            exit 1
        }

        if (-not (Wait-ServicesHealthy -Label "Infrastructure" -Services $infraServices -TimeoutSeconds 120)) {
            Write-Host "    Logs: .\scripts\dm.ps1 logs" -ForegroundColor DarkGray
            exit 1
        }

        # The MinIO bucket and its policies come from the minio-init container,
        # which the stack starts before the API. There is no step to run by hand.

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

        $appServices = @(
            @{ Name = "dm-api"; Label = "API" },
            @{ Name = "dm-mail-worker"; Label = "Mail" },
            @{ Name = "dm-notification-worker"; Label = "Notify" }
        )

        # Applications - start containers
        & $script:DockerPath compose up -d dm-mail-worker dm-notification-worker dmapi 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-FailedStep -Label "Applications" -Current 0 -Total $appServices.Count
            exit 1
        }

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
        if (-not (Invoke-WithAnimation -Label "Stopping" -Command $script:DockerPath -Arguments "compose down --remove-orphans")) {
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

    # --remove-orphans, because a service deleted from the compose file leaves
    # its container behind and "down" walks past it: the search worker and its
    # Elasticsearch were removed from the stack and survived every reset after
    # that, holding their names and their place on the network. A reset that
    # leaves containers of a service the project no longer has is not a reset.
    Push-Location $DockerDir
    try {
        if (-not (Invoke-WithAnimation -Label "Clearing" -Command $script:DockerPath -Arguments "compose down -v --remove-orphans")) {
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

    if ($process.ExitCode -eq 0) {
        return $stdoutTask.Result
    }

    Write-FailedStep -Label $Label -Current 0 -Total 1 -Detail "exit code $($process.ExitCode)"
    Write-ProcessFailure -StdOut $stdoutTask.Result -StdErr $stderrTask.Result
    return $null
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

    # The liveness endpoint, not a product route. Seeding writes straight to
    # Postgres and does not need the API at all, but the restart at the end of
    # this function does - and a precondition pinned to /v1/games/tags made the
    # whole command depend on one controller keeping its path and staying
    # anonymous. /_health is what the container healthcheck and CI already ask.
    $curlResult = curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5000/_health" 2>$null
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
        $checkResult = curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5000/_health" 2>$null
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

# Everything the stack runs outside docker. The status screen knew only about
# containers, so a second dev server on the next port up, or a pile of build
# nodes holding a gigabyte, were invisible on the one screen that exists to say
# what is running — and the answer to "what is up" had to be given from memory.
#
# Two dev servers at once is the interesting case rather than a tidiness one:
# the page then comes from whichever port the tab is pointed at, and an edit that
# went into one of them reads as a fix that did not work.
function Show-HostProcesses {
    $rows = @()

    $listeners = @{}
    foreach ($line in (netstat -ano | Select-String 'LISTENING')) {
        if ($line -match ':(\d+)\s.*LISTENING\s+(\d+)') {
            $port = $matches[1]
            $processId = [int]$matches[2]
            if (-not $listeners.ContainsKey($processId)) { $listeners[$processId] = @() }
            if ($listeners[$processId] -notcontains $port) { $listeners[$processId] += $port }
        }
    }

    $processes = Get-CimInstance Win32_Process -Filter "name='node.exe' or name='dotnet.exe'" -ErrorAction SilentlyContinue
    foreach ($process in $processes) {
        $command = $process.CommandLine
        if (-not $command) { continue }

        $kind = $null
        if ($command -match 'vite') { $kind = 'vite dev server' }
        elseif ($command -match 'MSBuild\.dll') { $kind = 'msbuild node' }
        elseif ($command -match 'VBCSCompiler|Roslyn') { $kind = 'roslyn server' }
        elseif ($command -match 'testhost') { $kind = 'test host' }
        if (-not $kind) { continue }

        # Someone else's node is not this project's business: the walk is over the
        # whole machine, so the repository path is what tells them apart.
        if ($kind -eq 'vite dev server' -and $command -notmatch 'dm3') { continue }

        $ports = if ($listeners.ContainsKey([int]$process.ProcessId)) {
            ($listeners[[int]$process.ProcessId] | Sort-Object) -join ', '
        } else { '' }

        # Get-CimInstance hands CreationDate over as a DateTime already, unlike the
        # Get-WmiObject it replaced, where it was a CIM_DATETIME string needing a
        # converter. Running that converter over a DateTime throws, and the column
        # came out empty for every row.
        $started = ''
        if ($process.CreationDate -is [DateTime]) {
            $started = $process.CreationDate.ToString('HH:mm')
        }

        $rows += [pscustomobject]@{ Kind = $kind; Pid = $process.ProcessId; Port = $ports; Started = $started }
    }

    Write-Host "  Host processes" -ForegroundColor DarkGray
    if ($rows.Count -eq 0) {
        Write-Host "    (none)" -ForegroundColor DarkGray
        Write-Host ""
        return
    }

    # Build nodes are reused by design and go away on their own, so they are
    # summarised rather than listed: nine identical lines would bury the two that
    # matter.
    $servers = @($rows | Where-Object { $_.Kind -eq 'vite dev server' -or $_.Kind -eq 'test host' })
    foreach ($row in ($servers | Sort-Object Started)) {
        $colour = 'Green'
        Write-Host "    [+] " -ForegroundColor $colour -NoNewline
        Write-Host ("{0,-20}" -f $row.Kind) -NoNewline
        Write-Host ("pid {0,-7}" -f $row.Pid) -ForegroundColor DarkGray -NoNewline
        if ($row.Port) { Write-Host (":{0,-12}" -f $row.Port) -ForegroundColor DarkGray -NoNewline } else { Write-Host ("{0,-13}" -f '') -NoNewline }
        Write-Host ("since {0}" -f $row.Started) -ForegroundColor DarkGray
    }

    $viteCount = @($rows | Where-Object { $_.Kind -eq 'vite dev server' }).Count
    if ($viteCount -gt 1) {
        Write-Host "    [!] two dev servers are up: the tab shows whichever port it points at" -ForegroundColor Yellow
    }

    $builders = @($rows | Where-Object { $_.Kind -eq 'msbuild node' -or $_.Kind -eq 'roslyn server' })
    if ($builders.Count -gt 0) {
        $megabytes = 0
        foreach ($builder in $builders) {
            $process = Get-Process -Id $builder.Pid -ErrorAction SilentlyContinue
            if ($process) { $megabytes += [int]($process.WorkingSet64 / 1MB) }
        }
        Write-Host ("    [~] {0} build server(s), {1} MB - dotnet build-server shutdown" -f $builders.Count, $megabytes) -ForegroundColor DarkGray
    }

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
        Show-HostProcesses
        return
    }

    # Group services. Loki and the MinIO initialiser were in none of the three
    # lists, so a container the start command waits on was missing from the
    # screen that exists to say what is up - which is why the last group below
    # is not a list at all but everything the three did not claim.
    $infra = @("pg", "mongo", "rmq", "minio", "minio-init", "imgproxy")
    $apps = @("api", "mail-worker", "notification-worker", "migration")
    $tools = @("mailhog", "grafana", "prometheus", "alertmanager", "jaeger", "loki")

    $all = @{}
    foreach ($line in $containers) {
        $parts = $line -split '\|'
        $name = $parts[0] -replace '^dm-', ''
        $status = $parts[1]
        $ports = if ($parts.Length -gt 2) { $parts[2] } else { "" }
        # Any published address, not 0.0.0.0 alone: the stack binds its ports to
        # 127.0.0.1 so that nothing outside the machine can reach them, and this
        # column has been empty ever since that was done. The optional range is
        # MinIO, which publishes 9000-9001 as one mapping.
        $port = ""
        if ($ports -match ':(\d+)(?:-\d+)?->') { $port = $matches[1] }

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

    # Whatever the three lists did not name: a container the project no longer
    # declares, or one added to compose and not to a list here. Both are worth
    # seeing, and the alternative is a status screen that hides them.
    $grouped = $infra + $apps + $tools
    $rest = @($all.Keys | Where-Object { $grouped -notcontains $_ } | Sort-Object)
    if ($rest.Count -gt 0) {
        Write-ServiceGroup "Other:" $rest
    }
    Write-Host ""

    Show-HostProcesses
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
# Every command that touches compose gets the same preparation, including the
# ones that only read: compose interpolates docker/.env before it will so much
# as list containers, so "status" needs the file as much as "start" does.
if ($Command -ne 'help') { Initialize-Environment }

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
