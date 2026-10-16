using ItemFighter.Core.Content;

namespace ItemFighter.UI;

/// <summary>Tooltip text for content, shared by the lab and the fight screen.</summary>
public static class Tooltips
{
    /// <summary>ownedByPlayer words an enemy ability the player has; ownedByEnemy words a shared passive an enemy has.</summary>
    public static (string Title, List<string> Lines) For(SourceDefinition def, int level, ContentDatabase db, bool ownedByPlayer = false, bool ownedByEnemy = false)
    {
        ownedByPlayer &= def.IsPassiveAbility; // only enemy passives change wording when the player has them
        ownedByEnemy &= def.Kind == SourceKind.Passive;
        var lines = new List<string>();
        var facts = new List<string>();
        if ((def.Kind is SourceKind.Item or SourceKind.Passive && !ownedByEnemy) || ownedByPlayer) facts.Add(def.Rarity.ToString() + (ownedByPlayer ? " passive" : ""));
        if (def.Tags.Count > 0) facts.Add(string.Join(", ", def.Tags));
        if (def.CooldownTicks is int cd) facts.Add($"{Words.Seconds(cd)} cooldown" + (def.Activation == Activation.Active ? " (click)" : ""));
        if (def.MaxHp > 0) facts.Add($"{def.MaxHp} HP");
        if (facts.Count > 0) lines.Add(string.Join("  ·  ", facts));
        lines.AddRange(SourceDescriber.Lines(def, level, ownedByPlayer, ownedByEnemy));
        if (def.Kind == SourceKind.Enemy)
        {
            foreach (string id in def.AbilityIds)
                if (db.TryGet(id, out var ability))
                    lines.Add($"{ability.Name}: {string.Join(" ", SourceDescriber.Lines(ability, 1, ownedByEnemy: ability.Kind == SourceKind.Passive))}");
            foreach (var phase in def.Phases)
                lines.Add($"Below {phase.HpBelowPercent}% HP: gains {string.Join(", ", phase.AddAbilityIds.Select(a => db.TryGet(a, out var d) ? d.Name : a))}");
        }
        if (def.Kind == SourceKind.Character && def.StartingItems.Count > 0)
            lines.Add("Starts with " + string.Join(", ", def.StartingItems.Select(i => db.TryGet(i, out var d) ? d.Name : i)));
        string title = def.Kind == SourceKind.Item && def.Levels.Count > 0 ? $"{def.Name}  (level {level})" : def.Name;
        return (title, lines);
    }
}
