$ErrorActionPreference = "Continue"
dotnet build -c Release --no-incremental 2>&1 | Select-String "warning CS86" | Select-String "test\\"
