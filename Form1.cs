using System.Globalization;
using static BaiTapLon.Ui;

namespace BaiTapLon;

public class Form1 : Form
{
    private const string UserName = "Nguyễn Văn Nam";
    private const string UserRole = "Nhân viên bán hàng";

    private static readonly CultureInfo ViCulture = new("vi-VN");

    private readonly Sidebar sidebar = new();
    private readonly Panel main = new();
    private readonly Panel contentHost = new();
    private readonly TitleLabel pageTitle = new();
    private readonly Label pageSubtitle = new();
    private readonly UserBadge userBadge = new();
    private readonly Dictionary<string, NavItem> navItems = new();

    // ---------- Dữ liệu mẫu (sau này thay bằng dữ liệu từ SQL Server) ----------
    private readonly List<Product> products = new()
    {
        new("SP001", "Bàn phím cơ K500", "Phụ kiện", 850_000m, 18),
        new("SP002", "Chuột không dây M220", "Phụ kiện", 320_000m, 4),
        new("SP003", "Tai nghe Gaming H7", "Thiết bị âm thanh", 690_000m, 0),
        new("SP004", "Màn hình 24 inch", "Màn hình", 2_850_000m, 12),
        new("SP005", "USB 64GB", "Lưu trữ", 180_000m, 3),
        new("SP006", "Laptop văn phòng A15", "Máy tính", 12_500_000m, 7),
        new("SP007", "Webcam Full HD", "Phụ kiện", 540_000m, 9),
        new("SP008", "Loa Bluetooth S30", "Thiết bị âm thanh", 450_000m, 11),
        new("SP009", "Ổ cứng SSD 512GB", "Lưu trữ", 1_290_000m, 15),
        new("SP010", "Bàn di chuột cỡ lớn", "Phụ kiện", 120_000m, 25),
        new("SP011", "Router Wi-Fi AC1200", "Phụ kiện", 750_000m, 2),
        new("SP012", "Màn hình cong 27 inch", "Màn hình", 4_990_000m, 6)
    };

    private readonly List<Invoice> invoices = new()
    {
        new("HD001", "Nguyễn Minh Anh", "07/11/2026", 1_170_000m, "Đã thanh toán"),
        new("HD002", "Trần Quốc Bảo", "07/11/2026", 2_850_000m, "Đã thanh toán"),
        new("HD003", "Lê Thu Hà", "06/11/2026", 690_000m, "Chờ thanh toán"),
        new("HD004", "Phạm Hoàng Long", "06/11/2026", 12_500_000m, "Đã thanh toán"),
        new("HD005", "Đỗ Ngọc Mai", "05/11/2026", 500_000m, "Đã hủy"),
        new("HD006", "Vũ Hải Đăng", "05/11/2026", 3_340_000m, "Đã thanh toán"),
        new("HD007", "Bùi Thanh Tâm", "04/11/2026", 960_000m, "Chờ thanh toán"),
        new("HD008", "Hoàng Gia Huy", "04/11/2026", 1_890_000m, "Đã thanh toán")
    };

    private readonly List<string[]> customers = new()
    {
        new[] { "KH001", "Nguyễn Minh Anh", "0901234567", "minhanh@email.com", "Chi tiết" },
        new[] { "KH002", "Trần Quốc Bảo", "0912345678", "quocbao@email.com", "Chi tiết" },
        new[] { "KH003", "Lê Thu Hà", "0987654321", "thuha@email.com", "Chi tiết" },
        new[] { "KH004", "Phạm Hoàng Long", "0934567890", "hoanglong@email.com", "Chi tiết" },
        new[] { "KH005", "Đỗ Ngọc Mai", "0966778899", "ngocmai@email.com", "Chi tiết" },
        new[] { "KH006", "Vũ Hải Đăng", "0977112233", "haidang@email.com", "Chi tiết" },
        new[] { "KH007", "Bùi Thanh Tâm", "0905998877", "thanhtam@email.com", "Chi tiết" }
    };

    private readonly List<string[]> staff = new()
    {
        new[] { "NV001", "Nguyễn Văn Nam", "Nhân viên", "0901111222", "Đang làm" },
        new[] { "NV002", "Trần Thị Lan", "Quản lý", "0903333444", "Đang làm" },
        new[] { "NV003", "Lê Minh Tuấn", "Nhân viên", "0905555666", "Đang làm" },
        new[] { "NV004", "Phạm Thu Trang", "Thu ngân", "0907777888", "Đang làm" },
        new[] { "NV005", "Ngô Quang Huy", "Kho", "0909999000", "Nghỉ việc" }
    };

