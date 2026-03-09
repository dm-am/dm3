#!/bin/bash
# DM3 Management Script (Linux/Mac)
# Usage: ./scripts/dm.sh <command>
# Commands: start, stop, reset, seed, status, logs

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
  status    Show status of all services
  logs      Show logs (optionally for specific service)
  help      Show this help

Examples:
  ./scripts/dm.sh start          # Start everything
  ./scripts/dm.sh stop           # Stop everything
  ./scripts/dm.sh reset          # Fresh start with clean DB
  ./scripts/dm.sh seed           # Populate test data
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

    # Check .env file
    if [ ! -f "$DOCKER_DIR/.env" ]; then
        echo -e "\033[33mCreating .env file from template...\033[0m"
        cp "$DOCKER_DIR/.env.example" "$DOCKER_DIR/.env"
        echo -e "\033[33mPlease edit docker/.env and set secure passwords!\033[0m"
    fi

    cd "$DOCKER_DIR"
    docker compose up -d --build

    echo -e "\n\033[32mServices started!\033[0m"
    echo "API: http://localhost:5000"
    echo "Swagger: http://localhost:5000/swagger"
    echo "MinIO: http://localhost:9001"
    echo "RabbitMQ: http://localhost:15672"
    echo ""
    echo "Note: Frontend runs separately - cd src/DM.Web.Client && npm run dev"
}

stop_services() {
    echo -e "\033[36mStopping DM3 services...\033[0m"

    cd "$DOCKER_DIR"
    docker compose down

    echo -e "\033[32mServices stopped.\033[0m"
}

reset_services() {
    echo -e "\033[36mResetting DM3 (stop, clear DB, restart)...\033[0m"

    # Stop services
    stop_services

    # Clear volumes
    echo -e "\n\033[33mClearing database volumes...\033[0m"
    cd "$DOCKER_DIR"
    docker compose down -v

    echo -e "\033[32mDatabase volumes cleared.\033[0m"

    # Restart
    echo -e "\n\033[36mRestarting services...\033[0m"
    start_services
}

run_seed() {
    echo -e "\033[36mSeeding test data...\033[0m"

    # Check if API is running
    if ! curl -s --max-time 5 "http://localhost:5000/v1/boards" > /dev/null; then
        echo -e "\033[31mError: API is not available at http://localhost:5000\033[0m"
        echo "Start services first: ./scripts/dm.sh start"
        exit 1
    fi

    # Call seed endpoint directly
    response=$(curl -s -X POST "http://localhost:5000/v1/moderation/seed" -H "Content-Type: application/json")

    if [ $? -ne 0 ]; then
        echo -e "\033[31mError: Failed to call seed endpoint\033[0m"
        exit 1
    fi

    # Parse and display results (requires jq)
    if command -v jq &> /dev/null; then
        created=$(echo "$response" | jq -r '.created')
        skipped=$(echo "$response" | jq -r '.skipped')
        echo ""
        echo -e "\033[32mCreated: $created\033[0m"
        echo -e "\033[33mSkipped: $skipped (already exist)\033[0m"

        createdLogins=$(echo "$response" | jq -r '.createdLogins[]?' 2>/dev/null)
        if [ -n "$createdLogins" ]; then
            echo ""
            echo -e "\033[32mCreated users:\033[0m"
            echo "$createdLogins" | while read -r login; do
                echo "  + $login"
            done
        fi
    else
        echo "$response"
    fi

    echo ""
    echo -e "\033[36mPassword: Test123!\033[0m"
    echo "All users are newbies (0 posts)"
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
    status) show_status ;;
    logs)   show_logs "$2" ;;
    help|*) show_help ;;
esac
