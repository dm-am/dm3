# The step lines scripts/dm.ps1 prints and the processes it runs behind them,
# for every command; scripts/dm.ps1 dot-sources this file. A step line is
# "<step>: <result>", one per line, with no redrawing.

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

function Write-Step {
    param([string]$Label, [string]$Result, [switch]$Failed)

    if ($Failed) {
        Write-Host "  ${Label}: $Result" -ForegroundColor Red
    } else {
        Write-Host "  ${Label}: $Result"
    }
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

    $pending = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($svc in $Services) {
        $pending.Add($svc.Name) | Out-Null
    }

    $elapsed = 0

    while ($pending.Count -gt 0 -and $elapsed -lt $TimeoutSeconds) {
        foreach ($svc in $Services) {
            if (-not $pending.Contains($svc.Name)) { continue }
            $status = & $script:DockerPath inspect $svc.Name --format='{{.State.Health.Status}}' 2>$null
            if ($status -eq "healthy") {
                $pending.Remove($svc.Name) | Out-Null
                Write-Step -Label $Label -Result "$($svc.Label) healthy"
            }
        }

        if ($pending.Count -gt 0) {
            Start-Sleep -Milliseconds 400
            $elapsed += 0.4
        }
    }

    if ($pending.Count -eq 0) { return $true }

    $failedNames = ($Services | Where-Object { $pending.Contains($_.Name) } | ForEach-Object { $_.Label }) -join ", "
    Write-Step -Failed -Label $Label -Result "not healthy after ${TimeoutSeconds}s: $failedNames"
    return $false
}

function Wait-SimpleStep {
    param(
        [string]$Label,
        [scriptblock]$Check,
        [int]$TimeoutSeconds = 30
    )

    $elapsed = 0

    while ($elapsed -lt $TimeoutSeconds) {
        $result = & $Check
        if ($result) {
            Write-Step -Label $Label -Result "done"
            return $true
        }

        Start-Sleep -Milliseconds 500
        $elapsed += 0.5
    }

    Write-Step -Failed -Label $Label -Result "timeout after ${TimeoutSeconds}s"
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
    Write-Step -Label $Label -Result "running"

    try {
        $process.Start() | Out-Null
    } catch {
        Write-Step -Failed -Label $Label -Result "failed to start: $_"
        return $null
    }

    # Both pipes are drained while the process runs: a redirected pipe holds tens of
    # kilobytes, the writer blocks once it is full, and a wait that reads nothing
    # until HasExited hangs on any build that prints more than that.
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()

    $process.WaitForExit()

    if ($process.ExitCode -eq 0) {
        if ($CaptureOutput) { return $stdoutTask.Result }
        Write-Step -Label $Label -Result "done"
        return $true
    }

    Write-Step -Failed -Label $Label -Result "exit code $($process.ExitCode)"
    Write-ProcessFailure -StdOut $stdoutTask.Result -StdErr $stderrTask.Result
    return $null
}
