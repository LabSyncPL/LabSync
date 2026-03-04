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
            FullMode = BoundedChannelFullMode.DropOldest
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
        // Input: raw BGRA frames via stdin.
        // Output: Annex B H.264 to stdout.
        var bitrate = options.TargetBitrateKbps > 0 ? options.TargetBitrateKbps : 2000;
        var fps = options.TargetFps > 0 ? options.TargetFps : 30;

        // -preset ultrafast (instead of veryfast) for maximum speed
        // -threads 0 to let ffmpeg use all cores
        // Add scaling filter to reduce resolution and improve encoding speed on weak hardware
        // Scale to width 1024, keeping aspect ratio (height=-2 ensures even height)
        return $"-f rawvideo -pix_fmt bgra -s {options.Width}x{options.Height} -r {fps} -i - " +
               $"-vf scale=1024:-2 " +
               $"-c:v libx264 -pix_fmt yuv420p -profile:v baseline -preset ultrafast -tune zerolatency -b:v {bitrate}k -g {fps} -keyint_min {fps} -sc_threshold 0 -threads 0 " +
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
        var buffer = new byte[1024 * 128]; // 128 KB bufora
        int bufferLen = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = await stdout.ReadAsync(buffer.AsMemory(bufferLen, buffer.Length - bufferLen), cancellationToken);
                if (read == 0) break;

                bufferLen += read;
                int i = 0;

                while (i < bufferLen - 3)
                {
                    // Wyszukiwanie sekwencji startowych Annex B
                    int startCodeLen = 0;
                    if (buffer[i] == 0 && buffer[i + 1] == 0 && buffer[i + 2] == 1)
                        startCodeLen = 3;
                    else if (buffer[i] == 0 && buffer[i + 1] == 0 && buffer[i + 2] == 0 && buffer[i + 3] == 1)
                        startCodeLen = 4;

                    if (startCodeLen > 0)
                    {
                        if (i > 0)
                        {
                            var nalUnit = new byte[i];
                            Buffer.BlockCopy(buffer, 0, nalUnit, 0, i);

                            var isKeyFrame = IsIdrNal(nalUnit);
                            var frame = new EncodedFrame(nalUnit, isKeyFrame, DateTime.UtcNow);
                            channel.Writer.TryWrite(frame);
                        }

                        // Usuwamy NAL i sekwencjê startow¹ z bufora, przesuwaj¹c resztê danych na pocz¹tek
                        int consume = i + startCodeLen;
                        bufferLen -= consume;
                        Buffer.BlockCopy(buffer, consume, buffer, 0, bufferLen);
                        i = 0; // Resetujemy indeks, szukamy dalej
                    }
                    else
                    {
                        i++;
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

