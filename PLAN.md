# Custom VPN — Bug Fix Plan

> All 7 bugs identified from live server diagnosis on 2026-10-06.
> Execute in order — later phases depend on earlier ones.

---

## Phase 1 — Database Fixes (Server, ~5 min)

These are run directly on the live server via SSH.
No code changes, no redeployment needed.

### [P1-1] Fix user assigned IPs — wrong subnet in DB

**Problem:** Users have `10.8.0.x` IPs but WireGuard interface `wg0` runs on `10.77.0.1/24`.
Any peer with a `10.8.0.x` IP gets zero traffic routed to it.

**Proof:** `wg show` shows 10.8.0.x peers with ~732 B total transfer vs 143 MiB for the one correct `10.77.0.x` peer.

**Fix (run on server):**
```bash
ssh -i "C:\sshkeys\ssh-key-2026-09-11.key" ubuntu@144.24.25.135
cd /var/www/Custom-VPN/vpn-server

sqlite3 prisma/dev.db "UPDATE users SET assigned_ip='10.77.0.2' WHERE username='vpn_001';"
sqlite3 prisma/dev.db "UPDATE users SET assigned_ip='10.77.0.3' WHERE username='vpn_002';"
sqlite3 prisma/dev.db "UPDATE users SET assigned_ip='10.77.0.4' WHERE username='vpn_003';"

# Verify
sqlite3 prisma/dev.db "SELECT username, assigned_ip FROM users;"
```

**Expected output:**
```
vpn_001|10.77.0.2
vpn_002|10.77.0.3
vpn_003|10.77.0.4
```

- [ ] Done

---

### [P1-2] Update stale server.pub file

**Problem:** `server.pub` has a key that does not match the actual `wg0` public key.

**Fix (run on server):**
```bash
echo "p+jZFDQaOQVcr84oqZiYDmamfTdID+3wUeH912gYLR8=" > /var/www/Custom-VPN/vpn-server/server.pub
```

- [ ] Done

---

## Phase 2 — Server-Side Code Fixes (vpn-server)

All changes in `vpn-server/src/`.
Requires redeployment after all fixes in this phase are done.

### [P2-1] Fix wg genkey failing silently and killing logins

**File:** `vpn-server/src/app/api/client/login/route.ts` — lines 49–63

**Problem:** `wg genkey` runs inside the Next.js process as user `ubuntu`.
If `wg` is not in PATH for that process it throws, and the entire login returns HTTP 500
before `prisma.session.create()` is ever called. This is why the sessions table is empty.

**Step A — Add wg to sudoers on server:**
```bash
echo "ubuntu ALL=(ALL) NOPASSWD: /usr/bin/wg" | sudo tee /etc/sudoers.d/wg-keygen
sudo chmod 440 /etc/sudoers.d/wg-keygen
```

**Step B — Wrap key generation in try/catch in `login/route.ts`:**

Before:
```ts
const pkRes = await execAsync('wg genkey');
clientPrivateKey = pkRes.stdout.trim();
const pubRes = await execAsync(`echo "${clientPrivateKey}" | wg pubkey`);
clientPublicKey = pubRes.stdout.trim();
```

After:
```ts
try {
  const pkRes = await execAsync('wg genkey');
  clientPrivateKey = pkRes.stdout.trim();
  const pubRes = await execAsync(`echo "${clientPrivateKey}" | wg pubkey`);
  clientPublicKey = pubRes.stdout.trim();
  if (!clientPrivateKey || !clientPublicKey) throw new Error("Empty key output from wg");
} catch (keyErr) {
  console.error("[login] wg key generation failed:", keyErr);
  return NextResponse.json({ error: "VPN key generation failed on server. Contact admin." }, { status: 500 });
}
```

- [ ] Sudoers entry added on server
- [ ] Code updated in login/route.ts

---

### [P2-2] Fix wrong fallback subnet in login response

**File:** `vpn-server/src/app/api/client/login/route.ts` — line 85 and 95

**Problem:** Fallback subnet is `10.8.0.0/24` (OpenVPN subnet, not WireGuard).

Before:
```ts
subnet: user.network?.subnet || "10.8.0.0/24",
```

After:
```ts
subnet: user.network?.subnet || "10.77.0.0/24",
```

