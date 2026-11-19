using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace WindowsProxySwitch
{
    internal sealed class ProxyPreset
    {
        private const string FileName = "proxy.txt";

        private ProxyPreset(IPAddress address, int port)
        {
            IP = address.ToString();
            Port = port.ToString(CultureInfo.InvariantCulture);
            Server = address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{IP}]:{Port}" : $"{IP}:{Port}";
        }

        internal string IP { get; }
        internal string Port { get; }
        internal string Server { get; }

        internal static ProxyPreset Load()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
            string endpoint;
            try
            {
                endpoint = File.ReadAllText(path).Trim();
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                throw new InvalidOperationException("无法读取程序同目录的 proxy.txt。", error);
            }

            const string invalidFormat = "请在 proxy.txt 中填写一组 IP:端口。IPv6 地址需加方括号，端口范围为 1 至 65535。";
            int separator = endpoint.LastIndexOf(':');
            if (separator <= 0)
                throw new InvalidOperationException(invalidFormat);

            string ip = endpoint.Substring(0, separator);
            bool bracketed = ip.StartsWith("[", StringComparison.Ordinal) && ip.EndsWith("]", StringComparison.Ordinal);
            if (bracketed)
                ip = ip.Substring(1, ip.Length - 2);

            if (!IPAddress.TryParse(ip, out IPAddress address) ||
                !int.TryParse(endpoint.Substring(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int port) ||
                port < 1 || port > 65535 ||
                (address.AddressFamily == AddressFamily.InterNetworkV6 ? !bracketed : bracketed || address.ToString() != ip))
                throw new InvalidOperationException(invalidFormat);

            return new ProxyPreset(address, port);
        }
    }
}
