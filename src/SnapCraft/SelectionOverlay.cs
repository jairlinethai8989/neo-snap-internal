namespace SnapCraft;

internal enum SelectionMode { Region, Window }
internal sealed record CaptureSelection(Rectangle Region, IntPtr WindowHandle);

internal sealed class SelectionOverlay : Form
{
    private readonly SelectionMode mode;
    private Point start;
    private Point current;
    private bool dragging;
    public CaptureSelection? Selection { get; private set; }

    private SelectionOverlay(SelectionMode mode)
    {
        this.mode = mode;
        FormBorderStyle = FormBorderStyle.None;
        Bounds = SystemInformation.VirtualScreen;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.45;
        Cursor = Cursors.Cross;
        KeyPreview = true;
        DoubleBuffered = true;
    }

    public static CaptureSelection? Choose(SelectionMode mode)
    {
        using var overlay = new SelectionOverlay(mode);
        overlay.ShowDialog();
        return overlay.Selection;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (mode == SelectionMode.Window)
        {
            var point = PointToScreen(e.Location);
            Hide();
            Application.DoEvents();
            var handle = NativeInput.RootWindowAt(point);
            if (handle != IntPtr.Zero) Selection = new CaptureSelection(NativeInput.WindowBounds(handle), handle);
            DialogResult = Selection is null ? DialogResult.Cancel : DialogResult.OK;
            Close();
            return;
        }
        start = e.Location;
        current = e.Location;
        dragging = true;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!dragging) return;
        current = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!dragging || e.Button != MouseButtons.Left) return;
        dragging = false;
        Capture = false;
        current = e.Location;
        var region = Rectangle.FromLTRB(Math.Min(start.X, current.X), Math.Min(start.Y, current.Y), Math.Max(start.X, current.X), Math.Max(start.Y, current.Y));
        if (region.Width < 24 || region.Height < 24) return;
        region.Offset(Location);
        Hide();
        Application.DoEvents();
        Selection = new CaptureSelection(region, NativeInput.RootWindowAt(new Point(region.Left + region.Width / 2, region.Top + region.Height / 2)));
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var instruction = mode == SelectionMode.Window ? "คลิกหน้าต่างที่ต้องการจับ  •  Esc ยกเลิก" : "ลากเลือกบริเวณที่ต้องการจับ  •  Esc ยกเลิก";
        TextRenderer.DrawText(e.Graphics, instruction, new Font("Segoe UI", 15, FontStyle.Bold), new Point(32, 24), Color.White);
        if (!dragging) return;
        var rectangle = Rectangle.FromLTRB(Math.Min(start.X, current.X), Math.Min(start.Y, current.Y), Math.Max(start.X, current.X), Math.Max(start.Y, current.Y));
        using var pen = new Pen(Color.FromArgb(114, 231, 189), 4);
        e.Graphics.DrawRectangle(pen, rectangle);
    }
}
