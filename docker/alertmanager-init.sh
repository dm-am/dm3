#!/bin/sh
set -eu

# Entrypoint of the alertmanager container.
#
# Alertmanager reads its configuration file verbatim: it expands no environment
# variable and has no include mechanism, so the smarthost, the mailbox and the
# credentials of a relay would have to be written into the repository. They are
# not: prometheus/alertmanager.yml carries ${...} placeholders, this renders them
# from the environment before the process starts, and the process reads the
# rendered copy.
#
# --render-only renders and stops. That is how CI validates the result with
# amtool: a template that cannot produce a configuration alertmanager accepts has
# to fail in a gate, because nothing else starts this container until a server
# does - and a container that will not start is a receiver that is not there.

TEMPLATE="${ALERTMANAGER_TEMPLATE:-/etc/alertmanager/alertmanager.yml}"
RENDERED="${ALERTMANAGER_CONFIG:-/tmp/alertmanager.yml}"

# The defaults live here as well as in compose. The container has to be startable
# on its own - that is what the gate runs - and under "set -u" a variable compose
# did not pass would take it down instead.
SMARTHOST="${ALERT_SMTP_SMARTHOST:-dm-mailhog:1025}"
EMAIL_FROM="${ALERT_EMAIL_FROM:-alertmanager@dm.am}"
EMAIL_TO="${ALERT_EMAIL_TO:-alerts@dm.am}"
SMTP_USERNAME="${ALERT_SMTP_USERNAME:-}"
SMTP_PASSWORD="${ALERT_SMTP_PASSWORD:-}"
REQUIRE_TLS="${ALERT_SMTP_REQUIRE_TLS:-false}"

if [ ! -f "$TEMPLATE" ]; then
    echo "ERROR: $TEMPLATE is not mounted: alertmanager has nothing to route with" >&2
    exit 1
fi

# A replacement is a sed replacement, not a literal: a password containing a
# backslash, an ampersand or the delimiter would otherwise be rewritten on its
# way into the file, and the failure would be an authentication error nobody
# connects to this script.
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

# A placeholder this script does not know about survives the render and reaches
# alertmanager as the literal text "${ALERT_...}". For a string field that is a
# smarthost nobody owns and mail that goes nowhere, with the configuration
# accepted and the container healthy.
# The bracket is the literal dollar: a backslash-escaped one reads to shellcheck
# as an expansion that was meant to happen.
if grep -q '[$]{ALERT_' "$RENDERED"; then
    echo "ERROR: $TEMPLATE names a placeholder this script does not render:" >&2
    grep -n '[$]{ALERT_' "$RENDERED" >&2
    exit 1
fi

if [ "${1:-}" = "--render-only" ]; then
    echo "rendered $TEMPLATE to $RENDERED"
    exit 0
fi

# Silences and the notification log live in the volume: without it a restart
# re-sends every firing alert and forgets every silence somebody set during the
# incident that is still going on.
#
# Clustering off: one instance, and the gossip listener defaults to on, so the
# stack would carry an open port and a two-second settle on every start for a
# peer that does not exist.
exec /bin/alertmanager \
    --config.file="$RENDERED" \
    --storage.path=/alertmanager \
    --cluster.listen-address=""