    public Form1()
    {
        Text = "Quản lý bán hàng";
        StartPosition = FormStartPosition.CenterScreen;

        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1366, 768);
        MinimumSize = new Size(Math.Min(S(1000), wa.Width), Math.Min(S(640), wa.Height));
        Size = new Size(Math.Min(S(1360), wa.Width - S(40)), Math.Min(S(840), wa.Height - S(40)));

        BackColor = Theme.PageBg;
        Font = Theme.Body;
        DoubleBuffered = true;

        BuildShell();
        ShowPage("Trang chủ");
    }

    // =====================================================================
    //  KHUNG CHÍNH: sidebar + header + vùng nội dung
    // =====================================================================
    private void BuildShell()
    {
        // ----- Vùng bên phải -----
        main.Dock = DockStyle.Fill;
        main.BackColor = Theme.PageBg;
        main.Padding = new Padding(S(32), S(26), S(32), S(24));

        contentHost.Dock = DockStyle.Fill;
        contentHost.BackColor = Theme.PageBg;

        var header = new Panel { Dock = DockStyle.Top, Height = S(96), BackColor = Theme.PageBg };
        pageTitle.SetBounds(0, 0, S(800), S(48));
        pageSubtitle.Font = Theme.Body;
        pageSubtitle.ForeColor = Theme.Muted;
        pageSubtitle.BackColor = Theme.PageBg;
        pageSubtitle.SetBounds(S(2), S(50), S(800), S(26));
        userBadge.UserName = UserName;
        userBadge.Role = UserRole;
        header.Controls.Add(pageTitle);
        header.Controls.Add(pageSubtitle);
        header.Controls.Add(userBadge);
        header.Resize += (_, _) =>
        {
            userBadge.Location = new Point(header.Width - userBadge.Width, S(4));
            int w = Math.Max(S(200), header.Width - userBadge.Width - S(20));
            pageTitle.Width = w;
            pageSubtitle.Width = w;
        };

        main.Controls.Add(contentHost);   // Fill thêm trước
        main.Controls.Add(header);        // Top thêm sau
        Controls.Add(main);

        // ----- Sidebar -----
        sidebar.Dock = DockStyle.Left;
        sidebar.Width = S(256);

        var items = new (string Text, string Icon)[]
        {
            ("Trang chủ", "home"),
            ("Quản lý sản phẩm", "box"),
            ("Bán hàng", "cart"),
            ("Quản lý hóa đơn", "invoice"),
            ("Khách hàng", "user"),
            ("Nhân viên", "team"),
            ("Báo cáo thống kê", "chart")
        };

        int y = S(142);
        foreach (var item in items)
        {
            string pageName = item.Text;
            var nav = new NavItem(item.Text, item.Icon)
            {
                Location = new Point(S(14), y),
                Width = sidebar.Width - S(28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            nav.Click += (_, _) => ShowPage(pageName);
            navItems[pageName] = nav;
            sidebar.Controls.Add(nav);
            y += S(52);
        }

        var logout = new RoundedButton
        {
            Text = "Đăng xuất",
            Icon = "logout",
            Fill = Color.White,
            Fore = Color.FromArgb(51, 65, 85),
            Outline = Theme.Line,
            LeftAligned = true,
            Font = Theme.Body,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        logout.Click += (_, _) =>
            MessageBox.Show("Phần đăng nhập/đăng xuất sẽ được tích hợp với thành viên phụ trách sau.",
                "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        sidebar.Controls.Add(logout);
        sidebar.Resize += (_, _) => logout.SetBounds(S(14), sidebar.Height - S(68), sidebar.Width - S(28), S(48));

        Controls.Add(sidebar);            // Left thêm cuối cùng
    }

    private void ShowPage(string page)
    {
        foreach (var pair in navItems)
            pair.Value.Active = pair.Key == page;

        if (page == "Trang chủ")
        {
            pageTitle.SetParts(
                ("Xin chào,", Theme.Text, Theme.Title),
                (" " + UserName, Theme.Blue, Theme.Title),
                (" 👋", Color.FromArgb(251, 191, 36), Theme.Emoji));
            pageSubtitle.Text = "Chào mừng bạn đến với hệ thống quản lý bán hàng";
        }
        else
        {
            pageTitle.SetParts((page, Theme.Text, Theme.Title));
            pageSubtitle.Text = GetSubtitle(page);
        }

        contentHost.SuspendLayout();
        foreach (var old in contentHost.Controls.Cast<Control>().ToList())
        {
            contentHost.Controls.Remove(old);
            old.Dispose();
        }

        Control content = page switch
        {
            "Trang chủ" => BuildHomePage(),
            "Quản lý sản phẩm" => BuildProductsPage(),
            "Bán hàng" => BuildSalesPage(),
            "Quản lý hóa đơn" => BuildInvoicesPage(),
            "Khách hàng" => BuildCustomersPage(),
            "Nhân viên" => BuildStaffPage(),
            "Báo cáo thống kê" => BuildReportsPage(),
            _ => BuildHomePage()
        };
        content.Dock = DockStyle.Fill;
        contentHost.Controls.Add(content);
        contentHost.ResumeLayout(true);
    }

    private static string GetSubtitle(string page) => page switch
    {
        "Quản lý sản phẩm" => "Theo dõi thông tin và tình trạng tồn kho sản phẩm",
        "Bán hàng" => "Tạo đơn hàng và tính tổng tiền thanh toán",
        "Quản lý hóa đơn" => "Theo dõi lịch sử và trạng thái thanh toán",
        "Khách hàng" => "Quản lý thông tin khách hàng",
        "Nhân viên" => "Quản lý thông tin nhân viên",
        "Báo cáo thống kê" => "Tổng quan doanh thu và tình hình kinh doanh",
        _ => "Hệ thống quản lý bán hàng"
    };

    // =====================================================================
    //  TRANG CHỦ: thẻ thống kê + hóa đơn gần đây
    // =====================================================================
    private Control BuildHomePage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };

        var stats = BuildStatRow(
            ("Doanh thu hôm nay", "18.240.000 đ", "Từ các hóa đơn đã thanh toán", Theme.Blue, "chart"),
            ("Hóa đơn hôm nay", "24", "Đơn hàng trong ngày", Theme.Green, "invoice"),
            ("Sản phẩm", products.Count.ToString(), "Mặt hàng trong danh mục", Color.FromArgb(124, 58, 237), "box"),
            ("Sắp hết hàng", products.Count(p => p.Stock > 0 && p.Stock <= 5).ToString(), "Sản phẩm cần nhập thêm", Color.FromArgb(217, 119, 6), "cart"));

        var gap = new Panel { Dock = DockStyle.Top, Height = S(16), BackColor = Theme.PageBg };

        var card = new Card { Dock = DockStyle.Fill };
        var heading = new Label
        {
            Text = "Hóa đơn gần đây",
            Font = Theme.Heading,
            ForeColor = Theme.Text,
            BackColor = Color.White,
            Dock = DockStyle.Top,
            Height = S(58),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(S(24), 0, 0, 0)
        };
        var table = new ModernTable { Dock = DockStyle.Fill };
        table.SetColumns(
            new ColumnDef("Mã hóa đơn", 1.1f),
            new ColumnDef("Khách hàng", 1.8f, ColKind.Link),
            new ColumnDef("Ngày lập", 1.2f),
            new ColumnDef("Tổng tiền", 1.3f),
            new ColumnDef("Trạng thái", 1.6f, ColKind.Status));
        table.SetRows(invoices.Take(6)
            .Select(i => new[] { i.Code, i.Customer, i.Date, FormatMoney(i.Total), i.Status })
            .ToList());
        card.Controls.Add(table);
        card.Controls.Add(heading);

        page.Controls.Add(card);   // Fill trước
        page.Controls.Add(gap);
        page.Controls.Add(stats);  // Top sau cùng
        return page;
    }

    private Control BuildStatRow(params (string Title, string Value, string Note, Color Accent, string Icon)[] items)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = S(116),
            ColumnCount = items.Length,
            RowCount = 1,
            BackColor = Theme.PageBg,
            Margin = Padding.Empty
        };
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        for (int i = 0; i < items.Length; i++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / items.Length));
            var it = items[i];
            row.Controls.Add(new StatCard
            {
                Title = it.Title,
                Value = it.Value,
                Note = it.Note,
                Accent = it.Accent,
                Icon = it.Icon,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, i == items.Length - 1 ? 0 : S(16), 0)
            }, i, 0);
        }
        return row;
    }

    // =====================================================================
    //  QUẢN LÝ SẢN PHẨM
    // =====================================================================
    private Control BuildProductsPage()
    {
        var categories = new[] { "Tất cả danh mục" }
            .Concat(products.Select(p => p.Category).Distinct().OrderBy(c => c)).ToArray();
        var statuses = new[] { "Tất cả trạng thái", "Còn hàng", "Sắp hết", "Hết hàng" };

        var page = new DataListPage(
            "Tìm kiếm theo tên sản phẩm, mã hàng, danh mục...",
            "sản phẩm",
            new[] { categories, statuses },
            new[]
            {
                new ColumnDef("Mã SP", 0.9f),
                new ColumnDef("Tên sản phẩm", 2.2f, ColKind.Link),
                new ColumnDef("Danh mục", 1.6f, ColKind.Tag),
                new ColumnDef("Giá bán", 1.3f),
                new ColumnDef("Tồn kho", 0.9f),
                new ColumnDef("Trạng thái", 1.6f, ColKind.Status),
                new ColumnDef("Thao tác", 1.2f, ColKind.Action)
            },
            "Thêm sản phẩm");

        page.Query = (term, f) =>
        {
            string cat = f.Count > 0 ? f[0] : "";
            string st = f.Count > 1 ? f[1] : "";
            return products
                .Where(p =>
                    (p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     p.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     p.Category.Contains(term, StringComparison.OrdinalIgnoreCase)) &&
                    (IsAll(cat) || p.Category == cat) &&
                    (IsAll(st) || p.Status == st))
                .Select(p => new[] { p.Code, p.Name, p.Category, FormatMoney(p.Price), p.Stock.ToString(), p.Status, "Sửa" })
                .ToList();
        };

        page.ExtraButton!.Click += (_, _) => ShowProductDialog(null, () => page.Reload());
        page.RowAction += row =>
        {
            var product = products.FirstOrDefault(p => p.Code == row[0]);
            if (product != null) ShowProductDialog(product, () => page.Reload(false));
        };

        page.Reload();
        return page;
    }

    private void ShowProductDialog(Product? existing, Action onSaved)
    {
        using var dialog = new Form
        {
            Text = existing == null ? "Thêm sản phẩm" : "Sửa sản phẩm",
            ClientSize = new Size(S(460), S(560)),
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
            Text = existing == null ? "Thêm sản phẩm mới" : "Cập nhật sản phẩm",
            Font = Theme.Heading,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(S(28), S(22))
        });

        int y = S(72);
        T AddField<T>(string caption, T input) where T : Control
        {
            dialog.Controls.Add(new Label
            {
                Text = caption,
                ForeColor = Theme.Muted,
                Font = Theme.Small,
                AutoSize = true,
                Location = new Point(S(28), y)
            });
            input.SetBounds(S(28), y + S(22), S(404), S(44));
            dialog.Controls.Add(input);
            y += S(80);
            return input;
        }

        var cats = products.Select(p => p.Category)
            .Concat(new[] { "Phụ kiện", "Máy tính", "Màn hình", "Lưu trữ", "Thiết bị âm thanh" })
            .Distinct().OrderBy(c => c).ToList();

        var code = AddField("Mã sản phẩm", new InputBox { Value = existing?.Code ?? "", Placeholder = "VD: SP013" });
        var name = AddField("Tên sản phẩm", new InputBox { Value = existing?.Name ?? "", Placeholder = "Nhập tên sản phẩm" });
        var category = AddField("Danh mục", new DropBox());
        category.SetItems(cats);
        int catIndex = cats.IndexOf(existing?.Category ?? "Phụ kiện");
        category.SelectedIndex = catIndex < 0 ? 0 : catIndex;
        var price = AddField("Giá bán (VNĐ)", new InputBox { Value = existing == null ? "" : ((long)existing.Price).ToString(), Placeholder = "VD: 850000" });
        var stock = AddField("Số lượng tồn", new InputBox { Value = existing?.Stock.ToString() ?? "0" });

        var save = new RoundedButton { Text = "Lưu sản phẩm", Fill = Theme.Blue };
        save.SetBounds(S(28), y + S(4), S(404), S(46));
        save.Click += (_, _) =>
        {
            string c = code.Value.Trim();
            string n = name.Value.Trim();
            if (c.Length == 0 || n.Length == 0)
            {
                MessageBox.Show(dialog, "Vui lòng nhập mã và tên sản phẩm.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string rawPrice = new string(price.Value.Where(char.IsDigit).ToArray());
            if (!decimal.TryParse(rawPrice, out decimal pr) || !int.TryParse(stock.Value.Trim(), out int st) || st < 0)
            {
                MessageBox.Show(dialog, "Giá bán và số lượng phải là số hợp lệ.", "Dữ liệu chưa đúng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var item = new Product(c, n, category.SelectedText, pr, st);
            bool duplicate = products.Any(p => p.Code.Equals(c, StringComparison.OrdinalIgnoreCase) && !ReferenceEquals(p, existing));
            if (duplicate)
            {
                MessageBox.Show(dialog, "Mã sản phẩm đã tồn tại.", "Trùng mã", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (existing == null)
            {
                products.Add(item);
            }
            else
            {
                int index = products.IndexOf(existing);
                if (index >= 0) products[index] = item;
            }
            onSaved();
            dialog.DialogResult = DialogResult.OK;
        };
        dialog.Controls.Add(save);
        dialog.ShowDialog(this);
    }

    // =====================================================================
    //  BÁN HÀNG
    // =====================================================================
    private Control BuildSalesPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };
        var lines = new List<SaleLine>();
        var available = products.Where(p => p.Stock > 0).ToList();

        // --- Thẻ chọn sản phẩm ---
        var top = new Card { Dock = DockStyle.Top, Height = S(88) };
        var combo = new DropBox();
        combo.SetItems(available.Select(p => $"{p.Code} - {p.Name} ({FormatMoney(p.Price)})"));
        var qty = new InputBox { Value = "1", Placeholder = "Số lượng" };
        var add = new RoundedButton { Text = "Thêm vào đơn", Icon = "plus", Width = S(170), Height = S(44) };
        top.Controls.Add(combo);
        top.Controls.Add(qty);
        top.Controls.Add(add);
        top.Resize += (_, _) =>
        {
            int y = S(20), h = S(44);
            add.SetBounds(top.Width - S(24) - add.Width, y, add.Width, h);
            qty.SetBounds(add.Left - S(12) - S(110), y, S(110), h);
            combo.SetBounds(S(24), y, Math.Max(S(200), qty.Left - S(12) - S(24)), h);
        };

        var gap = new Panel { Dock = DockStyle.Top, Height = S(16), BackColor = Theme.PageBg };

        // --- Thẻ đơn hàng ---
        var card = new Card { Dock = DockStyle.Fill };
        var table = new ModernTable { Dock = DockStyle.Fill };
        table.SetColumns(
            new ColumnDef("Mã SP", 1f),
            new ColumnDef("Tên sản phẩm", 2.6f, ColKind.Link),
            new ColumnDef("Đơn giá", 1.4f),
            new ColumnDef("Số lượng", 1f),
            new ColumnDef("Thành tiền", 1.5f),
            new ColumnDef("Thao tác", 1.1f, ColKind.Action, Theme.Red));

        var bar = new Panel { Dock = DockStyle.Bottom, Height = S(84), BackColor = Color.White };
        var total = new TitleLabel { BackColor = Color.White };
        var pay = new RoundedButton { Text = "Hoàn tất thanh toán", Fill = Theme.Green, Width = S(220), Height = S(46) };
        bar.Controls.Add(total);
        bar.Controls.Add(pay);
        bar.Resize += (_, _) =>
        {
            pay.SetBounds(bar.Width - S(24) - pay.Width, (bar.Height - pay.Height) / 2, pay.Width, pay.Height);
            total.SetBounds(S(24), 0, Math.Max(S(100), pay.Left - S(40)), bar.Height);
        };

        card.Controls.Add(table);
        card.Controls.Add(bar);

        void Redraw()
        {
            table.SetRows(lines.Select(l => new[]
            {
                l.Product.Code, l.Product.Name, FormatMoney(l.Product.Price), l.Qty.ToString(),
                FormatMoney(l.Product.Price * l.Qty), "Xóa"
            }).ToList());
            decimal sum = lines.Sum(l => l.Product.Price * l.Qty);
            total.SetParts(
                ("Tổng thanh toán:", Theme.Muted, Theme.BodyBold),
                ("   " + FormatMoney(sum), Theme.Blue, Theme.Heading));
        }
        Redraw();

        add.Click += (_, _) =>
        {
            if (combo.SelectedIndex < 0) return;
            var product = available[combo.SelectedIndex];
            if (!int.TryParse(qty.Value.Trim(), out int count) || count <= 0)
            {
                MessageBox.Show("Số lượng phải là số nguyên dương.", "Số lượng không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var line = lines.FirstOrDefault(l => l.Product.Code == product.Code);
            int newQty = (line?.Qty ?? 0) + count;
            if (newQty > product.Stock)
            {
                MessageBox.Show($"Số lượng vượt quá tồn kho (còn {product.Stock}).", "Không đủ hàng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (line == null) lines.Add(new SaleLine(product, count));
            else line.Qty = newQty;
            Redraw();
        };

        table.ActionClicked += row =>
        {
            lines.RemoveAll(l => l.Product.Code == row[0]);
            Redraw();
        };

        pay.Click += (_, _) =>
        {
            if (lines.Count == 0)
            {
                MessageBox.Show("Vui lòng thêm ít nhất một sản phẩm.", "Chưa có sản phẩm", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            decimal sum = lines.Sum(l => l.Product.Price * l.Qty);
            MessageBox.Show($"Đơn hàng mẫu đã được tạo.\nTổng thanh toán: {FormatMoney(sum)}\n\nChưa lưu vào cơ sở dữ liệu.",
                "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
            lines.Clear();
            Redraw();
        };

        page.Controls.Add(card);
        page.Controls.Add(gap);
        page.Controls.Add(top);
        return page;
    }

    // =====================================================================
    //  HÓA ĐƠN / KHÁCH HÀNG / NHÂN VIÊN
    // =====================================================================
    private Control BuildInvoicesPage()
    {
        var statuses = new[] { "Tất cả trạng thái", "Đã thanh toán", "Chờ thanh toán", "Đã hủy" };
        var page = new DataListPage(
            "Tìm kiếm theo mã hóa đơn hoặc tên khách hàng...",
            "hóa đơn",
            new[] { statuses },
            new[]
            {
                new ColumnDef("Mã hóa đơn", 1.1f),
                new ColumnDef("Khách hàng", 1.9f, ColKind.Link),
                new ColumnDef("Ngày lập", 1.2f),
                new ColumnDef("Tổng tiền", 1.4f),
                new ColumnDef("Trạng thái", 1.7f, ColKind.Status),
                new ColumnDef("Thao tác", 1.2f, ColKind.Action)
            });

        page.Query = (term, f) =>
        {
            string st = f.Count > 0 ? f[0] : "";
            return invoices
                .Where(i =>
                    (i.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     i.Customer.Contains(term, StringComparison.OrdinalIgnoreCase)) &&
                    (IsAll(st) || i.Status == st))
                .Select(i => new[] { i.Code, i.Customer, i.Date, FormatMoney(i.Total), i.Status, "Chi tiết" })
                .ToList();
        };
        // Hóa đơn đã hủy: nút chuyển xám (giống nút "Mượn" bị khóa trong ảnh mẫu)
        page.Table.ActionEnabled = row => row[4] != "Đã hủy";
        page.RowAction += row =>
            MessageBox.Show($"Mã hóa đơn: {row[0]}\nKhách hàng: {row[1]}\nNgày lập: {row[2]}\nTổng tiền: {row[3]}\nTrạng thái: {row[4]}",
                "Chi tiết hóa đơn", MessageBoxButtons.OK, MessageBoxIcon.Information);

        page.Reload();
        return page;
    }

    private Control BuildCustomersPage()
    {
        var page = BuildSimpleListPage(
            "Tìm kiếm theo tên, số điện thoại hoặc mã khách hàng...",
            "khách hàng",
            new[]
            {
                new ColumnDef("Mã khách hàng", 1.2f),
                new ColumnDef("Họ tên", 2f, ColKind.Link),
                new ColumnDef("Số điện thoại", 1.5f),
                new ColumnDef("Email", 2.2f),
                new ColumnDef("Thao tác", 1.2f, ColKind.Action)
            },
            customers);
        page.RowAction += row =>
            MessageBox.Show($"Mã khách hàng: {row[0]}\nHọ tên: {row[1]}\nSố điện thoại: {row[2]}\nEmail: {row[3]}",
                "Chi tiết khách hàng", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return page;
    }

    private Control BuildStaffPage()
    {
        return BuildSimpleListPage(
            "Tìm kiếm theo tên, mã nhân viên hoặc số điện thoại...",
            "nhân viên",
            new[]
            {
                new ColumnDef("Mã nhân viên", 1.2f),
                new ColumnDef("Họ tên", 2f, ColKind.Link),
                new ColumnDef("Chức vụ", 1.4f, ColKind.Tag),
                new ColumnDef("Số điện thoại", 1.5f),
                new ColumnDef("Trạng thái", 1.6f, ColKind.Status)
            },
            staff,
            4,
            new[] { "Tất cả trạng thái", "Đang làm", "Nghỉ việc" });
    }

    private DataListPage BuildSimpleListPage(string placeholder, string itemName, ColumnDef[] columns,
        List<string[]> data, int statusColumn = -1, string[]? statusOptions = null)
    {
        var filters = statusOptions == null ? Array.Empty<string[]>() : new[] { statusOptions };
        var page = new DataListPage(placeholder, itemName, filters, columns);
        page.Query = (term, f) =>
        {
            string st = f.Count > 0 ? f[0] : "";
            return data
                .Where(r =>
                    (term.Length == 0 || r.Any(cell => cell.Contains(term, StringComparison.OrdinalIgnoreCase))) &&
                    (statusColumn < 0 || IsAll(st) || r[statusColumn] == st))
                .ToList();
        };
        page.Reload();
        return page;
    }

    // =====================================================================
    //  BÁO CÁO THỐNG KÊ
    // =====================================================================
    private Control BuildReportsPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };

        decimal revenue = invoices.Where(i => i.Status == "Đã thanh toán").Sum(i => i.Total);
        int paid = invoices.Count(i => i.Status == "Đã thanh toán");
        int cancelled = invoices.Count(i => i.Status == "Đã hủy");

        var stats = BuildStatRow(
            ("Doanh thu (dữ liệu mẫu)", FormatMoney(revenue), "Hóa đơn đã thanh toán", Theme.Blue, "chart"),
            ("Đơn đã thanh toán", paid.ToString(), "Trong dữ liệu minh họa", Theme.Green, "invoice"),
            ("Đơn đã hủy", cancelled.ToString(), "Không tính vào doanh thu", Theme.Red, "cart"));

        var gap = new Panel { Dock = DockStyle.Top, Height = S(16), BackColor = Theme.PageBg };

        var card = new Card { Dock = DockStyle.Fill };
        var heading = new Label
        {
            Text = "Tổng hợp hóa đơn",
            Font = Theme.Heading,
            ForeColor = Theme.Text,
            BackColor = Color.White,
            Dock = DockStyle.Top,
            Height = S(58),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(S(24), 0, 0, 0)
        };
        var note = new Label
        {
            Text = "Dữ liệu đang là dữ liệu mẫu. Khi tích hợp SQL Server, các chỉ số sẽ được tính từ hóa đơn thực tế.",
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            BackColor = Color.White,
            Dock = DockStyle.Bottom,
            Height = S(44),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(S(24), 0, 0, 0)
        };
        var table = new ModernTable { Dock = DockStyle.Fill };
        table.SetColumns(
            new ColumnDef("Mã hóa đơn", 1.1f),
            new ColumnDef("Khách hàng", 1.8f, ColKind.Link),
            new ColumnDef("Ngày lập", 1.2f),
            new ColumnDef("Tổng tiền", 1.3f),
            new ColumnDef("Trạng thái", 1.6f, ColKind.Status));
        table.SetRows(invoices
            .Select(i => new[] { i.Code, i.Customer, i.Date, FormatMoney(i.Total), i.Status })
            .ToList());
        card.Controls.Add(table);
        card.Controls.Add(note);
        card.Controls.Add(heading);

        page.Controls.Add(card);
        page.Controls.Add(gap);
        page.Controls.Add(stats);
        return page;
    }

    // =====================================================================
    //  TIỆN ÍCH
    // =====================================================================
    private static bool IsAll(string s) => s.Length == 0 || s.StartsWith("Tất cả", StringComparison.Ordinal);

    private static string FormatMoney(decimal value) => value.ToString("#,##0", ViCulture) + " đ";

    private sealed record Product(string Code, string Name, string Category, decimal Price, int Stock)
    {
        public string Status => Stock == 0 ? "Hết hàng" : Stock <= 5 ? "Sắp hết" : "Còn hàng";
    }

    private sealed record Invoice(string Code, string Customer, string Date, decimal Total, string Status);

    private sealed class SaleLine(Product product, int qty)
    {
        public Product Product { get; } = product;
        public int Qty { get; set; } = qty;
    }
}
