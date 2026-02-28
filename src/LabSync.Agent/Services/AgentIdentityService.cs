using LabSync.Core.Dto;
using LabSync.Core.Types;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace LabSync.Agent.Services;

public class AgentIdentityService(ILogger<AgentIdentityService> logger)
{
    public RegisterAgentRequest CollectIdentity()
    {
        return new RegisterAgentRequest(
            GetMacAddress(),
            System.Net.Dns.GetHostName(),
            GetPlatform(),
            RuntimeInformation.OSDescription,
            GetLocalIpAddress()
        );
    }

    private DevicePlatform GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return DevicePlatform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return DevicePlatform.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return DevicePlatform.MacOS;
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
            logger.LogWarning("No active network interface found. Using fallback.");
            return "00:00:00:00:00:00";
        }

        var macBytes = nic.GetPhysicalAddress().GetAddressBytes();
        return string.Join(":", macBytes.Select(b => b.ToString("X2")));
    }

    private string? GetLocalIpAddress()
    {
        var ip = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback && ni.OperationalStatus == OperationalStatus.Up)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Where(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(ip => ip.Address.ToString())
            .FirstOrDefault();

        return ip ?? "127.0.0.1";
    }
}