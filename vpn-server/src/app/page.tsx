import Link from "next/link";
import { Zap, HardDrive, ArrowRight, ShieldCheck } from "lucide-react";

export default function Home() {
  return (
    <div className="relative overflow-hidden">
      {/* Hero Section */}
      <section className="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pt-20 pb-24 text-center">
        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full border border-[var(--border)] bg-[var(--card-bg)] text-[var(--accent)] text-xs font-medium mb-8 shadow-xs">
          <span className="w-2 h-2 rounded-full bg-[var(--success)] animate-pulse" />
          <span className="font-mono">WireGuard Node Active &bull; Subnet 10.77.0.0/24</span>
        </div>

        <h1 className="text-4xl sm:text-6xl font-extrabold tracking-tight max-w-4xl mx-auto leading-tight text-[var(--text-primary)]">
          A Private Encrypted Mesh{" "}
          <span className="text-[var(--accent)]">
            That Exists Only For You.
          </span>
        </h1>

        <p className="mt-6 text-base sm:text-lg text-[var(--text-secondary)] max-w-2xl mx-auto leading-relaxed">
          High-performance virtual network connecting authorized devices over isolated subnets. 
          Enforcing strict 1:1 single-device locking, high-speed P2P file transfers, and root-level management.
        </p>

        <div className="mt-10 flex flex-wrap items-center justify-center gap-3.5">
          <Link
            href="/login"
            className="pn-btn pn-btn-primary px-6 py-3 rounded-lg text-sm font-semibold shadow-sm inline-flex items-center gap-2"
          >
            <span>Admin Console</span>
            <ArrowRight className="w-4 h-4" />
          </Link>
          <Link
            href="/downloads"
            className="pn-btn pn-btn-secondary px-6 py-3 rounded-lg text-sm font-semibold inline-flex items-center gap-2"
          >
            <span>Windows Client</span>
          </Link>
        </div>

        {/* Feature Highlights Grid */}
        <div className="mt-24 grid grid-cols-1 md:grid-cols-3 gap-6 text-left">
          <div className="pn-card p-6 shadow-sm">
            <div className="w-10 h-10 rounded-lg bg-[var(--accent-soft)] border border-[var(--accent)]/20 flex items-center justify-center mb-4 text-[var(--accent)]">
              <Zap className="w-5 h-5" />
            </div>
            <h3 className="text-base font-semibold text-[var(--text-primary)]">Lightweight SQLite Engine</h3>
            <p className="mt-2 text-xs sm:text-sm text-[var(--text-secondary)] leading-relaxed">
              Fast local SQLite3 database storing network subnets, user accounts, and authenticated device state without external database bloat.
            </p>
          </div>

          <div className="pn-card p-6 shadow-sm">
            <div className="w-10 h-10 rounded-lg bg-[var(--success-soft)] border border-[var(--success-border)] flex items-center justify-center mb-4 text-[var(--success-text)]">
              <ShieldCheck className="w-5 h-5" />
            </div>
            <h3 className="text-base font-semibold text-[var(--text-primary)]">1:1 Device Concurrency Lock</h3>
            <p className="mt-2 text-xs sm:text-sm text-[var(--text-secondary)] leading-relaxed">
              Accounts are strictly bound to one hardware device at a time. Hijacking and simultaneous duplicate logins are completely blocked.
            </p>
          </div>

          <div className="pn-card p-6 shadow-sm">
            <div className="w-10 h-10 rounded-lg bg-[var(--accent-soft)] border border-[var(--accent)]/20 flex items-center justify-center mb-4 text-[var(--accent)]">
              <HardDrive className="w-5 h-5" />
            </div>
            <h3 className="text-base font-semibold text-[var(--text-primary)]">P2P File Transfer Protocol</h3>
            <p className="mt-2 text-xs sm:text-sm text-[var(--text-secondary)] leading-relaxed">
              Direct peer-to-peer chunked streaming over encrypted virtual IPs with complete SHA-256 integrity verification.
            </p>
          </div>
        </div>
      </section>
    </div>
  );
}
