namespace ItemFighter.Core.Engine;

/// <summary>Something that happened during a battle. The frontend animates from these.</summary>
public abstract record BattleEvent(int Tick);
