using ItemFighter.Core.Engine;

namespace ItemFighter.FightView;

/// <summary>
/// What one side's bars show, eased so they never jump (Luka 2026-10-20). HP drops at once but leaves a white trail
/// where it was, which holds for <see cref="TrailHoldSeconds"/> and then drains smoothly; shield and Heat glide to
/// their new values.
/// </summary>
public sealed class BarAnimator
{
    /// <summary>How long the white HP trail stays before it drains.</summary>
    public const double TrailHoldSeconds = 0.5;
    /// <summary>How fast a gliding value closes the gap (per second, as a share of what's left).</summary>
    public const float GlideRate = 12f;
    /// <summary>Slowest glide, as a share of the full bar per second, so the last bit doesn't crawl.</summary>
    public const float MinGlidePerSecond = 0.6f;

    private enum TrailPhase { Idle, Holding, Draining }

    private TrailPhase _phase;
    private double _holdLeft;
    private bool _started;

    /// <summary>Where the white trail ends (it shows from the real HP up to here).</summary>
    public float Trail { get; private set; }
    public float Shield { get; private set; }
    public float Heat { get; private set; }

    public void Update(Combatant c, int heatMax, double seconds)
    {
        float hp = Math.Max(0, c.Hp);
        if (!_started)
        {
            (Trail, Shield, Heat, _started) = (hp, c.Shield, c.Statuses.Heat, true);
            return;
        }
        if (hp >= Trail) (Trail, _phase) = (hp, TrailPhase.Idle);
        else if (_phase == TrailPhase.Idle) (_phase, _holdLeft) = (TrailPhase.Holding, TrailHoldSeconds);
        else if (_phase == TrailPhase.Holding)
        {
            _holdLeft -= seconds;
            if (_holdLeft <= 0) _phase = TrailPhase.Draining;
        }
        else
        {
            Trail = Glide(Trail, hp, c.MaxHp, seconds);
            if (Trail <= hp) _phase = TrailPhase.Idle;
        }
        Shield = Glide(Shield, c.Shield, c.MaxHp, seconds);
        Heat = Glide(Heat, c.Statuses.Heat, heatMax, seconds);
    }

    private static float Glide(float shown, float target, float full, double seconds)
    {
        float gap = target - shown;
        if (gap == 0) return target;
        float step = Math.Max(Math.Abs(gap) * GlideRate, full * MinGlidePerSecond) * (float)seconds;
        return Math.Abs(gap) <= step ? target : shown + Math.Sign(gap) * step;
    }
}
