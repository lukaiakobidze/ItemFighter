using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ItemFighter.Platform;

/// <summary>
/// Mouse and keyboard in virtual coordinates. Presses are collected across updates and cleared after each drawn
/// frame, so the immediate-mode UI never misses a click.
/// </summary>
public sealed class Input
{
    private MouseState _lastMouse;
    private KeyboardState _lastKeys;
    private readonly HashSet<Keys> _pressed = [];

    public Vector2 Mouse { get; private set; } = new(-1, -1);
    public bool LeftClicked { get; private set; }
    public bool RightClicked { get; private set; }
    public int Wheel { get; private set; }

    public bool KeyPressed(Keys key) => _pressed.Contains(key);

    public void Poll(VirtualScreen screen, Point windowSize, bool windowActive)
    {
        var mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
        var keys = Keyboard.GetState();
        Mouse = screen.ToVirtual(mouse.Position, windowSize);
        if (windowActive)
        {
            if (mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released) LeftClicked = true;
            if (mouse.RightButton == ButtonState.Pressed && _lastMouse.RightButton == ButtonState.Released) RightClicked = true;
            Wheel += Math.Sign(mouse.ScrollWheelValue - _lastMouse.ScrollWheelValue);
            foreach (var k in keys.GetPressedKeys())
                if (!_lastKeys.IsKeyDown(k)) _pressed.Add(k);
        }
        _lastMouse = mouse;
        _lastKeys = keys;
    }

    /// <summary>Automation: move the mouse and click without real hardware.</summary>
    public void Inject(Vector2 mouse, bool left = false, bool right = false, Keys? key = null)
    {
        Mouse = mouse;
        LeftClicked |= left;
        RightClicked |= right;
        if (key is Keys k) _pressed.Add(k);
    }

    /// <summary>Lets one press be handled once: whoever reacts to a click consumes it.</summary>
    public void ConsumeClicks()
    {
        LeftClicked = false;
        RightClicked = false;
    }

    public void EndFrame()
    {
        LeftClicked = RightClicked = false;
        Wheel = 0;
        _pressed.Clear();
    }
}
