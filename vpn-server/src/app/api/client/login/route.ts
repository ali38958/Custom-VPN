import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/prisma";
import { verifyPassword } from "@/lib/auth";
import { getClientIp } from "@/lib/client-ip";

export async function POST(req: NextRequest) {
  try {
    const { username, password, deviceId, deviceName, deviceInfo } = await req.json();

    if (!username || !password || !deviceId) {
      return NextResponse.json({ error: "Username, password, and deviceId are required" }, { status: 400 });
    }

    const user = await prisma.user.findUnique({
      where: { username },
      include: { session: true, network: true },
    });

    if (!user) return NextResponse.json({ error: "Invalid credentials" }, { status: 401 });
    if (user.disabled) return NextResponse.json({ error: "Account suspended" }, { status: 403 });

    const isValid = await verifyPassword(password, user.passwordHash);
    if (!isValid) return NextResponse.json({ error: "Invalid credentials" }, { status: 401 });

    const clientIp = getClientIp(req);

    if (user.session) {
      if (user.session.deviceId !== deviceId) {
        return NextResponse.json({ error: "Device locked" }, { status: 409 });
      }
      await prisma.session.update({
        where: { userId: user.id },
        data: {
          deviceName: deviceName || user.session.deviceName,
          deviceInfo: deviceInfo || user.session.deviceInfo,
          clientIp, lastSeenAt: new Date(), isOnline: true,
        },
      });
    } else {
      await prisma.session.create({
        data: {
          userId: user.id, deviceId, deviceName, deviceInfo, clientIp, isOnline: true,
        },
      });
    }

      const serverPublicKey = process.env.SERVER_PUBLIC_KEY || "p+jZFDQaOQVcr84oqZiYDmamfTdID+3wUeH912gYLR8=";

      let clientPrivateKey = "";
      let clientPublicKey = "";
      if (process.platform === "linux") {
        const { exec } = require('child_process');
        const util = require('util');
        const execAsync = util.promisify(exec);
        try {
          const pkRes = await execAsync('wg genkey');
          clientPrivateKey = pkRes.stdout.trim();
          const pubRes = await execAsync(`echo "${clientPrivateKey}" | wg pubkey`);
          clientPublicKey = pubRes.stdout.trim();
          if (!clientPrivateKey || !clientPublicKey) throw new Error("Empty key output");
        } catch (keyErr) {
          console.error("[login] wg key generation failed:", keyErr);
          return NextResponse.json({ error: "VPN key generation failed on server. Contact admin." }, { status: 500 });
        }
      } else {
        // Fallback for local windows dev
        clientPrivateKey = "4F... (mock private key)";
        clientPublicKey = "mock_pub_key";
      }

      const { addPeerToWireGuard, removePeerFromWireGuard } = require("@/lib/wg-agent");
      if (user.session?.publicKey && user.session.publicKey !== clientPublicKey) {
        await removePeerFromWireGuard(user.session.publicKey);
      }
      if (user.assignedIp) {
        await addPeerToWireGuard(clientPublicKey, user.assignedIp);
      }

      await prisma.session.update({
        where: { userId: user.id },
        data: {
          publicKey: clientPublicKey,
        },
      });

      const wgConfigText = `[Interface]
PrivateKey = ${clientPrivateKey}
Address = ${user.assignedIp}/24

[Peer]
PublicKey = ${serverPublicKey}
Endpoint = 144.24.25.135:51820
AllowedIPs = ${user.network?.subnet || "10.77.0.0/24"}
PersistentKeepalive = 10
`;

    return NextResponse.json({
      success: true,
      user: { id: user.id, username: user.username, assignedIp: user.assignedIp },
      network: user.network,
      serverConfig: {
        serverPublicKey: process.env.SERVER_PUBLIC_KEY || "p+jZFDQaOQVcr84oqZiYDmamfTdID+3wUeH912gYLR8=",
        endpoint: "144.24.25.135:51820",
        subnet: user.network?.subnet || "10.77.0.0/24",
      },
      wireguardConfigUrl: req.nextUrl.origin + "/api/client/config?username=" + username,
      wireguardConfigText: wgConfigText
    });
  } catch (error) {
    console.error("Client login error:", error);
    return NextResponse.json({ error: "Authentication failed" }, { status: 500 });
  }
}
