using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SnapCraft;

internal sealed class MainForm : Form
{
    [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    private readonly WebView2 web = new() { Dock = DockStyle.Fill };
    private readonly CaptureCoordinator capture = new();
    private readonly AppSettings settings = AppSettings.Load();
    private EditorHubForm? tabHub;
    private VideoSession? video;
    private readonly System.Windows.Forms.Timer recordingTimer = new() { Interval = 250 };
    private readonly NotifyIcon tray = new();
    private bool busy;
    private bool closingAfterVideo;

    public MainForm()
    {
        Text = "SnapCraft";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", "app.ico"));
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        TopMost = true;
        ClientSize = new Size(466, 200);
        Controls.Add(web);
        Shown += async (_, _) => await InitializeBrowserAsync();
        recordingTimer.Tick += (_, _) =>
        {
            if (video is not null) Send(new { type = "elapsed", text = (DateTime.Now - video.StartedAt).ToString(@"hh\:mm\:ss") });
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("เปิด SnapCraft", null, (_, _) => ShowLauncher());
        menu.Items.Add("หยุดและบันทึก MP4", null, async (_, _) => await StopVideoAsync(true));
        menu.Items.Add("ยกเลิกวิดีโอ", null, async (_, _) => await StopVideoAsync(false));
        tray.Icon = Icon;
        tray.Text = "SnapCraft";
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowLauncher();
        FormClosing += async (_, e) =>
        {
            if (video is not null && !closingAfterVideo)
            {
                e.Cancel = true;
                await StopVideoAsync(false);
                closingAfterVideo = true;
                Close();
                return;
            }
            tray.Visible = false;
            tray.Dispose();
        };
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(WebAssets.DataRoot, "WebView2"));
            await web.EnsureCoreWebView2Async(env);
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("snapcraft.local", WebAssets.WebRoot, CoreWebView2HostResourceAccessKind.DenyCors);
            web.CoreWebView2.WebMessageReceived += async (_, args) => await HandleMessageAsync(args.WebMessageAsJson);
            web.Source = new Uri("https://snapcraft.local/launcher.html");
            SetWindowDisplayAffinity(Handle, 0x11);
        }
        catch (Exception error)
        {
            MessageBox.Show(this, $"เปิดหน้าตา SnapCraft ไม่ได้: {error.Message}\nโปรดติดตั้ง Microsoft Edge WebView2 Runtime", "SnapCraft", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task HandleMessageAsync(string message)
    {
        try
        {
            using var json = JsonDocument.Parse(message);
            var root = json.RootElement;
            var action = root.GetProperty("action").GetString();
            switch (action)
            {
                case "ready": Send(new { type = "settings", delayMs = settings.DelayMs, openMode = settings.OpenMode }); break;
                case "settings":
                    settings.DelayMs = root.GetProperty("delayMs").GetInt32();
                    settings.OpenMode = root.GetProperty("openMode").GetString() == "tab" ? "tab" : "window";
                    settings.Save();
                    break;
                case "area": await CaptureAsync(CaptureKind.Area); break;
                case "window": await CaptureAsync(CaptureKind.Window); break;
                case "scroll": await CaptureAsync(CaptureKind.Scroll); break;
                case "videoScreen": await StartVideoAsync(false); break;
                case "videoWindow": await StartVideoAsync(true); break;
                case "stopVideo": await StopVideoAsync(true); break;
                case "cancelVideo": await StopVideoAsync(false); break;
            }
        }
        catch (Exception error)
        {
            SetStatus(error.Message, true);
            MessageBox.Show(this, error.Message, "SnapCraft", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task CaptureAsync(CaptureKind kind)
    {
        if (busy || video is not null) return;
        busy = true;
        Send(new { type = "busy", value = true });
        Hide();
        EditorHubForm? openedEditor = null;
        try
        {
            var path = await capture.CaptureAsync(kind, settings.DelayMs);
            if (path is not null)
            {
                openedEditor = OpenEditor(path);
                SetStatus("จับภาพเรียบร้อย");
            }
            else SetStatus("ยกเลิกการจับภาพ");
        }
        catch (Exception error)
        {
            SetStatus(error.Message, true);
            MessageBox.Show(error.Message, "SnapCraft", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            Show();
            busy = false;
            Send(new { type = "busy", value = false });
            if (openedEditor is not null) { TopMost = false; openedEditor.Activate(); }
        }
    }

    private EditorHubForm OpenEditor(string path)
    {
        EditorHubForm hub;
        if (settings.OpenMode == "tab")
        {
            if (tabHub is null || tabHub.IsDisposed) tabHub = new EditorHubForm(ShowLauncher);
            hub = tabHub;
        }
        else hub = new EditorHubForm(ShowLauncher);
        hub.AddCapture(path);
        hub.Show();
        hub.Activate();
        return hub;
    }

    private async Task StartVideoAsync(bool window)
    {
        if (busy || video is not null) return;
        IntPtr handle = IntPtr.Zero;
        if (window)
        {
            Hide();
            var selected = SelectionOverlay.Choose(SelectionMode.Window);
            Show();
            if (selected is null) return;
            handle = selected.WindowHandle;
        }
        using var dialog = new SaveFileDialog
        {
            Filter = "MP4 video (*.mp4)|*.mp4",
            FileName = $"snapcraft-{DateTime.Now:yyyyMMdd-HHmmss}.mp4",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            video = new VideoSession(handle, dialog.FileName);
            video.Start();
            recordingTimer.Start();
            Send(new { type = "recording", value = true });
            tray.Visible = true;
            SetStatus("กำลังบันทึก MP4");
            if (!window && !SetWindowDisplayAffinity(Handle, 0x11))
            {
                Hide();
                tray.ShowBalloonTip(3000, "SnapCraft", "คลิกขวาไอคอนในถาดระบบเพื่อหยุดวิดีโอ", ToolTipIcon.Info);
            }
        }
        catch (Exception error)
        {
            video?.Dispose();
            video = null;
            SetStatus($"บันทึก MP4 ไม่ได้: {error.Message}", true);
            MessageBox.Show(this, $"เริ่มบันทึก MP4 ไม่ได้: {error.Message}", "SnapCraft", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        await Task.CompletedTask;
    }

    private async Task StopVideoAsync(bool save)
    {
        if (video is null) return;
        var active = video;
        video = null;
        recordingTimer.Stop();
        try
        {
            var path = await active.StopAsync(save);
            SetStatus(path is null ? "ยกเลิกวิดีโอ" : $"บันทึก MP4 แล้ว: {Path.GetFileName(path)}");
        }
        catch (Exception error)
        {
            SetStatus($"บันทึก MP4 ไม่สำเร็จ: {error.Message}", true);
            MessageBox.Show(this, error.Message, "SnapCraft", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            active.Dispose();
            tray.Visible = false;
            ShowLauncher();
            Send(new { type = "recording", value = false });
        }
    }

    private void ShowLauncher() { Show(); Activate(); TopMost = true; }
    private void SetStatus(string text, bool error = false) => Send(new { type = "status", text, error });
    private void Send(object message)
    {
        if (web.CoreWebView2 is not null) web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));
    }
}
