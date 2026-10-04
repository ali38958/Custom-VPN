import nodemailer from "nodemailer";

export function getEmailTransporter() {
  const host = process.env.SMTP_HOST || "smtp.gmail.com";
  const port = parseInt(process.env.SMTP_PORT || "465", 10);
  const user = process.env.SMTP_USER;
  const pass = process.env.SMTP_PASS;

  if (!user || !pass) {
    console.warn("SMTP credentials not fully configured in environment.");
  }

  return nodemailer.createTransport({
    host,
    port,
    secure: port === 465,
    auth: {
      user,
      pass,
    },
  });
}

export async function sendPasswordResetEmail(to: string, resetLink: string) {
  const transporter = getEmailTransporter();
  const from = process.env.SMTP_FROM || `"Custom VPN" <${process.env.SMTP_USER}>`;

  const info = await transporter.sendMail({
    from,
    to,
    subject: "Reset Password - Custom VPN Admin",
    text: `You requested a password reset for your Custom VPN admin account.\n\nPlease click the link below to set a new password:\n${resetLink}\n\nThis link is valid for 30 minutes. If you did not request this, please ignore this email.`,
    html: `
      <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; background: #0f172a; color: #f8fafc; border-radius: 12px; border: 1px solid #1e293b;">
        <h2 style="color: #38bdf8; margin-bottom: 16px;">Custom VPN Admin Password Reset</h2>
        <p style="color: #94a3b8; font-size: 15px; line-height: 1.5;">
          A password reset was requested for your administrator account (<strong>BOwnerNo1</strong>).
        </p>
        <div style="margin: 28px 0;">
          <a href="${resetLink}" style="background: linear-gradient(135deg, #0ea5e9, #0284c7); color: #ffffff; padding: 12px 24px; border-radius: 8px; text-decoration: none; font-weight: 600; display: inline-block;">
            Reset Password
          </a>
        </div>
        <p style="color: #64748b; font-size: 13px;">
          This link will expire in 30 minutes. If you did not initiate this request, you can safely disregard this message.
        </p>
      </div>
    `,
  });

  return info;
}
