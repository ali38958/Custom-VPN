using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Media.Animation;

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
        private bool _suppressToggle = false;
        
        private readonly DoubleAnimation _blinkAnimation = new DoubleAnimation
        {
            From = 1.0, To = 0.3, Duration = new Duration(TimeSpan.FromSeconds(0.5)), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever
        };

        public MainWindow()
        {
            InitializeComponent();
            ListPeers.ItemsSource = _peers;

            this.Loaded += (s, e) =>
            {
                if (VpnService.IsConnected)
                {
                    PanelLogin.Visibility = Visibility.Collapsed;
                    PanelDashboard.Visibility = Visibility.Visible;
                    FileTransferService.StartListener();
                    _ = RefreshPeersLoop();
                }
            };

            FileTransferService.FileReceived += (fileName, fullPath, size) =>
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"File received: {fileName}\nSaved to: {fullPath}", "P2P File Transfer", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            };

            VpnService.SessionExpired += () =>
            {
                Dispatcher.Invoke(async () =>
                {
                    await VpnService.LogoutAsync();
                    FileTransferService.StopListener();
                    PanelDashboard.Visibility = Visibility.Collapsed;
                    PanelLogin.Visibility = Visibility.Visible;
                    TxtPassword.Password = string.Empty;
                    TxtLoginError.Text = "Session expired. Please sign in again.";
                    TxtLoginError.Visibility = Visibility.Visible;
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

        private void LoginField_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Return && BtnLogin.IsEnabled)
            {
                BtnLogin_Click(sender, e);
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

        private int _peersAuthFailures = 0;

        private async Task RefreshPeersLoop()
        {
            while (PanelDashboard.Visibility == Visibility.Visible)
            {
                try
                {
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var url = $"{VpnService.ServerUrl}/api/client/peers?username={VpnService.CurrentUsername}&deviceId={VpnService.DeviceId}";
                    var res = await client.GetAsync(url);
                    if (res.IsSuccessStatusCode)
                    {
                        _peersAuthFailures = 0;
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
                    else if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized || res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        _peersAuthFailures++;
                        if (_peersAuthFailures >= 3)
                        {
                            VpnService.TriggerSessionExpired();
                            break;
                        }
                    }
                }
                catch { }

                await Task.Delay(5000);
            }
        }

        private async void ChkRouteAll_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggle) return;
            VpnService.RouteAllTraffic = true;
            if (WireGuardTunnelManager.IsTunnelActive())
            {
                ChkRouteAll.IsEnabled = false;
                TxtStatus.Text = "ROUTING ALL TRAFFIC...";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Yellow;
                TxtStatus.BeginAnimation(UIElement.OpacityProperty, _blinkAnimation);

                bool ok = await WireGuardTunnelManager.SetRouteAllTrafficAsync(true);
                VpnService.IsConnected = ok;

                TxtStatus.BeginAnimation(UIElement.OpacityProperty, null);
                TxtStatus.Opacity = 1.0;
                ChkRouteAll.IsEnabled = true;
                TxtStatus.Text = ok ? "FULL TUNNEL ACTIVE" : "GATEWAY ERROR";
                TxtStatus.Foreground = ok ? System.Windows.Media.Brushes.DodgerBlue : System.Windows.Media.Brushes.Red;
                StatusDot.Background = ok ? System.Windows.Media.Brushes.DodgerBlue : System.Windows.Media.Brushes.Red;

                if (!ok)
                {
                    _suppressToggle = true;
                    ChkRouteAll.IsChecked = false;
                    _suppressToggle = false;
                }
            }
        }

        private async void ChkRouteAll_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggle) return;
            VpnService.RouteAllTraffic = false;
            if (WireGuardTunnelManager.IsTunnelActive())
            {
                ChkRouteAll.IsEnabled = false;
                TxtStatus.Text = "REVERTING TO SPLIT TUNNEL...";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Yellow;
                TxtStatus.BeginAnimation(UIElement.OpacityProperty, _blinkAnimation);

                bool ok = await WireGuardTunnelManager.SetRouteAllTrafficAsync(false);
                VpnService.IsConnected = ok;

                TxtStatus.BeginAnimation(UIElement.OpacityProperty, null);
                TxtStatus.Opacity = 1.0;
                ChkRouteAll.IsEnabled = true;
                TxtStatus.Text = ok ? "CONNECTED" : "GATEWAY ERROR";
                TxtStatus.Foreground = ok ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Red;
                StatusDot.Background = ok ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Red;
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

        private bool _isShuttingDown = false;
        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isShuttingDown)
            {
                base.OnClosing(e);
                return;
            }

            bool tunnelRunning = WireGuardTunnelManager.IsTunnelRunning();

            if (tunnelRunning)
            {
                e.Cancel = true;
                var result = MessageBox.Show(
                    "Closing the app will disconnect the VPN.\nAre you sure you want to exit?",
                    "Exit CustomVPN",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _isShuttingDown = true;
                    this.Hide();
                    await WireGuardTunnelManager.DeactivateTunnelAsync();
                    Application.Current.Shutdown();
                }
            }
            else
            {
                // No active tunnel — exit immediately but still clean up any leftover services.
                _isShuttingDown = true;
                _ = Task.Run(() => WireGuardTunnelManager.DeactivateTunnelAsync());
                base.OnClosing(e);
            }
        }
    }
}