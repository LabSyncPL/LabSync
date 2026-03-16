using System.Diagnostics;
using System.Threading.Channels;
using LabSync.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.RemoteDesktop.Encoding;

public abstract class BaseFfmpegEncoder : IVideoEncoder
{
    protected readonly ILogger Logger;
    protected readonly string FfmpegPath;
    protected readonly int ChannelCapacity;

    protected Channel<EncodedFrame>? Channel;
    protected EncoderOptions? Options;
    protected Process? Process;
    protected Stream? Stdin;
    protected CancellationTokenSource? ReaderCts;
    protected Task? ReaderTask;
    protected bool Disposed;
    protected readonly SemaphoreSlim ReconfigureLock = new(1, 1);

    public abstract bool HandlesCapture { get; }

    protected BaseFfmpegEncoder(ILogger logger, int channelCapacity, string ffmpegPath = "ffmpeg")
    {
        Logger = logger;
        ChannelCapacity = channelCapacity > 0 ? channelCapacity : 60;
        FfmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? "ffmpeg" : ffmpegPath;
    }

    public virtual async Task InitializeAsync(EncoderOptions options, CancellationToken cancellationToken = default)
    {
        Options = options;
        Channel = System.Threading.Channels.Channel.CreateBounded<EncodedFrame>(new BoundedChannelOptions(ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        await StartFfmpegProcessAsync(cancellationToken);
    }

    public virtual async Task UpdateSettingsAsync(EncoderOptions options, CancellationToken cancellationToken = default)
    {
        if (Disposed) return;

        await ReconfigureLock.WaitAsync(cancellationToken);
        try
        {
            if (Options == options) return;

            Logger.LogInformation("Reconfiguring encoder: {@Options}", options);
            
            await StopFfmpegProcessAsync();
            Options = options;
            await StartFfmpegProcessAsync(cancellationToken);
        }
        finally
        {
            ReconfigureLock.Release();
        }
    }

    protected abstract string BuildFfmpegArguments(EncoderOptions options);

    protected virtual async Task StartFfmpegProcessAsync(CancellationToken cancellationToken = default)
    {
        if (Options == null) return;

        var psi = new ProcessStartInfo
        {
            FileName = FfmpegPath,
            Arguments = BuildFfmpegArguments(Options),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            Process = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg process.");
            if (!HandlesCapture)
            {
                Stdin = Process.StandardInput.BaseStream;
            }

            _ = ReadStdErrAsync(Process);

            Logger.LogInformation("ffmpeg process started. PID: {Pid}", Process.Id);

            ReaderCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); 
            ReaderTask = Task.Run(() => ReadEncodedOutputAsync(Process, Channel, ReaderCts.Token), ReaderCts.Token);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start ffmpeg.");
            throw;
        }

        await Task.CompletedTask;
    }

    protected virtual async Task StopFfmpegProcessAsync()
    {
        ReaderCts?.Cancel();
        
        if (ReaderTask != null)
        {
            try { await ReaderTask; } catch { }
        }

        if (Stdin != null)
        {
            try { await Stdin.DisposeAsync(); } catch { }
            Stdin = null;
        }

        if (Process != null && !Process.HasExited)
        {
            try
            {
                Process.Kill(true);
                await Process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error killing ffmpeg process.");
            }
            finally
            {
                Process.Dispose();
                Process = null;
            }
        }
    }

    public virtual async Task EncodeAsync(CaptureFrame frame, CancellationToken cancellationToken = default)
    {
        if (Disposed || Stdin == null || Options == null) return;

        try
        {
            await Stdin.WriteAsync(frame.Data, 0, frame.Data.Length, cancellationToken);
            await Stdin.FlushAsync(cancellationToken);
        }
        catch (Exception)
        {
            
        }
    }

    public async IAsyncEnumerable<EncodedFrame> GetEncodedStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel;
        if (channel == null) yield break;

        await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return frame;
        }
    }

    protected async Task ReadEncodedOutputAsync(Process process, Channel<EncodedFrame>? channel, CancellationToken cancellationToken)
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
                // Check if process has exited to avoid reading from a closed stream indefinitely or blocking?
                // stdout.ReadAsync should return 0 on exit.

                int availableSpace = buffer.Length - bufferLen;
                if (availableSpace == 0)
                {
                    Logger.LogWarning("FFmpeg buffer full. Resetting.");
                    bufferLen = 0;
                    searchIndex = 0;
                    availableSpace = buffer.Length;
                }

                int read = await stdout.ReadAsync(buffer.AsMemory(bufferLen, availableSpace), cancellationToken);
                if (read == 0) 
                {
                    // EOF reached. Check if process exited with error.
                    if (process.HasExited && process.ExitCode != 0)
                    {
                        Logger.LogError("FFmpeg process exited unexpectedly with code {ExitCode}.", process.ExitCode);
                    }
                    else if (process.HasExited)
                    {
                        Logger.LogInformation("FFmpeg process exited normally.");
                    }
                    break; 
                }

                bufferLen += read;

                // Process buffer to find NAL units (separated by 00 00 01 or 00 00 00 01)
                while (searchIndex < bufferLen - 3)
                {
                    int startCodeLen = 0;
                    // Check for 00 00 01
                    if (buffer[searchIndex] == 0 && buffer[searchIndex + 1] == 0 && buffer[searchIndex + 2] == 1)
                        startCodeLen = 3;
                    // Check for 00 00 00 01
                    else if (buffer[searchIndex] == 0 && buffer[searchIndex + 1] == 0 && buffer[searchIndex + 2] == 0 && buffer[searchIndex + 3] == 1)
                        startCodeLen = 4;

                    if (startCodeLen > 0)
                    {
                        // Found a start code.
                        // If searchIndex > 0, the bytes before it are a NAL unit (or partial data).
                        if (searchIndex > 0)
                        {
                            var nalUnit = new byte[searchIndex];
                            Buffer.BlockCopy(buffer, 0, nalUnit, 0, searchIndex);

                            var nalType = nalUnit[0] & 0x1F;
                            
                            if (nalType == 7) Logger.LogDebug("Sending SPS NAL ({Size} bytes)", nalUnit.Length);
                            else if (nalType == 8) Logger.LogDebug("Sending PPS NAL ({Size} bytes)", nalUnit.Length);
                            else if (nalType == 5) Logger.LogDebug("Sending IDR NAL ({Size} bytes)", nalUnit.Length);

                            var isKeyFrame = nalType == 5;
                            var frame = new EncodedFrame(nalUnit, isKeyFrame, DateTime.UtcNow);

                            await channel.Writer.WriteAsync(frame, cancellationToken);
                        }

                        // Shift buffer: remove the processed part AND the start code
                        int consume = searchIndex + startCodeLen;
                        bufferLen -= consume;
                        
                        if (bufferLen > 0)
                        {
                            Buffer.BlockCopy(buffer, consume, buffer, 0, bufferLen);
                        }
                        
                        searchIndex = 0; // Reset search to beginning of new buffer content
                    }
                    else
                    {
                        searchIndex++;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error reading from FFmpeg.");
        }
    }
    
    protected async Task ReadStdErrAsync(Process process)
    {
        try
        {
            var stderr = process.StandardError;
            while (true)
            {
                var line = await stderr.ReadLineAsync();
                if (line == null) break;
                
                if (line.Contains("error", StringComparison.OrdinalIgnoreCase) || 
                    line.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("panic", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("ffmpeg stderr: {Line}", line);
                }
                else
                {
                    Logger.LogDebug("ffmpeg stderr: {Line}", line);
                }
            }
        }
        catch { }
    }

    protected static bool IsIdrNal(byte[] nalUnit)
    {
        if (nalUnit == null || nalUnit.Length == 0) return false;
        var nalType = nalUnit[0] & 0x1F;
        return nalType == 5;
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (Disposed) return;
        Disposed = true;

        await ReconfigureLock.WaitAsync();
        try
        {
            await StopFfmpegProcessAsync();
            Channel?.Writer.TryComplete();
        }
        finally
        {
            ReconfigureLock.Release();
            ReconfigureLock.Dispose();
        }
        
        GC.SuppressFinalize(this);
    }
}
