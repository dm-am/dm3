# docker/.env for scripts/dm.ps1, which dot-sources this file: prepared by
# its generator, then checked.

# Prepares docker/.env through docker/scripts/init-env.sh, the one script that owns that
# file. It is a shell script because the server installer is one too, and Git for Windows
# ships the interpreter the repository already requires for its git hooks. Without one, the
# command to run is printed: half-preparing the file produced a .env compose refused to
# interpolate. Git for Windows first, and by path: "bash" on PATH is
# C:\Windows\System32\bash.exe on machines with WSL, a launcher that with no distribution
# installed answers "execvpe(/bin/bash) failed" and wins whenever System32 is first in PATH.
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

# Environment check: docker compose refuses to do anything when a required variable has no
# value, and names the first service it failed to interpolate rather than the file to edit.
# The names come from .env.example and the compose files, not from a copy here.
function Assert-Environment {
    $envFile = Join-Path $DockerDir ".env"
    $exampleFile = Join-Path $DockerDir ".env.example"

    # Any name a shell would accept, not upper case only: a pattern starting at [A-Z] excluded
    # DM_CryptoConfiguration__KeyBase64, the one variable compose dies on.
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

    # Presence is not enough: compose marks the values it cannot start without as ${NAME:?...},
    # and an empty value fails as hard as a missing line, which is the state a hand-made .env
    # arrives in. The names are read out of the compose files, which declare the requirement.
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

# The environment is prepared and then checked, in that order.
function Initialize-Environment {
    if (-not (Invoke-EnvironmentInit)) { exit 1 }
    Assert-Environment
}
