using System.Windows;
using System.Windows.Controls;

namespace CustomVPN.Client
{
    public class ServerConfigDialog : Window
    {
        public string ServerAddress { get; private set; }

        private TextBox _txtAddress;

        public ServerConfigDialog(string currentAddress)
        {
            Title = "Target Server Configuration";
            Width = 420;
            Height = 220;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
            Foreground = System.Windows.Media.Brushes.White;
            ResizeMode = ResizeMode.NoResize;

            var stack = new StackPanel { Margin = new Thickness(24) };

            var label = new TextBlock
            {
                Text = "Server Endpoint (IP or Domain)",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 8)
            };

            _txtAddress = new TextBox
            {
                Text = currentAddress,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 6, 23)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85)),
                Margin = new Thickness(0, 0, 0, 20)
            };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnCancel = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(16, 8, 16, 8),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };

            var btnSave = new Button
            {
                Content = "Save & Apply",
                Padding = new Thickness(16, 8, 16, 8),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 182, 212)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 6, 23)),
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0)
            };
            btnSave.Click += (s, e) =>
            {
                ServerAddress = _txtAddress.Text.Trim();
                DialogResult = true;
                Close();
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnSave);

            stack.Children.Add(label);
            stack.Children.Add(_txtAddress);
            stack.Children.Add(btnPanel);

            Content = stack;
            ServerAddress = currentAddress;
        }
    }
}
