using System;
using System.ComponentModel;
using System.Windows;

namespace WindowsProxySwitch
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 0)
                return new Application().Run(new MainWindow());

            try
            {
                if (args.Length == 1 && args[0].Equals("On", StringComparison.OrdinalIgnoreCase))
                    LanProxy.Enable(ProxyPreset.Load());
                else if (args.Length == 1 && args[0].Equals("Off", StringComparison.OrdinalIgnoreCase))
                    LanProxy.Disable();
                else
                    throw new InvalidOperationException("支持的命令参数为 On 或 Off。");

                return 0;
            }
            catch (Win32Exception error)
            {
                ShowError(error.Message);
                return 1;
            }
            catch (InvalidOperationException error)
            {
                ShowError(error.Message);
                return 1;
            }
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, "系统代理", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
