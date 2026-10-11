# ItemFighter

A roguelike autobattler. Design: `docs/design/`. Build plans: `docs/superpowers/plans/`. Writing and rebalancing content: `docs/content-guide.md`.

## Playing the test build (Windows)

1. Install the **.NET 8 SDK** from https://dotnet.microsoft.com/download/dotnet/8.0 (pick "SDK", x64 installer).
2. Open a terminal in this folder and run:
   ```
   dotnet run --project src/ItemFighter.Game
   ```
   The first run downloads packages and takes a minute.
3. Main menu → **New run** to play a run (see below), or **Fight Lab** to test any board against any enemy.
   **A run:**
   - Pick a character. Each mission has 10 encounters, then its boss. Win 5 missions to win the run. Losing any fight ends the run.
   - Each day is a fight day (pick 1 of 3 different enemies; rarer ones pay more gold) or a shop day (pick a shop). Harder enemies show up as the run goes on.
   - Before each fight you arrange your hotbar (the 6 items that fight). It stays as you left it last fight.
   - Click an item to pick it up and click a slot to put it there. Right-click an item to sell it for half its price.
   - After a win you get gold and pick 1 of 3 rewards: items (the first is the enemy's own drop) or passives (the first is one of the enemy's passives).
   - A duplicate merges into your copy, one level higher.
   - "Save and quit to menu" (or Esc) keeps the run. **Continue run** picks it up again. Quitting mid-fight replays that fight.
   - Every run number (gold, prices, backpack size, bosses, odds) is in `src/ItemFighter.Content/Data/run.json`.
   **The Fight Lab:**
   - Click a board slot, then an item. Right-click a slot to empty it. Use the mouse wheel or −/+ to change an item's level.
   - Pick an enemy, a character and passives on the right, then press **FIGHT**.
4. In a fight:
   - Glowing items are ready actives: click to use them. Items that need a target are clicked first, then the item you want to aim at. Right-click or Esc cancels.
   - Hover anything to see what it does. Enemy abilities stay "???" until you see them in action.
   - 1× / 2× / 4× (or keys 1, 2, 3) change the speed. Pause (or Space / P) stops the fight; you can still click items while paused and they fire when you resume. Switching to another window pauses too.
   - A fight has 2 minutes. Running out of time counts as a defeat.
   - Your lab setup is remembered between sessions.

**Rebalancing while you play:** edit any JSON file in `src/ItemFighter.Content/Data/` (or drop PNGs into `src/ItemFighter.Game/Content/Art/`, see the README there), then press **F5** in the game. If a file has a mistake, the game lists it instead of crashing.

## Layout

- `src/ItemFighter.Core` – pure C# combat and run simulation (no MonoGame)
- `src/ItemFighter.Content` – item/enemy/character data (`Data/**/*.json`) and C# scripts for unique items
- `src/ItemFighter.Game` – MonoGame (DesktopGL) frontend
- `tools/ItemFighter.Sim` – command-line fight simulator
- `tests/ItemFighter.Tests` – xUnit tests

## Commands

```
dotnet build ItemFighter.sln
dotnet test
dotnet run --project src/ItemFighter.Game
dotnet run --project tools/ItemFighter.Sim -- help          # validate | fight | bulk | describe
```

Headless screenshots (Linux, for automated checks): `xvfb-run -a dotnet run --project src/ItemFighter.Game -- --screenshot shots --script fight` (scripts: `menu`, `lab`, `fight`, `bear`).
