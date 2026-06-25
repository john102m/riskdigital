namespace Risk.Server.Services;

public class RingBufferLogger : ILoggerProvider, ILogger
{
    private readonly Queue<string> _buffer = new();
    private const int MaxLines = 300;
    private readonly object _lock = new();

    public IReadOnlyList<string> GetLines()
    {
        lock (_lock) { return _buffer.ToList(); }
    }

    public ILogger CreateLogger(string categoryName) => this;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var line = $"{DateTime.Now:HH:mm:ss} [{logLevel.ToString()[..3]}] {formatter(state, exception)}";
        lock (_lock)
        {
            _buffer.Enqueue(line);
            while (_buffer.Count > MaxLines) _buffer.Dequeue();
        }
    }

    public void Dispose() { }
}
