using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.WinForms;
using SnapCraft;

internal static class InputLatencyTest
{
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    public static async Task RunAsync(MainForm form, WebView2 web)
    {
        var manager = (ShortcutManager)typeof(MainForm).GetField("shortcuts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        // Avoid the installed application's shortcuts. Exercise Windows message dispatch without sending keys to user applications.
        manager.Update("area", new ShortcutBinding(7, (uint)Keys.F10));
        var hotkeyId = new[] { 3, 4 }.Single(id => manager.ActionFor(Message.Create(form.Handle, 0x0312, new IntPtr(id), IntPtr.Zero)) == "area");
        foreach (var source in new[] { "button-area", "button-window", "button-scroll", "hotkey-message-area" })
        {
            var clock = Stopwatch.StartNew();
            Task? script = null;
            SelectionOverlay? overlay = null;
            try
            {
                if (source.StartsWith("button-"))
                    script = web.ExecuteScriptAsync($"document.querySelector('[data-action={source[7..]}]').click()");
                else if (!PostMessage(form.Handle, 0x0312, new IntPtr(hotkeyId), IntPtr.Zero))
                    throw new Exception("Could not post test hotkey message");
                while ((overlay = Application.OpenForms.OfType<SelectionOverlay>().FirstOrDefault(item => item.Visible)) is null)
                {
                    if (clock.ElapsedMilliseconds > 3000) throw new Exception($"{source}: selection did not appear within 3 seconds");
                    await Task.Delay(10);
                }
                Console.WriteLine($"input-to-selection {source}: {clock.ElapsedMilliseconds} ms");
            }
            finally
            {
                overlay?.Close();
                if (script is not null) await script;
            }
            for (var attempt = 0; !form.Visible; attempt++)
            {
                if (attempt > 200) throw new Exception("Capture cancellation did not restore launcher");
                await Task.Delay(10);
            }
            await Task.Delay(100);
        }
    }
}
