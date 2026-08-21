#nullable enable

namespace swgoh_command_bridge.Tests.Fixtures;

/// <summary>
/// Representative Comlink payloads used by the PlayerService mapping tests.
/// Keeping them named and reusable makes payload-shape coverage visible without
/// coupling the tests to large inline strings.
/// </summary>
internal static class ComlinkPayloadFixtures
{
    public const string EmptyRoster = """
        {
          "name": "Kenobi",
          "level": 85,
          "gp": 5100000,
          "rosterUnit": []
        }
        """;

    public const string InventoryMods = """
        {
          "name": "Ahsoka",
          "level": 85,
          "gp": 3000000,
          "rosterUnit": [
            {
              "definitionId": "AHSOKATANO:SEVEN_STAR",
              "currentRarity": 5,
              "currentLevel": 85,
              "currentGearLevel": 13
            }
          ],
          "mods": [
            {
              "id": "inventory_mod_1",
              "level": 15,
              "pips": 6,
              "tier": 5,
              "slot": 1,
              "set": 4,
              "primaryStat": { "stat": { "unitId": 1, "value": 100000000 } },
              "secondaryStat": [
                { "stat": { "unitId": 5, "value": 1500000000 }, "roll": 3 }
              ]
            }
          ]
        }
        """;

    public const string MalformedAndDuplicateRecords = """
        {
          "playerName": "Mixed Payload",
          "level": "85",
          "galacticPower": "1234567",
          "roster": [
            {
              "definitionId": "LUKE_SKYWALKER:SEVEN_STAR",
              "name": "Luke Skywalker",
              "currentLevel": "85",
              "currentGearLevel": "bad",
              "stars": 7,
              "equippedMods": [
                {
                  "id": "shared-mod",
                  "slot": "2",
                  "set": 4,
                  "primary": { "unitId": "5", "value": "3000000000" },
                  "secondaryStats": [
                    { "stat": { "unitId": 5, "value": 1500000000 }, "rollCount": "2" },
                    { "stat": { "unitId": "invalid", "value": "bad" } }
                  ]
                }
              ]
            },
            { "currentLevel": 85 }
          ],
          "inventory": {
            "mods": [
              { "id": "shared-mod", "slot": 2, "set": 4 },
              { "id": "inventory-mod", "slot": 1, "set": 1, "pips": "6" }
            ]
          }
        }
        """;

    public const string NestedMetadataAndMalformedEquippedMod = """
        {
          "units": [
            {
              "character": {
                "unitDefId": "REY:SEVEN_STAR",
                "displayName": "Rey"
              },
              "equippedStatMod": [
                { "slot": 1 },
                { "id": "rey-mod", "slot": 2, "set": 4 }
              ]
            }
          ]
        }
        """;

    public const string RosterForMetadataEnrichment = """
        {
          "rosterUnit": [
            { "definitionId": "REY:SEVEN_STAR" }
          ]
        }
        """;

    public const string MetadataCatalog = """
        {
          "unitDefinitions": [
            { "baseId": "REY", "localizedName": "Rey" }
          ]
        }
        """;

    public const string ValidRosterAndEquippedMod = """
        {
          "name": "Skywalker",
          "level": 85,
          "gp": 4200000,
          "rosterUnit": [
            {
              "definitionId": "DARTHTRAYA:SEVEN_STAR",
              "currentLevel": 85,
              "currentGearLevel": 12,
              "relic": { "currentTier": 5 },
              "gp": 24500,
              "equippedStatMod": [
                {
                  "id": "mod_speed_test",
                  "level": 15,
                  "pips": 6,
                  "tier": 5,
                  "slot": 2,
                  "set": 1,
                  "primaryStat": {
                    "stat": { "unitId": 5, "value": 3000000000 }
                  },
                  "secondaryStat": [
                    {
                      "stat": { "unitId": 5, "value": 1500000000 },
                      "roll": 2
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    public const string CanonicalComlinkGearTier = """
        {
          "name": "Canonical Tier Shape",
          "rosterUnit": [
            {
              "definitionId": "DARTHVADER:SEVEN_STAR",
              "currentLevel": 85,
              "currentRarity": 7,
              "currentTier": 13,
              "relic": { "currentTier": 8 }
            },
            {
              "definitionId": "PADME_AMIDALA:SEVEN_STAR",
              "currentLevel": 85,
              "currentRarity": 7,
              "currentTier": 7
            }
          ]
        }
        """;

    public const string RawRosterModShape = """
        {
          "name": "Raw Shape",
          "rosterUnit": [
            {
              "definitionId": "DARTHTRAYA:SEVEN_STAR",
              "equippedStatMod": [
                {
                  "id": "raw-mod",
                  "definitionId": "462",
                  "level": 15,
                  "tier": 5,
                  "primaryStat": {
                    "stat": { "unitStat": 5, "unscaledDecimalValue": 3200000000 }
                  },
                  "secondaryStat": [
                    {
                      "stat": { "unitStatId": 5, "unscaledDecimalValue": 1700000000 },
                      "roll": 3
                    },
                    {
                      "stat": { "unitStatId": 17, "unscaledDecimalValue": 500000000 }
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    public const string NestedEnvelopeWithUnequippedInventory = """
        {
          "data": {
            "player": {
              "playerName": "Envelope Player",
              "playerLevel": "85",
              "galacticPower": "2345678",
              "roster": [
                {
                  "baseId": "PADME_AMIDALA",
                  "displayName": "Padme Amidala",
                  "level": "85",
                  "gearLevel": "13",
                  "stars": "7",
                  "relicTier": "3",
                  "equippedMods": [
                    {
                      "modId": "envelope-equipped",
                      "slot": "2",
                      "setId": "4",
                      "level": "15",
                      "rarity": "6",
                      "tier": "5",
                      "primary": { "statId": "5", "amount": "3000000000" },
                      "secondaries": [
                        { "stat": { "type": "5", "amount": "1500000000" }, "rollCount": "2" }
                      ]
                    }
                  ]
                }
              ],
              "inventory": {
                "modInventory": [
                  {
                    "modId": "envelope-inventory",
                    "slot": "1",
                    "set": "1",
                    "level": "12",
                    "pips": "5",
                    "tier": "2",
                    "primaryStat": { "stat": { "unitId": "57", "value": "100000000" } }
                  }
                ]
              }
            }
          }
        }
        """;
}
