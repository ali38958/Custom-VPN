import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/prisma";

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const username = searchParams.get("username");
    const deviceId = searchParams.get("deviceId");

    if (!username || !deviceId) {
      return NextResponse.json({ error: "Authentication parameters required" }, { status: 400 });
    }

    const user = await prisma.user.findUnique({
      where: { username },
      include: { session: true },
    });

    if (!user || !user.session || user.session.deviceId !== deviceId) {
      return NextResponse.json({ error: "Active session required" }, { status: 401 });
    }

    // Update heartbeat
    await prisma.session.update({
      where: { userId: user.id },
      data: { lastSeenAt: new Date(), isOnline: true },
    });

    // Fetch peers in same network
    const peers = await prisma.user.findMany({
      where: {
        networkId: user.networkId,
        id: { not: user.id },
        disabled: false,
      },
      select: {
        id: true,
        username: true,
        assignedIp: true,
        session: {
          select: {
            deviceName: true,
            isOnline: true,
            lastSeenAt: true,
          },
        },
      },
    });

    type PeerRecord = {
      id: number;
      username: string;
      assignedIp: string | null;
      session: {
        deviceName: string | null;
        isOnline: boolean;
        lastSeenAt: Date;
      } | null;
    };

    return NextResponse.json({
      peers: (peers as PeerRecord[]).map((p: PeerRecord) => {
        const isRecentlySeen = p.session?.lastSeenAt 
          ? (new Date().getTime() - new Date(p.session.lastSeenAt).getTime() < 30000) 
          : false;

        return {
          id: p.id,
          username: p.username,
          ip: p.assignedIp,
          deviceName: p.session?.deviceName || "Unknown",
          isOnline: isRecentlySeen,
        };
      }),
    });
  } catch (error) {
    console.error("Peers fetch error:", error);
    return NextResponse.json({ error: "Failed to fetch peers" }, { status: 500 });
  }
}
