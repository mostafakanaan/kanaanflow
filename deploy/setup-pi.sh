#!/usr/bin/env bash
# One-time Raspberry Pi setup for KanaanFlow hosting (see docs/hosting-pi.md).
#
# Run on the Pi as the user that runs your portfolio's runner (not root):
#   curl -fsSL https://raw.githubusercontent.com/mostafakanaan/kanaanflow/main/deploy/setup-pi.sh \
#     | bash -s -- --token <RUNNER_TOKEN> [--hostname flow.example.com]
#
# <RUNNER_TOKEN>: GitHub › kanaanflow › Settings › Actions › Runners › New self-hosted runner
# (the value after --token in the "Configure" box; it expires after one hour).

set -euo pipefail

REPO_URL="https://github.com/mostafakanaan/kanaanflow"
RUNNER_DIR="${HOME}/actions-runner-kanaanflow"
TOKEN=""
HOSTNAME_PUBLIC=""

while [ $# -gt 0 ]; do
    case "$1" in
        --token) TOKEN="${2:-}"; shift 2 ;;
        --hostname) HOSTNAME_PUBLIC="${2:-}"; shift 2 ;;
        *) echo "Unknown option: $1" >&2; exit 2 ;;
    esac
done

step() { printf '\n==> %s\n' "$*"; }
warn() { printf '!!  %s\n' "$*" >&2; }

if [ "$(id -u)" -eq 0 ]; then
    echo "Run this as your normal user (the GitHub runner refuses to run as root)." >&2
    exit 1
fi

if [ "$(uname -m)" != "aarch64" ]; then
    warn "Expected a 64-bit Raspberry Pi OS (aarch64), found $(uname -m)."
fi

step "Checking passwordless sudo (the deploy workflow needs it)"
if sudo -n true 2>/dev/null; then
    echo "ok"
else
    warn "sudo asks for a password. Use the same user as the portfolio runner, or allow it with:"
    warn "  echo \"$USER ALL=(ALL) NOPASSWD:ALL\" | sudo tee /etc/sudoers.d/$USER-runner"
    sudo -v
fi

step "Installing packages (rsync, nginx, curl)"
sudo apt-get update -qq
sudo apt-get install -y -qq rsync nginx curl tar >/dev/null
echo "ok"

step "Preparing web root"
sudo mkdir -p /var/www/kanaanflow
if grep -q "conf.d/\*.conf" /etc/nginx/nginx.conf; then
    echo "ok (nginx loads /etc/nginx/conf.d/*.conf)"
else
    warn "/etc/nginx/nginx.conf doesn't include conf.d/*.conf; add 'include /etc/nginx/conf.d/*.conf;' to its http block."
fi

step "Installing the GitHub Actions runner for kanaanflow"
if [ -f "${RUNNER_DIR}/.runner" ]; then
    echo "Already configured in ${RUNNER_DIR}; skipping."
else
    if [ -z "$TOKEN" ]; then
        echo "Missing --token. Get one from ${REPO_URL}/settings/actions/runners/new" >&2
        exit 1
    fi
    mkdir -p "$RUNNER_DIR"
    cd "$RUNNER_DIR"
    version="$(curl -fsSL https://api.github.com/repos/actions/runner/releases/latest | sed -n 's/.*"tag_name": *"v\([^"]*\)".*/\1/p' | head -n1)"
    if [ -z "$version" ]; then
        echo "Couldn't find the latest runner version." >&2
        exit 1
    fi
    echo "Runner version ${version}"
    curl -fsSL -o runner.tar.gz "https://github.com/actions/runner/releases/download/v${version}/actions-runner-linux-arm64-${version}.tar.gz"
    tar xzf runner.tar.gz
    rm runner.tar.gz
    ./config.sh --unattended --url "$REPO_URL" --token "$TOKEN" \
        --name "$(hostname)-kanaanflow" --labels kanaanflow --work _work --replace
    sudo ./svc.sh install "$USER"
    sudo ./svc.sh start
fi

step "Cloudflare Tunnel"
config=""
for candidate in /etc/cloudflared/config.yml "${HOME}/.cloudflared/config.yml"; do
    if [ -f "$candidate" ]; then config="$candidate"; break; fi
done

if [ -z "$HOSTNAME_PUBLIC" ]; then
    echo "No --hostname given. Add a public hostname to your tunnel: <host> -> http://localhost:18081"
elif [ -z "$config" ]; then
    echo "Your tunnel is managed in the Cloudflare dashboard. Add it there:"
    echo "  Zero Trust › Networks › Tunnels › your tunnel › Public hostname › Add"
    echo "  ${HOSTNAME_PUBLIC}  ->  HTTP  localhost:18081"
elif sudo grep -q "hostname: *${HOSTNAME_PUBLIC}" "$config"; then
    echo "${HOSTNAME_PUBLIC} is already in ${config}"
else
    if ! sudo grep -q "service: *http_status:404" "$config"; then
        warn "Couldn't find the catch-all rule in ${config}. Add this under 'ingress:' yourself:"
        warn "  - hostname: ${HOSTNAME_PUBLIC}"
        warn "    service: http://localhost:18081"
    else
        sudo cp "$config" "${config}.bak.$(date +%Y%m%d%H%M%S)"
        indent="$(sudo grep -m1 "service: *http_status:404" "$config" | sed 's/^\( *\).*/\1/')"
        sudo sed -i "0,/^${indent}- service: *http_status:404/s||${indent}- hostname: ${HOSTNAME_PUBLIC}\n${indent}  service: http://localhost:18081\n&|" "$config"
        echo "Added ${HOSTNAME_PUBLIC} to ${config} (backup saved next to it)."
        tunnel="$(sudo sed -n 's/^tunnel: *//p' "$config" | head -n1)"
        if [ -n "$tunnel" ] && command -v cloudflared >/dev/null; then
            cloudflared tunnel route dns "$tunnel" "$HOSTNAME_PUBLIC" \
                || warn "DNS route failed; add a CNAME ${HOSTNAME_PUBLIC} -> ${tunnel}.cfargotunnel.com in Cloudflare DNS."
        fi
        sudo systemctl restart cloudflared || warn "Restart cloudflared yourself."
    fi
fi

step "Done"
echo "Runner: ${REPO_URL}/settings/actions/runners should list $(hostname)-kanaanflow as Idle."
echo "Next: merge to main (or run the 'Build & Deploy (Self-hosted Pi)' workflow) to deploy."
echo "In Cloudflare, keep Rocket Loader off for ${HOSTNAME_PUBLIC:-the KanaanFlow hostname}."
