# M2: Playable Fight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Luka can launch the game on Windows, build a board in a "Fight Lab" (any items, levels, enemy, character, passives), watch the fight play out on screen, click active items (including targeted ones), and read what everything does from tooltips.

**Architecture:** `ItemFighter.Game` drives a `Battle` from Core in real time (10 ticks/s, 1×/2×/4× speed, no pause) and animates from its event log. Presentation logic (clock, targeting, floating numbers, descriptions) lives in plain classes with unit tests. Drawing uses SpriteBatch at a fixed 1280×720 virtual resolution, scaled to the window. Text uses FontStashSharp with a bundled TTF. Art is loaded at runtime from PNGs, with generated placeholders when a file is missing.

**Tech Stack:** MonoGame.Framework.DesktopGL 3.8.2.1105, FontStashSharp.MonoGame 1.6.1, DejaVu Sans (Bitstream Vera licence, bundled), xUnit.

**Spec:** `docs/design/2026-10-10-itemfighter-design.md` (§3, §10 "Art pipeline", "UI", §11 M2). Luka's directions: build autonomously until there are visuals to test; content stays placeholder; everything easy to rebalance.

## Global Constraints

- Virtual resolution 1280×720, letterboxed to any window size. Window starts at 1280×720 and is resizable.
- The Game never mutates battle state directly. It only calls `Battle.Step()` and `Battle.Enqueue(...)`.
- Art paths: `Content/Art/Items/<id>.png`, `Content/Art/Enemies/<id>.png`, `Content/Art/Characters/<id>.png`, copied to output. A missing file means a placeholder, never a crash.
- Automation: `--screenshot <dir>` plus `--script <name>` runs a scripted session, saves PNGs and exits. This is how changes are verified headless (`xvfb-run`).

## Review Focus

1. **Clicks during the targeting state:** a click on an invalid target or empty space, and right-click/Escape. Expect it to cancel or ignore without sending a command. Test lives in Task 3.
2. **Missing or corrupt PNG:** expect the placeholder. Test lives in Task 2.
3. **Window resize to odd aspect ratios:** expect the mouse mapping to remain correct. Test lives in Task 2 (`VirtualScreen.ToVirtual`).
4. **Battle ending mid-targeting:** expect targeting to cancel and the result overlay to show. Test lives in Task 3.
5. **Very long names and descriptions in tooltips:** expect wrapping, with the tooltip kept on screen. Visual check in Task 6.

---

### Task 1: Ability descriptions (Core)

**Files:** Create `src/ItemFighter.Core/Content/Describe.cs`. Modify every effect and trigger class to implement `string Describe(DescribeContext ctx)`. Add an optional `"description"` JSON field on definitions that overrides the generated text. Test `tests/ItemFighter.Tests/DescribeTests.cs`.

**Interfaces:**
- `IEffect.Describe(DescribeContext)` and `ITrigger.Describe(DescribeContext)` (default-interface fallback returns the type name).
- `record DescribeContext(SourceDefinition Def, int Level)`.
- `static IReadOnlyList<string> SourceDescriber.Lines(SourceDefinition def, int level)` gives one line per ability, e.g. `"Every 4s: apply 5 Burn to the enemy."`.

- [ ] Failing tests:
  - `FireFlask_Level2`: `"Every 4s: apply 5 Burn to the enemy."`
  - `Bellows`: `"When an adjacent Fire item fires: charge it 1s."`
  - `Whetstone`: `"Click, then pick a Weapon: +5 Damage to it this fight."`
  - `DescriptionOverride_Wins`
  - `EveryContentItem_HasNonEmptyDescription`
- [ ] Implement. Run `dotnet test`. Commit `feat(core): generated ability descriptions for tooltips`.

### Task 2: Game shell, virtual screen, assets, screenshot automation

**Files:** Rewrite `src/ItemFighter.Game/Game1.cs`. Create `Platform/VirtualScreen.cs`, `Platform/Assets.cs` (fonts, textures + placeholder), `Platform/Automation.cs`, `Content/Fonts/DejaVuSans.ttf`, `Content/Fonts/DejaVuSans-Bold.ttf`, `Content/Fonts/LICENSE.txt`, `Content/Art/README.md`. Remove the old `Helpers/ShapeDrawer.cs` and `Input/InputState.cs` once replaced by `UI/Draw.cs` and `Platform/Input.cs`. Test project gets a reference to the Game project and `tests/ItemFighter.Tests/Game/VirtualScreenTests.cs` and `AssetsTests.cs`.

**Interfaces:**
- `VirtualScreen(int width=1280, int height=720)` with `Rectangle Destination(Point window)` and `Vector2 ToVirtual(Point windowPos, Point windowSize)`.
- `Assets.Texture(string kind, string id)` returns `Texture2D?` (null means draw a placeholder). `Assets.LoadPng(Stream)` returns null on bad data.
- `Screen` base class with `Update(GameTime, Input)` and `Draw(Ui)`. `ScreenManager` holds a stack and switches screens.

