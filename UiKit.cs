// WFO1000: các control này chỉ dựng bằng code (không dùng Designer) nên tắt cảnh báo serialization cho cả file.
#pragma warning disable WFO1000

using System.Drawing.Drawing2D;
using static BaiTapLon.Ui;

namespace BaiTapLon;

// =====================================================================
//  UiKit.cs - Bộ control giao diện tự vẽ cho ứng dụng Quản lý bán hàng
//  (đặt cùng thư mục với Form1.cs)
// =====================================================================

/// <summary>Hệ số co giãn theo DPI màn hình, để giao diện không bị lệch khi Windows scale 125%/150%.</summary>
internal static class Ui
{
    public static readonly float Scale = GetScale();

    private static float GetScale()
    {
        try
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            return g.DpiX / 96f;
        }
        catch
        {
            return 1f;
        }
    }

    /// <summary>Đổi số pixel thiết kế (96 DPI) sang pixel thật.</summary>
    public static int S(int px) => (int)Math.Round(px * Scale);
}

/// <summary>Màu sắc, font và các hàm vẽ dùng chung.</summary>
internal static class Theme
{
    public static readonly Color PageBg = Color.FromArgb(248, 250, 252);
    public static readonly Color CardBorder = Color.FromArgb(232, 236, 242);
    public static readonly Color Line = Color.FromArgb(226, 232, 240);
    public static readonly Color RowLine = Color.FromArgb(237, 241, 246);
    public static readonly Color Text = Color.FromArgb(30, 41, 59);
    public static readonly Color Muted = Color.FromArgb(100, 116, 139);
    public static readonly Color Blue = Color.FromArgb(37, 99, 235);
    public static readonly Color Navy = Color.FromArgb(30, 58, 138);
    public static readonly Color BlueSoft = Color.FromArgb(239, 246, 255);
    public static readonly Color Green = Color.FromArgb(22, 163, 74);
    public static readonly Color Red = Color.FromArgb(220, 38, 38);
    public static readonly Color Disabled = Color.FromArgb(209, 213, 219);

    public static readonly Font Body = new("Segoe UI", 10F);
    public static readonly Font BodyBold = new("Segoe UI", 10F, FontStyle.Bold);
    public static readonly Font Small = new("Segoe UI", 9F);
    public static readonly Font Tiny = new("Segoe UI", 8.5F);
    public static readonly Font Badge = new("Segoe UI", 9F, FontStyle.Bold);
    public static readonly Font Heading = new("Segoe UI", 13F, FontStyle.Bold);
    public static readonly Font Brand = new("Segoe UI", 13.5F, FontStyle.Bold);
    public static readonly Font Title = new("Segoe UI", 22F, FontStyle.Bold);
    public static readonly Font Value = new("Segoe UI", 19F, FontStyle.Bold);
    public static readonly Font Avatar = new("Segoe UI", 13F, FontStyle.Bold);
    public static readonly Font Emoji = new("Segoe UI Emoji", 20F);

    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static (Color Fore, Color Back) StatusStyle(string s)
    {
        if (s.Contains("Ngừng"))
            return (Muted, Color.FromArgb(241, 245, 249));
        if (s.Contains("Còn hàng") || s.Contains("Đã thanh toán") || s.Contains("Đang làm") ||
            s.Contains("Đang hoạt động") || s.Contains("Hoạt động"))
            return (Color.FromArgb(22, 163, 74), Color.FromArgb(220, 252, 231));
        if (s.Contains("Sắp hết") || s.Contains("Chờ"))
            return (Color.FromArgb(217, 119, 6), Color.FromArgb(254, 243, 199));
        if (s.Contains("Hết hàng") || s.Contains("Đã hủy") || s.Contains("Nghỉ") || s.Contains("Đã khóa"))
            return (Color.FromArgb(220, 38, 38), Color.FromArgb(254, 226, 226));
        return (Muted, Color.FromArgb(241, 245, 249));
    }

    private static readonly (Color Fore, Color Back)[] TagPalette =
    {
        (Color.FromArgb(37, 99, 235), Color.FromArgb(232, 240, 254)),
        (Color.FromArgb(124, 58, 237), Color.FromArgb(243, 236, 254)),
        (Color.FromArgb(22, 163, 74), Color.FromArgb(220, 252, 231)),
        (Color.FromArgb(225, 29, 72), Color.FromArgb(255, 228, 230)),
        (Color.FromArgb(13, 148, 136), Color.FromArgb(220, 247, 243)),
    };

    /// <summary>Màu thẻ danh mục: cùng một chữ luôn ra cùng một màu.</summary>
    public static (Color Fore, Color Back) TagStyle(string s)
    {
        int h = 0;
        foreach (char c in s) h += c;
        return TagPalette[h % TagPalette.Length];
    }
}

