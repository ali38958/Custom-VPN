using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CustomVPN.Client
{
    public class WireGuardTunnelManager
    {
        public static string TunnelName = "CustomVPN";
        public static string WireGuardExePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WireGuard", "wireguard.exe");

        public static bool IsWireGuardInstalled => File.Exists(WireGuardExePath);

        private static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CustomVPN");
        private static string ConfigPath => Path.Combine(ConfigDir, $"{TunnelName}.conf");

        public static async Task<bool> EnsureWireGuardInstalledAsync(Action<string>? statusCallback = null)
        {
            if (IsWireGuardInstalled) return true;

            statusCallback?.Invoke("WireGuard engine not found. Downloading official installer...");
            var tempInstaller = Path.Combine(Path.GetTempPath(), "wireguard-installer.exe");

            try
            {
                using var client = new HttpClient();
                var data = await client.GetByteArrayAsync("https://download.wireguard.com/windows-client/wireguard-installer.exe");
                await File.WriteAllBytesAsync(tempInstaller, data);

                statusCallback?.Invoke("Installing WireGuard kernel adapter (requires elevation)...");
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = tempInstaller,
                    Arguments = "/S",
                    UseShellExecute = true,
                    Verb = "runas"
                });

                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                }

                for (int i = 0; i < 15; i++)
                {
                    if (IsWireGuardInstalled) return true;
                    await Task.Delay(1000);
                }

                return IsWireGuardInstalled;
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"Failed to auto-install WireGuard: {ex.Message}");
                return false;
            }
        }

        public static bool CanPingServer(string ip = "10.8.0.1", int timeoutMs = 400)
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send(ip, timeoutMs);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> ActivateTunnelAsync(
            string configText,
            bool routeAllTraffic,
            Action<string>? logCallback = null)
        {
            if (!await EnsureWireGuardInstalledAsync(logCallback))
            {
                logCallback?.Invoke("WireGuard engine installation required.");
                return false;
            }

            logCallback?.Invoke("Applying WireGuard profile...");
            try
            {
                Directory.CreateDirectory(ConfigDir);

                if (routeAllTraffic)
                {
                    configText = configText.Replace("AllowedIPs = 10.8.0.0/24", "AllowedIPs = 0.0.0.0/0");
                }

                await File.WriteAllTextAsync(ConfigPath, configText);
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"Failed to write WireGuard profile: {ex.Message}");
                return false;
            }

            logCallback?.Invoke("Starting WireGuard Wintun adapter...");

            await DeactivateTunnelAsync(); // Ensure it's not already running

            var psi = new ProcessStartInfo
            {
                FileName = WireGuardExePath,
                Arguments = $"/installtunnelservice \"{ConfigPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var p = Process.Start(psi);
            if (p != null) await p.WaitForExitAsync();

            bool ok = false;
            for (int i = 0; i < 20; i++)
            {
                if (CanPingServer("10.8.0.1", 350))
                {
                    ok = true;
                    break;
                }
                await Task.Delay(500);
            }

            if (ok)
            {
                logCallback?.Invoke($"Virtual router adapter active via WireGuard!");
            }
            else
            {
                logCallback?.Invoke("Failed to activate WireGuard tunnel adapter. Network timed out.");
            }
            return ok;
        }

        public static async Task DeactivateTunnelAsync()
        {
            if (!IsWireGuardInstalled) return;

            var psi = new ProcessStartInfo
            {
                FileName = WireGuardExePath,
                Arguments = $"/uninstalltunnelservice {TunnelName}",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                var p = Process.Start(psi);
                if (p != null) await p.WaitForExitAsync();
            }
            catch { }
        }

        public static async Task SetRouteAllTrafficAsync(bool enable, string serverPublicKey, string serverEndpoint)
        {
            if (!IsWireGuardInstalled) return;
            
            // For WireGuard, dynamically changing AllowedIPs requires running `wg set` via CLI 
            // or reinstalling the tunnel service. We'll reinstall the tunnel service for simplicity.
            string configText = "";
            if (File.Exists(ConfigPath))
            {
                configText = await File.ReadAllTextAsync(ConfigPath);
            }
            else
            {
                return;
            }

            if (enable)
            {
                configText = configText.Replace("AllowedIPs = 10.8.0.0/24", "AllowedIPs = 0.0.0.0/0");
            }
            else
            {
                configText = configText.Replace("AllowedIPs = 0.0.0.0/0", "AllowedIPs = 10.8.0.0/24");
            }

            await File.WriteAllTextAsync(ConfigPath, configText);
            
            // Re-install the tunnel
            await DeactivateTunnelAsync();
            
            var psi = new ProcessStartInfo
            {
                FileName = WireGuardExePath,
                Arguments = $"/installtunnelservice \"{ConfigPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            var p = Process.Start(psi);
            if (p != null) await p.WaitForExitAsync();
        }
    }
}
