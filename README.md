# ItemFighter

A roguelike autobattler. Design: `docs/design/`. Build plans: `docs/superpowers/plans/`.

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
dotnet run --project tools/ItemFighter.Sim -- help
```
