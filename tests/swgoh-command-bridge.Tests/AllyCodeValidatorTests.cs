#nullable enable

using System;
using swgoh_command_bridge.Core.Models;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class AllyCodeValidatorTests
{
    [Fact]
    public void TryNormalize_TrimsValidNineDigitCode()
    {
        var valid = AllyCodeValidator.TryNormalize(
            " 123456789 ",
            out var normalized,
            out var errorMessage);

        Assert.True(valid);
        Assert.Equal("123456789", normalized);
        Assert.Empty(errorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("12345678A")]
    public void TryNormalize_RejectsMalformedCode(string value)
    {
        var valid = AllyCodeValidator.TryNormalize(value, out var normalized, out var errorMessage);

        Assert.False(valid);
        Assert.Empty(normalized);
        Assert.Contains("nine-digit", errorMessage);
    }

    [Fact]
    public void NormalizeOrThrow_RejectsMalformedCode()
    {
        Assert.Throws<ArgumentException>(() => AllyCodeValidator.NormalizeOrThrow("invalid"));
    }
}
