namespace ItemFighter.Platform;

/// <summary>
/// Where data and art are read from. Running from the source tree reads the repository folders directly, so
/// edited JSON and new PNGs show up after F5 without a rebuild; a shipped build uses the copies next to the exe.
/// </summary>
public static class Paths
{
    public static string Fonts => Path.Combine(AppContext.BaseDirectory, "Content", "Fonts");

    public static string Data => FindInRepo(Path.Combine("src", "ItemFighter.Content", "Data")) ?? Path.Combine(AppContext.BaseDirectory, "Data");

    public static string Art => FindInRepo(Path.Combine("src", "ItemFighter.Game", "Content", "Art")) ?? Path.Combine(AppContext.BaseDirectory, "Content", "Art");

    /// <summary>Automation points this elsewhere so scripted runs never touch the player's saved lab.</summary>
    public static string? SavesOverride { get; set; }

    public static string Saves => SavesOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ItemFighter");

    private static string? FindInRepo(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, relative);
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }
}
