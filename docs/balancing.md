# Balancing: where every number lives

Every balance number in the game is in one folder: **`src/ItemFighter.Content/Data/`**. Changing a file there changes the base game. Every build copies that folder in, so what you save is what ships. No C# needs to change.

When you run the game from the repo, it reads this folder directly. Press **F5** in the Fight Lab to reload after saving, and `dotnet test` lists any mistake (file, id and what's wrong).

| What you want to change | File | Keys |
|---|---|---|
| An item's damage, shield, burn, cooldown, rarity, tags | `items/<item>.json` | `cooldown`, `rarity`, `tags`, and the `levels` table (one number per level 1 to 4). Numbers written straight into an effect (like Ice Bow's 1 Chill or War Banner's +3 per weapon) are edited right there |
| What an item does | `items/<item>.json` | `abilities` (see [content-guide.md](content-guide.md) for triggers and effects) |
| An enemy's HP, when it shows up, what it has | `enemies/<enemy>.json` | `maxHp`, `difficulty` (1 to 10), `abilityIds`, `passiveIds` |
| An enemy's attacks | `enemies/abilities/<ability>.json` | `cooldown`, `amount`, `percent`… |
| A passive (enemies list them in `passiveIds`; the passive shop sells every one) | `passives/<passive>.json` | `amount`, `every`, `per`, `maxHpPerWin`, `rarity` (sets its shop price)… |
| A character's HP, starting items and passive | `characters/<character>.json` | `maxHp`, `startingItems`, `abilities` |
| Burn, poison, chill, freeze, nail, power, the fight time limit, the minimum cooldown | `rules.json` | see below |
| Gold, prices, shops, run length, which enemies show up when, reward odds, bosses | `run.json` | see the run.json table in [content-guide.md](content-guide.md#the-run-runjson) |

## rules.json

| Key | Now | What it does |
|---|---|---|
| `fightTimeLimitSeconds` | 120 | A fight not won by then is a loss |
| `burnTickSeconds` | 1 | Burn deals its stacks as damage this often |
| `burnLossPerTick` | 1 | Stacks burn loses after each tick |
| `burnShieldMultiplier` | 2 | Burn damage against shield is multiplied by this |
| `poisonThreshold` | 10 | Poison bursts at this many stacks |
| `poisonBurstDamage` | 20 | Damage of every poison burst, however much poison went in (shield blocks it, like all damage). Poison past the threshold carries over: 9 + 3 bursts once and leaves 2 |
| `chillSeconds` | 5 | How long chill stacks last; each new stack restarts the timer |
| `chillCooldownPercentPerStack` | 5 | Each chill stack makes every cooldown this % longer |
| `chillFreezeAtStacks` | 5 | Chill stacks that freeze the target |
| `freezeSeconds` | 1 | How long that freeze lasts |
| `nailDamage` | 4 | Damage per Nail when Uproot pulls them out (passives with `NailDamage` add to it) |
| `powerDamagePerStack` | 1 | Each Power stack adds this much to every hit (not burn, poison or pain) |
| `vulnerableDamagePercent` | 50 | While Vulnerable, hits and pain ticks taken do this much % more damage (not burn or poison) |
| `overheatAt` | 100 | Heat at which a side overheats; Heat then goes back to 0 |
| `overheatDamage` | 10 | Damage an overheat deals (to yourself, or to the enemy for the Locomotive Engineer) |
| `overheatStallSeconds` | 2 | How long an overheat stalls every item (the Engineer's passive halves it) |
| `heatCoolingPerSecond` | 0 | Heat lost every second (0 = never cools, Luka 2026-10-19) |
| `heatFreezeImmunityAt` | 50 | At this much Heat or more, 5 chill still slows but can't freeze |
| `minCooldownSeconds` | 0.5 | Cooldown cuts during a fight (Fast Reload, faster-cooldown passives) stop here |
| `slowestCooldownSpeedPercent` | 10 | However chilled, cooldowns never run slower than this % of normal speed |

A key you leave out keeps the value in this table. A misspelled key or a bad value is reported by `dotnet test` and on the game's error screen, and the game then uses these defaults.

## Levels

Each item has a `levels` table with one value per level (1 to 4), e.g. `"damage": [5, 6, 8, 9]`. A duplicate of an item you own merges into it and raises its level. The current tables add roughly 25% of the level 1 value per level; you can set any numbers you like.
