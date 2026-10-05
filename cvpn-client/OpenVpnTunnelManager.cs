using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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
        private static string KeyFile => Path.Combine(ConfigDir, "client_keys.json");

        public static async Task<bool> EnsureWireGuardInstalledAsync(Action<string>? statusCallback = null)
        {
            if (IsWireGuardInstalled) return true;

            statusCallback?.Invoke("WireGuard engine not found. Downloading official runtime...");
            var tempInstaller = Path.Combine(Path.GetTempPath(), "wireguard-installer.exe");

            try
            {
                using var client = new HttpClient();
                var data = await client.GetByteArrayAsync("https://download.wireguard.com/windows-client/wireguard-installer.exe");
                await File.WriteAllBytesAsync(tempInstaller, data);

                statusCallback?.Invoke("Installing WireGuard network adapter runtime...");
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = tempInstaller,
                    Arguments = "/quiet",
                    UseShellExecute = true,
                    Verb = "runas"
                });

                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                }

                // Wait up to 10 seconds for installation to finish
                for (int i = 0; i < 10; i++)
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

        public static (string PrivateKey, string PublicKey) GetOrCreateKeys()
        {
            Directory.CreateDirectory(ConfigDir);

            if (File.Exists(KeyFile))
            {
                try
                {
                    var lines = File.ReadAllLines(KeyFile);
                    if (lines.Length >= 2 && !string.IsNullOrWhiteSpace(lines[0]) && !string.IsNullOrWhiteSpace(lines[1]))
                    {
                        string priv = lines[0].Trim();
                        string expectedPub = ComputePublicKey(priv);
                        if (!string.IsNullOrEmpty(expectedPub))
                        {
                            if (lines[1].Trim() != expectedPub)
                            {
                                File.WriteAllLines(KeyFile, new[] { priv, expectedPub });
                            }
                            return (priv, expectedPub);
                        }
                        return (priv, lines[1].Trim());
                    }
                }
                catch { }
            }

            string privateKey = "";
            string publicKey = "";

            try
            {
                var wgExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WireGuard", "wg.exe");
                if (File.Exists(wgExe))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = wgExe,
                        Arguments = "genkey",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        privateKey = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit();
                        publicKey = ComputePublicKey(privateKey);
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(privateKey) || string.IsNullOrEmpty(publicKey))
            {
                byte[] keyBytes = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(keyBytes);
                }
                keyBytes[0] &= 248;
                keyBytes[31] &= 127;
                keyBytes[31] |= 64;
                privateKey = Convert.ToBase64String(keyBytes);
                publicKey = ComputePublicKey(privateKey);
            }

            Directory.CreateDirectory(ConfigDir);
            File.WriteAllLines(KeyFile, new[] { privateKey, publicKey });
            return (privateKey, publicKey);
        }

        public static bool CanPingServer(string ip = "10.77.0.1", int timeoutMs = 400)
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


        public static bool IsTunnelActive()
        {
            if (CanPingServer("10.77.0.1", 350)) return true;
            if (IsTunnelServiceRunning()) return true;
            return false;
        }

        public static string GetActiveTunnelInterfaceName()
        {
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        if (ni.Name.StartsWith(TunnelName, StringComparison.OrdinalIgnoreCase) ||
                            ni.Description.Contains("WireGuard", StringComparison.OrdinalIgnoreCase))
                        {
                            return ni.Name;
                        }
                    }
                }
            }
            catch { }
            return TunnelName;
        }

        public static int GetActiveTunnelInterfaceIndex()
        {
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.Name.StartsWith(TunnelName, StringComparison.OrdinalIgnoreCase) ||
                        ni.Description.Contains("WireGuard", StringComparison.OrdinalIgnoreCase))
                    {
                        var ipProps = ni.GetIPProperties();
                        var ipv4 = ipProps.GetIPv4Properties();
                        if (ipv4 != null) return ipv4.Index;
                    }
                }
            }
            catch { }
            return -1;
        }

        private static void SetSecureFileAcl(string filePath)
        {
            try
            {
                var fi = new FileInfo(filePath);
                var acl = fi.GetAccessControl();
                var adminSid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null);
                var systemSid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.LocalSystemSid, null);

                acl.SetOwner(adminSid);
                acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

                var existingRules = acl.GetAccessRules(true, false, typeof(System.Security.Principal.SecurityIdentifier));
                foreach (System.Security.AccessControl.FileSystemAccessRule rule in existingRules)
                {
                    acl.RemoveAccessRule(rule);
                }

                acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(systemSid, System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
                acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(adminSid, System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
                acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinUsersSid, null), System.Security.AccessControl.FileSystemRights.ReadAndExecute, System.Security.AccessControl.AccessControlType.Allow));

                fi.SetAccessControl(acl);
            }
            catch { }
        }

        public static async Task EnsureFirewallRulesAsync()
        {
            try
            {
                await ExecuteCommandAsync("netsh", "advfirewall firewall add rule name=\"CustomVPN-Allow-ICMP\" protocol=icmpv4:8,any dir=in action=allow");
                await ExecuteCommandAsync("netsh", $"advfirewall firewall add rule name=\"CustomVPN-Allow-P2P\" protocol=TCP localport={FileTransferService.TransferPort} dir=in action=allow");
            }
            catch { }
        }

        public static async Task<bool> ActivateTunnelAsync(
            string assignedIp,
            string serverPublicKey,
            string serverEndpoint,
            bool routeAllTraffic,
            Action<string>? logCallback = null)
        {
            if (!await EnsureWireGuardInstalledAsync(logCallback))
            {
                logCallback?.Invoke("WireGuard engine installation required.");
                return false;
            }

            // Check if WireGuard is installed

            var (privateKey, _) = GetOrCreateKeys();

            var configContent = new StringBuilder();
            configContent.AppendLine("[Interface]");
            configContent.AppendLine($"PrivateKey = {privateKey}");
            configContent.AppendLine($"Address = {assignedIp}/32");
            configContent.AppendLine("MTU = 1360");
            if (routeAllTraffic)
            {
                configContent.AppendLine("DNS = 1.1.1.1, 8.8.8.8");
            }
            configContent.AppendLine();
            configContent.AppendLine("[Peer]");
            configContent.AppendLine($"PublicKey = {serverPublicKey}");
            configContent.AppendLine($"Endpoint = {serverEndpoint}");
            configContent.AppendLine($"AllowedIPs = {(routeAllTraffic ? "0.0.0.0/0" : "10.77.0.0/24")}");
            configContent.AppendLine("PersistentKeepalive = 25");

            string newConfig = configContent.ToString();
            Directory.CreateDirectory(ConfigDir);
            
            bool configChanged = true;
            if (File.Exists(ConfigPath))
            {
                string oldConfig = await File.ReadAllTextAsync(ConfigPath);
                if (oldConfig.Trim() == newConfig.Trim())
                {
                    configChanged = false;
                }
            }

            await File.WriteAllTextAsync(ConfigPath, newConfig);
            SetSecureFileAcl(ConfigPath);

            // If config didn't change and tunnel is active, reuse it
            if (!configChanged && IsTunnelActive())
            {
                logCallback?.Invoke($"WireGuard tunnel already active! IP: {assignedIp}");
                _ = EnsureFirewallRulesAsync();
                return true;
            }

            // Otherwise, we need to restart/start the service
            if (IsTunnelServiceRunning())
            {
                logCallback?.Invoke("Restarting WireGuard adapter with new configuration...");
                await ExecuteCommandAsync("sc.exe", $"stop \"WireGuardTunnel${TunnelName}\"");
                await Task.Delay(1500); // Give it time to stop
            }

            // Auto-sync into official WireGuard directory
            try
            {
                var wgConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WireGuard", "Data", "Configurations");
                if (Directory.Exists(wgConfigDir))
                {
                    File.Copy(ConfigPath, Path.Combine(wgConfigDir, $"{TunnelName}.conf"), true);
                }
            }
            catch { }

            if (IsTunnelServiceInstalled())
            {
                logCallback?.Invoke("Starting existing WireGuard virtual adapter service...");
                await ExecuteCommandAsync("sc.exe", $"start \"WireGuardTunnel${TunnelName}\"");
            }
            else
            {
                logCallback?.Invoke("Installing and starting WireGuard virtual adapter service...");
                var psiInstall = new ProcessStartInfo
                {
                    FileName = WireGuardExePath,
                    Arguments = $"/installtunnelservice \"{ConfigPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psiInstall))
                {
                    if (proc != null) await proc.WaitForExitAsync();
                }
            }

            bool ok = false;
            for (int i = 0; i < 15; i++)
            {
                if (GetActiveTunnelInterfaceIndex() > 0)
                {
                    ok = true;
                    break;
                }
                await Task.Delay(300);
            }

            if (ok)
            {
                _ = EnsureFirewallRulesAsync();
                logCallback?.Invoke($"Virtual router adapter active! IP assigned: {assignedIp}");
            }
            else
            {
                logCallback?.Invoke("Failed to activate WireGuard tunnel adapter.");
            }
            return ok;
        }

        public static string? GetPhysicalDefaultGateway()
        {
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                        !ni.Name.StartsWith(TunnelName, StringComparison.OrdinalIgnoreCase) &&
                        !ni.Description.Contains("WireGuard", StringComparison.OrdinalIgnoreCase))
                    {
                        var props = ni.GetIPProperties();
                        foreach (var gw in props.GatewayAddresses)
                        {
                            if (gw.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                                !gw.Address.ToString().StartsWith("0.0.0.0"))
                            {
                                return gw.Address.ToString();
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static string ResolveServerIp(string endpoint)
        {
            string host = endpoint.Split(':')[0].Trim();
            try
            {
                var addrs = System.Net.Dns.GetHostAddresses(host);
                foreach (var a in addrs)
                {
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return a.ToString();
                    }
                }
            }
            catch { }
            return host;
        }

        public static Task<bool> SetRouteAllTrafficAsync(
            bool routeAll,
            string serverPublicKey,
            string serverEndpoint,
            Action<string>? logCallback = null)
        {
            return Task.FromResult(true);
        }

        private static async Task<int> ExecuteCommandAsync(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                    return proc.ExitCode;
                }
            }
            catch { }
            return -1;
        }

        public static bool IsTunnelServiceInstalled()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"query \"WireGuardTunnel${TunnelName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    return !output.Contains("1060");
                }
            }
            catch { }
            return false;
        }

        public static bool IsTunnelServiceRunning()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"query \"WireGuardTunnel${TunnelName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    return output.Contains("RUNNING");
                }
            }
            catch { }
            return false;
        }

        public static async Task DeactivateTunnelAsync()
        {
            if (IsTunnelServiceRunning())
            {
                await ExecuteCommandAsync("sc.exe", $"stop \"WireGuardTunnel${TunnelName}\"");
            }
        }

        private static string ComputePublicKey(string privateKey)
        {
            try
            {
                var wgExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WireGuard", "wg.exe");
                if (File.Exists(wgExe))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = wgExe,
                        Arguments = "pubkey",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        proc.StandardInput.WriteLine(privateKey);
                        proc.StandardInput.Close();
                        string pubKey = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit();
                        if (!string.IsNullOrEmpty(pubKey)) return pubKey;
                    }
                }
            }
            catch { }
            return "";
        }

        #region Curve25519 Math Implementation
        private static void Curve25519_Mul(byte[] result, byte[] scalar, byte[] point)
        {
            long[] x1 = new long[16];
            long[] x2 = new long[16];
            long[] z2 = new long[16];
            long[] x3 = new long[16];
            long[] z3 = new long[16];

            Unpack(x1, point);
            x2[0] = 1;
            Array.Copy(x1, x3, 16);
            z3[0] = 1;

            int swap = 0;
            for (int t = 254; t >= 0; --t)
            {
                int r = (scalar[t / 8] >> (t & 7)) & 1;
                swap ^= r;
                CSwap(x2, x3, swap);
                CSwap(z2, z3, swap);
                swap = r;

                long[] a = new long[16], b = new long[16], c = new long[16], d = new long[16], e = new long[16], f = new long[16];
                Add(a, x2, z2);
                Sub(b, x2, z2);
                Add(c, x3, z3);
                Sub(d, x3, z3);
                Mul(e, a, a);
                Mul(f, b, b);
                Mul(a, d, a);
                Mul(b, c, b);
                Add(c, a, b);
                Sub(d, a, b);
                Mul(x3, c, c);
                Mul(z3, d, d);
                Mul(z3, z3, x1);
                Mul(x2, e, f);
                Sub(f, e, f);
                Mul121666(a, f);
                Add(a, a, e);
                Mul(z2, f, a);
            }
            CSwap(x2, x3, swap);
            CSwap(z2, z3, swap);

            long[] inv = new long[16];
            Invert(inv, z2);
            Mul(x2, x2, inv);
            Pack(result, x2);
        }

        private static void CSwap(long[] a, long[] b, int swap)
        {
            long mask = -(long)swap;
            for (int i = 0; i < 16; ++i)
            {
                long t = mask & (a[i] ^ b[i]);
                a[i] ^= t;
                b[i] ^= t;
            }
        }

        private static void Unpack(long[] o, byte[] n)
        {
            for (int i = 0; i < 16; ++i)
            {
                o[i] = (n[2 * i] & 0xFF) | ((long)(n[2 * i + 1] & 0xFF) << 8);
            }
            o[15] &= 0x7fff;
        }

        private static void Pack(byte[] o, long[] n)
        {
            long[] m = new long[16];
            Array.Copy(n, m, 16);
            Carry(m);
            Carry(m);
            for (int i = 0; i < 16; ++i)
            {
                o[2 * i] = (byte)(m[i] & 0xFF);
                o[2 * i + 1] = (byte)((m[i] >> 8) & 0xFF);
            }
        }

        private static void Carry(long[] o)
        {
            for (int i = 0; i < 16; ++i)
            {
                long carry = o[i] >> 16;
                o[i] &= 0xFFFF;
                if (i < 15) o[i + 1] += carry;
                else o[0] += 38 * carry;
            }
        }

        private static void Add(long[] o, long[] a, long[] b)
        {
            for (int i = 0; i < 16; ++i) o[i] = a[i] + b[i];
        }

        private static void Sub(long[] o, long[] a, long[] b)
        {
            for (int i = 0; i < 16; ++i) o[i] = a[i] - b[i];
        }

        private static void Mul(long[] o, long[] a, long[] b)
        {
            long[] product = new long[31];
            for (int i = 0; i < 16; ++i)
            {
                for (int j = 0; j < 16; ++j)
                {
                    product[i + j] += a[i] * b[j];
                }
            }
            for (int i = 0; i < 15; ++i)
            {
                product[i] += 38 * product[i + 16];
            }
            Array.Copy(product, o, 16);
            Carry(o);
            Carry(o);
        }

        private static void Mul121666(long[] o, long[] a)
        {
            for (int i = 0; i < 16; ++i) o[i] = a[i] * 121666;
            Carry(o);
            Carry(o);
        }

        private static void Invert(long[] o, long[] z)
        {
            long[] t = new long[16];
            Array.Copy(z, t, 16);
            for (int i = 253; i >= 0; --i)
            {
                Mul(t, t, t);
                if (i != 2 && i != 4) Mul(t, t, z);
            }
            Array.Copy(t, o, 16);
        }
        #endregion
    }
}
