using ItemFighter.Content;
using ItemFighter.Core.Content;

namespace ItemFighter.Tests;

public class DescribeTests
{
    private static readonly ContentDatabase Db = GameContent.Load().Database;

    private static string Text(string id, int level = 1) => string.Join("\n", SourceDescriber.Lines(Db.Get(id), level));

    [Fact]
    public void FireFlask_Level2() => Assert.Equal("Every 4s: apply 5 Burn to the enemy.", Text("fire_flask", 2));

    [Fact]
    public void Bellows() => Assert.Equal("When an adjacent Fire item fires: charge it 1s.", Text("bellows"));

    [Fact]
    public void Whetstone() => Assert.Equal("Click, then pick a Weapon: +5 Damage to it this fight.", Text("whetstone"));

    [Fact]
    public void GreatAxe_Damage() => Assert.Equal("Every 7s: deal 20 damage.", Text("great_axe"));

    [Fact]
    public void EnemyAbility_SpeaksToThePlayer() => Assert.Equal("When it takes damage: deal 1 damage to you.", Text("thorny_hide"));

    [Fact]
    public void Passive_Modifier() => Assert.Equal("Your items: +5 shield gained.", Text("iron_skin"));

    [Fact]
    public void DescriptionOverride_Wins_AndFillsLevelValues()
    {
        var db = TestContent.Db(("items/x.json", """
            { "id": "x", "cooldown": 1, "description": "Hits for {d}.\nThen smiles.", "levels": { "d": [1, 2, 3, 4] },
              "abilities": [ { "effects": [ { "type": "Damage", "amount": "{d}" } ] } ] }
            """));
        Assert.Equal(["Hits for 3.", "Then smiles."], SourceDescriber.Lines(db.Get("x"), 3));
    }

    [Fact]
    public void EveryContentItem_HasNonEmptyDescription()
    {
        foreach (var def in Db.All.Where(d => d.Kind is SourceKind.Item or SourceKind.Passive or SourceKind.EnemyAbility))
        {
            var lines = SourceDescriber.Lines(def, 1);
            Assert.True(lines.Count > 0 && lines.All(l => l.Length > 3 && !l.Contains("Trigger") && !l.Contains("Effect")),
                $"{def.Id}: {string.Join(" | ", lines)}");
        }
    }
}
