# The step lines scripts/dm.ps1 prints and the processes it runs behind them, for every
# command; scripts/dm.ps1 dot-sources this file. In a console a step is one line that
# redraws while it runs and ends as "[+] <step> [n/n] <detail>". When the output is captured
# nothing redraws and a step prints plain "<step>: <result>" lines, since a captured carriage
# return only garbles the log.

# The addresses of the local services, read from addresses.txt: the file scripts/dm.sh reads
# too, so the two scripts cannot name different sets. Returns an ordered name -> url map.
function Get-Addresses {
    $addresses = [ordered]@{}
    foreach ($line in (Get-Content (Join-Path $PSScriptRoot "addresses.txt"))) {
        if ($line -match '^\s*(#|$)') { continue }
        $parts = $line.Trim() -split '\s+', 2
        $addresses[$parts[0]] = $parts[1]
    }
    return $addresses
}

$script:StepLabelWidth = 18
$script:StepDots = @('.', '..', '...')
$script:StepsRedraw = $false
try { $script:StepsRedraw = -not [Console]::IsOutputRedirected } catch {}
$script:StepFrameLength = 0

function Write-AnimatedStep {
    param([string]$Label, [int]$Current, [int]$Total, [string]$Detail = "", [int]$DotFrame = 0)

    if (-not $script:StepsRedraw) { return }
    try { [Console]::CursorVisible = $false } catch {}
    $dots = $script:StepDots[$DotFrame % 3].PadRight(3)
    $pad = " " * ($script:StepLabelWidth - $Label.Length)
    $text = if ($Detail) { "  [ ] $Label$pad[$Current/$Total]  $Detail  $dots" } else { "  [ ] $Label$pad[$Current/$Total]  $dots" }
    Write-Host "`r$text" -NoNewline
    $script:StepFrameLength = $text.Length
}

# The last line of a step, padded to the last frame: a shorter text would leave its tail on screen.
function Write-FinishedStep {
    param([string]$Mark, [string]$Color, [string]$Label, [int]$Count, [int]$Total, [string]$Detail)

    try { [Console]::CursorVisible = $true } catch {}
    $pad = " " * ($script:StepLabelWidth - $Label.Length)
    $text = if ($Detail) { "  [_] $Label$pad[$Count/$Total]  $Detail" } else { "  [_] $Label$pad[$Count/$Total]" }

    Write-Host "`r  [" -NoNewline
    Write-Host $Mark -ForegroundColor $Color -NoNewline
    Write-Host $text.PadRight($script:StepFrameLength).Substring(4)
}

function Write-CompletedStep {
    param([string]$Label, [int]$Total, [string]$Detail = "")

    if (-not $script:StepsRedraw) {
        Write-Host "  ${Label}: $(if ($Detail) { $Detail } else { 'done' })"
        return
    }
    Write-FinishedStep -Mark "+" -Color Green -Label $Label -Count $Total -Total $Total -Detail $Detail
}

function Write-FailedStep {
    param([string]$Label, [int]$Current, [int]$Total, [string]$Detail = "")

    if (-not $script:StepsRedraw) {
        Write-Host "  ${Label}: $(if ($Detail) { $Detail } else { 'failed' })" -ForegroundColor Red
        return
    }
    Write-FinishedStep -Mark "-" -Color Red -Label $Label -Count $Current -Total $Total -Detail $Detail
}

# A failure gets more than one line because the step line is a fixed width and the reason
# a container refuses to start is not. Stderr leads, since every tool here writes its
# diagnosis there; stdout is the fallback for the ones that do not.
function Write-ProcessFailure {
    param([string]$StdOut, [string]$StdErr, [int]$MaxLines = 20)

    $lines = @($StdErr -split "`n" | Where-Object { $_.Trim() -ne "" })
    if ($lines.Count -eq 0) {
        $lines = @($StdOut -split "`n" | Where-Object { $_.Trim() -ne "" })
    }
    if ($lines.Count -eq 0) { return }

    if ($lines.Count -gt $MaxLines) {
        Write-Host "      $($lines.Count - $MaxLines) earlier line(s) omitted" -ForegroundColor DarkGray
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
                if (-not $script:StepsRedraw) { Write-Host "  ${Label}: $($svc.Label) healthy" }
            }
        }

        Write-AnimatedStep -Label $Label -Current $readyNames.Count -Total $total -Detail ($readyNames -join " > ") -DotFrame $frame

        if ($pending.Count -gt 0) {
            Start-Sleep -Milliseconds 400
            $elapsed += 0.4
            $frame++
        }
    }

    if ($pending.Count -eq 0) {
        if ($script:StepsRedraw) { Write-CompletedStep -Label $Label -Total $total -Detail ($readyNames -join " > ") }
        return $true
    }

    $failedNames = ($Services | Where-Object { $pending.Contains($_.Name) } | ForEach-Object { $_.Label }) -join ", "
    Write-FailedStep -Label $Label -Current $readyNames.Count -Total $total -Detail "not healthy after ${TimeoutSeconds}s: $failedNames"
    return $false
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

# Runs a process behind a step line. Returns $true on success, or its standard output with
# -CaptureOutput (the caller reports success itself); $null on failure.
function Invoke-Step {
    param(
        [string]$Label,
        [string]$Command,
        [string]$Arguments,
        [switch]$CaptureOutput
    )

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

    # The process output is not shown, so a long step announces itself: silence for the
    # minutes a build takes reads as a hang.
    if (-not $script:StepsRedraw) { Write-Host "  ${Label}: running" }

    try {
        $process.Start() | Out-Null
    } catch {
        Write-FailedStep -Label $Label -Current 0 -Total 1 -Detail "failed to start: $_"
        return $null
    }

    # Both pipes are drained while the process runs: a redirected pipe holds tens of
    # kilobytes, the writer blocks once it is full, and a wait that reads nothing
    # until HasExited hangs on any build that prints more than that.
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
        if ($CaptureOutput) { return $stdoutTask.Result }
        Write-CompletedStep -Label $Label -Total 1
        return $true
    }

    Write-FailedStep -Label $Label -Current 0 -Total 1 -Detail "exit code $($process.ExitCode)"
    Write-ProcessFailure -StdOut $stdoutTask.Result -StdErr $stderrTask.Result
    return $null
}
