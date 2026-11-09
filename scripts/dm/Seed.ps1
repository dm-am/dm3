# The seed command of scripts/dm.ps1, which dot-sources this file.

function Get-SeedSummary {
    param(
        [string]$Output,
        [string]$Prefix
    )

    # The seeder prints one "<prefix>: ..." summary line per command, already filtered down
    # to what it created.
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

    $apiUrl = (Get-Addresses)["API"]

    # The liveness endpoint, not a product route: seeding writes straight to Postgres, but
    # the restart at the end needs the API, and a product route would tie the command to one
    # controller keeping its path and staying anonymous. /_health is what the container
    # healthcheck and CI ask.
    $curlResult = curl.exe -s -o NUL -w "%{http_code}" "$apiUrl/_health" 2>$null
    if ($curlResult -ne "200") {
        Write-Host "  API not available. Run first: powershell -File .\scripts\dm.ps1 start" -ForegroundColor Red
        Write-Host ""
        exit 1
    }

    # Seeding runs in its own container (tools profile), not over HTTP; --build keeps the
    # image in step with the seed data in the sources.
    Push-Location $DockerDir
    try {
        $response = Invoke-Step -CaptureOutput -Label "Users" -Command $script:DockerPath -Arguments "compose run --rm -T --build seeder users"
        if ($null -eq $response) { exit 1 }
        Write-CompletedStep -Label "Users" -Total 1 -Detail (Get-SeedSummary -Output $response -Prefix "users")

        $compResponse = Invoke-Step -CaptureOutput -Label "Content" -Command $script:DockerPath -Arguments "compose run --rm -T --build seeder content"
        if ($null -eq $compResponse) { exit 1 }
        Write-CompletedStep -Label "Content" -Total 1 -Detail (Get-SeedSummary -Output $compResponse -Prefix "content")
    } finally {
        Pop-Location
    }

    if (-not (Invoke-Step -Label "Restart API" -Command $script:DockerPath -Arguments "restart dm-api")) {
        exit 1
    }

    $apiReady = Wait-SimpleStep -Label "API Ready" -TimeoutSeconds 30 -Check {
        $checkResult = curl.exe -s -o NUL -w "%{http_code}" "$apiUrl/_health" 2>$null
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
