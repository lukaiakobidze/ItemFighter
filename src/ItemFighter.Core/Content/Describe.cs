using System.Globalization;
using System.Text.RegularExpressions;
using ItemFighter.Core.Engine;
using ItemFighter.Core.Engine.Triggers;

namespace ItemFighter.Core.Content;

/// <summary>What a description is being written for: one definition at one level. OwnedByPlayer words an enemy passive the player took.</summary>
public sealed record DescribeContext(SourceDefinition Def, int Level, bool OwnedByPlayer = false)
{
    /// <summary>Enemy abilities talk to the player ("deal 5 damage to you").</summary>
    public bool IsEnemy => !OwnedByPlayer && (Def.Kind is SourceKind.Enemy or SourceKind.EnemyAbility);

    public int Value(ValueRef v) => v.ResolveAt(Def, Level);

    public string Items(TargetSpec t) => Words.Items(t, IsEnemy);

    public string Seconds(ValueRef ticks) => Words.Seconds(Value(ticks));

    /// <summary>"you"/"the enemy" for a combatant target, from this source's point of view.</summary>
    public string Combatant(TargetSpec t) => (t.Kind == TargetKind.Self) != IsEnemy ? "you" : IsEnemy ? "itself" : "the enemy";
}

public static class Words
{
    public static string Seconds(int ticks) =>
        (ticks / 10m).ToString("0.#", CultureInfo.InvariantCulture) + "s";

    /// <summary>How an item target reads in a sentence. Chosen and triggering items are "it".</summary>
    public static string Items(TargetSpec t, bool enemy = false) => (t.Kind, enemy) switch
    {
        (TargetKind.ItemsWithTag, true) => $"its {t.Tag} abilities",
        (TargetKind.AllOwn, true) => "all its abilities",
        (TargetKind.RandomOwnItem, true) => "another random ability of its own",
        (TargetKind.RandomEnemyAbility, true) => "a random item of yours",
        (TargetKind.SelfItem, true) => "this ability",
        (TargetKind.AllEnemy, true) => "all your items",
        (TargetKind.AllEnemy, false) => "every enemy ability",
        _ => Items(t.Kind, t),
    };

    private static string Items(TargetKind kind, TargetSpec t) => kind switch
    {
        TargetKind.Chosen or TargetKind.TriggeringItem => "it",
        TargetKind.SelfItem => "this item",
        TargetKind.AdjacentItems => "adjacent items",
        TargetKind.LeftItem => "the item to the left",
        TargetKind.RightItem => "the item to the right",
        TargetKind.ItemsWithTag => $"your {t.Tag} items",
        TargetKind.AllOwn => "all your items",
        TargetKind.RandomOwnItem => "a random other item",
        TargetKind.RandomEnemyAbility => "a random enemy ability",
        _ => t.ToString(),
    };

    public static string Possessive(string phrase) =>
        phrase == "it" ? "its" : phrase.EndsWith('s') ? phrase + "'" : phrase + "'s";

    public static string Article(string word) => word.Length > 0 && "AEIOUaeiou".Contains(word[0]) ? "an " + word : "a " + word;

    public static string Ordinal(int n) => n + (n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
}

/// <summary>Turns a definition into plain-English tooltip lines. A "description" in JSON replaces the generated text.</summary>
public static class SourceDescriber
{
    public static IReadOnlyList<string> Lines(SourceDefinition def, int level, bool ownedByPlayer = false)
    {
        var ctx = new DescribeContext(def, level, ownedByPlayer);
        if (def.Description is not null)
            return Regex.Replace(def.Description, @"\{(\w+)\}", m =>
                    def.Levels.TryGetValue(m.Groups[1].Value, out var values) ? values[Math.Clamp(level, 1, 4) - 1].ToString(CultureInfo.InvariantCulture) : m.Value)
                .Split('\n');

        var lines = new List<string>();
        foreach (var ability in def.Abilities)
        {
            string when = When(ability.Trigger, ctx);
            string what = string.Join(", then ", ability.Effects.Select(e => e.Describe(ctx)));
            lines.Add($"{when}: {what}.");
        }
        foreach (var m in def.Modifiers) lines.Add(Modifier(m, ctx));
        return lines;
    }

    private static string When(ITrigger trigger, DescribeContext ctx)
    {
        var def = ctx.Def;
        if (trigger is OnCooldownTrigger or OnActivateTrigger)
        {
            if (def.Activation != Activation.Active)
                return def.CooldownTicks is int cd ? $"Every {Words.Seconds(cd)}" : "Never on its own";
            if (!def.RequiresTarget) return "Click";
            const string tagPrefix = "OwnItemTag:";
            return def.TargetFilter.StartsWith(tagPrefix, StringComparison.Ordinal)
                ? $"Click, then pick {Words.Article(def.TargetFilter[tagPrefix.Length..])}"
                : "Click, then pick one of your items";
        }
        return trigger.Describe(ctx);
    }

    private static string Modifier(ModifierDefinition m, DescribeContext ctx)
    {
        if (m.Stat == "DamageTaken")
        {
            string amount = $"{Math.Abs(m.Amount)} {(m.Amount < 0 ? "less" : "more")} damage from everything";
            return ctx.IsEnemy ? $"Takes {amount}." : $"You take {amount}.";
        }
        string who = m.Filter switch
        {
            { Kind: SourceKind.Item, Tag: string tag } => $"Your {tag} items",
            { Kind: SourceKind.Item } => "Your items",
            { Tag: string tag } => $"Your {tag} items",
            { Kind: SourceKind k } => $"Your {k.ToString().ToLowerInvariant()} effects",
            _ => "Everything you have",
        };
        string sign = m.Amount >= 0 ? "+" : "−";
        int abs = Math.Abs(m.Amount);
        string what = m.Stat switch
        {
            "ShieldGained" => $"{sign}{abs} shield gained",
            "DamageDealt" => $"{sign}{abs} damage dealt",
            "HealGained" => $"{sign}{abs} healing",
            "BurnApplied" => $"{sign}{abs} Burn applied",
            "PoisonApplied" => $"{sign}{abs} Poison applied",
            "ChillApplied" => $"{sign}{abs} Chill applied",
            "PoisonThreshold" => m.Amount < 0 ? $"Poison bursts {abs} stacks sooner" : $"Poison bursts {abs} stacks later",
            "CooldownPercent" => m.Amount < 0 ? $"{abs}% faster cooldowns" : $"{abs}% slower cooldowns",
            _ => $"{sign}{abs} {m.Stat}",
        };
        return $"{who}: {what}.";
    }
}
