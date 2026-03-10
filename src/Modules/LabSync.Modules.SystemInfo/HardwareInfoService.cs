using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using LabSync.Core.Types;
using LabSync.Modules.SystemInfo.Interfaces;
using LabSync.Modules.SystemInfo.Models;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.SystemInfo
{
    public class HardwareInfoService : IHardwareInfoService
    {
        private readonly ILogger? _logger;
        private readonly DevicePlatform _platform;

        public HardwareInfoService(ILogger? logger)
        {
            _logger = logger;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) _platform = DevicePlatform.Windows;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) _platform = DevicePlatform.Linux;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) _platform = DevicePlatform.MacOS;
            else _platform = DevicePlatform.Unknown;
        }

        public HardwareInfo GetHardwareInfo()
        {
            var info = new HardwareInfo();

            try
            {
                switch (_platform)
                {
                    case DevicePlatform.Windows:
                        GetWindowsHardwareInfo(info);
                        break;
                    case DevicePlatform.Linux:
                        GetLinuxHardwareInfo(info);
                        break;
                    case DevicePlatform.MacOS:
                        GetMacHardwareInfo(info);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get hardware info");
            }

            return info;
        }

        private void GetWindowsHardwareInfo(HardwareInfo info)
        {
#if NET9_0_OR_GREATER
            if (!OperatingSystem.IsWindows()) return;
#endif
            // CPU
            try
            {
                var output = RunPowerShell("Get-CimInstance Win32_Processor | Select-Object -Property Name | ConvertTo-Json");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    using var doc = JsonDocument.Parse(output);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                         foreach (var item in root.EnumerateArray())
                         {
                             if (item.TryGetProperty("Name", out var nameProp))
                             {
                                 info.CpuName = nameProp.GetString() ?? "Unknown";
                                 break;
                             }
                         }
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("Name", out var nameProp))
                        {
                            info.CpuName = nameProp.GetString() ?? "Unknown";
                        }
                    }
                }
            }
            catch { }

            // GPU
            try
            {
                var output = RunPowerShell("Get-CimInstance Win32_VideoController | Select-Object -Property Name | ConvertTo-Json");
                var gpuNames = new List<string>();
                if (!string.IsNullOrWhiteSpace(output))
                {
                     using var doc = JsonDocument.Parse(output);
                     var root = doc.RootElement;
                     if (root.ValueKind == JsonValueKind.Array)
                     {
                         foreach(var item in root.EnumerateArray())
                         {
                             if (item.TryGetProperty("Name", out var p)) gpuNames.Add(p.GetString() ?? "");
                         }
                     }
                     else if (root.ValueKind == JsonValueKind.Object)
                     {
                         if (root.TryGetProperty("Name", out var p)) gpuNames.Add(p.GetString() ?? "");
                     }
                }
                if (gpuNames.Count > 0) info.GpuName = string.Join(", ", gpuNames);
            }
            catch { }

            // RAM
            try
            {
                var output = RunPowerShell("Get-CimInstance Win32_ComputerSystem | Select-Object -Property TotalPhysicalMemory | ConvertTo-Json");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    using var doc = JsonDocument.Parse(output);
                    var root = doc.RootElement;
                    // Usually single object
                    JsonElement target = root.ValueKind == JsonValueKind.Array ? root[0] : root;
                    
                    if (target.TryGetProperty("TotalPhysicalMemory", out var memProp))
                    {
                        if (memProp.ValueKind == JsonValueKind.Number && memProp.TryGetInt64(out long bytes))
                        {
                             info.TotalRam = FormatBytes(bytes);
                        }
                    }
                }
            }
            catch { }

            // Disks
            try
            {
                var output = RunPowerShell("Get-CimInstance Win32_DiskDrive | Select-Object Model, Size, MediaType | ConvertTo-Json");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    using var doc = JsonDocument.Parse(output);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach(var item in root.EnumerateArray())
                        {
                            ParseDiskJson(item, info);
                        }
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        ParseDiskJson(root, info);
                    }
                }
            }
            catch { }

            // Network
            try
            {
                var output = RunPowerShell("Get-CimInstance Win32_NetworkAdapter | Where-Object { $_.NetConnectionStatus -eq 2 } | Select-Object Name, MACAddress | ConvertTo-Json");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    using var doc = JsonDocument.Parse(output);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach(var item in root.EnumerateArray())
                        {
                            ParseNetworkJson(item, info);
                        }
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        ParseNetworkJson(root, info);
                    }
                }
            }
            catch { }
        }

        private void ParseDiskJson(JsonElement item, HardwareInfo info)
        {
            var model = item.TryGetProperty("Model", out var m) ? m.GetString() : "Unknown";
            var type = item.TryGetProperty("MediaType", out var t) ? t.GetString() : "Disk";
            var sizeStr = "Unknown";
            
            if (item.TryGetProperty("Size", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt64(out long bytes))
            {
                sizeStr = FormatBytes(bytes);
            }
            
            info.Disks.Add(new DiskSpec { Model = model ?? "Unknown", Type = type ?? "Disk", Size = sizeStr });
        }

        private void ParseNetworkJson(JsonElement item, HardwareInfo info)
        {
             var name = item.TryGetProperty("Name", out var n) ? n.GetString() : "Unknown";
             var mac = item.TryGetProperty("MACAddress", out var m) ? m.GetString() : "";
             info.NetworkAdapters.Add(new NetworkAdapterSpec { Name = name ?? "Unknown", MacAddress = mac ?? "", Status = "Up" });
        }
        
        private string RunPowerShell(string script)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"{script}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(startInfo);
                if (process == null) return "";
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(2000);
                return output.Trim();
            }
            catch
            {
                return "";
            }
        }

        private void GetLinuxHardwareInfo(HardwareInfo info)
        {
            // CPU
            try
            {
                // Try lscpu first as it is cleaner
                var lscpu = RunCommand("lscpu", "");
                var modelName = lscpu.Split('\n').FirstOrDefault(l => l.Contains("Model name:"));
                if (modelName != null)
                {
                    info.CpuName = modelName.Replace("Model name:", "").Trim();
                }
                else
                {
                    // Fallback to /proc/cpuinfo
                    var cpuInfo = File.ReadAllText("/proc/cpuinfo");
                    var modelNameLine = cpuInfo.Split('\n').FirstOrDefault(l => l.Contains("model name"));
                    if (modelNameLine != null)
                    {
                        info.CpuName = modelNameLine.Split(':')[1].Trim();
                    }
                }
            }
            catch { }

            // RAM
            try
            {
                var memInfo = File.ReadAllText("/proc/meminfo");
                var totalMemLine = memInfo.Split('\n').FirstOrDefault(l => l.Contains("MemTotal"));
                if (totalMemLine != null)
                {
                    var parts = totalMemLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out long kb))
                    {
                        info.TotalRam = FormatBytes(kb * 1024);
                    }
                }
            }
            catch { }

            // GPU (lspci)
            try
            {
                var lspci = RunCommand("lspci", "");
                var vgaLines = lspci.Split('\n').Where(l => l.Contains("VGA") || l.Contains("3D") || l.Contains("Display"));
                info.GpuName = string.Join(", ", vgaLines.Select(l => l.Substring(l.IndexOf(':') + 1).Trim()));
            }
            catch { }

            // Disks (lsblk)
            try
            {
                var lsblkPairs = RunCommand("lsblk", "-d -n -P -o MODEL,SIZE,TYPE");
                foreach (var line in lsblkPairs.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    var model = ExtractValue(line, "MODEL");
                    var size = ExtractValue(line, "SIZE");
                    var type = ExtractValue(line, "TYPE");
                    
                    if (string.IsNullOrWhiteSpace(model) && string.IsNullOrWhiteSpace(size)) continue;
                    if (type == "loop" || type == "rom") continue; // skip loops and cdroms often
                    
                    info.Disks.Add(new DiskSpec 
                    { 
                        Model = model, 
                        Size = size, 
                        Type = type 
                    });
                }
            }
            catch { }

            // Network
            try
            {
                var ipLink = RunCommand("ip", "-o link show");
                foreach (var line in ipLink.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    // 2: enp3s0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 ... link/ether xx:xx:xx:xx:xx:xx brd ...
                    var parts = line.Split(new[] { ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        var name = parts[1];
                        var mac = "";
                        var macIdx = line.IndexOf("link/ether");
                        if (macIdx != -1 && macIdx + 11 < line.Length)
                        {
                             var remainder = line.Substring(macIdx + 11).Trim();
                             mac = remainder.Split(' ')[0];
                        }
                        
                        var status = line.Contains(",UP,") || line.Contains("<UP,") ? "Up" : "Down";
                        
                        if (name == "lo") continue;

                        info.NetworkAdapters.Add(new NetworkAdapterSpec { Name = name, MacAddress = mac, Status = status });
                    }
                }
            }
            catch { }
        }

        private string ExtractValue(string line, string key)
        {
            // MODEL="Value"
            var keyStr = key + "=\"";
            var idx = line.IndexOf(keyStr);
            if (idx == -1) return "";
            var start = idx + keyStr.Length;
            var end = line.IndexOf("\"", start);
            if (end == -1) return "";
            return line.Substring(start, end - start);
        }

        private void GetMacHardwareInfo(HardwareInfo info)
        {
        }

        private string RunCommand(string cmd, string args)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(startInfo);
                if (process == null) return "";
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(1000);
                return output.Trim();
            }
            catch
            {
                return "";
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
