namespace LabSync.Agent.Services;

public class SignalRLogForwarder(ServerClient serverClient) : ILogger
{
    private readonly Queue<(string level, string message)> _buffer = new();
    private readonly object _lock = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var level = logLevel switch
        {
            LogLevel.Information => "INFO",
            LogLevel.Warning     => "WARN",
            LogLevel.Error       => "ERROR",
            LogLevel.Critical    => "CRITICAL",
            _                    => logLevel.ToString().ToUpper()
        };

        var message = formatter(state, exception);
        if (exception != null)
            message += $" | {exception.Message}";

        if (!serverClient.IsConnected)
        {
            lock (_lock)
                if (_buffer.Count < 50)
                    _buffer.Enqueue((level, message));
            return;
        }

        _ = serverClient.PushLogAsync(level, message);
    }

    public (string level, string message)[] DrainBuffer()
    {
        lock (_lock)
        {
            var items = _buffer.ToArray();
            _buffer.Clear();
            return items;
        }
    }
}

public class SignalRLoggerProvider(ServerClient serverClient) : ILoggerProvider
{
    public readonly SignalRLogForwarder Forwarder = new(serverClient);
    public ILogger CreateLogger(string categoryName) => Forwarder;
    public void Dispose() { }
}