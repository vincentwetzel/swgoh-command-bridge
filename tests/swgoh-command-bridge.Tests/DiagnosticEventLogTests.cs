#nullable enable

using System.Linq;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class DiagnosticEventLogTests
{
    [Fact]
    public void FormatRecent_RedactsAllyCodesAndPreservesSupportContext()
    {
        var log = new DiagnosticEventLog();

        log.Info("account-sync", "Sync started for ally code 123456789.");
        log.Warning("scraper", "Character page returned HTTP 404.");

        var text = log.FormatRecent();

        Assert.Contains("[ally-code-redacted]", text);
        Assert.DoesNotContain("123456789", text);
        Assert.Contains("HTTP 404", text);
    }

    [Fact]
    public void GetRecent_IsBoundedToTheMostRecentEvents()
    {
        var log = new DiagnosticEventLog();

        for (var index = 0; index < 250; index++)
        {
            log.Info("test", $"event-{index}");
        }

        var events = log.GetRecent(200);

        Assert.Equal(200, events.Count);
        Assert.Equal("event-50", events.First().Message);
        Assert.Equal("event-249", events.Last().Message);
    }
}
