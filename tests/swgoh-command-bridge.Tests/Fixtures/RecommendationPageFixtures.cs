#nullable enable

namespace swgoh_command_bridge.Tests.Fixtures;

/// <summary>Reusable markup fixtures for recommendation section-boundary coverage.</summary>
internal static class RecommendationPageFixtures
{
    public const string MultipleSetsAndSlots = """
        <section class="recommendations">
          <img class="mod-set-image" alt="Speed Set">
          <span class="mod-set-percent">88%</span>
          <img alt="Health" data-set="Health Set" class="mod-set-image">
          <span class="mod-set-percent">55%</span>
          <div data-slot="2">
            <span class="mod-stat-name">Speed</span>
            <span class="mod-stat-percent">90%</span>
          </div>
          <div data-slot="4">
            <span class="mod-stat-name">Critical Damage</span>
            <span class="mod-stat-percent">76%</span>
          </div>
        </section>
        """;

    public const string NestedLocalizedSections = """
        <div class='mod-set-image' data-set='Potency Set'>
          <span class='mod-set-percent'>71,5%</span>
        </div>
        <article data-slot='5'>
          <div class='label'><span class='mod-stat-name'>Primaria</span></div>
          <div class='value'><strong class='mod-stat-percent'>64,25%</strong></div>
        </article>
        """;

    public const string FullPageVariation = """
        <!doctype html>
        <html lang="en">
          <body>
            <header><h1>Best Mods</h1><span class="mod-set-percent">not a recommendation</span></header>
            <section id="popular-sets">
              <img data-set='Speed Set' class='mod-set-image' alt='ignored label'>
              <span class='mod-set-percent'>88.0%</span>
              <div class="card"><img alt="Health Set" class="mod-set-image"><span class="mod-set-percent">55,5%</span></div>
              <div class="card"><img class="mod-set-image" alt="Speed Set"><span class="mod-set-percent">91%</span></div>
            </section>
            <section data-slot='2' class='primary-stat-card'>
              <div class='label'><span class='mod-stat-name'>Speed</span></div>
              <strong class='mod-stat-percent'>95%</strong>
            </section>
            <section>Slot 4
              <span data-label='primary' class='mod-stat-name'>Critical Damage</span>
              <span class='mod-stat-percent'>78,25%</span>
            </section>
            <footer><div class="mod-set-image">missing name</div></footer>
          </body>
        </html>
        """;
}
