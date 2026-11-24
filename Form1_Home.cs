using static BaiTapLon.Ui;


namespace BaiTapLon;


// =====================================================================
//  TRANG CHỦ = bảng điều khiển hỗ trợ ra quyết định, bày sẵn mọi thông tin người dùng quan tâm,
//  không bắt phải đi tìm chức năng. Mỗi vai trò thấy một trang chủ riêng:
//   - Admin             : toàn bộ số liệu (doanh thu, tiền nhập, lãi, hóa đơn theo tháng, hàng cần nhập, top bán chạy...).
//   - Thủ kho           : chỉ số lượng tồn, hàng hết/sắp hết cần nhập, biến động kho. KHÔNG có giá, doanh thu, lợi nhuận.
//   - Nhân viên bán hàng: số liệu của riêng mình + hàng sắp hết để tư vấn.
// (Form1 phải khai báo là "public partial class Form1 : Form")
// =====================================================================
public partial class Form1
{
    private static string GreetingByTime()
    {
        int h = DateTime.Now.Hour;
        return h < 11 ? "Chào buổi sáng" : h < 14 ? "Chào buổi trưa" : h < 18 ? "Chào buổi chiều" : "Chào buổi tối";
    }

    private Control BuildHomePage() =>
        IsAdmin ? BuildAdminHome() : IsWarehouse ? BuildWarehouseHome() : BuildStaffHome();

    private static Panel Gap(int h = 16) => new() { Dock = DockStyle.Top, Height = S(h), BackColor = Theme.PageBg };

    /// <summary>Số tiền dạng rút gọn cho thẻ thống kê: 12,3 tr / 1,25 tỷ (thẻ nhỏ nên không đủ chỗ ghi đầy đủ).</summary>
    private static string FormatCompact(decimal v)
    {
        if (Math.Abs(v) >= 1_000_000_000m) return (v / 1_000_000_000m).ToString("0.##", ViCulture) + " tỷ";
        if (Math.Abs(v) >= 1_000_000m) return (v / 1_000_000m).ToString("0.#", ViCulture) + " tr";
        return FormatMoney(v);
    }

