import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/prisma";
import { verifyPassword } from "@/lib/auth";

export async function POST(req: NextRequest) {
  try {
    const { username, password, newDeviceId, deviceName } = await req.json();

    if (!username || !password || !newDeviceId) {
      return NextResponse.json(
        { error: "Username, password, and newDeviceId are required" },
        { status: 400 }
      );
    }

    const user = await prisma.user.findUnique({
      where: { username },
      include: { session: true },
    });

    if (!user) {
      return NextResponse.json({ error: "Invalid credentials" }, { status: 401 });
    }

    const isValid = await verifyPassword(password, user.passwordHash);
    if (!isValid) {
      return NextResponse.json({ error: "Invalid credentials" }, { status: 401 });
    }

    // Force clear existing device session
    await prisma.session.deleteMany({
      where: { userId: user.id },
    });

    // Create session for new device immediately
    await prisma.session.create({
      data: {
        userId: user.id,
        deviceId: newDeviceId,
        deviceName: deviceName || "Recovered Device",
        isOnline: true,
      },
    });

    await prisma.auditLog.create({
      data: {
        action: "DEVICE_RESET",
        detail: `User ${username} reset device lock to new device ${newDeviceId}`,
      },
    });

    return NextResponse.json({
      success: true,
      message: "Previous device lock purged. Session transferred to this device.",
    });
  } catch (error) {
    console.error("Device reset error:", error);
    return NextResponse.json({ error: "Failed to reset device lock" }, { status: 500 });
  }
}