Also fix line 85 (AllowedIPs in wgConfigText):
Before:
```ts
AllowedIPs = ${user.network?.subnet || "10.77.0.0/24"}
```
Verify this already says `10.77.0.0/24` — if not, fix it to match.

- [ ] Done

---

### [P2-3] Add serverPublicKey to login response

**File:** `vpn-server/src/app/api/client/login/route.ts` — lines 93–95

**Problem:** `serverConfig` in the response is missing `serverPublicKey`.
The client reads `cfg.serverPublicKey` in VpnService.cs — if missing, it stays empty
and the generated WireGuard config has a blank PublicKey for the server peer.

Before:
```ts
serverConfig: {
  endpoint: "144.24.25.135:51820",
  subnet: user.network?.subnet || "10.8.0.0/24",
},
```

After:
```ts
serverConfig: {
  serverPublicKey: process.env.SERVER_PUBLIC_KEY || "p+jZFDQaOQVcr84oqZiYDmamfTdID+3wUeH912gYLR8=",
  endpoint: "144.24.25.135:51820",
  subnet: user.network?.subnet || "10.77.0.0/24",
},
```

- [ ] Done

---

### [P2-4] Stop broken wg-agent.mjs daemon

**File:** `vpn-server/scripts/wg-agent.mjs`

**Problem:** Imports `mysql2` and connects to MySQL. Server uses SQLite.
Agent crashes on startup, peer sync via socket is completely non-functional.
Also queries a `devices` table that does not exist in the Prisma schema.

**Immediate fix (run on server):**
```bash
pkill -f wg-agent.mjs || true
```

**Permanent fix:** Rewrite to use `better-sqlite3` querying the `sessions` table,
or remove entirely since `wg-agent.ts` in `src/lib/` already handles peer registration
via direct `wg set` from the login route.

- [ ] Daemon stopped on server
- [ ] Rewrite or removal scheduled

---

## Phase 3 — Client-Side Code Fixes (cvpn-client)

All changes in `cvpn-client/`. Requires rebuild + reinstall.

### [P3-1] Fix DeactivateTunnelAsync — no delay after uninstall

**File:** `cvpn-client/WireGuardTunnelManager.cs` — lines 143–163

**Problem:** On first launch the tunnel service does not exist yet.
The uninstall fails silently (catch swallows everything).
Then immediately calls `/installtunnelservice` without waiting for Windows SCM to settle,
causing "Tunnel already installed and running" on subsequent attempts.

Before:
```cs
try
{
    var p = Process.Start(psi);
    if (p != null) await p.WaitForExitAsync();
}
catch { }
```

After:
```cs
try
{
    var p = Process.Start(psi);
    if (p != null) await p.WaitForExitAsync();
    // Give Windows Service Control Manager time to fully unregister the service
    await Task.Delay(2000);
}
catch
{
    // Service not existing on first run is expected — not an error
    await Task.Delay(500);
}
```

- [ ] Done

---

### [P3-2] Fix AllowedIPs replace — fragile string literals

**File:** `cvpn-client/WireGuardTunnelManager.cs` — lines 91–94 and 183–188

**Problem:** RouteAllTraffic toggle uses hardcoded string replace:
```cs
configText.Replace("AllowedIPs = 10.77.0.0/24", "AllowedIPs = 0.0.0.0/0")
          .Replace("AllowedIPs = 10.77.0.0/22", "AllowedIPs = 0.0.0.0/0")
```
If the server returns any variation, the replace silently does nothing.

Add `using System.Text.RegularExpressions;` at the top, then:

Before (in ActivateTunnelAsync):
```cs
if (routeAllTraffic)
{
    configText = configText.Replace("AllowedIPs = 10.77.0.0/24", "AllowedIPs = 0.0.0.0/0")
                           .Replace("AllowedIPs = 10.77.0.0/22", "AllowedIPs = 0.0.0.0/0");
}
```

After:
```cs
if (routeAllTraffic)
{
    configText = Regex.Replace(configText, @"AllowedIPs\s*=\s*[^\r\n]+", "AllowedIPs = 0.0.0.0/0, ::/0");
}
```

