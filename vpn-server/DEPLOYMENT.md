# PrivateNet Server Deployment Guide

This guide describes how to replace your existing web service on `PrivateNet` (Ubuntu Server) with the PrivateNet VPN Server & Web Portal.

---

## 1. Prerequisites on the Ubuntu Server
- Ubuntu 22.04 or 24.04 LTS (x86_64 or ARM64 Ampere).
- DNS A record: `PrivateNet` pointing to your server's public IPv4.
- Ports required to be open in firewall / cloud security list:
  - `80/TCP` (HTTP for ACME challenges & redirect)
  - `443/TCP` (HTTPS for Web Portal & API)
  - `51820/UDP` (WireGuard VPN tunnel)
  - `22/TCP` (SSH admin access)

---

## 2. Shutting Down the Existing Web Service
On your server, stop and disable whatever is currently serving on port 80/443 (e.g. Apache, Nginx, or an existing PM2/Docker container):

```bash
# If using Nginx or Apache:
sudo systemctl stop nginx || sudo systemctl stop apache2
sudo systemctl disable nginx || sudo systemctl disable apache2

# If using PM2 or Docker:
pm2 stop all
# or: docker stop $(docker ps -q)
```

---

## 3. Transferring & Deploying `vpn-server`
Upload the `vpn-server` directory to `/opt/privatenet/vpn-server` or clone your repository:

```bash
# Example using rsync / git:
sudo mkdir -p /opt/privatenet
sudo chown -R $USER:$USER /opt/privatenet

# Copy vpn-server folder to /opt/privatenet/vpn-server
cd /opt/privatenet/vpn-server

# Make the deploy script executable and run it:
chmod +x deploy.sh
./deploy.sh
```

---

## 4. What `deploy.sh` Automates
1. **Installs System Dependencies:** `wireguard`, `iptables-persistent`, `mysql-server`, `caddy`, and `nodejs`.
2. **Generates WireGuard Server Keypair:** Creates `/etc/wireguard/server.key` and `/etc/wireguard/server.pub`.
3. **Configures the Kernel & Interface:** Sets up `wg0` on `10.77.0.1/22`, enables `net.ipv4.ip_forward=1`, and configures IPTables forwarding & isolation rules.
4. **Initializes MySQL Database:** Creates database `privatenet` and user `privnet_user`.
5. **Applies Prisma Schema:** Migrates tables (`users`, `devices`, `invites`, `enrollment_codes`, `audit_log`).
6. **Starts Daemons:**
   - Starts WireGuard via `systemctl restart wg-quick@wg0`.
   - Starts `wg-agent.mjs` (root daemon that dynamically updates WireGuard peers from MySQL).
   - Starts Next.js application on port 3000 via PM2.
   - Configures Caddy to automatically terminate HTTPS for `PrivateNet` with Let's Encrypt certificates.

---

## 5. First-Time Setup (Bootstrap Admin)
1. Open your browser and navigate to:
   ```
   https://PrivateNet/signup
   ```
2. Enter your email, display name, and password. Because this is the very first registered user in the database, the server **automatically promotes your account to Network Administrator** without requiring an invite code!
3. From your dashboard:
   - Click **Add New Device** to generate your first client enrollment code.
   - Go to `/admin` to generate 7-day invite codes for friends and colleagues.

---

## 6. Service Management Commands

```bash
# Check Next.js portal logs
pm2 logs vpn-server

# Check wg-agent logs
sudo pm2 logs wg-agent

# Inspect active WireGuard peers and handshakes
sudo wg show wg0

# Check Caddy HTTPS status
sudo systemctl status caddy
```
