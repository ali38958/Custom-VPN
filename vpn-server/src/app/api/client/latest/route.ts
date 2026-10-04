import { NextResponse } from "next/server";

export async function GET() {
  return NextResponse.json({
    version: "1.0.0",
    releaseDate: "2026-10-04",
    downloadUrl: "/downloads/PrivateNet-Installer.exe",
    sha256: "0000000000000000000000000000000000000000000000000000000000000000",
    mandatory: false,
    changelog: "Initial release of PrivateNet client for Windows.",
  });
}
