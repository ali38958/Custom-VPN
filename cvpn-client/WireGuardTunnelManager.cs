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
        public static string TunnelName = "CVPN";
        public static string WireGuardExePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WireGuard", "wireguard.exe");

        // Checks BOTH the exe AND the wireguard-nt driver file are present.
        // wireguard.exe can exist as a leftover from a corrupted/partial uninstall
        // while wireguard.sys is gone — which causes Manager to get stuck in START_PENDING.
        public static bool IsWireGuardInstalled
        {
            get
            {
                if (!File.Exists(WireGuardExePath)) return false;
                var driverPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "drivers", "wireguard.sys");
                var wgDir = Path.GetDirectoryName(WireGuardExePath)!;
                return File.Exists(driverPath) || File.Exists(Path.Combine(wgDir, "wireguard.dll"));
            }
        }

        private static string ConfigDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "CustomVPN");

        public static string ConfigPath => Path.Combine(ConfigDir, $"{TunnelName}.conf");

        // ─── Helpers ────────────────────────────────────────────────────────────

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
                p.WaitForExit(10000);
                return (p.ExitCode, outStr + "\n" + errStr);
            }
            catch (Exception ex)
            {
                return (-1, ex.Message);
            }
        }

        // ─── WireGuard Install ───────────────────────────────────────────────────

        public static async Task<(bool Success, string ErrorMsg)> EnsureWireGuardInstalledAsync(
            Action<string>? statusCallback = null, bool force = false)
        {
            if (!force && IsWireGuardInstalled) return (true, "");

            if (force)
                statusCallback?.Invoke("WireGuard installation appears broken. Reinstalling...");
            else
                statusCallback?.Invoke("WireGuard not found. Downloading installer...");

            var tempInstaller = Path.Combine(Path.GetTempPath(), "wireguard-installer.exe");

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                statusCallback?.Invoke("Downloading WireGuard...");
                var data = await client.GetByteArrayAsync("https://download.wireguard.com/windows-client/wireguard-installer.exe");
                await File.WriteAllBytesAsync(tempInstaller, data);

                statusCallback?.Invoke("Installing WireGuard (this may take a moment)...");
                RunProcess(tempInstaller, "/S");

                for (int i = 0; i < 30; i++)
                {
                    if (IsWireGuardInstalled) return (true, "");
                    await Task.Delay(1000);
                }

                if (File.Exists(WireGuardExePath)) return (true, "");
                return (false, "WireGuard installer ran but wireguard.exe not found after 30 seconds.");
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"Install failed: {ex.Message}");
                return (false, $"Download/Install error: {ex.Message}");
            }
        }

        // ─── Tunnel Status ───────────────────────────────────────────────────────

        public static bool IsTunnelInstalled()
        {
            if (!File.Exists(WireGuardExePath)) return false;
            try
            {
                var (output, _) = RunProcess("sc", $"query \"WireGuardTunnel${TunnelName}\"");
                if (output.Contains("SERVICE_NAME")) return true;
            }
            catch { }
            return IsAdapterPresent();
        }

        public static bool IsTunnelRunning()
        {
            if (!File.Exists(WireGuardExePath)) return false;
            try
            {
                var (output, _) = RunProcess("sc", $"query \"WireGuardTunnel${TunnelName}\"");
                if (output.Contains("RUNNING")) return true;
            }
            catch { }
            return IsAdapterPresent();
        }

        public static bool IsAdapterPresent()
        {
            try
            {
                var (output, _) = RunProcess("netsh", "interface show interface");
                return output.Contains(TunnelName, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

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

        private static string? GetDefaultGateway()
        {
            try
            {
                var (output, _) = RunProcess("route", "print 0.0.0.0");
                var m = Regex.Match(output, @"0\.0\.0\.0\s+0\.0\.0\.0\s+(\d+\.\d+\.\d+\.\d+)");
                if (m.Success) return m.Groups[1].Value;
            }
            catch { }
            return null;
        }

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

        private static async Task<bool> EnsureManagerServiceAsync(Action<string>? log = null)
        {
            var (scOut, _) = RunProcess("sc", "query WireGuardManager");
            if (scOut.Contains("RUNNING")) return true;

            log?.Invoke("Starting WireGuard Manager service...");
            RunWireGuard("/installmanagerservice");

            for (int i = 0; i < 30; i++)
            {
                var (status, _) = RunProcess("sc", "query WireGuardManager");
                if (status.Contains("RUNNING")) return true;
                await Task.Delay(500);
            }

            return false;
        }

        private static async Task<(bool Success, string ErrorMsg)> InstallTunnelServiceAsync(
            Action<string>? log = null)
        {
            log?.Invoke("Starting WireGuard engine...");

            bool managerOk = await EnsureManagerServiceAsync(log);

            if (!managerOk)
            {
                log?.Invoke("WireGuard Manager failed to start. Reinstalling WireGuard...");
                var (reinstallOk, reinstallErr) = await EnsureWireGuardInstalledAsync(log, force: true);
                if (!reinstallOk)
                    return (false, $"WireGuard reinstall failed: {reinstallErr}");

                await Task.Delay(2000);
                managerOk = await EnsureManagerServiceAsync(log);
                if (!managerOk)
                    return (false,
                        "WireGuard Manager could not start even after reinstall.\n" +
                        "Please install WireGuard manually from https://www.wireguard.com/install/ then relaunch this app.");
            }

            await Task.Delay(1500);

            log?.Invoke("Installing VPN tunnel service...");
            var (exitCode, output) = RunWireGuard($"/installtunnelservice \"{ConfigPath}\"");

            if (exitCode == 0)
            {
                for (int i = 0; i < 20; i++)
                {
                    if (IsTunnelRunning()) return (true, "");
                    await Task.Delay(500);
                }

                var (sc2, _) = RunProcess("sc", $"query \"WireGuardTunnel${TunnelName}\"");
                var (wgLog, _) = RunProcess(WireGuardExePath, "/dumplog");
                return (false, $"Tunnel service stuck in START_PENDING.\n{sc2.Trim()}\n\nWG Log:\n{wgLog}");
            }

            for (int i = 0; i < 10; i++)
            {
                if (IsTunnelRunning()) return (true, "");
                await Task.Delay(500);
            }

            var (wgLog2, _) = RunProcess(WireGuardExePath, "/dumplog");
            return (false, $"Tunnel install failed (exit {exitCode}).\n{output.Trim()}\n\nWG Log:\n{wgLog2}");
        }

        public static async Task DeactivateTunnelAsync()
        {
            if (!IsTunnelInstalled()) return;

            RunWireGuard($"/uninstalltunnelservice {TunnelName}");

            for (int i = 0; i < 30; i++)
            {
                if (!IsTunnelInstalled()) return;
                await Task.Delay(500);
            }
        }

        // ─── Public API ──────────────────────────────────────────────────────────

        public static async Task<(bool Success, string ErrorMsg)> ActivateTunnelAsync(
            string configText,
            bool routeAllTraffic,
            Action<string>? logCallback = null)
        {
            var (instOk, instErr) = await EnsureWireGuardInstalledAsync(logCallback);
            if (!instOk)
                return (false, $"WireGuard install failed: {instErr}");

            logCallback?.Invoke("Writing VPN profile...");
            Directory.CreateDirectory(ConfigDir);

            configText = Regex.Replace(configText, @"(Address\s*=\s*10\.77\.0\.\d+)/32", "$1/24");

            if (Regex.IsMatch(configText, @"PersistentKeepalive\s*=\s*\d+"))
                configText = Regex.Replace(configText, @"PersistentKeepalive\s*=\s*\d+", "PersistentKeepalive = 10");
            else
                configText = Regex.Replace(configText, @"(\[Peer\])", "$1\nPersistentKeepalive = 10");

            if (routeAllTraffic)
            {
                configText = Regex.Replace(configText, @"AllowedIPs\s*=\s*[^\r\n]+", "AllowedIPs = 0.0.0.0/0, ::/0");
                if (!Regex.IsMatch(configText, @"DNS\s*="))
                    configText = Regex.Replace(configText, @"(\[Interface\][\s\S]*?Address\s*=\s*[^\r\n]+)", "$1\nDNS = 1.1.1.1, 8.8.8.8");
            }
            else
            {
                configText = Regex.Replace(configText, @"DNS\s*=\s*[^\r\n]+(\r?\n)?", "");
            }

            await File.WriteAllTextAsync(ConfigPath, configText);

            await DeactivateTunnelAsync();

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
            if (!started) return (false, err);

            logCallback?.Invoke("Verifying VPN connectivity...");
            for (int i = 0; i < 10; i++)
            {
                if (CanPingServer()) return (true, "");
                await Task.Delay(500);
            }

            if (IsTunnelRunning())
            {
                logCallback?.Invoke("Tunnel up (ping timed out — may be server firewall).");
                return (true, "");
            }

            return (false, "Tunnel started but connectivity check failed. Check server status.");
        }

        public static async Task<bool> SetRouteAllTrafficAsync(bool enable)
        {
            if (!File.Exists(WireGuardExePath)) return false;
            if (!File.Exists(ConfigPath)) return false;

            string configText = await File.ReadAllTextAsync(ConfigPath);

            var epMatch    = Regex.Match(configText, @"Endpoint\s*=\s*([^\s]+)");
            string? epIp   = epMatch.Success ? ResolveEndpointIp(epMatch.Groups[1].Value) : null;
            string? gateway = GetDefaultGateway();

            configText = Regex.Replace(configText, @"(Address\s*=\s*10\.77\.0\.\d+)/32", "$1/24");

            if (Regex.IsMatch(configText, @"PersistentKeepalive\s*=\s*\d+"))
                configText = Regex.Replace(configText, @"PersistentKeepalive\s*=\s*\d+", "PersistentKeepalive = 10");
            else
                configText = Regex.Replace(configText, @"(\[Peer\])", "$1\nPersistentKeepalive = 10");

            if (enable)
            {
                configText = Regex.Replace(configText, @"AllowedIPs\s*=\s*[^\r\n]+", "AllowedIPs = 0.0.0.0/0, ::/0");
                if (!Regex.IsMatch(configText, @"DNS\s*="))
                    configText = Regex.Replace(configText, @"(\[Interface\][\s\S]*?Address\s*=\s*[^\r\n]+)", "$1\nDNS = 1.1.1.1, 8.8.8.8");
            }
            else
            {
                configText = Regex.Replace(configText, @"AllowedIPs\s*=\s*[^\r\n]+", $"AllowedIPs = {VpnService.Subnet}");
                configText = Regex.Replace(configText, @"DNS\s*=\s*[^\r\n]+(\r?\n)?", "");
                if (epIp != null) RunProcess("route", $"delete {epIp}");
            }

            await File.WriteAllTextAsync(ConfigPath, configText);
            await DeactivateTunnelAsync();

            if (enable && epIp != null && gateway != null)
            {
                RunProcess("route", $"delete {epIp}");
                RunProcess("route", $"add {epIp} MASK 255.255.255.255 {gateway} METRIC 1");
            }

            var (ok, _) = await InstallTunnelServiceAsync();
            return ok;
        }
    }
}
