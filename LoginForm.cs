#pragma warning disable WFO1000


using System.Drawing.Drawing2D;
using static BaiTapLon.Ui;


namespace BaiTapLon;


/// <summary>Màn hình đăng nhập: bên trái giới thiệu (nền gradient xanh), bên phải là form nhập.</summary>
public class LoginForm : Form
{
    private readonly Panel right = new();
    private readonly Panel formHost = new();
    private readonly InputBox userBox = new();
    private readonly InputBox passBox = new();
    private readonly CheckBox showPass = new();
    private readonly Label errorLabel = new();
    private readonly RoundedButton loginButton = new();


    public LoginForm()
    {
        Text = "Đăng nhập - Quản lý bán hàng";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.White;
        Font = Theme.Body;
        ClientSize = new Size(S(940), S(600));


        BuildUi();


        AcceptButton = loginButton;                          // bấm Enter = đăng nhập
        Load += (_, _) => CenterHost();
        Shown += (_, _) => userBox.FocusInput();
    }


    private void BuildUi()
    {
        right.Dock = DockStyle.Fill;
        right.BackColor = Color.White;
        right.Resize += (_, _) => CenterHost();


        formHost.Size = new Size(S(340), S(536));
        formHost.BackColor = Color.White;
        int w = formHost.Width;


        Label MakeLabel(string text, Font font, Color color, int y, int h) => new()
        {
            Text = text,
            Font = font,
            ForeColor = color,
            BackColor = Color.White,
            AutoSize = false,
            Location = new Point(0, y),
            Size = new Size(w, h)
        };


        formHost.Controls.Add(MakeLabel("Đăng nhập", Theme.Title, Theme.Text, 0, S(46)));
        formHost.Controls.Add(MakeLabel("Chào mừng trở lại! Vui lòng đăng nhập để tiếp tục quản lý cửa hàng.",
            Theme.Body, Theme.Muted, S(52), S(46)));


        formHost.Controls.Add(MakeLabel("Tên đăng nhập", Theme.BodyBold, Theme.Text, S(114), S(22)));
        userBox.Icon = "user";
        userBox.Placeholder = "Nhập tên đăng nhập";
        userBox.Location = new Point(0, S(140));
        userBox.Width = w;
        formHost.Controls.Add(userBox);


        formHost.Controls.Add(MakeLabel("Mật khẩu", Theme.BodyBold, Theme.Text, S(202), S(22)));
        passBox.Icon = "lock";
        passBox.Placeholder = "Nhập mật khẩu";
        passBox.Password = true;
        passBox.Location = new Point(0, S(228));
        passBox.Width = w;
        formHost.Controls.Add(passBox);


        showPass.Text = "Hiện mật khẩu";
        showPass.Font = Theme.Small;
        showPass.ForeColor = Theme.Muted;
        showPass.BackColor = Color.White;
        showPass.AutoSize = true;
        showPass.Cursor = Cursors.Hand;
        showPass.Location = new Point(S(2), S(284));
        showPass.CheckedChanged += (_, _) => passBox.Password = !showPass.Checked;
        formHost.Controls.Add(showPass);


        errorLabel.Font = Theme.Small;
        errorLabel.ForeColor = Theme.Red;
        errorLabel.BackColor = Color.White;
        errorLabel.AutoSize = false;
        errorLabel.Location = new Point(0, S(314));
        errorLabel.Size = new Size(w, S(24));
        formHost.Controls.Add(errorLabel);


        loginButton.Text = "Đăng nhập";
        loginButton.Font = Theme.BodyBold;
        loginButton.Location = new Point(0, S(346));
        loginButton.Size = new Size(w, S(48));
        loginButton.Click += (_, _) => TryLogin();
        formHost.Controls.Add(loginButton);


        formHost.Controls.Add(new Label
        {
            Text = "Tài khoản demo\nNhân viên: nam / 123456\nQuản lý: admin / admin123",
            Font = Theme.Small,
            ForeColor = Theme.Navy,
            BackColor = Theme.BlueSoft,
            AutoSize = false,
            Location = new Point(0, S(410)),
            Size = new Size(w, S(72)),
            Padding = new Padding(S(14), 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft
        });

        // Hệ thống không có chức năng tự đăng ký: tài khoản do Quản lý cấp
        formHost.Controls.Add(new Label
        {
            Text = "Chưa có tài khoản? Vui lòng liên hệ Quản lý để được cấp.",
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            BackColor = Color.White,
            AutoSize = false,
            Location = new Point(0, S(494)),
            Size = new Size(w, S(36)),
            TextAlign = ContentAlignment.TopLeft
        });


        right.Controls.Add(formHost);
        Controls.Add(right);                                  // Fill thêm trước
        Controls.Add(new LoginHero { Dock = DockStyle.Left, Width = S(420) });   // Left thêm sau
    }


    private void CenterHost() =>
        formHost.Location = new Point(Math.Max(0, (right.Width - formHost.Width) / 2),
                                      Math.Max(0, (right.Height - formHost.Height) / 2));


    private void TryLogin()
    {
        string user = userBox.Value.Trim();
        string pass = passBox.Value;


        if (user.Length == 0 || pass.Length == 0)
        {
            errorLabel.Text = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.";
            Shake();
            return;
        }


        if (Session.SignIn(user, pass, out string error) == null)
        {
            errorLabel.Text = error;
            passBox.Value = "";
            passBox.FocusInput();
            Shake();
            return;
        }


        DialogResult = DialogResult.OK;                       // Program.cs sẽ mở Form1
    }


    /// <summary>Hiệu ứng rung nhẹ khi nhập sai.</summary>
    private void Shake()
    {
        int[] offsets = { -12, 12, -9, 9, -5, 5, 0 };
        int i = 0;
        int baseLeft = formHost.Left;
        var timer = new System.Windows.Forms.Timer { Interval = 25 };
        timer.Tick += (_, _) =>
        {
            formHost.Left = baseLeft + S(offsets[i]);
            if (++i >= offsets.Length)
            {
                timer.Stop();
                timer.Dispose();
            }
        };
        timer.Start();
    }
}


/// <summary>Khối bên trái màn hình đăng nhập: nền gradient, logo, khẩu hiệu, 3 ưu điểm.</summary>
internal class LoginHero : Panel
{
    private static readonly Font BrandName = new("Segoe UI", 15F, FontStyle.Bold);
    private static readonly Font Headline = new("Segoe UI", 25F, FontStyle.Bold);
    private static readonly Font Lead = new("Segoe UI", 11F);
    private static readonly Font Feature = new("Segoe UI", 11F);


