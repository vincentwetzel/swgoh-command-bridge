#nullable enable

using System;
using swgoh_command_bridge.Core.Models;
using Xunit;

namespace swgoh_command_bridge.Tests;

public class ScrapeRetryPolicyTests
{
    [Fact]
    public void GetBackoff_UsesCappedExponentialSchedule()
    {
        var policy = new ScrapeRetryPolicy(
            maxAttempts: 4,
            initialBackoff: TimeSpan.FromSeconds(2),
            maximumBackoff: TimeSpan.FromSeconds(5),
            interRequestDelay: TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromSeconds(2), policy.GetBackoff(1));
        Assert.Equal(TimeSpan.FromSeconds(4), policy.GetBackoff(2));
        Assert.Equal(TimeSpan.FromSeconds(5), policy.GetBackoff(3));
    }

    [Fact]
    public void GetDelay_HonorsServerDelayWithinConfiguredCap()
    {
        var policy = new ScrapeRetryPolicy(
            initialBackoff: TimeSpan.FromSeconds(2),
            maximumBackoff: TimeSpan.FromSeconds(10),
            interRequestDelay: TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromSeconds(7), policy.GetDelay(1, TimeSpan.FromSeconds(7)));
        Assert.Equal(TimeSpan.FromSeconds(10), policy.GetDelay(1, TimeSpan.FromSeconds(20)));
        Assert.Equal(TimeSpan.FromSeconds(2), policy.GetDelay(1, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_RejectsInvalidTiming()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScrapeRetryPolicy(maxAttempts: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScrapeRetryPolicy(initialBackoff: TimeSpan.FromMilliseconds(-1)));
        Assert.Throws<ArgumentException>(() => new ScrapeRetryPolicy(
            initialBackoff: TimeSpan.FromSeconds(3),
            maximumBackoff: TimeSpan.FromSeconds(2)));
    }
}