    // =====================================================================
    //  ADMIN
    // =====================================================================
    private Control BuildAdminHome()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg, AutoScroll = true };

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var nextMonth = monthStart.AddMonths(1);

        // Doanh thu chỉ tính hóa đơn đã thanh toán (không cộng hóa đơn chờ / đã hủy)
        var paidAll = Store.Invoices.Where(i => i.Status == Invoice.Paid).ToList();
        var paidMonth = paidAll.Where(i => i.Date >= monthStart && i.Date < nextMonth).ToList();
        var invoicesMonth = Store.Invoices.Where(i => i.Date >= monthStart && i.Date < nextMonth).ToList();
        decimal revMonth = paidMonth.Sum(i => i.Total);
        decimal revToday = paidAll.Where(i => i.Date.Date == today).Sum(i => i.Total);

        var confirmed = Store.Imports.Where(r => r.Status == ImportReceipt.Confirmed).ToList();
        var importMonth = confirmed.Where(r => r.Date >= monthStart && r.Date < nextMonth).ToList();

        var costByCode = Store.Products.ToDictionary(p => p.Code, p => p.CostPrice);
        decimal Cost(string code) => costByCode.TryGetValue(code, out var c) ? c : 0m;
        decimal cogs = paidMonth.SelectMany(i => i.Lines).Sum(l => Cost(l.ProductCode) * l.Qty);

        decimal stockValue = Store.Products.Where(p => !p.Discontinued).Sum(p => p.CostPrice * p.Stock);
        var low = Store.LowStock().OrderBy(p => p.Stock).ThenBy(p => p.Code).ToList();
        int outOfStock = low.Count(p => p.Stock == 0);

        var row1 = BuildStatRow(
            ($"Doanh thu tháng {today.Month}", FormatCompact(revMonth), $"Hôm nay: {FormatCompact(revToday)}", Theme.Blue, "chart"),
            ("Tiền nhập hàng tháng này", FormatCompact(importMonth.Sum(r => r.Total)), $"{importMonth.Count} phiếu nhập", Color.FromArgb(124, 58, 237), "box"),
            ("Lãi gộp tạm tính", FormatCompact(revMonth - cogs), "Doanh thu - giá vốn", Theme.Green, "chart"),
            ($"Hóa đơn tháng {today.Month}", invoicesMonth.Count(i => i.Status != Invoice.Cancelled).ToString(),
                $"{invoicesMonth.Count(i => i.Status == Invoice.Pending)} chờ thu • {invoicesMonth.Count(i => i.Status == Invoice.Cancelled)} hủy",
                Color.FromArgb(13, 148, 136), "invoice"));

        var row2 = BuildStatRow(
            ("Tổng doanh thu", FormatCompact(paidAll.Sum(i => i.Total)), $"{paidAll.Count} hóa đơn đã thu", Theme.Blue, "chart"),
            ("Tổng tiền nhập hàng", FormatCompact(confirmed.Sum(r => r.Total)), $"{confirmed.Count} phiếu nhập", Color.FromArgb(124, 58, 237), "box"),
            ("Giá trị tồn kho", FormatCompact(stockValue), $"{Store.Products.Count(p => !p.Discontinued)} mặt hàng", Color.FromArgb(13, 148, 136), "box"),
            ("Cần nhập ngay", low.Count.ToString(), $"{outOfStock} hết hàng • {low.Count - outOfStock} sắp hết", Color.FromArgb(217, 119, 6), "cart"));

        // ----- Thân trang: trái = hàng cần nhập, phải = biểu đồ tháng + top bán chạy -----
        var body = new Panel { Dock = DockStyle.Top, Height = S(470), BackColor = Theme.PageBg };

        var restock = BuildRestockCard(low);
        restock.Dock = DockStyle.Fill;

        var colGap = new Panel { Dock = DockStyle.Right, Width = S(16), BackColor = Theme.PageBg };
        var right = new Panel { Dock = DockStyle.Right, Width = S(400), BackColor = Theme.PageBg };
        var chart = BuildMonthlyChart(paidAll);
        chart.Dock = DockStyle.Top;
        chart.Height = S(230);
        var top = BuildTopProductsCard(paidMonth, $"Top bán chạy tháng {today.Month}");
        top.Dock = DockStyle.Fill;
        right.Controls.Add(top);        // Fill thêm trước
        right.Controls.Add(Gap());
        right.Controls.Add(chart);      // Top thêm sau cùng

        body.Controls.Add(restock);     // Fill thêm trước
        body.Controls.Add(colGap);
        body.Controls.Add(right);       // Right thêm sau cùng
        body.Resize += (_, _) =>
        {
            bool wide = body.Width >= S(900);
            right.Visible = wide;
            colGap.Visible = wide;
            right.Width = Math.Clamp((int)(body.Width * 0.36), S(340), S(460));
        };

        var recentHost = new Panel { Dock = DockStyle.Top, Height = S(430), BackColor = Theme.PageBg };
        var recent = BuildRecentInvoicesCard(Store.Invoices.ToList(), 7, showStaff: true);
        recent.Dock = DockStyle.Fill;
        recentHost.Controls.Add(recent);

        // Control thêm sau nằm cao hơn: thêm từ dưới lên trên
        page.Controls.Add(recentHost);
        page.Controls.Add(Gap());
        page.Controls.Add(body);
        page.Controls.Add(Gap());
        page.Controls.Add(row2);
        page.Controls.Add(Gap());
        page.Controls.Add(row1);
        return page;
    }

    // =====================================================================
    //  THỦ KHO - chỉ thấy số lượng, không có tiền
    // =====================================================================
    private Control BuildWarehouseHome()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };

        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var active = Store.Products.Where(p => !p.Discontinued).ToList();
        var low = Store.LowStock().OrderBy(p => p.Stock).ThenBy(p => p.Code).ToList();
        int outOfStock = low.Count(p => p.Stock == 0);
        int importedMonth = Store.StockLogs.Where(l => l.Kind == StockLog.In && l.Time >= monthStart).Sum(l => l.Delta);

        var stats = BuildStatRow(
            ("Hết hàng", outOfStock.ToString(), "Cần nhập ngay", Theme.Red, "cart"),
            ("Sắp hết hàng", (low.Count - outOfStock).ToString(), "Tồn không quá mức tối thiểu", Color.FromArgb(217, 119, 6), "cart"),
            ("Mặt hàng đang kinh doanh", active.Count.ToString(), $"Tổng {active.Sum(p => p.Stock)} sản phẩm trong kho", Theme.Blue, "box"),
            ($"Đã nhập tháng {DateTime.Today.Month}", importedMonth.ToString(), "Số lượng sản phẩm nhập vào", Theme.Green, "box"));

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };

        var restock = BuildRestockCard(low);
        restock.Dock = DockStyle.Fill;

        var colGap = new Panel { Dock = DockStyle.Right, Width = S(16), BackColor = Theme.PageBg };
        var right = new Panel { Dock = DockStyle.Right, Width = S(400), BackColor = Theme.PageBg };
        var recent = BuildRecentStockCard();
        recent.Dock = DockStyle.Fill;
        right.Controls.Add(recent);

        body.Controls.Add(restock);     // Fill thêm trước
        body.Controls.Add(colGap);
        body.Controls.Add(right);       // Right thêm sau cùng
        body.Resize += (_, _) =>
        {
            bool wide = body.Width >= S(900);
            right.Visible = wide;
            colGap.Visible = wide;
            right.Width = Math.Clamp((int)(body.Width * 0.36), S(340), S(460));
        };

        page.Controls.Add(body);        // Fill thêm trước
        page.Controls.Add(Gap());
        page.Controls.Add(stats);       // Top thêm sau cùng
        return page;
    }

    // =====================================================================
    //  NHÂN VIÊN BÁN HÀNG - số liệu của riêng mình
    // =====================================================================
    private Control BuildStaffHome()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };

        var scope = ScopeInvoices().ToList();
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        decimal revenueToday = scope.Where(i => i.Status == Invoice.Paid && i.Date.Date == today).Sum(i => i.Total);
        decimal revenueMonth = scope.Where(i => i.Status == Invoice.Paid && i.Date >= monthStart).Sum(i => i.Total);
        int invoicesToday = scope.Count(i => i.Status != Invoice.Cancelled && i.Date.Date == today);
        int pending = scope.Count(i => i.Status == Invoice.Pending);
        int lowStock = Store.LowStock().Count();

        var stats = BuildStatRow(
            ("Doanh thu của tôi hôm nay", FormatCompact(revenueToday), "Hóa đơn tôi đã thu tiền", Theme.Blue, "chart"),
            ($"Doanh thu tháng {today.Month}", FormatCompact(revenueMonth), "Của riêng tôi", Theme.Green, "chart"),
            ("Hóa đơn hôm nay", invoicesToday.ToString(), $"{pending} đơn đang chờ thanh toán", Color.FromArgb(124, 58, 237), "invoice"),
            ("Sắp hết hàng", lowStock.ToString(), "Cần báo Admin/Thủ kho nhập", Color.FromArgb(217, 119, 6), "cart"));

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };

        var invoicesCard = BuildRecentInvoicesCard(scope, 6, showStaff: false);
        invoicesCard.Dock = DockStyle.Fill;

        var colGap = new Panel { Dock = DockStyle.Right, Width = S(16), BackColor = Theme.PageBg };
        var right = new Panel { Dock = DockStyle.Right, Width = S(400), BackColor = Theme.PageBg };

        var lowCard = BuildLowStockCard();
        lowCard.Dock = DockStyle.Fill;
        var chart = BuildRevenueChart(scope);
        chart.Dock = DockStyle.Top;
        chart.Height = S(220);

        right.Controls.Add(lowCard);     // Fill thêm trước
        right.Controls.Add(Gap());
        right.Controls.Add(chart);       // Top thêm sau cùng

        body.Controls.Add(invoicesCard); // Fill thêm trước
        body.Controls.Add(colGap);
        body.Controls.Add(right);        // Right thêm sau cùng

        body.Resize += (_, _) =>
        {
            bool wide = body.Width >= S(900);
            right.Visible = wide;
            colGap.Visible = wide;
            right.Width = Math.Clamp((int)(body.Width * 0.36), S(340), S(460));
        };

        page.Controls.Add(body);         // Fill thêm trước
        page.Controls.Add(Gap());
        page.Controls.Add(stats);        // Top thêm sau cùng
        return page;
    }

    // =====================================================================
    //  CÁC THẺ DÙNG CHUNG
    // =====================================================================
    private static Label CardHeading(string text, int height = 58) => new()
    {
        Text = text,
        Font = Theme.Heading,
        ForeColor = Theme.Text,
        BackColor = Color.White,
        Dock = DockStyle.Top,
        Height = S(height),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(S(24), 0, 0, 0)
    };

    /// <summary>Danh sách TOÀN BỘ hàng hết / sắp hết kèm số lượng gợi ý nhập (không hiện giá, dùng được cho cả Admin và Thủ kho).</summary>
    private Control BuildRestockCard(List<Product> low)
    {
        var card = new Card();
        var table = new ModernTable { Dock = DockStyle.Fill, HeaderHeight = S(44), RowHeight = S(50), SidePadding = S(24) };
        table.SetColumns(
            new ColumnDef("Mã SP", 0.9f),
            new ColumnDef("Sản phẩm cần nhập", 2.4f, ColKind.Link),
            new ColumnDef("Tồn", 0.6f),
            new ColumnDef("Tối thiểu", 0.9f),
            new ColumnDef("Gợi ý nhập", 1.0f),
            new ColumnDef("Trạng thái", 1.4f, ColKind.Status));
        table.SetRows(low
            .Select(p => new[] { p.Code, p.Name, p.Stock.ToString(), p.MinStock.ToString(), "+" + p.SuggestedImport, p.Status })
            .ToList());
        card.Controls.Add(table);
        card.Controls.Add(CardHeading(low.Count == 0
            ? "Không có mặt hàng nào cần nhập"
            : $"Hàng hết / sắp hết - cần nhập ({low.Count} mặt hàng)"));
        return card;
    }

    private Control BuildRecentStockCard()
    {
        var names = Store.Products.ToDictionary(p => p.Code, p => p.Name);
        var card = new Card();
        var table = new ModernTable { Dock = DockStyle.Fill, HeaderHeight = S(38), RowHeight = S(46), SidePadding = S(20) };
        table.SetColumns(
            new ColumnDef("Thời gian", 1.3f),
            new ColumnDef("Sản phẩm", 2.2f),
            new ColumnDef("Biến động", 1.0f));
        table.SetRows(Store.StockLogs.OrderByDescending(l => l.Time).Take(12)
            .Select(l => new[]
            {
                l.Time.ToString("dd/MM HH:mm"),
                names.TryGetValue(l.ProductCode, out var n) ? n : l.ProductCode,
                $"{l.Delta:+0;-0;0} ({l.Kind})"
            })
            .ToList());
        card.Controls.Add(table);
        card.Controls.Add(CardHeading("Nhập / xuất kho gần đây", 52));
        return card;
    }

    private Control BuildTopProductsCard(List<Invoice> paid, string heading)
    {
        var card = new Card();
        var table = new ModernTable { Dock = DockStyle.Fill, HeaderHeight = S(38), RowHeight = S(40), SidePadding = S(20) };
        table.SetColumns(
            new ColumnDef("Sản phẩm", 2.3f),
            new ColumnDef("SL", 0.6f),
            new ColumnDef("Doanh thu", 1.5f));
        table.SetRows(paid.SelectMany(i => i.Lines)
            .GroupBy(l => l.ProductCode)
            .Select(g => new { Name = g.First().ProductName, Qty = g.Sum(l => l.Qty), Rev = g.Sum(l => l.Amount) })
            .OrderByDescending(x => x.Qty).ThenByDescending(x => x.Rev)
            .Take(5)
            .Select(x => new[] { x.Name, x.Qty.ToString(), FormatMoney(x.Rev) })
            .ToList());
        card.Controls.Add(table);
        card.Controls.Add(CardHeading(heading, 52));
        return card;
    }

    private Control BuildRecentInvoicesCard(List<Invoice> scope, int take, bool showStaff)
    {
        var card = new Card();
        var table = new ModernTable { Dock = DockStyle.Fill, HeaderHeight = S(44), RowHeight = S(46) };
        if (showStaff)
            table.SetColumns(
                new ColumnDef("Mã hóa đơn", 1.0f),
                new ColumnDef("Khách hàng", 1.7f, ColKind.Link),
                new ColumnDef("Ngày lập", 1.3f),
                new ColumnDef("Nhân viên lập", 1.5f),
                new ColumnDef("Tổng tiền", 1.3f),
                new ColumnDef("Trạng thái", 1.5f, ColKind.Status));
        else
            table.SetColumns(
                new ColumnDef("Mã hóa đơn", 1.1f),
                new ColumnDef("Khách hàng", 1.8f, ColKind.Link),
                new ColumnDef("Ngày lập", 1.4f),
                new ColumnDef("Tổng tiền", 1.3f),
                new ColumnDef("Trạng thái", 1.6f, ColKind.Status));
        table.SetRows(scope.OrderByDescending(i => i.Date).Take(take)
            .Select(i => showStaff
                ? new[] { i.Code, i.Customer, i.Date.ToString("dd/MM HH:mm"), i.StaffName, FormatMoney(i.Total), i.Status }
                : new[] { i.Code, i.Customer, i.Date.ToString("dd/MM HH:mm"), FormatMoney(i.Total), i.Status })
            .ToList());
        card.Controls.Add(table);
        card.Controls.Add(CardHeading(showStaff ? "Hóa đơn gần đây" : "Hóa đơn gần đây của tôi"));
        return card;
    }

    private Control BuildLowStockCard()
    {
        var card = new Card();
        var table = new ModernTable { Dock = DockStyle.Fill, HeaderHeight = S(38), RowHeight = S(46), SidePadding = S(20) };
        table.SetColumns(
            new ColumnDef("Sản phẩm", 2.3f),
            new ColumnDef("Tồn", 0.7f),
            new ColumnDef("Trạng thái", 1.4f, ColKind.Status));
        table.SetRows(Store.LowStock()
            .OrderBy(p => p.Stock)
            .Take(6)
            .Select(p => new[] { p.Name, p.Stock.ToString(), p.Status })
            .ToList());
        card.Controls.Add(table);
        card.Controls.Add(CardHeading("Cảnh báo tồn kho", 52));
        return card;
    }

    /// <summary>Doanh thu 6 tháng gần nhất toàn cửa hàng (Admin).</summary>
    private ChartCard BuildMonthlyChart(List<Invoice> paidAll)
    {
        var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var months = Enumerable.Range(0, 6).Select(i => first.AddMonths(i - 5)).ToArray();
        decimal[] revenue = months
            .Select(m => paidAll.Where(i => i.Date.Year == m.Year && i.Date.Month == m.Month).Sum(i => i.Total))
            .ToArray();
        return new ChartCard
        {
            Title = "Doanh thu 6 tháng gần đây",
            Subtitle = "Tháng này: " + FormatCompact(revenue[revenue.Length - 1]) + "  •  toàn cửa hàng",
            Labels = months.Select(m => "T" + m.Month).ToArray(),
            Values = revenue
        };
    }

    private ChartCard BuildRevenueChart(List<Invoice> scope)
    {
        // Doanh thu 7 ngày gần nhất, tính từ các hóa đơn đã thanh toán trong phạm vi được xem
        var days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6)).ToArray();
        decimal[] revenue = days
            .Select(d => scope.Where(i => i.Status == Invoice.Paid && i.Date.Date == d).Sum(i => i.Total))
            .ToArray();
        return new ChartCard
        {
            Title = "Doanh thu 7 ngày gần đây",
            Subtitle = "Tổng " + FormatCompact(revenue.Sum()) + "  •  của bạn",
            Labels = days.Select(d => d.DayOfWeek == DayOfWeek.Sunday ? "CN" : "T" + ((int)d.DayOfWeek + 1)).ToArray(),
            Values = revenue
        };
    }
}
