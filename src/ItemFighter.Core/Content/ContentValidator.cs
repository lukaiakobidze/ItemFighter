using ItemFighter.Core.Engine;

namespace ItemFighter.Core.Content;

/// <summary>Checks references between definitions after loading. Reports every problem, never throws.</summary>
public static class ContentValidator
{
    public static IReadOnlyList<ContentError> Validate(ContentDatabase db, ScriptRegistry scripts)
    {
        var errors = new List<ContentError>();
        foreach (var def in db.All)
        {
            void Error(string message) => errors.Add(new ContentError(def.File, def.Id, message));

            if (def.Script is not null && !scripts.Contains(def.Script))
                Error($"unknown script '{def.Script}'");

            foreach (string abilityId in def.AbilityIds)
            {
                if (!db.TryGet(abilityId, out var ability)) Error($"unknown ability '{abilityId}'");
                else if (ability.Kind == SourceKind.Passive) Error($"'{abilityId}' is a passive: list it in passiveIds, not abilityIds");
                else if (ability.Kind != SourceKind.EnemyAbility) Error($"'{abilityId}' is not an EnemyAbility");
            }

            foreach (string passiveId in def.PassiveIds)
            {
                if (!db.TryGet(passiveId, out var passive)) Error($"unknown passive '{passiveId}'");
                else if (passive.Kind == SourceKind.EnemyAbility) Error($"'{passiveId}' is an enemy ability: list it in abilityIds, not passiveIds");
                else if (passive.Kind != SourceKind.Passive) Error($"'{passiveId}' is not a Passive");
            }

            foreach (string abilityId in def.Phases.SelectMany(p => p.AddAbilityIds))
            {
                if (!db.TryGet(abilityId, out var ability)) Error($"unknown ability '{abilityId}'");
                else if (ability.Kind is not (SourceKind.EnemyAbility or SourceKind.Passive)) Error($"'{abilityId}' is not an EnemyAbility or a Passive");
            }

            foreach (string itemId in def.StartingItems)
            {
                if (!db.TryGet(itemId, out var item)) Error($"unknown starting item '{itemId}'");
                else if (item.Kind != SourceKind.Item) Error($"starting item '{itemId}' is not an Item");
            }

            if (def.Difficulty is < 1 or > 10) Error($"'difficulty' must be 1 to 10, not {def.Difficulty}");

            if (def.GoldOnWin is not null && def.Kind != SourceKind.Item) Error("'goldOnWin' only works on items");

            if (def.Kind is SourceKind.Enemy or SourceKind.Character && def.MaxHp <= 0)
                Error("'maxHp' must be positive");
            if (def.RequiresTarget && def.Activation != Activation.Active)
                Error("'requiresTarget' only works on Active items");
            if (!(def.TargetFilter == "OwnItem" || (def.TargetFilter.StartsWith("OwnItemTag:", StringComparison.Ordinal) && def.TargetFilter.Length > "OwnItemTag:".Length)))
                Error($"'targetFilter' must be \"OwnItem\" or \"OwnItemTag:<tag>\", not \"{def.TargetFilter}\"");
        }
        return errors;
    }
}
