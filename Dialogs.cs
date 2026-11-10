#pragma warning disable WFO1000

using System.Drawing.Drawing2D;
using static BaiTapLon.Ui;

namespace BaiTapLon;

/// <summary>
/// Dựng nhanh hộp thoại nhập liệu đúng phong cách của app (nhãn nhỏ xám + ô bo góc + nút lưu xanh).
/// Có thể đặt 2 ô cạnh nhau bằng tham số half = true.
/// </summary>
internal sealed class DialogBuilder
{
    private readonly Form dialog;
    private readonly int width;
    private int y = S(72);
    private bool leftPlaced;

    public DialogBuilder(string title, string heading, int clientWidth = 460)
    {
        width = S(clientWidth);
        dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            BackColor = Color.White,
            Font = Theme.Body
        };
        dialog.Controls.Add(new Label
        {
            Text = heading,
            Font = Theme.Heading,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(S(28), S(22))
        });
    }

    private T Place<T>(string caption, T input, bool half) where T : Control
    {
        int full = width - S(56);
        int w = half ? (full - S(14)) / 2 : full;
        int x = S(28);

        if (!half && leftPlaced)            // ô lẻ còn dở dang -> xuống dòng trước
        {
            y += S(80);
            leftPlaced = false;
        }
        if (half && leftPlaced)
            x = S(28) + w + S(14);

        dialog.Controls.Add(new Label
        {
            Text = caption,
            ForeColor = Theme.Muted,
            Font = Theme.Small,
            AutoSize = true,
            Location = new Point(x, y)
        });
        input.SetBounds(x, y + S(22), w, S(44));
        dialog.Controls.Add(input);

        if (half && !leftPlaced) leftPlaced = true;
        else
        {
            leftPlaced = false;
            y += S(80);
        }
        return input;
    }

    public InputBox AddInput(string caption, string value = "", string placeholder = "",
        bool password = false, bool readOnly = false, bool half = false)
    {
        var box = new InputBox { Value = value, Placeholder = placeholder, Password = password, ReadOnly = readOnly };
        return Place(caption, box, half);
    }

    public DropBox AddDrop(string caption, IEnumerable<string> items, string? selected = null, bool half = false)
    {
        var drop = new DropBox();
        drop.SetItems(items);
        if (selected != null) drop.SelectText(selected);
        return Place(caption, drop, half);
    }

    /// <summary>Dòng ghi chú nhỏ màu xám (tự xuống dòng).</summary>
    public void AddNote(string text)
    {
        if (leftPlaced)
        {
            y += S(80);
            leftPlaced = false;
        }
        int w = width - S(56);
        var size = TextRenderer.MeasureText(text, Theme.Small, new Size(w, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        dialog.Controls.Add(new Label
        {
            Text = text,
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Location = new Point(S(28), y - S(6)),
            Size = new Size(w, size.Height + S(4))
        });
        y += size.Height + S(14);
    }

    /// <summary>
    /// Hiện hộp thoại. onSave trả về null nếu lưu thành công, hoặc câu thông báo lỗi để hiện cho người dùng.
    /// Trả về true nếu đã lưu.
    /// </summary>
    public bool Show(IWin32Window owner, string saveText, Func<string?> onSave, Color? fill = null)
    {
        if (leftPlaced)
        {
            y += S(80);
            leftPlaced = false;
        }

        var save = new RoundedButton { Text = saveText, Fill = fill ?? Theme.Blue };
        save.SetBounds(S(28), y + S(4), width - S(56), S(46));
        save.Click += (_, _) =>
        {
            string? error = onSave();
            if (error != null)
            {
                MessageBox.Show(dialog, error, "Dữ liệu chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            dialog.DialogResult = DialogResult.OK;
        };
        dialog.Controls.Add(save);
        dialog.AcceptButton = save;
        dialog.ClientSize = new Size(width, y + S(4) + S(46) + S(28));

        var result = dialog.ShowDialog(owner);
        dialog.Dispose();
        return result == DialogResult.OK;
    }
}

/// <summary>
/// Menu hồ sơ người dùng (bấm vào avatar góc phải trên): tên + chức vụ, rồi
/// Thông tin cá nhân / Đổi mật khẩu / Đăng xuất. Tự đóng khi bấm ra ngoài.
/// </summary>
internal sealed class UserMenuPopup : Form
{
    private readonly string fullName;
    private readonly string role;
    private readonly (string Key, string Text, string Icon)[] items =
    {
        ("profile", "Thông tin cá nhân", "user"),
        ("password", "Đổi mật khẩu", "lock"),
        ("logout", "Đăng xuất", "logout")
    };
    private int hover = -1;
    private bool closing;

    private void CloseOnce()
    {
        if (closing) return;
        closing = true;
        Close();
    }

    private int HeaderH => S(84);
    private int ItemH => S(46);

    public event Action<string>? ItemChosen;

    public UserMenuPopup(string fullName, string role, int width)
    {
        this.fullName = fullName;
        this.role = role;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = Color.White;
        DoubleBuffered = true;
        ClientSize = new Size(width, HeaderH + S(1) + S(8) + items.Length * ItemH + S(10));

        using var path = Theme.RoundRect(new Rectangle(0, 0, Width, Height), S(14));
        Region = new Region(path);
    }

    private Rectangle ItemRect(int i) =>
        new(S(8), HeaderH + S(8) + i * ItemH, Width - S(16), ItemH);

    private int ItemAt(Point p)
    {
        for (int i = 0; i < items.Length; i++)
            if (ItemRect(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        CloseOnce();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = ItemAt(e.Location);
        Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
        if (i != hover) { hover = i; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hover != -1) { hover = -1; Invalidate(); }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int i = ItemAt(e.Location);
        if (i >= 0) ItemChosen?.Invoke(items[i].Key);
        CloseOnce();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Viền bo góc
        using (var border = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), S(14)))
        using (var pen = new Pen(Theme.CardBorder))
            g.DrawPath(pen, border);

        // Đầu menu: avatar + tên + chức vụ + mũi tên
        var circle = new Rectangle(S(16), (HeaderH - S(48)) / 2, S(48), S(48));
        using (var bg = new SolidBrush(Color.FromArgb(219, 234, 254)))
            g.FillEllipse(bg, circle);
        string letter = fullName.Length > 0 ? fullName.Substring(fullName.LastIndexOf(' ') + 1, 1).ToUpper() : "?";
        TextRenderer.DrawText(g, letter, Theme.Avatar, circle, Theme.Blue,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        int tx = circle.Right + S(14);
        int tw = Math.Max(10, Width - tx - S(46));
        const TextFormatFlags one = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, fullName, Theme.BodyBold, new Rectangle(tx, S(23), tw, S(22)), Theme.Text, one);
        TextRenderer.DrawText(g, role, Theme.Small, new Rectangle(tx, S(47), tw, S(20)), Theme.Muted, one);
        Icons.Draw(g, "chevron", new Rectangle(Width - S(38), (HeaderH - S(16)) / 2, S(16), S(16)), Theme.Muted, 1.6f);

        using (var line = new Pen(Theme.RowLine))
            g.DrawLine(line, S(16), HeaderH, Width - S(16), HeaderH);

        // Các mục
        for (int i = 0; i < items.Length; i++)
        {
            var r = ItemRect(i);
            if (i == hover)
            {
                using var path = Theme.RoundRect(r, S(8));
                using var bg = new SolidBrush(Color.FromArgb(244, 247, 251));
                g.FillPath(bg, path);
            }
            Icons.Draw(g, items[i].Icon, new Rectangle(r.X + S(14), r.Y + (r.Height - S(20)) / 2, S(20), S(20)),
                Color.FromArgb(71, 85, 105), 1.6f);
            TextRenderer.DrawText(g, items[i].Text, Theme.Body,
                new Rectangle(r.X + S(50), r.Y, r.Width - S(60), r.Height), Theme.Text,
                TextFormatFlags.VerticalCenter | one);
        }
    }
}
