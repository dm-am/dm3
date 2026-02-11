FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG PROJECT_NAME

WORKDIR /app

# 1. Copy solution and project files FIRST (for restore caching)
COPY DM.sln Directory.Build.props Directory.Packages.props ./
COPY src/DM.Services.Core/DM.Services.Core.csproj src/DM.Services.Core/
COPY src/DM.Services.DataAccess/DM.Services.DataAccess.csproj src/DM.Services.DataAccess/
COPY src/DM.Services.Authentication/DM.Services.Authentication.csproj src/DM.Services.Authentication/
COPY src/DM.Services.Common/DM.Services.Common.csproj src/DM.Services.Common/
COPY src/DM.Services.Community/DM.Services.Community.csproj src/DM.Services.Community/
COPY src/DM.Services.Forum/DM.Services.Forum.csproj src/DM.Services.Forum/
COPY src/DM.Services.Game/DM.Services.Game.csproj src/DM.Services.Game/
COPY src/DM.Services.Uploading/DM.Services.Uploading.csproj src/DM.Services.Uploading/
COPY src/DM.Services.MessageQueuing/DM.Services.MessageQueuing.csproj src/DM.Services.MessageQueuing/
COPY src/DM.Services.Notifications/DM.Services.Notifications.csproj src/DM.Services.Notifications/
COPY src/DM.Services.Notifications.Consumer/DM.Services.Notifications.Consumer.csproj src/DM.Services.Notifications.Consumer/
COPY src/DM.Services.Mail.Rendering/DM.Services.Mail.Rendering.csproj src/DM.Services.Mail.Rendering/
COPY src/DM.Services.Mail.Sender/DM.Services.Mail.Sender.csproj src/DM.Services.Mail.Sender/
COPY src/DM.Services.Mail.Sender.Consumer/DM.Services.Mail.Sender.Consumer.csproj src/DM.Services.Mail.Sender.Consumer/
COPY src/DM.Services.Search/DM.Services.Search.csproj src/DM.Services.Search/
COPY src/DM.Services.Search.Consumer/DM.Services.Search.Consumer.csproj src/DM.Services.Search.Consumer/
COPY src/DM.Web.API/DM.Web.API.csproj src/DM.Web.API/
COPY src/DM.Web.Core/DM.Web.Core.csproj src/DM.Web.Core/

# 2. Restore packages
RUN dotnet restore src/${PROJECT_NAME}/${PROJECT_NAME}.csproj

# 3. Copy source code AFTER restore (only code changes invalidate this layer)
COPY src/ src/

# 4. Publish
RUN dotnet publish src/${PROJECT_NAME}/${PROJECT_NAME}.csproj -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

ARG PROJECT_NAME

WORKDIR /app
COPY --from=build /app/out ./

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

RUN adduser --disabled-password --no-create-home dmuser
USER dmuser

ENV RUNTIME_PROJECT=${PROJECT_NAME}.dll
ENTRYPOINT ["sh", "-c", "dotnet ${RUNTIME_PROJECT}"]
