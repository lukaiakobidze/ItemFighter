using ItemFighter.Core.Engine;
using ItemFighter.UI;
using Microsoft.Xna.Framework;

namespace ItemFighter.FightView;

/// <summary>Where a floating number appears: over a combatant's HP bar or over one item/ability.</summary>
public readonly record struct FloatAnchor(Side? Side, int? SourceId)
{
    public static FloatAnchor ForSide(Side side) => new(side, null);
    public static FloatAnchor ForSource(int id) => new(null, id);
}

public sealed class FloatingText(string text, Color color, FloatAnchor anchor, int stack)
{
    public string Text => text;
    public Color Color => color;
    public FloatAnchor Anchor => anchor;

    /// <summary>Position among the texts showing on the same anchor, so they don't overlap.</summary>
    public int Stack => stack;
    public double Age { get; set; }

    /// <summary>How far it rises over its life, in pixels; varied so two texts don't hide exactly behind each other.</summary>
    public float Speed { get; init; } = 80;
}

/// <summary>Numbers that pop up and drift away when damage, shield, healing and statuses happen.</summary>
public sealed class FloatingTexts
{
    public const double Lifetime = 1.2;
    private readonly List<FloatingText> _active = [];
    private readonly Dictionary<FloatAnchor, int> _counters = [];
    private readonly Random _rng = new(); // looks only; the fight itself stays seeded

    /// <summary>How far a text rises over its life: each picks a random distance in this range (Luka 2026-10-16).</summary>
    public const float MinRise = 60, MaxRise = 130;

    public IReadOnlyList<FloatingText> Active => _active;

    public void Consume(IEnumerable<BattleEvent> events, Battle battle)
    {
        foreach (var e in events)
        {
            switch (e)
            {
                case DamageDealt d when d.Total > 0:
                    Add($"-{d.Total}", d.HpDamage == 0 ? Palette.Shield : Palette.For(d.Kind), FloatAnchor.ForSide(d.Target));
                    break;
                case ShieldGained g:
                    Add($"+{g.Amount}", Palette.Shield, FloatAnchor.ForSide(g.Target));
                    break;
                case Healed h:
                    Add($"+{h.Amount}", Palette.Heal, FloatAnchor.ForSide(h.Target));
                    break;
                case StatusApplied { TargetSide: Side side, Status: StatusType.Vulnerable }:
                    Add("Vulnerable!", Palette.Vulnerable, FloatAnchor.ForSide(side));
                    break;
                case StatusApplied { TargetSide: Side side, Status: StatusType.NoHeatGain }:
                    Add("No Heat Gain", Palette.Freeze, FloatAnchor.ForSide(side));
                    break;
                case StatusRemoved { Status: StatusType.NoHeatGain } r:
                    Add("Heat unlocked", Palette.Heat, FloatAnchor.ForSide(r.Target));
                    break;
                case StatusApplied { TargetSide: Side side } s:
                    Add($"+{s.Amount} {s.Status}", Palette.For(s.Status), FloatAnchor.ForSide(side));
                    break;
                case StatusApplied { TargetSourceId: int id } s:
                    Add(s.Status == StatusType.Freeze ? "Frozen" : s.Status.ToString(), Palette.For(s.Status), FloatAnchor.ForSource(id));
                    break;
                case StatusRemoved { Status: StatusType.Heat } r:
                    Add($"-{r.Amount} Heat", Palette.Heat, FloatAnchor.ForSide(r.Target));
                    break;
                case StatusRemoved r:
                    Add($"-{r.Status}", Palette.For(r.Status), FloatAnchor.ForSide(r.Target));
                    break;
                case NailsUprooted n:
                    Add($"Uprooted {n.Nails}!", Palette.Nail, FloatAnchor.ForSide(n.Target));
                    break;
                case PoisonBurst p:
                    Add("Poison burst!", Palette.Poison, FloatAnchor.ForSide(p.Target));
                    break;
                case Overheated o:
                    Add("OVERHEAT!", Palette.Heat, FloatAnchor.ForSide(o.Side));
                    break;
                case ChillFroze c:
                    Add("FROZEN!", Palette.Freeze, FloatAnchor.ForSide(c.Target));
                    break;
                case Charged c:
                    Add($"+{c.Ticks / 10.0:0.#}s", Palette.Haste, FloatAnchor.ForSource(c.TargetSourceId));
                    break;
                case Delayed d:
                    Add($"Slow +{d.Ticks / 10.0:0.#}s", Palette.Slow, FloatAnchor.ForSource(d.TargetSourceId));
                    break;
                case CooldownReset r:
                    Add("Ready!", Palette.Ready, FloatAnchor.ForSource(r.TargetSourceId));
                    break;
                case StatModified m:
                    Add($"+{m.Amount} {m.Stat}", Palette.Accent, FloatAnchor.ForSource(m.TargetSourceId));
                    break;
                case CooldownChanged cd:
                    Add($"Cooldown {cd.ToTicks / 10.0:0.#}s", Palette.Haste, FloatAnchor.ForSource(cd.TargetSourceId));
                    break;
            }
        }
    }

    public void Update(double seconds)
    {
        foreach (var t in _active) t.Age += seconds;
        _active.RemoveAll(t => t.Age >= Lifetime);
    }

    private void Add(string text, Color color, FloatAnchor anchor)
    {
        // A rolling position per anchor spreads consecutive numbers apart; it restarts once the anchor is quiet.
        int stack = _active.Any(t => t.Anchor == anchor) ? _counters.GetValueOrDefault(anchor) + 1 : 0;
        _counters[anchor] = stack;
        _active.Add(new FloatingText(text, color, anchor, stack) { Speed = MinRise + (float)_rng.NextDouble() * (MaxRise - MinRise) });
    }
}
