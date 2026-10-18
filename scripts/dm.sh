#!/bin/bash
# DM3 Management Script (Linux/Mac)
# Usage: ./scripts/dm.sh <command>
# Commands: start, stop, reset, seed, e2e, status, logs

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
DOCKER_DIR="$PROJECT_ROOT/docker"

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

Environment:
  Docker Compose: $DOCKER_DIR/docker-compose.yml
  API: http://localhost:5000
  Frontend: http://localhost:5173 (run separately with npm)
EOF
}

start_services() {
    echo -e "\033[36mStarting DM3 services...\033[0m"

    # docker/.env is created by the one script that also generates the
    # encryption key. Copying the template alone is half the job: it ships that
    # key empty on purpose, and compose declares it through ${...:?}, which
    # rejects an empty value as hard as a missing one. So the documented first
    # run on Linux and macOS died on interpolation before a single container
    # started, on a file this script had just created.
    bash "$DOCKER_DIR/scripts/init-env.sh" local

    cd "$DOCKER_DIR"
    docker compose up -d --build

    echo -e "\n\033[32mServices started!\033[0m"
    echo "API: http://localhost:5000"
    echo "Swagger: http://localhost:5000/swagger"
    echo "Storage: http://localhost:9000"
    echo "RabbitMQ: http://localhost:15672"
    echo ""
    echo "Note: Frontend runs separately - cd src/DM.Web.Client && npm run dev"
}

stop_services() {
    echo -e "\033[36mStopping DM3 services...\033[0m"

    # --remove-orphans: a service deleted from the compose file leaves its
    # container behind and a plain "down" walks past it, so it survives every
    # stop and every reset after that, holding its name and its place on the
    # network.
    cd "$DOCKER_DIR"
    docker compose down --remove-orphans

    echo -e "\033[32mServices stopped.\033[0m"
}

reset_services() {
    echo -e "\033[36mResetting DM3 (stop, clear DB, restart)...\033[0m"

    # Stop services
    stop_services

    # Clear volumes
    echo -e "\n\033[33mClearing database volumes...\033[0m"
    cd "$DOCKER_DIR"
    docker compose down -v --remove-orphans

    echo -e "\033[32mDatabase volumes cleared.\033[0m"

    # Restart
    echo -e "\n\033[36mRestarting services...\033[0m"
    start_services
}

run_seed() {
    echo -e "\033[36mSeeding test data...\033[0m"

    # The seeder is a console tool, not an HTTP endpoint: it writes straight to
    # Postgres and the object storage, so it must not be reachable over
    # the network. It runs under the "tools" compose profile, which never starts
    # with a plain "docker compose up". The API still has to be up: seeding
    # itself does not need it, but the restart at the end of this function does.
    # The upload bucket is created by seaweedfs on its own start, not by the API.
    #
    # The liveness endpoint, the one the container healthcheck and CI already
    # ask, rather than a product route: this used to be pinned to /v1/boards,
    # so the command depended on one controller keeping its path and staying
    # anonymous. The two management scripts asked two different routes for the
    # same thing.
    if ! curl -s --max-time 5 "http://localhost:5000/_health" > /dev/null; then
        echo -e "\033[31mError: API is not available at http://localhost:5000\033[0m"
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

    # The API computes its periodic projections at start - popularity, the best
    # post of the week - so without this restart the site keeps showing what it
    # worked out over the empty database, and there is nothing on screen to say
    # so. dm.ps1 has always restarted it here and this script never did, which
    # is two different outcomes from one documented command.
    echo -e "\n\033[36mRestarting API so it picks up the seeded data...\033[0m"
    docker restart dm-api > /dev/null

    for _ in $(seq 1 30); do
        if curl -s --max-time 2 "http://localhost:5000/_health" > /dev/null; then
            break
        fi
        sleep 1
    done

    if ! curl -s --max-time 2 "http://localhost:5000/_health" > /dev/null; then
        echo -e "\033[31mError: API did not come back after the restart\033[0m"
        echo "Logs: ./scripts/dm.sh logs dm-api"
        exit 1
    fi

    echo ""
    echo -e "\033[36mPassword: Test123!\033[0m"
}

# The API the way the end-to-end suite meets it in CI: this tree's build, with
# rate limiting off. One page load is a dozen and more API requests against a
# budget of a hundred per address per minute, so under the limits the suite
# fails on 429s that read exactly like stale selectors. CI switches them off
# with a compose overlay written in the e2e job of .github/workflows/dotnet.yml;
# this hands compose the same switch on stdin rather than as a file in docker/,
# because the files there are the set that gets deployed and a switch that must
# never reach a server does not belong among them.
#
# The preview origin the CI overlay also lets in needs nothing here: the local
# stack runs Development, and the API admits both development ports there.
#
# Only the API is recreated. The next "start" recreates it without the overlay,
# which is how the limits come back. scripts/check-e2e-stand.sh reads the same
# variable off the running container before the pre-push hook runs the suite.
e2e_stand() {
    echo -e "\033[36mRecreating the API for the e2e suite (rate limiting off)...\033[0m"

    # The commit the image is built on, written into the container for
    # scripts/check-e2e-stand.sh: nothing rebuilds the API after this, and a
    # server change committed later would be pushed without the suite ever
    # meeting it.
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
    echo -e "\033[36mDM3 Service Status\033[0m"
    echo "=================="
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
