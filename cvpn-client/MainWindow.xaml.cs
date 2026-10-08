using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Controls;

namespace CustomVPN.Client
{
    public class PeerModel : System.ComponentModel.INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        private bool _isOnline;
        public bool IsOnline 
        { 
            get => _isOnline;
            set 
            { 
                _isOnline = value; 
                OnPropertyChanged(nameof(IsOnline));
                OnPropertyChanged(nameof(StatusBg));
                OnPropertyChanged(nameof(StatusFg));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(LatencyVisibility));
                OnPropertyChanged(nameof(LastSeenVisibility));
            }
        }

        public Visibility LatencyVisibility => IsOnline ? Visibility.Visible : Visibility.Collapsed;
        public Visibility LastSeenVisibility => IsOnline ? Visibility.Collapsed : Visibility.Visible;
        
        // Dark Green vs Dark Gray
        public Brush StatusBg => IsOnline ? new SolidColorBrush(Color.FromRgb(6, 78, 59)) : new SolidColorBrush(Color.FromRgb(39, 39, 42));
        // Light Green vs Light Gray
        public Brush StatusFg => IsOnline ? new SolidColorBrush(Color.FromRgb(167, 243, 208)) : new SolidColorBrush(Color.FromRgb(161, 161, 170));
        public string StatusText => IsOnline ? "ONLINE" : "OFFLINE";

        private string _lastSeenString = string.Empty;
        public string LastSeenString 
        { 
            get => _lastSeenString; 
            set { _lastSeenString = value; OnPropertyChanged(nameof(LastSeenString)); }
        }

        private string _latencyString = string.Empty;
        public string LatencyString 
        { 
            get => _latencyString; 
            set { _latencyString = value; OnPropertyChanged(nameof(LatencyString)); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainWindow : Window
    {
        private ObservableCollection<PeerModel> _peers = new ObservableCollection<PeerModel>();
        private bool _suppressToggle = false;
        
        private readonly DoubleAnimation _blinkAnimation = new DoubleAnimation
        {
            From = 1.0, To = 0.3, Duration = new Duration(TimeSpan.FromSeconds(0.5)), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever
        };

        private string PrefsFilePath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CustomVPN", "prefs.json");

        private void LoadPreferences()
        {
            try
            {
                if (System.IO.File.Exists(PrefsFilePath))
                {
                    var json = System.IO.File.ReadAllText(PrefsFilePath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("RememberUsername", out var remElement))
                    {
                        ChkRememberMe.IsChecked = remElement.GetBoolean();
                    }
                    if (doc.RootElement.TryGetProperty("Username", out var userElement))
                    {
                        TxtUsername.Text = userElement.GetString() ?? "";
                    }
                }
            }
            catch { }
        }

        private void SavePreferences()
        {
            try
            {
                var prefs = new
                {
                    RememberUsername = ChkRememberMe.IsChecked == true,
                    Username = ChkRememberMe.IsChecked == true ? TxtUsername.Text.Trim() : ""
                };
                System.IO.File.WriteAllText(PrefsFilePath, JsonSerializer.Serialize(prefs));
            }
            catch { }
        }

        private void ShakeLoginError()
        {
            var shake = new DoubleAnimation
            {
                From = 0,
                To = 5,
                Duration = TimeSpan.FromMilliseconds(50),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            LoginErrorTransform.BeginAnimation(TranslateTransform.XProperty, shake);
        }

        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            if (TxtPasswordVisible.Visibility == Visibility.Collapsed)
            {
                TxtPasswordVisible.Text = TxtPassword.Password;
                TxtPasswordVisible.Visibility = Visibility.Visible;
                TxtPassword.Visibility = Visibility.Collapsed;
                BtnTogglePassword.Foreground = (System.Windows.Media.Brush)FindResource("AccentSecondary");
            }
            else
            {
                TxtPassword.Password = TxtPasswordVisible.Text;
                TxtPasswordVisible.Visibility = Visibility.Collapsed;
                TxtPassword.Visibility = Visibility.Visible;
                BtnTogglePassword.Foreground = (System.Windows.Media.Brush)FindResource("TextMuted");
            }
        }

        private void StartStatusDotAnimation()
        {
            var glow = new DoubleAnimation
            {
                From = 1.0,
                To = 0.4,
                Duration = TimeSpan.FromSeconds(1.5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            StatusDot.BeginAnimation(UIElement.OpacityProperty, glow);
        }

        private void StopStatusDotAnimation()
        {
            StatusDot.BeginAnimation(UIElement.OpacityProperty, null);
            StatusDot.Opacity = 1.0;
        }

        private DispatcherTimer? _uptimeTimer;
        private DateTime _connectionStartTime;

        private void StartUptimeTimer()
        {
            _connectionStartTime = DateTime.Now;
            if (_uptimeTimer == null)
            {
                _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _uptimeTimer.Tick += (s, e) =>
                {
                    var diff = DateTime.Now - _connectionStartTime;
                    if (diff.TotalHours >= 1.0)
                        TxtUptime.Text = $"Connected for {(int)diff.TotalHours}h {diff.Minutes}m";
                    else if (diff.TotalMinutes >= 1.0)
                        TxtUptime.Text = $"Connected for {diff.Minutes}m {diff.Seconds}s";
                    else
                        TxtUptime.Text = $"Connected for {diff.Seconds}s";
                };
            }
            _uptimeTimer.Start();
        }

        private void StopUptimeTimer()
        {
            _uptimeTimer?.Stop();
            TxtUptime.Text = "Connected for 0s";
            TxtPing.Text = "•  -- ms";
            TxtPing.Foreground = (System.Windows.Media.Brush)FindResource("TextMuted");
        }

        private async Task PingGatewayLoop()
        {
            while (VpnService.IsConnected && PanelDashboard.Visibility == Visibility.Visible)
            {
                try
                {
                    var ping = new System.Net.NetworkInformation.Ping();
                    var reply = await ping.SendPingAsync("10.77.0.1", 2000);
                    if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    {
                        TxtPing.Text = $"•  {reply.RoundtripTime} ms";
                        TxtPing.Foreground = (System.Windows.Media.Brush)FindResource("SuccessColor");
                    }
                    else
                    {
                        TxtPing.Text = "•  Timeout";
                        TxtPing.Foreground = (System.Windows.Media.Brush)FindResource("DangerIcon");
                    }
                }
                catch
                {
                    TxtPing.Text = "•  Error";
                    TxtPing.Foreground = (System.Windows.Media.Brush)FindResource("DangerIcon");
                }
                await Task.Delay(5000);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            ListPeers.ItemsSource = _peers;

            LoadPreferences();

            this.Loaded += (s, e) =>
            {
                if (VpnService.IsConnected)
                {
                    TxtAssignedIp.Text = !string.IsNullOrEmpty(VpnService.AssignedIp) ? VpnService.AssignedIp : "—";
                    TxtCurrentUser.Text = $"Account: {VpnService.CurrentUsername}";
                    PanelLogin.Visibility = Visibility.Collapsed;
                    PanelDashboard.Visibility = Visibility.Visible;
                    StartStatusDotAnimation();
                    StartUptimeTimer();
                    _ = PingGatewayLoop();
                    FileTransferService.StartListener();
                    _ = RefreshPeersLoop();
                }
                else
                {
                    TxtUsername.Focus();
                }
            };

            FileTransferService.FileReceived += (fileName, fullPath, size) =>
            {
                Dispatcher.Invoke(() =>
                {
                    OverlayProgress.Visibility = Visibility.Collapsed;
                    MessageBox.Show($"File received: {fileName}\nSaved to: {fullPath}", "P2P File Transfer", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            };

            FileTransferService.TransferProgress += (fileName, percent) =>
            {
                Dispatcher.Invoke(() =>
                {
                    OverlayProgress.Visibility = Visibility.Visible;
                    TxtTransferFile.Text = $"Transferring {fileName}...";
                    ProgressTransfer.Value = percent;
                    TxtTransferPercent.Text = $"{percent}%";

                    if (percent >= 100)
                    {
                        Task.Delay(1000).ContinueWith(_ => Dispatcher.Invoke(() => OverlayProgress.Visibility = Visibility.Collapsed));
                    }
                });
            };

            VpnService.SessionExpired += () =>
            {
                Dispatcher.Invoke(async () =>
                {
                    await VpnService.LogoutAsync();
                    FileTransferService.StopListener();
                    StopStatusDotAnimation();
                    StopUptimeTimer();
                    TxtAssignedIp.Text = "—";
                    TxtCurrentUser.Text = "Account: —";
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
            
            if (TxtPasswordVisible.Visibility == Visibility.Visible)
            {
                TxtPassword.Password = TxtPasswordVisible.Text;
            }
            var password = TxtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                TxtLoginError.Text = "Please enter username and password.";
                TxtLoginError.Visibility = Visibility.Visible;
                ShakeLoginError();
                return;
            }

            BtnLogin.IsEnabled = false;
            TxtLoginStatus.Visibility = Visibility.Visible;
            TxtLoginStatus.Text = "Starting login...";

            var result = await VpnService.LoginAsync(username, password, (status) =>
            {
                Dispatcher.Invoke(() =>
                {
                    TxtLoginStatus.Text = status;
                });
            });

            BtnLogin.IsEnabled = true;
            TxtLoginStatus.Visibility = Visibility.Collapsed;

            if (result.Success)
            {
                SavePreferences();
                
                TxtAssignedIp.Text = result.AssignedIp ?? (!string.IsNullOrEmpty(VpnService.AssignedIp) ? VpnService.AssignedIp : "—");
                TxtCurrentUser.Text = $"Account: {VpnService.CurrentUsername}";
                PanelLogin.Visibility = Visibility.Collapsed;
                PanelDashboard.Visibility = Visibility.Visible;
                StartStatusDotAnimation();
                StartUptimeTimer();
                _ = PingGatewayLoop();

                FileTransferService.StartListener();
                _ = RefreshPeersLoop();
            }
            else
            {
                TxtLoginError.Text = result.Message;
                TxtLoginError.Visibility = Visibility.Visible;
                ShakeLoginError();
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

                        var newPeers = new System.Collections.Generic.List<PeerModel>();
                        foreach (var el in peersArray.EnumerateArray())
                        {
                            bool isOnline = el.GetProperty("isOnline").GetBoolean();
                            string lastSeenStr = string.Empty;
                            if (!isOnline && el.TryGetProperty("lastSeenAt", out var ls) && ls.ValueKind == JsonValueKind.String)
                            {
                                if (DateTime.TryParse(ls.GetString(), out var lastSeenDate))
                                {
                                    var diff = DateTime.UtcNow - lastSeenDate;
                                    if (diff.TotalDays >= 1)
                                        lastSeenStr = $"Offline • {(int)diff.TotalDays}d ago";
                                    else if (diff.TotalHours >= 1)
                                        lastSeenStr = $"Offline • {(int)diff.TotalHours}h ago";
                                    else if (diff.TotalMinutes >= 1)
                                        lastSeenStr = $"Offline • {(int)diff.TotalMinutes}m ago";
                                    else
                                        lastSeenStr = $"Offline • just now";
                                }
                            }
                            newPeers.Add(new PeerModel
                            {
                                Id = el.GetProperty("id").GetInt32(),
                                Username = el.GetProperty("username").GetString() ?? "",
                                Ip = el.GetProperty("ip").GetString() ?? "",
                                DeviceName = el.GetProperty("deviceName").GetString() ?? "",
                                IsOnline = isOnline,
                                LastSeenString = lastSeenStr
                            });
                        }

                        // Sort online first, then by username
                        newPeers.Sort((a, b) =>
                        {
                            if (a.IsOnline && !b.IsOnline) return -1;
                            if (!a.IsOnline && b.IsOnline) return 1;
                            return string.Compare(a.Username, b.Username, StringComparison.OrdinalIgnoreCase);
                        });

                        _peers.Clear();
                        foreach (var p in newPeers)
                        {
                            _peers.Add(p);
                        }
                        
                        TxtEmptyPeers.Visibility = _peers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                        // Ping online peers
                        foreach (var p in _peers)
                        {
                            if (p.IsOnline && !string.IsNullOrEmpty(p.Ip))
                            {
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        var ping = new System.Net.NetworkInformation.Ping();
                                        var reply = await ping.SendPingAsync(p.Ip, 1000);
                                        if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                                        {
                                            Dispatcher.Invoke(() => p.LatencyString = $"• {reply.RoundtripTime} ms");
                                        }
                                        else
                                        {
                                            Dispatcher.Invoke(() => p.LatencyString = "• Timeout");
                                        }
                                    }
                                    catch { Dispatcher.Invoke(() => p.LatencyString = "• Error"); }
                                });
                            }
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
                
                StatusDot.Visibility = Visibility.Collapsed;
                StatusSpinner.Visibility = Visibility.Visible;

                bool ok = await WireGuardTunnelManager.SetRouteAllTrafficAsync(true);
                VpnService.IsConnected = ok;

                TxtStatus.BeginAnimation(UIElement.OpacityProperty, null);
                TxtStatus.Opacity = 1.0;
                ChkRouteAll.IsEnabled = true;
                
                StatusDot.Visibility = Visibility.Visible;
                StatusSpinner.Visibility = Visibility.Collapsed;

                TxtStatus.Text = ok ? "FULL TUNNEL ACTIVE" : "GATEWAY ERROR";
                TxtStatus.Foreground = ok ? System.Windows.Media.Brushes.DodgerBlue : System.Windows.Media.Brushes.Red;
                StatusDot.Background = ok ? System.Windows.Media.Brushes.DodgerBlue : System.Windows.Media.Brushes.Red;
                TxtTunnelMode.Text = ok ? "Current Mode: Full Tunnel" : "Current Mode: Error";
                TxtTunnelMode.Foreground = ok ? System.Windows.Media.Brushes.DodgerBlue : System.Windows.Media.Brushes.Red;

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

                StatusDot.Visibility = Visibility.Collapsed;
                StatusSpinner.Visibility = Visibility.Visible;

                bool ok = await WireGuardTunnelManager.SetRouteAllTrafficAsync(false);
                VpnService.IsConnected = ok;

                TxtStatus.BeginAnimation(UIElement.OpacityProperty, null);
                TxtStatus.Opacity = 1.0;
                ChkRouteAll.IsEnabled = true;

                StatusDot.Visibility = Visibility.Visible;
                StatusSpinner.Visibility = Visibility.Collapsed;

                TxtStatus.Text = ok ? "CONNECTED" : "GATEWAY ERROR";
                TxtStatus.Foreground = ok ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Red;
                StatusDot.Background = ok ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Red;
                TxtTunnelMode.Text = ok ? "Current Mode: Split Tunnel" : "Current Mode: Error";
                TxtTunnelMode.Foreground = ok ? System.Windows.Media.Brushes.MediumSeaGreen : System.Windows.Media.Brushes.Red;
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
        private void BtnCopyIp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string ip)
            {
                Clipboard.SetText(ip);
                MessageBox.Show($"Copied IP address: {ip}", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            PanelSettings.Visibility = Visibility.Visible;
        }

        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            PanelSettings.Visibility = Visibility.Collapsed;
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // Save preferences (In a real app, update prefs.json here)
            PanelSettings.Visibility = Visibility.Collapsed;
        }

        private void ChkTheme_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            
            bool isDark = ChkTheme.IsChecked == true;
            string themeUri = isDark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
            
            var dict = new ResourceDictionary() { Source = new Uri(themeUri, UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            string originalContent = BtnLogout.Content?.ToString() ?? "Release Device & Sign Out";
            BtnLogout.Content = "Logging out...";
            BtnLogout.IsEnabled = false;

            await VpnService.LogoutAsync();
            FileTransferService.StopListener();
            StopStatusDotAnimation();
            StopUptimeTimer();

            TxtAssignedIp.Text = "—";
            TxtCurrentUser.Text = "Account: —";
            PanelDashboard.Visibility = Visibility.Collapsed;
            PanelLogin.Visibility = Visibility.Visible;
            TxtPassword.Password = string.Empty;

            BtnLogout.Content = originalContent;
            BtnLogout.IsEnabled = true;
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