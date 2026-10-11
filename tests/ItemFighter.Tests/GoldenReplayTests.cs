using ItemFighter.Core.Engine;

namespace ItemFighter.Tests;

/// <summary>
/// Recorded fights whose event logs must not change unless intended.
/// After an intended change: UPDATE_GOLDEN=1 dotnet test --filter GoldenReplay, then review the diff.
/// </summary>
public class GoldenReplayTests
{
    private static readonly string GoldenDir = Path.Combine(FindRepoRoot(), "tests", "ItemFighter.Tests", "Golden");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ItemFighter.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private static void Check(string name, string enemy, string? character, string[] passives, string[] items, ulong seed)
    {
        var content = ContentIntegrityTests.WithDummy();
        var board = items.Select((id, slot) => new BoardItem(id, slot)).ToList();
        var b = new Battle(new BattleSetup(new PlayerSetup(character, board, passives), enemy, seed), content.Database, content.Scripts);
        AutoPilot.Run(b);
        string log = string.Join("\n", b.Events) + "\n";
        string path = Path.Combine(GoldenDir, name + ".txt");
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(GoldenDir);
            File.WriteAllText(path, log);
            return;
        }
        Assert.True(File.Exists(path), $"missing golden file {path}; run with UPDATE_GOLDEN=1");
        Assert.Equal(File.ReadAllText(path), log);
    }

    [Fact]
    public void GoldenReplay_Burn_vs_FlamingRock() =>
        Check("burn_vs_flaming_rock", "flaming_rock", "silver_knight", [], ["cinder", "molten_dagger", "sword", "metal_plating"], 1);

    [Fact]
    public void GoldenReplay_Poison_vs_GreenSnake() =>
        Check("poison_vs_green_snake", "green_snake", "silver_knight", [], ["venom_fang", "fang_knife", "tower_shield", "apple", "dagger"], 2);

    [Fact]
    public void GoldenReplay_Mixed_vs_GrizzlyBear() =>
        Check("mixed_vs_grizzly_bear", "grizzly_bear", "silver_knight", [], ["axe", "weights", "apple", "ice_bow", "tower_shield", "fang_knife"], 3);
}
