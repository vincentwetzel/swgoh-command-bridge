#nullable enable

using System;
using Microsoft.Extensions.Logging;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Bridges Core ILogger activity into the bounded, privacy-safe diagnostics event log.
/// </summary>
public sealed class DiagnosticLogger<T> : ILogger<T>
{
    private readonly DiagnosticEventLog _eventLog;

    public DiagnosticLogger(DiagnosticEventLog eventLog)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        _eventLog = eventLog;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
        NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (exception != null)
        {
            message = $"{message} ({exception.GetType().Name})";
        }

        _eventLog.Record(
            logLevel.ToString().ToUpperInvariant(),
            typeof(T).Name,
            message);
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
