"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Key, LogOut } from "lucide-react";
import ThemeToggle from "./ThemeToggle";

interface AdminSession {
  id: number;
  username: string;
  email: string | null;
}

export default function Navbar() {
  const pathname = usePathname();
  const router = useRouter();
  const [admin, setAdmin] = useState<AdminSession | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch("/api/auth/me")
      .then((res) => res.json())
      .then((data) => {
        if (data.authenticated) {
          setAdmin(data.admin);
        } else {
          setAdmin(null);
        }
      })
      .catch(() => setAdmin(null))
      .finally(() => setLoading(false));
  }, [pathname]);

  const handleLogout = async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    setAdmin(null);
    router.push("/login");
  };

  return (
    <header className="sticky top-0 z-50 backdrop-blur-md bg-[var(--card-bg)]/90 border-b border-[var(--border)] transition-colors">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        {/* Brand */}
        <Link href="/" className="flex items-center gap-2.5 group">
          <div className="w-8 h-8 rounded-lg bg-[var(--accent)] text-white flex items-center justify-center font-bold text-xs tracking-wider shadow-sm group-hover:scale-105 transition-transform">
            PN
          </div>
          <div className="flex items-center gap-2">
            <span className="font-semibold text-sm tracking-tight text-[var(--text-primary)]">
              PrivateNet
            </span>
            <span className="hidden sm:inline-block text-[11px] font-mono px-2 py-0.5 rounded bg-[var(--sidebar-active)] text-[var(--text-secondary)] border border-[var(--border)]">
              v2.4.1
            </span>
          </div>
        </Link>

        {/* Navigation & Actions */}
        <nav className="flex items-center gap-2">
          <ThemeToggle />

          {admin ? (
            <>
              <Link
                href="/admin"
                className={`px-3 py-1.5 rounded-lg text-xs font-semibold font-mono transition-colors flex items-center gap-1.5 ${
                  pathname === "/admin"
                    ? "bg-[var(--accent-soft)] text-[var(--accent)] border border-[var(--accent)]/20"
                    : "text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--sidebar-active)]"
                }`}
              >
                <Key className="w-3.5 h-3.5 text-[var(--accent)]" />
                <span>Control Core</span>
              </Link>

              <div className="h-4 w-px bg-[var(--border)] mx-1" />

              <span className="text-xs font-mono text-[var(--success-text)] bg-[var(--success-soft)] border border-[var(--success-border)] px-2.5 py-0.5 rounded-full font-medium">
                {admin.username}
              </span>

              <button
                onClick={handleLogout}
                className="p-1.5 rounded-lg text-[var(--text-muted)] hover:text-[var(--danger)] hover:bg-[var(--danger-soft)] transition-colors cursor-pointer"
                title="Log out"
              >
                <LogOut className="w-4 h-4" />
              </button>
            </>
          ) : !loading ? (
            <Link
              href="/login"
              className="px-3.5 py-1.5 rounded-lg text-xs font-medium bg-[var(--accent)] hover:bg-[var(--accent-hover)] text-white transition-colors"
            >
              Sign In
            </Link>
          ) : null}
        </nav>
      </div>
    </header>
  );
}
