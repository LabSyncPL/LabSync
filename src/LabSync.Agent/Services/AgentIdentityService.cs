using LabSync.Core.Dto;
using LabSync.Core.ValueObjects;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace LabSync.Agent.Services
{
    public class AgentIdentityService
    {
        private readonly ILogger<AgentIdentityService> _logger;

        public AgentIdentityService(ILogger<AgentIdentityService> logger)
        {
            _logger = logger;
        }

        public RegisterAgentRequest CollectIdentity()
        {
            return new RegisterAgentRequest
            {
                Hostname   = System.Net.Dns.GetHostName(),
                MacAddress = GetMacAddress(),
                Platform   = GetPlatform(),
                OsVersion  = RuntimeInformation.OSDescription,
                IpAddress  = GetLocalIpAddress()
            };
        }

        private DevicePlatform GetPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return DevicePlatform.Windows;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))   return DevicePlatform.Linux;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))     return DevicePlatform.MacOS;
            return DevicePlatform.Unknown;
        }

        private string GetMacAddress()
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet || n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .OrderByDescending(n => n.Speed)
                .FirstOrDefault();

            if (nic == null)
            {
                _logger.LogWarning("No active network interface found. Using fallback.");
                return "00:00:00:00:00:00";
            }

            var macBytes = nic.GetPhysicalAddress().GetAddressBytes();
            return string.Join(":", macBytes.Select(b => b.ToString("X2")));
        }

        private string? GetLocalIpAddress()
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString();
        }
    }
}