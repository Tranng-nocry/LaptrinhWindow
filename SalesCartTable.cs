using System;
using System.Collections.Generic;
using System.Drawing;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static BaiTapLon.Ui;

namespace BaiTapLon;

internal class SalesCartTable : Control
{
    private IReadOnlyList<string[]> rows = Array.Empty<string[]>();
    private int top;

    public event Action<string>? IncreaseClicked;
    public event Action<string>? DecreaseClicked;
    public event Action<string>? RemoveClicked;

    [System.ComponentModel.DesignerSerializationVisibility(
       System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int RowHeight { get; set; } = S(62);

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int HeaderHeight { get; set; } = S(54);

    public SalesCartTable()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw, true);

        BackColor = Color.White;
        Font = Theme.Body;
    }

    private int Capacity =>
        Math.Max(1, (Height - HeaderHeight) / RowHeight);

    public void SetRows(IReadOnlyList<string[]> data)
    {
        rows = data;
        top = Math.Clamp(top, 0, Math.Max(0, rows.Count - Capacity));
        Invalidate();
    }

    private Rectangle[] Columns()
    {
        int pad = S(20);
        int available = Math.Max(S(300), Width - pad * 2);

        float[] weights = { 1.0f, 2.4f, 1.4f, 1.8f, 1.5f, 0.7f };
        float sum = 0;

        foreach (float w in weights)
            sum += w;

        var result = new Rectangle[weights.Length];
        int x = pad;

        for (int i = 0; i < weights.Length; i++)
        {
            int w = (int)(available * weights[i] / sum);
            result[i] = new Rectangle(x, 0, w, Height);
            x += w;
        }

        return result;
    }

    private Rectangle ButtonRect(int row, int column, int index)
    {
        var columns = Columns();
        int y = HeaderHeight + (row - top) * RowHeight;
        int size = S(30);

        int centerX = columns[column].X + columns[column].Width / 2;

        int x = index switch
        {
            0 => centerX - S(48),
            1 => centerX + S(18),
            _ => centerX - size / 2
        };

        return new Rectangle(
            x,
            y + (RowHeight - size) / 2,
            size,
            size);
    }

    private static void DrawButton(
        Graphics g, Rectangle rect, string text,
        Color background, Color foreground)
    {
        using var path = Theme.RoundRect(rect, S(7));
        using var brush = new SolidBrush(background);
        using var pen = new Pen(Theme.Line);

        g.FillPath(brush, path);
        g.DrawPath(pen, path);

        TextRenderer.DrawText(
            g, text, Theme.BodyBold, rect, foreground,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var columns = Columns();

        string[] headers =
        {
            "Mã SP", "Tên sản phẩm", "Đơn giá",
            "Số lượng", "Thành tiền", "Xóa"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var rect = new Rectangle(
                columns[c].X, 0,
                columns[c].Width, HeaderHeight);

            TextRenderer.DrawText(
                g,
                headers[c],
                Theme.BodyBold,
                rect,
                Theme.Text,
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.SingleLine);
        }

        using var line = new Pen(Theme.RowLine);
        g.DrawLine(line, 0, HeaderHeight - 1, Width, HeaderHeight - 1);

        for (int i = 0; i < Capacity; i++)
        {
            int r = top + i;
            if (r >= rows.Count) break;

            var data = rows[r];
            int y = HeaderHeight + i * RowHeight;

            for (int c = 0; c < Math.Min(6, data.Length); c++)
            {
                if (c == 3 || c == 5)
                    continue;

                var rect = new Rectangle(
                    columns[c].X, y,
                    Math.Max(10, columns[c].Width - S(8)),
                    RowHeight);
                TextRenderer.DrawText(
    g,
    data[c],
    Theme.Body,
    rect,
    c == 1 ? Theme.Blue : Theme.Text,
    TextFormatFlags.VerticalCenter |
    TextFormatFlags.EndEllipsis |
    TextFormatFlags.SingleLine);

            }

            DrawButton(g, ButtonRect(r, 3, 0),
                "−", Theme.BlueSoft, Theme.Blue);

            DrawButton(g, ButtonRect(r, 3, 1),
                "+", Theme.BlueSoft, Theme.Blue);

            var qtyRect = new Rectangle(
                columns[3].X + columns[3].Width / 2 - S(16),
                y, S(32), RowHeight);

            TextRenderer.DrawText(
                g, data.Length > 3 ? data[3] : "0",
                Theme.BodyBold, qtyRect, Theme.Text,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter);

            DrawButton(g, ButtonRect(r, 5, 2),
                "×", Color.FromArgb(254, 226, 226), Theme.Red);

            g.DrawLine(line, 0, y + RowHeight - 1,
                Width, y + RowHeight - 1);
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        if (e.Button != MouseButtons.Left || e.Y < HeaderHeight)
            return;

        int row = top + (e.Y - HeaderHeight) / RowHeight;
        if (row < 0 || row >= rows.Count)
            return;

        string code = rows[row][0];

        if (ButtonRect(row, 3, 0).Contains(e.Location))
            DecreaseClicked?.Invoke(code);
        else if (ButtonRect(row, 3, 1).Contains(e.Location))
            IncreaseClicked?.Invoke(code);
        else if (ButtonRect(row, 5, 2).Contains(e.Location))
            RemoveClicked?.Invoke(code);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        Cursor = Cursors.Default;

        if (e.Y < HeaderHeight) return;

        int row = top + (e.Y - HeaderHeight) / RowHeight;
        if (row < 0 || row >= rows.Count) return;

        if (ButtonRect(row, 3, 0).Contains(e.Location) ||
            ButtonRect(row, 3, 1).Contains(e.Location) ||
            ButtonRect(row, 5, 2).Contains(e.Location))
        {
            Cursor = Cursors.Hand;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (rows.Count <= Capacity) return;

        top = Math.Clamp(
            top - Math.Sign(e.Delta),
            0, Math.Max(0, rows.Count - Capacity));

        Invalidate();
    }
}
