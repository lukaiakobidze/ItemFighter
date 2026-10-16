using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;

namespace ItemFighter.Tests;

/// <summary>Luka 2026-10-16: an enemy file lists its abilities and its passives separately.</summary>
public class EnemyPassivesListTests
{
    private static readonly (string, string)[] Files =
    [
        ("enemies/abilities/poke.json", """{ "id": "poke", "kind": "EnemyAbility", "cooldown": 2, "abilities": [ { "effects": [ { "type": "Damage", "amount": 1 } ] } ] }"""),
        ("passives/tough.json", """{ "id": "tough", "name": "Tough", "modifiers": [ { "stat": "DamageTaken", "amount": -1 } ] }"""),
    ];

    private static ContentDatabase Load(string enemyJson) =>
        TestContent.Load([.. Files, ("enemies/brute.json", enemyJson)]).Database;

    private static IReadOnlyList<ContentError> Errors(ContentDatabase db) => ContentValidator.Validate(db, new ScriptRegistry());

    [Fact]
    public void Enemy_GetsBothItsAbilitiesAndItsPassives()
    {
        var db = Load("""{ "id": "brute", "maxHp": 10, "abilityIds": ["poke"], "passiveIds": ["tough"] }""");
        Assert.Empty(Errors(db));
        var brute = db.Get("brute");
        Assert.Equal(["poke"], brute.AbilityIds);
        Assert.Equal(["tough"], brute.PassiveIds);
        var b = new Battle(new BattleSetup(new PlayerSetup(null, [], []), "brute", 1), db, new ScriptRegistry());
        Assert.Contains(b.AllSources, s => s.Def.Id == "tough" && s.Owner == Side.Enemy);
        Assert.Contains(b.AllSources, s => s.Def.Id == "poke" && s.Owner == Side.Enemy);
    }

    [Fact]
    public void PassiveInAbilityIds_OrAbilityInPassiveIds_IsAnError()
    {
        var errors = Errors(Load("""{ "id": "brute", "maxHp": 10, "abilityIds": ["tough"], "passiveIds": ["poke"] }"""));
        Assert.Contains(errors, e => e.Message.Contains("'tough'") && e.Message.Contains("passiveIds"));
        Assert.Contains(errors, e => e.Message.Contains("'poke'") && e.Message.Contains("abilityIds"));
    }

    [Fact]
    public void GameEnemies_ListPassivesSeparately()
    {
        var db = GameContent.Load().Database;
        foreach (var enemy in db.OfKind(SourceKind.Enemy))
        {
            Assert.All(enemy.AbilityIds, id => Assert.Equal(SourceKind.EnemyAbility, db.Get(id).Kind));
            Assert.All(enemy.PassiveIds, id => Assert.Equal(SourceKind.Passive, db.Get(id).Kind));
        }
        Assert.Contains("thick_skin", db.Get("grizzly_bear").PassiveIds);
    }
}
