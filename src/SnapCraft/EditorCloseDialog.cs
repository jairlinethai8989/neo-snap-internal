namespace SnapCraft;

internal sealed class EditorCloseDialog : Form
{
    public EditorCloseDialog(string imageName, bool video = false)
    {
        Name = video ? "closeVideoDialog" : "closeImageDialog";
        Text = "Neo Snap";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(370, 150);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10);
        Controls.Add(new Label { Text = $"บันทึก {imageName} ก่อนปิดหรือไม่?", AutoSize = false, Location = new Point(16, 16), Size = new Size(338, 32) });
        Controls.Add(new Label { Text = video ? "คลิปนี้ยังไม่ได้คัดลอกหรือบันทึก" : "ภาพนี้ยังไม่ได้เก็บ หรือมีการแก้ไขเพิ่มเติม", ForeColor = Color.DimGray, AutoSize = false, Location = new Point(16, 48), Size = new Size(338, 36) });
        var save = Choice("saveClose", "บันทึก", DialogResult.Yes, 82);
        save.BackColor = Color.FromArgb(21, 94, 239);
        save.ForeColor = Color.White;
        Choice("discardClose", "ไม่บันทึก", DialogResult.No, 172);
        var cancel = Choice("cancelClose", "ยกเลิก", DialogResult.Cancel, 262);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private Button Choice(string name, string label, DialogResult result, int left)
    {
        var button = new Button { Name = name, Text = label, DialogResult = result, Location = new Point(left, 100), Size = new Size(82, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.White };
        button.FlatAppearance.BorderColor = Color.FromArgb(215, 225, 239);
        Controls.Add(button);
        return button;
    }
}
