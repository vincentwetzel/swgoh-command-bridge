#nullable enable

using Microsoft.Extensions.Logging;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class DiagnosticLoggerTests
{
    [Fact]
    public void Logger_ForwardsStructuredCoreMessagesWithPrivacyRedaction()
    {
        var eventLog = new DiagnosticEventLog();
        var logger = new DiagnosticLogger<DiagnosticLoggerTests>(eventLog);

        logger.LogInformation(
            "Requesting account {AllyCode} from {Endpoint}",
            "123456789",
            "https://localhost:3000/player");

        var message = eventLog.FormatRecent();

        Assert.Contains("DiagnosticLoggerTests", message);
        Assert.DoesNotContain("123456789", message);
        Assert.DoesNotContain("https://localhost:3000/player", message);
    }

    [Fact]
    public void Logger_IgnoresDebugNoise()
    {
        var eventLog = new DiagnosticEventLog();
        var logger = new DiagnosticLogger<DiagnosticLoggerTests>(eventLog);

        logger.LogDebug("Debug-only implementation detail.");

        Assert.Contains("No application events recorded", eventLog.FormatRecent());
    }
}
