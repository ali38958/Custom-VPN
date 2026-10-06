# Custom VPN — Feature TODO & Roadmap

> **For the next agent:** Read this entire file before touching any code.
> The project is: `vpn-server/` (Next.js 16 + Prisma + SQLite) + `cvpn-client/` (C# WPF .NET 10).
> WireGuard runs as `wg-quick@wg0` on the server. The wg0 interface is `10.77.0.1/24`.
> Server SSH: `ssh -i "C:\sshkeys\ssh-key-2026-09-11.key" ubuntu@144.24.25.135`
> After any server-side code change: `sudo git pull && sudo npm run build` then restart the server process.

---

## FEATURE 1 — Firewall Rule Manager (Admin UI + Server)

**What it does:** Let the admin create per-user or global iptables FORWARD chain rules from the web dashboard.
Block specific ports, IPs, or protocols per peer. No SSH required.

**Current state:** wg0.conf has static PostUp/PostDown rules. No dynamic rule management exists.

### Recommended Approach

#### 1a. Database — Add FirewallRule model to prisma/schema.prisma
```prisma
model FirewallRule {
  id        Int      @id @default(autoincrement())
  userId    Int?     @map("user_id")       // null = global rule
  direction String   @default("FORWARD")
  action    String   @default("DROP")      // DROP | ACCEPT | REJECT
  protocol  String?                        // tcp | udp | icmp | null = all
  destPort  Int?     @map("dest_port")
  destIp    String?  @map("dest_ip")
  comment   String?
  enabled   Boolean  @default(true)
  createdAt DateTime @default(now()) @map("created_at")

  user User? @relation(fields: [userId], references: [id], onDelete: Cascade)

  @@map("firewall_rules")
}
```
Run: npx prisma db push on the server after schema change.

#### 1b. API — Create /api/admin/firewall/route.ts
- GET  → fetch all rules from DB
- POST → create rule in DB, then run the live iptables command
- DELETE /api/admin/firewall/[id] → delete from DB + run iptables -D

Helper in src/lib/firewall.ts:
```ts
export async function applyFirewallRule(rule, action: '-A' | '-D') {
  let cmd = `sudo iptables -${action} FORWARD`;
  if (rule.user?.assignedIp) cmd += ` -s ${rule.user.assignedIp}`;
  if (rule.protocol) cmd += ` -p ${rule.protocol}`;
  if (rule.destPort) cmd += ` --dport ${rule.destPort}`;
  if (rule.destIp)   cmd += ` -d ${rule.destIp}`;
  cmd += ` -j ${rule.action}`;
  await execAsync(cmd);
}
```

IMPORTANT — extend the sudoers entry to include iptables:
  ubuntu ALL=(ALL) NOPASSWD: /usr/bin/wg, /usr/sbin/iptables, /sbin/iptables-save
Save to /etc/sudoers.d/wg-keygen

#### 1c. Persistence on reboot
After every rule change call: sudo iptables-save > /etc/iptables/rules.v4
Package needed: sudo apt install iptables-persistent

#### 1d. Admin UI
- Add a "Firewall" tab/section to vpn-server/src/app/admin/page.tsx
- Table of rules: User (or Global), Protocol, Dest Port, Dest IP, Action, Delete button
- "Add Rule" modal: same dark zinc Tailwind style as existing Users table
- Fetch from GET /api/admin/firewall on load

---

## FEATURE 2 — Bandwidth & Traffic Monitor (Per Peer)

**What it does:** Show each peer RX/TX bytes live on the admin dashboard from `wg show wg0 dump`.

**Current state:** No traffic monitoring exists.

### Recommended Approach

#### 2a. API — Create /api/admin/traffic/route.ts
```ts
export async function GET() {
  const { stdout } = await execAsync('sudo wg show wg0 dump');
  // Each line: pubKey  presharedKey  endpoint  allowedIPs  lastHandshake  rxBytes  txBytes  keepalive
  const peers = stdout.trim().split('\n').slice(1).map(line => {
    const [pubKey,,endpoint,allowedIPs,lastHandshake,rxBytes,txBytes] = line.split('\t');
    return { pubKey, endpoint, allowedIPs, lastHandshake: Number(lastHandshake), rxBytes: Number(rxBytes), txBytes: Number(txBytes) };
  });
  return NextResponse.json({ peers });
}
```
Join pubKey against sessions.public_key in DB to resolve to username.

#### 2b. Admin UI
- Add a "Traffic" section below the users table
- Poll GET /api/admin/traffic every 5s with setInterval
- Columns: username, assigned IP, RX (formatted MB/GB), TX, last handshake (relative time)
- Green pulsing dot = handshake < 3 minutes ago (peer is live)

---

## FEATURE 3 — Auto IP Allocation (No Manual IP Entry)

**What it does:** Automatically assign the next free IP in the subnet when creating a user.

**Current state:** Admin manually types 10.77.0.X — error prone and collision-risk.

### Recommended Approach

In POST /api/admin/users/route.ts, add before user creation:
```ts
async function getNextFreeIp(): Promise<string> {
  const usedIps = await prisma.user.findMany({ select: { assignedIp: true } });
  const used = new Set(usedIps.map(u => u.assignedIp));
  for (let i = 2; i <= 254; i++) {
    const candidate = `10.77.0.${i}`;
    if (!used.has(candidate)) return candidate;
  }
  throw new Error('Subnet exhausted');
}
```
Remove the manual assignedIp input from the Add User modal in admin/page.tsx.

---

## FEATURE 4 — Per-User WireGuard Peer Control (Live Enable/Disable)

**What it does:** When admin disables a user, actually remove them from the live wg0 interface (not just DB flag).

**Current state:** disabled flag is set in DB but the WireGuard peer keeps running — user can still connect.

### Recommended Approach

In PATCH /api/admin/users/[id]/route.ts:

When disabling (disabled: true):
```ts
const session = await prisma.session.findUnique({ where: { userId: user.id } });
if (session?.publicKey) {
  await execAsync(`sudo wg set wg0 peer ${session.publicKey} remove`);
  await execAsync(`sudo wg-quick save wg0`);  // persist the change
}
```

When re-enabling (disabled: false):
```ts
if (session?.publicKey && user.assignedIp) {
  await execAsync(`sudo wg set wg0 peer ${session.publicKey} allowed-ips ${user.assignedIp}/32`);
  await execAsync(`sudo wg-quick save wg0`);
}
```
Always call `sudo wg-quick save wg0` after any `wg set` to persist to wg0.conf.

---

## FEATURE 5 — Audit Log Viewer in Admin UI

**What it does:** Display the audit_logs table visually in the admin dashboard.

**Current state:** audit_logs table exists in DB and GET /api/admin/audit route presumably exists, but nothing renders it in the UI.

### Recommended Approach
- Check vpn-server/src/app/api/admin/audit/route.ts — if it exists, it returns the logs
- Add a scrollable "Audit Log" panel at the bottom of admin/page.tsx
- Columns: Timestamp, Action, Detail, IP
- Load last 100 rows, sorted newest first
- Add a "Clear Logs" button (DELETE on the audit route)

---

## FEATURE 6 — Client: Real-Time Connection Stats (WPF)

**What it does:** Show live TX/RX stats in the WPF client window.

**Current state:** Client shows static "Connected" state with no live data.

### Recommended Approach

In MainWindow.xaml: add two TextBlock elements in the connected section:
  - "↑ TX: 0 MB"
  - "↓ RX: 0 MB"

In code-behind or VpnService.cs: start a DispatcherTimer (10s interval) after login.
Poll GET {ServerUrl}/api/admin/traffic, find the peer matching VpnService.ServerPublicKey, parse rxBytes/txBytes.
Format with a helper: bytes < 1MB → "X KB", bytes < 1GB → "X.X MB", else "X.X GB".

---

## MISC — Small Fixes & Cleanup

- admin/page.tsx line 303: still says "Active OpenVPN relay network segment" → change to "Active WireGuard network segment"
- deploy.sh still references MySQL → update to reflect SQLite reality (or rewrite entirely)
- Stale root files to delete and push: vpn-server-plan.md, changeOplan.txt, query, wglog.txt, vpn_001.ovpn, vpn_003.ovpn, dist/
- VpnService.cs line 19: Subnet default is "10.8.0.0/24" → change to "10.77.0.0/24"
- Verify .env is in .gitignore

---

## Architecture Reference

```
Custom-VPN/
├── cvpn-client/
│   ├── WireGuardTunnelManager.cs   ← wireguard.exe lifecycle + routing
│   ├── VpnService.cs               ← API calls (login/logout/reset)
│   └── MainWindow.xaml             ← WPF UI
└── vpn-server/
    ├── src/app/
    │   ├── admin/page.tsx          ← Admin dashboard (client component)
    │   └── api/
    │       ├── admin/users/        ← CRUD for VPN users
    │       ├── admin/networks/     ← CRUD for network segments
    │       ├── admin/audit/        ← Audit log fetch
    │       ├── client/login/       ← Client auth + wg key gen + config gen
    │       ├── client/logout/      ← Session cleanup
    │       └── client/config/      ← WireGuard .conf file delivery
    ├── src/lib/
    │   └── wg-agent.ts             ← WireGuard peer sync helpers
    └── prisma/
        ├── schema.prisma           ← Models: Admin, User, Session, Network, AuditLog
        └── dev.db                  ← Live DB on server at /var/www/Custom-VPN/vpn-server/prisma/dev.db
```

Key constraints:
- Server non-root user has NOPASSWD sudo for /usr/bin/wg and /usr/sbin/iptables
- All exec calls must use sudo prefix
- After any `wg set` change: always call `sudo wg-quick save wg0` to persist
- dist/ at project root is stale build output — ignore
