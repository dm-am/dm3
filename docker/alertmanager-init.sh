#!/bin/sh
set -eu

# Entrypoint of the alertmanager container. Alertmanager expands no environment variable
# and has no include mechanism, so the smarthost, mailbox and relay credentials would have
# to be committed. prometheus/alertmanager.yml carries ${...} placeholders instead, this
# renders them from the environment, and the process reads the rendered copy.
#
# --render-only renders and stops, which is how CI validates the result with amtool:
# nothing else starts this container until a server does.

TEMPLATE="${ALERTMANAGER_TEMPLATE:-/etc/alertmanager/alertmanager.yml}"
RENDERED="${ALERTMANAGER_CONFIG:-/tmp/alertmanager.yml}"

# The defaults live here only: the container has to start on its own, as the gate runs
# it, and under "set -u" a variable compose did not pass would take it down.
SMARTHOST="${ALERT_SMTP_SMARTHOST:-dm-mailpit:1025}"
EMAIL_FROM="${ALERT_EMAIL_FROM:-alertmanager@dm.am}"
EMAIL_TO="${ALERT_EMAIL_TO:-alerts@dm.am}"
SMTP_USERNAME="${ALERT_SMTP_USERNAME:-}"
SMTP_PASSWORD="${ALERT_SMTP_PASSWORD:-}"
REQUIRE_TLS="${ALERT_SMTP_REQUIRE_TLS:-false}"

if [ ! -f "$TEMPLATE" ]; then
    echo "ERROR: $TEMPLATE is not mounted: alertmanager has nothing to route with" >&2
    exit 1
fi

# A replacement is a sed replacement, not a literal: a password with a backslash, an
# ampersand or the delimiter would otherwise be rewritten on its way into the file.
escape() {
    printf '%s' "$1" | sed -e 's/[\\&|]/\\&/g'
}

sed \
    -e "s|\${ALERT_SMTP_SMARTHOST}|$(escape "$SMARTHOST")|g" \
    -e "s|\${ALERT_EMAIL_FROM}|$(escape "$EMAIL_FROM")|g" \
    -e "s|\${ALERT_EMAIL_TO}|$(escape "$EMAIL_TO")|g" \
    -e "s|\${ALERT_SMTP_USERNAME}|$(escape "$SMTP_USERNAME")|g" \
    -e "s|\${ALERT_SMTP_PASSWORD}|$(escape "$SMTP_PASSWORD")|g" \
    -e "s|\${ALERT_SMTP_REQUIRE_TLS}|$(escape "$REQUIRE_TLS")|g" \
    "$TEMPLATE" > "$RENDERED"

# A placeholder this script does not render survives as the literal "${ALERT_...}", which
# alertmanager accepts as a smarthost nobody owns. The bracket is the literal dollar,
# since a backslash-escaped one reads to shellcheck as an intended expansion.
if grep -q '[$]{ALERT_' "$RENDERED"; then
    echo "ERROR: $TEMPLATE names a placeholder this script does not render:" >&2
    grep -n '[$]{ALERT_' "$RENDERED" >&2
    exit 1
fi

if [ "${1:-}" = "--render-only" ]; then
    echo "rendered $TEMPLATE to $RENDERED"
    exit 0
fi

# Silences and the notification log live in the volume, or a restart re-sends every firing
# alert and forgets the silences. Clustering is off: one instance, and the gossip listener
# would cost an open port and a two-second settle on every start.
exec /bin/alertmanager \
    --config.file="$RENDERED" \
    --storage.path=/alertmanager \
    --cluster.listen-address=""
