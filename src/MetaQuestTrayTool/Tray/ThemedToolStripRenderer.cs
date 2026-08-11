using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tray;

public sealed class ThemedToolStripRenderer : ToolStripProfessionalRenderer
{
    private TrayMenuPalette _palette;

    public ThemedToolStripRenderer(TrayMenuPalette palette)
        : base(new ThemedColorTable(palette))
    {
        _palette = palette;
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(_palette.Background);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(_palette.Border);
        var bounds = e.AffectedBounds;
        bounds.Width -= 1;
        bounds.Height -= 1;
        e.Graphics.DrawRectangle(pen, bounds);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        var selected = e.Item.Selected && e.Item.Enabled;
        using var brush = new SolidBrush(selected ? _palette.Hover : _palette.Background);
        e.Graphics.FillRectangle(brush, bounds);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = !e.Item.Enabled
            ? _palette.Muted
            : e.Item.Selected
                ? _palette.Accent
                : _palette.Text;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == false ? _palette.Muted : _palette.Text;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var y = e.Item.Height / 2;
        using var pen = new Pen(_palette.Border);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var box = e.ImageRectangle;
        if (box.Width <= 0 || box.Height <= 0)
        {
            box = new Rectangle(4, (e.Item.Height - 14) / 2, 14, 14);
        }

        using var border = new Pen(_palette.Accent, 1.5f);
        using var fill = new SolidBrush(_palette.Surface);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillRectangle(fill, box);
        e.Graphics.DrawRectangle(border, box);
        using var check = new Pen(_palette.Accent, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var x = box.Left;
        var y = box.Top;
        e.Graphics.DrawLines(check, new Point[]
        {
            new(x + 3, y + 7),
            new(x + 6, y + 10),
            new(x + 11, y + 3)
        });
    }

    private sealed class ThemedColorTable(TrayMenuPalette palette) : ProfessionalColorTable
    {
        public override Color MenuBorder => palette.Border;
        public override Color MenuItemBorder => palette.Accent;
        public override Color MenuItemSelected => palette.Hover;
        public override Color MenuItemSelectedGradientBegin => palette.Hover;
        public override Color MenuItemSelectedGradientEnd => palette.Hover;
        public override Color MenuItemPressedGradientBegin => palette.Hover;
        public override Color MenuItemPressedGradientEnd => palette.Hover;
        public override Color ToolStripDropDownBackground => palette.Background;
        public override Color ImageMarginGradientBegin => palette.Background;
        public override Color ImageMarginGradientMiddle => palette.Background;
        public override Color ImageMarginGradientEnd => palette.Background;
        public override Color SeparatorDark => palette.Border;
        public override Color SeparatorLight => palette.Border;
        public override Color OverflowButtonGradientBegin => palette.Background;
        public override Color OverflowButtonGradientEnd => palette.Background;
        public override Color RaftingContainerGradientBegin => palette.Background;
        public override Color RaftingContainerGradientEnd => palette.Background;
    }
}
