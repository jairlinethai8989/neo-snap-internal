using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SnapCraft;

internal sealed class EditorHubForm : Form
{
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly Button closeTab = new() { Text = "ปิดแท็บ", Width = 84, Height = 27, FlatStyle = FlatStyle.Flat };
    private readonly Action showLauncher;

    public EditorHubForm(Action showLauncher)
    {
        this.showLauncher = showLauncher;
        Text = "SnapCraft | แก้ไขภาพ";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", "app.ico"));
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 540);
        Size = new Size(1300, 850);
        BackColor = Color.White;
        var header = new Panel { Dock = DockStyle.Top, Height = 29, BackColor = Color.FromArgb(245, 249, 247) };
        closeTab.Dock = DockStyle.Right;
        closeTab.BackColor = Color.White;
        closeTab.Click += (_, _) => CloseSelected();
        header.Controls.Add(closeTab);
        Controls.Add(tabs);
        Controls.Add(header);
        Shown += OnFirstShown;
    }

    public void AddCapture(string path)
    {
        var page = new TabPage($"ภาพ {tabs.TabCount + 1}") { Tag = path };
        var web = new WebView2 { Dock = DockStyle.Fill };
        page.Controls.Add(web);
        tabs.TabPages.Add(page);
        tabs.SelectedTab = page;
        if (Visible) _ = InitializeEditorAsync(web, path);
    }

    private void OnFirstShown(object? sender, EventArgs e)
    {
        Shown -= OnFirstShown;
        foreach (TabPage page in tabs.TabPages)
        {
            if (page.Controls[0] is WebView2 web && web.CoreWebView2 is null)
                _ = InitializeEditorAsync(web, (string)page.Tag!);
        }
    }

    private async Task InitializeEditorAsync(WebView2 web, string path)
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(WebAssets.DataRoot, "WebView2"));
            await web.EnsureCoreWebView2Async(env);
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("snapcraft.local", WebAssets.WebRoot, CoreWebView2HostResourceAccessKind.DenyCors);
            web.CoreWebView2.WebMessageReceived += (_, args) =>
            {
                if (args.TryGetWebMessageAsString() == "showLauncher") showLauncher();
            };
            web.CoreWebView2.DownloadStarting += (_, args) =>
            {
                using var dialog = new SaveFileDialog
                {
                    Filter = "PNG image (*.png)|*.png",
                    FileName = $"snapcraft-{DateTime.Now:yyyyMMdd-HHmmss}.png",
                    AddExtension = true
                };
                if (dialog.ShowDialog(this) == DialogResult.OK) args.ResultFilePath = dialog.FileName;
                else args.Cancel = true;
                args.Handled = true;
            };
            web.Source = new Uri($"https://snapcraft.local/editor.html?image={Uri.EscapeDataString(WebAssets.CaptureUrl(path))}");
        }
        catch (Exception error)
        {
            MessageBox.Show(this, $"เปิดหน้าแก้ไขภาพไม่ได้: {error.Message}", "SnapCraft", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CloseSelected()
    {
        var page = tabs.SelectedTab;
        if (page is null) return;
        var path = page.Tag as string;
        tabs.TabPages.Remove(page);
        page.Dispose();
        if (path is not null) { try { File.Delete(path); } catch (IOException) { } }
        if (tabs.TabCount == 0) Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        foreach (TabPage page in tabs.TabPages)
        {
            if (page.Tag is string path) { try { File.Delete(path); } catch (IOException) { } }
        }
        base.OnFormClosed(e);
    }
}
