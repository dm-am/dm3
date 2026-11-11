#!/bin/bash
set -euo pipefail

# Who may connect to the application database, read from the running server. Pushes are
# notifications, and any role that connects to dm3 hears every one and sends its own.
# postgres-init.sh takes the right from PUBLIC only when it initialises an empty
# volume; a restore into an existing database does not carry the revoke, so the pipeline
# (deployment-smoke) and an operator after a restore read it back here.
#
# Usage:
#   check-database-privileges.sh
#
# PG_CONTAINER names the database container for a stand under another project name.

CONTAINER="${PG_CONTAINER:-dm-pg}"

# The superuser over the local socket bypasses the privilege it reads. The exporter is
# named on its own: a grant to it would pass the PUBLIC column.
got="$(docker exec "$CONTAINER" psql -U postgres -d postgres -tAX -v ON_ERROR_STOP=1 -c "
    SELECT has_database_privilege('public', 'dm3', 'CONNECT'),
           has_database_privilege('dm_exporter', 'dm3', 'CONNECT'),
           has_database_privilege('dm_app', 'dm3', 'CONNECT')")"

if [ "$got" != "f|f|t" ]; then
    echo "FAIL: CONNECT on dm3 for PUBLIC|dm_exporter|dm_app is $got, expected f|f|t." >&2
    echo "After a restore: REVOKE CONNECT ON DATABASE dm3 FROM PUBLIC, as postgres." >&2
    exit 1
fi
echo "ok: only dm_app may connect to dm3"
