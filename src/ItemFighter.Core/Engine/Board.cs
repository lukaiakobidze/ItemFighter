namespace ItemFighter.Core.Engine;

public static class Board
{
    /// <summary>Number of item slots on the player's board. Every item takes one slot.</summary>
    public const int SlotCount = 6;
}

public sealed record BoardItem(string ItemId, int Slot, int Level = 1);

/// <param name="CharacterId">Null uses a plain 100 HP character with no passives (handy for tests).</param>
public sealed record PlayerSetup(string? CharacterId, IReadOnlyList<BoardItem> Board, IReadOnlyList<string> PassiveIds);

/// <summary>EnemyHpPercent scales the enemy's max HP (run missions, elites and bosses).</summary>
public sealed record BattleSetup(PlayerSetup Player, string EnemyId, ulong Seed, int EnemyHpPercent = 100);
