import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/prisma";
import { getCurrentAdmin, hashPassword } from "@/lib/auth";

export async function PATCH(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const admin = await getCurrentAdmin();
  if (!admin) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  try {
    const { id } = await params;
    const userId = parseInt(id, 10);
    const body = await req.json();

    const dataToUpdate: Record<string, unknown> = {};

    if (typeof body.disabled === "boolean") {
      dataToUpdate.disabled = body.disabled;
    }

    if (body.assignedIp !== undefined) {
      dataToUpdate.assignedIp = body.assignedIp || null;
    }

    if (body.networkId !== undefined) {
      dataToUpdate.networkId = body.networkId ? parseInt(body.networkId, 10) : null;
    }

    if (body.password) {
      dataToUpdate.passwordHash = await hashPassword(body.password);
    }

    // Force disconnect session if disabled or explicitly requested
    if (body.clearSession || body.disabled) {
      await prisma.session.deleteMany({
        where: { userId },
      });
    }

    const updatedUser = await prisma.user.update({
      where: { id: userId },
      data: dataToUpdate,
      include: {
        network: true,
        session: true,
      },
    });

    await prisma.auditLog.create({
      data: {
        action: "UPDATE_USER",
        detail: `Updated user ID ${userId} (disabled: ${body.disabled}, clearSession: ${!!body.clearSession})`,
      },
    });

    return NextResponse.json({ user: updatedUser });
  } catch (error) {
    console.error("Update user error:", error);
    return NextResponse.json({ error: "Failed to update user" }, { status: 500 });
  }
}

export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const admin = await getCurrentAdmin();
  if (!admin) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  try {
    const { id } = await params;
    const userId = parseInt(id, 10);

    await prisma.user.delete({
      where: { id: userId },
    });

    await prisma.auditLog.create({
      data: {
        action: "DELETE_USER",
        detail: `Deleted user ID ${userId}`,
      },
    });

    return NextResponse.json({ success: true });
  } catch (error) {
    console.error("Delete user error:", error);
    return NextResponse.json({ error: "Failed to delete user" }, { status: 500 });
  }
}
