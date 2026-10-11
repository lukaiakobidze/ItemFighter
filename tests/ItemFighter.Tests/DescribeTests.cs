using ItemFighter.Content;
using ItemFighter.Core.Content;

namespace ItemFighter.Tests;

public class DescribeTests
{
    private static readonly ContentDatabase Db = GameContent.Load().Database;

    private static string Text(string id, int level = 1) => string.Join("\n", SourceDescriber.Lines(Db.Get(id), level));

    [Fact]
    public void Sword() => Assert.Equal("Every 4s: deal 5 damage.", Text("sword"));

    [Fact]
    public void MoltenDagger_TwoEffects() => Assert.Equal("Every 8s: apply 4 Burn to the enemy, then deal 4 damage.", Text("molten_dagger"));

    [Fact]
    public void Weights_Slow() => Assert.Equal("Click: add 2s to the cooldown of every enemy ability.", Text("weights"));

    [Fact]
    public void Apple_Charge() => Assert.Equal("Every 6s: heal 5, then charge a random other item 1s.", Text("apple"));

    [Fact]
    public void EnemyAbility_SpeaksToThePlayer() => Assert.Equal("When it deals damage: apply 2 Poison to you.", Text("venomous"));

    [Fact]
    public void EveryFifth() => Assert.Equal("Every 5th time it takes damage: apply 3 Burn to you.", Text("explosive"));

    [Fact]
    public void ThickSkin() => Assert.Equal("Takes 1 less damage from everything.", Text("thick_skin"));

    [Fact]
    public void SilverKnight() => Assert.Equal("Every 5th time you take damage: gain shield equal to 5% of max HP.", Text("silver_knight"));

    [Fact]
    public void TargetedActive_WithTagFilter()
    {
        var db = TestContent.Db(("items/hone.json", """
            { "id": "hone", "cooldown": 8, "activation": "Active", "requiresTarget": true, "targetFilter": "OwnItemTag:Weapon",
              "abilities": [ { "effects": [ { "type": "ModifyStat", "stat": "Damage", "amount": 5 } ] } ] }
            """));
        Assert.Equal(["Click, then pick a Weapon: +5 Damage to it this fight."], SourceDescriber.Lines(db.Get("hone"), 1));
    }

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
        foreach (var def in Db.All.Where(d => d.Kind is SourceKind.Item or SourceKind.Passive or SourceKind.EnemyAbility or SourceKind.Character))
        {
            var lines = SourceDescriber.Lines(def, 1);
            Assert.True(lines.Count > 0 && lines.All(l => l.Length > 3 && !l.Contains("Trigger") && !l.Contains("Effect")),
                $"{def.Id}: {string.Join(" | ", lines)}");
        }
    }
}
