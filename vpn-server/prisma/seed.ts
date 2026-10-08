import { PrismaClient } from "@prisma/client";
import bcrypt from "bcryptjs";

const prisma = new PrismaClient();

async function main() {
  const existing = await prisma.admin.findUnique({
    where: { username: "Knight" },
  });

  const passwordHash = await bcrypt.hash("passwordowner1", 10);

  if (!existing) {
    await prisma.admin.create({
      data: {
        username: "Knight",
        passwordHash,
        email: process.env.ADMIN_EMAIL || "owner@example.com",
      },
    });
    console.log("Seeded default admin: Knight");
  } else {
    console.log("Admin Knight already exists.");
  }

  // Create default network if none exists
  const defaultNetwork = await prisma.network.findFirst();
  if (!defaultNetwork) {
    await prisma.network.create({
      data: {
        name: "Default Network",
        subnet: "10.77.0.0/24",
        description: "Primary VPN network subnet",
      },
    });
    console.log("Seeded default network: 10.77.0.0/24");
  }
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
