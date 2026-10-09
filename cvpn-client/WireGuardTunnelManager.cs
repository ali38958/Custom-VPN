using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CustomVPN.Client
{
    public class WireGuardTunnelManager
    {
        public static string TunnelName = "CustomVPN";
        public static string WireGuardExePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WireGuard", "wireguard.exe");

        public static bool IsWireGuardInstalled => File.Exists(WireGuardExePath);

        private static string ConfigDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "CustomVPN");

        public static string ConfigPath => Path.Combine(ConfigDir, $"{TunnelName}.conf");

        // ─── Helpers ────────────────────────────────────────────────────────────

        /// <summary>Run a process as the current user (NOT elevated) so it can use redirected I/O.</summary>
        private static (string stdout, int exitCode) RunProcess(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return ("", -1);
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return (output, p.ExitCode);
        }

        private static (int exitCode, string output) RunWireGuard(string args)
        {
            var psi = new ProcessStartInfo(WireGuardExePath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            try
            {
                using var p = Process.Start(psi);
                if (p == null) return (-1, "Process start failed");
                string outStr = p.StandardOutput.ReadToEnd();
                string errStr = p.StandardError.ReadToEnd();
                p.WaitForExit(10000); // wait up to 10s
                return (p.ExitCode, outStr + "\n" + errStr);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RunWireGuard failed: {ex.Message}");
                return (-1, ex.Message);
            }
        }

        // ─── WireGuard Install ───────────────────────────────────────────────────

        public static async Task<(bool Success, string ErrorMsg)> EnsureWireGuardInstalledAsync(Action<string>? statusCallback = null)
        {
            if (IsWireGuardInstalled) return (true, "");

            statusCallback?.Invoke("WireGuard engine not found. Downloading installer...");
            var tempInstaller = Path.Combine(Path.GetTempPath(), "wireguard-installer.exe");

            try
            {
                using var client = new HttpClient();
                var data = await client.GetByteArrayAsync("https://download.wireguard.com/windows-client/wireguard-installer.exe");
                await File.WriteAllBytesAsync(tempInstaller, data);

                statusCallback?.Invoke("Installing WireGuard (elevation required)...");
                var (stdout, exitCode) = RunProcess(tempInstaller, "/S");

                for (int i = 0; i < 15; i++)
                {
                    if (IsWireGuardInstalled) return (true, "");
                    await Task.Delay(1000);
                }
                return (false, $"Installer exited with code {exitCode}. Output: {stdout}");
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"Auto-install failed: {ex.Message}");
                return (false, $"Download/Install error: {ex.Message}");
            }
        }

        // ─── Tunnel Status ───────────────────────────────────────────────────────

        public static bool IsTunnelInstalled()
        {
            if (!IsWireGuardInstalled) return false;
            try
            {
                var (output, _) = RunProcess("sc", $"query \"WireGuardTunnel${TunnelName}\"");
                if (output.Contains("SERVICE_NAME")) return true;
            }
            catch { }
            // Also check if the network adapter exists.
            return IsAdapterPresent();
        }

        public static bool IsTunnelRunning()
        {
            if (!IsWireGuardInstalled) return false;
            try
            {
                var (output, _) = RunProcess("sc", $"query \"WireGuardTunnel${TunnelName}\"");
                if (output.Contains("RUNNING")) return true;
            }
            catch { }
            // Adapter present = tunnel is effectively running.
            return IsAdapterPresent();
        }

        /// <summary>Returns true if the CustomVPN network adapter exists in Windows.</summary>
        public static bool IsAdapterPresent()
        {
            try
            {
                var (output, _) = RunProcess("netsh", "interface show interface");
                return output.Contains(TunnelName, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // Keep old name as alias so existing callers don't break.
        public static bool IsTunnelActive() => IsTunnelRunning();

        public static bool CanPingServer(string ip = "10.77.0.1", int timeoutMs = 1000)
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send(ip, timeoutMs);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            }
            catch { return false; }
        }

        // ─── Routing helpers ─────────────────────────────────────────────────────

        /// <summary>Get the current default gateway IP (pre-VPN, from Wi-Fi / Ethernet).</summary>
        private static string? GetDefaultGateway()
        {
            try
            {
                // Parse "route print 0.0.0.0" which always lists the default route first.
                var (output, _) = RunProcess("route", "print 0.0.0.0");
                var m = Regex.Match(output, @"0\.0\.0\.0\s+0\.0\.0\.0\s+(\d+\.\d+\.\d+\.\d+)");
                if (m.Success) return m.Groups[1].Value;
            }
            catch { }
            return null;
        }

        /// <summary>Resolve a host:port or plain host string to an IP string.</summary>
        private static string? ResolveEndpointIp(string endpointStr)
        {
            try
            {
                var host = endpointStr.Contains("://")
                    ? new Uri(endpointStr).Host
                    : endpointStr.Split(':')[0];
                var addresses = System.Net.Dns.GetHostAddresses(host);
                return addresses.Length > 0 ? addresses[0].ToString() : null;
            }
            catch { return null; }
        }

        // ─── Tunnel Lifecycle ────────────────────────────────────────────────────

        /// <summary>Install and start the WireGuard tunnel service. Waits until running.</summary>
        private static async Task<(bool Success, string ErrorMsg)> InstallTunnelServiceAsync(Action<string>? log = null)
        {
            log?.Invoke("Preparing WireGuard engine...");

            // Kill any WireGuard GUI — it holds a lock on the service manager pipe.
            foreach (var proc in Process.GetProcessesByName("WireGuard"))
            {
                try { proc.Kill(); proc.WaitForExit(2000); } catch { }
            }
            await Task.Delay(500);

            log?.Invoke("Installing WireGuard tunnel service...");

            // App runs as Administrator — inherit the token directly, wait for real exit.
            var (exitCode, output) = RunWireGuard($"/installtunnelservice \"{ConfigPath}\"");

            // Exit code 0 = service registered successfully. The tunnel is live.
            // WireGuard GUI won't show it (it uses its own IPC channel) but the adapter works.
            if (exitCode == 0) return (true, "");

            // Fallback: maybe SCM is just slow. Poll briefly.
            for (int i = 0; i < 10; i++)
            {
                if (IsTunnelRunning()) return (true, "");
                await Task.Delay(500);
            }

            log?.Invoke($"Tunnel service install failed (exit code {exitCode}).");
            return (false, $"Exit code {exitCode}. Output: {output}");
        }

        /// <summary>Stop and uninstall the WireGuard tunnel service. Waits until gone.</summary>
        public static async Task DeactivateTunnelAsync()
        {
            if (!IsTunnelInstalled()) return;

            RunWireGuard($"/uninstalltunnelservice {TunnelName}");

            // Poll until the service is confirmed gone.
            for (int i = 0; i < 30; i++)
            {
                if (!IsTunnelInstalled()) return;
                await Task.Delay(500);
            }
        }

        // ─── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Write config and bring up the WireGuard tunnel.
        /// If routeAllTraffic=true, patches AllowedIPs to 0.0.0.0/0 and adds a
        /// /32 static route for the VPN server so the tunnel packets themselves
        /// don't loop back into the tunnel.
        /// </summary>
        public static async Task<(bool Success, string ErrorMsg)> ActivateTunnelAsync(
            string configText,
            bool routeAllTraffic,
            Action<string>? logCallback = null)
        {
            var (instOk, instErr) = await EnsureWireGuardInstalledAsync(logCallback);
            if (!instOk)
                return (false, $"WireGuard is not installed: {instErr}");

            logCallback?.Invoke("Writing WireGuard profile...");
            Directory.CreateDirectory(ConfigDir);

            // Sanitize configText: enforce /24 subnet mask on-link for Windows
            configText = Regex.Replace(configText, @"(Address\s*=\s*10\.77\.0\.\d+)/32", "$1/24");

            // Enforce PersistentKeepalive = 10 to keep aggressive ISP NAT mappings open
            if (Regex.IsMatch(configText, @"PersistentKeepalive\s*=\s*\d+"))
            {
                configText = Regex.Replace(configText, @"PersistentKeepalive\s*=\s*\d+", "PersistentKeepalive = 10");
            }
            else
            {
                configText = Regex.Replace(configText, @"(\[Peer\])", "$1\nPersistentKeepalive = 10");
            }

            if (routeAllTraffic)
            {
                configText = Regex.Replace(configText, @"AllowedIPs\s*=\s*[^\r\n]+",
                    "AllowedIPs = 0.0.0.0/0, ::/0");

                // Ensure DNS is present for full tunnel
                if (!Regex.IsMatch(configText, @"DNS\s*="))
                {
                    configText = Regex.Replace(configText, @"(\[Interface\][\s\S]*?Address\s*=\s*[^\r\n]+)", "$1\nDNS = 1.1.1.1, 8.8.8.8");
                }
            }
            else
            {
                // In split tunnel mode, strip DNS so Windows NCSI and local physical DNS do not get blackholed
                configText = Regex.Replace(configText, @"DNS\s*=\s*[^\r\n]+(\r?\n)?", "");
            }

            await File.WriteAllTextAsync(ConfigPath, configText);

            // Tear down any existing tunnel first.
            await DeactivateTunnelAsync();

            // If full-tunnel, inject the /32 exception route BEFORE installing the
            // 0.0.0.0/0 tunnel (so we still have a working default route at that point).
            if (routeAllTraffic)
            {
                var epMatch = Regex.Match(configText, @"Endpoint\s*=\s*([^\s]+)");
                if (epMatch.Success)
                {
                    var endpointIp = ResolveEndpointIp(epMatch.Groups[1].Value);
                    var gateway    = GetDefaultGateway();

                    if (endpointIp != null && gateway != null)
                    {
                        logCallback?.Invoke($"Adding exception route: {endpointIp} via {gateway}");
                        RunProcess("route", $"delete {endpointIp}");
                        RunProcess("route", $"add {endpointIp} MASK 255.255.255.255 {gateway} METRIC 1");
                    }
                }
            }

            var (started, err) = await InstallTunnelServiceAsync(logCallback);
            if (!started) return (false, $"Service fail: {err}");

            // Verify connectivity.
            logCallback?.Invoke("Verifying VPN connectivity...");
            for (int i = 0; i < 10; i++)
            {
                if (CanPingServer()) return (true, "");
                await Task.Delay(500);
            }

            // Service is running but ping failed — still usable.
            logCallback?.Invoke("Tunnel up (ping timed out — may be server firewall).");
            bool running = IsTunnelRunning();
            if (running) return (true, "");
            
            var (scOutput, _) = RunProcess("sc", $"query \"WireGuardTunnel${TunnelName}\"");
            var (eventLog, _) = RunProcess("powershell", $"-NoProfile -Command \"Get-WinEvent -LogName System -MaxEvents 5 -ErrorAction SilentlyContinue | Where-Object {{ $_.Message -like '*WireGuard Tunnel: {TunnelName}*' }} | Select-Object -ExpandProperty Message\"");
            return (false, $"Adapter failed to start.\nService status:\n{scOutput.Trim()}\nEvent Log:\n{eventLog.Trim()}");
        }

        /// <summary>
        /// Toggle "route all internet traffic" without tearing down the whole session.
        /// Updates the config file, removes old exception routes, re-installs the service.
        /// Returns true if tunnel is running after the operation.
        /// </summary>
        public static async Task<bool> SetRouteAllTrafficAsync(bool enable)
        {
            if (!IsWireGuardInstalled) return false;
            if (!File.Exists(ConfigPath)) return false;

            string configText = await File.ReadAllTextAsync(ConfigPath);

            // Resolve endpoint before we change anything.
            var epMatch    = Regex.Match(configText, @"Endpoint\s*=\s*([^\s]+)");
            string? epIp   = epMatch.Success ? ResolveEndpointIp(epMatch.Groups[1].Value) : null;
            string? gateway = GetDefaultGateway();

            // Enforce /24 subnet mask on-link for Windows
            configText = Regex.Replace(configText, @"(Address\s*=\s*10\.77\.0\.\d+)/32", "$1/24");

            // Enforce PersistentKeepalive = 10
            if (Regex.IsMatch(configText, @"PersistentKeepalive\s*=\s*\d+"))
            {
                configText = Regex.Replace(configText, @"PersistentKeepalive\s*=\s*\d+", "PersistentKeepalive = 10");
            }
            else
            {
                configText = Regex.Replace(configText, @"(\[Peer\])", "$1\nPersistentKeepalive = 10");
            }

            if (enable)
            {
                // Switch to full-tunnel config.
                configText = Regex.Replace(configText, @"AllowedIPs\s*=\s*[^\r\n]+",
                    "AllowedIPs = 0.0.0.0/0, ::/0");

                // Ensure DNS is added for full tunnel
                if (!Regex.IsMatch(configText, @"DNS\s*="))
                {
                    configText = Regex.Replace(configText, @"(\[Interface\][\s\S]*?Address\s*=\s*[^\r\n]+)", "$1\nDNS = 1.1.1.1, 8.8.8.8");
                }
            }
            else
            {
                // Revert to split-tunnel (VPN subnet only).
                configText = Regex.Replace(configText,
                    @"AllowedIPs\s*=\s*[^\r\n]+",
                    $"AllowedIPs = {VpnService.Subnet}");

                // Remove DNS in split-tunnel mode
                configText = Regex.Replace(configText, @"DNS\s*=\s*[^\r\n]+(\r?\n)?", "");

                // Remove the exception route we added earlier.
                if (epIp != null)
                    RunProcess("route", $"delete {epIp}");
            }

            await File.WriteAllTextAsync(ConfigPath, configText);

            // Tear down.
            await DeactivateTunnelAsync();

            // Add exception route BEFORE starting full-tunnel so we don't lose connectivity.
            if (enable && epIp != null && gateway != null)
            {
                RunProcess("route", $"delete {epIp}");
                RunProcess("route", $"add {epIp} MASK 255.255.255.255 {gateway} METRIC 1");
            }

            // Bring back up.
            var (ok, _) = await InstallTunnelServiceAsync();
            return ok;
        }
    }
}
