using ItemFighter.Content;
using ItemFighter.Core.Content;
using ItemFighter.Core.Determinism;
using ItemFighter.Core.Engine;

// Command-line fight simulator. Active items are clicked as soon as they are ready.
//   validate
//   fight --items rusty_sword@0,buckler@1 --enemy goblin_scrapper [--seed 1] [--character wanderer] [--passives a,b] [--log]
//   bulk  --enemy ember_king [--runs 1000] [--seed 1]

var options = ParseOptions(args.Skip(1));
string command = args.FirstOrDefault() ?? "help";

var content = GameContent.Load();
if (content.Errors.Count > 0)
{
    Console.Error.WriteLine($"{content.Errors.Count} content error(s):");
    foreach (var e in content.Errors) Console.Error.WriteLine("  " + e);
    return 1;
}

switch (command)
{
    case "validate":
        Console.WriteLine($"OK: {content.Database.All.Count} definitions, no errors.");
        return 0;
    case "fight":
        return Fight();
    case "bulk":
        return Bulk();
    default:
        Console.WriteLine("usage: validate | fight --items id@slot,... --enemy id [--seed n] [--character id] [--passives a,b] [--log] | bulk --enemy id [--runs n] [--seed n]");
        return command == "help" ? 0 : 1;
}

int Fight()
{
    var board = Get("items", "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s =>
    {
        var parts = s.Split('@');
        var levelParts = parts[0].Split(':');
        return new BoardItem(levelParts[0], int.Parse(parts[1]), levelParts.Length > 1 ? int.Parse(levelParts[1]) : 1);
    }).ToList();
    var passives = Get("passives", "").Split(',', StringSplitOptions.RemoveEmptyEntries);
    string? character = options.GetValueOrDefault("character");
    var battle = new Battle(new BattleSetup(new PlayerSetup(character, board, passives), Get("enemy"), ulong.Parse(Get("seed", "1"))),
        content.Database, content.Scripts);
    AutoPilot.Run(battle);
    if (options.ContainsKey("log"))
        foreach (var e in battle.Events) Console.WriteLine(e);
    Console.WriteLine($"{battle.Result} at {battle.Tick / 10.0:0.0}s. Player {battle.Player.Hp}/{battle.Player.MaxHp} HP, enemy {battle.Enemy.Hp}/{battle.Enemy.MaxHp} HP.");
    return 0;
}

int Bulk()
{
    string enemy = Get("enemy");
    int runs = int.Parse(Get("runs", "1000"));
    var rng = new Rng(ulong.Parse(Get("seed", "1")));
    int wins = 0;
    long ticks = 0;
    var included = new SortedDictionary<string, (int Fights, int Wins)>(StringComparer.Ordinal);
    for (int i = 0; i < runs; i++)
    {
        var player = RandomBoard.Create(content.Database, rng);
        var battle = new Battle(new BattleSetup(player, enemy, (ulong)i), content.Database, content.Scripts);
        bool won = AutoPilot.Run(battle) == BattleResult.PlayerWon;
        wins += won ? 1 : 0;
        ticks += battle.Tick;
        foreach (var item in player.Board)
        {
            var (f, w) = included.GetValueOrDefault(item.ItemId);
            included[item.ItemId] = (f + 1, w + (won ? 1 : 0));
        }
    }
    Console.WriteLine($"{enemy}: {runs} random boards, player win rate {100.0 * wins / runs:0.0}%, average fight {ticks / 10.0 / runs:0.0}s");
    Console.WriteLine();
    Console.WriteLine($"{"item",-16} {"fights",7} {"win% when included",19}");
    foreach (var (id, (f, w)) in included.OrderByDescending(kv => (double)kv.Value.Wins / kv.Value.Fights))
        Console.WriteLine($"{id,-16} {f,7} {100.0 * w / f,18:0.0}%");
    return 0;
}

string Get(string key, string? fallback = null) =>
    options.TryGetValue(key, out var v) ? v : fallback ?? throw new ArgumentException($"missing --{key}");

static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    string? key = null;
    foreach (string a in args)
    {
        if (a.StartsWith("--")) { key = a[2..]; result[key] = ""; }
        else if (key is not null) { result[key] = a; key = null; }
    }
    return result;
}
