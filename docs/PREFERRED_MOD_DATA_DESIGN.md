# Preferred Mod Data Design

## Purpose

This feature provides character-based mod guidance derived from a maintainer-owned sample of highly ranked Grand Arena Championship (GAC) accounts. The desktop application consumes an aggregated, versioned dataset; users do not query hundreds of accounts directly.

The first version is intentionally primary-stat driven, while retaining enough detail for later mod-farming recommendations.

## Decisions

- The local publisher selects a configurable number of top GAC accounts. It currently samples up to 250 unique accounts across Kyber divisions and does not expose this choice to desktop users.
- “Current” is the latest available GAC leaderboard snapshot at refresh time. Recency weighting across completed seasons is deferred until a history source is added.
- The sample is character-based, not squad-based.
- Recommendations include set patterns and slot-level primary distributions.
- A dominant primary is preferred, but close alternatives remain viable. A 55%/45% split therefore produces “Health preferred” and “Protection viable.”
- Missing slots receive advice just like incorrectly equipped slots.
- Insufficient data produces a low-confidence result rather than a fabricated universal recommendation.
- The user’s character priority (0–100) is a weighting factor for future farming recommendations, not a filter.
- The dataset is global and contains no ally codes or raw player profiles.

## Separation from existing recommendation data

The existing `RecommendationSnapshot` and `SwgohGgRecommendationEntity` represent account-scoped, locally scraped `swgoh.gg` data. They should remain unchanged for compatibility.

Preferred GAC data should use a separate global contract and repository:

- `PreferredModsDataset`: immutable loaded dataset plus metadata.
- `PreferredCharacterRecommendation`: one character’s preferred setups and slot distributions.
- `PreferredSetupPattern`: a commonly observed complete set pattern with its population share.
- `PreferredSlotRecommendation`: normalized slot/primary usage with counts, percentages, and confidence.
- `PreferredModQualityProfile`: optional quality distributions retained for farming analysis.

The assignment layer can later consume both sources through an explicit provider. It should not silently combine percentages from the two sources.

## Dataset contract

The published JSON should have a stable envelope similar to this:

```json
{
  "schemaVersion": 1,
  "datasetVersion": "2026-08-19.1",
  "generatedAtUtc": "2026-08-19T12:00:00Z",
  "source": {
    "gameMode": "GAC",
    "seasons": ["2026-season-18", "2026-season-17"],
    "accountCount": 250,
    "observationCount": 287
  },
  "characters": [
    {
      "characterId": "example-id",
      "sampleSize": 287,
      "confidence": "High",
      "setups": [
        {
          "share": 0.62,
          "sets": [
            { "set": "Speed", "count": 4 },
            { "set": "Health", "count": 2 }
          ],
          "slotPrimaries": {
            "Arrow": [{ "primary": "Speed", "share": 1.0 }],
            "Triangle": [
              { "primary": "HealthPercent", "share": 0.55 },
              { "primary": "ProtectionPercent", "share": 0.45 }
            ]
          }
        }
      ],
      "slotPrimaries": {
        "Triangle": [
          { "primary": "HealthPercent", "share": 0.55, "status": "Preferred" },
          { "primary": "ProtectionPercent", "share": 0.45, "status": "ViableAlternative" }
        ]
      },
      "qualityProfiles": []
    }
  ]
}
```

The actual contract should use numeric enums or stable canonical names for slots, sets, and primary stats. Display names belong in the application’s existing catalog/localization layer.

### Required fields

Each character should include:

- sample size and source metadata;
- complete setup patterns, including set counts and the associated slot primaries;
- aggregate slot-primary distributions for direct slot advice;
- confidence and recommendation status for each primary option.

### Future-ready fields

The updater should retain, or be able to regenerate, quality summaries keyed by set, slot, and primary:

- sample count;
- speed percentiles (at least median, upper quartile, and high percentile);
- secondary-stat presence/quality summaries;
- mod rarity/tier summaries where available.

These fields support future advice such as “farm Critical Damage triangles with better speed,” without changing the primary recommendation contract.

## Recommendation semantics

The publisher should calculate the statuses; the client should not infer certainty from a single percentage.

