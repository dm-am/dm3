#!/bin/bash
set -euo pipefail

# DM3 server setup for a clean Ubuntu: the host part. It installs the packages, caps the
# container logs, opens the ports and checks the tool versions the release script relies on,
# then hands over to docker/scripts/install-release.sh, which lays out the server and installs
# the first signed release.
#
# Usage, as root, from a clone checked out at the release tag after its signature verified:
#   sudo bash docker/setup-server.sh --signers <allowed_signers> --tag <vX.Y.Z> --mail-host <host> --mail-port <port>
# Not from a pipe: what runs as root here is the tree whose tag the operator verified.

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

[ "$(id -u)" -eq 0 ] || { echo "Error: setup-server.sh runs as root (sudo bash docker/setup-server.sh ...)" >&2; exit 1; }

# The answers the layout part refuses to start without are asked for here too, before apt
# and a docker restart change the machine.
case " $* " in
    *" --signers "*) ;;
    *) echo "Error: --signers names the allowed_signers file of the release key" >&2; exit 2 ;;
esac
case " $* " in
    *" --tag "*) ;;
    *) echo "Error: --tag names the release to install" >&2; exit 2 ;;
esac
case " $* " in
    *" --mail-host "*) ;;
    *) echo "Error: --mail-host names the mail relay of the server" >&2; exit 2 ;;
esac
case " $* " in
    *" --mail-port "*) ;;
    *) echo "Error: --mail-port names the port of the mail relay" >&2; exit 2 ;;
esac

# Asked before anything is installed: a repeat run used to die halfway, after apt and a docker
# restart on a live stand.
bash "$HERE/scripts/require-empty-install-dir.sh" /opt/dm3 /etc/dm3 /var/lib/dm3

echo "=== Установка пакетов ==="
apt update
# git keeps the mirror, jq reads the resolved compose configuration, openssh-client brings the
# ssh-keygen that verifies the tag signatures.
apt install -y docker.io docker-compose-v2 git jq openssh-client iptables-persistent openssl certbot

if [ -n "${SUDO_USER:-}" ]; then
    echo "=== Docker для пользователя $SUDO_USER ==="
    usermod -aG docker "$SUDO_USER"
fi

echo "=== Ротация журналов Docker ==="
# The default json-file driver keeps every line forever and Serilog also writes to the
# console, so a full disk kills Postgres first. Set on the daemon, which also covers the
# one-off containers, and it applies to containers created after it.
DOCKER_DAEMON_CONFIG=/etc/docker/daemon.json
if [ -f "$DOCKER_DAEMON_CONFIG" ]; then
    echo "$DOCKER_DAEMON_CONFIG уже существует, ротацию журналов проверить вручную:"
    echo "  log-driver json-file, log-opts max-size 10m, max-file 3"
else
    mkdir -p "$(dirname "$DOCKER_DAEMON_CONFIG")"
    tee "$DOCKER_DAEMON_CONFIG" > /dev/null << 'JSON'
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
JSON
    systemctl restart docker
fi

echo "=== Открытие портов (firewall) ==="
iptables -I INPUT -p tcp --dport 80 -j ACCEPT
iptables -I INPUT -p tcp --dport 443 -j ACCEPT
netfilter-persistent save

echo "=== Версии инструментов ==="
# What the release script needs: compose v2 for --pull never and --wait, git 2.34 for tags signed
# with SSH keys, OpenSSH 8.2 for ssh-keygen -Y find-principals.
at_least() {
    local have=$1 want=$2
    [ "$(printf '%s\n%s\n' "$want" "$have" | sort -V | head -n 1)" = "$want" ]
}
compose_version="$(docker compose version --short 2> /dev/null | sed 's/^v//' || true)"
git_version="$(git --version | sed -n 's/^git version \([0-9.]*\).*/\1/p')"
ssh_version="$(ssh -V 2>&1 | sed -n 's/^OpenSSH_\([0-9.]*\).*/\1/p')"
at_least "${compose_version:-0}" 2.0 || { echo "Error: docker compose ${compose_version:-is missing}, v2 is needed" >&2; exit 1; }
at_least "${git_version:-0}" 2.34 || { echo "Error: git ${git_version:-is missing}, 2.34 or newer is needed" >&2; exit 1; }
at_least "${ssh_version:-0}" 8.2 || { echo "Error: OpenSSH ${ssh_version:-is missing}, 8.2 or newer is needed" >&2; exit 1; }
echo "docker compose $compose_version, git $git_version, OpenSSH $ssh_version"

echo "=== Раскладка сервера и первый релиз ==="
exec bash "$HERE/scripts/install-release.sh" "$@"
