using ItemFighter.Core.Content;

namespace ItemFighter.Core.Engine;

/// <summary>Runtime state of one item, passive, character or enemy ability in a battle.</summary>
public sealed class SourceInstance
{
    public SourceInstance(int id, SourceDefinition def, Side owner, int? slot, int level)
    {
        Id = id;
        Def = def;
        Owner = owner;
        Slot = slot;
        Level = level;
    }

    public int Id { get; }
    public SourceDefinition Def { get; }
    public Side Owner { get; }
    public int? Slot { get; }
    public int Level { get; }

    public override string ToString() => $"{Def.Id}#{Id}";
}
