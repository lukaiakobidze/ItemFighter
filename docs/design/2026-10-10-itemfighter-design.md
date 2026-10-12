# ItemFighter: Design Plan

Status: draft for Luka's review · 2026-10-10

## 1. Vision

A roguelike autobattler for Steam (PC). You collect items onto a board. Most fire on their own when their cooldowns finish, and some you click yourself at the right moment. You fight AI enemies and bosses whose abilities and passives you have to build around. Runs are long enough to grow attached to a build.

The main goal is **a huge, ever-growing pool of items with genuinely different behaviours that interact with each other**. The game should be easy to extend with new items, characters, enemies and mechanics in updates without breaking what exists.

Team: Luka (solo dev, design, art as PNGs) with Claude writing the code. Target: a commercial Steam release, with a demo early.

## 2. Core loop

```
Pick character → Mission 1..5
   each mission: 10 encounter picks (choose 1 of 3) → mission boss (shown from mission start)
→ win the run → (later) Endless mode
Between runs: unlock characters and items, leaderboards
```

## 3. Combat

- Two sides: the **player** (HP, board of items, passives, character) and the **enemy** (HP, list of abilities and passives).
- Real time, simulated on **fixed 0.1s ticks** by the backend. The frontend only plays back what the backend reports.
- **Passive items** fire as soon as their cooldown finishes. If several finish on the same tick, the tie is broken by board position (left to right), and the order is the same every time.
- **Active items** have their own cooldown. When ready, the player clicks to fire them. Some actives are **targeted**: click the item, then click another item (or the enemy) to apply the effect, for example "make that item stronger", "reset its cooldown" or "copy its effect".
- **No pausing** during fights (timing is a skill). A slow-motion accessibility option may come later.
- **Time limit (Luka, 2026-10-11):** no fatigue. A fight not won within 2 minutes counts as a loss. Fights can be paused.
- **Deterministic:** each fight has a seed. The same seed plus the same player inputs (tick-stamped commands) always gives the same result. This powers replays, tests, save/load, bug reproduction and verified leaderboard scores.

## 4. The effect engine (heart of the game)

Everything that "does something" is a **Source** built from the same parts:

| Source kind | Where it lives | Examples |
|---|---|---|
| Item | On the board, has a position (1 slot each) | Sword, Shield Generator, Fire Flask |
| Passive | No slot, always on | "+5 to all shield gained from items" |
| Character | Picked at run start | "Fire items start each fight with 1 charge" |
| Enemy ability | The enemy's ability list | "Every 4s: slam for 30", "Reflects 20% of burn" |

A Source has:

