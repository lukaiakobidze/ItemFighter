using ItemFighter.Content.Scripts;
using ItemFighter.Core.Engine;
using static ItemFighter.Tests.BattleHelpers;

namespace ItemFighter.Tests;

public class ScriptTests
{
    [Fact]
    public void ScriptRegistry_DiscoversMirrorShard()
    {
        var scripts = ScriptRegistry.FromAssemblies(typeof(MirrorShard).Assembly);
        Assert.True(scripts.Contains("MirrorShard"));
        Assert.IsType<MirrorShard>(scripts.Create("MirrorShard"));
    }

    [Fact]
    public void ScriptRegistry_EachSourceGetsItsOwnInstance()
    {
        var scripts = ScriptRegistry.FromAssemblies(typeof(MirrorShard).Assembly);
        Assert.NotSame(scripts.Create("MirrorShard"), scripts.Create("MirrorShard"));
    }

    [Fact]
    public void MirrorShard_DoublesNextFire_Once()
    {
        var db = TestContent.Db(
            ("items/sword.json", Item("sword", 2, """{ "type": "Damage", "amount": 10 }""")),
            ("items/mirror.json", """{ "id": "mirror", "cooldown": 1, "activation": "Active", "requiresTarget": true, "script": "MirrorShard" }"""),
            ("enemies/dummy.json", Enemy(maxHp: 1000)));
        var b = new Battle(new BattleSetup(new PlayerSetup(null, [new BoardItem("sword", 0), new BoardItem("mirror", 1)], []), "dummy", 1),
            db, ScriptRegistry.FromAssemblies(typeof(MirrorShard).Assembly));
        b.Steps(10);
        var sword = b.Player.Items.Single(i => i.Slot == 0);
        b.Enqueue(new ActivateCommand(11, b.Player.Items.Single(i => i.Slot == 1).Id, sword.Id));
        b.Steps(10);
        Assert.Equal(980, b.Enemy.Hp);
        b.Steps(20);
        Assert.Equal(970, b.Enemy.Hp);
    }

    [Fact]
    public void Battle_WithUnknownScript_Throws()
    {
        var db = TestContent.Db(("items/x.json", """{ "id": "x", "cooldown": 1, "script": "Nope" }"""), ("enemies/dummy.json", Enemy()));
        Assert.Throws<ArgumentException>(() => Fight(db, "dummy", ("x", 0)));
    }
}
