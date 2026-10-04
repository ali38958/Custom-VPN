import { NextResponse } from "next/server";
import { getCurrentAdmin } from "@/lib/auth";

export async function GET() {
  const admin = await getCurrentAdmin();
  if (!admin) {
    return NextResponse.json({ authenticated: false, admin: null });
  }

  return NextResponse.json({
    authenticated: true,
    admin: {
      id: admin.id,
      username: admin.username,
      email: admin.email,
      createdAt: admin.createdAt,
    },
  });
}
