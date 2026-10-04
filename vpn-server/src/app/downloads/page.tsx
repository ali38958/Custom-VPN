import Link from "next/link";
import { Download, Shield, Monitor, FileCode, CheckCircle, ExternalLink } from "lucide-react";

export default function DownloadsPage() {
  return (
    <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-12 space-y-12">
      <div className="text-center max-w-2xl mx-auto space-y-3">
        <h1 className="text-3xl font-extrabold tracking-tight text-zinc-100">
          Client Downloads &amp; Setup
        </h1>
        <p className="text-sm text-zinc-400">
          Install the client utility to establish encrypted tunnels, discover mesh peers, and transfer files.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
        {/* Main Client App Card */}
        <div className="p-8 rounded-3xl bg-zinc-900/60 border border-zinc-800 backdrop-blur-xl flex flex-col justify-between space-y-6">
          <div className="space-y-4">
            <div className="w-12 h-12 rounded-2xl bg-cyan-950 border border-cyan-800/60 flex items-center justify-center text-cyan-400">
              <Monitor className="w-6 h-6" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-zinc-100">PrivateNet for Windows</h2>
              <span className="text-xs font-mono text-cyan-400">v1.0.0 &bull; Windows 10/11 x64</span>
            </div>
            <p className="text-sm text-zinc-400 leading-relaxed">
              Avalonia GUI desktop application paired with an unprivileged helper service. Generates cryptographic keys locally via DPAPI and automatically handles WireGuard routing.
            </p>

            <div className="space-y-2 text-xs text-zinc-400 pt-2">
              <div className="flex items-center gap-2">
                <CheckCircle className="w-4 h-4 text-emerald-400" />
                <span>One-click one-time code enrollment</span>
              </div>
              <div className="flex items-center gap-2">
                <CheckCircle className="w-4 h-4 text-emerald-400" />
                <span>Direct peer-to-peer file transfer engine</span>
              </div>
              <div className="flex items-center gap-2">
                <CheckCircle className="w-4 h-4 text-emerald-400" />
                <span>System tray status and quick connect</span>
              </div>
            </div>
          </div>

          <a
            href="/api/client/latest"
            className="w-full py-3.5 rounded-xl bg-gradient-to-r from-cyan-500 to-emerald-500 hover:from-cyan-400 hover:to-emerald-400 text-zinc-950 font-bold text-sm shadow-lg shadow-cyan-500/20 transition-all flex items-center justify-center gap-2"
          >
            <Download className="w-4 h-4" />
            <span>Download Installer (.exe)</span>
          </a>
        </div>

        {/* Prerequisites & Manual WireGuard */}
        <div className="p-8 rounded-3xl bg-zinc-900/60 border border-zinc-800 backdrop-blur-xl flex flex-col justify-between space-y-6">
          <div className="space-y-4">
            <div className="w-12 h-12 rounded-2xl bg-emerald-950 border border-emerald-800/60 flex items-center justify-center text-emerald-400">
              <Shield className="w-6 h-6" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-zinc-100">Prerequisite: WireGuard NT</h2>
              <span className="text-xs font-mono text-emerald-400">Official Windows Driver</span>
            </div>
            <p className="text-sm text-zinc-400 leading-relaxed">
              PrivateNet utilizes the official in-kernel WireGuard driver for high throughput and optimal power efficiency. Please ensure the WireGuard package is installed.
            </p>

            <div className="p-4 rounded-xl bg-zinc-950 border border-zinc-800/80 text-xs text-zinc-400 space-y-1">
              <div className="font-semibold text-zinc-200">Alternative / Linux &amp; Mobile:</div>
              <p>
                Linux, macOS, and iOS/Android users can run standard WireGuard configurations with custom client keys upon request.
              </p>
            </div>
          </div>

          <a
            href="https://www.wireguard.com/install/"
            target="_blank"
            rel="noreferrer"
            className="w-full py-3.5 rounded-xl bg-zinc-800 hover:bg-zinc-700 text-zinc-100 font-semibold text-sm transition-colors flex items-center justify-center gap-2"
          >
            <span>Official WireGuard Downloads</span>
            <ExternalLink className="w-4 h-4" />
          </a>
        </div>
      </div>

      {/* 3 Steps Guide */}
      <div className="p-8 rounded-3xl bg-zinc-900/30 border border-zinc-800 space-y-6">
        <h3 className="text-lg font-bold text-zinc-100">Setup Walkthrough</h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="space-y-2">
            <div className="w-8 h-8 rounded-lg bg-zinc-800 text-cyan-400 font-bold text-sm flex items-center justify-center">
              1
            </div>
            <h4 className="font-semibold text-sm text-zinc-200">Register Device</h4>
            <p className="text-xs text-zinc-400">
              Go to your portal dashboard and click &ldquo;Add New Device&rdquo; to generate a 15-minute enrollment ticket.
            </p>
          </div>

          <div className="space-y-2">
            <div className="w-8 h-8 rounded-lg bg-zinc-800 text-cyan-400 font-bold text-sm flex items-center justify-center">
              2
            </div>
            <h4 className="font-semibold text-sm text-zinc-200">Enter Code in App</h4>
            <p className="text-xs text-zinc-400">
              Launch PrivateNet and enter your code. The client generates your private key locally and sends only the public key to the portal.
            </p>
          </div>

          <div className="space-y-2">
            <div className="w-8 h-8 rounded-lg bg-zinc-800 text-cyan-400 font-bold text-sm flex items-center justify-center">
              3
            </div>
            <h4 className="font-semibold text-sm text-zinc-200">Connect &amp; Share</h4>
            <p className="text-xs text-zinc-400">
              Click Connect! You are now in the encrypted mesh network. Send files by dragging them onto any online peer.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
