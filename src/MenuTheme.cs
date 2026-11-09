using System.Drawing;
using System.Windows.Forms;

namespace K2
{
    // Палитра контекстных/трей-меню в стиле окна настроек (тёмно-синяя с бирюзовым)
    class DarkColors : ProfessionalColorTable
    {
        static readonly Color Back   = Color.FromArgb(16, 22, 30);
        static readonly Color Sel    = Color.FromArgb(34, 49, 66);
        static readonly Color Border = Color.FromArgb(52, 70, 92);
        static readonly Color Line   = Color.FromArgb(38, 50, 65);

        public override Color ToolStripDropDownBackground { get { return Back; } }
        public override Color MenuBorder { get { return Border; } }
        public override Color MenuItemBorder { get { return Border; } }
        public override Color MenuItemSelected { get { return Sel; } }
        public override Color MenuItemSelectedGradientBegin { get { return Sel; } }
        public override Color MenuItemSelectedGradientEnd { get { return Sel; } }
        public override Color MenuItemPressedGradientBegin { get { return Back; } }
        public override Color MenuItemPressedGradientEnd { get { return Back; } }
        public override Color MenuStripGradientBegin { get { return Back; } }
        public override Color MenuStripGradientEnd { get { return Back; } }
        public override Color ImageMarginGradientBegin { get { return Back; } }
        public override Color ImageMarginGradientMiddle { get { return Back; } }
        public override Color ImageMarginGradientEnd { get { return Back; } }
        public override Color SeparatorDark { get { return Line; } }
        public override Color SeparatorLight { get { return Line; } }
    }

    class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        static readonly Font CheckFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        public DarkMenuRenderer() : base(new DarkColors()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled
                ? Color.FromArgb(232, 240, 245)
                : Color.FromArgb(110, 124, 140);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Color back = e.Item.Selected ? Color.FromArgb(34, 49, 66) : Color.FromArgb(16, 22, 30);
            using (SolidBrush b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Graphics.ClipBounds);
            TextRenderer.DrawText(e.Graphics, "✓", CheckFont, e.ImageRectangle,
                Color.FromArgb(0, 191, 166),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? Color.FromArgb(150, 164, 180) : Color.FromArgb(90, 104, 120);
            base.OnRenderArrow(e);
        }
    }
}
