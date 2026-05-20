using LabSync.Core.Dto;
using LabSync.Core.Types;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Net;

namespace LabSync.Agent.Services;

public class AgentIdentityService(ILogger<AgentIdentityService> logger)
{
    public RegisterAgentRequest CollectIdentity()
    {
        var ipAddress = GetLocalIpAddress() ?? "127.0.0.1";
        var macAddress = GetMacAddress(ipAddress);

        return new RegisterAgentRequest(
            macAddress,
            System.Net.Dns.GetHostName(),
            GetPlatform(),
            RuntimeInformation.OSDescription,
            ipAddress
        );
    }

    private DevicePlatform GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return DevicePlatform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return DevicePlatform.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return DevicePlatform.MacOS;
        return DevicePlatform.Unknown;
    }

    private string? GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                return endPoint.Address.ToString();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve real local IP using UDP trick. Falling back to iteration.");
        }

        var fallbackIp = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback && ni.OperationalStatus == OperationalStatus.Up)
            .Where(ni => !ni.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase) && 
                         !ni.Name.Contains("docker", StringComparison.OrdinalIgnoreCase))
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(ip => ip.Address.ToString())
            .FirstOrDefault();

        return fallbackIp;
    }

    private string GetMacAddress(string targetIp)
    {
        try
        {
            var activeInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up);
            foreach (var nic in activeInterfaces)
            {
                var ipProps = nic.GetIPProperties();
                var hasTargetIp = ipProps.UnicastAddresses.Any(ip => ip.Address.ToString() == targetIp);

                if (hasTargetIp)
                {
                    var macBytes = nic.GetPhysicalAddress().GetAddressBytes();
                    if (macBytes.Length > 0)
                    {
                        return string.Join(":", macBytes.Select(b => b.ToString("X2")));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve MAC by target IP. Using fallback.");
        }

        var fallbackNic = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Where(n => !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                        !n.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) &&
                        !n.Description.Contains("docker", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.Speed)
            .FirstOrDefault();

        if (fallbackNic == null)
        {
            logger.LogWarning("No suitable network interface found for MAC address. Returning default.");
            return "00:00:00:00:00:00";
        }

        var fallbackMacBytes = fallbackNic.GetPhysicalAddress().GetAddressBytes();
        return string.Join(":", fallbackMacBytes.Select(b => b.ToString("X2")));
    }
}