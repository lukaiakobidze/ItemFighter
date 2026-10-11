# Content guide: writing and rebalancing items

All game content lives in `src/ItemFighter.Content/Data/` as JSON, one file per thing. To rebalance, change a number and save. No code changes are needed. `dotnet test` checks every file and lists every mistake at once (file, id and what's wrong).

## An item

```json
{
  "id": "fire_flask",            // unique, lowercase_with_underscores
  "name": "Fire Flask",
  "rarity": "Common",            // Common, Uncommon, Rare, Epic, Legendary (shop odds)
  "tags": ["Fire", "Potion"],    // anything you like; other items can look for tags
  "pools": ["neutral"],          // which shop pools offer it (character pools come later)
  "cooldown": 4,                 // seconds; leave out for items that only react
  "activation": "Passive",       // Passive (fires itself), Active (player clicks), None (only reacts)
  "levels": { "burn": [3, 5, 8, 12] },   // values per level 1-4, used as "{burn}"
  "abilities": [
    { "trigger": "OnCooldown",
      "effects": [ { "type": "ApplyStatus", "status": "Burn", "amount": "{burn}" } ] }
  ]
}
```

Active items can add `"requiresTarget": true` (the player clicks the item, then another of their items) and `"targetFilter": "OwnItemTag:Weapon"` to only allow certain targets.

Times are in seconds (`"seconds": 1.5`). Every number can be written directly (`"amount": 5`) or as a level value (`"amount": "{damage}"`).

## Tooltip text

Tooltips are written automatically from the item's triggers and effects, e.g. "Every 4s: apply 5 Burn to the enemy." To see them all, run `dotnet run --project tools/ItemFighter.Sim -- describe --level 1`. To write your own text instead, add `"description": "Hits for {damage}.\nThen smiles."`, where `{damage}` shows the level value and `\n` starts a new line. Scripted items always need one.

## Triggers: when an ability happens

| trigger | meaning |
|---|---|
| `OnCooldown` | cooldown finished (default for Passive items) |
| `OnActivate` | player clicked it (default for Active items) |
| `OnFightStart` | once, at the start of the fight |
| `{ "type": "OnItemFired", "filter": [...] }` | another of your items fired. Filters: `Adjacent`, `Tag:Fire`, `Self`, `AnyOwn` |
| `OnDamageTaken` | your side took damage (any kind: hits, burn, poison, fatigue). Add `"every": 5` for every 5th time |
| `OnDamageDealt` | your side hit the other side directly (not burn/poison). Also takes `"every"` |
| `{ "type": "OnHpBelow", "percent": 50 }` | once, when your HP first drops below 50% |
| `{ "type": "OnStatusApplied", "status": "Burn", "side": "Enemy" }` | burn was applied to the enemy (or `"Self"`) |
| `OnShieldGained` | your side gained shield |
| `{ "type": "EveryNthUse", "n": 3 }` | every 3rd time this item fires |

## Effects: what an ability does

| effect | fields | default target |
|---|---|---|
| `Damage` | `amount` | Enemy |
| `Shield` | `amount` and/or `percentOfMaxHp` | Self |
| `Heal` | `amount` | Self |
| `ApplyStatus` | `status`, `amount` (stacks); for Freeze/Haste use `seconds` | Enemy for Burn/Poison/Chill; items for Freeze/Haste |
| `Charge` | `seconds` | Chosen. Takes time off the current cooldown; if that passes zero the item fires and the rest carries into its next cooldown |
| `Slow` | `seconds` | AllEnemy. Adds time to the current cooldown |
| `ResetCooldown` | | Chosen |
| `ModifyStat` | `stat` (Damage, Shield, Heal, Burn, Poison, Chill), `amount` | Chosen. The target gets +amount for the rest of the fight |
| `CopyEffect` | | Chosen. The target's effects run once more |

**Targets** (`"target": ...`): `Self`, `Enemy`, `SelfItem`, `AdjacentItems`, `LeftItem`, `RightItem`, `ItemsWithTag:Fire`, `AllOwn`, `RandomOwnItem`, `RandomEnemyAbility`, `AllEnemy` (every enemy ability, or every player item when an enemy uses it), `Chosen` (what the player clicked), `TriggeringItem` (the item whose firing caused this reaction).

## Statuses

- **Burn:** every 0.5s deals its stacks as damage (double against shield), then loses 1 stack.
- **Poison:** no damage until 10 stacks, then bursts for 3 per stack (ignores shield) and resets.
- **Chill:** each stack makes all the target's cooldowns 5% longer. Stacks last 5s, and each new stack refreshes the timer; when it runs out the cooldowns go back to normal. 5 stacks freeze the target for 1s.
- **Freeze / Haste:** on single items or abilities, for some `seconds`. Haste ×2 speed, Freeze stops. A frozen item that gets charged or reset to full waits and fires when the freeze ends.
- **Slow** is an effect, not a status: it adds seconds to the current cooldown.

## Safety limits

- Reaction chains stop 8 levels deep, and at most 256 reactions (including `CopyEffect` copies) happen per 0.1s tick. `CopyEffect` never copies its own item.
- Healing does nothing once HP is 0 or below: lethal damage stays lethal.
- Cooldowns must be above 0 and at most 600 seconds.

## Passives, characters and enemies

```json
{ "id": "iron_skin", "modifiers": [ { "stat": "ShieldGained", "amount": 5, "filter": { "kind": "Item" } } ] }
```

Modifier stats are `DamageTaken` (−1 = takes 1 less from every hit and tick), `ShieldGained`, `DamageDealt`, `HealGained`, `BurnApplied`, `PoisonApplied`, `ChillApplied`, `PoisonThreshold` (negative = bursts sooner) and `CooldownPercent` (−20 = 20% faster). A filter can name a `kind` (Item, Passive, Character...) and/or a `tag`.

A character has `maxHp`, optional `modifiers` and `abilities`, and `startingItems`. An enemy has `maxHp`, an optional `difficulty` (shown on fight previews), `abilityIds` (files in `enemies/abilities/` with `"kind": "EnemyAbility"`) and optional `phases`: `[{ "hpBelowPercent": 50, "addAbilities": ["molten_rage"] }]`.

## Unique behaviour

If something can't be built from these pieces, it gets a small C# script in `src/ItemFighter.Content/Scripts/` and the item says `"script": "Name"` (see Mirror Shard).