- **Triggers:** when it reacts. Examples: `OnCooldown`, `OnActivate`, `OnFightStart`, `OnItemFired(filter)`, `OnAdjacentFired`, `OnDamageTaken`, `OnHpBelow(%)`, `OnStatusApplied(burn/poison/...)`, `OnShieldGained`, `EveryNthUse`.
- **Effects:** what it does. Examples: `Damage`, `Shield`, `Heal`, `ApplyStatus` (burn, poison, chill, freeze, slow, haste), `Charge` (advance another item's cooldown), `ModifyStat`, `CopyEffect`, `ResetCooldown`, `Transform`, `SpawnTemporaryItem`.
- **Statuses** each have their own identity (not copied from The Bazaar):
  - **Burn** is fast and anti-armour: it ticks every 0.5s for its stack count, then loses a stack, and deals double damage to shield.
  - **Poison** builds up: it does no damage until it reaches 10 stacks, then bursts for big damage that ignores shield and resets.
  - **Chill** stacks slow the target's cooldowns, and the timer refreshes with every new stack. Reaching 5 stacks freezes the target briefly, so chill must be applied fast.
  - Later: water/cleansing counters and element **reactions** (water + burn → steam, water + chill → instant freeze, burn + poison → early burst). Each status is its own module, so these are added without reworking the engine.
- **Targets:** self, enemy, adjacent items, left or right neighbour, all items with a tag, a random item, the player-chosen item.
- **Modifiers:** ongoing stat changes that other effects read. Examples: "+5 shield from items", "Fire items have −10% cooldown", "Burn deals double to shielded targets". These live in one modifier pipeline, so every source of shield is boosted the same way whatever produced it.
- **Tags:** Weapon, Food, Tech, Fire, Ice, Poison, Tool... Tags are how items "find" each other.

Design rules:
1. **New mechanics add new triggers, effects or tags.** They never special-case existing items, so old content keeps working.
2. **Most content is pure data** (JSON) combining these blocks. Truly unique behaviours get a small C# script class, referenced by id from the data.
3. **Enemies use the exact same engine** as the player, so every status, modifier and trigger works on them automatically.

## 5. Items

- **Board:** a row of 6 slots (for now). Every item takes exactly 1 slot. Adjacency matters.
- **Rarity** (Common, Uncommon, Rare, Epic, Legendary) only controls how often an item shows up.
- **Level** (1 to 4, bronze → silver → gold → diamond): buying a duplicate levels the item up. Every item can grow, so early favourites stay viable.
- **Evolution:** some items transform when a condition is met (e.g. "after 50 burns, becomes Inferno Core").
- **Enchantments** (later): modifiers attached to a single item from events or shops.
- **Pools:** for now every item is in one shared pool (Luka, 2026-10-12). Character pools may come back later.

## 6. Characters

- Picked before a run, unlocked through play. **Sidegrades, never stronger** than the starting ones.
- Each has a starting passive or rule change, and a starting board.
- Examples: *Pyromancer* (burn stacks never decay); *Tinkerer* (5 slots, but items charge each other on fire); *Duelist* (active items have −30% cooldown, passives +30%).

## 7. Enemies and bosses

- Each enemy has HP and a list of **abilities** (on cooldown, or passive) built from the same blocks as items.
- **Readable intents:** the UI shows ability cooldowns and what they'll do ("Slam: 40 dmg in 3s").
- **Elites:** tougher enemies with an extra passive and a better reward.
- **Bosses:** one per mission, shown from mission start so you can build toward it. They gain new abilities at HP thresholds (phases) and have signature passives that punish one-dimensional builds (e.g. "immune to the first 3 freezes").

## 8. Run structure and economy

- **5 missions** per run. Each has **10 encounter picks**, then the **boss**.
- Each pick offers **3 options** drawn from: Fight, Elite fight, Shop, Themed shop (Blacksmith: weapons; Alchemist: potions...), Event, Rest/Upgrade.
- **Gold** is earned from fights and spent in shops. Shops can be **rerolled** for gold, with the cost rising per reroll.
- **Losing any fight ends the run** (like Slay the Spire). Fight options show the enemy and its HP, but not its abilities: those you learn by fighting it. During a fight, ability cooldowns and intents are visible. A bestiary (later) fills in with what you've seen.
- **Item offer weighting** keeps a huge pool from feeling thin:
  1. One shared pool of all items (Luka, 2026-10-12: no character pools for now), weighted by rarity.
  2. **Build gravity:** tags you already own are slightly more likely to appear.
  3. Themed shops let you steer on purpose.
  4. Rarity weights shift upward as missions progress.
- **Backpack and hotbar (Luka, 2026-10-11):** items you own live in a backpack with limited space. Unwanted items can be sold. Before each fight you pick which items go on the 6-slot hotbar; it starts from the previous fight's hotbar (empty on the first fight of a run).
- **Post-fight reward (Luka, 2026-10-12):** after each win you pick 1 of 3 items, random by rarity and owned tags. A harder enemy (higher difficulty) makes rarer items more likely. Enemies no longer carry a drop item, and wins never offer passives.
- **Passives (Luka, 2026-10-12):** come from passive shops (to be added), not from fights.
- **Days (Luka, 2026-10-11):** each encounter is a day. A fight day offers only fights, against different enemies; a harder enemy pays more gold. A shop day offers the shops (one for now, more later). More kinds of day later. No elites.
- **Enemy difficulty (Luka, 2026-10-11, revised 2026-10-12):** enemies never grow; they keep their base stats. Enemies have no rarity, only a hidden difficulty 1 to 10. The run is split into 10 halves of missions, and each half has a run difficulty (1 for the first half of mission 1, 2 for the second half, … 10 for the second half of mission 5). A fight day offers enemies of that difficulty or one above or below it (second half of mission 1 → 1 to 3). Grizzly Bear and the Blur are 2; the other starter enemies are 1.
- **Rarity:** items and passives carry a rarity; enemies don't.
- **Item levels:** each level slightly improves the item's main number (about +25% per level, set per item in data). Whether upgrading stays in the game is still open.
- Expected run length: roughly 1.5 to 3 hours. **Mid-run save/quit is required.**

## 9. Meta progression and competition

- **Unlocks:** characters (sidegrades). Items have light onboarding locks: a core set is available at first, and the rest unlocks over the first 5 to 10 runs through milestones ("win a fight with 5 burn items").
- **Endless mode** (later update): keep fighting past the final boss with escalating enemies.
- **Leaderboards** (Steam): endless depth **per character**.
- **Daily or weekly seeded run** (later): everyone gets the same seed. Scores are submitted with the input log and can be verified by replaying the deterministic simulation.
- **Difficulty tiers** (ascensions) can come later.

## 10. Technical architecture

Engine: **MonoGame**, entirely code-driven so Claude can build and test without an editor. .NET 8, C#.

### Solution layout

```
ItemFighter.sln
  src/ItemFighter.Core/      Pure C# simulation: no MonoGame reference.
                             Combat engine, effect engine, run/shop logic, RNG, save state.
  src/ItemFighter.Content/   Data: items/*.json, enemies/*.json, characters/*.json, passives/*.json
                             + Scripts/ (C# classes for unique behaviours)
  src/ItemFighter.Game/      MonoGame frontend: screens, UI, animation, input → commands.
  tools/ItemFighter.Sim/     Command-line runner: simulate N fights, balance reports, content validation.
  tests/ItemFighter.Tests/   xUnit tests against Core and Content.
```

The current `ItemFighter` project becomes `ItemFighter.Game`. Its screen manager and input helper can be kept and grown.

**Platform switch:** change from `MonoGame.Framework.WindowsDX` to `MonoGame.Framework.DesktopGL`. It builds and runs headless tests on Linux (where Claude works), runs on Windows, and works with Steam Deck/Linux for free.

### Core rules (determinism)

- The backend never reads the clock, the frame rate, or `System.Random`. It uses its own seeded RNG (e.g. xoshiro/PCG), split into streams per purpose (shop, combat, events) so adding a shop roll doesn't change combat outcomes.
- Use integer or fixed-point math for gameplay values (HP, damage, cooldowns in ticks). No float gameplay math.
- Iterate in a defined order (by board slot, then id), never over a `Dictionary`'s ordering.
- The backend produces an **event log** per tick (`ItemFired`, `DamageDealt`, `StatusApplied`, `CooldownChanged`...). The frontend animates from this log.
- The frontend sends the backend only **commands** (`Activate(slot, tick, target?)`, `BuyItem`, `MoveItem`, `PickEncounter`...).

### Content format (sketch)

```json
{
  "id": "fire_flask",
  "name": "Fire Flask",
  "rarity": "Common",
  "tags": ["Fire", "Potion"],
  "cooldown": 4.0,
  "activation": "Passive",
  "levels": { "burn": [3, 5, 8, 12] },
  "abilities": [
    { "trigger": "OnCooldown",
      "effects": [ { "type": "ApplyStatus", "status": "Burn", "amount": "{burn}", "target": "Enemy" } ] }
  ]
}
```

Unique items reference a script: `"script": "MirrorShard"` maps to a C# class implementing the same `ISourceBehaviour` interface the data-driven effects use.

On startup (and in tests), a **content validator** checks every file: valid ids, known triggers and effects, sane numbers, art present (with fallback).

### Art pipeline

- Luka drops PNGs into `Content/Art/Items/<item_id>.png` (and `Enemies/`, `Characters/`, `UI/`). Loaded at runtime with `Texture2D.FromStream`, with no content pipeline step.
- Missing art falls back to a generated placeholder (coloured frame + name), so items can be built before their art exists.
- Fonts: FontStashSharp (runtime TTF, crisp at any size).

### UI

A small in-house UI layer on top of MonoGame: layout boxes, buttons, drag and drop, rich tooltips (keywords inside tooltips explain themselves), cooldown sweeps, floating damage numbers, tweened animations. It is built incrementally as screens need it.

### Saves

Run save = run seed + character + current run state (board, gold, passives, mission/encounter index) as JSON, written after each encounter. Settings and unlocks are separate files. Steam Cloud syncs the save folder later.

### Steam

Steamworks.NET for achievements, leaderboards, cloud saves and later Workshop. Integrated late (milestone 6). The game runs without Steam during development.

### Testing and balancing

- Unit tests for each trigger and effect, plus **combo tests** ("Fire Flask next to Bellows deals X burn in 10s").
- **Golden replays:** recorded fights whose results must not change unless intended.
- **Balance sim:** `ItemFighter.Sim` runs thousands of fights with random boards and reports win rates and DPS outliers per item, to flag broken items before Luka plays them.

## 11. Milestones

| # | Milestone | Done when |
|---|---|---|
| M0 | Project setup | Solution restructured as above, DesktopGL, tests run, builds on Linux and Windows |
| M1 | Combat backend | Effect engine + ~20 items + 3 enemies, fully simulated and tested headless |
| M2 | Playable fight | One fight on screen: board, cooldowns, clicking actives (incl. targeted), enemy intents, damage numbers, win/lose |
| M3 | One mission | Encounter picks (1 of 3), shop with reroll, item levels, gold, rest, a boss with phases |
| M4 | Full run | 5 missions, mid-run save/load, run summary screen, ~60 items, 2 characters |
| M5 | Content and meta | Unlocks, character pools, themed shops, build gravity, evolutions, ~150 items, 4 characters |
| M6 | Steam demo | Steamworks integration, settings, polish pass, demo build |
| Later | Updates | Endless mode, leaderboards, daily seeded runs, enchantments, difficulty tiers, more content |

## 12. Open for later

- Event design (choices, risks, curses).
- Number of slots per character, and whether board size grows during a run.
- Visual style and resolution target (affects how UI scales).
- Music and sound sourcing.
