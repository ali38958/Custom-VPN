import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/prisma";
import { getCurrentAdmin, hashPassword } from "@/lib/auth";

export async function GET() {
  const admin = await getCurrentAdmin();
  if (!admin) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const users = await prisma.user.findMany({
    orderBy: { createdAt: "desc" },
    include: {
      network: true,
      session: true,
    },
  });

  return NextResponse.json({ users });
}

export async function POST(req: NextRequest) {
  const admin = await getCurrentAdmin();
  if (!admin) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  try {
    const { username, password, assignedIp, networkId } = await req.json();

    if (!username || !password) {
      return NextResponse.json(
        { error: "Username and password are required" },
        { status: 400 }
      );
    }

    const existingUser = await prisma.user.findUnique({
      where: { username },
    });

    if (existingUser) {
      return NextResponse.json(
        { error: "Username already exists" },
        { status: 400 }
      );
    }

    if (assignedIp) {
      const existingIp = await prisma.user.findUnique({
        where: { assignedIp },
      });
      if (existingIp) {
        return NextResponse.json(
          { error: "IP address already assigned to another user" },
          { status: 400 }
        );
      }
    }

    const passwordHash = await hashPassword(password);

    const newUser = await prisma.user.create({
      data: {
        username,
        passwordHash,
        assignedIp: assignedIp || null,
        networkId: networkId ? parseInt(networkId, 10) : null,
      },
      include: {
        network: true,
      },
    });

    await prisma.auditLog.create({
      data: {
        action: "CREATE_USER",
        detail: `Created user ${username} with assigned IP ${assignedIp || "none"}`,
      },
    });

    return NextResponse.json({ user: newUser });
  } catch (error) {
    console.error("Create user error:", error);
    return NextResponse.json({ error: "Failed to create user" }, { status: 500 });
  }
}
