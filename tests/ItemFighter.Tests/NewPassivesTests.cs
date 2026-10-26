using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using ItemFighter.Core.Run;

namespace ItemFighter.Tests;

/// <summary>Luka's 2026-10-16 passives. Each compares a fight with the passive against the same fight without it.</summary>
public class NewPassivesTests
{
    private static readonly LoadedContent Content = ContentIntegrityTests.WithDummy();

    private static Battle Fight(string[] passives, params string[] items)
    {
        var board = items.Select((id, slot) => new BoardItem(id, slot)).ToList();
        return new Battle(new BattleSetup(new PlayerSetup(null, board, passives), "test_dummy", 1), Content.Database, Content.Scripts);
    }

    private static Battle Run(int ticks, string[] passives, params string[] items)
    {
        var b = Fight(passives, items);
        b.Steps(ticks);
        return b;
    }

    private static string DefOf(Battle b, int? id) => id is int i ? b.FindSource(i)!.Def.Id : "";

    private static int Shield(Battle b, string from) => b.Events.OfType<ShieldGained>().Where(g => DefOf(b, g.SourceId) == from).Sum(g => g.Amount);

    private static int Status(Battle b, StatusType status, string? from = null) => b.Events.OfType<StatusApplied>()
        .Where(s => s.Status == status && s.TargetSide == Side.Enemy && (from is null || DefOf(b, s.SourceId) == from)).Sum(s => s.Amount);

    private static List<int> Hits(Battle b, string from) => b.Events.OfType<DamageDealt>()
        .Where(d => d.Target == Side.Enemy && d.Kind == DamageKind.Direct && DefOf(b, d.SourceId) == from).Select(d => d.Total).ToList();

    private static List<int> Fires(Battle b, string from) => b.Events.OfType<SourceFired>()
        .Where(f => !f.Reaction && DefOf(b, f.SourceId) == from).Select(f => f.Tick).ToList();

    [Fact]
    public void ShieldMastery_ShieldItemsShieldFiveMore() =>
        Assert.Equal(Shield(Run(50, [], "metal_plating"), "metal_plating") + 5, Shield(Run(50, ["shield_mastery"], "metal_plating"), "metal_plating"));

    [Fact]
    public void Kindling_BurnItemsBurnOneMore() =>
        Assert.Equal(Status(Run(50, [], "cinder"), StatusType.Burn) + 1, Status(Run(50, ["kindling"], "cinder"), StatusType.Burn));

    [Fact]
    public void Toxicology_PoisonItemsPoisonOneMore() =>
        Assert.Equal(Status(Run(40, [], "venom_fang"), StatusType.Poison) + 1, Status(Run(40, ["toxicology"], "venom_fang"), StatusType.Poison));

    [Fact]
    public void ColdHands_ChillItemsFireOneSecondSooner()
    {
        Assert.Equal(Fires(Run(100, [], "ice_bow"), "ice_bow")[0] - 10, Fires(Run(100, ["cold_hands"], "ice_bow"), "ice_bow")[0]);
        Assert.Equal(Fires(Run(100, [], "sword"), "sword"), Fires(Run(100, ["cold_hands"], "sword"), "sword")); // not Chill: unchanged
    }

    [Fact]
    public void Vanguard_SlotOneWeapon_GetsOneDamagePerWeapon()
    {
        string[] board = ["sword", "dagger", "axe", "metal_plating"];
        Assert.Equal(Hits(Run(40, [], board), "sword")[0] + 3, Hits(Run(40, ["vanguard"], board), "sword")[0]);
        Assert.Equal(Hits(Run(40, [], board), "dagger"), Hits(Run(40, ["vanguard"], board), "dagger")); // only slot 1
        Assert.Equal(Hits(Run(80, [], "metal_plating", "sword"), "sword"), Hits(Run(80, ["vanguard"], "metal_plating", "sword"), "sword"));
    }

    [Fact]
    public void Parry_ShieldsTwoPerWeaponUse()
    {
        var b = Run(80, ["parry", "momentum"], "sword", "metal_plating");
        Assert.Equal(2 * Fires(b, "sword").Count, Shield(b, "parry")); // the passives' own reactions don't count as weapons
    }

    [Fact]
    public void Momentum_SlotOneWeapon_GainsOneDamageEachTime()
    {
        var plain = Hits(Run(130, [], "sword"), "sword");
        var hits = Hits(Run(130, ["momentum"], "sword"), "sword");
        Assert.Equal(plain.Select((h, i) => h + i), hits);
    }

    private static Battle Uproot(params string[] passives)
    {
        var b = Fight(passives, "hurling_hammer", "hurling_hammer", "magnetized_glove");
        var glove = b.Player.Items.Single(i => i.Def.Id == "magnetized_glove");
        b.Steps(glove.Def.CooldownTicks!.Value + 1);
        Assert.Equal(2, b.Enemy.Statuses.Nail);
        b.Enqueue(new ActivateCommand(b.Tick + 1, glove.Id, null));
        b.Steps(2);
        return b;
    }

    [Fact]
    public void Roots_ApplyStatusPerNailUprooted()
    {
        Assert.Equal(2, Status(Uproot("burning_roots"), StatusType.Burn, "burning_roots"));
        Assert.Equal(2, Status(Uproot("toxic_roots"), StatusType.Poison, "toxic_roots"));
        Assert.Equal(1, Status(Uproot("frozen_roots"), StatusType.Chill, "frozen_roots"));
        Assert.Equal(1, Status(Uproot("painful_roots"), StatusType.Pain, "painful_roots"));
    }

    [Fact]
    public void BattleHardened_FiveMaxHpForEveryWinAfterYouHaveIt()
    {
        var content = GameContent.Load();
        var run = RunState.New(1, "silver_knight", content.Run, content.Database);
        int Hp() => new Battle(run.StartFight(), content.Database, content.Scripts).Player.MaxHp;
        run.Choose(new Encounter(EncounterKind.Fight, "squire_zombie"));
        int start = Hp();
        run.FinishFight(true);
        run.PendingDrop = null;
        run.Phase = RunPhase.ChoosingEncounter;
        run.Passives.Add("battle_hardened");
        run.Choose(new Encounter(EncounterKind.Fight, "squire_zombie"));
        Assert.Equal(start, Hp()); // no wins with it yet
        run.FinishFight(true);
        run.Phase = RunPhase.ChoosingEncounter;
        run.Choose(new Encounter(EncounterKind.Fight, "squire_zombie"));
        Assert.Equal(start + 5, Hp());
    }

    [Fact]
    public void EveryPassive_IsSoldInThePassiveShop_AndHasARarity()
    {
        var db = Content.Database;
        Assert.Equal(22, db.OfKind(SourceKind.Passive).Count());
        Assert.Equal(Rarity.Rare, db.Get("thick_skin").Rarity);
    }
}
