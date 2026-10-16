using ItemFighter.Content;
using ItemFighter.Platform;
using ItemFighter.Screens;
using ItemFighter.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ItemFighter;

public sealed class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly VirtualScreen _screen = new();
    private readonly Input _input = new();
    private readonly Automation? _automation;
    private SpriteBatch _batch = null!;
    private RenderTarget2D? _target;
    private Ui _ui = null!;
    private Screen? _current;

    public Game1(AutomationOptions? automation = null)
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true,
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "ItemFighter";
        if (automation is null) SetFullScreen(true, apply: false); // applied when the device is created; screenshots stay 1280×720
        Window.ClientSizeChanged += (_, _) =>
        {
            if (_graphics.IsFullScreen) return;
            var size = Window.ClientBounds;
            if (size.Width <= 0 || size.Height <= 0) return;
            if (_graphics.PreferredBackBufferWidth == size.Width && _graphics.PreferredBackBufferHeight == size.Height) return;
            _graphics.PreferredBackBufferWidth = size.Width;
            _graphics.PreferredBackBufferHeight = size.Height;
            _graphics.ApplyChanges();
        };
        if (automation is not null) _automation = new Automation(automation);
        Data = GameContent.Load(Paths.Data);
    }

    public LoadedContent Data { get; private set; }
    public Assets Assets { get; private set; } = null!;
    public Input Input => _input;
    public Screen? Current => _current;

    /// <summary>Borderless fullscreen at the desktop's resolution, or a 1280×720 window. F11 or Alt+Enter toggles.</summary>
    public void SetFullScreen(bool on, bool apply = true)
    {
        var desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = on;
        _graphics.PreferredBackBufferWidth = on ? desktop.Width : 1280;
        _graphics.PreferredBackBufferHeight = on ? desktop.Height : 720;
        if (apply) _graphics.ApplyChanges();
    }

    public void Show(Screen screen)
    {
        _current?.OnLeave();
        _current = screen;
    }

    /// <summary>Re-reads every JSON file and PNG, so edits show up without restarting. The lab stays open.</summary>
    public void ReloadContent()
    {
        bool inLab = _current is FightLabScreen;
        _current?.OnLeave();
        _current = null;
        Data = GameContent.Load(Paths.Data);
        Assets.ClearArt();
        Show(Data.Errors.Count > 0 ? new ContentErrorScreen(this) : inLab ? new FightLabScreen(this) : new MainMenuScreen(this));
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        Assets = new Assets(GraphicsDevice, Paths.Fonts, Paths.Art);
        _ui = new Ui(_batch, Assets, _input, _screen);
        Show(Data.Errors.Count > 0 ? new ContentErrorScreen(this) : new MainMenuScreen(this));
    }

    protected override void Update(GameTime gameTime)
    {
        double dt = _automation is null ? gameTime.ElapsedGameTime.TotalSeconds : 1.0 / 60;
        _ui.Time += dt;
        if (_automation is null)
            _input.Poll(_screen, new Point(Window.ClientBounds.Width, Window.ClientBounds.Height), IsActive);
        else
            _automation.Update(this, dt);
        if (_input.KeyPressed(Keys.F5))
        {
            _input.Consume(Keys.F5); // several updates can run per drawn frame
            ReloadContent();
        }
        var keys = Keyboard.GetState();
        if (_automation is null && (_input.KeyPressed(Keys.F11) || (_input.KeyPressed(Keys.Enter) && (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt)))))
        {
            _input.Consume(Keys.F11);
            _input.Consume(Keys.Enter);
            SetFullScreen(!_graphics.IsFullScreen);
        }
        _current?.Update(dt);
        base.Update(gameTime);
    }

    /// <summary>
    /// The layout is 1600×900 virtual pixels, but it is drawn straight at the window's real resolution (scaled by a
    /// transform, text rasterised at the real size), so nothing is upscaled from a small image.
    /// </summary>
    protected override void Draw(GameTime gameTime)
    {
        var window = new Point(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
        var dest = _automation is null ? _screen.Destination(window) : _screen.Bounds;
        float scale = dest.Width / (float)_screen.Width;
        if (_target is null || _target.Width != dest.Width || _target.Height != dest.Height)
        {
            _target?.Dispose();
            _target = new RenderTarget2D(GraphicsDevice, dest.Width, dest.Height);
        }
        _ui.Scale = scale;

        GraphicsDevice.SetRenderTarget(_target);
        GraphicsDevice.Clear(Palette.Background);
        _batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: Matrix.CreateScale(scale, scale, 1));
        _ui.Picture("background", _screen.Bounds, cover: true);
        _current?.Draw(_ui);
        _ui.DrawTooltip();
        _batch.End();
        _input.EndFrame();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_target, dest, Color.White); // same size: a 1:1 copy
        _batch.End();

        _automation?.AfterDraw(this, _target);
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        Assets.Dispose();
        _target?.Dispose();
        base.UnloadContent();
    }
}
