param([string]$Project)
$ErrorActionPreference = "Continue"
dotnet build "test\$Project\$Project.csproj" -c Release --no-incremental 2>&1 | Select-String "warning CS86"
