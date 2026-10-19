# The e2e command of scripts/dm.ps1, which dot-sources this file.

# The API the way the end-to-end suite meets it in CI: this tree's build, with rate limiting
# off. One page load is a dozen and more API requests against a budget of a hundred per
# address per minute, so under the limits the suite fails on 429s that read like stale
# selectors. CI switches them off with a compose overlay written in the e2e job of
# .github/workflows/dotnet.yml; this writes the same switch into a temporary file rather
# than into docker/, whose files are the set that gets deployed.
#
# Only the API is recreated, and the next "start" recreates it without the overlay, which is
# how the limits come back. The preview origin the CI overlay lets in needs nothing here:
# the local stack runs Development, where the API admits both development ports.
# scripts/check-e2e-stand.sh reads the same variable off the container before the pre-push
# hook runs the suite.
function Start-EndToEndStand {
    Write-Host ""
    Write-Host "DM3 E2E" -ForegroundColor Cyan
    Write-Host ""

    # The commit the image is built on, written into the container for
    # scripts/check-e2e-stand.sh: nothing rebuilds the API after this, so a server change
    # committed later would be pushed without the suite ever meeting it.
    $sourceCommit = & git -C $ProjectRoot rev-parse HEAD 2>$null
    if (-not $sourceCommit) { $sourceCommit = "unknown" }

    $overlay = Join-Path ([System.IO.Path]::GetTempPath()) "dm3-e2e-overlay.yml"
    Set-Content -Path $overlay -Encoding ASCII -Value @(
        "services:",
        "  dmapi:",
        "    environment:",
        "      DM_RateLimiting__Enabled: `"false`"",
        "      E2E_SOURCE_COMMIT: `"$sourceCommit`""
    )

    Push-Location $DockerDir
    try {
        $arguments = "compose -f docker-compose.yml -f `"$overlay`" up -d --build --no-deps --wait dmapi"
        if (-not (Invoke-Step -Label "API for e2e" -Command $script:DockerPath -Arguments $arguments)) {
            Write-Host "    Logs: powershell -File .\scripts\dm.ps1 logs dm-api" -ForegroundColor DarkGray
            exit 1
        }
    } finally {
        Pop-Location
        Remove-Item -Path $overlay -ErrorAction SilentlyContinue
    }

    Write-Host ""
    Write-Host "Ready!" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Rate limiting is off until the next start."
    Write-Host "  Suite: npm --prefix `"$ProjectRoot\src\DM.Web.Client`" run test:e2e:fast"
    Write-Host ""
}
