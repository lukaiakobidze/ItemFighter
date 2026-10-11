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

            foreach (string abilityId in def.AbilityIds.Concat(def.Phases.SelectMany(p => p.AddAbilityIds)))
            {
                if (!db.TryGet(abilityId, out var ability)) Error($"unknown ability '{abilityId}'");
                else if (ability.Kind != SourceKind.EnemyAbility) Error($"'{abilityId}' is not an EnemyAbility");
            }

            foreach (string itemId in def.StartingItems)
            {
                if (!db.TryGet(itemId, out var item)) Error($"unknown starting item '{itemId}'");
                else if (item.Kind != SourceKind.Item) Error($"starting item '{itemId}' is not an Item");
            }

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
