using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LabSync.Agent.Modules.RemoteDesktop.Infrastructure;

public static class LinuxDisplayHelper
{
    public static async Task<(int Width, int Height)> GetScreenResolutionAsync(CancellationToken cancellationToken = default)
    {
        // Try xrandr first
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xrandr",
                Arguments = "--current",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                var match = Regex.Match(output, @"current\s+(\d+)\s+x\s+(\d+)");
                if (match.Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out int w) && int.TryParse(match.Groups[2].Value, out int h))
                    {
                        return (w, h);
                    }
                }
            }
        }
        catch { /* Ignore and try next method */ }

        // Try xdpyinfo
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xdpyinfo",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                var match = Regex.Match(output, @"dimensions:\s+(\d+)x(\d+)\s+pixels");
                if (match.Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out int w) && int.TryParse(match.Groups[2].Value, out int h))
                    {
                        return (w, h);
                    }
                }
            }
        }
        catch { /* Ignore */ }

        return (1920, 1080);
    }
}
