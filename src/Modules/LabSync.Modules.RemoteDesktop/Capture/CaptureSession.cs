using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Collections.Generic;
using LabSync.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LabSync.Modules.RemoteDesktop.Configuration;

namespace LabSync.Modules.RemoteDesktop.Capture;

public class CaptureSession : ICaptureSession
{
    private readonly ILogger<CaptureSession> _logger;
    private readonly RemoteDesktopConfiguration _config;

    public CaptureSession(ILogger<CaptureSession> logger, IOptions<RemoteDesktopConfiguration> options)
    {
        _logger = logger;
        _config = options.Value;
    }

    public (Task CaptureTask, Task EncodeTask) Start(
        IScreenCaptureService? capture,
        IAsyncEnumerator<CaptureFrame>? enumerator,
        CaptureFrame? firstFrame,
        IVideoEncoder encoder,
        CancellationToken cancellationToken)
    {
        if (encoder.HandlesCapture)
        {
            return (Task.CompletedTask, Task.CompletedTask);
        }

        int capacity = _config.Capture.ChannelCapacity;
        if (capacity <= 0) capacity = 3;

        var captureChannel = Channel.CreateBounded<CaptureFrame>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        Task captureTask = Task.CompletedTask;
        if (capture != null && enumerator != null)
        {
            captureTask = RunCaptureLoopAsync(enumerator, firstFrame, captureChannel.Writer, cancellationToken);
        }
        
        Task encodeTask = RunEncodingLoopAsync(captureChannel.Reader, encoder, cancellationToken);

        return (captureTask, encodeTask);
    }

    private async Task RunCaptureLoopAsync(
        IAsyncEnumerator<CaptureFrame> enumerator,
        CaptureFrame? firstFrame,
        ChannelWriter<CaptureFrame> writer,
        CancellationToken cancellationToken)
    {
        int frameCount = 0;
        try
        {
            if (firstFrame != null)
            {
                frameCount++;
                await writer.WriteAsync(firstFrame, cancellationToken);
            }

            while (await enumerator.MoveNextAsync())
            {
                var frame = enumerator.Current;
                await writer.WriteAsync(frame, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) 
        { 
            _logger.LogWarning(ex, "Capture loop encountered an error.");
        }
        finally
        {
            await enumerator.DisposeAsync();
            writer.Complete();
        }
    }

    private async Task RunEncodingLoopAsync(
        ChannelReader<CaptureFrame> reader,
        IVideoEncoder encoder,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in reader.ReadAllAsync(cancellationToken))
            {
                await encoder.EncodeAsync(frame, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) 
        { 
            _logger.LogWarning(ex, "Encoding loop encountered an error.");
        }
    }
}
