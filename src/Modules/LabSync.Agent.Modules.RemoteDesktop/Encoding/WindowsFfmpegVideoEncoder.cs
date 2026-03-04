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

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = BuildFfmpegArguments(options),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true, // Capture stderr to debug crashes
            CreateNoWindow = true
        };

        try
        {
            _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg process.");
            _stdin = _process.StandardInput.BaseStream;
            
            // Start reading stderr in background
            _ = ReadStdErrAsync(_process);
            
            _logger.LogInformation("ffmpeg process started. PID: {Pid}", _process.Id);
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

    private async Task ReadStdErrAsync(Process process)
    {
        try
        {
            var stderr = process.StandardError;
            while (true)
            {
                var line = await stderr.ReadLineAsync();
                if (line == null) break;

                _logger.LogTrace("ffmpeg stderr: {Line}", line);
            }
        }
        catch { }
    }

    private static string BuildFfmpegArguments(EncoderOptions options)
    {
        var bitrate = options.TargetBitrateKbps > 0 ? options.TargetBitrateKbps : 1500;
        var fps = options.TargetFps > 0 ? options.TargetFps : 30;

        // USUNIÊTO -tune zerolatency!
        // DODANO -preset fast oraz bufsize równe 2-krotnoœci bitrate'u, co daje buforowanie.
        return $"-f rawvideo -pix_fmt bgra -s {options.Width}x{options.Height} -r {fps} -i - " +
               $"-vf scale=1024:-2 " +
               $"-c:v libx264 -pix_fmt yuv420p -profile:v baseline -preset fast -b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k -g {fps} -keyint_min {fps} -sc_threshold 0 -bf 0 -slices 1 -threads 0 " +
               "-f h264 -an -";
    }

    public async Task EncodeAsync(CaptureFrame frame, CancellationToken cancellationToken = default)
    {
        if (_disposed || _stdin == null || _options == null)
            return;

        try
        {
            // Assume frame.Format is BGRA32 and matches ffmpeg -pix_fmt bgra.
            // _logger.LogTrace("Writing {Bytes} bytes to ffmpeg stdin.", frame.Data.Length);
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
                    _logger.LogWarning("Bufor FFmpeg jest pe³ny! Resetowanie.");
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
        catch (Exception ex) { _logger.LogWarning(ex, "B³¹d podczas odczytu z FFmpeg."); }
        finally { channel.Writer.TryComplete(); }
    }

    private static bool IsIdrNal(byte[] nalUnit)
    {
        // Check NAL unit type from the first byte (since start codes are stripped)
        if (nalUnit == null || nalUnit.Length == 0) return false;
        
        var nalHeader = nalUnit[0];
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

