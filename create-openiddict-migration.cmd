@echo off
cd src\DM.Services.DataAccess
dotnet ef migrations add AddOpenIddict --startup-project ..\DM.Web.API
cd ..\..
echo.
echo Migration created successfully. To apply it, run:
echo dotnet ef database update --project src\DM.Services.DataAccess --startup-project src\DM.Web.API