Same pattern in `SetRouteAllTrafficAsync` for the reverse direction:
```cs
configText = Regex.Replace(configText, @"AllowedIPs\s*=\s*[^\r\n]+", $"AllowedIPs = {VpnService.Subnet}");
```

- [ ] Done in ActivateTunnelAsync
- [ ] Done in SetRouteAllTrafficAsync

---

### [P3-3] Verify ping IP is correct (no code change needed)

**File:** `cvpn-client/WireGuardTunnelManager.cs` — line 124

After Phase 1 and Phase 2 fixes:
- All user IPs will be on `10.77.0.x`
- `AllowedIPs` in the config will be `10.77.0.0/24`
- The client will have a route to `10.77.0.1` through the tunnel
- Pinging `10.77.0.1` will succeed

No code change needed here. Verify after full fix is deployed.

- [ ] Verified working after P1 + P2 complete

---

## Phase 4 — Deploy and Validate

### [P4-1] Redeploy server

```bash
ssh -i "C:\sshkeys\ssh-key-2026-09-11.key" ubuntu@144.24.25.135

cd /var/www/Custom-VPN/vpn-server

# Pull latest
git pull origin main

# Rebuild Next.js
npm run build

# Restart the Next.js process
pkill -f "next start" || true
sleep 2
nohup npm start > /var/log/cvpn-server.log 2>&1 &
sleep 3

# Quick smoke test
curl -s -X POST https://resolvia.cc.cd/api/client/login \
  -H "Content-Type: application/json" \
  -d '{"username":"vpn_001","password":"CORRECT_PASSWORD","deviceId":"test-123"}' | python3 -m json.tool
```

- [ ] Build succeeded
- [ ] Process restarted
- [ ] Login API returns valid JSON (not 500)

---

### [P4-2] Rebuild and reinstall client

```powershell
cd D:\Projects\MyVPN\cvpn-client
dotnet build -c Release

# Clear old tunnel service (run as admin)
& "C:\Program Files\WireGuard\wireguard.exe" /uninstalltunnelservice CustomVPN
Start-Sleep -Seconds 3
```

- [ ] Built with no errors
- [ ] Old tunnel cleared

---

### [P4-3] End-to-end test checklist

1. Launch rebuilt client
2. Log in as `vpn_001`
3. No "Tunnel already installed" or "Service does not exist" errors
4. Dashboard shows IP `10.77.0.2` (not `10.8.0.2`)
5. Open CMD: `ping 10.77.0.1` — should get replies
6. SSH into server, run `sudo wg show wg0`:
   - `vpn_001`'s peer has `allowed ips: 10.77.0.2/32`
   - Handshake timestamp is recent (within last minute)
7. Check sessions table: `sqlite3 prisma/dev.db "SELECT * FROM sessions;"`
   - Should have a row for `vpn_001`

- [ ] Login succeeds without errors
- [ ] Assigned IP is `10.77.0.x`
- [ ] `ping 10.77.0.1` gets replies
- [ ] `wg show` shows active handshake
- [ ] Sessions table has data

---

## Bug Tracker

| ID   | Bug Description                                          | Phase   | Status     |
|------|----------------------------------------------------------|---------|------------|
| P1-1 | User IPs are `10.8.0.x`, wg0 is on `10.77.0.x`         | Phase 1 | ⬜ Open    |
| P1-2 | `server.pub` contains stale/wrong public key             | Phase 1 | ⬜ Open    |
| P2-1 | `wg genkey` fails silently, sessions table empty         | Phase 2 | ⬜ Open    |
| P2-2 | Fallback subnet is `10.8.0.0/24` (OpenVPN, not WireGuard)| Phase 2 | ⬜ Open    |
| P2-3 | `serverPublicKey` missing from login API response        | Phase 2 | ⬜ Open    |
| P2-4 | `wg-agent.mjs` uses MySQL driver on SQLite server        | Phase 2 | ⬜ Open    |
| P3-1 | `DeactivateTunnelAsync` swallows errors, no delay        | Phase 3 | ⬜ Open    |
| P3-2 | Ping verify IP — fixed automatically by P1-1 + P2-2     | Phase 3 | ⬜ Blocked |
| P3-3 | AllowedIPs replace uses brittle hardcoded strings        | Phase 3 | ⬜ Open    |
