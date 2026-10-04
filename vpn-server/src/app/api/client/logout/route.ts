import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/prisma";

export async function POST(req: NextRequest) {
  try {
    const { username, deviceId } = await req.json();

    if (!username || !deviceId) {
      return NextResponse.json({ error: "Username and deviceId required" }, { status: 400 });
    }

    const user = await prisma.user.findUnique({
      where: { username },
      include: { session: true },
    });

    if (!user || !user.session) {
      return NextResponse.json({ success: true, message: "No active session to clear" });
    }

    // Only allow clearing if the device matches
    if (user.session.deviceId === deviceId) {
      await prisma.session.delete({
        where: { userId: user.id },
      });
      return NextResponse.json({ success: true, message: "Session successfully released" });
    }

    return NextResponse.json(
      { error: "Cannot logout: device ID does not match active session" },
      { status: 403 }
    );
  } catch (error) {
    console.error("Client logout error:", error);
    return NextResponse.json({ error: "Failed to release session" }, { status: 500 });
  }
}
