using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ItemFighter.Input;

namespace ItemFighter.Screens;

public abstract class Screen
{
    public abstract void Update(GameTime gameTime);
    public abstract void Draw(SpriteBatch spriteBatch);
    public abstract void HandleInput(InputState inputState);
    
}