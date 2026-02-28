using System.Threading.Channels;
using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.Encoding;

public class PlaceholderVideoEncoder : IVideoEncoder
{
    private readonly ILogger _logger;
    private Channel<EncodedFrame>? _channel;
    private EncoderOptions? _options;

    public PlaceholderVideoEncoder(ILogger logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(EncoderOptions options, CancellationToken cancellationToken = default)
    {
        _options = options;
        _channel = Channel.CreateUnbounded<EncodedFrame>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        return Task.CompletedTask;
    }

    public Task EncodeAsync(CaptureFrame frame, CancellationToken cancellationToken = default)
    {
        if (_channel == null)
            return Task.CompletedTask;
        _channel.Writer.TryWrite(new EncodedFrame(Array.Empty<byte>(), false, DateTime.UtcNow));
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
}
