using System.Configuration;
using System.Data;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using CustomVPN.Client;

namespace CustomVPN.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private TaskbarIcon? _taskbarIcon;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _taskbarIcon = new TaskbarIcon
        {
            Icon = System.Drawing.SystemIcons.Shield,
            ToolTipText = "CustomVPN"
        };

        var menu = new System.Windows.Controls.ContextMenu();
        var showItem = new System.Windows.Controls.MenuItem { Header = "Show" };
        showItem.Click += (s, ev) =>
        {
            if (MainWindow != null)
            {
                MainWindow.Show();
                MainWindow.WindowState = WindowState.Normal;
                MainWindow.Activate();
            }
        };

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Disconnect & Exit" };
        exitItem.Click += async (s, ev) =>
        {
            if (VpnService.IsConnected || WireGuardTunnelManager.IsTunnelActive())
            {
                await VpnService.LogoutAsync();
            }
            Shutdown();
        };

        menu.Items.Add(showItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(exitItem);

        _taskbarIcon.ContextMenu = menu;
        _taskbarIcon.TrayMouseDoubleClick += (s, ev) =>
        {
            if (MainWindow != null)
            {
                MainWindow.Show();
                MainWindow.WindowState = WindowState.Normal;
                MainWindow.Activate();
            }
        };

        bool restored = await VpnService.TryRestoreSessionAsync();
        if (!restored && WireGuardTunnelManager.IsTunnelActive())
        {
            await WireGuardTunnelManager.DeactivateTunnelAsync();
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    public void ShowBalloonTip()
    {
        _taskbarIcon?.ShowBalloonTip("CustomVPN", "CustomVPN is running in the background. Right-click tray icon to manage.", BalloonIcon.Info);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _taskbarIcon?.Dispose();
        base.OnExit(e);
    }
}
