#!/usr/bin/env bash
# PrivateNet Server Deployment Script for Ubuntu 24.04 / 22.04 LTS
# Target Domain: resolvia.cc.cd
# Subnet: 10.77.0.0/22 | Hub IP: 10.77.0.1
set -e

echo "=== [1/8] Installing System Packages (WireGuard, MySQL, Node, Caddy) ==="
sudo apt update
sudo apt install -y wireguard iptables-persistent mysql-server curl git ufw

# Install Node.js 20 LTS if not present
if ! command -v node &> /dev/null; then
  echo "Installing Node.js 20 LTS..."
  curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
  sudo apt install -y nodejs
fi

# Install Caddy if not present
if ! command -v caddy &> /dev/null; then
  echo "Installing Caddy web server..."
  sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
  sudo apt update
  sudo apt install -y caddy
fi

# Install PM2 for process management
sudo npm install -g pm2

echo "=== [2/8] Configuring WireGuard Hub (/etc/wireguard/wg0.conf) ==="
sudo mkdir -p /etc/wireguard
if [ ! -f /etc/wireguard/server.key ]; then
  wg genkey | sudo tee /etc/wireguard/server.key | wg pubkey | sudo tee /etc/wireguard/server.pub
  sudo chmod 600 /etc/wireguard/server.key
fi

SERVER_PRIVKEY=$(sudo cat /etc/wireguard/server.key)
SERVER_PUBKEY=$(sudo cat /etc/wireguard/server.pub)

echo "Server WireGuard Public Key: $SERVER_PUBKEY"

cat <<EOF | sudo tee /etc/wireguard/wg0.conf
[Interface]
Address    = 10.77.0.1/22
ListenPort = 51820
PrivateKey = $SERVER_PRIVKEY
MTU        = 1380

PostUp   = sysctl -w net.ipv4.ip_forward=1
PostUp   = iptables -A FORWARD -i wg0 -o wg0 -j ACCEPT
PostUp   = iptables -A INPUT -i wg0 -p icmp -j ACCEPT
PostUp   = iptables -A INPUT -i wg0 -p tcp --dport 3001 -j ACCEPT
PostUp   = iptables -A INPUT -i wg0 -j DROP
PostDown = iptables -D FORWARD -i wg0 -o wg0 -j ACCEPT
PostDown = iptables -D INPUT -i wg0 -p icmp -j ACCEPT
PostDown = iptables -D INPUT -i wg0 -p tcp --dport 3001 -j ACCEPT
PostDown = iptables -D INPUT -i wg0 -j DROP
EOF

# Ensure ip_forward persists across reboots
echo "net.ipv4.ip_forward=1" | sudo tee /etc/sysctl.d/99-privatenet.conf
sudo sysctl --system

echo "=== [3/8] Configuring Firewall (Oracle / Ubuntu gotcha) ==="
# Allow WireGuard UDP and Web traffic
sudo iptables -I INPUT 1 -p udp --dport 51820 -j ACCEPT
sudo iptables -I INPUT 1 -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT 1 -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save

echo "=== [4/8] Initializing MySQL Database ==="
sudo mysql -e "CREATE DATABASE IF NOT EXISTS privatenet CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
sudo mysql -e "CREATE USER IF NOT EXISTS 'privnet_user'@'localhost' IDENTIFIED BY 'PrivNetSecret2026!';"
sudo mysql -e "GRANT ALL PRIVILEGES ON privatenet.* TO 'privnet_user'@'localhost';"
sudo mysql -e "FLUSH PRIVILEGES;"

echo "=== [5/8] Building Next.js VPN Portal ==="
npm install
npx prisma generate
npx prisma db push

# Generate random JWT secret if not set
RAND_SECRET=$(openssl rand -hex 32)

cat <<EOF > .env
DATABASE_URL="mysql://privnet_user:PrivNetSecret2026!@127.0.0.1:3306/privatenet"
JWT_SECRET="$RAND_SECRET"
SERVER_PUBLIC_KEY="$SERVER_PUBKEY"
SERVER_ENDPOINT="resolvia.cc.cd:51820"
VIRTUAL_SUBNET="10.77.0.0/22"
SERVER_TUNNEL_IP="10.77.0.1"
INTERNAL_API_URL="http://10.77.0.1:3000"
WG_AGENT_SOCKET="/run/wg-agent.sock"
EOF

npm run build

echo "=== [6/8] Starting WireGuard and Services ==="
sudo systemctl enable wg-quick@wg0
sudo systemctl restart wg-quick@wg0

# Start wg-agent as root service via PM2
sudo pm2 delete wg-agent 2>/dev/null || true
sudo DATABASE_URL="mysql://privnet_user:PrivNetSecret2026!@127.0.0.1:3306/privatenet" pm2 start scripts/wg-agent.mjs --name wg-agent
sudo pm2 save

# Start Next.js Portal on port 3000
pm2 delete vpn-server 2>/dev/null || true
pm2 start npm --name "vpn-server" -- start -- -p 3000
pm2 save

echo "=== [7/8] Configuring Caddy for resolvia.cc.cd ==="
cat <<EOF | sudo tee /etc/caddy/Caddyfile
resolvia.cc.cd {
    encode zstd gzip
    reverse_proxy 127.0.0.1:3000
    header Strict-Transport-Security "max-age=31536000"
}
EOF

sudo systemctl restart caddy

echo "=== [8/8] Deployment Complete! ==="
echo "Your PrivateNet server is live at: https://resolvia.cc.cd"
echo "WireGuard Endpoint: resolvia.cc.cd:51820"
echo "Server Public Key: $SERVER_PUBKEY"
echo "Visit https://resolvia.cc.cd/signup to create the initial Admin account!"
