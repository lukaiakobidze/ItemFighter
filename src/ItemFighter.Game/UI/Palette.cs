using ItemFighter.Core.Content;
using ItemFighter.Core.Engine;
using Microsoft.Xna.Framework;

namespace ItemFighter.UI;

public static class Palette
{
    public static readonly Color Background = new(22, 24, 32);
    public static readonly Color Panel = new(34, 37, 50);
    public static readonly Color PanelLight = new(48, 52, 70);
    public static readonly Color Border = new(78, 84, 110);
    public static readonly Color Text = new(230, 232, 240);
    public static readonly Color TextDim = new(150, 156, 178);
    public static readonly Color Accent = new(255, 196, 64);
    public static readonly Color Hp = new(204, 56, 64);
    public static readonly Color HpBack = new(60, 26, 32);
    public static readonly Color Shield = new(120, 170, 230);
    public static readonly Color Heal = new(90, 210, 110);
    public static readonly Color Damage = new(255, 90, 80);
    public static readonly Color Burn = new(255, 140, 40);
    public static readonly Color Poison = new(176, 96, 230);
    public static readonly Color Chill = new(110, 220, 240);
    public static readonly Color Freeze = new(170, 230, 255);
    public static readonly Color Haste = new(130, 240, 130);
    public static readonly Color Slow = new(200, 170, 110);
    public static readonly Color Ready = new(255, 214, 90);
    public static readonly Color Valid = new(110, 230, 140);

    public static Color For(Rarity r) => r switch
    {
        Rarity.Common => new Color(170, 170, 180),
        Rarity.Uncommon => new Color(90, 200, 110),
        Rarity.Rare => new Color(80, 150, 240),
        Rarity.Epic => new Color(180, 100, 240),
        _ => new Color(250, 170, 50),
    };

    public static Color For(StatusType s) => s switch
    {
        StatusType.Burn => Burn,
        StatusType.Poison => Poison,
        StatusType.Chill => Chill,
        StatusType.Freeze => Freeze,
        StatusType.Haste => Haste,
        _ => Slow,
    };

    public static Color For(DamageKind k) => k switch
    {
        DamageKind.Burn => Burn,
        DamageKind.Poison => Poison,
        _ => Damage,
    };

    /// <summary>A stable, muted colour per id for placeholder art.</summary>
    public static Color PlaceholderFor(string id)
    {
        uint h = 2166136261;
        foreach (char c in id) h = (h ^ c) * 16777619;
        return new Color(60 + (int)(h % 90), 60 + (int)((h >> 8) % 90), 70 + (int)((h >> 16) % 90));
    }
}
