using ItemFighter.Core.Engine;
using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.FightView;

public sealed record LogEntry(int Tick, string Text, Color Color);

/// <summary>The fight in plain sentences, newest last.</summary>
public sealed class CombatLog
{
    public const int Capacity = 200;
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

    public void Consume(IEnumerable<BattleEvent> events, Battle battle)
    {
        foreach (var e in events)
        {
            var line = Describe(e, battle);
            if (line is null) continue;
            _entries.Add(new LogEntry(e.Tick, line.Value.Text, line.Value.Color));
        }
        if (_entries.Count > Capacity) _entries.RemoveRange(0, _entries.Count - Capacity);
    }

    private static (string Text, Color Color)? Describe(BattleEvent e, Battle b)
    {
        string Name(int? id) => id is int i && b.FindSource(i) is { } s ? s.Def.Name : "Something";
        string Side(Side side) => side == Core.Engine.Side.Player ? "you" : b.EnemyDefinition.Name;

        return e switch
        {
            DamageDealt { Kind: DamageKind.Direct } d => ($"{Name(d.SourceId)} hits {Side(d.Target)} for {d.Total}" + Absorbed(d), Palette.For(d.Kind)),
            DamageDealt d => ($"{d.Kind} deals {d.Total} to {Side(d.Target)}" + Absorbed(d), Palette.For(d.Kind)),
            ShieldGained g => ($"{Name(g.SourceId)}: +{g.Amount} shield" + (g.Target == Core.Engine.Side.Enemy && b.FindSource(g.SourceId ?? 0)?.Owner != Core.Engine.Side.Enemy ? $" for {Side(g.Target)}" : ""), Palette.Shield),
            Healed h => ($"{Name(h.SourceId)}: {(h.Target == Core.Engine.Side.Player ? "you heal" : Side(h.Target) + " heals")} {h.Amount}", Palette.Heal),
            StatusApplied { TargetSide: Side side, Status: StatusType.Vulnerable } s => ($"{Name(s.SourceId)}: {Side(side)} {(side == Core.Engine.Side.Player ? "are" : "is")} Vulnerable for {Words(s.Amount)}", Palette.Vulnerable),
            StatusApplied { TargetSide: Side side } s => ($"{Name(s.SourceId)}: {s.Amount} {s.Status} on {Side(side)}", Palette.For(s.Status)),
            StatusApplied { TargetSourceId: int id } s => ($"{Name(s.SourceId)}: {s.Status} on {Name(id)} for {Words(s.Amount)}", Palette.For(s.Status)),
            StatusRemoved r => ($"{Name(r.SourceId)}: removes {r.Amount} {r.Status} from {Side(r.Target)}", Palette.For(r.Status)),
            NailsUprooted n => ($"{Name(n.SourceId)} uproots {n.Nails} Nail{(n.Nails == 1 ? "" : "s")} from {Side(n.Target)}!", Palette.Nail),
            PoisonBurst p => ($"Poison bursts on {Side(p.Target)} for {p.Damage}!", Palette.Poison),
            ChillFroze c => ($"{Capital(Side(c.Target))} {(c.Target == Core.Engine.Side.Player ? "are" : "is")} frozen solid!", Palette.Freeze),
            Charged c => ($"{Name(c.SourceId)} charges {Name(c.TargetSourceId)} {Words(c.Ticks)}", Palette.Haste),
            Delayed d => ($"{Name(d.SourceId)} slows {Name(d.TargetSourceId)} by {Words(d.Ticks)}", Palette.Slow),
            CooldownReset r => ($"{Name(r.SourceId)} readies {Name(r.TargetSourceId)}", Palette.Ready),
            StatModified m => ($"{Name(m.SourceId)}: {Name(m.TargetSourceId)} +{m.Amount} {m.Stat}", Palette.Accent),
            CooldownChanged c => ($"{Name(c.SourceId)}: {Name(c.TargetSourceId)} cooldown {c.FromTicks / 10.0:0.#}s -> {c.ToTicks / 10.0:0.#}s", Palette.Haste),
            PhaseStarted p => ($"{b.EnemyDefinition.Name} enters phase {p.Index + 2}!", Palette.Damage),
            SourceAdded a when a.Owner == Core.Engine.Side.Enemy => ($"{b.EnemyDefinition.Name} gains {Name(a.SourceId)}", Palette.Damage),
            BattleEnded end => (end.Result switch
            {
                BattleResult.PlayerWon => "Victory!",
                BattleResult.EnemyWon => "Defeat.",
                _ => "Draw.",
            }, Palette.Accent),
            _ => null,
        };
    }

    private static string Absorbed(DamageDealt d) => d.ShieldDamage > 0 ? $" ({d.ShieldDamage} on shield)" : "";

    private static string Words(int ticks) => Core.Content.Words.Seconds(ticks);

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
