import { NextRequest, NextResponse } from "next/server";
import fs from "fs";

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const username = searchParams.get("username");

    if (!username) {
      return NextResponse.json({ error: "Username required" }, { status: 400 });
    }

    // For now, always return the pre-generated client1.ovpn
    const configPath = "/home/ubuntu/openvpn-ca/client1.ovpn";
    if (fs.existsSync(configPath)) {
        const config = fs.readFileSync(configPath, "utf-8");
        return new NextResponse(config, {
            headers: {
                "Content-Type": "application/x-openvpn-profile",
                "Content-Disposition": `attachment; filename="${username}.ovpn"`
            }
        });
    } else {
        return NextResponse.json({ error: "Config not found on server" }, { status: 404 });
    }
  } catch (error) {
    console.error("Config fetch error:", error);
    return NextResponse.json({ error: "Failed to fetch config" }, { status: 500 });
  }
}
