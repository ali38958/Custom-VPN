using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CustomVPN.Client
{
    public class OpenVpnTunnelManager
    {
        public static string TunnelName = "CustomVPN";
        public static string OpenVpnExePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenVPN", "bin", "openvpn.exe");

        public static bool IsOpenVpnInstalled => File.Exists(OpenVpnExePath);

        private static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CustomVPN");
        private static string ConfigPath => Path.Combine(ConfigDir, $"{TunnelName}.ovpn");

        private static Process? _openVpnProcess;

        public static async Task<bool> EnsureOpenVpnInstalledAsync(Action<string>? statusCallback = null)
        {
            if (IsOpenVpnInstalled) return true;

            statusCallback?.Invoke("OpenVPN engine not found. Downloading official MSI installer...");
            var tempInstaller = Path.Combine(Path.GetTempPath(), "openvpn-installer.msi");

            try
            {
                using var client = new HttpClient();
                // Download a known OpenVPN installer
                var data = await client.GetByteArrayAsync("https://swupdate.openvpn.org/community/releases/OpenVPN-2.6.8-I001-amd64.msi");
                await File.WriteAllBytesAsync(tempInstaller, data);

                statusCallback?.Invoke("Installing OpenVPN network adapter runtime (requires elevation)...");
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{tempInstaller}\" /quiet /norestart",
                    UseShellExecute = true,
                    Verb = "runas"
                });

                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                }

                // Wait up to 15 seconds for installation to finish
                for (int i = 0; i < 15; i++)
                {
                    if (IsOpenVpnInstalled) return true;
                    await Task.Delay(1000);
                }

                return IsOpenVpnInstalled;
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"Failed to auto-install OpenVPN: {ex.Message}");
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
            if (!await EnsureOpenVpnInstalledAsync(logCallback))
            {
                logCallback?.Invoke("OpenVPN engine installation required.");
                return false;
            }

            logCallback?.Invoke("Applying OpenVPN profile...");
            try
            {
                string ovpnConfig = configText;

                Directory.CreateDirectory(ConfigDir);

                if (routeAllTraffic)
                {
                    ovpnConfig += "\nredirect-gateway def1 bypass-dhcp\n";
                    ovpnConfig += "dhcp-option DNS 1.1.1.1\n";
                    ovpnConfig += "dhcp-option DNS 8.8.8.8\n";
                }

                await File.WriteAllTextAsync(ConfigPath, ovpnConfig);
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"Failed to download or parse profile: {ex.Message}");
                return false;
            }

            logCallback?.Invoke("Starting OpenVPN adapter...");

            if (_openVpnProcess != null && !_openVpnProcess.HasExited)
            {
                try { _openVpnProcess.Kill(); } catch { }
            }

            var psi = new ProcessStartInfo
            {
                FileName = OpenVpnExePath,
                Arguments = $"--config \"{ConfigPath}\"",
                UseShellExecute = true, // We might need UseShellExecute=true + Verb="runas" for TAP adapters, but app is already running as Admin
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            _openVpnProcess = Process.Start(psi);

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
                logCallback?.Invoke($"Virtual router adapter active via OpenVPN!");
            }
            else
            {
                logCallback?.Invoke("Failed to activate OpenVPN tunnel adapter. Network timed out.");
            }
            return ok;
        }

        public static async Task DeactivateTunnelAsync()
        {
            if (_openVpnProcess != null && !_openVpnProcess.HasExited)
            {
                try { _openVpnProcess.Kill(); } catch { }
                _openVpnProcess = null;
            }

            // Also kill any lingering openvpn instances
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/F /IM openvpn.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null) await proc.WaitForExitAsync();
            }
            catch { }
        }

        public static Task<bool> SetRouteAllTrafficAsync(
            bool routeAll,
            string serverPublicKey,
            string serverEndpoint,
            Action<string>? logCallback = null)
        {
            return Task.FromResult(true);
        }
    }
}
