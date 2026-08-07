#nullable enable

using System;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class CharacterMetadataParserTests
{
    [Fact]
    public void Parse_ExtractsNestedAndLocalizedCharacterNames()
    {
        var json = """
                   {
                     "metadata": {
                       "unitDefinitions": [
                         { "BASEID": "REY:SEVEN_STAR", "localizedName": "Rey" },
                         { "characterId": "KYLO_REN", "displayName": "Kylo Ren" }
                       ]
                     }
                   }
                   """;

        var names = new CharacterMetadataParser().Parse(json);

        Assert.Equal("Rey", names["REY"]);
        Assert.Equal("Kylo Ren", names["KYLO_REN"]);
    }

    [Fact]
    public void Parse_RejectsMalformedJsonAtTheMetadataBoundary()
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => new CharacterMetadataParser().Parse("{not-json"));
    }
}