    public LoginHero()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }


    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 0 || Height <= 0) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;


        using (var bg = new LinearGradientBrush(ClientRectangle, Theme.Navy, Theme.Blue, 55f))
            g.FillRectangle(bg, ClientRectangle);


        // Vài hình tròn mờ trang trí
        using (var soft = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
        {
            g.FillEllipse(soft, Width - S(220), -S(120), S(340), S(340));
            g.FillEllipse(soft, -S(120), Height - S(220), S(300), S(300));
            g.FillEllipse(soft, Width - S(150), Height - S(280), S(180), S(180));
        }


        // Logo + tên
        var logo = new Rectangle(S(48), S(52), S(52), S(52));
        using (var path = Theme.RoundRect(logo, S(12)))
        using (var fill = new SolidBrush(Color.FromArgb(46, 255, 255, 255)))
            g.FillPath(fill, path);
        Icons.Draw(g, "book", new Rectangle(logo.X + S(10), logo.Y + S(10), S(32), S(32)), Color.White, 2f);
        const TextFormatFlags one = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(g, "Quản lý bán hàng", BrandName, new Point(S(114), S(55)), Color.White, one);
        TextRenderer.DrawText(g, "SALES MANAGEMENT", Theme.Tiny, new Point(S(116), S(82)), Color.FromArgb(191, 219, 254), one);


        // Khẩu hiệu
        TextRenderer.DrawText(g, "Quản lý bán hàng", Headline, new Point(S(48), S(188)), Color.White, one);
        TextRenderer.DrawText(g, "đơn giản & hiệu quả", Headline, new Point(S(48), S(228)), Color.White, one);
        TextRenderer.DrawText(g, "Theo dõi sản phẩm, hóa đơn, khách hàng và doanh thu ngay trên một màn hình.",
            Lead, new Rectangle(S(48), S(290), Width - S(96), S(60)), Color.FromArgb(191, 219, 254),
            TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);


        // 3 ưu điểm
        string[] features = { "Quản lý sản phẩm và tồn kho", "Lập hóa đơn nhanh chóng", "Báo cáo doanh thu trực quan" };
        int y = S(384);
        foreach (var text in features)
        {
            var c = new Rectangle(S(48), y, S(28), S(28));
            using (var fill = new SolidBrush(Color.FromArgb(46, 255, 255, 255)))
                g.FillEllipse(fill, c);
               using (var pen = new Pen(Color.White, 2.2f * Ui.Scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })  
                g.DrawLines(pen, new[]
                {
                    new Point(c.X + S(8), c.Y + S(14)),
                    new Point(c.X + S(12), c.Y + S(18)),
                    new Point(c.X + S(20), c.Y + S(9))
                });
            TextRenderer.DrawText(g, text, Feature, new Point(S(90), y + S(3)), Color.White, one);
            y += S(46);
        }


        TextRenderer.DrawText(g, "© 2026 Bài tập lớn - Quản lý bán hàng", Theme.Tiny,
            new Point(S(48), Height - S(44)), Color.FromArgb(147, 197, 253), one);
    }
}
