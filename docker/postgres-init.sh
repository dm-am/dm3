#!/bin/bash
set -euo pipefail

# Creates the roles the application and the exporter connect as.
#
# Postgres runs /docker-entrypoint-initdb.d exactly once, on an empty volume, so
# nothing here can reach a database that already exists. That leaves this script
# the one job it alone can do: creating the roles, which needs the superuser the
# workloads themselves must never hold.
#
# Every workload used to connect as postgres. A password reaching a log, a
# connection string in an image, an injection anywhere - and what the holder gets
# is not the data of this site but the server: every database on it, the roles,
# and COPY FROM PROGRAM. Nothing about that is visible while things work.
#
# The database is created here rather than by the migration, and owned by the
# application role. Since Postgres 15 the CREATE privilege on the public schema
# belongs to the database owner alone, so a database owned by postgres leaves the
# application unable to create the tables it is about to migrate.

if [ -z "${DM_APP_PASSWORD:-}" ]; then
    echo "ОШИБКА: DM_APP_PASSWORD не задан — роль приложения создать нечем." >&2
    exit 1
fi

if [ -z "${DM_EXPORTER_PASSWORD:-}" ]; then
    echo "ОШИБКА: DM_EXPORTER_PASSWORD не задан — роль экспортера создать нечем." >&2
    exit 1
fi

# psql with ON_ERROR_STOP: a role that failed to be created must not leave the
# volume initialised and the stand up, because the failure surfaces later as a
# login that does not work and a database that is empty.
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<-SQL
    CREATE ROLE dm_app LOGIN PASSWORD '${DM_APP_PASSWORD}';
    CREATE DATABASE dm3 OWNER dm_app;

    -- Reads the statistics views and nothing else: pg_monitor is a predefined
    -- role for exactly this, and it carries no access to any table of any
    -- database.
    CREATE ROLE dm_exporter LOGIN PASSWORD '${DM_EXPORTER_PASSWORD}';
    GRANT pg_monitor TO dm_exporter;
SQL

echo "Роли dm_app и dm_exporter созданы, база dm3 принадлежит dm_app."
