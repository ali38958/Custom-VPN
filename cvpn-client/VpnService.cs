using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CustomVPN.Client
{
    public class VpnService
    {
        public static string ServerUrl { get; set; } = "https://resolvia.cc.cd";
        public static string CurrentUsername { get; set; } = string.Empty;
        public static string DeviceId { get; private set; } = string.Empty;
        public static string DeviceName { get; set; } = Environment.MachineName;
        public static string AssignedIp { get; set; } = string.Empty;
        public static string ServerPublicKey { get; set; } = string.Empty;
        public static string ServerEndpoint { get; set; } = "144.24.25.135:51820";
        public static string Subnet { get; set; } = "10.77.0.0/24";
        public static bool IsConnected { get; set; } = false;
        public static bool RouteAllTraffic { get; set; } = false;

        private static readonly HttpClient _httpClient = new HttpClient();

        static VpnService()
        {
            LoadOrCreateDeviceId();
        }

        private static void LoadOrCreateDeviceId()
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CustomVPN");
            Directory.CreateDirectory(appData);
            var idFile = Path.Combine(appData, "device.id");

            if (File.Exists(idFile))
            {
                DeviceId = File.ReadAllText(idFile).Trim();
            }
            else
            {
                DeviceId = Guid.NewGuid().ToString("N");
                File.WriteAllText(idFile, DeviceId);
            }
        }

        public static async Task<(bool Success, string Message, string? AssignedIp)> LoginAsync(
            string username, 
            string password, 
            Action<string>? statusCallback = null)
        {
            try
            {
                var (_, publicKey) = WireGuardTunnelManager.GetOrCreateKeys();

                var payload = new
                {
                    username,
                    password,
                    deviceId = DeviceId,
                    deviceName = DeviceName,
                    deviceInfo = $"{Environment.OSVersion}; {Environment.MachineName}",
                    publicKey
                };

                statusCallback?.Invoke("Authenticating with Custom VPN server...");
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync($"{ServerUrl}/api/client/login", content);
                var rawJson = await res.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                if (res.IsSuccessStatusCode)
                {
                    CurrentUsername = username;
                    var ip = root.GetProperty("user").GetProperty("assignedIp").GetString();
                    AssignedIp = ip ?? "10.77.0.2";

                    if (root.TryGetProperty("serverConfig", out var cfg))
                    {
                        if (cfg.TryGetProperty("serverPublicKey", out var spk)) ServerPublicKey = spk.GetString() ?? "";
                        if (cfg.TryGetProperty("endpoint", out var ep)) ServerEndpoint = ep.GetString() ?? ServerEndpoint;
                        if (cfg.TryGetProperty("subnet", out var sn)) Subnet = sn.GetString() ?? Subnet;
                    }

                    statusCallback?.Invoke("Activating virtual router network adapter...");

                    // Activate the actual WireGuard kernel tunnel adapter on Windows
                    bool tunnelOk = await WireGuardTunnelManager.ActivateTunnelAsync(
                        AssignedIp,
                        ServerPublicKey,
                        ServerEndpoint,
                        RouteAllTraffic,
                        statusCallback
                    );

                    IsConnected = true;
                    return (true, tunnelOk ? "Connected to virtual VPN router!" : "Authenticated, but WireGuard adapter creation failed.", AssignedIp);
                }
                else
                {
                    var errorMsg = root.TryGetProperty("error", out var err) ? err.GetString() : "Authentication failed";
                    return (false, errorMsg ?? "Authentication failed", null);
                }
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}", null);
            }
        }

        public static async Task<bool> LogoutAsync()
        {
            try
            {
                // Revert full tunnel routes if active
                await WireGuardTunnelManager.SetRouteAllTrafficAsync(false, ServerPublicKey, ServerEndpoint);
                await WireGuardTunnelManager.DeactivateTunnelAsync();
                IsConnected = false;

                var payload = new
                {
                    username = CurrentUsername,
                    deviceId = DeviceId
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync($"{ServerUrl}/api/client/logout", content);
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<(bool Success, string Message)> ResetSessionAsync(string username, string password)
        {
            try
            {
                var payload = new
                {
                    username,
                    password,
                    newDeviceId = DeviceId,
                    deviceName = DeviceName
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync($"{ServerUrl}/api/client/reset-session", content);
                var rawJson = await res.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                if (res.IsSuccessStatusCode)
                {
                    return (true, "Device lock released! You can now log in.");
                }
                else
                {
                    var errorMsg = root.TryGetProperty("error", out var err) ? err.GetString() : "Failed to reset session";
                    return (false, errorMsg ?? "Failed to reset session");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Reset error: {ex.Message}");
            }
        }
    }
}
