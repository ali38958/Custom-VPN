import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import Navbar from "@/components/Navbar";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "PrivateNet — Encrypted Virtual Network",
  description: "Self-hosted WireGuard private network for seamless peer-to-peer connectivity and high-speed file transfer.",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html
      lang="en"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased dark`}
    >
      <body className="min-h-full flex flex-col bg-zinc-950 text-zinc-100 selection:bg-cyan-500/20 selection:text-cyan-300">
        <Navbar />
        <main className="flex-1">{children}</main>
        <footer className="border-t border-zinc-900 py-6 text-center text-xs text-zinc-600">
          <p>PrivateNet Server &bull; Node &bull; WireGuard Virtual Subnet 10.77.0.0/22 &bull; resolvia.cc.cd</p>
        </footer>
      </body>
    </html>
  );
}
