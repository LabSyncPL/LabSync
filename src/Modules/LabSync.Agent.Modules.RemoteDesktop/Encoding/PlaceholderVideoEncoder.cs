using System.Threading.Channels;
using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.Encoding;

public class PlaceholderVideoEncoder : IVideoEncoder
{
    private readonly ILogger _logger;
    private readonly int _channelCapacity;
    private Channel<EncodedFrame>? _channel;
    private EncoderOptions? _options;

    public bool HandlesCapture => false;

    public PlaceholderVideoEncoder(ILogger logger, int channelCapacity)
    {
        _logger = logger;
        _channelCapacity = channelCapacity > 0 ? channelCapacity : 2;
    }

    public Task InitializeAsync(EncoderOptions options, CancellationToken cancellationToken = default)
    {
        _options = options;
        _channel = Channel.CreateBounded<EncodedFrame>(new BoundedChannelOptions(_channelCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        return Task.CompletedTask;
    }

    public Task EncodeAsync(CaptureFrame frame, CancellationToken cancellationToken = default)
    {
        if (_channel == null)
            return Task.CompletedTask;
        if (!_channel.Writer.TryWrite(new EncodedFrame(Array.Empty<byte>(), false, DateTime.UtcNow)))
            _logger.LogTrace("Encoder channel full, frame dropped.");
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<EncodedFrame> GetEncodedStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_channel == null)
            yield break;
        await foreach (var frame in _channel.Reader.ReadAllAsync(cancellationToken))
            yield return frame;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task UpdateSettingsAsync(EncoderOptions options, CancellationToken cancellationToken = default)
    {
        _options = options;
        return Task.CompletedTask;
    }
}
