using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace CustomVPN.Client
{
    public class PeerModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
    }

    public partial class MainWindow : Window
    {
        private ObservableCollection<PeerModel> _peers = new ObservableCollection<PeerModel>();

        public MainWindow()
        {
            InitializeComponent();
            ListPeers.ItemsSource = _peers;

            FileTransferService.FileReceived += (fileName, fullPath, size) =>
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"File received: {fileName}\nSaved to: {fullPath}", "P2P File Transfer", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            };
        }

        private void BtnChangeServer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ServerConfigDialog(VpnService.ServerUrl);
            if (dialog.ShowDialog() == true)
            {
                VpnService.ServerUrl = dialog.ServerAddress;
                TxtServerEndpoint.Text = $"Target: {dialog.ServerAddress}";
            }
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            TxtLoginError.Visibility = Visibility.Collapsed;
            var username = TxtUsername.Text.Trim();
            var password = TxtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                TxtLoginError.Text = "Please enter username and password.";
                TxtLoginError.Visibility = Visibility.Visible;
                return;
            }

            BtnLogin.IsEnabled = false;

            var result = await VpnService.LoginAsync(username, password, (status) =>
            {
                Dispatcher.Invoke(() =>
                {
                    BtnLogin.Content = status;
                });
            });

            BtnLogin.IsEnabled = true;
            BtnLogin.Content = "Establish VPN Session";

            if (result.Success)
            {
                TxtAssignedIp.Text = result.AssignedIp ?? "10.77.0.2";
                PanelLogin.Visibility = Visibility.Collapsed;
                PanelDashboard.Visibility = Visibility.Visible;

                FileTransferService.StartListener();
                _ = RefreshPeersLoop();
            }
            else
            {
                TxtLoginError.Text = result.Message;
                TxtLoginError.Visibility = Visibility.Visible;
            }
        }

        private async void BtnResetDeviceLock_Click(object sender, RoutedEventArgs e)
        {
            var username = TxtUsername.Text.Trim();
            var password = TxtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Enter username and password to authenticate the emergency device reset.", "Reset Lock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show($"Force purge the active device lock for '{username}' and bind this PC?", "Confirm Device Lock Reset", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            var res = await VpnService.ResetSessionAsync(username, password);
            MessageBox.Show(res.Message, "Device Lock Purge", MessageBoxButton.OK, res.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        private async Task RefreshPeersLoop()
        {
            while (PanelDashboard.Visibility == Visibility.Visible)
            {
                try
                {
                    using var client = new HttpClient();
                    var url = $"{VpnService.ServerUrl}/api/client/peers?username={VpnService.CurrentUsername}&deviceId={VpnService.DeviceId}";
                    var res = await client.GetAsync(url);
                    if (res.IsSuccessStatusCode)
                    {
                        var rawJson = await res.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(rawJson);
                        var peersArray = doc.RootElement.GetProperty("peers");

                        _peers.Clear();
                        foreach (var el in peersArray.EnumerateArray())
                        {
                            _peers.Add(new PeerModel
                            {
                                Id = el.GetProperty("id").GetInt32(),
                                Username = el.GetProperty("username").GetString() ?? "",
                                Ip = el.GetProperty("ip").GetString() ?? "",
                                DeviceName = el.GetProperty("deviceName").GetString() ?? "",
                                IsOnline = el.GetProperty("isOnline").GetBoolean()
                            });
                        }
                    }
                }
                catch { }

                await Task.Delay(5000);
            }
        }

        private async void ChkRouteAll_Checked(object sender, RoutedEventArgs e)
        {
            VpnService.RouteAllTraffic = true;
            if (VpnService.IsConnected)
            {
                await WireGuardTunnelManager.ActivateTunnelAsync(
                    VpnService.AssignedIp,
                    VpnService.ServerPublicKey,
                    VpnService.ServerEndpoint,
                    true
                );
                MessageBox.Show("Virtual adapter reconfigured: Default gateway redirected. All internet traffic is now encrypted through the VPN server.", "Full Tunnel Active", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void ChkRouteAll_Unchecked(object sender, RoutedEventArgs e)
        {
            VpnService.RouteAllTraffic = false;
            if (VpnService.IsConnected)
            {
                await WireGuardTunnelManager.ActivateTunnelAsync(
                    VpnService.AssignedIp,
                    VpnService.ServerPublicKey,
                    VpnService.ServerEndpoint,
                    false
                );
                MessageBox.Show("Virtual adapter reconfigured: Split-tunnel LAN mode active (only 10.77.0.0/24 routes through VPN). Native internet restored.", "Split Tunnel Active", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnSendFile_Click(object sender, RoutedEventArgs e)
        {
            var selectedPeer = ListPeers.SelectedItem as PeerModel;
            if (selectedPeer == null)
            {
                MessageBox.Show("Please select a target peer from the list first.", "Send File", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                var filePath = openFileDialog.FileName;
                var success = await FileTransferService.SendFileAsync(selectedPeer.Ip, filePath);

                if (success)
                {
                    MessageBox.Show($"File successfully transferred to {selectedPeer.Username} ({selectedPeer.Ip})!", "Transfer Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Failed to transfer file to {selectedPeer.Ip}.", "Transfer Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            await VpnService.LogoutAsync();
            FileTransferService.StopListener();

            PanelDashboard.Visibility = Visibility.Collapsed;
            PanelLogin.Visibility = Visibility.Visible;
            TxtPassword.Password = string.Empty;
        }
    }
}