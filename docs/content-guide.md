# Content guide: writing and rebalancing items

All game content lives in `src/ItemFighter.Content/Data/` as JSON, one file per thing. To rebalance, change a number and save. No code changes are needed. For a map of where every number lives, see [balancing.md](balancing.md). `dotnet test` checks every file and lists every mistake at once (file, id and what's wrong).

## An item

```json
{
  "id": "fire_flask",            // unique, lowercase_with_underscores
  "name": "Fire Flask",
  "rarity": "Common",            // Common, Uncommon, Rare, Epic, Legendary (shop and reward odds)
  "tags": ["Burn", "Potion"],    // anything you like; other items look for tags, and owning a tag makes items with it show up more
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

Times are in seconds (`"seconds": 1.5`). Every number can be written directly (`"amount": 5`) or as a level value (`"amount": "{damage}"`). Either way it lives in the item's file, so a "5%" in an item's text is a number you can change there.

Items that do something outside the fight: `"goldOnWin": "{gold}"` gives that much gold for every fight won with the item on the board (Gold Pouch).

## Tooltip text

Tooltips are written automatically from the item's triggers and effects, e.g. "Every 4s: apply 5 Burn to the enemy." To see them all, run `dotnet run --project tools/ItemFighter.Sim -- describe --level 1`. To write your own text instead, add `"description": "Hits for {damage}.\nThen smiles."`, where `{damage}` shows the level value and `\n` starts a new line. Scripted items always need one.

## Triggers: when an ability happens

| trigger | meaning |
|---|---|
| `OnCooldown` | cooldown finished (default for Passive items) |
| `OnActivate` | player clicked it (default for Active items) |
| `OnFightStart` | once, at the start of the fight |
| `{ "type": "OnItemFired", "filter": [...] }` | another of your items fired. Filters: `Adjacent`, `Tag:Burn`, `Slot:1` (the item in board slot 1; for an enemy, its 1st attack), `Self`, `AnyOwn` |
| `OnDamageTaken` | your side took damage: hits, burn ticks and poison bursts. Add `"every": 5` for every 5th time |
| `OnDamageDealt` | your side hit the other side directly (not burn/poison). Also takes `"every"` |
| `{ "type": "OnHpBelow", "percent": 50 }` | once, when your HP first drops below 50% |
| `{ "type": "OnStatusApplied", "status": "Burn", "side": "Enemy" }` | burn was applied to the enemy (or `"Self"`) |
| `OnShieldGained` | your side gained shield |
| `{ "type": "EveryNthUse", "n": 3 }` | every 3rd time this item fires |

## Effects: what an ability does

| effect | fields | default target |
|---|---|---|
| `Damage` | `amount`; optional `hits` (2 = two separate hits), `percentOfShield` (adds that % of your current shield; 100 = equal to it), `perItem` + `perItemTag` (adds `perItem` for each board item with that tag, itself included) | Enemy |
| `Shield` | `amount` and/or `percentOfMaxHp` | Self |
| `Heal` | `amount` | Self |
| `ApplyStatus` | `status`, `amount` (stacks); for Freeze/Haste use `seconds`. Optional `percentOfCurrent` adds that % of the stacks the target already has (100 = doubles them) | Enemy for Burn/Poison/Chill/Pain/Nail; items for Freeze/Haste |
| `RemoveStatus` | `statuses`, e.g. `["Poison", "Burn"]` | Self. Removes every stack |
| `Uproot` | | Enemy. Pulls out every Nail for `nailDamage` each (rules.json, plus `NailDamage` modifiers), as one hit |
| `Charge` | `seconds` | Chosen. Takes time off the current cooldown; if that passes zero the item fires and the rest carries into its next cooldown |
| `Slow` | `seconds` | AllEnemy. Adds time to the current cooldown |
| `ResetCooldown` | | Chosen |
| `ReduceCooldown` | `percent` | Chosen. Shortens the target's cooldown by that % of what it is now, for the rest of the fight (never below `minCooldownSeconds`) |
| `ModifyStat` | `stat` (Damage, Shield, Heal, Burn, Poison, Chill), `amount` | Chosen. The target gets +amount for the rest of the fight |
| `CopyEffect` | | Chosen. The target's effects run once more |

**Targets** (`"target": ...`): `Self`, `Enemy`, `SelfItem`, `AdjacentItems`, `LeftItem`, `RightItem`, `ItemsWithTag:Burn`, `AllOwn`, `RandomOwnItem`, `RandomEnemyAbility`, `AllEnemy` (every enemy ability, or every player item when an enemy uses it), `Chosen` (what the player clicked), `TriggeringItem` (the item whose firing caused this reaction).

## Statuses

The numbers below are the defaults; all of them are in `Data/rules.json` (see [balancing.md](balancing.md)).

- **Burn:** every 0.5s deals its stacks as damage (double against shield), then loses 1 stack.
- **Poison:** no damage until 10 stacks, then bursts for 3 per stack (ignores shield) and resets.
- **Chill:** each stack makes all the target's cooldowns 5% longer. Stacks last 5s, and each new stack refreshes the timer; when it runs out the cooldowns go back to normal. 5 stacks freeze the target for 1s.
- **Pain:** stays all fight. Each time that side's item or ability fires (on its own or by click, not as a reaction), that side takes damage equal to its Pain. Shield blocks it, and it counts as taking damage.
- **Nail:** does nothing alone. Uproot pulls them all out for `nailDamage` (4) each.
- **Freeze / Haste:** on single items or abilities, for some `seconds`. Haste ×2 speed, Freeze stops. A frozen item that gets charged or reset to full waits and fires when the freeze ends.
- **Slow** is an effect, not a status: it adds seconds to the current cooldown.

## Safety limits

- Reaction chains stop 8 levels deep, and at most 256 reactions (including `CopyEffect` copies) happen per 0.1s tick. `CopyEffect` never copies its own item.
- Healing does nothing once HP is 0 or below: lethal damage stays lethal.
- Cooldowns must be above 0 and at most 600 seconds. Things that shorten a cooldown during a fight never take it below `minCooldownSeconds` (0.5s) in `rules.json`.

## Passives, characters and enemies

```json
{ "id": "iron_skin", "modifiers": [ { "stat": "ShieldGained", "amount": 5, "filter": { "kind": "Item" } } ] }
```

Modifier stats are `DamageTaken` (−1 = takes 1 less from every hit and tick), `ShieldGained`, `DamageDealt`, `HealGained`, `BurnApplied`, `PoisonApplied`, `ChillApplied`, `PainApplied`, `NailApplied`, `NailDamage` (+ damage per Nail uprooted), `PoisonThreshold` (negative = bursts sooner) and `CooldownPercent` (−20 = 20% faster). A filter can name a `kind` (Item, Passive, Character...) and/or a `tag`.

A character has `maxHp`, optional `modifiers` and `abilities`, and `startingItems`. An enemy has `maxHp`, a hidden `difficulty` from 1 to 10 (default 1; it decides when in a run the enemy shows up, how much gold it pays and how rare its rewards are), `abilityIds` (files in `enemies/abilities/` with `"kind": "EnemyAbility"`) and optional `phases`: `[{ "hpBelowPercent": 50, "addAbilities": ["molten_rage"] }]`.

Enemy abilities with no `cooldown` (like Venomous or Thick Skin) are the enemy's passives. Give an enemy ability `"tags": ["Weapon"]` when other effects should treat it as a weapon (the Blur's Rifle Shot).

## The run: run.json

`Data/run.json` holds every number of the run loop. Leave a key out to use its default; a misspelled key is reported as an error. Rarity weights are always 5 numbers, for Common, Uncommon, Rare, Epic and Legendary; a 0 means "never".

| Key | Default | What it does |
|---|---|---|
| `missions`, `encountersPerMission`, `choicesPerPick` | 5, 10, 3 | Run length, and how many encounters you choose from |
| `hotbarSize`, `backpackSize` | 6, 12 | Items that fight, and items you can carry besides |
| `startingGold` | 10 | Gold at the start of a run |
| `fightGold` | [5 … 14] | Gold for a normal win, by the enemy's difficulty 1 to 10 |
| `bossGold` | 15 | Gold for beating the mission boss |
| `prices` | Common 4 … Legendary 25 | Buy price per rarity (× level). Selling gives `sellPercent` of it |
| `sellPercent` | 50 | |
| `shopSize`, `rerollBaseCost`, `rerollCostStep` | 5, 1, 1 | Shop stock size; the first reroll costs 1, each one after costs 1 more |
| `dayWeights` | Fight 70, Shop 30 | How often each kind of day comes up. A fight day offers different enemies; a shop day offers the shops |
| `shops` | one "Shop" | `{ "id", "name", "tags": [] }` per shop. Tags limit the stock to items with any of those tags; empty sells everything |
| `daysPerDifficulty` | 5 | The run's difficulty goes up by 1 every 5 days: 1 in the first half of mission 1, 2 in the second half, … 10 in the second half of mission 5 |
| `difficultySpread` | 1 | Fight days offer enemies within this much of the run's difficulty (run difficulty 2 → enemies of difficulty 1, 2 and 3), all equally likely. When there are too few, the range widens |
| `bosses` | 5 entries | `{ "enemy": id }` per mission (missing missions reuse the last). Enemies always keep their base stats |
| `dropRarityWeights` | one row per difficulty | Rarity odds of the 3 items offered after beating an enemy of that difficulty |
| `shopRarityWeights` | one row per mission | Rarity odds of shop items |
| `tagBonusPercent` | 15 | Each item you own that shares a tag with an offer makes it 15% more likely |

## Unique behaviour

If something can't be built from these pieces, it gets a small C# script in `src/ItemFighter.Content/Scripts/` and the item says `"script": "Name"` (see Mirror Shard).
