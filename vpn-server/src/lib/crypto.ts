import crypto from "crypto";

export function sha256(data: string): string {
  return crypto.createHash("sha256").update(data).digest("hex");
}

export function generateRandomCode(bytes = 16): string {
  // Generates e.g. 32-character hex or alphanumeric
  return crypto.randomBytes(bytes).toString("hex");
}
