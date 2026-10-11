using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using ItemFighter.Input;
using ItemFighter.Helpers;
using System.Collections.Generic;

namespace ItemFighter.Screens;

public class MainMenuScreen : Screen
{
    private SpriteFont _font;
    private GraphicsDeviceManager _graphics;
    private ShapeDrawer _shapes;
    private List<Texture2D> _textures;
    private int _selectionIndex = 0;
    private List<string> _strings;
    public MainMenuScreen(SpriteFont font, GraphicsDeviceManager graphics, ShapeDrawer shapes, List<Texture2D> textures)
    {
        _font = font;
        _graphics = graphics;
        _shapes = shapes;
        _textures = textures;
        _strings = new List<string>() {"Start Game", "Exit"};
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin();
        for (int i = 0; i < _strings.Count; i++)
        {
            Color color = (i == _selectionIndex) ? Color.Yellow : Color.White;
            spriteBatch.DrawString(_font, _strings[i], new Vector2(100, 100 + i * 30), color);
        }
        spriteBatch.End();
    }

    public override void HandleInput(InputState inputState)
    {
        
    }
}