- [ ] Failing tests:
  - `ToVirtual_Letterboxed` (2560×1080 window, so the bars are on the sides)
  - `ToVirtual_Pillarboxed`
  - `LoadPng_Corrupt_ReturnsNull`
- [ ] Implement. Run under `xvfb-run` with `--screenshot` and check the main menu image. Commit `feat(game): game shell with virtual resolution, runtime assets, screenshot automation`.

### Task 3: Fight presentation logic

**Files:** Create `src/ItemFighter.Game/Fight/FightClock.cs`, `TargetingState.cs`, `FloatingTexts.cs`. Test `tests/ItemFighter.Tests/Game/FightLogicTests.cs`.

**Interfaces:**
- `FightClock(Battle)` with `int Speed` (1, 2 or 4), `void Advance(double seconds)` (steps 10 ticks/s × speed, at most 40 steps per call), and `int Interpolation` progress for smooth bars.
- `TargetingState` with `Idle | Selecting(SourceInstance active)`. `ClickItem(SourceInstance, Battle)` returns `ICommand?`. `Cancel()`. `OnBattleEnded()`.
- `FloatingTexts.Consume(IReadOnlyList<BattleEvent> newEvents)` produces floating numbers (damage red, shield blue, heal green, burn orange, poison purple, chill cyan).

- [ ] Failing tests:
  - `Clock_StepsTenPerSecond_TimesSpeed`
  - `Clock_StopsWhenBattleEnds`
  - `Click_ReadyUntargetedActive_ReturnsCommand`
  - `Click_TargetedActive_ThenValidTarget_ReturnsCommandWithTarget`
  - `Click_InvalidTarget_Ignored`
  - `Cancel_ReturnsToIdle`
  - `BattleEnd_CancelsTargeting`
  - `Click_NotReady_NoCommand`
  - `FloatingTexts_OnePerDamageEvent`
- [ ] Implement. Run `dotnet test`. Commit `feat(game): fight clock, targeting and floating text logic`.

### Task 4: UI kit

**Files:** Create `src/ItemFighter.Game/UI/Ui.cs` (rects, borders, bars, text with wrap/align, tooltip that stays on screen), `UI/Palette.cs`, `UI/Button.cs`.

- [ ] Implement, then verify visually in Tasks 5 and 6. This is drawing code with no logic worth unit testing. Commit with Task 5.

### Task 5: Fight Lab screen

**Files:** Create `src/ItemFighter.Game/Screens/FightLabScreen.cs`, `Screens/LabSetup.cs` (saved to `%AppData%/ItemFighter/lab.json`; any read error falls back to the default setup). Rewrite `Screens/MainMenuScreen.cs` with Fight Lab and Exit.

- [ ] Lab: 6 slots (click a slot → item picker grid of every item with art/placeholder, name, tags; right-click a slot to clear it; mouse wheel or +/− for level 1–4), enemy picker, character picker, passive toggles, and a big Fight button. Hovering anything shows its description tooltip.
- [ ] Failing test `LabSetup_RoundTrips_And_BadFile_FallsBack`, then implement.
- [ ] Screenshot check with script `lab`. Commit `feat(game): main menu and fight lab`.

### Task 6: Fight screen

**Files:** Create `src/ItemFighter.Game/Screens/FightScreen.cs`, `Fight/ItemCard.cs`, `Fight/CombatantPanel.cs`, `Fight/CombatLog.cs`.

- [ ] Layout (1280×720):
  - **Enemy, top:** portrait/placeholder, name, HP bar with shield overlay, status chips (burn/poison x/10 and chill with timer), and ability rows with name, cooldown bar, "in 2.3s" and description.
  - **Player board, bottom:** 6 item cards with art, level pips, a cooldown fill rising from the bottom, a ready glow plus "CLICK" for actives, and frozen/haste/slow tints. The player HP bar, shield and statuses sit above the board.
  - **Combat log, right side:** the last ~14 events in plain words.
  - **Overlays:** speed buttons 1×/2×/4×, floating numbers, and a card flash when an item fires.
  - **Result overlay:** Victory/Defeat/Draw, with Retry (same seed+1) and Back to Lab.
- [ ] Targeting: click an active that needs a target → valid targets highlight and the cursor hint reads "pick a Weapon". Right-click or Esc cancels.
- [ ] Screenshot scripts: `fight-start`, `fight-mid` (after 8s, with a targeted activation), `fight-end`. Read each PNG and check the layout, readability and overlaps.
- [ ] Commit `feat(game): playable fight screen`.

### Task 7: Hand-off

- [ ] README "How to play the test build on Windows" (install the .NET 8 SDK, then `dotnet run --project src/ItemFighter.Game`), plus where to drop art PNGs.
- [ ] Full `dotnet test`. Commit `docs: how to run the test build`.
