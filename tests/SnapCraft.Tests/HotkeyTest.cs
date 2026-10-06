using SnapCraft;

internal static class HotkeyTest
{
    public static void Run()
    {
        if (!GlobalHotkey.IsValid(2, (uint)Keys.PrintScreen) || GlobalHotkey.IsValid(0, (uint)Keys.PrintScreen)) throw new Exception("Ctrl+PrintScreen validation failed");
        if (new AppSettings().GetShortcuts()["area"] != new ShortcutBinding(2, (uint)Keys.PrintScreen)) throw new Exception("New install capture shortcut is incorrect");
        var existing = new AppSettings { Shortcuts = new() { ["area"] = new(6, 65) } };
        if (existing.GetShortcuts()["area"] != new ShortcutBinding(6, 65)) throw new Exception("Existing shortcut was overwritten");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var first = new Form();
                using var second = new Form();
                using var binding = new GlobalHotkey(first.Handle);
                using var conflict = new GlobalHotkey(second.Handle);
                binding.Set(7, 121);
                binding.Set(7, 121);
                conflict.Set(7, 120);
                try { binding.Set(7, 120); throw new Exception("Expected hotkey collision"); }
                catch (InvalidOperationException) { }
                var message = Message.Create(first.Handle, 0x0312, new IntPtr(1), IntPtr.Zero);
                if (!binding.Matches(message)) throw new Exception("Old hotkey lost after collision");
                binding.Dispose();
                conflict.Set(7, 121);
                var modes = new[] { "launcher", "area", "window", "scroll", "video" };
                var settings = modes.Select((mode, index) => (mode, binding: new ShortcutBinding(7, (uint)(112 + index)))).ToDictionary(item => item.mode, item => item.binding);
                using var manager = new ShortcutManager(first.Handle, settings);
                if (manager.Errors.Count > 0) throw new Exception("Mode shortcut registration failed");
                for (var index = 0; index < modes.Length; index++)
                    if (manager.ActionFor(Message.Create(first.Handle, 0x0312, new IntPtr(index * 2 + 1), IntPtr.Zero)) != modes[index]) throw new Exception("Wrong shortcut action routed");
                try { manager.Update("window", settings["area"]); throw new Exception("Expected mode collision"); }
                catch (InvalidOperationException) { }
                if (manager.ActionFor(Message.Create(first.Handle, 0x0312, new IntPtr(5), IntPtr.Zero)) != "window") throw new Exception("Mode lost on conflict");
                manager.Update("window", settings["window"] with { Enabled = false });
                if (manager.ActionFor(Message.Create(first.Handle, 0x0312, new IntPtr(5), IntPtr.Zero)) is not null) throw new Exception("Disabled shortcut active");
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (failure is not null) throw failure;
    }
}
