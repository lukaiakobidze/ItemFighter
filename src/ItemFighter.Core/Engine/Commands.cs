namespace ItemFighter.Core.Engine;

/// <summary>Player input, stamped with the tick it applies on. Recorded commands replay a fight exactly.</summary>
public interface ICommand
{
    int Tick { get; }
}

/// <summary>Click a ready active item, optionally on a target item.</summary>
public sealed record ActivateCommand(int Tick, int SourceId, int? TargetSourceId) : ICommand;

public static class RejectReasons
{
    public const string Late = "late";
    public const string UnknownSource = "unknown_source";
    public const string NotActive = "not_active";
    public const string Frozen = "frozen";
    public const string Stalled = "stalled";
    public const string NotReady = "not_ready";
    public const string TargetRequired = "target_required";
    public const string InvalidTarget = "invalid_target";
}
