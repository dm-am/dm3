FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG PROJECT_NAME

WORKDIR /app

# 1. Copy solution and project files FIRST (for restore caching)
# The compiler policy the local build reads has to reach this one too: the
# props carry TreatWarningsAsErrors and .editorconfig carries the exemptions it
# is calibrated against. Without the latter the image build fails on warnings a
# developer never sees, and the failure names a source file rather than a
# missing file.
COPY DM.sln Directory.Build.props Directory.Packages.props .editorconfig ./

# Vendored third-party sources
COPY src/BBCodeParser/BBCodeParser.csproj src/BBCodeParser/

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

# The commit the image was built from, stamped into the assembly so the process can
# name its own release. Declared here rather than at the top: an argument that
# changes on every commit invalidates every layer below it, and the restore above
# is the expensive one.
#
# Left unset it is empty, which is what a local build wants - the reader turns into
# an explicit "unknown" rather than a plausible wrong answer.
ARG SOURCE_REVISION

# 4. Publish. --no-restore: the restore above already ran for this project, and
# without the flag publish does the whole of it a second time on every build.
RUN dotnet publish src/${PROJECT_NAME}/${PROJECT_NAME}.csproj -c Release -o out --no-restore -p:SourceRevisionId=${SOURCE_REVISION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

ARG PROJECT_NAME

WORKDIR /app

# Ahead of the COPY: neither layer depends on anything in the repository, and
# below it the package index was fetched again on every change to a single
# source file. curl is here for the healthcheck the compose files declare.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# useradd, not adduser: the Debian 13 base of aspnet:10.0 no longer ships the
# adduser wrapper in its slim variant, while useradd is part of passwd and stays.
RUN useradd --no-create-home --shell /usr/sbin/nologin dmuser

COPY --from=build /app/out ./
USER dmuser

ENV RUNTIME_PROJECT=${PROJECT_NAME}.dll

# exec, so that dotnet replaces the shell and becomes PID 1. Without it the shell
# is PID 1, docker stop delivers SIGTERM to the shell alone, and .NET never hears
# it: no host shutdown, no log flush, no finishing the message already in hand.
# Every stop ended in SIGKILL once the grace period ran out. The shell itself stays
# because the exec form of ENTRYPOINT expands no variables.
ENTRYPOINT ["sh", "-c", "exec dotnet ${RUNTIME_PROJECT}"]
