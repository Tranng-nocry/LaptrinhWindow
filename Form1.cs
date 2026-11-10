using System.Globalization;
using static BaiTapLon.Ui;

namespace BaiTapLon;

public partial class Form1 : Form
{
    private static Account Me => Session.Current!;
    private static bool IsManager => Session.IsManager;
    private static string FullName => Session.Current?.FullName ?? "Khách";
    private static string RoleName => Session.Current?.Role ?? "";

    private static readonly CultureInfo ViCulture = new("vi-VN");

    private readonly Sidebar sidebar = new();
    private readonly Panel main = new();
    private readonly Panel contentHost = new();
    private readonly TitleLabel pageTitle = new();
    private readonly Label pageSubtitle = new();
    private readonly UserBadge userBadge = new();
    private readonly Dictionary<string, NavItem> navItems = new();
    private long userMenuClosedAt;

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

        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing && Session.Current != null &&
                MessageBox.Show("Bạn có chắc muốn thoát chương trình?", "Xác nhận",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                e.Cancel = true;
        };
    }

    // =====================================================================
    //  PHÂN QUYỀN MENU: mỗi vai trò chỉ thấy các trang của mình
    // =====================================================================
    private static (string Text, string Icon)[] PagesForRole() => IsManager
        ? new[]
        {
            ("Trang chủ", "home"),
            ("Quản lý sản phẩm", "box"),
            ("Quản lý hóa đơn", "invoice"),
            ("Khách hàng", "user"),
            ("Nhân viên", "team"),
            ("Báo cáo thống kê", "chart")
        }
        : new[]
        {
            ("Trang chủ", "home"),
            ("Sản phẩm", "box"),
            ("Bán hàng", "cart"),
            ("Hóa đơn của tôi", "invoice"),
            ("Khách hàng", "user"),
            ("Báo cáo của tôi", "chart")
        };

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

        userBadge.UserName = FullName;
        userBadge.Role = RoleName;
        userBadge.Click += (_, _) => ShowUserMenu();

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

        // ----- Sidebar (không còn nút Đăng xuất: nằm trong menu hồ sơ ở góc phải trên) -----
        sidebar.Dock = DockStyle.Left;
        sidebar.Width = S(256);

        int y = S(142);
        foreach (var item in PagesForRole())
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

        Controls.Add(sidebar);            // Left thêm cuối cùng
    }

    private void ShowPage(string page)
    {
        if (!navItems.ContainsKey(page)) page = "Trang chủ";   // không có quyền -> về trang chủ

        foreach (var pair in navItems)
            pair.Value.Active = pair.Key == page;

        if (page == "Trang chủ")
        {
            pageTitle.SetParts(
                (GreetingByTime() + ",", Theme.Text, Theme.Title),
                (" " + FullName, Theme.Blue, Theme.Title),
                (" 👋", Color.FromArgb(251, 191, 36), Theme.Emoji));
            var today = DateTime.Now.ToString("dddd, dd/MM/yyyy", ViCulture);
            pageSubtitle.Text = $"Hôm nay là {today} - chúc bạn một ngày làm việc hiệu quả";
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
            "Quản lý sản phẩm" or "Sản phẩm" => BuildProductsPage(),
            "Bán hàng" => BuildSalesPage(),
            "Quản lý hóa đơn" or "Hóa đơn của tôi" => BuildInvoicesPage(),
            "Khách hàng" => BuildCustomersPage(),
            "Nhân viên" => BuildStaffPage(),
            "Báo cáo thống kê" or "Báo cáo của tôi" => BuildReportsPage(),
            _ => BuildHomePage()
        };
        content.Dock = DockStyle.Fill;
        contentHost.Controls.Add(content);
        contentHost.ResumeLayout(true);
    }

    private static string GetSubtitle(string page) => page switch
    {
        "Quản lý sản phẩm" => "Quản lý sản phẩm, giá bán, nhập kho và điều chỉnh tồn kho",
        "Sản phẩm" => "Tra cứu sản phẩm và số lượng tồn kho để tư vấn, bán hàng",
        "Bán hàng" => "Chọn khách hàng, thêm sản phẩm, tính tiền và thanh toán",
        "Quản lý hóa đơn" => "Theo dõi toàn bộ hóa đơn, xem chi tiết và hủy hóa đơn khi cần",
        "Hóa đơn của tôi" => "Lịch sử các hóa đơn do bạn lập",
        "Khách hàng" => IsManager ? "Quản lý thông tin khách hàng" : "Thêm, sửa và tìm kiếm khách hàng",
        "Nhân viên" => "Cấp tài khoản và quản lý nhân viên",
        "Báo cáo thống kê" => "Doanh thu, sản phẩm bán chạy, tồn kho và hoạt động nhân viên",
        "Báo cáo của tôi" => "Doanh thu và hóa đơn do bạn lập",
        _ => "Hệ thống quản lý bán hàng"
    };

    // =====================================================================
    //  MENU HỒ SƠ NGƯỜI DÙNG (góc phải trên)
    // =====================================================================
    private void ShowUserMenu()
    {
        // Bấm vào avatar khi menu đang mở: menu tự đóng trước, tránh mở lại ngay
        if (Environment.TickCount64 - userMenuClosedAt < 250) return;

        var popup = new UserMenuPopup(FullName, RoleName, Math.Max(userBadge.Width, S(290)));
        popup.ItemChosen += key => BeginInvoke(new Action(() => HandleUserMenu(key)));
        popup.FormClosed += (_, _) => userMenuClosedAt = Environment.TickCount64;
        popup.Location = userBadge.PointToScreen(new Point(userBadge.Width - popup.Width, userBadge.Height + S(6)));
        popup.Show(this);
    }

    private void HandleUserMenu(string key)
    {
        switch (key)
        {
            case "profile": ShowProfileDialog(); break;
            case "password": ShowChangePasswordDialog(); break;
            case "logout": Logout(); break;
        }
    }

    private void Logout()
    {
        if (MessageBox.Show("Bạn có chắc muốn đăng xuất?", "Đăng xuất",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        Session.SignOut();
        Close();   // Program.cs sẽ hiện lại màn hình đăng nhập
    }

    // =====================================================================
    //  THẺ THỐNG KÊ (dùng cho Trang chủ + Báo cáo)
    // =====================================================================
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
    //  SẢN PHẨM
    //  Quản lý: xem/tìm/lọc, thêm, sửa, cập nhật giá, ngừng kinh doanh, nhập kho, điều chỉnh tồn.
    //  Nhân viên: chỉ xem, tìm kiếm, lọc theo danh mục (không có nút thao tác).
    // =====================================================================
    private Control BuildProductsPage()
    {
        var categories = new[] { "Tất cả danh mục" }
            .Concat(Store.Products.Select(p => p.Category).Distinct().OrderBy(c => c)).ToArray();
        var statuses = new[] { "Tất cả trạng thái", "Còn hàng", "Sắp hết", "Hết hàng", "Ngừng kinh doanh" };

        var columns = new List<ColumnDef>
        {
            new("Mã SP", 0.9f),
            new("Tên sản phẩm", 2.2f, ColKind.Link),
            new("Danh mục", 1.6f, ColKind.Tag),
            new("Giá bán", 1.3f),
            new("Tồn kho", 0.9f),
            new("Trạng thái", 1.6f, ColKind.Status)
        };
        if (IsManager) columns.Add(new ColumnDef("Thao tác", 1.2f, ColKind.Action));

        var page = new DataListPage(
            "Tìm kiếm theo tên sản phẩm, mã hàng, danh mục...",
            "sản phẩm",
            new[] { categories, statuses },
            columns.ToArray(),
            IsManager ? "Thêm sản phẩm" : null);

        page.Query = (term, f) =>
        {
            string cat = f.Count > 0 ? f[0] : "";
            string st = f.Count > 1 ? f[1] : "";
            return Store.Products
                .Where(p =>
                    (p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     p.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     p.Category.Contains(term, StringComparison.OrdinalIgnoreCase)) &&
                    (IsAll(cat) || p.Category == cat) &&
                    (IsAll(st) || p.Status == st))
                .Select(p =>
                {
                    var cells = new List<string> { p.Code, p.Name, p.Category, FormatMoney(p.Price), p.Stock.ToString(), p.Status };
                    if (IsManager) cells.Add("Thao tác");
                    return cells.ToArray();
                })
                .ToList();
        };

        if (IsManager)
        {
            page.ExtraButton!.Click += (_, _) => ShowProductDialog(null, () => page.Reload());
            page.RowAction += row =>
            {
                var p = Store.Products.FirstOrDefault(x => x.Code == row[0]);
                if (p == null) return;
                ShowRowMenu(
                    ("Sửa thông tin / cập nhật giá", () => ShowProductDialog(p, () => page.Reload(false))),
                    ("Nhập kho", () => ShowStockInDialog(p, () => page.Reload(false))),
                    ("Điều chỉnh tồn kho (kiểm kê)", () => ShowAdjustDialog(p, () => page.Reload(false))),
                    (p.Discontinued ? "Kinh doanh lại" : "Ngừng kinh doanh", () =>
                    {
                        p.Discontinued = !p.Discontinued;
                        page.Reload(false);
                    }));
            };
        }

        page.Reload();
        return page;
    }

    private void ShowProductDialog(Product? existing, Action onSaved)
    {
        var cats = Store.Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();

        var d = new DialogBuilder(existing == null ? "Thêm sản phẩm" : "Sửa sản phẩm",
            existing == null ? "Thêm sản phẩm mới" : "Cập nhật sản phẩm");
        var code = d.AddInput("Mã sản phẩm", existing?.Code ?? Store.NextProductCode(), "VD: SP013", readOnly: existing != null);
        var name = d.AddInput("Tên sản phẩm", existing?.Name ?? "", "Nhập tên sản phẩm");
        var category = d.AddDrop("Danh mục", cats, existing?.Category);
        var newCategory = d.AddInput("Danh mục mới (nếu chưa có trong danh sách)", "", "Để trống nếu dùng danh mục ở trên");
        var price = d.AddInput("Giá bán (VNĐ)", existing == null ? "" : ((long)existing.Price).ToString(), "VD: 850000");
        d.AddNote(existing == null
            ? "Sản phẩm mới có tồn kho bằng 0. Dùng \"Nhập kho\" để thêm số lượng."
            : $"Tồn kho hiện tại: {existing.Stock}. Muốn thay đổi tồn kho hãy dùng \"Nhập kho\" hoặc \"Điều chỉnh tồn kho\".");

        bool saved = d.Show(this, "Lưu sản phẩm", () =>
        {
            string c = code.Value.Trim();
            string n = name.Value.Trim();
            if (c.Length == 0 || n.Length == 0) return "Vui lòng nhập mã và tên sản phẩm.";

            string cat = newCategory.Value.Trim().Length > 0 ? newCategory.Value.Trim() : category.SelectedText;
            if (cat.Length == 0) return "Vui lòng chọn hoặc nhập danh mục.";

            string rawPrice = new string(price.Value.Where(char.IsDigit).ToArray());
            if (!decimal.TryParse(rawPrice, out decimal pr) || pr <= 0) return "Giá bán phải là số lớn hơn 0.";

            if (existing == null)
            {
                if (Store.Products.Any(p => p.Code.Equals(c, StringComparison.OrdinalIgnoreCase)))
                    return "Mã sản phẩm đã tồn tại.";
                Store.Products.Add(new Product { Code = c, Name = n, Category = cat, Price = pr, Stock = 0 });
            }
            else
            {
                existing.Name = n;
                existing.Category = cat;
                existing.Price = pr;    // hóa đơn cũ giữ nguyên giá lúc bán (đã lưu trong chi tiết hóa đơn)
            }
            return null;
        });
        if (saved) onSaved();
    }

    private void ShowStockInDialog(Product p, Action onSaved)
    {
        var d = new DialogBuilder("Nhập kho", "Nhập kho sản phẩm");
        d.AddInput("Sản phẩm", $"{p.Code} - {p.Name}", readOnly: true);
        d.AddInput("Tồn kho hiện tại", p.Stock.ToString(), readOnly: true);
        var qty = d.AddInput("Số lượng nhập", "", "VD: 20");
        var note = d.AddInput("Ghi chú (nhà cung cấp, số phiếu...)", "", "Không bắt buộc");

        bool saved = d.Show(this, "Xác nhận nhập kho", () =>
        {
            if (!int.TryParse(qty.Value.Trim(), out int n) || n <= 0)
                return "Số lượng nhập phải là số nguyên dương.";
            return Store.StockIn(p, n, note.Value.Trim(), Me);   // chỉ khi xác nhận mới tăng tồn
        }, Theme.Green);
        if (saved) onSaved();
    }

    private void ShowAdjustDialog(Product p, Action onSaved)
    {
        var d = new DialogBuilder("Điều chỉnh tồn kho", "Điều chỉnh tồn kho");
        d.AddInput("Sản phẩm", $"{p.Code} - {p.Name}", readOnly: true);
        d.AddInput("Tồn kho hiện tại (theo hệ thống)", p.Stock.ToString(), readOnly: true);
        var qty = d.AddInput("Số lượng thực tế sau kiểm kê", p.Stock.ToString());
        var reason = d.AddInput("Lý do điều chỉnh (bắt buộc)", "", "VD: Hàng hỏng, kiểm kê lệch...");

        bool saved = d.Show(this, "Lưu điều chỉnh", () =>
        {
            if (!int.TryParse(qty.Value.Trim(), out int n) || n < 0)
                return "Số lượng thực tế phải là số nguyên không âm.";
            return Store.AdjustStock(p, n, reason.Value, Me);
        });
        if (saved) onSaved();
    }

    // =====================================================================
    //  TIỆN ÍCH
    // =====================================================================
    private static bool IsAll(string s) => s.Length == 0 || s.StartsWith("Tất cả", StringComparison.Ordinal);

    private static string FormatMoney(decimal value) => value.ToString("#,##0", ViCulture) + " đ";

    private static string FormatDate(DateTime d) => d.ToString("dd/MM/yyyy HH:mm");

    /// <summary>Hiện menu thao tác ngay tại vị trí con trỏ chuột (dùng cho nút "Thao tác" trong bảng).</summary>
    private void ShowRowMenu(params (string Text, Action Do)[] items)
    {
        var menu = new ContextMenuStrip { Font = Theme.Body, ShowImageMargin = false };
        foreach (var (text, action) in items)
        {
            var mi = new ToolStripMenuItem(text) { Padding = new Padding(S(4), S(6), S(4), S(6)) };
            mi.Click += (_, _) => action();
            menu.Items.Add(mi);
        }
        menu.Closed += (_, _) => BeginInvoke(new Action(() => menu.Dispose()));
        menu.Show(Cursor.Position);
    }

    /// <summary>Hóa đơn nằm trong phạm vi được xem: Quản lý xem tất cả, Nhân viên chỉ xem hóa đơn của mình.</summary>
    private static IEnumerable<Invoice> ScopeInvoices() =>
        IsManager ? Store.Invoices : Store.Invoices.Where(i => i.StaffUser == Me.UserName);

    private sealed class CartLine(Product product, int qty)
    {
        public Product Product { get; } = product;
        public int Qty { get; set; } = qty;
    }
}
