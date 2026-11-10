using System.Text;
using static BaiTapLon.Ui;

namespace BaiTapLon;

// TV4: báo cáo thống kê.
//  - Quản lý: doanh thu toàn cửa hàng, sản phẩm bán chạy, tồn kho (nhập/xuất/điều chỉnh), hoạt động từng nhân viên.
//  - Nhân viên: chỉ doanh thu + sản phẩm bán chạy trong phạm vi hóa đơn của mình, và xem tồn kho hiện tại.
//  Doanh thu chỉ tính hóa đơn "Đã thanh toán" (không cộng hóa đơn chờ thanh toán hoặc đã hủy).
public partial class Form1
{
    private Control BuildReportsPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };

        // ----- Thanh lọc: loại báo cáo + kiểu thời gian + giá trị thời gian + nút xuất -----
        var kinds = IsManager ? new[] { "rev", "top", "stock", "staff" } : new[] { "rev", "top", "stock" };
        var toolbar = new Card { Dock = DockStyle.Top, Height = S(88) };
        var typeBox = new DropBox();
        typeBox.SetItems(IsManager
            ? new[] { "Doanh thu", "Sản phẩm bán chạy", "Hàng tồn kho", "Hoạt động nhân viên" }
            : new[] { "Doanh thu của tôi", "Sản phẩm tôi đã bán", "Tồn kho hiện tại" });
        var modeBox = new DropBox();
        modeBox.SetItems(new[] { "Theo ngày", "Theo tháng", "Theo năm" });
        var valueBox = new DropBox();
        var export = new RoundedButton
        {
            Text = "Xuất Excel (CSV)",
            Fill = Theme.BlueSoft,
            Fore = Theme.Blue,
            Width = S(190),
            Height = S(44)
        };
        toolbar.Controls.Add(typeBox);
        toolbar.Controls.Add(modeBox);
        toolbar.Controls.Add(valueBox);
        toolbar.Controls.Add(export);
        toolbar.Resize += (_, _) =>
        {
            int y = S(20), h = S(44), gap = S(12);
            export.SetBounds(toolbar.Width - S(24) - export.Width, y, export.Width, h);
            int free = export.Left - gap - S(24);
            int each = Math.Max(S(130), (free - gap * 2) / 3);
            typeBox.SetBounds(S(24), y, each, h);
            modeBox.SetBounds(typeBox.Right + gap, y, each, h);
            valueBox.SetBounds(modeBox.Right + gap, y, each, h);
        };

        var gap1 = new Panel { Dock = DockStyle.Top, Height = S(16), BackColor = Theme.PageBg };
        var statsHost = new Panel { Dock = DockStyle.Top, Height = S(116), BackColor = Theme.PageBg };
        var gap2 = new Panel { Dock = DockStyle.Top, Height = S(16), BackColor = Theme.PageBg };
        var bodyHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };

        page.Controls.Add(bodyHost);     // Fill thêm trước, rồi các khối Top từ dưới lên trên
        page.Controls.Add(gap2);
        page.Controls.Add(statsHost);
        page.Controls.Add(gap1);
        page.Controls.Add(toolbar);

        // ----- Khoảng thời gian -----
        var periods = new List<(string Label, DateTime From, DateTime To)>();
        bool loading = false;

        void LoadPeriods()
        {
            periods.Clear();
            var today = DateTime.Today;
            switch (modeBox.SelectedIndex)
            {
                case 0:
                    for (int i = 0; i < 30; i++)
                    {
                        var d = today.AddDays(-i);
                        periods.Add((i == 0 ? "Hôm nay - " + d.ToString("dd/MM/yyyy") : d.ToString("dddd, dd/MM/yyyy", ViCulture),
                            d, d.AddDays(1)));
                    }
                    break;
                case 1:
                    for (int i = 0; i < 12; i++)
                    {
                        var m = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
                        periods.Add(("Tháng " + m.ToString("MM/yyyy"), m, m.AddMonths(1)));
                    }
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                    {
                        var y = new DateTime(today.Year - i, 1, 1);
                        periods.Add(("Năm " + y.Year, y, y.AddYears(1)));
                    }
                    break;
            }
            loading = true;
            valueBox.SetItems(periods.Select(p => p.Label));
            loading = false;
        }

        // ----- Dữ liệu đang hiển thị (để xuất file) -----
        string exportName = "bao_cao";
        string[] exportHeaders = Array.Empty<string>();
        var exportRows = new List<string[]>();

        void Rebuild()
        {
            int pi = valueBox.SelectedIndex;
            if (pi < 0 || pi >= periods.Count) return;
            var (rangeLabel, rangeFrom, rangeTo) = periods[pi];
            string kind = kinds[Math.Max(0, typeBox.SelectedIndex)];
            int mode = modeBox.SelectedIndex;

            var scope = ScopeInvoices().ToList();
            var inRange = scope.Where(i => i.Date >= rangeFrom && i.Date < rangeTo).ToList();
            var paid = inRange.Where(i => i.Status == Invoice.Paid).ToList();
            decimal revenue = paid.Sum(i => i.Total);

            // --- Thẻ thống kê ---
            var stats = BuildStatRow(
                (IsManager ? "Doanh thu" : "Doanh thu của tôi", FormatMoney(revenue), rangeLabel, Theme.Blue, "chart"),
                ("Hóa đơn đã thanh toán", paid.Count.ToString(),
                    "Trung bình " + FormatMoney(paid.Count == 0 ? 0m : revenue / paid.Count) + " / đơn", Theme.Green, "invoice"),
                ("Chờ thanh toán", inRange.Count(i => i.Status == Invoice.Pending).ToString(),
                    "Chưa tính vào doanh thu", Color.FromArgb(217, 119, 6), "cart"),
                ("Đã hủy", inRange.Count(i => i.Status == Invoice.Cancelled).ToString(),
                    "Không tính vào doanh thu", Theme.Red, "box"));
            stats.Dock = DockStyle.Fill;
            ReplaceContent(statsHost, stats);

            // --- Bảng + biểu đồ theo loại báo cáo ---
            string heading;
            ColumnDef[] cols;
            List<string[]> rows;
            ChartCard? chart = null;
            string who = IsManager ? "toàn cửa hàng" : "của bạn";

            switch (kind)
            {
                case "rev":
                    exportName = "bao_cao_doanh_thu";
                    heading = "Hóa đơn - " + rangeLabel;
                    cols = IsManager
                        ? new[]
                        {
                            new ColumnDef("Mã hóa đơn", 1.1f), new ColumnDef("Khách hàng", 1.7f, ColKind.Link),
                            new ColumnDef("Ngày lập", 1.5f), new ColumnDef("Nhân viên lập", 1.4f),
                            new ColumnDef("Tổng tiền", 1.3f), new ColumnDef("Trạng thái", 1.5f, ColKind.Status)
                        }
                        : new[]
                        {
                            new ColumnDef("Mã hóa đơn", 1.1f), new ColumnDef("Khách hàng", 1.8f, ColKind.Link),
                            new ColumnDef("Ngày lập", 1.5f), new ColumnDef("Tổng tiền", 1.3f),
                            new ColumnDef("Trạng thái", 1.5f, ColKind.Status)
                        };
                    rows = inRange.OrderByDescending(i => i.Date)
                        .Select(i => IsManager
                            ? new[] { i.Code, i.Customer, FormatDate(i.Date), i.StaffName, FormatMoney(i.Total), i.Status }
                            : new[] { i.Code, i.Customer, FormatDate(i.Date), FormatMoney(i.Total), i.Status })
                        .ToList();
                    {
                        var (labels, values, chartTitle) = Trend(scope, mode, rangeFrom);
                        chart = new ChartCard
                        {
                            Title = chartTitle,
                            Subtitle = "Tổng " + FormatMoney(values.Sum()) + "  •  " + who,
                            Labels = labels,
                            Values = values
                        };
                    }
                    break;

                case "top":
                    exportName = "san_pham_ban_chay";
                    heading = (IsManager ? "Sản phẩm bán chạy - " : "Sản phẩm tôi đã bán - ") + rangeLabel;
                    cols = new[]
                    {
                        new ColumnDef("Hạng", 0.6f), new ColumnDef("Mã SP", 0.9f),
                        new ColumnDef("Tên sản phẩm", 2.4f, ColKind.Link),
                        new ColumnDef("Số lượng bán", 1.1f), new ColumnDef("Doanh thu", 1.4f)
                    };
                    rows = paid.SelectMany(i => i.Lines)
                        .GroupBy(l => l.ProductCode)
                        .Select(grp => new
                        {
                            Code = grp.Key,
                            Name = grp.First().ProductName,
                            Qty = grp.Sum(l => l.Qty),
                            Revenue = grp.Sum(l => l.Amount)
                        })
                        .OrderByDescending(x => x.Qty).ThenByDescending(x => x.Revenue)
                        .Take(10)
                        .Select((x, idx) => new[] { (idx + 1).ToString(), x.Code, x.Name, x.Qty.ToString(), FormatMoney(x.Revenue) })
                        .ToList();
                    {
                        var (labels, values, chartTitle) = Trend(scope, mode, rangeFrom);
                        chart = new ChartCard
                        {
                            Title = chartTitle,
                            Subtitle = "Tổng " + FormatMoney(values.Sum()) + "  •  " + who,
                            Labels = labels,
                            Values = values
                        };
                    }
                    break;

                case "stock":
                    exportName = "bao_cao_ton_kho";
                    if (IsManager)
                    {
                        // Tồn cuối = Tồn đầu + Nhập - Xuất + Điều chỉnh (tính trong khoảng thời gian đã chọn)
                        heading = "Báo cáo tồn kho - " + rangeLabel;
                        cols = new[]
                        {
                            new ColumnDef("Mã SP", 0.9f), new ColumnDef("Tên sản phẩm", 2.0f, ColKind.Link),
                            new ColumnDef("Tồn đầu", 0.9f), new ColumnDef("Nhập", 0.8f), new ColumnDef("Xuất", 0.8f),
                            new ColumnDef("Điều chỉnh", 1.0f), new ColumnDef("Tồn cuối", 0.9f),
                            new ColumnDef("Trạng thái", 1.5f, ColKind.Status)
                        };
                        rows = Store.Products.OrderBy(p => p.Code).Select(p =>
                        {
                            var logs = Store.StockLogs.Where(l => l.ProductCode == p.Code).ToList();
                            int closing = p.Stock - logs.Where(l => l.Time >= rangeTo).Sum(l => l.Delta);
                            var period = logs.Where(l => l.Time >= rangeFrom && l.Time < rangeTo).ToList();
                            int imported = period.Where(l => l.Kind == StockLog.In).Sum(l => l.Delta);
                            int exported = -period.Where(l => l.Kind == StockLog.Sale || l.Kind == StockLog.Refund).Sum(l => l.Delta);
                            int adjusted = period.Where(l => l.Kind == StockLog.Adjust).Sum(l => l.Delta);
                            int opening = closing - imported + exported - adjusted;
                            return new[]
                            {
                                p.Code, p.Name, opening.ToString(), imported.ToString(), exported.ToString(),
                                adjusted.ToString("+0;-0;0"), closing.ToString(), p.Status
                            };
                        }).ToList();
                    }
                    else
                    {
                        heading = "Tồn kho hiện tại";
                        cols = new[]
                        {
                            new ColumnDef("Mã SP", 0.9f), new ColumnDef("Tên sản phẩm", 2.2f, ColKind.Link),
                            new ColumnDef("Danh mục", 1.6f, ColKind.Tag), new ColumnDef("Tồn kho", 0.9f),
                            new ColumnDef("Trạng thái", 1.6f, ColKind.Status)
                        };
                        rows = Store.Products.OrderBy(p => p.Code)
                            .Select(p => new[] { p.Code, p.Name, p.Category, p.Stock.ToString(), p.Status })
                            .ToList();
                    }
                    break;

                default:    // "staff" - chỉ Quản lý
                    exportName = "hoat_dong_nhan_vien";
                    heading = "Hoạt động nhân viên - " + rangeLabel;
                    cols = new[]
                    {
                        new ColumnDef("Nhân viên", 2.0f, ColKind.Link), new ColumnDef("Vai trò", 1.2f, ColKind.Tag),
                        new ColumnDef("Đã thanh toán", 1.2f), new ColumnDef("Doanh thu", 1.4f),
                        new ColumnDef("Chờ thanh toán", 1.3f), new ColumnDef("Đã hủy", 0.9f)
                    };
                    var perStaff = Store.Accounts
                        .Where(a => !a.IsManager || inRange.Any(i => i.StaffUser == a.UserName))
                        .Select(a =>
                        {
                            var mine = inRange.Where(i => i.StaffUser == a.UserName).ToList();
                            var mp = mine.Where(i => i.Status == Invoice.Paid).ToList();
                            return new
                            {
                                a.FullName,
                                a.Role,
                                Paid = mp.Count,
                                Revenue = mp.Sum(i => i.Total),
                                Pending = mine.Count(i => i.Status == Invoice.Pending),
                                Cancelled = mine.Count(i => i.Status == Invoice.Cancelled)
                            };
                        })
                        .OrderByDescending(x => x.Revenue)
                        .ToList();
                    rows = perStaff
                        .Select(x => new[] { x.FullName, x.Role, x.Paid.ToString(), FormatMoney(x.Revenue), x.Pending.ToString(), x.Cancelled.ToString() })
                        .ToList();
                    chart = new ChartCard
                    {
                        Title = "Doanh thu theo nhân viên",
                        Subtitle = "Tổng " + FormatMoney(perStaff.Sum(x => x.Revenue)) + "  •  " + rangeLabel,
                        Labels = perStaff.Select(x => x.FullName.Substring(x.FullName.LastIndexOf(' ') + 1)).ToArray(),
                        Values = perStaff.Select(x => x.Revenue).ToArray()
                    };
                    break;
            }

            exportHeaders = cols.Select(c => c.Header).ToArray();
            exportRows = rows;

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };
            var tableCard = BuildTableCard(heading, cols, rows);
            tableCard.Dock = DockStyle.Fill;
            body.Controls.Add(tableCard);                 // Fill thêm trước

            if (chart != null)
            {
                var colGap = new Panel { Dock = DockStyle.Right, Width = S(16), BackColor = Theme.PageBg };
                var right = new Panel { Dock = DockStyle.Right, Width = S(400), BackColor = Theme.PageBg };
                chart.Dock = DockStyle.Top;
                chart.Height = S(300);
                right.Controls.Add(chart);
                body.Controls.Add(colGap);
                body.Controls.Add(right);                 // Right thêm sau cùng

                // Cửa sổ hẹp thì ẩn biểu đồ để bảng không bị bóp
                body.Resize += (_, _) =>
                {
                    bool wide = body.Width >= S(900);
                    right.Visible = wide;
                    colGap.Visible = wide;
                    right.Width = Math.Clamp((int)(body.Width * 0.36), S(340), S(460));
                };
            }
            ReplaceContent(bodyHost, body);
        }

        typeBox.SelectedIndexChanged += (_, _) => Rebuild();
        modeBox.SelectedIndexChanged += (_, _) =>
        {
            LoadPeriods();
            Rebuild();
        };
        valueBox.SelectedIndexChanged += (_, _) =>
        {
            if (!loading) Rebuild();
        };
        export.Click += (_, _) =>
        {
            if (exportRows.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu để xuất.", "Xuất báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ExportCsv(exportName, exportHeaders, exportRows);
        };

        LoadPeriods();
        Rebuild();
        return page;
    }

    /// <summary>Thay toàn bộ nội dung của một khung bằng control mới (giải phóng control cũ).</summary>
    private static void ReplaceContent(Panel host, Control content)
    {
        foreach (var old in host.Controls.Cast<Control>().ToList())
        {
            host.Controls.Remove(old);
            old.Dispose();
        }
        host.Controls.Add(content);
    }

    private static Card BuildTableCard(string headingText, ColumnDef[] cols, List<string[]> rows)
    {
        var card = new Card();
        var heading = new Label
        {
            Text = $"{headingText}  ({rows.Count} dòng)",
            Font = Theme.Heading,
            ForeColor = Theme.Text,
            BackColor = Color.White,
            Dock = DockStyle.Top,
            Height = S(58),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(S(24), 0, 0, 0)
        };
        var table = new ModernTable { Dock = DockStyle.Fill };
        table.SetColumns(cols);
        table.SetRows(rows);
        card.Controls.Add(table);
        card.Controls.Add(heading);
        return card;
    }

    /// <summary>
    /// Số liệu cho biểu đồ doanh thu (chỉ hóa đơn đã thanh toán):
    /// theo ngày = 7 ngày kết thúc vào ngày chọn; theo tháng = gom theo tuần; theo năm = 12 tháng.
    /// </summary>
    private static (string[] Labels, decimal[] Values, string Title) Trend(List<Invoice> scope, int mode, DateTime rangeFrom)
    {
        var paid = scope.Where(i => i.Status == Invoice.Paid).ToList();

        if (mode == 0)
        {
            var days = Enumerable.Range(0, 7).Select(k => rangeFrom.AddDays(k - 6)).ToArray();
            return (days.Select(d => d.ToString("dd/MM")).ToArray(),
                    days.Select(d => paid.Where(i => i.Date.Date == d).Sum(i => i.Total)).ToArray(),
                    "Doanh thu 7 ngày đến " + rangeFrom.ToString("dd/MM"));
        }

        if (mode == 1)
        {
            int daysInMonth = DateTime.DaysInMonth(rangeFrom.Year, rangeFrom.Month);
            int buckets = (daysInMonth + 6) / 7;
            var labels = new string[buckets];
            var values = new decimal[buckets];
            for (int b = 0; b < buckets; b++)
            {
                int d1 = b * 7 + 1;
                int d2 = Math.Min(daysInMonth, d1 + 6);
                labels[b] = $"{d1}-{d2}";
                values[b] = paid
                    .Where(i => i.Date.Year == rangeFrom.Year && i.Date.Month == rangeFrom.Month && i.Date.Day >= d1 && i.Date.Day <= d2)
                    .Sum(i => i.Total);
            }
            return (labels, values, "Doanh thu theo tuần - " + rangeFrom.ToString("MM/yyyy"));
        }

        var monthLabels = Enumerable.Range(1, 12).Select(m => "T" + m).ToArray();
        var monthValues = Enumerable.Range(1, 12)
            .Select(m => paid.Where(i => i.Date.Year == rangeFrom.Year && i.Date.Month == m).Sum(i => i.Total))
            .ToArray();
        return (monthLabels, monthValues, "Doanh thu theo tháng - " + rangeFrom.Year);
    }

    /// <summary>Xuất bảng đang xem ra file .csv (UTF-8 có BOM nên mở bằng Excel không bị lỗi tiếng Việt).</summary>
    private void ExportCsv(string defaultName, string[] headers, IEnumerable<string[]> rows)
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Xuất báo cáo",
            Filter = "Tệp CSV (mở bằng Excel)|*.csv",
            FileName = $"{defaultName}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Quote)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(Quote)));

        try
        {
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            MessageBox.Show("Đã xuất báo cáo:\n" + dlg.FileName, "Xuất báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (IOException ex)
        {
            MessageBox.Show("Không thể ghi file (có thể file đang được mở):\n" + ex.Message, "Xuất báo cáo",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
