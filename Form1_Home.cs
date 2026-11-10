using static BaiTapLon.Ui;


namespace BaiTapLon;


// Trang chủ: thẻ thống kê + hóa đơn gần đây + biểu đồ doanh thu + cảnh báo tồn kho.
// Quản lý thấy số liệu toàn cửa hàng; Nhân viên chỉ thấy số liệu của chính mình.
// (Form1 phải khai báo là "public partial class Form1 : Form")
public partial class Form1
{
    private static string GreetingByTime()
    {
        int h = DateTime.Now.Hour;
        return h < 11 ? "Chào buổi sáng" : h < 14 ? "Chào buổi trưa" : h < 18 ? "Chào buổi chiều" : "Chào buổi tối";
    }


    private Control BuildHomePage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };

        var scope = ScopeInvoices().ToList();
        var today = DateTime.Today;

        // Doanh thu chỉ tính hóa đơn đã thanh toán (không cộng hóa đơn chờ / đã hủy)
        decimal revenueToday = scope.Where(i => i.Status == Invoice.Paid && i.Date.Date == today).Sum(i => i.Total);
        int invoicesToday = scope.Count(i => i.Status != Invoice.Cancelled && i.Date.Date == today);
        int pending = scope.Count(i => i.Status == Invoice.Pending);
        int activeProducts = Store.Products.Count(p => !p.Discontinued);
        int lowStock = Store.LowStock().Count();

        var stats = IsManager
            ? BuildStatRow(
                ("Doanh thu hôm nay", FormatMoney(revenueToday), "Toàn cửa hàng, hóa đơn đã thanh toán", Theme.Blue, "chart"),
                ("Hóa đơn hôm nay", invoicesToday.ToString(), $"{pending} đơn đang chờ thanh toán", Theme.Green, "invoice"),
                ("Sản phẩm", activeProducts.ToString(), "Mặt hàng đang kinh doanh", Color.FromArgb(124, 58, 237), "box"),
                ("Sắp hết hàng", lowStock.ToString(), "Sản phẩm cần nhập thêm", Color.FromArgb(217, 119, 6), "cart"))
            : BuildStatRow(
                ("Doanh thu của tôi hôm nay", FormatMoney(revenueToday), "Hóa đơn tôi đã thu tiền", Theme.Blue, "chart"),
                ("Hóa đơn của tôi hôm nay", invoicesToday.ToString(), $"{pending} đơn đang chờ thanh toán", Theme.Green, "invoice"),
                ("Sản phẩm", activeProducts.ToString(), "Mặt hàng đang kinh doanh", Color.FromArgb(124, 58, 237), "box"),
                ("Sắp hết hàng", lowStock.ToString(), "Cần báo Quản lý nhập thêm", Color.FromArgb(217, 119, 6), "cart"));

        var gap = new Panel { Dock = DockStyle.Top, Height = S(16), BackColor = Theme.PageBg };

        // ----- Phần thân: trái = hóa đơn gần đây, phải = biểu đồ + tồn kho -----
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };

        var invoicesCard = BuildRecentInvoicesCard(scope);
        invoicesCard.Dock = DockStyle.Fill;

        var colGap = new Panel { Dock = DockStyle.Right, Width = S(16), BackColor = Theme.PageBg };
        var right = new Panel { Dock = DockStyle.Right, Width = S(400), BackColor = Theme.PageBg };

        var lowCard = BuildLowStockCard();
        lowCard.Dock = DockStyle.Fill;
        var rightGap = new Panel { Dock = DockStyle.Top, Height = S(16), BackColor = Theme.PageBg };
        var chart = BuildRevenueChart(scope);
        chart.Dock = DockStyle.Top;
        chart.Height = S(220);


        right.Controls.Add(lowCard);     // Fill thêm trước
        right.Controls.Add(rightGap);
        right.Controls.Add(chart);       // Top thêm sau cùng


        body.Controls.Add(invoicesCard); // Fill thêm trước
        body.Controls.Add(colGap);
        body.Controls.Add(right);        // Right thêm sau cùng


        // Cửa sổ hẹp thì ẩn cột phải để bảng hóa đơn không bị bóp
        body.Resize += (_, _) =>
        {
            bool wide = body.Width >= S(900);
            right.Visible = wide;
            colGap.Visible = wide;
            right.Width = Math.Clamp((int)(body.Width * 0.36), S(340), S(460));
        };


        page.Controls.Add(body);         // Fill thêm trước
        page.Controls.Add(gap);
        page.Controls.Add(stats);        // Top thêm sau cùng
        return page;
    }


    private Control BuildRecentInvoicesCard(List<Invoice> scope)
    {
        var card = new Card();
        var heading = new Label
        {
            Text = IsManager ? "Hóa đơn gần đây" : "Hóa đơn gần đây của tôi",
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
            new ColumnDef("Ngày lập", 1.4f),
            new ColumnDef("Tổng tiền", 1.3f),
            new ColumnDef("Trạng thái", 1.6f, ColKind.Status));
        table.SetRows(scope.OrderByDescending(i => i.Date).Take(6)
            .Select(i => new[] { i.Code, i.Customer, i.Date.ToString("dd/MM HH:mm"), FormatMoney(i.Total), i.Status })
            .ToList());
        card.Controls.Add(table);
        card.Controls.Add(heading);
        return card;
    }


    private Control BuildLowStockCard()
    {
        var card = new Card();
        var heading = new Label
        {
            Text = "Cảnh báo tồn kho",
            Font = Theme.Heading,
            ForeColor = Theme.Text,
            BackColor = Color.White,
            Dock = DockStyle.Top,
            Height = S(52),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(S(24), 0, 0, 0)
        };
        var table = new ModernTable
        {
            Dock = DockStyle.Fill,
            HeaderHeight = S(38),
            RowHeight = S(46),
            SidePadding = S(20)
        };
        table.SetColumns(
            new ColumnDef("Sản phẩm", 2.3f),
            new ColumnDef("Tồn", 0.7f),
            new ColumnDef("Trạng thái", 1.4f, ColKind.Status));
        table.SetRows(Store.LowStock()
            .OrderBy(p => p.Stock)
            .Take(4)
            .Select(p => new[] { p.Name, p.Stock.ToString(), p.Status })
            .ToList());
        card.Controls.Add(table);
        card.Controls.Add(heading);
        return card;
    }


    private Control BuildRevenueChart(List<Invoice> scope)
    {
        // Doanh thu 7 ngày gần nhất, tính từ các hóa đơn đã thanh toán trong phạm vi được xem
        var days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6)).ToArray();
        decimal[] revenue = days
            .Select(d => scope.Where(i => i.Status == Invoice.Paid && i.Date.Date == d).Sum(i => i.Total))
            .ToArray();
        return new ChartCard
        {
            Title = "Doanh thu 7 ngày gần đây",
            Subtitle = "Tổng " + FormatMoney(revenue.Sum()) + "  •  " + (IsManager ? "toàn cửa hàng" : "của bạn"),
            Labels = days.Select(d => d.DayOfWeek == DayOfWeek.Sunday ? "CN" : "T" + ((int)d.DayOfWeek + 1)).ToArray(),
            Values = revenue
        };
    }
}
