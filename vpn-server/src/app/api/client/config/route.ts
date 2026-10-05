import { NextRequest, NextResponse } from "next/server";
import { ovpnConfig } from "./configData";

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const username = searchParams.get("username");

    if (!username) {
      return NextResponse.json({ error: "Username required" }, { status: 400 });
    }

    return new NextResponse(ovpnConfig, {
        headers: {
            "Content-Type": "application/x-openvpn-profile",
            "Content-Disposition": `attachment; filename="${username}.ovpn"`
        }
    });
  } catch (error) {
    console.error("Config fetch error:", error);
    return NextResponse.json({ error: "Failed to fetch config" }, { status: 500 });
  }
}
