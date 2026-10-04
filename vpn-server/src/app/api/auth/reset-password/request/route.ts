import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/prisma";
import crypto from "crypto";
import { sendPasswordResetEmail } from "@/lib/email";

export async function POST(req: NextRequest) {
  try {
    const { email } = await req.json();

    if (!email) {
      return NextResponse.json({ error: "Email is required" }, { status: 400 });
    }

    const admin = await prisma.admin.findFirst({
      where: {
        OR: [
          { email },
          { email: null }, // Fallback if no email configured yet, matches owner
        ],
      },
    });

    if (admin) {
      // Create a reset token
      const token = crypto.randomBytes(32).toString("hex");
      const expiresAt = new Date(Date.now() + 30 * 60 * 1000); // 30 minutes

      await prisma.passwordResetToken.create({
        data: {
          token,
          expiresAt,
        },
      });

      const appUrl = process.env.APP_URL || "http://localhost:3000";
      const resetLink = `${appUrl}/reset-password/confirm?token=${token}`;

      try {
        await sendPasswordResetEmail(email, resetLink);
      } catch (err) {
        console.error("Failed to send email via SMTP:", err);
      }
    }

    return NextResponse.json({
      success: true,
      message: "If an account matches, a reset link was dispatched.",
    });
  } catch (error) {
    console.error("Reset request error:", error);
    return NextResponse.json({ error: "Failed to process request" }, { status: 500 });
  }
}
