import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/prisma";
import { getCurrentAdmin } from "@/lib/auth";

export async function GET() {
  const admin = await getCurrentAdmin();
  if (!admin) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const networks = await prisma.network.findMany({
    include: {
      users: {
        select: {
          id: true,
          username: true,
          assignedIp: true,
          disabled: true,
          session: true,
        },
      },
    },
  });

  return NextResponse.json({ networks });
}

export async function POST(req: NextRequest) {
  const admin = await getCurrentAdmin();
  if (!admin) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  try {
    const { name, subnet, description } = await req.json();

    if (!name) {
      return NextResponse.json({ error: "Network name is required" }, { status: 400 });
    }

    const network = await prisma.network.create({
      data: {
        name,
        subnet: subnet || "10.77.0.0/24",
        description,
      },
    });

    return NextResponse.json({ network });
  } catch (error) {
    console.error("Create network error:", error);
    return NextResponse.json({ error: "Failed to create network" }, { status: 500 });
  }
}
