using Microsoft.Extensions.Logging;

namespace OrderConverterEXE;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly StreamWriter _writer;

    public FileLoggerProvider(string filePath)
    {
        _filePath = filePath;
        var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(fileStream, System.Text.Encoding.UTF8) { AutoFlush = true };
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(_writer);
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }

    private class FileLogger : ILogger
    {
        private readonly StreamWriter _writer;

        public FileLogger(StreamWriter writer)
        {
            _writer = writer;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var level = logLevel.ToString().ToUpper().PadRight(5);
            var logMessage = $"{timestamp} - {level} - {message}";

            lock (_writer)
            {
                _writer.WriteLine(logMessage);
            }
        }
    }
}
