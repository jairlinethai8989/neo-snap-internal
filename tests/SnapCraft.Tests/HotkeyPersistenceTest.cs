using System.IO;
using System.Reflection;
using SnapCraft;

internal static class HotkeyPersistenceTest
{
    public static void Run()
    {
        var update = typeof(ShortcutManager).GetMethod("UpdateAndSave")
            ?? throw new Exception("Hotkey changes need a transactional UpdateAndSave operation.");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previous = File.Exists(WebAssets.SettingsPath) ? File.ReadAllBytes(WebAssets.SettingsPath) : null;
            try
            {
                using var window = new Form();
                var original = new ShortcutBinding(7, 122);
                var settings = new AppSettings { Shortcuts = new() { ["area"] = original } };
                settings.Save();
                var saved = File.ReadAllBytes(WebAssets.SettingsPath);
                using var manager = new ShortcutManager(window.Handle, new Dictionary<string, ShortcutBinding> { ["area"] = original });
                if (manager.Errors.Count != 0) throw new Exception("Isolated test shortcut is unavailable.");
                foreach (var next in new[] { original with { Enabled = false }, original with { Key = 119 } })
                {
                    using (var locked = new FileStream(WebAssets.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        try { update.Invoke(manager, [settings, "area", next]); throw new Exception("Locked settings write must fail."); }
                        catch (TargetInvocationException error) when (error.InnerException is IOException or UnauthorizedAccessException) { }
                        if (settings.GetShortcuts()["area"] != original) throw new Exception("In-memory binding was not restored.");
                        if (!File.ReadAllBytes(WebAssets.SettingsPath).SequenceEqual(saved)) throw new Exception("Failed save changed persisted settings.");
                        using var contender = new GlobalHotkey(window.Handle, 50);
                        try { contender.Set(original.Modifiers, original.Key); throw new Exception("Original native binding was not restored."); }
                        catch (InvalidOperationException) { }
                    }
                    if (Directory.GetFiles(WebAssets.DataRoot, "*.tmp").Length != 0) throw new Exception("Failed settings save leaked a temporary file.");
                }
                var disabled = original with { Enabled = false };
                update.Invoke(manager, [settings, "area", disabled]);
                if (AppSettings.Load().GetShortcuts()["area"] != disabled) throw new Exception("Successful save was not persisted.");
                using var reusable = new GlobalHotkey(window.Handle, 60);
                reusable.Set(original.Modifiers, original.Key);
                Console.WriteLine("hotkey save: locked-file rollback / active binding / atomic file / successful disable: pass");
            }
            catch (Exception error) { failure = error; }
            finally
            {
                if (previous is not null) File.WriteAllBytes(WebAssets.SettingsPath, previous);
                else File.Delete(WebAssets.SettingsPath);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (failure is not null) throw failure;
    }
}
