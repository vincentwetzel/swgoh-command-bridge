#nullable enable

using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class CharacterCatalogParserTests
{
    [Fact]
    public void ParseWithAudit_UsesLocalizedNameAndThumbnailAndReportsCoverage()
    {
        var payload = new CharacterCatalogPayload(
            "{\"data\":[" +
            "{\"baseId\":\"BARRISSOFFEE\",\"nameKey\":\"UNIT_BARRISS_NAME\",\"thumbnailName\":\"tex.charui_barriss_light\",\"alignment\":\"Light Side\"}," +
            "{\"baseId\":\"CC2224\",\"nameKey\":\"UNIT_CODY_NAME\",\"thumbnailName\":\"charui_trooperclone_cody.png\",\"alignment\":\"Dark Side\"}]}",
            "{\"data\":{\"UNIT_BARRISS_NAME\":\"Barriss Offee\",\"UNIT_CODY_NAME\":\"Commander Cody\"}}");

        var result = new CharacterCatalogParser().ParseWithAudit(payload);

        Assert.Equal("Barriss Offee", result.Entries["BARRISSOFFEE"].Name);
        Assert.Equal("charui_barriss_light.png", result.Entries["BARRISSOFFEE"].PortraitAsset);
        Assert.Equal("Light Side", result.Entries["BARRISSOFFEE"].Alignment);
        Assert.Equal("charui_trooperclone_cody.png", result.Entries["CC2224"].PortraitAsset);
        Assert.Equal("Dark Side", result.Entries["CC2224"].Alignment);
        Assert.Equal(2, result.Audit.Entries);
        Assert.Equal(2, result.Audit.EntriesWithLocalizedNames);
        Assert.Equal(2, result.Audit.EntriesWithPortraits);
        Assert.Empty(result.Audit.MissingNameIds);
    }

    [Fact]
    public void ParseWithAudit_PrefersTheMostCompleteDuplicateUnitRecord()
    {
        var payload = new CharacterCatalogPayload(
            "{\"units\":[" +
            "{\"baseId\":\"CROSSHAIRS3\",\"nameKey\":\"CROSSHAIR_NAME\"}," +
            "{\"baseId\":\"CROSSHAIRS3\",\"nameKey\":\"CROSSHAIR_NAME\",\"thumbnailName\":\"tex.charui_crosshair_scarred\"}]}",
            "{\"CROSSHAIR_NAME\":\"Crosshair\"}");

        var result = new CharacterCatalogParser().ParseWithAudit(payload);

        Assert.Equal("Crosshair", result.Entries["CROSSHAIRS3"].Name);
        Assert.Equal("charui_crosshair_scarred.png", result.Entries["CROSSHAIRS3"].PortraitAsset);
        Assert.Equal(2, result.Audit.CandidateUnitRecords);
        Assert.Equal(1, result.Audit.DuplicateIds);
    }

    [Fact]
    public void ParseWithAudit_MergesAuthoritativeUnitRecordsFromAllSegments()
    {
        var payload = new CharacterCatalogPayload(
            [
                "{\"units\":[{\"baseId\":\"HOTHHAN\",\"nameKey\":\"HOTH_HAN\",\"thumbnailName\":\"tex.charui_hoth_han\"}]}",
                "{\"units\":[{\"baseId\":\"BARRISSOFFEE\",\"nameKey\":\"BARRISS\",\"thumbnailName\":\"tex.charui_barriss_light\"}]}",
                "{\"units\":[]}" 
            ],
            "{\"HOTH_HAN\":\"Hoth Han\",\"BARRISS\":\"Barriss Offee\"}");

        var result = new CharacterCatalogParser().ParseWithAudit(payload);

        Assert.Equal("Hoth Han", result.Entries["HOTHHAN"].Name);
        Assert.Equal("charui_hoth_han.png", result.Entries["HOTHHAN"].PortraitAsset);
        Assert.Equal("Barriss Offee", result.Entries["BARRISSOFFEE"].Name);
        Assert.Equal(2, result.Audit.Entries);
    }

}
