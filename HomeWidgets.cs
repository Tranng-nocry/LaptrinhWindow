#pragma warning disable WFO1000


using System.Drawing.Drawing2D;
using System.Globalization;
using static BaiTapLon.Ui;


namespace BaiTapLon;


/// <summary>Thẻ biểu đồ cột (doanh thu theo ngày). Rê chuột vào cột để xem giá trị.</summary>
internal class ChartCard : Card
{
    private static readonly CultureInfo Vi = new("vi-VN");
    private string[] labels = Array.Empty<string>();
    private decimal[] values = Array.Empty<decimal>();
    private int hover = -1;


    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Title { get; set; } = "";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Subtitle { get; set; } = "";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string[] Labels { get => labels; set { labels = value; Invalidate(); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public decimal[] Values { get => values; set { values = value; Invalidate(); } }


    private Rectangle Plot => new(S(24), S(78), Math.Max(10, Width - S(48)), Math.Max(10, Height - S(78) - S(40)));


    private static string Short(decimal v) =>
        v >= 1_000_000m ? (v / 1_000_000m).ToString("0.#", Vi) + " tr" : (v / 1_000m).ToString("0", Vi) + " k";


    private Rectangle BarRect(int i)
    {
        var p = Plot;
        float slot = p.Width / (float)values.Length;
        int barW = (int)(slot * 0.5f);
        decimal max = Math.Max(1m, values.Max()) * 1.18m;
        int h = Math.Max(S(4), (int)(p.Height * (double)(values[i] / max)));
        int x = p.X + (int)(slot * i + (slot - barW) / 2f);
        return new Rectangle(x, p.Bottom - h, barW, h);
    }


    private int HitSlot(Point pt)
    {
        var p = Plot;
        if (values.Length == 0 || pt.X < p.X || pt.X >= p.Right || pt.Y < p.Y || pt.Y > p.Bottom + S(30)) return -1;
        return Math.Min(values.Length - 1, (int)((pt.X - p.X) / (p.Width / (float)values.Length)));
    }


    protected override void OnMouseMove(MouseEventArgs e)
    {
        int i = HitSlot(e.Location);
        if (i != hover) { hover = i; Invalidate(); }
        base.OnMouseMove(e);
    }


    protected override void OnMouseLeave(EventArgs e)
    {
        if (hover != -1) { hover = -1; Invalidate(); }
        base.OnMouseLeave(e);
    }


    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(g, Title, Theme.Heading, new Rectangle(S(24), S(14), Width - S(48), S(26)), Theme.Text, flags);
        TextRenderer.DrawText(g, Subtitle, Theme.Small, new Rectangle(S(24), S(42), Width - S(48), S(20)), Theme.Muted, flags);
        if (values.Length == 0) return;


        var p = Plot;
        using (var grid = new Pen(Theme.RowLine))
            for (int k = 0; k <= 3; k++)
            {
                int y = p.Bottom - (int)(p.Height * k / 3f);
                g.DrawLine(grid, p.X, y, p.Right, y);
            }


        float slot = p.Width / (float)values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            bool strong = i == values.Length - 1 || i == hover;
            var r = BarRect(i);
            using (var path = Theme.RoundRect(r, S(5)))
            using (var brush = new SolidBrush(strong ? Theme.Blue : Color.FromArgb(191, 219, 254)))
                g.FillPath(brush, path);


            string label = i < labels.Length ? labels[i] : "";
            TextRenderer.DrawText(g, label, Theme.Small,
                new Rectangle(p.X + (int)(slot * i), p.Bottom + S(8), (int)slot, S(20)),
                strong ? Theme.Text : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }


        if (hover >= 0 && hover < values.Length)
        {
            var r = BarRect(hover);
            string text = Short(values[hover]);
            var size = TextRenderer.MeasureText(g, text, Theme.Badge, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int w = size.Width + S(18), h = S(26);
            int x = Math.Clamp(r.X + r.Width / 2 - w / 2, S(8), Math.Max(S(8), Width - w - S(8)));
            var tip = new Rectangle(x, r.Y - h - S(6), w, h);
            using (var path = Theme.RoundRect(tip, S(6)))
            using (var fill = new SolidBrush(Theme.Text))
                g.FillPath(fill, path);
            TextRenderer.DrawText(g, text, Theme.Badge, tip, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
