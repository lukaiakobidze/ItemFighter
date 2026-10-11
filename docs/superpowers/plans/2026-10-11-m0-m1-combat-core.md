# M0 + M1: Project Setup and Combat Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure ItemFighter into a cross-platform solution and build a deterministic, data-driven, headless combat engine with ~20 items, 3 enemies (one boss with phases), 1 character and 2 passives, all covered by tests.

**Architecture:** `ItemFighter.Core` is a pure C# library (no MonoGame) holding the tick-based battle simulation and the trigger/effect/modifier engine. `ItemFighter.Content` holds JSON definitions plus C# scripts for unique items. `ItemFighter.Game` (MonoGame DesktopGL) only builds in this milestone; rendering fights is M2. `ItemFighter.Sim` is a CLI that runs fights in bulk.

**Tech Stack:** .NET 8, C# 12, MonoGame.Framework.DesktopGL 3.8.2.1105, xUnit, System.Text.Json.

**Spec:** `docs/design/2026-10-10-itemfighter-design.md` (sections 3, 4, 5, 7 and 10 are implemented here).

## Global Constraints

- Root: `/mnt/project-files/ItemFighter` (Luka's synced project folder). Tooling in the cloud container: `apt-get install -y dotnet-sdk-8.0`.
- `ItemFighter.Core` must not reference MonoGame or any package besides the BCL.
- 1 tick = 0.1s. `TicksPerSecond = 10`. All gameplay values are `int`. Data cooldowns are given in seconds (decimal) and converted with `ticks = (int)Math.Round(seconds * 10m, MidpointRounding.AwayFromZero)`, minimum 1.
- No `System.Random`, `DateTime`, `Stopwatch`, `string.GetHashCode()` or `float`/`double` in Core gameplay logic. Never depend on `Dictionary`/`HashSet` enumeration order. Iterate sources in order **(side: Player then Enemy, slot ascending, then runtime id ascending)**.
- Board: 6 slots (`Board.SlotCount = 6`); every item takes exactly 1 slot (no sizes, per Luka 2026-10-11). Item levels 1–4; every per-level value array has exactly 4 entries.
- Fatigue starts at tick 300 (30s). From then on, every 10 ticks both sides take fatigue damage that ignores shield: 1, then 2, then 3, and so on.
- If both sides reach 0 HP in the same tick, the result is `Draw`, which counts as a player loss.
- Reaction chain depth cap: 8. A trigger that would fire at depth > 8 is dropped and emits `ChainCapped`.
- Battle hard cap: 1200 ticks (2 minutes). If reached, the result is `Draw`.
- Commit after every task, with the attribution lines from the session in each message.

## Review Focus

1. **Infinite reaction loops** (A fires → triggers B → triggers A). Expect the chain to stop at depth 8 and the battle to continue. Test lives in Task 6.
2. **Simultaneous death** in one tick, including from fatigue. Expect `Draw`, never a player win. Test lives in Task 4.
3. **Bad player commands:** activating a passive, a not-ready item, an unknown id, a frozen item, or a targeted effect with no or invalid target. Expect `CommandRejected(reason)` with no state change and no exception. Tests live in Task 7.
4. **Stacked speed effects:** haste + slow + chill + freeze on one item, and charge larger than the remaining cooldown. Expect freeze to win, cooldown speed never below 10%, cooldown never negative, and at most one fire per tick. Tests live in Tasks 5 and 7.
5. **Broken content files:** unknown trigger or effect type, duplicate ids, a level array not 4 long, an unknown `{placeholder}`, or an unknown script name. Expect the validator to report all errors at once with file and id, and the loader never to throw halfway through. Tests live in Task 3.

---

## File Structure

```
ItemFighter.sln
Directory.Build.props              net8.0, Nullable enable, ImplicitUsings, LangVersion 12, TreatWarningsAsErrors
.gitignore                         bin/ obj/ .idea/ .vs/ *.user
src/ItemFighter.Game/              existing game moved here (Game1, Program, Screens/, Input/, Helpers/, Icon.ico, app.manifest)
src/ItemFighter.Core/
  Determinism/Rng.cs               PCG32 + stream forking
  Determinism/StableHash.cs        FNV-1a 64-bit
  Content/Definitions.cs           SourceDefinition, AbilityDefinition, PhaseDefinition, ModifierDefinition, enums
  Content/ContentDatabase.cs       lookup by id
  Content/ContentLoader.cs         JSON → definitions (collects errors, never throws mid-load)
  Content/ContentValidator.cs      cross-reference checks
  Content/ValueRef.cs              int literal or "{name}" level lookup
  Engine/Battle.cs                 tick loop, commands, result
  Engine/Combatant.cs              HP, shield, statuses, sources
  Engine/SourceInstance.cs         runtime state of one item/passive/ability
  Engine/BattleEvents.cs           event records
  Engine/Commands.cs               ActivateCommand
  Engine/Statuses.cs               burn/poison/chill/freeze/haste/slow rules
  Engine/Modifiers.cs              ModifierPipeline
  Engine/Targeting.cs              TargetSpec resolution
  Engine/Triggers/                 ITrigger + one file per trigger + TriggerRegistry
  Engine/Effects/                  IEffect + one file per effect + EffectRegistry
  Engine/Scripting.cs              ISourceScript, [SourceScript] attribute, ScriptRegistry
src/ItemFighter.Content/
  Data/items/*.json  Data/enemies/*.json  Data/characters/*.json  Data/passives/*.json  (copied to output)
  Scripts/*.cs                     unique behaviours
  ContentPaths.cs                  DataDirectory helper
tools/ItemFighter.Sim/Program.cs   CLI
tests/ItemFighter.Tests/           xUnit, one file per Core area + Content/ComboTests.cs + GoldenReplayTests.cs
```

---

### Task 1: Solution restructure and toolchain

**Files:**
- Move: everything in the project root that belongs to the game → `src/ItemFighter.Game/` (`git mv` after `git init`)
- Delete: `bin/`, `obj/`, `Content/Content.mgcb` (assets load at runtime per the spec), root `ItemFighter.csproj` and `ItemFighter.sln` (recreated)
- Create: `.gitignore`, `Directory.Build.props`, `ItemFighter.sln`, `src/ItemFighter.Game/ItemFighter.Game.csproj`, `src/ItemFighter.Core/ItemFighter.Core.csproj`, `src/ItemFighter.Content/ItemFighter.Content.csproj`, `tools/ItemFighter.Sim/ItemFighter.Sim.csproj`, `tests/ItemFighter.Tests/ItemFighter.Tests.csproj`, `README.md` (build/test commands)
- Test: `tests/ItemFighter.Tests/SmokeTests.cs`

**Interfaces:**
- Produces: project references Game → Core, Content; Content → Core; Sim → Core, Content; Tests → Core, Content.

- [ ] **Step 1:** `git init` in the root. Commit the untouched original files first (excluding bin/obj) as `chore: import original MonoGame skeleton`, so Luka's starting point is preserved in history.
- [ ] **Step 2:** Create the projects. Game.csproj: `OutputType WinExe`, `TargetFramework net8.0`, `MonoGame.Framework.DesktopGL 3.8.2.1105`, keep `app.manifest` and `Icon.ico`, no Content.Builder package and no `RestoreDotnetTools` target. Content.csproj copies `Data/**/*.json` to output (`CopyToOutputDirectory PreserveNewest`); Tests and Sim reference Content so the data lands in their output too.
- [ ] **Step 3:** Write `SmokeTests.CoreHasNoMonoGameReference`, which asserts that `typeof(ItemFighter.Core.Determinism.Rng).Assembly.GetReferencedAssemblies()` contains no name starting with `MonoGame`. Add a stub `Rng` class so it compiles.
- [ ] **Step 4:** Run `dotnet build ItemFighter.sln && dotnet test`. Expected: build succeeds with 0 warnings and 1 test passes.
- [ ] **Step 5:** Commit `chore: restructure into Core/Content/Game/Sim/Tests solution on DesktopGL`.

### Task 2: Deterministic RNG and stable hashing

**Files:** Create `src/ItemFighter.Core/Determinism/Rng.cs`, `StableHash.cs`. Test `tests/ItemFighter.Tests/DeterminismTests.cs`.

**Interfaces:**
- Produces: `public static ulong StableHash.Of(string s)` (FNV-1a 64, UTF-8 bytes); `public sealed class Rng { Rng(ulong seed, ulong stream = 0); Rng Fork(string streamName); int NextInt(int maxExclusive); bool Chance(int percent); ulong State { get; } }`. `Fork` derives the new stream from `StableHash.Of(streamName)` and the parent seed, and does not advance the parent.

- [ ] **Step 1: Failing tests**
  - `StableHash_KnownVector`: `StableHash.Of("") == 0xcbf29ce484222325UL` and `StableHash.Of("a") == 0xaf63dc4c8601ec8cUL`.
  - `Rng_SameSeed_SameSequence`: two `new Rng(42)` produce identical 1000 `NextInt(100)` values.
  - `Rng_Fork_IsIndependentOfParentUsage`: `new Rng(7).Fork("combat")` and an `Rng(7)` that drew 50 numbers before `.Fork("combat")` yield identical sequences.
  - `Rng_NextInt_InRange_AndRoughlyUniform`: 100k draws of `NextInt(10)` are all in 0..9, and each bucket holds between 9,000 and 11,000.
- [ ] **Step 2:** Run `dotnet test --filter DeterminismTests`. Expected: FAIL (types missing).
- [ ] **Step 3:** Implement PCG32 (XSH-RR). `NextInt` uses rejection sampling (Lemire or threshold method) to avoid modulo bias.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit `feat(core): deterministic PCG32 rng and FNV-1a stable hash`.

### Task 3: Content definitions, loader and validator

**Files:** Create `Content/Definitions.cs`, `ValueRef.cs`, `ContentDatabase.cs`, `ContentLoader.cs`, `ContentValidator.cs`, `Engine/Triggers/TriggerRegistry.cs`, `Engine/Effects/EffectRegistry.cs` (registries hold names → factory, empty of real triggers/effects for now apart from test doubles). Test `tests/ItemFighter.Tests/ContentLoaderTests.cs` with JSON fixtures as inline strings.

**Interfaces:**
- Produces:
  - `enum SourceKind { Item, Passive, Character, EnemyAbility, Enemy }`, `enum Activation { Passive, Active, None }`, `enum Rarity { Common, Uncommon, Rare, Epic, Legendary }`.
  - `record SourceDefinition(string Id, string Name, SourceKind Kind, Rarity Rarity, IReadOnlyList<string> Tags, IReadOnlyList<string> Pools, int? CooldownTicks, Activation Activation, IReadOnlyDictionary<string,int[]> Levels, IReadOnlyList<AbilityDefinition> Abilities, IReadOnlyList<ModifierDefinition> Modifiers, string? Script, int MaxHp, IReadOnlyList<PhaseDefinition> Phases, IReadOnlyList<string> AbilityIds, IReadOnlyList<string> StartingItems)`. Enemy/character-only fields default to 0 or empty.
  - `record AbilityDefinition(ITrigger Trigger, IReadOnlyList<IEffect> Effects)`.
  - `record PhaseDefinition(int HpBelowPercent, IReadOnlyList<string> AddAbilityIds)`.
  - `record ModifierDefinition(string Stat, int Amount, ModifierFilter Filter)` where `record ModifierFilter(SourceKind? SourceKind, string? Tag)`. `Stat` is one of `ShieldGained`, `DamageDealt`, `HealGained`, `BurnApplied`, `PoisonApplied`, `ChillApplied`, `PoisonThreshold`, `CooldownPercent`.
  - `readonly record struct ValueRef(int? Literal, string? LevelKey) { int Resolve(SourceInstance s); }` parses `5` or `"{burn}"`.
  - `TriggerRegistry.Register(string type, Func<JsonElement, ITrigger> factory)`, same for `EffectRegistry`. Both are instance classes with a `CreateDefault()` that registers all built-ins, so tests can build custom registries.
  - `ContentLoadResult ContentLoader.LoadDirectory(string dataDir, TriggerRegistry t, EffectRegistry e)` → `record ContentLoadResult(ContentDatabase Database, IReadOnlyList<ContentError> Errors)`. `record ContentError(string File, string? Id, string Message)`.
  - `IReadOnlyList<ContentError> ContentValidator.Validate(ContentDatabase db, ScriptRegistry scripts)`.
  - `ContentDatabase.Get(string id)`, `TryGet`, `All`.

- [ ] **Step 1: Failing tests** (one per Review Focus #5 case plus the happy path)
  - `Loads_Item_FromSpecExample`: the Fire Flask JSON from spec §10 loads with `CooldownTicks == 40`, `Levels["burn"] == [3,5,8,12]`, 1 ability.
  - `UnknownEffectType_ReportsError_AndContinues`: a directory with one bad and one good file gives 1 error naming the file, the id and `"unknown effect type 'Explode'"`, and the good item is still in the database.
  - `DuplicateId_Reported`, `LevelArrayNotFour_Reported`, `UnknownPlaceholder_Reported` (`"{dmg}"` with no `dmg` level key), `UnknownScript_Reported`, `UnknownAbilityIdOnEnemy_Reported`.
  - `MalformedJson_ReportedNotThrown`.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement. JSON property names are camelCase as in spec §10. `cooldown` is seconds → ticks per Global Constraints. Each file holds one definition object or an array of them.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit `feat(core): content definitions, json loader and validator`.

### Task 4: Battle loop, cooldowns, damage/shield/heal, fatigue, result

**Files:** Create `Engine/Battle.cs`, `Combatant.cs`, `SourceInstance.cs`, `BattleEvents.cs`, `Targeting.cs`, `Effects/IEffect.cs`, `Effects/DamageEffect.cs`, `ShieldEffect.cs`, `HealEffect.cs`, `Triggers/ITrigger.cs`, `Triggers/OnCooldownTrigger.cs`. Test `BattleLoopTests.cs`.

**Interfaces:**
- Consumes: Task 2 `Rng`, Task 3 definitions and registries.
- Produces:
  - `record BattleSetup(PlayerSetup Player, string EnemyId, ulong Seed)`; `record PlayerSetup(string CharacterId, IReadOnlyList<BoardItem> Board, IReadOnlyList<string> PassiveIds)`; `record BoardItem(string ItemId, int Slot, int Level)`.
  - `sealed class Battle { Battle(BattleSetup setup, ContentDatabase db, ScriptRegistry scripts); int Tick; BattleResult Result; Combatant Player; Combatant Enemy; IReadOnlyList<BattleEvent> Events /* all events so far */; void Enqueue(ICommand c); void Step(); BattleResult RunToEnd(IEnumerable<ICommand>? commands = null); }`.
  - `enum BattleResult { Ongoing, PlayerWon, EnemyWon, Draw }`; `enum Side { Player, Enemy }`.
  - `class Combatant { Side Side; int MaxHp; int Hp; int Shield; StatusSet Statuses; IReadOnlyList<SourceInstance> Sources; }`.
  - `class SourceInstance { int Id; SourceDefinition Def; Side Owner; int? Slot; int Level; int CooldownTicks; int Progress /* speed units: 100 per normal tick, see Task 5 */; bool Ready; int UseCount; }`.
  - `interface IEffect { void Apply(EffectContext ctx); }`, `record EffectContext(Battle Battle, SourceInstance Source, SourceInstance? ChosenTarget, int Depth)`.
  - `interface ITrigger { /* see Task 6 */ }`, with `OnCooldownTrigger` as the marker used by the tick loop.
  - Events: `abstract record BattleEvent(int Tick)`. Subtypes `SourceFired(int SourceId)`, `DamageDealt(Side Target, int Amount, int Absorbed, int SourceId)`, `ShieldGained(Side, int Amount, int SourceId)`, `Healed(Side, int Amount, int SourceId)`, `FatigueDamage(int Amount)`, `BattleEnded(BattleResult)`, `CommandRejected(int SourceId, string Reason)`, `ChainCapped(int SourceId)`.
  - `TargetSpec` values: `Self`, `Enemy`, `SelfItem`, `AdjacentItems`, `LeftItem`, `RightItem`, `ItemsWithTag:<tag>`, `RandomOwnItem`, `RandomEnemyAbility`, `Chosen`. Random targets draw from the battle's `"combat"` rng stream. `Targeting.ResolveItems(...)` returns items in canonical order. "Adjacent" means the items in slot − 1 and slot + 1.
- Tick order inside `Step()` (decided here, used everywhere): ① apply commands queued for this tick (Task 7) → ② status ticks (Task 5) → ③ advance cooldowns → ④ fire every passive source whose cooldown completed this tick, in canonical order; actives become `Ready` → ⑤ fatigue → ⑥ phase checks (Task 8) → ⑦ resolve result (both ≤ 0 → Draw).
- Damage hits shield first, then HP. Heal is capped at MaxHp. HP may go below 0 internally; the result checks `<= 0`.

- [ ] **Step 1: Failing tests**
  - `Item_FiresOnItsCooldown`: a 2s Damage(10) item vs an enemy with 100 HP and no abilities. After 20 Steps there is exactly one `SourceFired` at tick 20, and the enemy has 90 HP.
  - `Shield_AbsorbsBeforeHp`: enemy shield 15, then 10 damage gives shield 5 and HP unchanged; then 10 more damage gives shield 0 and HP −5.
  - `Heal_CappedAtMax`.
  - `TwoItems_SameTick_FireInSlotOrder`: events are ordered slot 0 then slot 3.
  - `Fatigue_StartsAtTick300_Escalates`: two inert sides with 1000 HP. `FatigueDamage` amounts at ticks 300, 310, 320 are 1, 2, 3, and they ignore shield.
  - `SimultaneousDeath_IsDraw` (Review Focus #2): both sides at 1 HP, fatigue tick → `Draw`.
  - `HardCap_1200Ticks_IsDraw`.
  - `Battle_IsDeterministic`: the same setup run twice gives identical `Events` sequences (compare via `string.Join` of `ToString()`).
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement per the Interfaces above.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit `feat(core): tick-based battle loop with damage, shield, heal, fatigue`.

### Task 5: Statuses (burn, poison, chill, freeze, haste, slow)

**Files:** Create `Engine/Statuses.cs`, `Effects/ApplyStatusEffect.cs`. Test `StatusTests.cs`.

**Interfaces:**
- Produces: `class StatusSet { int Burn; int Poison; int PoisonThreshold /* 10 */; int Chill; int ChillExpiresAtTick; int FrozenUntilTick; }` on combatants. Item-level timers on `SourceInstance`: `int FreezeTicks, HasteTicks, SlowTicks`. Effect JSON `{"type":"ApplyStatus","status":"Burn|Poison|Chill|Freeze|Haste|Slow","amount":ValueRef,"target":TargetSpec}`. Burn, Poison and Chill are stacks on a combatant (`Self`/`Enemy`). Freeze, Haste and Slow target items/abilities, and their amount is a duration in **ticks**. Events: `StatusApplied(Side?, int? TargetSourceId, string Status, int Amount)`, `StatusTicked(Side, string Status, int Damage)`, `PoisonBurst(Side, int Damage)`, `ChillFroze(Side)`. Each status lives in its own class implementing `IStatusRule { void OnApply(...); void OnTick(...); }`, so new statuses (water, shock) and later reactions are added as new classes.
- Rules:
  - **Burn (fast, anti-shield):** every 5 ticks (`tick % 5 == 0`) deals damage equal to `Burn` stacks, then `Burn -= 1`. Against shield it counts **double**: shield loses up to `2 × Burn`, each 2 shield removed uses 1 point of the burn damage (rounded up), and the rest hits HP. Example: burn 5 vs shield 4 → shield 0, HP −3.
  - **Poison (build-up, burst):** no damage over time and no decay. Whenever stacks reach `PoisonThreshold` (10), it bursts for `stacks × 3` damage ignoring shield, and stacks reset to 0. Excess stacks are included in the burst.
  - **Chill (stacking slow):** all stacks share one 30-tick timer that **refreshes whenever a new stack is applied**; when it runs out, all chill is removed. Each active stack lowers the speed of **all** the combatant's sources by 10 percentage points. Reaching 5 stacks clears all chill and freezes the whole combatant for 20 ticks (`FrozenUntilTick`), emitting `ChillFroze`.
  - **Cooldown speed:** an item needs `CooldownTicks × 100` progress. Each tick it gains `speed` = 100, ×2 if hasted, ×½ if slowed (both = 100), then minus 10 per chill stack, with a floor of 10. A frozen item, or any item of a frozen combatant, gains 0. Freeze/haste/slow timers count down by 1 each tick.

- [ ] **Step 1: Failing tests:** `Burn_TicksEvery5_AndDecays` (burn 3 → damage 3,2,1 at ticks 5,10,15), `Burn_DoubleAgainstShield` (burn 5 vs shield 4 → shield 0, HP −3; burn 2 vs shield 10 → shield 6, HP unchanged), `Poison_NoTickDamage_BelowThreshold`, `Poison_BurstsAtTen_IgnoresShield_Resets` (apply 4, 4, 4 → burst 36 on the third application, stacks 0), `Chill_NewStackRefreshesTimer` (apply at ticks 0 and 10 → 2 stacks at tick 35, 0 at tick 41), `Chill_SlowsCooldown` (2s item, owner with 2 chill → speed 80 → fires at tick 25), `Chill_FiveStacks_FreezesCombatant20Ticks_AndClears`, `Freeze_StopsCooldown` (2s item frozen 10 ticks fires at tick 30), `Haste_DoublesSpeed` (2s item hasted 20 ticks fires at tick 10 and 20), `Slow_HalvesSpeed`, `HasteAndSlow_Cancel`, `SpeedFloor_TenPercent` (slowed item whose owner has 4 chill → speed max(10, 50−40) = 10), `FreezeBeatsHaste` (Review Focus #4).
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit `feat(core): burn, poison, chill, freeze, haste, slow`.

### Task 6: Triggers, reactions and chain cap

**Files:** Create `Triggers/OnFightStartTrigger.cs`, `OnItemFiredTrigger.cs`, `OnDamageTakenTrigger.cs`, `OnHpBelowTrigger.cs`, `OnStatusAppliedTrigger.cs`, `OnShieldGainedTrigger.cs`, `EveryNthUseTrigger.cs`. Modify `Battle.cs` (event dispatch). Test `TriggerTests.cs`.

**Interfaces:**
- Produces: `interface ITrigger { bool Matches(BattleEvent e, SourceInstance owner, Battle b); }`. JSON forms:
  - `{"type":"OnFightStart"}` (fires once before tick 1)
  - `{"type":"OnItemFired","filter":"Self|Adjacent|Tag:<t>|AnyOwn"}`
  - `{"type":"OnDamageTaken"}` (owner side took ≥1 HP or shield damage)
  - `{"type":"OnHpBelow","percent":50}` (fires once, the first time)
  - `{"type":"OnStatusApplied","status":"Burn","side":"Enemy|Self"}`
  - `{"type":"OnShieldGained"}`
  - `{"type":"EveryNthUse","n":3}` (on the owner's own fire count)
- Dispatch: every emitted event is offered, in canonical source order, to all abilities except the one that produced it (`OnItemFired:Self` excepted). A matching ability runs its effects at `Depth + 1`. If the depth would exceed 8, emit `ChainCapped` and skip it. A reaction does **not** reset the reacting item's cooldown, and does count as a use (`UseCount++`) and emits `SourceFired`.

- [ ] **Step 1: Failing tests:** one per trigger type (e.g. `OnItemFired_Adjacent_OnlyNeighboursReact` with an item in slot 2, neighbours in slots 1 and 3, a non-neighbour in slot 5), `OnHpBelow_FiresOnce`, `EveryNthUse_FiresOnThird`, `MutualReactionLoop_StopsAtDepth8_BattleContinues` (Review Focus #1: two items that each react to the other's fire; expect exactly one `ChainCapped` per origin fire and the battle reaching a result).
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit `feat(core): trigger system with reaction chains and depth cap`.

### Task 7: Active items, commands and item-targeting effects

**Files:** Create `Engine/Commands.cs`, `Effects/ChargeEffect.cs`, `ResetCooldownEffect.cs`, `ModifyStatEffect.cs`, `CopyEffect.cs`. Modify `Battle.cs`. Test `ActiveItemTests.cs`.

**Interfaces:**
- Produces: `interface ICommand { int Tick { get; } }`; `record ActivateCommand(int Tick, int SourceId, int? TargetSourceId) : ICommand`. Commands are applied in step ① of the tick matching `Tick`. Commands for past ticks are rejected with reason `"late"`.
- Definitions gain `bool RequiresTarget` and `string TargetFilter` (`"OwnItem"` default, `"OwnItemTag:<t>"`).
- Effects:
  - `Charge(amountTicks, target)` adds `amount × 100` progress, capped so it completes at most once this tick.
  - `ResetCooldown(target)` sets an item to Ready (active) or fires it next step ④ (passive).
  - `ModifyStat(stat, amount, target)` gives a permanent-for-this-battle `+amount` to the target's resolved effect values for `Damage|Shield|Heal|Burn|Poison`. Stored as `SourceInstance.Bonus[stat]`; `ValueRef.Resolve` adds it.
  - `CopyEffect(target)` runs the target's `OnCooldown`/`OnActivate` effects once as if this source owned them.
- Rejections (`CommandRejected` reasons): `"unknown_source"`, `"not_active"`, `"not_ready"`, `"frozen"`, `"target_required"`, `"invalid_target"`. A rejected command changes nothing.
- An activated item fires, sets `Ready=false`, and restarts its cooldown. An active item's abilities use trigger `{"type":"OnActivate"}`.

- [ ] **Step 1: Failing tests:** `Active_BecomesReady_DoesNotAutoFire`, `Activate_WhenReady_Fires_AndRestartsCooldown`, `Activate_Targeted_ModifyStat_BuffsTargetDamage` (target's next fire deals 10+5), `Charge_CompletesAtMostOnce`, and one rejection test per reason (Review Focus #3), each also asserting the HP, progress and ready state is unchanged.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit `feat(core): player-activated and targeted items`.

### Task 8: Modifiers, passives, characters, enemy phases

**Files:** Create `Engine/Modifiers.cs`. Modify `Battle.cs` (build sources from character + passives + enemy abilities; phase check step ⑥), `ShieldEffect`, `DamageEffect`, `HealEffect`, `ApplyStatusEffect`, cooldown setup. Test `ModifierTests.cs`, `EnemyPhaseTests.cs`.

**Interfaces:**
- Produces: `sealed class ModifierPipeline { int Apply(string stat, int baseValue, SourceInstance producer); }`. It sums `Amount` over all of the producer's side's `ModifierDefinition`s whose filter matches the producer (kind and tag). `CooldownPercent` modifies `CooldownTicks` once at battle start: `max(1, ticks * (100 + sum) / 100)`, using integer division.
- Character: `MaxHp` and `Modifiers`/`Abilities` become the player's base. `StartingItems` are not used by Battle (run setup is M3).
- Enemy: `MaxHp`, `AbilityIds` → `SourceInstance`s with `Slot = null`. When HP first drops below `PhaseDefinition.HpBelowPercent`, the listed abilities are added with fresh cooldowns, and `PhaseStarted(int Index)` is emitted.

- [ ] **Step 1: Failing tests:** `Passive_PlusFiveShieldFromItems_AppliesToItemShieldOnly` (character shield not boosted, since the filter is Kind=Item), `Modifier_TagFilter`, `CooldownPercent_Minus50_Halves`, `CooldownPercent_NeverBelowOneTick`, `Boss_AddsAbilitiesBelow50Percent_Once`, `EnemyAbility_UsesSameEffects` (enemy burn on the player ticks the same as player burn).
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit `feat(core): modifier pipeline, characters, passives, boss phases`.

### Task 9: Script hook for unique items

**Files:** Create `Engine/Scripting.cs`, `src/ItemFighter.Content/Scripts/MirrorShard.cs`. Test `ScriptTests.cs`.

**Interfaces:**
- Produces: `interface ISourceScript { void OnEvent(BattleEvent e, SourceInstance self, Battle b, int depth); }`; `[AttributeUsage(Class)] SourceScriptAttribute(string Name)`; `ScriptRegistry.FromAssemblies(params Assembly[])`, which discovers attributed types and orders them by name. Scripted sources receive every dispatched event after the data abilities, under the same depth cap.
- `MirrorShard` (Rare, Active, 6s): "Activate on an item: the next time that item fires, it fires a second time." Uses the `Chosen` target and a per-battle flag.

- [ ] **Step 1: Failing tests:** `ScriptRegistry_DiscoversMirrorShard`, `MirrorShard_DoublesNextFire_Once`.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit `feat(core): C# script hook for unique items`.

### Task 10: Starter content and combo tests

**Files:** Create JSON under `src/ItemFighter.Content/Data/`, plus `ContentPaths.cs`. Tests `ContentIntegrityTests.cs`, `ComboTests.cs`, `GoldenReplayTests.cs` + `tests/ItemFighter.Tests/Golden/*.txt`.

Activation column: P = passive (fires on cooldown), A = active (player clicks), N = none (reacts only). Content to author (numbers are starting values. Every item has 4-entry level arrays scaling roughly ×1, ×1.6, ×2.5, ×4):

| id | tags | act. | cd s | behaviour (level 1) |
|---|---|---|---|---|
| rusty_sword | Weapon | P | 3 | 8 damage |
| great_axe | Weapon | P | 7 | 35 damage |
| twin_daggers | Weapon | P | 2 | 4 damage, twice (two effects) |
| buckler | Armor | P | 4 | 10 shield |
| tower_shield | Armor | P | 8 | 40 shield; adjacent items' shield +3 (ModifyStat on adjacent at fight start) |
| fire_flask | Fire, Potion | P | 4 | 3 burn |
| bellows | Fire, Tool | N | — | OnItemFired Adjacent+Tag:Fire: charge that item 1s |
| inferno_tome | Fire | A | 10 | 8 burn; haste all Fire items 20 ticks |
| venom_vial | Poison, Potion | P | 3 | 2 poison |
| plague_bellows | Poison, Tool | P | 6 | 2 poison; OnItemFired Adjacent+Tag:Poison: 1 poison |
| frost_shard | Ice | A | 6 | 2 chill; freeze a random enemy ability 20 ticks |
| ice_wall | Ice, Armor | P | 6 | 15 shield; 1 chill |
| apple | Food | P | 5 | heal 8 |
| feast_table | Food | P | 9 | heal 10; haste adjacent items 20 ticks |
| whetstone | Tool | A | 8 | targeted: ModifyStat Damage +5 on chosen Weapon |
| gear_box | Tech | P | 4 | charge a random own item 1s |
| overclocker | Tech | A | 12 | targeted: ResetCooldown on chosen item |
| mirror_shard | Tech | A | 6 | script (Task 9) |
| spiked_mail | Armor | N | — | OnDamageTaken: 3 damage to enemy |
| war_drum | Tool | P | 6 | haste all Weapon items 10 ticks |

Enemies: `goblin_scrapper` (60 HP; `scrap_stab`: 3s 6 dmg), `bog_witch` (90 HP; `hex_poison`: 4s 2 poison; passive `thorny_hide`: OnDamageTaken 1 dmg back), `ember_king` boss (300 HP; `royal_slash` 4s 15 dmg; `ember_aura`: 6s, 4 burn on player; phase at 50%: adds `molten_rage` 3s 10 dmg + haste own abilities). Character `wanderer` (100 HP, no modifier). Passives `iron_skin` (+5 ShieldGained from Kind=Item), `kindling` (+1 BurnApplied, Tag Fire).

- [ ] **Step 1: Failing tests**
  - `AllContent_LoadsAndValidates_WithZeroErrors` (loads `ContentPaths.DataDirectory` with the default registries + `ScriptRegistry.FromAssemblies(contentAssembly)`).
  - `Counts`: ≥ 20 items, ≥ 3 enemies, ≥ 1 character, ≥ 2 passives.
  - `EveryItemSoloVsDummy_FiresWithin30s`: each item alone vs a 10,000 HP inert dummy produces ≥ 1 `SourceFired` before tick 300 (actives are auto-activated each time they're ready by a test helper `AutoPilot.Commands(battle)`). The exception is pure reactors (`bellows`, `spiked_mail`), tested with a partner.
  - Combos: `FireFlask_NextToBellows_FiresFasterThanAlone` (fire counts over 10s: alone 2, with bellows ≥ 3), `Kindling_AddsOneBurnPerFireApplication`, `Whetstone_OnGreatAxe_Adds5Damage`, `EmberKing_EntersPhase2`.
  - `GoldenReplay_<name>`: 3 fixed setups+seeds. Each serialises its `Events` to text and compares it to `Golden/<name>.txt`. If the env var `UPDATE_GOLDEN=1` is set, the files are rewritten instead.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Author the JSON and `ContentPaths` (`AppContext.BaseDirectory/Data`). Add `AutoPilot` in the test project.
- [ ] **Step 4:** Run `UPDATE_GOLDEN=1 dotnet test --filter GoldenReplay` once and review the generated files by eye (sane damage numbers). Then run `dotnet test`. Expected: all PASS.
- [ ] **Step 5:** Commit `feat(content): 20 starter items, 3 enemies, character, passives`.

### Task 11: Sim CLI and bulk robustness

**Files:** Create `tools/ItemFighter.Sim/Program.cs`. Test `BulkSimTests.cs`.

**Interfaces:**
- Consumes: everything above, plus `AutoPilot` (move it to `ItemFighter.Core/Engine/AutoPilot.cs` so Sim can use it: `static IEnumerable<ICommand> AutoPilot.ReadyActives(Battle b)` activates each ready active on its first legal target).
- Produces: CLI `dotnet run --project tools/ItemFighter.Sim -- fight --items rusty_sword@0,buckler@1 --enemy goblin_scrapper --seed 1` prints the result, ticks and final HPs. `-- bulk --enemy ember_king --runs 1000 --seed 1` builds random legal boards from all items, prints the win rate, average ticks and a per-item table (fights included in, win rate when included), sorted descending.

- [ ] **Step 1: Failing tests:** `RandomBoards_1000Fights_NoExceptions_AllEndByCap` (random legal boards vs each enemy, seeds 0..999), `RandomBoards_SameSeed_SameResult` (100 seeds run twice, identical results and event counts).
- [ ] **Step 2:** Run. Expected: FAIL (helper missing).
- [ ] **Step 3:** Implement the board randomiser (`Core/Engine/RandomBoard.cs`, `static PlayerSetup RandomBoard.Create(ContentDatabase db, Rng rng)`, which fills the 6 slots with random distinct items) and the CLI.
- [ ] **Step 4:** Run `dotnet test` (all PASS) and `dotnet run --project tools/ItemFighter.Sim -- bulk --enemy ember_king --runs 1000 --seed 1`. Expected: it completes and prints a table. Note any item with an inclusion win rate > 85% or < 15% in the hand-off message for Luka.
- [ ] **Step 5:** Commit `feat(sim): fight and bulk simulation CLI`.

---

## Done when

`dotnet build ItemFighter.sln` gives 0 warnings, `dotnet test` is all green, the Game project still compiles on DesktopGL, and the Sim bulk report for each enemy runs without errors.
