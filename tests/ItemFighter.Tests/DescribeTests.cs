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
    public void EnemyAbility_SpeaksToThePlayer() => Assert.Equal("Every 3s: deal 6 damage to you.", Text("bow_shot"));

    // Passives come from the shared pool (passives/), so the same passive reads from the enemy's side or the player's.
    private static readonly ContentDatabase PassiveDb = TestContent.Db(
        ("passives/venomous.json", """{ "id": "venomous", "name": "Venomous", "abilities": [ { "trigger": "OnDamageDealt", "effects": [ { "type": "ApplyStatus", "status": "Poison", "amount": 2 } ] } ] }"""),
        ("passives/explosive.json", """{ "id": "explosive", "name": "Explosive", "abilities": [ { "trigger": { "type": "OnDamageTaken", "every": 5 }, "effects": [ { "type": "ApplyStatus", "status": "Burn", "amount": 3 } ] } ] }"""),
        ("passives/thick_skin.json", """{ "id": "thick_skin", "name": "Thick Skin", "modifiers": [ { "stat": "DamageTaken", "amount": -1 } ] }"""));

    private static string PassiveText(string id, bool enemy) => string.Join("\n", SourceDescriber.Lines(PassiveDb.Get(id), 1, ownedByEnemy: enemy));

    [Fact]
    public void Passive_OnAnEnemy_SpeaksToThePlayer() => Assert.Equal("When it deals damage: apply 2 Poison to you.", PassiveText("venomous", enemy: true));

    [Fact]
    public void Passive_OnThePlayer_SpeaksToThePlayer() => Assert.Equal("When you deal damage: apply 2 Poison to the enemy.", PassiveText("venomous", enemy: false));

    [Fact]
    public void EveryFifth() => Assert.Equal("Every 5th time it takes damage: apply 3 Burn to you.", PassiveText("explosive", enemy: true));

    [Fact]
    public void ThickSkin() => Assert.Equal("Takes 1 less damage from everything.", PassiveText("thick_skin", enemy: true));

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

    [Theory]
    [InlineData("shield_mastery", "Your Shield items: +5 shield gained.")]
    [InlineData("cold_hands", "Your Chill items: 1s faster cooldowns.")]
    [InlineData("vanguard", "Your Weapon item in slot 1: +1 damage dealt for each Weapon item on your board.")]
    [InlineData("parry", "When one of your Weapon items fires: gain 2 shield.")]
    [InlineData("frozen_roots", "For every 2 Nails you uproot: apply 1 Chill to the enemy.")]
    [InlineData("toxic_roots", "For each Nail you uproot: apply 1 Poison to the enemy.")]
    [InlineData("battle_hardened", "Win a fight: +5 max HP for the rest of the run.")]
    [InlineData("momentum", "When the item in slot 1 fires, if it is a Weapon: +1 Damage to it this fight.")]
    public void NewPassives_ReadPlainly(string id, string expected) => Assert.Equal(expected, Text(id));
}
