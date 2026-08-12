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

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
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
        using var fill = new SolidBrush(_palette.AccentDark);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillRectangle(fill, box);
        e.Graphics.DrawRectangle(border, box);
        using var check = new Pen(_palette.Text, 2f)
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

    private sealed class ThemedColorTable : ProfessionalColorTable
    {
        private readonly TrayMenuPalette _palette;

        public ThemedColorTable(TrayMenuPalette palette)
        {
            _palette = palette;
            UseSystemColors = false;
        }

        public override Color MenuBorder => _palette.Border;
        public override Color MenuItemBorder => _palette.Accent;
        public override Color MenuItemSelected => _palette.Hover;
        public override Color MenuItemSelectedGradientBegin => _palette.Hover;
        public override Color MenuItemSelectedGradientEnd => _palette.Hover;
        public override Color MenuItemPressedGradientBegin => _palette.Hover;
        public override Color MenuItemPressedGradientEnd => _palette.Hover;
        public override Color ToolStripDropDownBackground => _palette.Background;
        public override Color ImageMarginGradientBegin => _palette.Background;
        public override Color ImageMarginGradientMiddle => _palette.Background;
        public override Color ImageMarginGradientEnd => _palette.Background;
        public override Color ImageMarginRevealedGradientBegin => _palette.Background;
        public override Color ImageMarginRevealedGradientMiddle => _palette.Background;
        public override Color ImageMarginRevealedGradientEnd => _palette.Background;
        public override Color CheckBackground => _palette.AccentDark;
        public override Color CheckSelectedBackground => _palette.Accent;
        public override Color CheckPressedBackground => _palette.Accent;
        public override Color ButtonSelectedHighlight => _palette.Hover;
        public override Color ButtonCheckedHighlight => _palette.AccentDark;
        public override Color SeparatorDark => _palette.Border;
        public override Color SeparatorLight => _palette.Border;
        public override Color OverflowButtonGradientBegin => _palette.Background;
        public override Color OverflowButtonGradientEnd => _palette.Background;
        public override Color RaftingContainerGradientBegin => _palette.Background;
        public override Color RaftingContainerGradientEnd => _palette.Background;
    }
}
