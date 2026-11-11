using System.Drawing.Printing;
using static BaiTapLon.Ui;

namespace BaiTapLon;

// TV3: bán hàng, thanh toán, hóa đơn.
//  - Nhân viên: tạo đơn, chọn khách hàng, thêm/sửa số lượng, thanh toán hoặc lưu chờ thanh toán, in hóa đơn, xem hóa đơn CỦA MÌNH.
//  - Quản lý: xem toàn bộ hóa đơn + chi tiết, in, hủy hóa đơn (bắt buộc lý do). Không lập hóa đơn, không thu tiền.
public partial class Form1
{
    // =====================================================================
    //  BÁN HÀNG (chỉ Nhân viên)
    // =====================================================================
    private Control BuildSalesPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };
        var cart = new List<CartLine>();
        var customers = new List<Customer>();
        var available = new List<Product>();

        // --- Thẻ chọn khách hàng + sản phẩm ---
        var top = new Card { Dock = DockStyle.Top, Height = S(152) };
        var customerBox = new DropBox();
        var newCustomer = new RoundedButton
        {
            Text = "Khách mới",
            Icon = "plus",
            Fill = Theme.BlueSoft,
            Fore = Theme.Blue,
            Width = S(170),
            Height = S(44)
        };
        var productBox = new DropBox();
        var add = new RoundedButton { Text = "Thêm vào đơn", Icon = "plus", Width = S(170), Height = S(44) };
        top.Controls.Add(customerBox);
        top.Controls.Add(newCustomer);
        top.Controls.Add(productBox);
        top.Controls.Add(add);
        top.Resize += (_, _) =>
        {
            int h = S(44);
            int right = top.Width - S(24);
            newCustomer.SetBounds(right - newCustomer.Width, S(20), newCustomer.Width, h);
            customerBox.SetBounds(S(24), S(20), Math.Max(S(200), newCustomer.Left - S(12) - S(24)), h);
            add.SetBounds(right - add.Width, S(80), add.Width, h);
            productBox.SetBounds(S(24), S(80), Math.Max(S(200), add.Left - S(12) - S(24)), h);
        };
        var gap = new Panel { Dock = DockStyle.Top, Height = S(16), BackColor = Theme.PageBg };

        // --- Thẻ đơn hàng ---
        var card = new Card { Dock = DockStyle.Fill };
        var table = new SalesCartTable { Dock = DockStyle.Fill };
        var bar = new Panel { Dock = DockStyle.Bottom, Height = S(84), BackColor = Color.White };
        var total = new TitleLabel { BackColor = Color.White };
        var pay = new RoundedButton { Text = "Thanh toán", Fill = Theme.Green, Width = S(190), Height = S(46) };
        var hold = new RoundedButton
        {
            Text = "Lưu chờ thanh toán",
            Fill = Color.White,
            Fore = Color.FromArgb(51, 65, 85),
            Outline = Theme.Line,
            Width = S(210),
            Height = S(46)
        };
        bar.Controls.Add(total);
        bar.Controls.Add(hold);
        bar.Controls.Add(pay);
        bar.Resize += (_, _) =>
        {
            int y = (bar.Height - pay.Height) / 2;
            pay.SetBounds(bar.Width - S(24) - pay.Width, y, pay.Width, pay.Height);
            hold.SetBounds(pay.Left - S(12) - hold.Width, y, hold.Width, hold.Height);
            total.SetBounds(S(24), 0, Math.Max(S(100), hold.Left - S(40)), bar.Height);
        };

        card.Controls.Add(table);
        card.Controls.Add(bar);

        // --- Nạp danh sách khách hàng / sản phẩm ---
        void RefreshCustomers(string? selectCode = null)
        {
            customers = Store.Customers.Where(c => c.Active).OrderBy(c => c.Name).ToList();
            customerBox.SetItems(new[] { "Khách lẻ (không lưu thông tin)" }
                .Concat(customers.Select(c => $"{c.Code} - {c.Name} - {c.Phone}")));
            int idx = selectCode == null ? -1 : customers.FindIndex(c => c.Code == selectCode);
            customerBox.SelectedIndex = idx + 1;      // 0 = khách lẻ
        }

        void RefreshProducts()
        {
            available = Store.Products.Where(p => !p.Discontinued && p.Stock > 0).OrderBy(p => p.Name).ToList();
            productBox.SetItems(available.Select(p => $"{p.Code} - {p.Name} ({FormatMoney(p.Price)}) • còn {p.Stock}"));
        }

        void Redraw()
        {
            table.SetRows(cart.Select(l => new[]
            {
                l.Product.Code, l.Product.Name, FormatMoney(l.Product.Price), l.Qty.ToString(),
                FormatMoney(l.Product.Price * l.Qty), "Thao tác"
            }).ToList());
            decimal sum = cart.Sum(l => l.Product.Price * l.Qty);
            total.SetParts(
                ("Tổng thanh toán:", Theme.Muted, Theme.BodyBold),
                ("   " + FormatMoney(sum), Theme.Blue, Theme.Heading));
        }

        RefreshCustomers();
        RefreshProducts();
        Redraw();

        newCustomer.Click += (_, _) =>
        {
            var c = ShowCustomerDialog(null);
            if (c != null) RefreshCustomers(c.Code);
        };

        add.Click += (_, _) =>
        {
            if (productBox.SelectedIndex < 0) return;
            var product = available[productBox.SelectedIndex];
            int count = 1;
            var line = cart.FirstOrDefault(l => l.Product.Code == product.Code);
            int newQty = (line?.Qty ?? 0) + count;
            if (newQty > product.Stock)
            {
                MessageBox.Show($"Số lượng vượt quá tồn kho (còn {product.Stock}).", "Không đủ hàng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (line == null) cart.Add(new CartLine(product, count));
            else line.Qty = newQty;
            Redraw();
        };

        void ChangeQty(string code, int delta)
        {
            var line = cart.FirstOrDefault(l => l.Product.Code == code);
            if (line == null) return;
            int n = line.Qty + delta;
            if (n <= 0) cart.Remove(line);
            else if (n > line.Product.Stock)
            {
                MessageBox.Show($"Số lượng vượt quá tồn kho (còn {line.Product.Stock}).", "Không đủ hàng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            else line.Qty = n;
            Redraw();
        }
        table.IncreaseClicked += code => ChangeQty(code, +1);

        table.DecreaseClicked += code => ChangeQty(code, -1);

        table.RemoveClicked += code =>
        {
            cart.RemoveAll(l => l.Product.Code == code);
            Redraw();
        };

        // --- Lập hóa đơn ---
        void Checkout(bool paid)
        {
            if (cart.Count == 0)
            {
                MessageBox.Show("Vui lòng thêm ít nhất một sản phẩm.", "Chưa có sản phẩm", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string method = "";
            decimal sum = cart.Sum(l => l.Product.Price * l.Qty);
            if (paid && !ShowPaymentDialog(sum, out method)) return;

            Customer? customer = customerBox.SelectedIndex > 0 ? customers[customerBox.SelectedIndex - 1] : null;
            string? error = Store.CreateInvoice(Me, customer,
                cart.Select(l => (l.Product, l.Qty)).ToList(), paid, method, out var invoice);
            if (error != null || invoice == null)
            {
                MessageBox.Show(error ?? "Không thể lập hóa đơn.", "Không thể lập hóa đơn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RefreshProducts();     // tồn kho có thể đã thay đổi
                return;
            }

            cart.Clear();
            RefreshProducts();
            RefreshCustomers();
            Redraw();


            if (paid)
            {
                if (MessageBox.Show($"Thanh toán thành công hóa đơn {invoice.Code}.\nTổng tiền: {FormatMoney(invoice.Total)}\n\nBạn có muốn in hóa đơn không?",
                        "Hoàn tất", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    PrintInvoice(invoice);
            }
            else
            {
                MessageBox.Show($"Đã lưu hóa đơn {invoice.Code} ở trạng thái chờ thanh toán.\nHàng đã được giữ lại trong kho.\nBạn có thể thu tiền sau trong mục \"Hóa đơn của tôi\".",
                    "Đã lưu hóa đơn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        pay.Click += (_, _) => Checkout(true);
        hold.Click += (_, _) => Checkout(false);

        page.Controls.Add(card);
        page.Controls.Add(gap);
        page.Controls.Add(top);
        return page;
    }
    /// <summary>Hộp thoại thanh toán: chọn hình thức, nhập tiền khách đưa (tiền mặt) và xem tiền thừa.</summary>
    private bool ShowPaymentDialog(decimal total, out string method)
    {
        string chosen = "Tiền mặt";
        var d = new DialogBuilder("Thanh toán", "Thanh toán đơn hàng");
        d.AddInput("Tổng thanh toán", FormatMoney(total), readOnly: true);
        var methodBox = d.AddDrop("Hình thức thanh toán", new[] { "Tiền mặt", "Chuyển khoản", "Quẹt thẻ" });
        var received = d.AddInput("Tiền khách đưa (chỉ với tiền mặt)", "", "Để trống nếu khách đưa đúng số tiền");
        var change = d.AddInput("Tiền thừa trả khách", "0 đ", readOnly: true);

        decimal Received()
        {
            string digits = new string(received.Value.Where(char.IsDigit).ToArray());
            return digits.Length == 0 ? total : (decimal.TryParse(digits, out var v) ? v : 0m);
        }

        void UpdateChange()
        {
            bool cash = methodBox.SelectedText == "Tiền mặt";
            received.ReadOnly = !cash;
            change.Value = cash ? FormatMoney(Math.Max(0m, Received() - total)) : "0 đ";
        }
        received.InputChanged += (_, _) => UpdateChange();
        methodBox.SelectedIndexChanged += (_, _) => UpdateChange();

        bool ok = d.Show(this, "Xác nhận thanh toán", () =>
        {
            chosen = methodBox.SelectedText;
            if (chosen == "Tiền mặt" && Received() < total)
                return "Tiền khách đưa chưa đủ để thanh toán đơn hàng.";
            return null;
        }, Theme.Green);

        method = chosen;
        return ok;
    }

    // =====================================================================
    //  HÓA ĐƠN
    // =====================================================================
    private Control BuildInvoicesPage()
    {
        var statuses = new[] { "Tất cả trạng thái", Invoice.Paid, Invoice.Pending, Invoice.Cancelled };

        var columns = new List<ColumnDef>
        {
            new("Mã hóa đơn", 1.1f),
            new("Khách hàng", 1.8f, ColKind.Link),
            new("Ngày lập", 1.5f)
        };
        if (IsManager) columns.Add(new ColumnDef("Nhân viên lập", 1.5f));
        columns.Add(new ColumnDef("Tổng tiền", 1.3f));
        columns.Add(new ColumnDef("Trạng thái", 1.6f, ColKind.Status));
        columns.Add(new ColumnDef("Thao tác", 1.2f, ColKind.Action));

        var page = new DataListPage(
            IsManager ? "Tìm kiếm theo mã hóa đơn, khách hàng hoặc nhân viên..." : "Tìm kiếm theo mã hóa đơn hoặc tên khách hàng...",
            "hóa đơn",
            new[] { statuses },
            columns.ToArray());

        // Nút "Thao tác" mở menu khi còn việc để làm; nếu chỉ xem được thì hiện thẳng "Chi tiết"
        static string ActionLabel(Invoice i) =>
            (IsManager ? i.Status != Invoice.Cancelled : i.Status == Invoice.Pending) ? "Thao tác" : "Chi tiết";

        page.Query = (term, f) =>
        {
            string st = f.Count > 0 ? f[0] : "";
            return ScopeInvoices()
                .Where(i =>
                    (term.Length == 0 ||
                     i.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     i.Customer.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     (IsManager && i.StaffName.Contains(term, StringComparison.OrdinalIgnoreCase))) &&
                    (IsAll(st) || i.Status == st))
                .OrderByDescending(i => i.Date)
                .Select(i =>
                {
                    var cells = new List<string> { i.Code, i.Customer, FormatDate(i.Date) };
                    if (IsManager) cells.Add(i.StaffName);
                    cells.Add(FormatMoney(i.Total));
                    cells.Add(i.Status);
                    cells.Add(ActionLabel(i));
                    return cells.ToArray();
                })
                .ToList();
        };

        page.RowAction += row =>
        {
            var inv = Store.Invoices.FirstOrDefault(i => i.Code == row[0]);
            if (inv == null) return;

            var items = new List<(string Text, Action Do)> { ("Xem chi tiết", () => ShowInvoiceDetail(inv)) };
            if (!IsManager && inv.Status == Invoice.Pending && inv.StaffUser == Me.UserName)
                items.Add(("Thanh toán", () => PayPendingInvoice(inv, () => page.Reload(false))));
            if (IsManager && inv.Status != Invoice.Cancelled)
                items.Add(("Hủy hóa đơn", () => ShowCancelInvoiceDialog(inv, () => page.Reload(false))));

            if (items.Count == 1) items[0].Do();
            else ShowRowMenu(items.ToArray());
        };

        page.Reload();
        return page;
    }

    private void PayPendingInvoice(Invoice inv, Action onDone)
    {
        if (!ShowPaymentDialog(inv.Total, out string method)) return;
        string? error = Store.PayInvoice(inv, Me, method);
        if (error != null)
        {
            MessageBox.Show(error, "Không thể thanh toán", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        onDone();
        if (MessageBox.Show($"Đã thanh toán hóa đơn {inv.Code}.\n\nBạn có muốn in hóa đơn không?", "Hoàn tất",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            PrintInvoice(inv);
    }

    /// <summary>Hủy hóa đơn: lưu lý do, người hủy, thời điểm; hàng được hoàn lại kho đúng một lần.</summary>
    private void ShowCancelInvoiceDialog(Invoice inv, Action onDone)
    {
        var d = new DialogBuilder("Hủy hóa đơn", "Hủy hóa đơn " + inv.Code);
        d.AddInput("Hóa đơn", $"{inv.Code} - {inv.Customer} - {FormatMoney(inv.Total)}", readOnly: true);
        var reason = d.AddInput("Lý do hủy (bắt buộc)", "", "VD: Khách đổi ý, nhập sai sản phẩm...");
        d.AddNote("Hàng trong hóa đơn sẽ được hoàn lại kho. Hệ thống ghi nhận người hủy và thời điểm hủy.");

        if (d.Show(this, "Xác nhận hủy hóa đơn", () => Store.CancelInvoice(inv, Me, reason.Value), Theme.Red))
            onDone();
    }

    private void ShowInvoiceDetail(Invoice inv)
    {
        int w = S(640);
        using var dlg = new Form
        {
            Text = "Chi tiết hóa đơn " + inv.Code,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            BackColor = Color.White,
            Font = Theme.Body
        };
        dlg.Controls.Add(new Label
        {
            Text = "Hóa đơn " + inv.Code,
            Font = Theme.Heading,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(S(28), S(22))
        });

        var info = new List<string>
        {
            $"Khách hàng: {inv.Customer}",
            $"Ngày lập: {FormatDate(inv.Date)}",
            $"Nhân viên lập: {inv.StaffName}",
            $"Trạng thái: {inv.Status}"
        };
        if (inv.PayMethod.Length > 0) info.Add($"Hình thức thanh toán: {inv.PayMethod}");
        if (inv.Status == Invoice.Cancelled)
        {
            info.Add($"Lý do hủy: {inv.CancelReason}");
            info.Add($"Người hủy: {inv.CancelledBy} lúc {FormatDate(inv.CancelledAt ?? inv.Date)}");
        }

        int y = S(64);
        var infoLabel = new Label
        {
            Text = string.Join("\n", info),
            Font = Theme.Body,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Location = new Point(S(28), y),
            Size = new Size(w - S(56), info.Count * S(24))
        };
        dlg.Controls.Add(infoLabel);
        y += infoLabel.Height + S(12);

        var table = new ModernTable { HeaderHeight = S(40), RowHeight = S(44), SidePadding = S(8) };
        table.SetColumns(
            new ColumnDef("Sản phẩm", 2.6f),
            new ColumnDef("SL", 0.6f),
            new ColumnDef("Đơn giá", 1.3f),
            new ColumnDef("Thành tiền", 1.4f));
        table.SetRows(inv.Lines
            .Select(l => new[] { $"{l.ProductCode} - {l.ProductName}", l.Qty.ToString(), FormatMoney(l.Price), FormatMoney(l.Amount) })
            .ToList());
        int visibleRows = Math.Min(Math.Max(inv.Lines.Count, 1), 5);
        table.SetBounds(S(28), y, w - S(56), table.HeaderHeight + visibleRows * table.RowHeight);
        dlg.Controls.Add(table);
        y += table.Height + S(12);

        dlg.Controls.Add(new Label
        {
            Text = "Tổng cộng: " + FormatMoney(inv.Total),
            Font = Theme.Heading,
            ForeColor = Theme.Blue,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(S(28), y),
            Size = new Size(w - S(56), S(36))
        });
        y += S(48);

        var print = new RoundedButton { Text = "In hóa đơn", Fill = Theme.BlueSoft, Fore = Theme.Blue };
        print.SetBounds(S(28), y, S(180), S(46));
        print.Click += (_, _) => PrintInvoice(inv);
        var close = new RoundedButton
        {
            Text = "Đóng",
            Fill = Color.White,
            Fore = Color.FromArgb(51, 65, 85),
            Outline = Theme.Line
        };
        close.SetBounds(w - S(28) - S(140), y, S(140), S(46));
        close.Click += (_, _) => dlg.Close();
        dlg.Controls.Add(print);
        dlg.Controls.Add(close);
        dlg.CancelButton = close;

        dlg.ClientSize = new Size(w, y + S(46) + S(28));
        dlg.ShowDialog(this);
    }

    /// <summary>Xem trước và in hóa đơn (chọn "Microsoft Print to PDF" nếu muốn lưu ra file PDF).</summary>
    private void PrintInvoice(Invoice inv)
    {
        try
        {
            using var doc = new PrintDocument { DocumentName = "Hóa đơn " + inv.Code };
            doc.PrintPage += (_, e) =>
            {
                var g = e.Graphics;
                if (g == null) return;

                using var title = new Font("Segoe UI", 16F, FontStyle.Bold);
                using var bold = new Font("Segoe UI", 10F, FontStyle.Bold);
                using var body = new Font("Segoe UI", 10F);
                using var rightAlign = new StringFormat { Alignment = StringAlignment.Far };
                float left = e.MarginBounds.Left;
                float width = e.MarginBounds.Width;
                float y = e.MarginBounds.Top;

                void Row(string l, string r, Font f)
                {
                    g.DrawString(l, f, Brushes.Black, left, y);
                    if (r.Length > 0)
                        g.DrawString(r, f, Brushes.Black, new RectangleF(left, y, width, f.GetHeight(g) + 4), rightAlign);
                    y += f.GetHeight(g) + 6;
                }

                void Rule()
                {
                    g.DrawLine(Pens.Gray, left, y, left + width, y);
                    y += 8;
                }

                Row("QUẢN LÝ BÁN HÀNG", "", title);
                Row("HÓA ĐƠN BÁN HÀNG" + (inv.Status == Invoice.Cancelled ? " (ĐÃ HỦY)" : ""), "", bold);
                Rule();
                Row("Mã hóa đơn: " + inv.Code, "", body);
                Row("Ngày lập: " + FormatDate(inv.Date), "", body);
                Row("Khách hàng: " + inv.Customer, "", body);
                Row("Nhân viên: " + inv.StaffName, "", body);
                Row("Trạng thái: " + inv.Status + (inv.PayMethod.Length > 0 ? " (" + inv.PayMethod + ")" : ""), "", body);
                Rule();
                Row("Sản phẩm", "Thành tiền", bold);
                foreach (var l in inv.Lines)
                {
                    Row($"{l.ProductName}", FormatMoney(l.Amount), body);
                    Row($"    {l.Qty} x {FormatMoney(l.Price)}", "", body);
                }
                Rule();
                Row("TỔNG CỘNG", FormatMoney(inv.Total), bold);
                y += 12;
                Row("Cảm ơn quý khách!", "", body);
                e.HasMorePages = false;
            };

            using var preview = new PrintPreviewDialog { Document = doc, Width = S(900), Height = S(720) };
            preview.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Không thể in hóa đơn: " + ex.Message, "Lỗi in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
