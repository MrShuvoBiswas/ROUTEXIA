#!/bin/bash
# RouteXia Backend Management API — Setup Script for VPS
# Usage: sudo ./install-backend.sh [PORT]

set -e

API_PORT=${1:-8080}
BINARY_NAME="routexia-backend"
WORK_DIR="/opt/routexia-backend"

echo "=== RouteXia Backend API Server Setup (port: $API_PORT) ==="

# ── 1. Install Go if not present ─────────────────────────────────────────────
export PATH=$PATH:/usr/local/go/bin
if ! command -v go &> /dev/null; then
    echo "[+] Installing Go 1.21.5..."
    wget -q https://go.dev/dl/go1.21.5.linux-amd64.tar.gz
    rm -rf /usr/local/go
    tar -C /usr/local -xzf go1.21.5.linux-amd64.tar.gz
    export PATH=$PATH:/usr/local/go/bin
    echo 'export PATH=$PATH:/usr/local/go/bin' >> /etc/profile
    rm -f go1.21.5.linux-amd64.tar.gz
    echo "[+] Go installed: $(go version)"
fi

# ── 2. Create directory & copy source ─────────────────────────────────────────
echo "[+] Setting up $WORK_DIR..."
mkdir -p "$WORK_DIR"
cd "$WORK_DIR"

# If running from cloned ROUTEXIA repo, copy backend/ files
if [ -d "/root/ROUTEXIA/backend" ]; then
    cp -r /root/ROUTEXIA/backend/* "$WORK_DIR/"
elif [ -d "$HOME/ROUTEXIA/backend" ]; then
    cp -r "$HOME/ROUTEXIA/backend/"* "$WORK_DIR/"
elif [ -d "../backend" ]; then
    cp -r ../backend/* "$WORK_DIR/"
fi

# ── 3. Download dependencies & Build binary ───────────────────────────────────
echo "[+] Building backend binary..."
/usr/local/go/bin/go mod tidy
/usr/local/go/bin/go build -o /usr/local/bin/$BINARY_NAME main.go
chmod +x /usr/local/bin/$BINARY_NAME
echo "[+] Binary installed: /usr/local/bin/$BINARY_NAME"

# ── 4. Open TCP port in firewall ──────────────────────────────────────────────
echo "[+] Opening TCP port $API_PORT in firewall..."
ufw allow $API_PORT/tcp 2>/dev/null || true
iptables -I INPUT -p tcp --dport $API_PORT -j ACCEPT 2>/dev/null || true

# ── 5. Create systemd service ─────────────────────────────────────────────────
echo "[+] Creating systemd service..."
cat > /etc/systemd/system/routexia-backend.service << EOF
[Unit]
Description=RouteXia Management & Subscription API
After=network.target

[Service]
Type=simple
WorkingDirectory=$WORK_DIR
ExecStart=/usr/local/bin/$BINARY_NAME -port $API_PORT -db $WORK_DIR/routexia.db
Restart=always
RestartSec=3
LimitNOFILE=65536

[Install]
WantedBy=multi-user.target
EOF

# ── 6. Enable and start service ───────────────────────────────────────────────
fuser -k -9 $API_PORT/tcp 2>/dev/null || true
killall -9 $BINARY_NAME 2>/dev/null || true

systemctl daemon-reload
systemctl enable routexia-backend
systemctl restart routexia-backend
systemctl status routexia-backend --no-pager

echo ""
echo "=================================================="
echo "  ✅ RouteXia Backend API Server Ready!"
echo "  Port   : TCP $API_PORT"
echo "  Logs   : journalctl -u routexia-backend -f"
echo "=================================================="
