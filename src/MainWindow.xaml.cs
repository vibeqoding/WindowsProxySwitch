using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace WindowsProxySwitch
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowActivated(object sender, EventArgs e) => RefreshWindow();

        private void RefreshWindow()
        {
            ShowFeedback(null);
            try
            {
                DisplayState(LanProxy.Get());
            }
            catch (Win32Exception error)
            {
                DisplayReadError();
                ShowFeedback(error.Message);
            }

            ReadPreset();
        }

        private void EnableClicked(object sender, RoutedEventArgs e)
        {
            ShowFeedback(null);
            ProxyPreset preset = ReadPreset("开启代理失败。");
            if (preset != null)
                ChangeProxy("开启代理", () => LanProxy.Enable(preset));
        }

        private void DisableClicked(object sender, RoutedEventArgs e)
        {
            ShowFeedback(null);
            ChangeProxy("关闭代理", LanProxy.Disable);
        }

        private ProxyPreset ReadPreset(string failurePrefix = "")
        {
            try
            {
                ProxyPreset preset = ProxyPreset.Load();
                IPAddressInput.Text = preset.IP;
                PortInput.Text = preset.Port;
                return preset;
            }
            catch (InvalidOperationException error)
            {
                IPAddressInput.Clear();
                PortInput.Clear();
                ShowFeedback(failurePrefix + error.Message);
                return null;
            }
        }

        private void ChangeProxy(string action, Func<LanProxy.State> change)
        {
            try
            {
                DisplayState(change());
                ShowFeedback(action + "成功。", succeeded: true);
            }
            catch (Exception error) when (error is Win32Exception || error is InvalidOperationException)
            {
                try
                {
                    DisplayState(LanProxy.Get());
                }
                catch (Win32Exception)
                {
                    DisplayReadError();
                }
                ShowFeedback(action + "失败。" + error.Message);
            }
        }

        private void DisplayState(LanProxy.State state)
        {
            StateText.Text = state.Enabled ? "已开启" : "已关闭";
            StateText.Foreground = new SolidColorBrush(state.Enabled ? Color.FromRgb(16, 96, 71) : Color.FromRgb(73, 87, 107));
            StateBadge.Background = new SolidColorBrush(state.Enabled ? Color.FromRgb(224, 247, 236) : Color.FromRgb(237, 241, 246));
        }

        private void DisplayReadError()
        {
            StateText.Text = "读取失败";
            StateText.Foreground = Brushes.DarkRed;
            StateBadge.Background = Brushes.MistyRose;
        }

        private void ShowFeedback(string message, bool succeeded = false)
        {
            Feedback.Text = message ?? string.Empty;
            Feedback.Foreground = succeeded ? Brushes.DarkGreen : Brushes.DarkRed;
            Feedback.Visibility = string.IsNullOrEmpty(message) ? Visibility.Hidden : Visibility.Visible;
        }
    }
}
