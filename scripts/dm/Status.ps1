# The status and logs commands of scripts/dm.ps1, which dot-sources this
# file.

# Everything the stack runs outside docker, so the status screen says what is running. Two
# dev servers at once is the case that matters: the page comes from whichever port the tab
# points at, and an edit that went into the other reads as a fix that did not work.
function Get-HostProcesses {
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

        # Each kind is known by the script it runs: a bare 'vite' matched every vitest worker
        # and the type check as well. A build runs the same vite.js as a server and is told
        # apart by its command; it holds no port and is gone in a minute, so it is not listed.
        $kind = $null
        if ($command -match '[\\/]vitest[\\/]') { $kind = 'vitest' }
        elseif ($command -match '[\\/]vite[\\/]bin[\\/]vite\.js"?\s+build\b') { $kind = 'vite build' }
        elseif ($command -match '[\\/]vite[\\/]bin[\\/]vite\.js') { $kind = 'vite dev server' }
        elseif ($command -match 'MSBuild\.dll') { $kind = 'msbuild node' }
        elseif ($command -match 'VBCSCompiler|Roslyn') { $kind = 'roslyn server' }
        elseif ($command -match 'testhost') { $kind = 'test host' }
        if (-not $kind -or $kind -eq 'vite build') { continue }

        # Someone else's node is not this project's business: the walk is over the
        # whole machine, so the repository path is what tells them apart.
        if (($kind -eq 'vite dev server' -or $kind -eq 'vitest') -and $command -notmatch 'dm3') { continue }

        $ports = if ($listeners.ContainsKey([int]$process.ProcessId)) {
            ($listeners[[int]$process.ProcessId] | Sort-Object) -join ', '
        } else { '' }

        # Get-CimInstance hands CreationDate over as a DateTime, so no converter is needed.
        $started = ''
        if ($process.CreationDate -is [DateTime]) {
            $started = $process.CreationDate.ToString('HH:mm')
        }

        $rows += [pscustomobject]@{ Kind = $kind; Pid = $process.ProcessId; Port = $ports; Started = $started }
    }

    return $rows
}

function Write-HostProcesses {
    param([array]$Rows)

    Write-Host "  Host processes" -ForegroundColor DarkGray
    if ($rows.Count -eq 0) {
        Write-Host "    (none)" -ForegroundColor DarkGray
        Write-Host ""
        return
    }

    # Build nodes are reused by design and go away on their own, so they are summarised
    # rather than listed.
    $servers = @($rows | Where-Object { $_.Kind -eq 'vite dev server' -or $_.Kind -eq 'test host' })
    foreach ($row in ($servers | Sort-Object Started)) {
        Write-Host "    [+] " -ForegroundColor Green -NoNewline
        Write-Host ("{0,-20}" -f $row.Kind) -NoNewline
        Write-Host ("pid {0,-7}" -f $row.Pid) -ForegroundColor DarkGray -NoNewline
        if ($row.Port) { Write-Host (":{0,-12}" -f $row.Port) -ForegroundColor DarkGray -NoNewline } else { Write-Host ("{0,-13}" -f '') -NoNewline }
        Write-Host ("since {0}" -f $row.Started) -ForegroundColor DarkGray
    }

    $viteCount = @($rows | Where-Object { $_.Kind -eq 'vite dev server' }).Count
    if ($viteCount -gt 1) {
        Write-Host "    [!] two dev servers are up: the tab shows whichever port it points at" -ForegroundColor Yellow
    }

    # A test run is a parent and its workers, gone when the run ends: one line.
    $testRuns = @($rows | Where-Object { $_.Kind -eq 'vitest' })
    if ($testRuns.Count -gt 0) {
        Write-Host ("    [~] vitest, {0} process(es) - a client test run" -f $testRuns.Count) -ForegroundColor DarkGray
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
        Write-Host "  Run: powershell -File .\scripts\dm.ps1 start" -ForegroundColor DarkGray
        Write-Host ""
        Write-HostProcesses -Rows @(Get-HostProcesses)
        return
    }

    # Group services. The last group is not a list but everything the three did not claim, so
    # a container the start command waits on cannot go missing from the screen.
    $infra = @("pg", "seaweedfs", "storage")
    $apps = @("api", "mail-worker", "notification-worker", "migration")
    $tools = @("mailpit", "grafana", "prometheus", "alertmanager", "jaeger", "victorialogs")

    $all = @{}
    foreach ($line in $containers) {
        $parts = $line -split '\|'
        $name = $parts[0] -replace '^dm-', ''
        $status = $parts[1]
        $ports = if ($parts.Length -gt 2) { $parts[2] } else { "" }
        # Any published address, not 0.0.0.0 alone: the stack binds its ports to 127.0.0.1.
        # The optional range is a service that publishes several ports as one mapping.
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

    # Whatever the three lists did not name: a container the project no longer declares, or
    # one added to compose and not to a list here. Both are worth seeing.
    $grouped = $infra + $apps + $tools
    $rest = @($all.Keys | Where-Object { $grouped -notcontains $_ } | Sort-Object)
    if ($rest.Count -gt 0) {
        Write-ServiceGroup "Other:" $rest
    }
    Write-Host ""

    Write-HostProcesses -Rows @(Get-HostProcesses)
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
