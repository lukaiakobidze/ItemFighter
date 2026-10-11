using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;

namespace ItemFighter.Tests;

public static class BattleHelpers
{
    public static string Enemy(string id = "dummy", int maxHp = 10_000, string abilityIds = "") =>
        $$"""{ "id": "{{id}}", "maxHp": {{maxHp}}, "abilityIds": [{{abilityIds}}] }""";

    /// <summary>An item JSON with one ability. effects is the JSON list body.</summary>
    public static string Item(string id, decimal? cooldown, string effects, string trigger = "\"OnCooldown\"", string extra = "") =>
        $$"""
        { "id": "{{id}}", {{(cooldown is null ? "" : $"\"cooldown\": {cooldown},")}} {{extra}}
          "abilities": [ { "trigger": {{trigger}}, "effects": [ {{effects}} ] } ] }
        """;

    public static Battle Fight(ContentDatabase db, string enemyId, params (string Id, int Slot)[] board) =>
        Fight(db, enemyId, 1, board);

    public static Battle Fight(ContentDatabase db, string enemyId, ulong seed, params (string Id, int Slot)[] board) =>
        new(new BattleSetup(new PlayerSetup(null, board.Select(b => new BoardItem(b.Id, b.Slot)).ToList(), []), enemyId, seed), db);

    public static void Steps(this Battle b, int n)
    {
        for (int i = 0; i < n; i++) b.Step();
    }

    public static IEnumerable<T> Of<T>(this Battle b) where T : BattleEvent => b.Events.OfType<T>();

    public static string Log(this Battle b) => string.Join("\n", b.Events.Select(e => e.ToString()));
}
