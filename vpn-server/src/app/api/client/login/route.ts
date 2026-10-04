import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/prisma";
import { verifyPassword } from "@/lib/auth";
import { getClientIp } from "@/lib/client-ip";
import { addPeerToWireGuard } from "@/lib/wg-agent";

export async function POST(req: NextRequest) {
  try {
    const { username, password, deviceId, deviceName, deviceInfo, publicKey } = await req.json();

    if (!username || !password || !deviceId) {
      return NextResponse.json(
        { error: "Username, password, and deviceId are required" },
        { status: 400 }
      );
    }

    const user = await prisma.user.findUnique({
      where: { username },
      include: {
        session: true,
        network: true,
      },
    });

    if (!user) {
      return NextResponse.json({ error: "Invalid credentials" }, { status: 401 });
    }

    if (user.disabled) {
      return NextResponse.json(
        { error: "Account has been suspended by the administrator" },
        { status: 403 }
      );
    }

    const isValid = await verifyPassword(password, user.passwordHash);
    if (!isValid) {
      return NextResponse.json({ error: "Invalid credentials" }, { status: 401 });
    }

    const clientIp = getClientIp(req);

    // Strict 1:1 Device Lock Check
    if (user.session) {
      if (user.session.deviceId !== deviceId) {
        return NextResponse.json(
          {
            error: "Device locked: Account is actively bound to another device. Manual logout from that device or admin release required.",
            lockedDeviceId: user.session.deviceId,
            lockedDeviceName: user.session.deviceName,
          },
          { status: 409 }
        );
      } else {
        // Same device reconnecting: update session info
        await prisma.session.update({
          where: { userId: user.id },
          data: {
            deviceName: deviceName || user.session.deviceName,
            deviceInfo: deviceInfo || user.session.deviceInfo,
            publicKey: publicKey || user.session.publicKey,
            clientIp,
            lastSeenAt: new Date(),
            isOnline: true,
          },
        });
      }
    } else {
      // Create new exclusive session lock
      await prisma.session.create({
        data: {
          userId: user.id,
          deviceId,
          deviceName,
          deviceInfo,
          publicKey,
          clientIp,
          isOnline: true,
        },
      });
    }

    // Register peer in WireGuard interface if publicKey and assignedIp exist
    if (publicKey && user.assignedIp) {
      await addPeerToWireGuard(publicKey, user.assignedIp);
    }

    return NextResponse.json({
      success: true,
      user: {
        id: user.id,
        username: user.username,
        assignedIp: user.assignedIp,
      },
      network: user.network,
      serverConfig: {
        endpoint: process.env.SERVER_ENDPOINT || "resolvia.cc.cd:51820",
        serverPublicKey: process.env.SERVER_PUBLIC_KEY || "mock-key",
        subnet: user.network?.subnet || "10.77.0.0/24",
      },
    });
  } catch (error) {
    console.error("Client login error:", error);
    return NextResponse.json({ error: "Authentication failed" }, { status: 500 });
  }
}
