FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG PROJECT_NAME

# The architecture BuildKit builds for (amd64 or arm64). Restore and publish both name it,
# so the output carries the native libraries of that platform and not all seven of
# Magick.NET (200 MB together).
ARG TARGETARCH

WORKDIR /app

# 1. Solution and project files first, for restore caching. The compiler policy (props and
# .editorconfig) has to reach this build too, or it fails on warnings a developer never sees.
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

# 2. Restore for the architecture publish names below: a restore for another one leaves
# publish --no-restore without its target (NETSDK1047).
RUN dotnet restore src/${PROJECT_NAME}/${PROJECT_NAME}.csproj -a ${TARGETARCH}

# 3. Copy source code AFTER restore (only code changes invalidate this layer)
COPY src/ src/

# The commit the image was built from, stamped into the assembly. Declared here and not at
# the top because an argument that changes every commit invalidates every layer below it.
# Unset it is empty and the reader says "unknown".
ARG SOURCE_REVISION

# 4. Publish. --no-restore: restore already ran for this project. --no-self-contained: the
# runtime comes with the aspnet image.
RUN dotnet publish src/${PROJECT_NAME}/${PROJECT_NAME}.csproj -c Release -o out --no-restore -a ${TARGETARCH} --no-self-contained -p:SourceRevisionId=${SOURCE_REVISION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

ARG PROJECT_NAME

WORKDIR /app

# Ahead of the COPY, so the package index is not fetched again on every source change. curl
# is for the healthcheck the compose files declare.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# useradd, not adduser: the slim Debian 13 base of aspnet:10.0 no longer ships the wrapper.
RUN useradd --no-create-home --shell /usr/sbin/nologin dmuser

# Where the API keeps the country table it downloads. Owned by dmuser because docker gives a
# new named volume the owner of the directory it mounts over, and root would block the write.
RUN mkdir -p /data/geo && chown dmuser /data/geo

COPY --from=build /app/out ./
USER dmuser

# A stage sees only the arguments it declares, so the one of the build stage does not reach
# here. The release reads the image's revision from this label. Unset it is empty.
ARG SOURCE_REVISION
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}"

ENV RUNTIME_PROJECT=${PROJECT_NAME}.dll

# exec, so dotnet becomes PID 1 and hears SIGTERM; with the shell as PID 1 every stop ended
# in SIGKILL. The shell stays because the exec form of ENTRYPOINT expands no variables.
ENTRYPOINT ["sh", "-c", "exec dotnet ${RUNTIME_PROJECT}"]
