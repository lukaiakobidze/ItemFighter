using ItemFighter.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ItemFighter.Platform;

/// <summary>Command line: --screenshot &lt;dir&gt; --script &lt;name&gt; plays a scripted session, saves PNGs and quits.</summary>
public sealed record AutomationOptions(string ScreenshotDirectory, string Script)
{
    public static AutomationOptions? Parse(string[] args)
    {
        string? dir = null, script = null;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--screenshot") dir = args[i + 1];
            if (args[i] == "--script") script = args[i + 1];
        }
        return dir is null ? null : new AutomationOptions(dir, script ?? "menu");
    }
}

/// <summary>
/// Runs a list of steps on a fixed 60 fps clock. Each step waits, optionally until a condition holds, then
/// acts (usually a fake click) and/or takes a screenshot.
/// </summary>
public sealed class Automation
{
    public sealed record Step(double Wait = 0, Func<Game1, bool>? Until = null, Action<Game1>? Do = null, string? Shot = null);

    private const double UntilTimeout = 180;
    private readonly AutomationOptions _options;
    private readonly IReadOnlyList<Step> _steps;
    private int _index;
    private double _elapsed;
    private string? _pendingShot;

    public Automation(AutomationOptions options)
    {
        _options = options;
        _steps = AutomationScripts.Get(options.Script);
        Directory.CreateDirectory(options.ScreenshotDirectory);
        Paths.SavesOverride = Path.Combine(options.ScreenshotDirectory, "saves");
        if (Directory.Exists(Paths.SavesOverride)) Directory.Delete(Paths.SavesOverride, recursive: true);
    }

    public void Update(Game1 game, double dt)
    {
        if (_pendingShot is not null || _index >= _steps.Count) return;
        _elapsed += dt;
        var step = _steps[_index];
        if (_elapsed < step.Wait) return;
        if (step.Until is not null && !step.Until(game))
        {
            if (_elapsed < UntilTimeout) return;
            Console.WriteLine($"automation: step {_index} timed out");
        }
        step.Do?.Invoke(game);
        _pendingShot = step.Shot;
        _index++;
        _elapsed = 0;
    }

    public void AfterDraw(Game1 game, RenderTarget2D frame)
    {
        if (_pendingShot is not null)
        {
            using var file = File.Create(Path.Combine(_options.ScreenshotDirectory, _pendingShot + ".png"));
            frame.SaveAsPng(file, frame.Width, frame.Height);
            Console.WriteLine($"screenshot: {_pendingShot}.png");
            _pendingShot = null;
        }
        if (_index >= _steps.Count) game.Exit();
    }

    public static Action<Game1> Click(Rectangle r, bool right = false) =>
        g => g.Input.Inject(r.Center.ToVector2(), left: !right, right: right);

    public static Action<Game1> Hover(Rectangle r) => g => g.Input.Inject(r.Center.ToVector2());

    public static Action<Game1> Key(Keys k) => g => g.Input.Inject(g.Input.Mouse, key: k);
}
