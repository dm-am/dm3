#!/bin/bash
# DM3 Management Script (Linux/Mac)
# Usage: ./scripts/dm.sh <command>
# Commands: start, stop, reset, seed, e2e, status, logs

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
DOCKER_DIR="$PROJECT_ROOT/docker"

# The addresses of the local services, one list for this script and for scripts/dm.ps1.
ADDRESSES_FILE="$SCRIPT_DIR/dm/addresses.txt"

print_addresses() {
    local name url
    while read -r name url; do
        case "$name" in
            '' | '#'*) continue ;;
        esac
        printf '  %-11s%s\n' "$name" "$url"
    done < "$ADDRESSES_FILE"
}

# The address of one service by the name in the list.
address_of() {
    local name url
    while read -r name url; do
        if [ "$name" = "$1" ]; then
            echo "$url"
            return 0
        fi
    done < "$ADDRESSES_FILE"
    return 1
}

API_URL="$(address_of API)"

show_help() {
    cat << EOF
DM3 Management Script

Usage: ./scripts/dm.sh <command> [service]

Commands:
  start     Start all Docker services (infrastructure + API)
  stop      Stop all Docker services
  reset     Stop, clear databases, and restart
  seed      Run seed scripts (requires running API)
  e2e       Recreate the API the way the e2e suite needs it: rate limiting off
  status    Show status of all services
  logs      Show logs (optionally for specific service)
  help      Show this help

Examples:
  ./scripts/dm.sh start          # Start everything
  ./scripts/dm.sh stop           # Stop everything
  ./scripts/dm.sh reset          # Fresh start with clean DB
  ./scripts/dm.sh seed           # Populate test data
  ./scripts/dm.sh e2e            # Stand for the e2e suite and the pre-push hook
  ./scripts/dm.sh logs dm-api    # Show API logs
  ./scripts/dm.sh status         # Check what's running

Docker Compose: $DOCKER_DIR/docker-compose.yml

Addresses (scripts/dm/addresses.txt, the frontend runs separately with npm):
EOF
    print_addresses
}

start_services() {
    echo -e "\033[36mDM3 Start\033[0m"

    # docker/.env comes from the script that also generates the encryption key: a bare copy of
    # the template carries it empty, and compose stops at ${...:?} before any container starts.
    bash "$DOCKER_DIR/scripts/init-env.sh" local

    cd "$DOCKER_DIR"
    docker compose up -d --build

    echo -e "\n\033[32mReady!\033[0m"
    echo ""
    print_addresses
    echo ""
    echo "  Test data:    ./scripts/dm.sh seed"
    echo "  Run frontend: npm --prefix \"$PROJECT_ROOT/src/DM.Web.Client\" run dev"
}

stop_services() {
    echo -e "\033[36mDM3 Stop\033[0m"

    # --remove-orphans: a service deleted from the compose file leaves its container behind,
    # which a plain "down" walks past.
    cd "$DOCKER_DIR"
    docker compose down --remove-orphans

    echo -e "\033[32mStopped.\033[0m"
}

reset_services() {
    echo -e "\033[36mDM3 Reset\033[0m"

    stop_services

    echo -e "\n\033[33mClearing database volumes\033[0m"
    cd "$DOCKER_DIR"
    docker compose down -v --remove-orphans

    echo -e "\033[32mDatabase volumes cleared.\033[0m"

    echo -e "\n\033[36mRestarting services\033[0m"
    start_services
}

run_seed() {
    echo -e "\033[36mDM3 Seed\033[0m"

    # The seeder is a console tool under the "tools" profile, which a plain "docker compose up"
    # never starts; it writes straight to Postgres and the object storage. The API must be up
    # for the restart at the end. The upload bucket is created by seaweedfs, not by the API.
    #
    # The liveness endpoint, the one the container healthcheck and CI ask, not a product route
    # that would tie the command to one controller keeping its path and staying anonymous.
    if ! curl -s --max-time 5 "$API_URL/_health" > /dev/null; then
        echo -e "\033[31mAPI not available at $API_URL\033[0m"
        echo "Start services first: ./scripts/dm.sh start"
        exit 1
    fi

    cd "$DOCKER_DIR"

    if ! docker compose run --rm -T --build seeder users; then
        echo -e "\033[31mError: user seeding failed\033[0m"
        exit 1
    fi

    if ! docker compose run --rm -T --build seeder content; then
        echo -e "\033[31mError: content seeding failed\033[0m"
        exit 1
    fi

    # The API computes its periodic projections (popularity, the best post of the week) at
    # start, so without this restart the site keeps showing what it worked out over the empty
    # database.
    echo -e "\n\033[36mRestarting API so it picks up the seeded data\033[0m"
    docker restart dm-api > /dev/null

    for _ in $(seq 1 30); do
        if curl -s --max-time 2 "$API_URL/_health" > /dev/null; then
            break
        fi
        sleep 1
    done

    if ! curl -s --max-time 2 "$API_URL/_health" > /dev/null; then
        echo -e "\033[31mError: API did not come back after the restart\033[0m"
        echo "Logs: ./scripts/dm.sh logs dm-api"
        exit 1
    fi

    echo ""
    echo -e "\033[36mPassword: Test123!\033[0m"
}

# The API the way the end-to-end suite meets it in CI: this tree's build, with rate limiting
# off (one page load is a dozen and more requests against a hundred per address per minute,
# so the suite would fail on 429s that read like stale selectors). CI switches the limits off
# with a compose overlay written in the e2e job of .github/workflows/dotnet.yml; this hands
# compose the same switch on stdin rather than as a file in docker/, whose files are the set
# that gets deployed. The preview origin the CI overlay lets in needs nothing here: the local
# stack runs Development, where the API admits both development ports. Only the API is
# recreated, and the next "start" recreates it without the overlay, which is how the limits
# come back. scripts/check-e2e-stand.sh reads the same variable off the container before the
# pre-push hook runs the suite.
e2e_stand() {
    echo -e "\033[36mDM3 E2E (recreating the API, rate limiting off)\033[0m"

    # The commit the image is built on, written into the container for
    # scripts/check-e2e-stand.sh: a server change committed after the build would otherwise be
    # pushed without the suite ever meeting it.
    local source_commit
    source_commit="$(git -C "$PROJECT_ROOT" rev-parse HEAD 2> /dev/null || echo unknown)"

    cd "$DOCKER_DIR"
    printf 'services:\n  dmapi:\n    environment:\n      DM_RateLimiting__Enabled: "false"\n      E2E_SOURCE_COMMIT: "%s"\n' "$source_commit" |
        docker compose -f docker-compose.yml -f - up -d --build --no-deps --wait dmapi

    echo ""
    echo -e "\033[32mRate limiting is off until the next start.\033[0m"
    echo "Suite: npm --prefix \"$PROJECT_ROOT/src/DM.Web.Client\" run test:e2e:fast"
}

show_status() {
    echo -e "\033[36mDM3 Status\033[0m"
    echo ""

    docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" --filter "name=dm-"

    echo ""
}

show_logs() {
    local service="$1"
    if [ -n "$service" ]; then
        docker logs "$service" --tail 100 -f
    else
        cd "$DOCKER_DIR"
        docker compose logs --tail 50 -f
    fi
}

# Main
case "${1:-help}" in
    start)  start_services ;;
    stop)   stop_services ;;
    reset)  reset_services ;;
    seed)   run_seed ;;
    e2e)    e2e_stand ;;
    status) show_status ;;
    logs)   show_logs "$2" ;;
    help|*) show_help ;;
esac
