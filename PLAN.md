# Custom VPN System: Implementation Plan

Working Name: **Custom-VPN**.

---

## 1. High-Level Architecture

The system consists of two main parts:
1. **Server (vpn-server):** A Next.js-based web portal and API backend with an SQLite3 database. It runs on Ubuntu and manages users, networks, device sessions, and the WireGuard VPN interface (via a root-level agent).
2. **Client (cvpn-client):** A .NET GUI application (Windows 10/11 x64) running as a SYSTEM service in the background with a modern user interface. It handles VPN connections, device-locked sessions, and direct peer-to-peer file transfers.

### Core Architecture Shifts (Based on latest requirements)
- **Database:** Switched from MySQL to **SQLite3** for lightweight operation.
- **Admin Auth:** Single-owner system. Only one admin (`BOwnerNo1` with default password `passwordowner1`).
- **Device-Locked Sessions:** An account can only be logged in on **one device at a time**. The server records the device fingerprint. If another device tries to log in with the same account, it is blocked until the first device logs out or the session is reset.
- **System-Level Client:** The client app must run in the background with `SYSTEM` access level to maintain the VPN connection seamlessly.

---

## 2. Server Specifications (vpn-server)

### Tech Stack
- **Framework:** Next.js (App Router) for API endpoints and Admin/User Portal UI.
- **Database:** SQLite3 (via Prisma ORM).
- **Email:** Nodemailer (using a Google App Password provided in `.env`).
- **VPN Core:** WireGuard + a small root-level `wg-agent` to apply configurations.

### 2.1 The Single Admin (BOwnerNo1)
- The admin is the master controller of the VPN server.
- Default login: `BOwnerNo1` / `passwordowner1`.
- Has access to a sleek, cool profile/login page.
- Password can be changed using a "Reset Password" flow (sends an email via Google App Password).

### 2.2 Admin Capabilities
- **Create/Manage Users:** Add new users, set their names, and initial passwords.
- **Network Management:** Create network segments and assign users specific IP addresses.
- **Enable/Disable Accounts:** Suspend access for any user.
- **Monitor Connections:** See who is currently connected to the VPN and from which device.
- **Force Session Reset:** Manually kick a device off a user's account to free it up for another device.

### 2.3 User Device & Session Lock Mechanism
- When a user logs in from the .NET client, the client generates a unique device identifier and keypair.
- The server records this device for the user account.
- **Strict 1:1 Lock:** As long as that device holds the session, no other device can log in to that account.
- **Offline Persistence:** Even if the device disconnects or goes offline, the lock remains until a manual logout occurs.
- **Logout:** Clears the session on the server, allowing a new device to log in.
- **Lost Device Recovery:** If a user loses their device, the client offers a "Forgot/Lost Device" request button. This securely signals the server (or admin) to drop the old device's session, freeing the account for the new device.
- **Security:** End-to-end authenticated requests ensure malicious actors cannot spoof a logout/reset request to hijack the session.

### 2.4 API & Routing
- Web UI for admin to manage everything.
- API endpoints for the .NET client to:
  - Authenticate and establish the device lock.
  - Fetch VPN config (WireGuard keys/IPs).
  - Get network peers for file transfer.
  - Perform heartbeats and handle manual session resets.

---

## 3. Client Specifications (cvpn-client)

### Tech Stack
- **Framework:** .NET 8 (Avalonia UI or WPF), packaged as a self-contained executable.
- **OS Support:** Windows 10/11 x64.
- **Privilege:** Runs as a Windows Service (`SYSTEM` level) to maintain WireGuard tunnels and manage firewall rules, with a normal-user GUI for interaction.

### 3.1 Client GUI & UX
- Modern, visually striking, and easy-to-use interface.
- **Login Screen:** User enters credentials provided by the admin. Includes a setting/button to configure the target server (by IP or domain name) so the client can connect to any hosted instance.
- **Dashboard:**
  - Status indicator (Connected/Disconnected).
  - Big toggle button to route all internet traffic through the VPN (encrypted).
  - List of other devices on the network (if admin granted visibility).
- **Session Management:** "Logout" button to cleanly release the account lock on the server.
- **File Transfer:** Built-in system to send/receive files directly to/from other peers on the VPN network (P2P over the encrypted tunnel).

---

## 4. Execution Plan (Step-by-Step)

**Phase 1: Server Foundation (vpn-server)**
1. Initialize a Next.js project with Prisma and SQLite3 in the `vpn-server` folder.
2. Setup `.env` and `.env.example` (Google App Password, DB url, etc.).
3. Implement the Admin schema, seeding the `BOwnerNo1` account.
4. Build the cool admin login UI and password reset flow via Nodemailer.
5. Build the Admin Dashboard (CRUD for users, networks, IPs, and session monitoring/revocation).

**Phase 2: Client API & Session Logic (vpn-server)**
1. Implement the strict 1:1 device lock login endpoint.
2. Create the logout and "lost device" reset flows with cryptographic/token verification.
3. Integrate WireGuard config generation (`wg-agent` integration) triggered upon successful device lock.

**Phase 3: The Windows Client (cvpn-client)**
1. Setup a .NET 8 self-contained project.
2. Build the `SYSTEM` level helper service to handle WireGuard interfaces.
3. Build the User GUI (Modern design) for login, device locking, and session reset.
4. Implement the "Route all traffic" VPN toggle.
5. Implement the P2P file transfer mechanism over the VPN IPs.

**Phase 4: Deployment & Polish**
1. Ensure the server gracefully handles SQLite locks and concurrent Next.js API requests.
2. Package the Windows client into a single installer.
3. Test end-to-end encryption, device lock persistence, and file transfer speeds.