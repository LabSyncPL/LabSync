using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using LabSync.Modules.SystemInfo.Interfaces;
using LabSync.Modules.SystemInfo.Models;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.SystemInfo
{
    public class NetworkInfoService : INetworkInfoService
    {
        private readonly ILogger? _logger;

        public NetworkInfoService(ILogger? logger)
        {
            _logger = logger;
        }

        public NetworkInfo GetNetworkInfo()
        {
            try
            {
                var snapshot1 = CaptureInterfaceStats();
                Thread.Sleep(1000);
                var snapshot2 = CaptureInterfaceStats();

                var interfaces = new List<NetworkInterfaceInfo>();
                double totalSentPerSecond = 0;
                double totalReceivedPerSecond = 0;

                foreach (var entry in snapshot1)
                {
                    if (!snapshot2.TryGetValue(entry.Key, out var second))
                    {
                        continue;
                    }

                    var deltaSent = second.BytesSent - entry.Value.BytesSent;
                    var deltaReceived = second.BytesReceived - entry.Value.BytesReceived;

                    if (deltaSent < 0 || deltaReceived < 0)
                    {
                        continue;
                    }

                    totalSentPerSecond += deltaSent;
                    totalReceivedPerSecond += deltaReceived;

                    interfaces.Add(new NetworkInterfaceInfo
                    {
                        Name = entry.Value.Name,
                        Description = entry.Value.Description,
                        BytesSentPerSecond = deltaSent,
                        BytesReceivedPerSecond = deltaReceived,
                        IPv4Address = entry.Value.IPv4Address,
                        IsUp = entry.Value.IsUp
                    });
                }

                return new NetworkInfo
                {
                    TotalBytesSentPerSecond = totalSentPerSecond,
                    TotalBytesReceivedPerSecond = totalReceivedPerSecond,
                    Interfaces = interfaces
                };
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get network info");
                return new NetworkInfo();
            }
        }

        private static Dictionary<string, InterfaceSample> CaptureInterfaceStats()
        {
            var result = new Dictionary<string, InterfaceSample>();

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var stats = ni.GetIPv4Statistics();
                var ip = ni.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();

                result[ni.Id] = new InterfaceSample
                {
                    Name = ni.Name,
                    Description = ni.Description,
                    BytesSent = stats.BytesSent,
                    BytesReceived = stats.BytesReceived,
                    IPv4Address = ip,
                    IsUp = ni.OperationalStatus == OperationalStatus.Up
                };
            }

            return result;
        }

        private sealed class InterfaceSample
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public long BytesSent { get; set; }
            public long BytesReceived { get; set; }
            public string? IPv4Address { get; set; }
            public bool IsUp { get; set; }
        }
    }
}

