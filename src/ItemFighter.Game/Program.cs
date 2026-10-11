using ItemFighter.Platform;

// A Windows build has no console, so an unexpected crash is written to crash.log next to the saves.
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    try
    {
        Directory.CreateDirectory(Paths.Saves);
        File.AppendAllText(Path.Combine(Paths.Saves, "crash.log"), $"{DateTime.Now:u}\n{e.ExceptionObject}\n\n");
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
};

using var game = new ItemFighter.Game1(AutomationOptions.Parse(args));
game.Run();
