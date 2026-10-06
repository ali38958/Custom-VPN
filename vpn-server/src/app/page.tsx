import Link from "next/link";
import { Shield, Zap, Lock, HardDrive, Cpu, Terminal, ArrowRight, ShieldCheck } from "lucide-react";

export default function Home() {
  return (
    <div className="relative overflow-hidden">
      {/* Background glow effects */}
      <div className="absolute top-1/4 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-gradient-to-tr from-cyan-600/10 to-emerald-500/10 rounded-full blur-3xl pointer-events-none" />

      {/* Hero Section */}
      <section className="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pt-20 pb-24 text-center">
        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full border border-cyan-500/30 bg-cyan-950/40 text-cyan-400 text-xs font-medium mb-8 backdrop-blur-sm">
          <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
          <span>Hub Endpoint Online: PrivateNet:51820</span>
        </div>

        <h1 className="text-4xl sm:text-6xl font-extrabold tracking-tight max-w-4xl mx-auto leading-tight">
          A Private Encrypted Mesh{" "}
          <span className="bg-gradient-to-r from-cyan-400 via-teal-300 to-emerald-400 bg-clip-text text-transparent">
            That Exists Only For You.
          </span>
        </h1>

        <p className="mt-6 text-lg sm:text-xl text-zinc-400 max-w-2xl mx-auto leading-relaxed">
          High-performance OpenVPN connecting authorized devices over isolated virtual LANs. 
          Enforcing strict 1:1 single-device locking, high-speed P2P file transfers, and root-level management.
        </p>

        <div className="mt-10 flex flex-wrap items-center justify-center gap-4">
          <Link
            href="/login"
            className="px-6 py-3 rounded-xl bg-gradient-to-r from-cyan-500 to-emerald-500 hover:from-cyan-400 hover:to-emerald-400 text-zinc-950 font-bold shadow-lg shadow-cyan-500/25 transition-all flex items-center gap-2 text-base"
          >
            <span>Admin Console Login</span>
            <ArrowRight className="w-4 h-4" />
          </Link>
          <Link
            href="/downloads"
            className="px-6 py-3 rounded-xl bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 text-zinc-200 font-semibold transition-colors flex items-center gap-2 text-base"
          >
            <span>Windows Client (.NET 8)</span>
          </Link>
        </div>

        {/* Feature Highlights Grid */}
        <div className="mt-28 grid grid-cols-1 md:grid-cols-3 gap-8 text-left">
          <div className="p-6 rounded-2xl bg-zinc-900/50 border border-zinc-800/80 backdrop-blur-sm hover:border-cyan-500/30 transition-colors">
            <div className="w-12 h-12 rounded-xl bg-cyan-950/80 border border-cyan-800/50 flex items-center justify-center mb-5 text-cyan-400">
              <Zap className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-zinc-100">Lightweight SQLite Engine</h3>
            <p className="mt-2 text-sm text-zinc-400 leading-relaxed">
              Ultra-fast local SQLite3 database storing network subnets, user accounts, and authenticated device state without external database bloat.
            </p>
          </div>

          <div className="p-6 rounded-2xl bg-zinc-900/50 border border-zinc-800/80 backdrop-blur-sm hover:border-emerald-500/30 transition-colors">
            <div className="w-12 h-12 rounded-xl bg-emerald-950/80 border border-emerald-800/50 flex items-center justify-center mb-5 text-emerald-400">
              <ShieldCheck className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-zinc-100">1:1 Device Concurrency Lock</h3>
            <p className="mt-2 text-sm text-zinc-400 leading-relaxed">
              Accounts are strictly bound to one hardware device at a time. Hijacking and simultaneous duplicate logins are completely blocked.
            </p>
          </div>

          <div className="p-6 rounded-2xl bg-zinc-900/50 border border-zinc-800/80 backdrop-blur-sm hover:border-indigo-500/30 transition-colors">
            <div className="w-12 h-12 rounded-xl bg-indigo-950/80 border border-indigo-800/50 flex items-center justify-center mb-5 text-indigo-400">
              <HardDrive className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-zinc-100">P2P File Transfer Protocol</h3>
            <p className="mt-2 text-sm text-zinc-400 leading-relaxed">
              Direct peer-to-peer chunked streaming over encrypted OpenVPN virtual IPs with full SHA-256 integrity verification.
            </p>
          </div>
        </div>
      </section>
    </div>
  );
}
