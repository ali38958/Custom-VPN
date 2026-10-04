#!/usr/bin/env node
/**
 * wg-agent: Root-privileged WireGuard synchronization daemon
 * Listens on /run/wg-agent.sock
 * Communicates with WireGuard kernel module and updates peer handshakes
 */

import net from "net";
import fs from "fs";
import { exec, execSync } from "child_process";
import { promisify } from "util";
import mysql from "mysql2/promise";

const execAsync = promisify(exec);

const SOCKET_PATH = process.env.WG_AGENT_SOCKET || "/run/wg-agent.sock";
const WG_INTERFACE = process.env.WG_INTERFACE || "wg0";
const DB_URL = process.env.DATABASE_URL || "mysql://root:password@127.0.0.1:3306/privatenet";

function intToIp(int) {
  return [
    (int >>> 24) & 255,
    (int >>> 16) & 255,
    (int >>> 8) & 255,
    int & 255,
  ].join(".");
}

async function getDbConnection() {
  return mysql.createConnection(DB_URL);
}

/**
 * Regenerates WireGuard peer configuration from active devices in MySQL
 * and applies it cleanly with wg syncconf
 */
async function syncPeers() {
  console.log(`[wg-agent] Starting WireGuard peer sync...`);
  let conn;
  try {
    conn = await getDbConnection();
    const [rows] = await conn.execute(
      "SELECT id, name, public_key, ip_address FROM devices WHERE status = 'active' AND public_key IS NOT NULL"
    );

    // Format peers into wg configuration
    let peerConfig = "";
    for (const dev of rows) {
      peerConfig += `\n[Peer]\n# Device: ${dev.name} (id: ${dev.id})\n`;
      peerConfig += `PublicKey = ${dev.public_key}\n`;
      peerConfig += `AllowedIPs = ${intToIp(dev.ip_address)}/32\n`;
    }

    const tmpConfPath = `/tmp/wg-peers-sync-${Date.now()}.conf`;
    fs.writeFileSync(tmpConfPath, peerConfig, { mode: 0o600 });

    try {
      // Apply delta sync to WireGuard interface
      await execAsync(`wg syncconf ${WG_INTERFACE} ${tmpConfPath}`);
      console.log(`[wg-agent] Successfully synced ${rows.length} peers to ${WG_INTERFACE}`);
    } finally {
      if (fs.existsSync(tmpConfPath)) {
        fs.unlinkSync(tmpConfPath);
      }
    }

    return { success: true, count: rows.length };
  } catch (err) {
    console.error(`[wg-agent] Sync failed:`, err);
    return { success: false, error: err.message };
  } finally {
    if (conn) await conn.end();
  }
}

/**
 * Reads `wg show <interface> dump` and updates `last_handshake_at` in the database
 */
async function updateHandshakes() {
  let conn;
  try {
    const { stdout } = await execAsync(`wg show ${WG_INTERFACE} dump`);
    const lines = stdout.trim().split("\n");
    if (lines.length <= 1) return; // First line is interface info

    conn = await getDbConnection();

    for (let i = 1; i < lines.length; i++) {
      const parts = lines[i].split("\t");
      if (parts.length >= 5) {
        const pubKey = parts[0];
        const lastHandshakeEpoch = parseInt(parts[4], 10);

        if (lastHandshakeEpoch > 0) {
          const handshakeDate = new Date(lastHandshakeEpoch * 1000);
          await conn.execute(
            "UPDATE devices SET last_handshake_at = ? WHERE public_key = ? AND status = 'active'",
            [handshakeDate, pubKey]
          );
        }
      }
    }
  } catch (err) {
    // Interface might not be up yet or running in non-linux dev
  } finally {
    if (conn) await conn.end();
  }
}

// Start periodic handshake status updater (every 30 seconds)
setInterval(updateHandshakes, 30000);

// Setup Unix domain socket
if (fs.existsSync(SOCKET_PATH)) {
  fs.unlinkSync(SOCKET_PATH);
}

const server = net.createServer((socket) => {
  socket.on("data", async (chunk) => {
    const cmd = chunk.toString().trim();
    if (cmd === "sync") {
      const res = await syncPeers();
      socket.write(JSON.stringify(res) + "\n");
    } else if (cmd === "status") {
      await updateHandshakes();
      socket.write(JSON.stringify({ status: "ok" }) + "\n");
    } else {
      socket.write(JSON.stringify({ error: "unknown_command" }) + "\n");
    }
    socket.end();
  });
});

server.listen(SOCKET_PATH, () => {
  console.log(`[wg-agent] Listening on ${SOCKET_PATH}`);
  try {
    fs.chmodSync(SOCKET_PATH, 0o660);
  } catch (e) {}
  // Initial sync on boot
  syncPeers();
});

process.on("SIGINT", () => {
  if (fs.existsSync(SOCKET_PATH)) fs.unlinkSync(SOCKET_PATH);
  process.exit(0);
});

process.on("SIGTERM", () => {
  if (fs.existsSync(SOCKET_PATH)) fs.unlinkSync(SOCKET_PATH);
  process.exit(0);
});
