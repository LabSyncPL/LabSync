using System.Diagnostics;
using System.Threading.Channels;
using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.Encoding;

/// <summary>
/// Windows-only H.264 encoder using an external ffmpeg process.
/// Expects raw BGRA frames and produces Annex B H.264 NAL units.
/// Supports dynamic reconfiguration (resolution, bitrate, encoder type) without dropping the stream.
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
    private readonly SemaphoreSlim _reconfigureLock = new(1, 1);

    public WindowsFfmpegVideoEncoder(ILogger logger, int channelCapacity, string ffmpegPath = "ffmpeg")
    {
        _logger = logger;
        _channelCapacity = channelCapacity > 0 ? channelCapacity : 60;
        _ffmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? "ffmpeg" : ffmpegPath;
    }

    public async Task InitializeAsync(EncoderOptions options, CancellationToken cancellationToken = default)
    {
        _options = options;
        _channel = Channel.CreateBounded<EncodedFrame>(new BoundedChannelOptions(_channelCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        await StartFfmpegProcessAsync(cancellationToken);
    }

    public async Task UpdateSettingsAsync(EncoderOptions options, CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        await _reconfigureLock.WaitAsync(cancellationToken);
        try
        {
            if (_options == options) return;

            _logger.LogInformation("Reconfiguring encoder: {@Options}", options);
            
            // Stop current process
            await StopFfmpegProcessAsync();

            _options = options;

            // Start new process
            await StartFfmpegProcessAsync(cancellationToken);
        }
        finally
        {
            _reconfigureLock.Release();
        }
    }

    private async Task StartFfmpegProcessAsync(CancellationToken cancellationToken = default)
    {
        if (_options == null) return;

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = BuildFfmpegArguments(_options),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg process.");
            _stdin = _process.StandardInput.BaseStream;

            // Start reading stderr in background
            _ = ReadStdErrAsync(_process);

            _logger.LogInformation("ffmpeg process started. PID: {Pid}", _process.Id);

            _readerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); // Note: This token is just for the loop
            _readerTask = Task.Run(() => ReadEncodedOutputAsync(_process, _channel, _readerCts.Token), _readerCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ffmpeg.");
            throw;
        }

        await Task.CompletedTask;
    }

    private async Task StopFfmpegProcessAsync()
    {
        // Cancel the reader task
        _readerCts?.Cancel();
        
        if (_readerTask != null)
        {
            try { await _readerTask; } catch { }
        }

        if (_stdin != null)
        {
            try { await _stdin.DisposeAsync(); } catch { }
            _stdin = null;
        }

        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill(true);
                await _process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error killing ffmpeg process.");
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }
    }

    private string BuildFfmpegArguments(EncoderOptions options)
    {
        var bitrate = options.TargetBitrateKbps > 0 ? options.TargetBitrateKbps : 1500;
        var fps = options.TargetFps > 0 ? options.TargetFps : 30;
        
        // Input options
        var args = $"-f rawvideo -pix_fmt bgra -s {options.SourceWidth}x{options.SourceHeight} -r {fps} -i - ";

        // Scaling logic
        string scaleFilter = "";
        if (options.OutputWidth != options.SourceWidth || options.OutputHeight != options.SourceHeight)
        {
            // Use -2 to keep aspect ratio if one dimension is provided, or explicit size
            scaleFilter = $"-vf scale={options.OutputWidth}:{options.OutputHeight}";
        }

        // Encoder selection
        string encoderArgs = options.EncoderType switch
        {
            VideoEncoderType.NvidiaNvenc => $"-c:v h264_nvenc -preset p1 -rc:v cbr -b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k -g {fps} -zerolatency 1",
            VideoEncoderType.AmdAmf => $"-c:v h264_amf -usage ultra_low_latency -rc cbr -b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k -g {fps}",
            VideoEncoderType.IntelQsv => $"-c:v h264_qsv -preset veryfast -b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k -g {fps} -async_depth 1",
            _ => $"-c:v libx264 -pix_fmt yuv420p -profile:v baseline -preset fast -tune zerolatency -b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k -g {fps} -keyint_min {fps} -sc_threshold 0 -bf 0 -slices 1 -threads 0"
        };

        // Combine
        return $"{args} {scaleFilter} {encoderArgs} -f h264 -an -";
    }

    public async Task EncodeAsync(CaptureFrame frame, CancellationToken cancellationToken = default)
    {
        if (_disposed || _stdin == null || _options == null) return;

        try
        {
            await _stdin.WriteAsync(frame.Data, 0, frame.Data.Length, cancellationToken);
            await _stdin.FlushAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Ignore write errors (process might be restarting)
        }
    }

    public async IAsyncEnumerable<EncodedFrame> GetEncodedStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = _channel;
        if (channel == null) yield break;

        await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return frame;
        }
    }

    private async Task ReadEncodedOutputAsync(Process process, Channel<EncodedFrame>? channel, CancellationToken cancellationToken)
    {
        if (channel == null) return;

        var stdout = process.StandardOutput.BaseStream;
        var buffer = new byte[1024 * 1024 * 4];
        int bufferLen = 0;
        int searchIndex = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int availableSpace = buffer.Length - bufferLen;
                if (availableSpace == 0)
                {
                    _logger.LogWarning("FFmpeg buffer full. Resetting.");
                    bufferLen = 0;
                    searchIndex = 0;
                    availableSpace = buffer.Length;
                }

                int read = await stdout.ReadAsync(buffer.AsMemory(bufferLen, availableSpace), cancellationToken);
                if (read == 0) break;

                bufferLen += read;

                while (searchIndex < bufferLen - 3)
                {
                    int startCodeLen = 0;
                    if (buffer[searchIndex] == 0 && buffer[searchIndex + 1] == 0 && buffer[searchIndex + 2] == 1)
                        startCodeLen = 3;
                    else if (buffer[searchIndex] == 0 && buffer[searchIndex + 1] == 0 && buffer[searchIndex + 2] == 0 && buffer[searchIndex + 3] == 1)
                        startCodeLen = 4;

                    if (startCodeLen > 0)
                    {
                        if (searchIndex > 0)
                        {
                            var nalUnit = new byte[searchIndex];
                            Buffer.BlockCopy(buffer, 0, nalUnit, 0, searchIndex);

                            var isKeyFrame = IsIdrNal(nalUnit);
                            var frame = new EncodedFrame(nalUnit, isKeyFrame, DateTime.UtcNow);

                            await channel.Writer.WriteAsync(frame, cancellationToken);
                        }

                        int consume = searchIndex + startCodeLen;
                        bufferLen -= consume;
                        Buffer.BlockCopy(buffer, consume, buffer, 0, bufferLen);
                        searchIndex = 0;
                    }
                    else
                    {
                        searchIndex++;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogWarning(ex, "Error reading from FFmpeg."); }
        // IMPORTANT: Do NOT complete the channel writer here, because we might just be restarting the process!
        // The channel should only be completed in DisposeAsync.
    }

    private async Task ReadStdErrAsync(Process process)
    {
        try
        {
            var stderr = process.StandardError;
            while (true)
            {
                var line = await stderr.ReadLineAsync();
                if (line == null) break;
                // Only log warnings/errors to reduce noise, unless debugging
                if (line.Contains("error", StringComparison.OrdinalIgnoreCase) || line.Contains("warning", StringComparison.OrdinalIgnoreCase))
                    _logger.LogWarning("ffmpeg stderr: {Line}", line);
                else
                    _logger.LogTrace("ffmpeg stderr: {Line}", line);
            }
        }
        catch { }
    }

    private static bool IsIdrNal(byte[] nalUnit)
    {
        if (nalUnit == null || nalUnit.Length == 0) return false;
        var nalType = nalUnit[0] & 0x1F;
        return nalType == 5;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _reconfigureLock.WaitAsync();
        try
        {
            await StopFfmpegProcessAsync();
            _channel?.Writer.TryComplete();
        }
        finally
        {
            _reconfigureLock.Release();
            _reconfigureLock.Dispose();
        }
    }
}
