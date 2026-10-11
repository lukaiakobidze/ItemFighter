using ItemFighter.Core.Engine;

namespace ItemFighter.Content.Scripts;

/// <summary>Activate on an item: the next time that item fires on its own, it fires a second time.</summary>
[SourceScript("MirrorShard")]
public sealed class MirrorShard : ISourceScript
{
    private int? _marked;

    public void OnEvent(BattleEvent e, SourceInstance self, Battle battle, int depth)
    {
        if (e is not SourceFired { Reaction: false } fired) return;
        if (fired.SourceId == self.Id)
        {
            _marked = fired.TargetSourceId;
            return;
        }
        if (_marked != fired.SourceId) return;
        _marked = null;
        var target = battle.FindSource(fired.SourceId);
        if (target is not null) battle.Fire(target, depth, null);
    }
}
