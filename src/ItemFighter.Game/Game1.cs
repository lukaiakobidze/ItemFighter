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
    private RenderTarget2D _target = null!;
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
        Window.ClientSizeChanged += (_, _) =>
        {
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

    public void Show(Screen screen) => _current = screen;

    /// <summary>Re-reads every JSON file and PNG, so edits show up without restarting.</summary>
    public void ReloadContent()
    {
        Data = GameContent.Load(Paths.Data);
        Assets.ClearArt();
        Show(Data.Errors.Count > 0 ? new ContentErrorScreen(this) : new MainMenuScreen(this));
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _target = new RenderTarget2D(GraphicsDevice, _screen.Width, _screen.Height);
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
        if (_input.KeyPressed(Keys.F5)) ReloadContent();
        _current?.Update(dt);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(_target);
        GraphicsDevice.Clear(Palette.Background);
        _batch.Begin(samplerState: SamplerState.LinearClamp);
        _current?.Draw(_ui);
        _ui.DrawTooltip();
        _batch.End();
        _input.EndFrame();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _batch.Begin(samplerState: SamplerState.LinearClamp);
        _batch.Draw(_target, _screen.Destination(new Point(GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight)), Color.White);
        _batch.End();

        _automation?.AfterDraw(this, _target);
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        Assets.Dispose();
        _target.Dispose();
        base.UnloadContent();
    }
}
