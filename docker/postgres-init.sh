#!/bin/bash
set -euo pipefail

# Creates the roles the application and the exporter connect as. Postgres runs this
# directory once, on an empty volume, so the one job left for the script is creating roles,
# which needs the superuser workloads must never hold: a leaked superuser password is every
# database on the server, the roles and COPY FROM PROGRAM. The database is created here
# and owned by the application role, because since Postgres 15 only the owner may CREATE
# in schema public.

if [ -z "${DM_APP_PASSWORD:-}" ]; then
    echo "ОШИБКА: DM_APP_PASSWORD не задан — роль приложения создать нечем." >&2
    exit 1
fi

if [ -z "${DM_EXPORTER_PASSWORD:-}" ]; then
    echo "ОШИБКА: DM_EXPORTER_PASSWORD не задан — роль экспортера создать нечем." >&2
    exit 1
fi

# ON_ERROR_STOP: a failed role must not leave the volume initialised and the stand up, as
# it surfaces later as a login that does not work.
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<-SQL
    CREATE ROLE dm_app LOGIN PASSWORD '${DM_APP_PASSWORD}';
    CREATE DATABASE dm3 OWNER dm_app;
    -- Pushes are notifications, which no privilege guards: a role that connects to dm3 hears
    -- every one and sends its own. The owner keeps the right, the superuser
    -- bypasses it.
    REVOKE CONNECT ON DATABASE dm3 FROM PUBLIC;

    -- pg_monitor reads the statistics views and no table of any database.
    CREATE ROLE dm_exporter LOGIN PASSWORD '${DM_EXPORTER_PASSWORD}';
    GRANT pg_monitor TO dm_exporter;
SQL

echo "Роли dm_app и dm_exporter созданы, база dm3 принадлежит dm_app."