/// <summary>Icon vẽ bằng GDI+ (không cần font icon hay file ảnh).</summary>
internal static class Icons
{
    public static void Draw(Graphics g, string name, Rectangle bounds, Color color, float width = 1.7f)
    {
        var oldMode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float s = Math.Min(bounds.Width, bounds.Height) / 24f;
        float ox = bounds.X + (bounds.Width - 24f * s) / 2f;
        float oy = bounds.Y + (bounds.Height - 24f * s) / 2f;
        PointF P(float x, float y) => new(ox + x * s, oy + y * s);
        RectangleF R(float x, float y, float w, float h) => new(ox + x * s, oy + y * s, w * s, h * s);

        using var pen = new Pen(color, width * Scale)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        switch (name)
        {
            case "home":
                g.DrawLines(pen, new[] { P(3, 11), P(12, 3), P(21, 11) });
                g.DrawLines(pen, new[] { P(5, 9.5f), P(5, 21), P(19, 21), P(19, 9.5f) });
                g.DrawLines(pen, new[] { P(10, 21), P(10, 14), P(14, 14), P(14, 21) });
                break;
            case "box":
                g.DrawLines(pen, new[] { P(12, 2.5f), P(20.5f, 7), P(20.5f, 17), P(12, 21.5f), P(3.5f, 17), P(3.5f, 7), P(12, 2.5f) });
                g.DrawLines(pen, new[] { P(3.5f, 7), P(12, 12), P(20.5f, 7) });
                g.DrawLine(pen, P(12, 12), P(12, 21.5f));
                break;
            case "cart":
                g.DrawLines(pen, new[] { P(2, 3.5f), P(5, 3.5f), P(7.5f, 15), P(19, 15), P(21, 7), P(6, 7) });
                g.DrawEllipse(pen, R(8, 18, 2.6f, 2.6f));
                g.DrawEllipse(pen, R(16, 18, 2.6f, 2.6f));
                break;
            case "invoice":
            {
                var body = R(5, 4, 14, 17);
                g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
                var tab = R(9, 2, 6, 4);
                g.DrawRectangle(pen, tab.X, tab.Y, tab.Width, tab.Height);
                g.DrawLine(pen, P(8.5f, 11), P(15.5f, 11));
                g.DrawLine(pen, P(8.5f, 15), P(15.5f, 15));
                break;
            }
            case "user":
                g.DrawEllipse(pen, R(8, 3.5f, 8, 8));
                g.DrawArc(pen, R(4, 13, 16, 16), 180, 180);
                break;
            case "team":
                g.DrawEllipse(pen, R(6, 4, 7, 7));
                g.DrawArc(pen, R(2.5f, 13, 14, 14), 180, 180);
                g.DrawEllipse(pen, R(15, 6.5f, 4.5f, 4.5f));
                g.DrawArc(pen, R(14.5f, 14, 8, 10), 180, 180);
                break;
            case "chart":
                g.DrawLine(pen, P(4, 20), P(20, 20));
                g.DrawLine(pen, P(7, 17), P(7, 12));
                g.DrawLine(pen, P(12, 17), P(12, 6));
                g.DrawLine(pen, P(17, 17), P(17, 10));
                break;
            case "logout":
                g.DrawLines(pen, new[] { P(10, 4), P(4, 4), P(4, 20), P(10, 20) });
                g.DrawLine(pen, P(9, 12), P(20, 12));
                g.DrawLines(pen, new[] { P(16, 8), P(20, 12), P(16, 16) });
                break;
            case "search":
                g.DrawEllipse(pen, R(3.5f, 3.5f, 12, 12));
                g.DrawLine(pen, P(14, 14), P(20, 20));
                break;
            case "chevron":
                g.DrawLines(pen, new[] { P(6, 9), P(12, 15), P(18, 9) });
                break;
            case "plus":
                g.DrawLine(pen, P(12, 5), P(12, 19));
                g.DrawLine(pen, P(5, 12), P(19, 12));
                break;
            case "lock":
            {
                var body = R(5, 11, 14, 10);
                g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
                g.DrawArc(pen, R(8, 3.5f, 8, 14), 180, 180);
                g.DrawLine(pen, P(12, 15), P(12, 17.5f));
                break;
            }
            case "book":
                g.DrawLines(pen, new[] { P(12, 5.5f), P(6, 4), P(2.5f, 5), P(2.5f, 19), P(6, 18), P(12, 19.5f) });
                g.DrawLines(pen, new[] { P(12, 5.5f), P(18, 4), P(21.5f, 5), P(21.5f, 19), P(18, 18), P(12, 19.5f) });
                g.DrawLine(pen, P(12, 5.5f), P(12, 19.5f));
                break;
        }

        g.SmoothingMode = oldMode;
    }
}

