$ErrorActionPreference = "Continue"

Write-Host "Building test projects..." -ForegroundColor Cyan

$projects = @(
    "DM.Services.Community.Tests",
    "DM.Services.Authentication.Tests",
    "DM.Services.Forum.Tests",
    "DM.Services.Common.Tests"
)

$hasWarnings = $false

foreach ($project in $projects) {
    Write-Host "`nBuilding $project..." -ForegroundColor Yellow
    $output = dotnet build "test\$project\$project.csproj" -c Release --no-incremental 2>&1 | Out-String
    $warnings = $output | Select-String "warning CS86.*test\\"

    if ($warnings) {
        Write-Host "Found CS86XX warnings in $project`:" -ForegroundColor Red
        $warnings | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        $hasWarnings = $true
    } else {
        Write-Host "No CS86XX warnings in $project" -ForegroundColor Green
    }
}

if (-not $hasWarnings) {
    Write-Host "`nAll test projects built successfully without CS86XX warnings!" -ForegroundColor Green
}
