using ItemFighter.Core.Content;
using ItemFighter.Core.Determinism;

namespace ItemFighter.Core.Engine;

public static class RandomBoard
{
    /// <summary>A board of distinct random items (level 1) in every slot, no character or passives.</summary>
    public static PlayerSetup Create(ContentDatabase db, Rng rng)
    {
        var pool = db.OfKind(SourceKind.Item).ToList();
        var board = new List<BoardItem>();
        for (int slot = 0; slot < Board.SlotCount && pool.Count > 0; slot++)
        {
            int pick = rng.NextInt(pool.Count);
            board.Add(new BoardItem(pool[pick].Id, slot));
            pool.RemoveAt(pick);
        }
        return new PlayerSetup(null, board, []);
    }
}
