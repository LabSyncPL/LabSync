using System.Diagnostics;
using System.Threading.Channels;
using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.Encoding;

/// <summary>
/// Windows-only H.264 encoder using an external ffmpeg process.
/// Expects raw BGRA frames and produces Annex B H.264 NAL units.
/// </summary>
public sealed class WindowsFfmpegVideoEncoder : IVideoEncoder
{
    private readonly ILogger _logger;
    private readonly int _channelCapacity;
    private readonly string _ffmpegPath;

    private Channel<EncodedFrame>? _channel;
    private EncoderOptions? _options;
    private Process? _process;
    private Stream? _stdin;
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;
    private bool _disposed;

    public WindowsFfmpegVideoEncoder(ILogger logger, int channelCapacity, string ffmpegPath = "ffmpeg")
    {
        _logger = logger;
        _channelCapacity = channelCapacity > 0 ? channelCapacity : 4;
        _ffmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? "ffmpeg" : ffmpegPath;
    }

    public async Task InitializeAsync(EncoderOptions options, CancellationToken cancellationToken = default)
    {
        _options = options;
        _channel = Channel.CreateBounded<EncodedFrame>(new BoundedChannelOptions(_channelCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = BuildFfmpegArguments(options),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            CreateNoWindow = true
        };

        try
        {
            _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg process.");
            _stdin = _process.StandardInput.BaseStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ffmpeg. Falling back to no-op encoding.");
            // Leave _channel initialized so GetEncodedStreamAsync still works, but no frames will be produced.
            return;
        }

        _readerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readerTask = Task.Run(() => ReadEncodedOutputAsync(_process, _channel, _readerCts.Token), _readerCts.Token);

        await Task.CompletedTask;
    }

    private static string BuildFfmpegArguments(EncoderOptions options)
    {
        // Input: raw BGRA frames via stdin.
        // Output: Annex B H.264 to stdout.
        var bitrate = options.TargetBitrateKbps > 0 ? options.TargetBitrateKbps : 2000;
        var fps = options.TargetFps > 0 ? options.TargetFps : 30;

        return $"-f rawvideo -pix_fmt bgra -s {options.Width}x{options.Height} -r {fps} -i - " +
               $"-c:v libx264 -preset veryfast -tune zerolatency -b:v {bitrate}k -g {fps * 2} -keyint_min {fps} " +
               "-f h264 -an -";
    }

    public async Task EncodeAsync(CaptureFrame frame, CancellationToken cancellationToken = default)
    {
        if (_disposed || _stdin == null || _options == null)
            return;

        try
        {
            // Assume frame.Format is BGRA32 and matches ffmpeg -pix_fmt bgra.
            await _stdin.WriteAsync(frame.Data, 0, frame.Data.Length, cancellationToken);
            await _stdin.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Normal on shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while sending raw frame to ffmpeg.");
        }
    }

    public async IAsyncEnumerable<EncodedFrame> GetEncodedStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = _channel;
        if (channel == null)
            yield break;

        await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return frame;
        }
    }

    private static async Task ReadEncodedOutputAsync(Process process, Channel<EncodedFrame>? channel, CancellationToken cancellationToken)
    {
        if (channel == null)
            return;

        var stdout = process.StandardOutput.BaseStream;
        var buffer = new byte[4096];
        var nalBuffer = new List<byte>(64 * 1024);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await stdout.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                    break;

                for (var i = 0; i < read; i++)
                {
                    nalBuffer.Add(buffer[i]);

                    // Simple Annex B start-code based splitting: look for 0x000001
                    if (nalBuffer.Count >= 4 &&
                        nalBuffer[^4] == 0x00 &&
                        nalBuffer[^3] == 0x00 &&
                        nalBuffer[^2] == 0x00 &&
                        nalBuffer[^1] == 0x01)
                    {
                        // When a new start code begins, previous bytes (except the 4-byte start code) form a NAL unit.
                        if (nalBuffer.Count > 4)
                        {
                            var nalUnit = nalBuffer.GetRange(0, nalBuffer.Count - 4).ToArray();
                            if (nalUnit.Length > 0)
                            {
                                var isKeyFrame = IsIdrNal(nalUnit);
                                var frame = new EncodedFrame(nalUnit, isKeyFrame, DateTime.UtcNow);
                                if (!channel.Writer.TryWrite(frame))
                                {
                                    // Drop oldest when full; bounded channel configured for DropOldest in caller.
                                }
                            }
                        }

                        nalBuffer.Clear();
                        nalBuffer.Add(0x00);
                        nalBuffer.Add(0x00);
                        nalBuffer.Add(0x00);
                        nalBuffer.Add(0x01);
                    }
                }
            }

            if (nalBuffer.Count > 0)
            {
                var nalUnit = nalBuffer.ToArray();
                var isKeyFrame = IsIdrNal(nalUnit);
                var frame = new EncodedFrame(nalUnit, isKeyFrame, DateTime.UtcNow);
                channel.Writer.TryWrite(frame);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal on shutdown.
        }
        catch
        {
            // If ffmpeg exits or stream errors, just stop reading.
        }
        finally
        {
            channel.Writer.TryComplete();
        }
    }

    private static bool IsIdrNal(byte[] nalUnit)
    {
        // Very basic NALU header parsing: NAL type is lower 5 bits of first byte after start code.
        // We expect nalUnit to include start code; find first non-zero sequence after it.
        var index = 0;
        // Skip leading zero bytes.
        while (index < nalUnit.Length && nalUnit[index] == 0x00)
            index++;
        // Skip single 0x01 if present.
        if (index < nalUnit.Length && nalUnit[index] == 0x01)
            index++;
        if (index >= nalUnit.Length)
            return false;

        var nalHeader = nalUnit[index];
        var nalType = nalHeader & 0x1F;
        // IDR slice = 5 in H.264.
        return nalType == 5;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _readerCts?.Cancel();
        }
        catch { }

        if (_stdin != null)
        {
            try
            {
                await _stdin.FlushAsync();
                _stdin.Dispose();
            }
            catch { }
        }

        if (_readerTask != null)
        {
            try
            {
                await _readerTask;
            }
            catch { }
        }

        if (_process != null)
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill(true);
            }
            catch { }
            finally
            {
                _process.Dispose();
            }
        }
    }
}

