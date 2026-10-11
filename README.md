# ItemFighter

A roguelike autobattler. Design: `docs/design/`. Build plans: `docs/superpowers/plans/`. Writing and rebalancing content: `docs/content-guide.md`.

## Playing the test build (Windows)

1. Install the **.NET 8 SDK** from https://dotnet.microsoft.com/download/dotnet/8.0 (pick "SDK", x64 installer).
2. Open a terminal in this folder and run:
   ```
   dotnet run --project src/ItemFighter.Game
   ```
   The first run downloads packages and takes a minute.
3. Main menu → **Fight Lab**.
   - Click a board slot, then an item. Right-click a slot to empty it. Use the mouse wheel or −/+ to change an item's level.
   - Pick an enemy, a character and passives on the right, then press **FIGHT**.
4. In a fight:
   - Glowing items are ready actives: click to use them. Items that need a target (Whetstone, Overclocker, Mirror Shard) are clicked first, then the item you want to aim at. Right-click or Esc cancels.
   - Hover anything to see what it does. Enemy abilities stay "???" until you see them in action.
   - 1× / 2× / 4× (or keys 1, 2, 3) change the speed.
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

Headless screenshots (Linux, for automated checks): `xvfb-run -a dotnet run --project src/ItemFighter.Game -- --screenshot shots --script fight` (scripts: `menu`, `lab`, `fight`, `boss`).
