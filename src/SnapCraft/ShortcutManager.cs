namespace SnapCraft;

internal sealed class ShortcutManager : IDisposable
{
    private readonly Dictionary<string, GlobalHotkey> bindings = new();
    public Dictionary<string, string> Errors { get; } = new();

    public ShortcutManager(IntPtr window, IReadOnlyDictionary<string, ShortcutBinding> settings)
    {
        var id = 1;
        foreach (var action in new[] { "launcher", "area", "window", "scroll", "video" })
        {
            bindings[action] = new GlobalHotkey(window, id);
            id += 2;
            if (!settings.TryGetValue(action, out var binding)) continue;
            try { Update(action, binding); }
            catch (Exception error) { Errors[action] = error.Message; }
        }
    }

    public void Update(string action, ShortcutBinding binding)
    {
        if (!bindings.TryGetValue(action, out var hotkey)) throw new ArgumentException("ไม่รู้จักโหมดคีย์ลัด");
        if (binding.Enabled) hotkey.Set(binding.Modifiers, binding.Key);
        else hotkey.Dispose();
        Errors.Remove(action);
    }

    public string? ActionFor(Message message) => bindings.FirstOrDefault(entry => entry.Value.Matches(message)).Key;
    public void Dispose() { foreach (var hotkey in bindings.Values) hotkey.Dispose(); }
}
