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
                    if (lines.Length >= 2) return (lines[0].Trim(), lines[1].Trim());
                }
                catch { }
            }

            // Generate clamp-compatible random Curve25519 private key
            byte[] keyBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }
            keyBytes[0] &= 248;
            keyBytes[31] &= 127;
            keyBytes[31] |= 64;

            string privateKey = Convert.ToBase64String(keyBytes);
            string publicKey = ComputePublicKey(keyBytes);

            File.WriteAllLines(KeyFile, new[] { privateKey, publicKey });
            return (privateKey, publicKey);
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
                logCallback?.Invoke("WireGuard engine installation required to create virtual network adapter.");
                return false;
            }

            var (privateKey, _) = GetOrCreateKeys();

            // Allowed IPs: Split tunnel (LAN subnet only) vs Full tunnel (0.0.0.0/0)
            string allowedIps = routeAllTraffic ? "0.0.0.0/0, ::/0" : "10.77.0.0/24";
            string dns = routeAllTraffic ? "DNS = 1.1.1.1, 8.8.8.8\n" : "";

            var configContent = new StringBuilder();
            configContent.AppendLine("[Interface]");
            configContent.AppendLine($"PrivateKey = {privateKey}");
            configContent.AppendLine($"Address = {assignedIp}/24");
            if (!string.IsNullOrEmpty(dns)) configContent.Append(dns);
            configContent.AppendLine();
            configContent.AppendLine("[Peer]");
            configContent.AppendLine($"PublicKey = {serverPublicKey}");
            configContent.AppendLine($"Endpoint = {serverEndpoint}");
            configContent.AppendLine($"AllowedIPs = {allowedIps}");
            configContent.AppendLine("PersistentKeepalive = 25");

            Directory.CreateDirectory(ConfigDir);
            await File.WriteAllTextAsync(ConfigPath, configContent.ToString());

            logCallback?.Invoke("Installing WireGuard virtual network adapter service...");

            // First uninstall existing service if present
            await DeactivateTunnelAsync();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = WireGuardExePath,
                    Arguments = $"/installtunnelservice \"{ConfigPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var proc = Process.Start(psi);
                if (proc != null) await proc.WaitForExitAsync();

                logCallback?.Invoke("Virtual router adapter active! IP assigned: " + assignedIp);
                return true;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"Tunnel activation error: {ex.Message}");
                return false;
            }
        }

        public static async Task DeactivateTunnelAsync()
        {
            if (!IsWireGuardInstalled) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = WireGuardExePath,
                    Arguments = $"/uninstalltunnelservice {TunnelName}",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var proc = Process.Start(psi);
                if (proc != null) await proc.WaitForExitAsync();
            }
            catch { }
        }

        /// <summary>
        /// Pure C# Curve25519 public key computation from 32-byte clamped private key.
        /// </summary>
        private static string ComputePublicKey(byte[] privateKey)
        {
            // Curve25519 scalar multiplication of basepoint 9
            byte[] basePoint = new byte[32];
            basePoint[0] = 9;
            byte[] result = new byte[32];
            Curve25519_Mul(result, privateKey, basePoint);
            return Convert.ToBase64String(result);
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
