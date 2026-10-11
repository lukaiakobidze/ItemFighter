using ItemFighter.Core.Engine;

namespace ItemFighter.FightView;

/// <summary>Plays a battle in real time: 10 ticks per second times the chosen speed.</summary>
public sealed class FightClock(Battle battle)
{
    /// <summary>Most ticks played per frame, so a long hitch can't skip a whole fight.</summary>
    public const int MaxStepsPerAdvance = 40;

    private double _pending;

    public int Speed { get; set; } = 1;

    /// <summary>While paused no ticks are played and no time is banked.</summary>
    public bool Paused { get; set; }

    /// <summary>How far (0..1) the clock is towards the next tick, for smooth bars.</summary>
    public double Fraction => battle.Result == BattleResult.Ongoing ? _pending : 0;

    public int Advance(double seconds)
    {
        if (battle.Result != BattleResult.Ongoing || Paused) return 0;
        _pending += seconds * 10 * Speed;
        int steps = (int)(_pending + 1e-9);
        _pending -= steps;
        if (steps > MaxStepsPerAdvance) steps = MaxStepsPerAdvance;
        int done = 0;
        for (; done < steps && battle.Result == BattleResult.Ongoing; done++) battle.Step();
        return done;
    }
}

/// <summary>Hands out the battle's events once each, in order.</summary>
public sealed class EventCursor(Battle battle)
{
    private int _next;

    public IReadOnlyList<BattleEvent> TakeNew()
    {
        var events = battle.Events;
        if (_next >= events.Count) return [];
        var slice = new List<BattleEvent>(events.Count - _next);
        for (int i = _next; i < events.Count; i++) slice.Add(events[i]);
        _next = events.Count;
        return slice;
    }
}
