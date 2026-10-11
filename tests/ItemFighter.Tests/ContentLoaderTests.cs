using System.Text.Json;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using ItemFighter.Core.Engine.Effects;
using ItemFighter.Core.Engine.Triggers;

namespace ItemFighter.Tests;

public class ContentLoaderTests
{
    private sealed class StubEffect : IEffect
    {
        public void Apply(EffectContext ctx) { }
    }

    private sealed class StubTrigger : ITrigger
    {
        public bool Matches(BattleEvent e, SourceInstance owner, Battle battle) => false;
    }

    private static (TriggerRegistry, EffectRegistry) Registries()
    {
        var t = new TriggerRegistry();
        t.Register("OnCooldown", _ => new StubTrigger());
        var e = new EffectRegistry();
        e.Register("ApplyStatus", ctx => { ctx.ReadValue("amount"); return new StubEffect(); });
        e.Register("Damage", ctx => { ctx.ReadValue("amount"); return new StubEffect(); });
        return (t, e);
    }

    private const string FireFlask = """
    {
      "id": "fire_flask",
      "name": "Fire Flask",
      "rarity": "Common",
      "tags": ["Fire", "Potion"],
      "pools": ["neutral"],
      "cooldown": 4.0,
      "activation": "Passive",
      "levels": { "burn": [3, 5, 8, 12] },
      "abilities": [
        { "trigger": "OnCooldown",
          "effects": [ { "type": "ApplyStatus", "status": "Burn", "amount": "{burn}", "target": "Enemy" } ] }
      ]
    }
    """;

    private static string Item(string id, string effectType = "Damage", string levels = "\"dmg\": [1,2,3,4]", string amount = "\"{dmg}\"", string extra = "") => $$"""
    { "id": "{{id}}", "cooldown": 1, "levels": { {{levels}} } {{extra}},
      "abilities": [ { "trigger": "OnCooldown", "effects": [ { "type": "{{effectType}}", "amount": {{amount}} } ] } ] }
    """;

    private static ContentLoadResult Load(params (string, string)[] files)
    {
        var (t, e) = Registries();
        return ContentLoader.LoadFromStrings(files, t, e);
    }

    [Fact]
    public void Loads_Item_FromSpecExample()
    {
        var result = Load(("items/fire_flask.json", FireFlask));
        Assert.Empty(result.Errors);
        var def = result.Database.Get("fire_flask");
        Assert.Equal("Fire Flask", def.Name);
        Assert.Equal(SourceKind.Item, def.Kind);
        Assert.Equal(40, def.CooldownTicks);
        Assert.Equal(new[] { 3, 5, 8, 12 }, def.Levels["burn"]);
        Assert.Single(def.Abilities);
        Assert.Equal(new[] { "Fire", "Potion" }, def.Tags);
    }

    [Fact]
    public void Cooldown_SecondsRoundToTicks_MinimumOne()
    {
        var result = Load(("items/a.json", Item("a").Replace("\"cooldown\": 1", "\"cooldown\": 0.04")),
                          ("items/b.json", Item("b").Replace("\"cooldown\": 1", "\"cooldown\": 2.25")));
        Assert.Empty(result.Errors);
        Assert.Equal(1, result.Database.Get("a").CooldownTicks);
        Assert.Equal(23, result.Database.Get("b").CooldownTicks);
    }

    [Fact]
    public void UnknownEffectType_ReportsError_AndContinues()
    {
        var result = Load(("items/bad.json", Item("bad", effectType: "Explode")), ("items/good.json", Item("good")));
        var error = Assert.Single(result.Errors);
        Assert.Equal("items/bad.json", error.File);
        Assert.Equal("bad", error.Id);
        Assert.Contains("unknown effect type 'Explode'", error.Message);
        Assert.True(result.Database.TryGet("good", out _));
        Assert.False(result.Database.TryGet("bad", out _));
    }

    [Fact]
    public void DuplicateId_Reported()
    {
        var result = Load(("items/a.json", Item("same")), ("items/b.json", Item("same")));
        var error = Assert.Single(result.Errors);
        Assert.Equal("items/b.json", error.File);
        Assert.Contains("duplicate id", error.Message);
    }

    [Fact]
    public void LevelArrayNotFour_Reported()
    {
        var result = Load(("items/a.json", Item("a", levels: "\"dmg\": [1,2,3]")));
        Assert.Contains("must have 4 entries", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void UnknownPlaceholder_Reported()
    {
        var result = Load(("items/a.json", Item("a", amount: "\"{dmgg}\"")));
        Assert.Contains("unknown level value '{dmgg}'", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void UnknownTriggerType_Reported()
    {
        var result = Load(("items/a.json", Item("a").Replace("\"OnCooldown\"", "\"OnSneeze\"")));
        Assert.Contains("unknown trigger type 'OnSneeze'", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void UnknownScript_Reported()
    {
        var result = Load(("items/a.json", Item("a", extra: ", \"script\": \"Nope\"")));
        Assert.Empty(result.Errors);
        var errors = ContentValidator.Validate(result.Database, new ScriptRegistry());
        Assert.Contains("unknown script 'Nope'", Assert.Single(errors).Message);
    }

    [Fact]
    public void UnknownAbilityIdOnEnemy_Reported()
    {
        var result = Load(("enemies/goblin.json", """{ "id": "goblin", "maxHp": 50, "abilityIds": ["stab", "missing"] }"""),
                          ("enemies/stab.json", """{ "id": "stab", "kind": "EnemyAbility", "cooldown": 2, "abilities": [ { "trigger": "OnCooldown", "effects": [ { "type": "Damage", "amount": 3 } ] } ] }"""));
        Assert.Empty(result.Errors);
        Assert.Equal(SourceKind.Enemy, result.Database.Get("goblin").Kind);
        var errors = ContentValidator.Validate(result.Database, new ScriptRegistry());
        Assert.Contains("unknown ability 'missing'", Assert.Single(errors).Message);
    }

    [Fact]
    public void MalformedJson_ReportedNotThrown()
    {
        var result = Load(("items/a.json", "{ \"id\": \"a\", "), ("items/b.json", Item("b")));
        var error = Assert.Single(result.Errors);
        Assert.Equal("items/a.json", error.File);
        Assert.True(result.Database.TryGet("b", out _));
    }

    [Fact]
    public void ArrayFile_LoadsEachDefinition()
    {
        var result = Load(("items/many.json", $"[{Item("a")}, {Item("b")}]"));
        Assert.Empty(result.Errors);
        Assert.Equal(new[] { "a", "b" }, result.Database.All.Select(d => d.Id));
    }

    [Fact]
    public void AllErrorsReportedAtOnce()
    {
        var result = Load(("items/a.json", Item("a", effectType: "Explode")), ("items/b.json", Item("b", levels: "\"dmg\": [1]")), ("items/c.json", "nope"));
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void LoadDirectory_ReadsFilesRecursively()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir.FullName, "items", "fire"));
            File.WriteAllText(Path.Combine(dir.FullName, "items", "fire", "flask.json"), FireFlask);
            var (t, e) = Registries();
            var result = ContentLoader.LoadDirectory(dir.FullName, t, e);
            Assert.Empty(result.Errors);
            Assert.Equal(SourceKind.Item, result.Database.Get("fire_flask").Kind);
        }
        finally { dir.Delete(true); }
    }
}
