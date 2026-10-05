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

    return NextResponse.json({
      success: true,
      user: { id: user.id, username: user.username, assignedIp: user.assignedIp },
      network: user.network,
      serverConfig: {
        endpoint: "144.24.25.135:443",
        subnet: user.network?.subnet || "10.8.0.0/24",
      },
      openvpnConfigUrl: "http://144.24.25.135:3001/api/client/config?username=" + username
    });
  } catch (error) {
    console.error("Client login error:", error);
    return NextResponse.json({ error: "Authentication failed" }, { status: 500 });
  }
}