/// <summary>Thẻ trắng bo góc có đổ bóng nhẹ (giống các khối trong ảnh mẫu).</summary>
internal class Card : Panel
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = S(12);

    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Padding = new Padding(S(4), S(4), S(4), S(6));
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.PageBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - S(4));
        for (int i = 3; i >= 1; i--)
        {
            var shadowRect = new Rectangle(rect.X, rect.Y + i, rect.Width, rect.Height);
            using var shadowPath = Theme.RoundRect(shadowRect, Radius);
            using var shadowBrush = new SolidBrush(Color.FromArgb(6, 15, 23, 42));
            g.FillPath(shadowBrush, shadowPath);
        }

        using var path = Theme.RoundRect(rect, Radius);
        using var fill = new SolidBrush(Color.White);
        g.FillPath(fill, path);
        using var pen = new Pen(Theme.CardBorder);
        g.DrawPath(pen, path);
    }
}

/// <summary>Nút bo góc, có icon, có hiệu ứng hover. Dùng cho nút Tìm kiếm, Mượn/Sửa, Đăng xuất...</summary>
internal class RoundedButton : Button
{
    private bool hover;
    private bool down;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Fill { get; set; } = Theme.Blue;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Fore { get; set; } = Color.White;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Outline { get; set; } = Color.Empty;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = S(8);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string? Icon { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool LeftAligned { get; set; }

    public RoundedButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = Theme.BodyBold;
        Height = S(44);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { down = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { down = false; Invalidate(); base.OnMouseUp(mevent); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    private static Color Shade(Color c, float amount) =>
        c.ToArgb() == Color.White.ToArgb() ? Color.FromArgb(244, 247, 251) : ControlPaint.Dark(c, amount);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.PageBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        Color fill = !Enabled ? Theme.Disabled : down ? Shade(Fill, 0.15f) : hover ? Shade(Fill, 0.08f) : Fill;
        Color fore = Enabled ? Fore : Color.FromArgb(243, 244, 246);

        using (var path = Theme.RoundRect(rect, Radius))
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
            if (Outline != Color.Empty && Enabled)
            {
                using var pen = new Pen(Outline);
                g.DrawPath(pen, path);
            }
        }

        int iconSize = Icon == null ? 0 : S(16);
        int gap = Icon == null ? 0 : S(8);
        var textSize = TextRenderer.MeasureText(g, Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        int total = iconSize + gap + textSize.Width;
        int startX = LeftAligned ? S(18) : (Width - total) / 2;

        if (Icon != null)
            Icons.Draw(g, Icon, new Rectangle(startX, (Height - iconSize) / 2, iconSize, iconSize), fore, 1.6f);

        TextRenderer.DrawText(g, Text, Font,
            new Rectangle(startX + iconSize + gap, 0, textSize.Width + S(6), Height), fore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}

/// <summary>Ô nhập liệu bo góc, có icon kính lúp tùy chọn.</summary>
internal class InputBox : Control
{
    private readonly TextBox box = new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Color.White,
        ForeColor = Theme.Text,
        Font = Theme.Body
    };
    private bool focused;
    private bool readOnly;
    private string? icon;

    public event EventHandler? InputChanged;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string? Icon
    {
        get => icon;
        set { icon = value; LayoutBox(); Invalidate(); }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Placeholder
    {
        get => box.PlaceholderText;
        set => box.PlaceholderText = value;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Value
    {
        get => box.Text;
        set => box.Text = value;
    }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ReadOnly
    {
        get => readOnly;
        set
        {
            readOnly = value;
            box.ReadOnly = value;
            box.BackColor = value ? Color.FromArgb(241, 245, 249) : Color.White;
            Invalidate();
        }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Password
    {
        get => box.UseSystemPasswordChar;
        set => box.UseSystemPasswordChar = value;
    }

    public void FocusInput() => box.Focus();
    public InputBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = S(44);
        Controls.Add(box);
        box.TextChanged += (_, _) => InputChanged?.Invoke(this, EventArgs.Empty);
        box.Enter += (_, _) => { focused = true; Invalidate(); };
        box.Leave += (_, _) => { focused = false; Invalidate(); };
        Click += (_, _) => box.Focus();
    }

    private void LayoutBox()
    {
        int left = icon == null ? S(14) : S(44);
        box.Left = left;
        box.Width = Math.Max(S(10), Width - left - S(14));
        box.Top = (Height - box.Height) / 2;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutBox();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.PageBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.RoundRect(rect, S(8));
        using var fill = new SolidBrush(readOnly ? Color.FromArgb(241, 245, 249) : Color.White);
        g.FillPath(fill, path);
        using var pen = new Pen(focused ? Theme.Blue : Theme.Line, focused ? 1.5f : 1f);
        g.DrawPath(pen, path);

        if (icon != null)
            Icons.Draw(g, icon, new Rectangle(S(15), (Height - S(18)) / 2, S(18), S(18)), Theme.Muted, 1.6f);
    }
}

/// <summary>Ô chọn (thay ComboBox): bo góc, mũi tên, bấm vào sẽ xổ danh sách.</summary>
internal class DropBox : Control
{
    private readonly List<string> items = new();
    private int selected = -1;
    private bool hover;

    public event EventHandler? SelectedIndexChanged;

    public DropBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Height = S(44);
    }

    public void SetItems(IEnumerable<string> list)
    {
        items.Clear();
        items.AddRange(list);
        SelectedIndex = items.Count > 0 ? 0 : -1;
        Invalidate();
    }

    /// <summary>Chọn mục có nội dung trùng với text (không có thì giữ nguyên).</summary>
    public void SelectText(string text)
    {
        int i = items.IndexOf(text);
        if (i >= 0) SelectedIndex = i;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selected;
        set
        {
            if (selected == value) return;
            selected = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string SelectedText => selected >= 0 && selected < items.Count ? items[selected] : "";

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (items.Count == 0) return;

        var menu = new ContextMenuStrip { ShowImageMargin = false, Font = Theme.Body, MinimumSize = new Size(Width, 0) };
        for (int i = 0; i < items.Count; i++)
        {
            int idx = i;
            var mi = new ToolStripMenuItem(items[i])
            {
                Font = i == selected ? Theme.BodyBold : Theme.Body,
                Padding = new Padding(S(4), S(6), S(4), S(6))
            };
            mi.Click += (_, _) => SelectedIndex = idx;
            menu.Items.Add(mi);
        }
        menu.Closed += (_, _) => BeginInvoke(new MethodInvoker(() => menu.Dispose()));
        menu.Show(this, new Point(0, Height + S(4)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.PageBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.RoundRect(rect, S(8));
        using var fill = new SolidBrush(Color.White);
        g.FillPath(fill, path);
        using var pen = new Pen(hover ? Color.FromArgb(203, 213, 225) : Theme.Line);
        g.DrawPath(pen, path);

        TextRenderer.DrawText(g, SelectedText, Theme.Body, new Rectangle(S(16), 0, Math.Max(10, Width - S(52)), Height), Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        Icons.Draw(g, "chevron", new Rectangle(Width - S(32), (Height - S(16)) / 2, S(16), S(16)), Theme.Muted, 1.6f);
    }
}

/// <summary>Mục menu bên trái. Mục đang chọn có nền xanh nhạt và vạch xanh bên trái.</summary>
internal class NavItem : Control
{
    private bool hover;
    private bool active;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Icon { get; }

    public NavItem(string text, string icon)
    {
        Icon = icon;
        Text = text;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Height = S(46);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Active
    {
        get => active;
        set { if (active != value) { active = value; Invalidate(); } }
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Color.White);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (active || hover)
        {
            using var path = Theme.RoundRect(rect, S(6));
            using var bg = new SolidBrush(active ? Theme.BlueSoft : Color.FromArgb(248, 250, 252));
            g.FillPath(bg, path);
            if (active)
            {
                var state = g.Save();
                g.SetClip(path);
                using var bar = new SolidBrush(Theme.Blue);
                g.FillRectangle(bar, 0, 0, S(3), Height);
                g.Restore(state);
            }
        }

        Icons.Draw(g, Icon, new Rectangle(S(18), (Height - S(20)) / 2, S(20), S(20)),
            active ? Theme.Blue : Color.FromArgb(71, 85, 105), 1.6f);
        TextRenderer.DrawText(g, Text, active ? Theme.BodyBold : Theme.Body,
            new Rectangle(S(52), 0, Math.Max(10, Width - S(60)), Height),
            active ? Theme.Blue : Color.FromArgb(51, 65, 85),
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}

/// <summary>Thanh menu trái: vẽ logo, tên hệ thống, chữ MENU và các đường kẻ.</summary>
internal class Sidebar : Panel
{
    public Sidebar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var line = new Pen(Theme.Line);
        g.DrawLine(line, Width - 1, 0, Width - 1, Height);

        Icons.Draw(g, "book", new Rectangle(S(22), S(28), S(40), S(40)), Theme.Blue, 2.2f);
        TextRenderer.DrawText(g, "Quản lý bán hàng", Theme.Brand, new Point(S(72), S(27)), Theme.Navy, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "SALES MANAGEMENT", Theme.Tiny, new Point(S(74), S(53)), Theme.Muted, TextFormatFlags.NoPadding);
        g.DrawLine(line, 0, S(98), Width - 1, S(98));
        TextRenderer.DrawText(g, "MENU", Theme.Tiny, new Point(S(20), S(116)), Theme.Muted, TextFormatFlags.NoPadding);
    }
}

/// <summary>Nhãn tiêu đề nhiều màu: ví dụ "Xin chào, " đen + tên màu xanh.</summary>
internal class TitleLabel : Control
{
    private readonly List<(string Text, Color Color, Font Font)> parts = new();

    public TitleLabel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.PageBg;
    }

    public void SetParts(params (string Text, Color Color, Font Font)[] items)
    {
        parts.Clear();
        parts.AddRange(items);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        int x = 0;
        foreach (var part in parts)
        {
            var size = TextRenderer.MeasureText(g, part.Text, part.Font, new Size(int.MaxValue, int.MaxValue), flags);
            TextRenderer.DrawText(g, part.Text, part.Font, new Point(x, (Height - size.Height) / 2), part.Color, flags);
            x += size.Width;
        }
    }
}

/// <summary>Khối thông tin người dùng ở góc phải trên cùng (avatar tròn + tên + chức vụ).</summary>
internal class UserBadge : Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string UserName { get; set; } = "";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Role { get; set; } = "";

    public UserBadge()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Size = new Size(S(250), S(52));
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.PageBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var circle = new Rectangle(0, (Height - S(44)) / 2, S(44), S(44));
        using (var bg = new SolidBrush(Color.FromArgb(219, 234, 254)))
            g.FillEllipse(bg, circle);
        string letter = UserName.Length > 0 ? UserName.Substring(UserName.LastIndexOf(' ') + 1, 1).ToUpper() : "?";
        TextRenderer.DrawText(g, letter, Theme.Avatar, circle, Theme.Blue,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        int tx = S(56);
        int tw = Math.Max(10, Width - tx - S(30));
        TextRenderer.DrawText(g, UserName, Theme.BodyBold, new Rectangle(tx, S(5), tw, S(22)), Theme.Text,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, Role, Theme.Small, new Rectangle(tx, S(27), tw, S(20)), Theme.Muted,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        Icons.Draw(g, "chevron", new Rectangle(Width - S(24), (Height - S(16)) / 2, S(16), S(16)), Theme.Muted, 1.6f);
    }
}

/// <summary>Thẻ thống kê (doanh thu, số hóa đơn...) với icon tròn màu.</summary>
internal class StatCard : Card
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Title { get; set; } = "";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Value { get; set; } = "";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Note { get; set; } = "";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Icon { get; set; } = "chart";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Accent { get; set; } = Theme.Blue;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int pad = S(22);
        int inner = Height - S(4);
        var circle = new Rectangle(pad, (inner - S(52)) / 2, S(52), S(52));
        using (var tint = new SolidBrush(Color.FromArgb(30, Accent)))
            g.FillEllipse(tint, circle);
        Icons.Draw(g, Icon, new Rectangle(circle.X + S(14), circle.Y + S(14), S(24), S(24)), Accent, 1.7f);

        int tx = circle.Right + S(16);
        int w = Math.Max(10, Width - tx - pad);
        const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, Title, Theme.Small, new Rectangle(tx, S(17), w, S(20)), Theme.Muted, flags);
        TextRenderer.DrawText(g, Value, Theme.Value, new Rectangle(tx, S(37), w, S(36)), Theme.Text, flags);
        TextRenderer.DrawText(g, Note, Theme.Tiny, new Rectangle(tx, S(76), w, S(18)), Theme.Muted, flags);
    }
}

// =====================================================================
//  BẢNG DỮ LIỆU + PHÂN TRANG
// =====================================================================

internal enum ColKind { Text, Link, Tag, Status, Action }

internal record ColumnDef(string Header, float Weight, ColKind Kind = ColKind.Text, Color? ActionColor = null);

/// <summary>Bảng tự vẽ: tiêu đề đậm, dòng kẻ mảnh, chữ xanh cho tên, badge màu, nút thao tác.</summary>
internal class ModernTable : Control
{
    private readonly List<ColumnDef> cols = new();
    private IReadOnlyList<string[]> rows = Array.Empty<string[]>();
    private int top;
    private int hoverRow = -1;
    private bool hoverBtn;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int RowHeight { get; set; } = S(62);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int HeaderHeight { get; set; } = S(54);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SidePadding { get; set; } = S(24);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string[], bool>? ActionEnabled { get; set; }
    public event Action<string[]>? ActionClicked;

    public ModernTable()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Capacity => Math.Max(1, (Height - HeaderHeight) / RowHeight);

    public void SetColumns(params ColumnDef[] defs)
    {
        cols.Clear();
        cols.AddRange(defs);
        Invalidate();
    }

    public void SetRows(IReadOnlyList<string[]> data)
    {
        rows = data;
        hoverRow = -1;
        top = Math.Max(0, Math.Min(top, rows.Count - Capacity));
        Invalidate();
    }

    private bool IsEnabled(string[] row) => ActionEnabled?.Invoke(row) ?? true;

    private new (int X, int W)[] Layout()
    {
        var result = new (int X, int W)[cols.Count];
        if (cols.Count == 0) return result;
        int inner = Math.Max(10, Width - SidePadding * 2);
        float total = cols.Sum(c => c.Weight);
        float x = SidePadding;
        for (int i = 0; i < cols.Count; i++)
        {
            float w = inner * cols[i].Weight / total;
            result[i] = ((int)Math.Round(x), (int)Math.Round(w));
            x += w;
        }
        return result;
    }

    private Rectangle ActionRect(int col, int absRow, (int X, int W)[] lay)
    {
        int w = Math.Min(Math.Max(S(60), lay[col].W - S(16)), S(100));
        int h = S(34);
        int y = HeaderHeight + (absRow - top) * RowHeight + (RowHeight - h) / 2;
        return new Rectangle(lay[col].X, y, w, h);
    }

    private int RowAt(int y)
    {
        if (y < HeaderHeight) return -1;
        int i = (y - HeaderHeight) / RowHeight;
        int r = top + i;
        return i < Capacity && r < rows.Count ? r : -1;
    }

    private bool HitAction(Point p, out int row)
    {
        row = RowAt(p.Y);
        if (row < 0) return false;
        var lay = Layout();
        for (int c = 0; c < cols.Count; c++)
        {
            if (cols[c].Kind == ColKind.Action && IsEnabled(rows[row]) && ActionRect(c, row, lay).Contains(p))
                return true;
        }
        return false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int r = RowAt(e.Y);
        bool onBtn = HitAction(e.Location, out _);
        Cursor = onBtn ? Cursors.Hand : Cursors.Default;
        if (r != hoverRow || onBtn != hoverBtn)
        {
            hoverRow = r;
            hoverBtn = onBtn;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hoverRow = -1;
        hoverBtn = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && HitAction(e.Location, out int r))
            ActionClicked?.Invoke(rows[r]);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (rows.Count <= Capacity) return;
        top = Math.Max(0, Math.Min(top - Math.Sign(e.Delta), rows.Count - Capacity));
        Invalidate();
    }

    private static void DrawWrapped(Graphics g, string text, Font font, Color color, Rectangle cell)
    {
        var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        var size = TextRenderer.MeasureText(g, text, font, new Size(Math.Max(10, cell.Width), int.MaxValue), flags);
        int h = Math.Min(size.Height, cell.Height - S(8));
        var rect = new Rectangle(cell.X, cell.Y + (cell.Height - h) / 2, cell.Width, h);
        TextRenderer.DrawText(g, text, font, rect, color, flags | TextFormatFlags.EndEllipsis);
    }

    private static void DrawBadge(Graphics g, string text, (Color Fore, Color Back) style, bool dot, Rectangle cell)
    {
        if (string.IsNullOrEmpty(text)) return;
        var ts = TextRenderer.MeasureText(g, text, Theme.Badge, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        int padX = S(12);
        int h = S(28);
        int dotW = dot ? S(14) : 0;
        int w = Math.Min(cell.Width, ts.Width + padX * 2 + dotW);
        var rect = new Rectangle(cell.X, cell.Y + (cell.Height - h) / 2, w, h);

        using var path = Theme.RoundRect(rect, S(7));
        using var back = new SolidBrush(style.Back);
        g.FillPath(back, path);

        int tx = rect.X + padX;
        if (dot)
        {
            using var dotBrush = new SolidBrush(style.Fore);
            g.FillEllipse(dotBrush, tx, rect.Y + h / 2 - S(3), S(7), S(7));
            tx += dotW;
        }
        TextRenderer.DrawText(g, text, Theme.Badge, new Rectangle(tx, rect.Y, Math.Max(10, rect.Right - tx - S(6)), h), style.Fore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (cols.Count == 0) return;

        var lay = Layout();
        using var linePen = new Pen(Theme.Line);
        using var rowPen = new Pen(Theme.RowLine);

        // Tiêu đề cột
        for (int c = 0; c < cols.Count; c++)
        {
            TextRenderer.DrawText(g, cols[c].Header, Theme.BodyBold, new Rectangle(lay[c].X, 0, lay[c].W, HeaderHeight), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
        g.DrawLine(linePen, 0, HeaderHeight - 1, Width, HeaderHeight - 1);

        // Các dòng
        for (int i = 0; i < Capacity; i++)
        {
            int r = top + i;
            if (r >= rows.Count) break;
            int y = HeaderHeight + i * RowHeight;
            var row = rows[r];

            if (r == hoverRow)
            {
                using var hoverBrush = new SolidBrush(Color.FromArgb(250, 252, 255));
                g.FillRectangle(hoverBrush, 0, y, Width, RowHeight);
            }

            for (int c = 0; c < cols.Count; c++)
            {
                string text = c < row.Length ? row[c] : "";
                var cell = new Rectangle(lay[c].X, y, Math.Max(10, lay[c].W - S(10)), RowHeight);
                switch (cols[c].Kind)
                {
                    case ColKind.Text:
                        DrawWrapped(g, text, Theme.Body, Theme.Text, cell);
                        break;
                    case ColKind.Link:
                        DrawWrapped(g, text, Theme.Body, Theme.Blue, cell);
                        break;
                    case ColKind.Tag:
                        DrawBadge(g, text, Theme.TagStyle(text), false, cell);
                        break;
                    case ColKind.Status:
                        DrawBadge(g, text, Theme.StatusStyle(text), true, cell);
                        break;
                    case ColKind.Action:
                    {
                        var rc = ActionRect(c, r, lay);
                        bool enabled = IsEnabled(row);
                        Color baseColor = cols[c].ActionColor ?? Theme.Green;
                        Color fill = !enabled ? Theme.Disabled
                            : (hoverRow == r && hoverBtn ? ControlPaint.Dark(baseColor, 0.1f) : baseColor);
                        using var actionPath = Theme.RoundRect(rc, S(7));
                        using var actionBrush = new SolidBrush(fill);
                        g.FillPath(actionBrush, actionPath);
                        TextRenderer.DrawText(g, text, Theme.BodyBold, rc,
                            enabled ? Color.White : Color.FromArgb(243, 244, 246),
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                        break;
                    }
                }
            }
            g.DrawLine(rowPen, 0, y + RowHeight - 1, Width, y + RowHeight - 1);
        }
    }
}

/// <summary>Thanh phân trang: « ‹ 1 2 3 … 4 › và dòng "Hiển thị a đến b trong tổng số N".</summary>
internal class Pager : Control
{
    private readonly List<(Rectangle Rect, int Target)> hits = new();

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int TotalItems { get; private set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int PageSize { get; private set; } = 6;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int CurrentPage { get; set; } = 1;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string ItemName { get; set; } = "mục";
    public event Action<int>? PageChanged;

    public Pager()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Height = S(72);
    }

    private int PageCount => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)Math.Max(1, PageSize)));

    public void Configure(int total, int pageSize, int page)
    {
        TotalItems = total;
        PageSize = Math.Max(1, pageSize);
        CurrentPage = Math.Min(Math.Max(1, page), PageCount);
        Invalidate();
    }

    private IEnumerable<int> PageNumbers(int pc)
    {
        var set = new SortedSet<int> { 1, pc };
        for (int d = -1; d <= 1; d++)
        {
            int p = CurrentPage + d;
            if (p >= 1 && p <= pc) set.Add(p);
        }
        if (pc <= 6)
        {
            for (int p = 1; p <= pc; p++) set.Add(p);
        }
        return set;
    }

    private List<(string Label, int Target, bool Active, bool Enabled)> BuildTokens()
    {
        var list = new List<(string Label, int Target, bool Active, bool Enabled)>();
        int pc = PageCount;
        list.Add(("«", 1, false, CurrentPage > 1));
        list.Add(("‹", CurrentPage - 1, false, CurrentPage > 1));
        int prev = 0;
        foreach (int p in PageNumbers(pc))
        {
            if (prev != 0 && p - prev > 1) list.Add(("…", -1, false, false));
            list.Add((p.ToString(), p, p == CurrentPage, true));
            prev = p;
        }
        list.Add(("›", CurrentPage + 1, false, CurrentPage < pc));
        return list;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = hits.Any(h => h.Rect.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        foreach (var h in hits)
        {
            if (!h.Rect.Contains(e.Location) || h.Target == CurrentPage) continue;
            CurrentPage = h.Target;
            Invalidate();
            PageChanged?.Invoke(CurrentPage);
            break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        hits.Clear();

        int from = TotalItems == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
        int to = Math.Min(TotalItems, CurrentPage * PageSize);
        TextRenderer.DrawText(g, $"Hiển thị {from} đến {to} trong tổng số {TotalItems} {ItemName}", Theme.Small,
            new Rectangle(S(24), 0, Math.Max(10, Width / 2 - S(24)), Height), Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        int box = S(38);
        int gap = S(8);
        var tokens = BuildTokens();
        int totalW = tokens.Count * box + (tokens.Count - 1) * gap;
        int x = Width - S(24) - totalW;
        int y = (Height - box) / 2;

        foreach (var t in tokens)
        {
            var rc = new Rectangle(x, y, box, box);
            const TextFormatFlags center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

            if (t.Target < 0)
            {
                TextRenderer.DrawText(g, t.Label, Theme.Body, rc, Theme.Muted, center);
            }
            else
            {
                using var path = Theme.RoundRect(rc, S(8));
                if (t.Active)
                {
                    using var activeBrush = new SolidBrush(Theme.Blue);
                    g.FillPath(activeBrush, path);
                }
                else
                {
                    using var whiteBrush = new SolidBrush(Color.White);
                    g.FillPath(whiteBrush, path);
                    using var borderPen = new Pen(Theme.Line);
                    g.DrawPath(borderPen, path);
                }
                Color fore = t.Active ? Color.White : t.Enabled ? Theme.Text : Theme.Disabled;
                TextRenderer.DrawText(g, t.Label, t.Active ? Theme.BodyBold : Theme.Body, rc, fore, center);
                if (t.Enabled) hits.Add((rc, t.Target));
            }
            x += box + gap;
        }
    }
}

/// <summary>
/// Một trang danh sách hoàn chỉnh giống ảnh mẫu: thẻ tìm kiếm/lọc phía trên + thẻ bảng có phân trang phía dưới.
/// Chỉ cần đặt <see cref="Query"/> rồi gọi <see cref="Reload"/>.
/// </summary>
internal class DataListPage : Panel
{
    private readonly Card toolbar = new();
    private readonly Card tableCard = new();
    private List<string[]> results = new();

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public InputBox Search { get; } = new();
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public List<DropBox> Filters { get; } = new();
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public RoundedButton SearchButton { get; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public RoundedButton? ExtraButton { get; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public ModernTable Table { get; } = new();
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Pager Pager { get; } = new();

    /// <summary>(chuỗi tìm kiếm, danh sách giá trị đang chọn ở các ô lọc) -> các dòng hiển thị.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string, List<string>, List<string[]>> Query { get; set; } = (_, _) => new List<string[]>();

    public event Action<string[]>? RowAction;

    public DataListPage(string placeholder, string itemName, IEnumerable<string[]> filterOptions, ColumnDef[] columns, string? extraText = null)
    {
        BackColor = Theme.PageBg;
        Dock = DockStyle.Fill;

        toolbar.Dock = DockStyle.Top;
        toolbar.Height = S(88);
        tableCard.Dock = DockStyle.Fill;
        var gap = new Panel { Dock = DockStyle.Top, Height = S(16), BackColor = Theme.PageBg };

        Search.Icon = "search";
        Search.Placeholder = placeholder;
        Search.InputChanged += (_, _) => Reload();
        toolbar.Controls.Add(Search);

        foreach (var options in filterOptions)
        {
            var drop = new DropBox();
            drop.SetItems(options);
            drop.SelectedIndexChanged += (_, _) => Reload();
            Filters.Add(drop);
            toolbar.Controls.Add(drop);
        }

        SearchButton = new RoundedButton { Text = "Tìm kiếm", Icon = "search", Width = S(130), Height = S(44) };
        SearchButton.Click += (_, _) => Reload();
        toolbar.Controls.Add(SearchButton);

        if (extraText != null)
        {
            ExtraButton = new RoundedButton
            {
                Text = extraText,
                Icon = "plus",
                Fill = Theme.BlueSoft,
                Fore = Theme.Blue,
                Width = S(180),
                Height = S(44)
            };
            toolbar.Controls.Add(ExtraButton);
        }
        toolbar.Resize += (_, _) => LayoutToolbar();

        Table.Dock = DockStyle.Fill;
        Table.SetColumns(columns);
        Table.Resize += (_, _) => ApplyPage();
        Table.ActionClicked += row => RowAction?.Invoke(row);

        Pager.ItemName = itemName;
        Pager.Dock = DockStyle.Bottom;
        Pager.PageChanged += _ => ApplyPage();

        tableCard.Controls.Add(Table);
        tableCard.Controls.Add(Pager);

        // Thứ tự add: Fill trước, rồi mới tới Top (để docking đúng)
        Controls.Add(tableCard);
        Controls.Add(gap);
        Controls.Add(toolbar);
    }

    private void LayoutToolbar()
    {
        int y = S(20), h = S(44), gap = S(12);
        int right = toolbar.Width - S(24);

        if (ExtraButton != null)
        {
            ExtraButton.SetBounds(right - ExtraButton.Width, y, ExtraButton.Width, h);
            right -= ExtraButton.Width + gap;
        }
        SearchButton.SetBounds(right - SearchButton.Width, y, SearchButton.Width, h);
        right -= SearchButton.Width + gap;

        int fw = 0;
        if (Filters.Count > 0)
        {
            int free = right - S(24) - S(200) - gap;
            fw = Math.Clamp(free / Filters.Count - gap, S(120), S(210));
        }
        for (int i = Filters.Count - 1; i >= 0; i--)
        {
            Filters[i].SetBounds(right - fw, y, fw, h);
            right -= fw + gap;
        }
        Search.SetBounds(S(24), y, Math.Max(S(160), right - S(24)), h);
    }

    /// <summary>Chạy lại truy vấn (khi đổi từ khóa/bộ lọc hoặc dữ liệu thay đổi).</summary>
    public void Reload(bool resetPage = true)
    {
        results = Query(Search.Value.Trim(), Filters.Select(f => f.SelectedText).ToList());
        if (resetPage) Pager.CurrentPage = 1;
        ApplyPage();
    }

    private void ApplyPage()
    {
        int size = Table.Capacity;
        Pager.Configure(results.Count, size, Pager.CurrentPage);
        var slice = results.Skip((Pager.CurrentPage - 1) * size).Take(size).ToList();
        Table.SetRows(slice);
    }
}