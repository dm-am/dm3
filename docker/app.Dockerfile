FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG PROJECT_NAME

WORKDIR /app

# 1. Copy solution and project files FIRST (for restore caching)
# The compiler policy the local build reads has to reach this one too: the
# props carry TreatWarningsAsErrors and .editorconfig carries the exemptions it
# is calibrated against. Without the latter the image build fails on warnings a
# developer never sees, and the failure names a source file rather than a
# missing file.
COPY DM.sln Directory.Build.props Directory.Packages.props .editorconfig ./

# Domain projects
COPY src/DM.Domain.Core/DM.Domain.Core.csproj src/DM.Domain.Core/
COPY src/DM.Domain.Account/DM.Domain.Account.csproj src/DM.Domain.Account/
COPY src/DM.Domain.Blog/DM.Domain.Blog.csproj src/DM.Domain.Blog/
COPY src/DM.Domain.Community/DM.Domain.Community.csproj src/DM.Domain.Community/
COPY src/DM.Domain.Forum/DM.Domain.Forum.csproj src/DM.Domain.Forum/
COPY src/DM.Domain.Game/DM.Domain.Game.csproj src/DM.Domain.Game/
COPY src/DM.Domain.Messaging/DM.Domain.Messaging.csproj src/DM.Domain.Messaging/
COPY src/DM.Domain.Moderation/DM.Domain.Moderation.csproj src/DM.Domain.Moderation/
COPY src/DM.Domain.Personal/DM.Domain.Personal.csproj src/DM.Domain.Personal/

# Infrastructure projects
COPY src/DM.Infrastructure.Core/DM.Infrastructure.Core.csproj src/DM.Infrastructure.Core/
COPY src/DM.Infrastructure.Mail/DM.Infrastructure.Mail.csproj src/DM.Infrastructure.Mail/
COPY src/DM.Infrastructure.Messaging/DM.Infrastructure.Messaging.csproj src/DM.Infrastructure.Messaging/
COPY src/DM.Infrastructure.Persistence/DM.Infrastructure.Persistence.csproj src/DM.Infrastructure.Persistence/

# Web, Workers and Tools
COPY src/DM.Web.API/DM.Web.API.csproj src/DM.Web.API/
COPY src/DM.Workers.Mail/DM.Workers.Mail.csproj src/DM.Workers.Mail/
COPY src/DM.Workers.NotificationDispatcher/DM.Workers.NotificationDispatcher.csproj src/DM.Workers.NotificationDispatcher/
COPY src/DM.Tools.Seeder/DM.Tools.Seeder.csproj src/DM.Tools.Seeder/

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