- `Preferred`: highest-supported option in a sufficiently strong distribution.
- `ViableAlternative`: close enough to the preferred option that changing an otherwise usable mod is not justified.
- `Inconclusive`: no clear winner or insufficient evidence.
- `NoData`: no usable observations for the character or slot.

The dominance gap, minimum sample sizes, recency weighting, and viable-alternative tolerance are publisher configuration. They must be recorded with the generated dataset so a future change is explainable.

## Farming recommendation model

Farming advice is a separate client-side analysis over the user’s cached mods, character priorities, and the preferred dataset. It should rank opportunities rather than emit one command.

The initial opportunity key should support three levels:

1. broad category, such as “Critical Damage triangles”;
2. set + slot + primary, such as “Critical Damage set, triangle, Critical Damage primary”;
3. set + slot + primary + quality target, such as “15+ speed.”

Each opportunity should carry separate components so the explanation remains understandable:

- `WeaknessScore`: how far the user’s mods trail their own baseline or the target quality profile;
- `DemandScore`: how many characters need the category;
- `CharacterPriorityScore`: the weighted priority of those characters;
- `GameplayImpactScore`: configured impact of the slot/set/primary;
- `FinalScore` and tier;
- a concise explanation and the affected characters.

Character priority is a multiplier, not a gate. A low-priority character can contribute, but ten high-priority characters with the same weakness should dominate the ranking.

## Offline cache and silent update flow

Add a dedicated application-data directory, separate from the SQLite account cache:

- `preferred-mods/embedded.json`: release-bundled baseline, or an embedded Core resource;
- `preferred-mods/current.json`: last known-good downloaded dataset;
- `preferred-mods/manifest.json`: last accepted manifest and validation metadata;
- `preferred-mods/*.tmp`: transient download files only.

At startup, after cached account data is available, a best-effort background update check runs without requiring the user's Comlink to be online. The app checks no more often than the configured interval, initially one week, except that an empty bootstrap baseline retries at subsequent startups until real data has been published.

Update sequence:

1. Load the downloaded dataset if valid; otherwise load the bundled baseline.
2. Fetch the small GitHub-hosted manifest using conditional HTTP requests when possible.
3. Ignore the result when the manifest is not newer or the schema is unsupported.
4. Download the dataset to a temporary file.
5. Validate size, JSON shape, schema, character IDs, slot coverage, percentages, and SHA-256.
6. Atomically replace `current.json` and the accepted manifest.
7. Keep the previous dataset when any step fails.

The update path must never delay opening cached account data, and update failures should be diagnostic-only unless no bundled or cached dataset is usable.

Compact UI metadata is sufficient: `Updated Aug 12 · 312 accounts`. Detailed source seasons, schema, and validation diagnostics belong in Diagnostics or an advanced view.

## GitHub distribution flow

The first implementation keeps all ComLink access on the maintainer's local PC:

- the maintainer runs the publisher against local ComLink, normally `http://localhost:3000`;
- leaderboard divisions, account target, and thresholds are local publisher configuration;
- the publisher writes a versioned aggregate dataset to `data/preferred-mods/`;
- the maintainer reviews, commits, and pushes those data files to GitHub;
- a small stable manifest points to the dataset and includes its SHA-256, schema version, generation time, and source summary.

The publisher must validate before publishing:

- all required character/slot records are structurally valid;
- percentages are within bounds and distributions are normalized;
- the sample is large enough for the configured claims;
- no raw ally codes or player payloads are present;
- unexpected large changes are reported and optionally require manual approval;
- the generated file can be consumed by the client contract tests.

GitHub is a distribution channel only. It does not query ComLink, store a ComLink URL/secret, or receive raw profiles. This is an operational constraint, not a reason to make desktop clients query the top accounts.

## Implementation sequence

1. Add Core contract records, parser/validator, embedded baseline loading, and file-backed cache.
2. Add the GitHub manifest client with atomic replacement and offline fallback.
3. Add tests for malformed manifests, checksum failures, unsupported schemas, partial downloads, and fallback behavior.
4. Add a local publisher project/script that consumes Comlink snapshots and emits the aggregate contract.
5. Add assignment/UI integration for character setup and slot-primary advice while retaining the existing `swgoh.gg` path.
6. Add farming-opportunity scoring and tiered presentation after the preferred-data path is stable.
