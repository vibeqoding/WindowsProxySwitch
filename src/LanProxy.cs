using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WindowsProxySwitch
{
    internal static class LanProxy
    {
        private const uint ProxyEnabled = 2;
        private const uint PerConnectionOption = 75;
        private const uint ConnectionFlags = 1;
        private const uint ProxyServer = 2;
        private const uint ConnectionFlagsUI = 10;

        internal sealed class State
        {
            internal State(uint flags, string server)
            {
                Flags = flags;
                Server = server ?? string.Empty;
            }

            internal uint Flags { get; }
            internal string Server { get; }
            internal bool Enabled => (Flags & ProxyEnabled) != 0;
        }

        internal static State Get()
        {
            var options = new[] { new Option { Name = ConnectionFlagsUI }, new Option { Name = ProxyServer } };
            try
            {
                Execute(options, false);
                return new State(options[0].Value.Number, Marshal.PtrToStringUni(options[1].Value.Text));
            }
            finally
            {
                if (options[1].Value.Text != IntPtr.Zero)
                    GlobalFree(options[1].Value.Text);
            }
        }

        internal static State Enable(ProxyPreset preset) => SetEnabled(true, preset.Server);

        internal static State Disable() => SetEnabled(false, null);

        private static State SetEnabled(bool enabled, string newServer)
        {
            State current = Get();
            uint flags = enabled ? current.Flags | ProxyEnabled : current.Flags & ~ProxyEnabled;
            string server = enabled ? newServer : current.Server;

            bool serverChanged = !string.Equals(current.Server, server, StringComparison.OrdinalIgnoreCase);
            if (flags == current.Flags && !serverChanged)
                return current;

            var options = new List<Option>(2);
            IntPtr address = IntPtr.Zero;
            try
            {
                if (serverChanged)
                {
                    address = Marshal.StringToHGlobalUni(server);
                    options.Add(new Option { Name = ProxyServer, Value = new OptionValue { Text = address } });
                }
                if (flags != current.Flags)
                    options.Add(new Option { Name = ConnectionFlags, Value = new OptionValue { Number = flags } });

                Execute(options.ToArray(), true);
            }
            finally
            {
                Marshal.FreeHGlobal(address);
            }

            const uint ProxySettingsChanged = 95;
            const uint Refresh = 37;
            bool notified = InternetSetOption(IntPtr.Zero, ProxySettingsChanged, IntPtr.Zero, 0);
            int notificationError = Marshal.GetLastWin32Error();
            if (!InternetSetOption(IntPtr.Zero, Refresh, IntPtr.Zero, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!notified)
                throw new Win32Exception(notificationError);

            State actual = Get();
            if (actual.Flags != flags || !string.Equals(actual.Server, server, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("读取到的系统代理设置与本次操作不一致。");

            return actual;
        }

        private static void Execute(Option[] options, bool write)
        {
            int optionSize = Marshal.SizeOf(typeof(Option));
            uint listSize = (uint)Marshal.SizeOf(typeof(OptionList));
            // A null connection name selects the default LAN connection.
            var list = new OptionList { Size = listSize, Count = (uint)options.Length };
            IntPtr buffer = Marshal.AllocHGlobal(optionSize * options.Length);
            list.Options = buffer;
            try
            {
                for (int i = 0; i < options.Length; i++)
                    Marshal.StructureToPtr(options[i], IntPtr.Add(buffer, i * optionSize), false);

                bool success = write
                    ? InternetSetOption(IntPtr.Zero, PerConnectionOption, ref list, listSize)
                    : InternetQueryOption(IntPtr.Zero, PerConnectionOption, ref list, ref listSize);
                int error = Marshal.GetLastWin32Error();

                if (!write)
                {
                    for (int i = 0; i < options.Length; i++)
                        options[i] = (Option)Marshal.PtrToStructure(IntPtr.Add(buffer, i * optionSize), typeof(Option));
                }
                if (!success)
                    throw new Win32Exception(error);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct OptionValue
        {
            [FieldOffset(0)] public uint Number;
            [FieldOffset(0)] public IntPtr Text;
            // FILETIME takes 8 bytes even in a 32-bit process.
            [FieldOffset(4)] private uint fileTimeHigh;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Option
        {
            public uint Name;
            public OptionValue Value;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OptionList
        {
            public uint Size;
            public IntPtr Connection;
            public uint Count;
            public uint ErrorIndex;
            public IntPtr Options;
        }

        [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool InternetQueryOption(IntPtr internet, uint option,
            ref OptionList list, ref uint length);

        [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr internet, uint option,
            ref OptionList list, uint length);

        [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr internet, uint option,
            IntPtr buffer, uint length);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalFree(IntPtr memory);
    }
}
