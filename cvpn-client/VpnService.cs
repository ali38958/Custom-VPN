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
        private static string SessionFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CustomVPN", "session.json");
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
        public static string OpenVpnConfigText { get; set; } = string.Empty;
        
        public static event Action? SessionExpired;

        public static void TriggerSessionExpired()
        {
            SessionExpired?.Invoke();
        }

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


                var payload = new
                {
                    username,
                    password,
                    deviceId = DeviceId,
                    deviceName = DeviceName,
                    deviceInfo = $"{Environment.OSVersion}; {Environment.MachineName}"
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
                    string configText = root.TryGetProperty("wireguardConfigText", out var ctxt) ? (ctxt.GetString() ?? "") : "";
                    OpenVpnConfigText = configText; // We keep the variable name for compatibility or rename it later
                    
                    statusCallback?.Invoke("Activating virtual router network adapter...");

                    // Activate the actual WireGuard tunnel adapter on Windows
                    bool tunnelOk = await WireGuardTunnelManager.ActivateTunnelAsync(
                        configText,
                        RouteAllTraffic,
                        statusCallback
                    );

                    if (tunnelOk)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(SessionFilePath)!);
                        var sessionData = new
                        {
                            Username = CurrentUsername,
                            Ip = AssignedIp,
                            ConfigText = OpenVpnConfigText,
                            RouteAll = RouteAllTraffic,
                            ServerPubKey = ServerPublicKey,
                            ServerEp = ServerEndpoint,
                            Sub = Subnet,
                            Url = ServerUrl
                        };
                        await File.WriteAllTextAsync(SessionFilePath, JsonSerializer.Serialize(sessionData));
                    }

                    IsConnected = tunnelOk;
                    return (tunnelOk, tunnelOk ? "Connected to WireGuard virtual router!" : "Authenticated, but WireGuard adapter creation failed.", AssignedIp);
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
                await WireGuardTunnelManager.SetRouteAllTrafficAsync(false);
                await WireGuardTunnelManager.DeactivateTunnelAsync();
                IsConnected = false;
                if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath);

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

        public static async Task<bool> TryRestoreSessionAsync(Action<string>? statusCallback = null)
        {
            if (!File.Exists(SessionFilePath)) return false;

            try
            {
                var root = JsonDocument.Parse(await File.ReadAllTextAsync(SessionFilePath)).RootElement;
                CurrentUsername = root.GetProperty("Username").GetString();
                AssignedIp = root.GetProperty("Ip").GetString();
                OpenVpnConfigText = root.GetProperty("ConfigText").GetString();
                RouteAllTraffic = root.GetProperty("RouteAll").GetBoolean();
                ServerPublicKey = root.GetProperty("ServerPubKey").GetString() ?? "";
                ServerEndpoint = root.GetProperty("ServerEp").GetString() ?? "";
                Subnet = root.GetProperty("Sub").GetString() ?? "";
                ServerUrl = root.GetProperty("Url").GetString() ?? "https://resolvia.cc.cd";

                statusCallback?.Invoke("Restoring VPN session...");
                bool tunnelOk = await WireGuardTunnelManager.ActivateTunnelAsync(
                    OpenVpnConfigText,
                    RouteAllTraffic,
                    statusCallback
                );

                IsConnected = tunnelOk;
                return tunnelOk;
            }
            catch
            {
                if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath);
                return false;
            }
        }
    }
}
