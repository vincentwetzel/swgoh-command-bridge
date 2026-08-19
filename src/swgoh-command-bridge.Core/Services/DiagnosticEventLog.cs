#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Bounded, privacy-safe application event history retained in memory and on disk.
/// </summary>
public sealed class DiagnosticEventLog
{
    private const int MaximumEvents = 200;
    private static readonly Regex AllyCodePattern = new(
        @"(?<!\d)\d{9}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UrlPattern = new(
        @"https?://[^\s]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private readonly object _gate = new();
    private readonly Queue<DiagnosticEvent> _events = new();

    public string PersistentLogPath => Path.Combine(AppDataPaths.DiagnosticsDirectory, "application-events.log");

    public void Info(string operation, string message) => Add("INFO", operation, message);

    public void Warning(string operation, string message) => Add("WARN", operation, message);

    public void Error(string operation, string message) => Add("ERROR", operation, message);

    public void Record(string level, string operation, string message) => Add(level, operation, message);

    public IReadOnlyList<DiagnosticEvent> GetRecent(int maximum = 50)
    {
        var count = Math.Clamp(maximum, 1, MaximumEvents);
        lock (_gate)
        {
            return _events.TakeLast(count).ToList().AsReadOnly();
        }
    }

    public string FormatRecent(int maximum = 50)
    {
        var events = GetRecent(maximum);
        return events.Count == 0
            ? "No application events recorded in this session."
            : string.Join(Environment.NewLine, events.Select(entry => entry.ToString()));
    }

    private void Add(string level, string operation, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var safeMessage = AllyCodePattern.Replace(message, "[ally-code-redacted]");
        safeMessage = UrlPattern.Replace(safeMessage, "[url-redacted]");
        lock (_gate)
        {
            var entry = new DiagnosticEvent(DateTime.UtcNow, level, operation, safeMessage);
            _events.Enqueue(entry);
            while (_events.Count > MaximumEvents)
            {
                _events.Dequeue();
            }

            TryAppendToDisk(entry);
        }
    }

    private void TryAppendToDisk(DiagnosticEvent entry)
    {
        try
        {
            Directory.CreateDirectory(AppDataPaths.DiagnosticsDirectory);
            File.AppendAllText(
                PersistentLogPath,
                entry + Environment.NewLine,
                Encoding.UTF8);
        }
        catch
        {
            // Diagnostics must never interrupt the user flow if the filesystem is unavailable.
        }
    }
}

public sealed record DiagnosticEvent(
    DateTime OccurredAtUtc,
    string Level,
    string Operation,
    string Message)
{
    public override string ToString() =>
        $"{OccurredAtUtc:O} [{Level}] {Operation}: {Message}";
}
