using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using SnapCraft;

internal static class StartupTest
{
    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);

    public static void RunAssets(string root)
    {
        var directory = Path.Combine(root, "assets-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(directory, "source");
        var destination = Path.Combine(directory, "web");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        var input = Path.Combine(source, "nested", "ui.txt");
        File.WriteAllText(input, "original");
        File.SetLastWriteTimeUtc(input, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        if (WebAssets.CopyUpdatedFiles(source, destination) != 1) throw new Exception("Initial asset copy failed");
        var output = Path.Combine(destination, "nested", "ui.txt");
        var creation = File.GetCreationTimeUtc(output);
        if (WebAssets.CopyUpdatedFiles(source, destination) != 0 || File.GetCreationTimeUtc(output) != creation)
            throw new Exception("Unchanged assets were rewritten");
        File.WriteAllText(input, "modified");
        File.SetLastWriteTimeUtc(input, new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc));
        if (WebAssets.CopyUpdatedFiles(source, destination) != 1 || File.ReadAllText(output) != "modified")
            throw new Exception("Same-length changed asset did not update");
        File.Delete(output);
        if (WebAssets.CopyUpdatedFiles(source, destination) != 1) throw new Exception("Missing asset was not restored");
        using var icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", "app.ico"), 48, 48);
        using var bitmap = icon.ToBitmap();
        var blue = bitmap.GetPixel(8, 24);
        if (blue.B < 230 || blue.R > 40 || blue.G < 80 || blue.G > 130) throw new Exception("App ICO is not the new blue icon");
        Console.WriteLine("startup asset cache / update / repair / blue ICO: pass");
    }

    public static Task RunLauncherAsync(bool measureInput = false, bool startInTray = false, bool verifyControls = false)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            WebAssets.Prepare();
            using var form = new MainForm(startInTray);
            var tray = (NotifyIcon)typeof(MainForm).GetField("tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            using var startupTimer = new System.Windows.Forms.Timer { Interval = 300 };
            if (startInTray)
            {
                startupTimer.Tick += (_, _) =>
                {
                    startupTimer.Stop();
                    if (form.Visible || !tray.Visible || !form.IsHandleCreated)
                    {
                        completion.TrySetException(new Exception("Tray startup showed a window or failed to initialize tray/hotkeys"));
                        tray.ContextMenuStrip!.Items[^1].PerformClick();
                        return;
                    }
                    Console.WriteLine("silent tray startup: hidden window, live handle and tray icon: pass");
                    form.ShowLauncher();
                };
                startupTimer.Start();
            }
            var clock = Stopwatch.StartNew();
            form.Shown += async (_, _) =>
            {
                try
                {
                    var web = form.Controls.OfType<WebView2>().Single();
                    for (var attempt = 0; attempt < 200; attempt++)
                    {
                        await Task.Delay(50);
                        if (web.CoreWebView2 is not null && await web.ExecuteScriptAsync($"document.querySelector('#version')?.textContent === {JsonSerializer.Serialize($"v{AppInfo.Version}")}") == "true") break;
                        if (attempt == 199) throw new Exception("Launcher did not become ready");
                    }
                    Console.WriteLine($"launcher cold UI ready: {clock.ElapsedMilliseconds} ms");
                    if (verifyControls) await VerifyControlsAsync(web);
                    if (measureInput) await InputLatencyTest.RunAsync(form, web);
                    if (form.TopMost) throw new Exception("Launcher must not permanently cover installer or other windows");
                    if (form.ClientSize.Width > 400 || form.ClientSize.Height > 170) throw new Exception("Launcher did not restore its compact size");
                    form.Close();
                    if (form.Visible || form.IsDisposed) throw new Exception("Close-to-tray behavior changed");
                    clock.Restart();
                    typeof(NotifyIcon).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(tray, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                    if (!form.Visible || form.WindowState != FormWindowState.Normal) throw new Exception("Single tray click did not reopen launcher");
                    Console.WriteLine($"tray single-click restore: {clock.ElapsedMilliseconds} ms");
                    form.WindowState = FormWindowState.Minimized;
                    await Task.Delay(100);
                    form.ShowLauncher();
                    if (form.WindowState != FormWindowState.Normal) throw new Exception("Minimized launcher did not restore");
                    if (form.TopMost) throw new Exception("Reopening launcher must not make it permanently topmost");
                    form.Hide();
                    typeof(NotifyIcon).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(tray, new object[] { new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0) });
                    if (form.Visible) throw new Exception("Right tray click unexpectedly reopened launcher");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally { tray.ContextMenuStrip!.Items[^1].PerformClick(); }
            };
            Application.Run(form);
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(25));
    }

    private static async Task VerifyControlsAsync(WebView2 web)
    {
        async Task WaitForAsync(string expression)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (await web.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(50);
            }
            throw new Exception($"Native launcher did not acknowledge: {expression}");
        }
        await web.ExecuteScriptAsync("if(document.documentElement.lang !== 'en') document.querySelector('#languageToggle').click()");
        await WaitForAsync("document.documentElement.lang === 'en'");
        await web.ExecuteScriptAsync("document.querySelector('#languageToggle').click()");
        await WaitForAsync("document.documentElement.lang === 'th'");
        for (var attempt = 0; attempt < 100 && AppSettings.Load().Language != "th"; attempt++) await Task.Delay(50);
        if (AppSettings.Load().Language != "th") throw new Exception("Language toggle did not persist through the native host.");
        await web.ExecuteScriptAsync("document.querySelector('#languageToggle').click(); document.querySelector('#shortcutButton').click(); document.querySelector('#hotkeyEnabled').checked=false; document.querySelector('#saveShortcut').click()");
        await WaitForAsync("document.querySelector('#shortcutSavedDialog').open && !document.querySelector('#shortcutDialog').open");
        if (AppSettings.Load().GetShortcuts()["launcher"].Enabled) throw new Exception("Shortcut confirmation appeared without saving the binding.");
        await web.ExecuteScriptAsync("document.querySelector('#closeShortcutSaved').click()");
        await WaitForAsync("!document.querySelector('#shortcutSavedDialog').open");
        Console.WriteLine("native UI: compact language toggle persists, confirmed hotkey save popup: pass");
    }

    public static Task RunSingleInstanceAsync(string root)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var host = new Form { Text = "Neo Snap activation test", Size = new Size(240, 100), StartPosition = FormStartPosition.Manual, Location = new Point(40, 40), TopMost = true };
            host.Shown += async (_, _) =>
            {
                var cursor = Cursor.Position;
                try { await TestProcessesAsync(root, host); completion.TrySetResult(); }
                catch (Exception error) { completion.TrySetException(error); }
                finally { Cursor.Position = cursor; host.Close(); }
            };
            Application.Run(host);
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(40));
    }

    private static async Task TestProcessesAsync(string root, Form host)
    {
        var executable = Path.ChangeExtension(typeof(AppInfo).Assembly.Location, ".exe");
        if (!File.Exists(executable)) throw new Exception("Application EXE is missing from test output");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        info.Environment["SNAPCRAFT_DATA_DIR"] = Path.Combine(root, "single-instance-" + Guid.NewGuid().ToString("N"));
        using var primary = Process.Start(info) ?? throw new Exception("Primary process did not start");
        try
        {
            var window = IntPtr.Zero;
            await WaitAsync(() =>
            {
                EnumWindows((handle, _) =>
                {
                    GetWindowThreadProcessId(handle, out var pid);
                    if (pid == primary.Id && IsWindowVisible(handle)) window = handle;
                    return window == IntPtr.Zero;
                }, IntPtr.Zero);
                return window != IntPtr.Zero;
            }, "Primary window did not appear");
            await Task.Delay(1200);
            foreach (var command in new[] { 0, 6, 0 })
            {
                ShowWindowAsync(window, command);
                await WaitAsync(() => command == 6 ? IsIconic(window) : !IsWindowVisible(window), "Could not hide/minimize primary");
                // A user-initiated taskbar/shortcut launch transfers permission from its foreground process.
                host.BringToFront();
                Cursor.Position = host.PointToScreen(new Point(20, 20));
                mouse_event(0x02 | 0x04, 0, 0, 0, UIntPtr.Zero);
                await WaitAsync(() => GetForegroundWindow() == host.Handle, "Test launch host did not receive the click");
                var clock = Stopwatch.StartNew();
                using var secondary = Process.Start(info) ?? throw new Exception("Secondary process did not start");
                await secondary.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                if (secondary.ExitCode != 0) throw new Exception("Secondary instance did not exit cleanly");
                await WaitAsync(() => IsWindowVisible(window) && !IsIconic(window), "Reopening EXE did not restore original window");
                await WaitAsync(() =>
                {
                    GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundPid);
                    return foregroundPid == primary.Id;
                }, "Reopened launcher is not in the foreground");
                Console.WriteLine($"single-instance reopen (command {command}): {clock.ElapsedMilliseconds} ms; original PID {primary.Id}");
            }
            if (primary.HasExited) throw new Exception("Primary instance unexpectedly exited");
        }
        finally
        {
            // Only this test-owned process is terminated; the user's installed app is untouched.
            if (!primary.HasExited) { primary.Kill(true); await primary.WaitForExitAsync(); }
        }
    }

    private static async Task WaitAsync(Func<bool> condition, string failure)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(10)) throw new Exception(failure);
            await Task.Delay(25);
        }
    }
}
