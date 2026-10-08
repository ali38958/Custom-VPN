import { Download, Shield, Monitor, CheckCircle, ExternalLink } from "lucide-react";

export default function DownloadsPage() {
  return (
    <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-12 space-y-10">
      <div className="text-center max-w-2xl mx-auto space-y-2">
        <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-[var(--text-primary)]">
          Client Downloads &amp; Setup
        </h1>
        <p className="text-sm text-[var(--text-secondary)]">
          Install the client utility to establish encrypted tunnels, discover mesh peers, and transfer files.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Main Client App Card */}
        <div className="pn-card p-6 sm:p-7 flex flex-col justify-between space-y-6 shadow-sm">
          <div className="space-y-4">
            <div className="w-11 h-11 rounded-xl bg-[var(--accent-soft)] border border-[var(--accent)]/20 flex items-center justify-center text-[var(--accent)]">
              <Monitor className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-[var(--text-primary)]">Custom VPN for Windows</h2>
              <span className="text-xs font-mono text-[var(--accent)]">.NET 10 &bull; Windows 10/11 x64</span>
            </div>
            <p className="text-xs sm:text-sm text-[var(--text-secondary)] leading-relaxed">
              WPF desktop client paired with WireGuard engine. Automatically configures on-link network routing, device lock validation, and split/full-tunneling.
            </p>

            <div className="space-y-2 text-xs text-[var(--text-secondary)] pt-1">
              <div className="flex items-center gap-2">
                <CheckCircle className="w-4 h-4 text-[var(--success)]" />
                <span>Single-device concurrent hardware lock</span>
              </div>
              <div className="flex items-center gap-2">
                <CheckCircle className="w-4 h-4 text-[var(--success)]" />
                <span>P2P direct chunked file streaming</span>
              </div>
              <div className="flex items-center gap-2">
                <CheckCircle className="w-4 h-4 text-[var(--success)]" />
                <span>Full-tunnel internet toggle or split-subnet</span>
              </div>
            </div>
          </div>

          <a
            href="/api/client/latest"
            className="pn-btn pn-btn-primary w-full justify-center py-2.5 text-sm"
          >
            <Download className="w-4 h-4" />
            <span>Download Client (.exe)</span>
          </a>
        </div>

        {/* Prerequisites & WireGuard */}
        <div className="pn-card p-6 sm:p-7 flex flex-col justify-between space-y-6 shadow-sm">
          <div className="space-y-4">
            <div className="w-11 h-11 rounded-xl bg-[var(--success-soft)] border border-[var(--success-border)] flex items-center justify-center text-[var(--success-text)]">
              <Shield className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-[var(--text-primary)]">WireGuard Engine</h2>
              <span className="text-xs font-mono text-[var(--success-text)]">Official Driver</span>
            </div>
            <p className="text-xs sm:text-sm text-[var(--text-secondary)] leading-relaxed">
              Custom VPN leverages the official WireGuard for Windows service and WinTun driver for ultra-low latency and maximum network throughput.
            </p>

            <div className="p-3.5 rounded-lg bg-[var(--sidebar-active)] border border-[var(--border)] text-xs text-[var(--text-secondary)] space-y-1">
              <div className="font-semibold text-[var(--text-primary)]">Automatic Auto-Install:</div>
              <p>
                The client auto-downloads and installs the WireGuard engine seamlessly if it is not already present on your PC.
              </p>
            </div>
          </div>

          <a
            href="https://www.wireguard.com/install/"
            target="_blank"
            rel="noreferrer"
            className="pn-btn pn-btn-secondary w-full justify-center py-2.5 text-sm"
          >
            <span>Official WireGuard Site</span>
            <ExternalLink className="w-4 h-4" />
          </a>
        </div>
      </div>

      {/* 3 Steps Guide */}
      <div className="pn-card p-6 sm:p-7 space-y-5 shadow-sm">
        <h3 className="text-base font-bold text-[var(--text-primary)]">Setup Walkthrough</h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="space-y-1.5">
            <div className="w-7 h-7 rounded-md bg-[var(--accent-soft)] text-[var(--accent)] font-bold text-xs flex items-center justify-center font-mono">
              1
            </div>
            <h4 className="font-semibold text-xs sm:text-sm text-[var(--text-primary)]">Sign In as Admin</h4>
            <p className="text-xs text-[var(--text-secondary)] leading-relaxed">
              Log into the Admin Console to register user accounts and assign dedicated virtual IPs in the 10.77.0.0/24 subnet.
            </p>
          </div>

          <div className="space-y-1.5">
            <div className="w-7 h-7 rounded-md bg-[var(--accent-soft)] text-[var(--accent)] font-bold text-xs flex items-center justify-center font-mono">
              2
            </div>
            <h4 className="font-semibold text-xs sm:text-sm text-[var(--text-primary)]">Launch CustomVPN App</h4>
            <p className="text-xs text-[var(--text-secondary)] leading-relaxed">
              Run `CustomVPN.exe` as Administrator, enter user credentials, and click Establish VPN Session.
            </p>
          </div>

          <div className="space-y-1.5">
            <div className="w-7 h-7 rounded-md bg-[var(--accent-soft)] text-[var(--accent)] font-bold text-xs flex items-center justify-center font-mono">
              3
            </div>
            <h4 className="font-semibold text-xs sm:text-sm text-[var(--text-primary)]">Connect &amp; Share</h4>
            <p className="text-xs text-[var(--text-secondary)] leading-relaxed">
              Tunnel is active! Ping 10.77.0.1, select online peers from the list, and transfer files directly over P2P.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
