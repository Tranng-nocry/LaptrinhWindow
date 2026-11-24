using static BaiTapLon.Ui;

namespace BaiTapLon;

// TV1: hồ sơ người dùng, đổi mật khẩu, quản lý nhân viên (chỉ Admin), quản lý khách hàng.
public partial class Form1
{
    // =====================================================================
    //  HỒ SƠ + ĐỔI MẬT KHẨU (mở từ menu avatar góc phải trên)
    // =====================================================================
    private void ShowProfileDialog()
    {
        var a = Me;
        var d = new DialogBuilder("Thông tin cá nhân", "Thông tin cá nhân", 520);
        d.AddInput("Mã nhân viên", a.Code, readOnly: true, half: true);
        d.AddInput("Vai trò", a.Role, readOnly: true, half: true);
        d.AddInput("Họ và tên", a.FullName, readOnly: true);
        d.AddInput("Số điện thoại", a.Phone, readOnly: true, half: true);
        d.AddInput("Tên đăng nhập", a.UserName, readOnly: true, half: true);
        d.AddInput("Địa chỉ", a.Address, readOnly: true);
        d.AddNote("Thông tin do Admin cập nhật. Cần chỉnh sửa vui lòng liên hệ Admin.");
        d.Show(this, "Đóng", () => null);
    }

    private static string? ValidateNewPassword(string password, string again)
    {
        if (password.Length < 6) return "Mật khẩu mới phải có ít nhất 6 ký tự.";
        if (password != again) return "Hai lần nhập mật khẩu mới không khớp.";
        return null;
    }

    private void ShowChangePasswordDialog()
    {
        var d = new DialogBuilder("Đổi mật khẩu", "Đổi mật khẩu");
        var current = d.AddInput("Mật khẩu hiện tại", "", "Nhập mật khẩu hiện tại", password: true);
        var next = d.AddInput("Mật khẩu mới", "", "Tối thiểu 6 ký tự", password: true);
        var again = d.AddInput("Nhập lại mật khẩu mới", "", "Nhập lại mật khẩu mới", password: true);

        bool saved = d.Show(this, "Đổi mật khẩu", () =>
        {
            if (current.Value.Length == 0) return "Vui lòng nhập mật khẩu hiện tại.";
            string? err = ValidateNewPassword(next.Value, again.Value);
            if (err != null) return err;
            if (next.Value == current.Value) return "Mật khẩu mới phải khác mật khẩu hiện tại.";
            return Store.ChangePassword(Me, current.Value, next.Value);   // kiểm tra mật khẩu cũ và ghi vào SQL Server
        });
        if (saved)
            MessageBox.Show("Đã đổi mật khẩu thành công.", "Đổi mật khẩu", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // =====================================================================
    //  NHÂN VIÊN (chỉ Admin): tạo tài khoản, sửa thông tin, đổi vai trò, đặt lại mật khẩu, khóa/mở khóa.
    //  Hệ thống không có đăng ký tự do - mọi tài khoản đều do Admin cấp ở đây.
    // =====================================================================
    private Control BuildStaffPage()
    {
        var roles = new[] { "Tất cả vai trò" }.Concat(Roles.All).ToArray();
        var statuses = new[] { "Tất cả trạng thái", "Đang hoạt động", "Đã khóa" };

        var page = new DataListPage(
            "Tìm kiếm theo tên, mã nhân viên, số điện thoại hoặc tên đăng nhập...",
            "nhân viên",
            new[] { roles, statuses },
            new[]
            {
                new ColumnDef("Mã nhân viên", 1.1f),
                new ColumnDef("Họ tên", 1.9f, ColKind.Link),
                new ColumnDef("Vai trò", 1.2f, ColKind.Tag),
                new ColumnDef("Số điện thoại", 1.4f),
                new ColumnDef("Tên đăng nhập", 1.4f),
                new ColumnDef("Trạng thái", 1.5f, ColKind.Status),
                new ColumnDef("Thao tác", 1.2f, ColKind.Action)
            },
            "Thêm nhân viên");

        page.Query = (term, f) =>
        {
            string role = f.Count > 0 ? f[0] : "";
            string st = f.Count > 1 ? f[1] : "";
            return Store.Accounts
                .Where(a =>
                    (term.Length == 0 ||
                     a.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     a.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     a.Phone.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     a.UserName.Contains(term, StringComparison.OrdinalIgnoreCase)) &&
                    (IsAll(role) || a.Role == role) &&
                    (IsAll(st) || a.StatusText == st))
                .Select(a => new[] { a.Code, a.FullName, a.Role, a.Phone, a.UserName, a.StatusText, "Thao tác" })
                .ToList();
        };

        page.ExtraButton!.Click += (_, _) => ShowStaffDialog(null, () => page.Reload());
        page.RowAction += row =>
        {
            var acc = Store.Accounts.FirstOrDefault(a => a.Code == row[0]);
            if (acc == null) return;
            ShowRowMenu("Quản lý nhân viên", $"{acc.Code} · {acc.FullName}",
                ("Sửa thông tin", () => ShowStaffDialog(acc, () => page.Reload(false))),
                ("Đặt lại mật khẩu", () => ShowResetPasswordDialog(acc)),
                (acc.Active ? "Khóa tài khoản" : "Mở khóa tài khoản", () => ToggleLock(acc, () => page.Reload(false))));
        };

        page.Reload();
        return page;
    }

    private void ToggleLock(Account acc, Action onDone)
    {
        if (acc.Code == Me.Code)
        {
            MessageBox.Show("Bạn không thể tự khóa tài khoản của chính mình.", "Không thể thực hiện",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (acc.Active &&
            MessageBox.Show($"Khóa tài khoản của {acc.FullName}?\nNgười này sẽ không thể đăng nhập cho đến khi được mở khóa.",
                "Khóa tài khoản", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        string? err = Store.SetAccountActive(Me, acc, !acc.Active);
        if (err != null)
        {
            MessageBox.Show(err, "Không thể thực hiện", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        onDone();
    }

    private void ShowStaffDialog(Account? existing, Action onSaved)
    {
        var d = new DialogBuilder(existing == null ? "Thêm nhân viên" : "Sửa thông tin nhân viên",
            existing == null ? "Tạo tài khoản nhân viên" : "Cập nhật thông tin nhân viên", 520);

        InputBox name, phone, address;
        DropBox role;
        InputBox? user = null, pass = null, pass2 = null;
        var roleItems = Roles.All;

        if (existing == null)
        {
            name = d.AddInput("Họ và tên", "", "Nhập họ tên", half: true);
            phone = d.AddInput("Số điện thoại", "", "VD: 0901234567", half: true);
            address = d.AddInput("Địa chỉ", "", "Số nhà, đường, quận/huyện...");
            user = d.AddInput("Tên đăng nhập", "", "VD: nguyenvana", half: true);
            role = d.AddDrop("Vai trò", roleItems, Roles.Staff, half: true);
            pass = d.AddInput("Mật khẩu ban đầu", "", "Tối thiểu 6 ký tự", password: true, half: true);
            pass2 = d.AddInput("Nhập lại mật khẩu", "", "Nhập lại mật khẩu", password: true, half: true);
            d.AddNote("Nhân viên đăng nhập bằng tài khoản này và có thể tự đổi mật khẩu trong menu hồ sơ.");
        }
        else
        {
            d.AddInput("Mã nhân viên", existing.Code, readOnly: true, half: true);
            d.AddInput("Tên đăng nhập", existing.UserName, readOnly: true, half: true);
            name = d.AddInput("Họ và tên", existing.FullName);
            phone = d.AddInput("Số điện thoại", existing.Phone, half: true);
            role = d.AddDrop("Vai trò", roleItems, existing.Role, half: true);
            address = d.AddInput("Địa chỉ", existing.Address);
        }

        bool saved = d.Show(this, existing == null ? "Tạo tài khoản" : "Lưu thay đổi", () =>
        {
            string n = name.Value.Trim();
            string ph = phone.Value.Trim();
            if (n.Length == 0) return "Vui lòng nhập họ tên.";
            if (!Store.IsValidPhone(ph)) return "Số điện thoại không hợp lệ (gồm 9 đến 11 chữ số).";
            if (Store.Accounts.Any(a => !ReferenceEquals(a, existing) && a.Phone == ph))
                return "Số điện thoại này đã được dùng cho nhân viên khác.";

            if (existing == null)
            {
                string u = user!.Value.Trim();
                if (u.Length < 3 || !u.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '.'))
                    return "Tên đăng nhập gồm ít nhất 3 ký tự (chữ, số, dấu _ hoặc .), không có khoảng trắng.";
                if (Store.UserNameTaken(u)) return "Tên đăng nhập đã tồn tại.";
                string? err = ValidateNewPassword(pass!.Value, pass2!.Value);
                if (err != null) return err.Replace("Mật khẩu mới", "Mật khẩu");

                return Store.SaveAccount(Me, null, n, ph, address.Value.Trim(), u, pass.Value, role.SelectedText);
            }

            if (existing.Code == Me.Code && role.SelectedText != existing.Role)
                return "Bạn không thể tự đổi vai trò của chính mình.";
            return Store.SaveAccount(Me, existing, n, ph, address.Value.Trim(), existing.UserName, "", role.SelectedText);
        });

        if (!saved) return;
        if (existing != null && existing.Code == Me.Code)
        {
            userBadge.UserName = Me.FullName;
            userBadge.Invalidate();
        }
        onSaved();
    }

    private void ShowResetPasswordDialog(Account acc)
    {
        var d = new DialogBuilder("Đặt lại mật khẩu", "Đặt lại mật khẩu");
        d.AddInput("Tài khoản", $"{acc.FullName} ({acc.UserName})", readOnly: true);
        var next = d.AddInput("Mật khẩu mới", "", "Tối thiểu 6 ký tự", password: true);
        var again = d.AddInput("Nhập lại mật khẩu mới", "", "Nhập lại mật khẩu mới", password: true);

        bool saved = d.Show(this, "Đặt lại mật khẩu", () =>
        {
            string? err = ValidateNewPassword(next.Value, again.Value);
            if (err != null) return err;
            return Store.ResetPassword(Me, acc, next.Value);
        });
        if (saved)
            MessageBox.Show($"Đã đặt lại mật khẩu cho {acc.FullName}.", "Đặt lại mật khẩu",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // =====================================================================
    //  KHÁCH HÀNG (Admin và Nhân viên bán hàng; Thủ kho không có trang này)
    //  Admin: xem, thêm, sửa, ngừng hoạt động, xóa có điều kiện.
    //  Nhân viên bán hàng: xem, tìm kiếm, thêm, sửa (không được xóa).
    // =====================================================================
    private Control BuildCustomersPage()
    {
        var statusOptions = new[] { "Tất cả trạng thái", "Hoạt động", "Ngừng hoạt động" };
        var purchaseOptions = IsAdmin
            ? new[] { "Tất cả lượt mua", "Mua từ 2 lần", "Chi tiêu cao", "Lâu chưa quay lại" }
            : new[] { "Tất cả lượt mua", "Chưa từng mua", "Đã mua từ 2 lần trở lên" };

        var columns = IsAdmin
            ? new[]
            {
                new ColumnDef("Mã KH", 0.85f),
                new ColumnDef("Họ tên", 1.65f, ColKind.Link),
                new ColumnDef("SĐT", 1.2f),
                new ColumnDef("Số lần mua", 1.0f),
                new ColumnDef("Tổng chi tiêu", 1.4f),
                new ColumnDef("Trạng thái", 1.3f, ColKind.Status),
                new ColumnDef("Thao tác", 1.0f, ColKind.Action, Theme.Blue)
            }
            : new[]
            {
                new ColumnDef("Mã KH", 0.95f),
                new ColumnDef("Khách hàng", 1.8f, ColKind.Link),
                new ColumnDef("SĐT", 1.3f),
                new ColumnDef("Số lần mua", 1.1f),
                new ColumnDef("Lần mua cuối", 1.5f),
                new ColumnDef("Thao tác", 1.1f, ColKind.Action, Theme.Blue)
            };

        var page = new DataListPage(
            "Tìm theo tên, SĐT hoặc mã khách hàng...",
            "khách hàng",
            IsAdmin ? new[] { statusOptions, purchaseOptions } : new[] { purchaseOptions },
            columns,
            "Thêm khách hàng");

        // Chỉ tính hóa đơn đã thanh toán; không tính chờ thanh toán / đã hủy.
        List<Invoice> PaidInvoices(Customer c) =>
            Store.Invoices.Where(i =>
                i.CustomerCode == c.Code && i.Status == Invoice.Paid).ToList();

        page.Query = (term, filters) =>
        {
            string status = IsAdmin && filters.Count > 0 ? filters[0] : "";
            string purchase = filters.Count > (IsAdmin ? 1 : 0)
                ? filters[IsAdmin ? 1 : 0] : "";

            var customers = Store.Customers.Where(c =>
                (term.Length == 0 ||
                 c.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 c.Phone.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 c.Email.Contains(term, StringComparison.OrdinalIgnoreCase)) &&
                (!IsAdmin || IsAll(status) || c.StatusText == status));

            // "Chi tiêu cao" là nhóm khách chi từ 1 triệu đồng trở lên.
            // "Lâu chưa quay lại" là khách đã mua nhưng không mua trong 90 ngày.
            customers = customers.Where(c =>
            {
                var paid = PaidInvoices(c);
                decimal spent = paid.Sum(i => i.Total);
                DateTime? last = paid.Count == 0 ? null : paid.Max(i => i.Date);
                return purchase switch
                {
                    "Mua từ 2 lần" or "Đã mua từ 2 lần trở lên" => paid.Count >= 2,
                    "Chưa từng mua" => paid.Count == 0,
                    "Chi tiêu cao" => spent >= 1000000m,
                    "Lâu chưa quay lại" => last.HasValue &&
                        last.Value < DateTime.Now.AddDays(-90),
                    _ => true
                };
            });

            return customers.Select(c =>
            {
                var paid = PaidInvoices(c);
                if (IsAdmin)
                    return new[]
                    {
                        c.Code, c.Name, c.Phone, paid.Count.ToString(),
                        paid.Sum(i => i.Total).ToString("N0") + " đ",
                        c.StatusText, "Chi tiết"
                    };

                return new[]
                {
                    c.Code, c.Name, c.Phone, paid.Count.ToString(),
                    paid.Count == 0 ? "Chưa mua" :
                        paid.Max(i => i.Date).ToString("dd/MM/yyyy"),
                    "Chi tiết"
                };
            }).ToList();
        };

        Label? totalValue = null;
        Label? repeatValue = null;
        Label? topValue = null;
        Label? topCode = null;

        void RefreshCustomers(bool resetPage = false)
        {
            page.Reload(resetPage);
            if (!IsAdmin) return;

            if (totalValue != null)
                totalValue.Text = Store.Customers.Count.ToString("N0");

            var purchased = Store.Customers
                .Select(c => new { Customer = c, Count = PaidInvoices(c).Count })
                .Where(x => x.Count > 0).ToList();
            if (repeatValue != null)
                repeatValue.Text = purchased.Count == 0 ? "0%" :
                    (100m * purchased.Count(x => x.Count >= 2) / purchased.Count)
                        .ToString("N1") + "%";

            var top = purchased.OrderByDescending(x => x.Count)
                .ThenBy(x => x.Customer.Name).FirstOrDefault();
            if (topValue != null)
                topValue.Text = top == null ? "Chưa có" :
                    top.Customer.Name;
            if (topCode != null)
                topCode.Text = top == null ? "" : "Mã KH: " + top.Customer.Code;
        }

        page.ExtraButton!.Click += (_, _) =>
        {
            if (ShowCustomerDialog(null) != null) RefreshCustomers(true);
        };

        page.RowAction += row =>
        {
            var customer = Store.Customers.FirstOrDefault(c => c.Code == row[0]);
            if (customer == null) return;

            void Edit()
            {
                if (ShowCustomerDialog(customer) != null) RefreshCustomers();
            }

            ShowCustomerPurchaseDetails(customer, Edit,
                () =>
                {
                    string? err = Store.SetCustomerActive(customer, !customer.Active);
                    if (err != null)
                        MessageBox.Show(err, "Không thể thực hiện", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RefreshCustomers();
                },
                () => DeleteCustomer(customer, () => RefreshCustomers()));
        };

        if (!IsAdmin)
        {
            page.Reload();
            return page;
        }

        // Chỉ trang Admin có ba thẻ thống kê; không sửa các UI dùng chung.
        var root = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 248, 252) };
        var stats = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = S(144),
            Padding = new Padding(0, 0, 0, S(12)),
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.FromArgb(245, 248, 252)
        };
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

        Panel MakeStat(string title, string icon, Color accent, out Label value, bool last = false)
        {
            var card = new CustomerStatCard(accent) { Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, last ? 0 : S(10), 0),
                Padding = new Padding(S(14), S(10), S(12), S(12)) };
            var badge = new Panel { Dock = DockStyle.Right, Width = S(48), BackColor = Color.White };
            badge.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var r = new Rectangle(S(3), S(4), S(42), S(42));
                using var brush = new SolidBrush(Color.FromArgb(28, accent));
                e.Graphics.FillEllipse(brush, r);
                Icons.Draw(e.Graphics, icon, new Rectangle(S(12), S(13), S(24), S(24)), accent);
            };
            var heading = new Label { Text = title, Dock = DockStyle.Top, Height = S(25),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Theme.Text, AutoEllipsis = true };
            value = new Label { Dock = DockStyle.Fill, AutoEllipsis = true,
                Font = new Font("Segoe UI", last ? 18F : 26F, FontStyle.Bold), ForeColor = accent,
                TextAlign = ContentAlignment.MiddleLeft };
            card.Controls.Add(value);
            if (last)
            {
                topCode = new Label { Dock = DockStyle.Bottom, Height = S(24),
                    Font = new Font("Segoe UI", 11F), ForeColor = Theme.Muted,
                    TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
                card.Controls.Add(topCode);
            }
            card.Controls.Add(heading);
            card.Controls.Add(badge);
            return card;
        }

        stats.Controls.Add(MakeStat("Tổng khách hàng", "team", Theme.Blue, out totalValue), 0, 0);
        stats.Controls.Add(MakeStat("Tỷ lệ khách quay lại", "chart", Theme.Green, out repeatValue), 1, 0);
        stats.Controls.Add(MakeStat("Khách mua nhiều nhất", "cart", Color.FromArgb(124, 58, 237), out topValue, true), 2, 0);
        root.Controls.Add(page);
        root.Controls.Add(stats);
        RefreshCustomers(true);
        return root;
    }

    private sealed class CustomerStatCard : Card
    {
        private readonly Color accent;
        public CustomerStatCard(Color accent) { this.accent = accent; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < S(8) || Height < S(10)) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Width - 3, Height - S(4) - 2);
            using var path = Theme.RoundRect(rect, Radius);
            using var pen = new Pen(Color.FromArgb(145, accent), S(2));
            e.Graphics.DrawPath(pen, path);
        }
    }

    /// <summary>Xem thông tin và lịch sử hóa đơn; nhân viên chỉ xem ba hóa đơn gần nhất.</summary>
    private void ShowCustomerPurchaseDetails(Customer customer, Action onEdit, Action onToggle, Action onDelete)
    {
        bool admin = IsAdmin;
        var paid = Store.Invoices.Where(i => i.CustomerCode == customer.Code && i.Status == Invoice.Paid)
            .OrderByDescending(i => i.Date).ToList();
        using var dialog = new Form
        {
            Text = "Chi tiết khách hàng", StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(S(780), S(570)), MinimumSize = new Size(S(650), S(500)),
            BackColor = Theme.PageBg, Font = Theme.Body, AutoScaleMode = AutoScaleMode.None,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
            Padding = new Padding(S(18))
        };
        Label Line(string text, Font font, Color color, int height) => new Label
        { Text = text, Font = font, ForeColor = color, Dock = DockStyle.Top,
          Height = S(height), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        var header = new Card { Dock = DockStyle.Top, Height = S(90), Padding = new Padding(S(16)) };
        var avatar = new Panel { Dock = DockStyle.Left, Width = S(66), BackColor = Color.White };
        avatar.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Theme.BlueSoft);
            var r = new Rectangle(0, S(2), S(52), S(52));
            e.Graphics.FillEllipse(brush, r);
            Icons.Draw(e.Graphics, "user", new Rectangle(S(13), S(15), S(26), S(26)), Theme.Blue);
        };
        var status = Theme.StatusStyle(customer.StatusText);
        header.Controls.Add(Line(customer.Code + "  •  " + customer.StatusText, Theme.Body, status.Fore, 25));
        header.Controls.Add(Line(customer.Name, Theme.Heading, Theme.Text, 30));
        header.Controls.Add(avatar);
        var info = new Card { Dock = DockStyle.Top, Height = S(156), Padding = new Padding(S(16), S(8), S(16), S(12)) };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Color.White };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        void Field(string title, string value, int col, int row)
        {
            var p = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, S(8), 0) };
            p.Controls.Add(Line(string.IsNullOrWhiteSpace(value) ? "Chưa có" : value, Theme.BodyBold, Theme.Text, 24));
            p.Controls.Add(Line(title, Theme.Small, Theme.Muted, 20));
            fields.Controls.Add(p, col, row);
        }
        Field("Số điện thoại", customer.Phone, 0, 0);
        Field("Email", customer.Email, 1, 0);
        Field("Địa chỉ", customer.Address, 0, 1);
        Field("Lần mua cuối", paid.Count == 0 ? "Chưa mua" : paid[0].Date.ToString("dd/MM/yyyy"), 1, 1);
        info.Controls.Add(fields);
        info.Controls.Add(Line("Thông tin khách hàng", Theme.Heading, Theme.Text, 30));
        var summary = new Card { Dock = DockStyle.Top, Height = S(56), Padding = new Padding(S(16), S(6), S(16), S(8)) };
        summary.Controls.Add(Line("Số lần mua: " + paid.Count + (admin ? "     |     Tổng chi tiêu: " + paid.Sum(i => i.Total).ToString("N0") + " đ" : ""), Theme.BodyBold, Theme.Blue, 36));
        var history = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(14), S(8), S(14), S(12)) };
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, AllowUserToResizeRows = false, MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, EnableHeadersVisualStyles = false,
            GridColor = Theme.Line, ColumnHeadersHeight = S(32), RowTemplate = { Height = S(32) }
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.BlueSoft;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
        grid.ColumnHeadersDefaultCellStyle.Font = Theme.BodyBold;
        grid.DefaultCellStyle.ForeColor = Theme.Text;
        grid.DefaultCellStyle.SelectionBackColor = Theme.BlueSoft;
        grid.DefaultCellStyle.SelectionForeColor = Theme.Text;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Theme.PageBg;
        grid.Columns.Add("InvoiceCode", "Mã hóa đơn");
        grid.Columns.Add("InvoiceDate", "Ngày mua");
        grid.Columns.Add("InvoiceItems", "Số sản phẩm");
        if (admin) grid.Columns.Add("InvoiceTotal", "Thành tiền");
        foreach (var inv in admin ? paid : paid.Take(3))
        {
            int quantity = inv.Lines.Sum(line => line.Qty);
            if (admin) grid.Rows.Add(inv.Code, inv.Date.ToString("dd/MM/yyyy HH:mm"), quantity, inv.Total.ToString("N0") + " đ");
            else grid.Rows.Add(inv.Code, inv.Date.ToString("dd/MM/yyyy HH:mm"), quantity);
        }
        history.Controls.Add(grid);
        if (paid.Count == 0) history.Controls.Add(Line("Chưa có hóa đơn đã thanh toán.", Theme.Small, Theme.Muted, 26));
        history.Controls.Add(Line(admin ? "Lịch sử hóa đơn đã thanh toán" : "3 hóa đơn gần nhất đã thanh toán", Theme.Heading, Theme.Text, 34));
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = S(58), FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, S(8), 0, 0) };
        void AddAction(string text, int width, Color fill, Color fore, Action action)
        {
            var btn = new RoundedButton { Text = text, Width = S(width), Height = S(38), Fill = fill, Fore = fore, Margin = new Padding(S(6), 0, 0, 0) };
            btn.Click += (_, _) => { dialog.Close(); action(); };
            footer.Controls.Add(btn);
        }
        AddAction("Sửa thông tin", 125, Theme.Blue, Color.White, onEdit);
        AddAction("Đóng", 75, Color.White, Theme.Text, () => { });
        if (admin)
        {
            AddAction(customer.Active ? "Ngừng hoạt động" : "Kích hoạt lại", 145, Theme.BlueSoft, Theme.Blue, onToggle);
            AddAction("Xóa khách hàng", 130, Color.FromArgb(254, 226, 226), Theme.Red, onDelete);
        }
        dialog.Controls.Add(history);
        dialog.Controls.Add(summary);
        dialog.Controls.Add(info);
        dialog.Controls.Add(header);
        dialog.Controls.Add(footer);
        dialog.Shown += (_, _) => grid.ClearSelection();
        dialog.ShowDialog(this);
    }

    /// <summary>Khách hàng đã có hóa đơn thì không xóa hẳn, chỉ chuyển sang ngừng hoạt động để giữ lịch sử giao dịch.</summary>
    private void DeleteCustomer(Customer c, Action onDone)
    {
        if (Store.CustomerHasInvoices(c))
        {
            if (MessageBox.Show(
                    $"Khách hàng {c.Name} đã có hóa đơn nên không thể xóa (để giữ lịch sử giao dịch).\n\nChuyển sang \"Ngừng hoạt động\"?",
                    "Không thể xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                string? err = Store.SetCustomerActive(c, false);
                if (err != null)
                    MessageBox.Show(err, "Không thể thực hiện", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                onDone();
            }
            return;
        }

        if (MessageBox.Show($"Xóa khách hàng {c.Name}?", "Xác nhận xóa",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            string? err = Store.DeleteCustomer(c);
            if (err != null)
            {
                MessageBox.Show(err, "Không thể xóa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            onDone();
        }
    }

    /// <summary>Thêm/sửa khách hàng. Trả về khách hàng đã lưu, hoặc null nếu người dùng hủy.</summary>
    private Customer? ShowCustomerDialog(Customer? existing)
    {
        var d = new DialogBuilder(existing == null ? "Thêm khách hàng" : "Sửa khách hàng",
            existing == null ? "Thêm khách hàng mới" : "Cập nhật khách hàng", 520);
        d.AddInput("Mã khách hàng", existing?.Code ?? Store.NextCustomerCode(), readOnly: true, half: true);
        var phone = d.AddInput("Số điện thoại", existing?.Phone ?? "", "VD: 0901234567", half: true);
        var name = d.AddInput("Họ và tên", existing?.Name ?? "", "Nhập họ tên khách hàng");
        var email = d.AddInput("Email (không bắt buộc)", existing?.Email ?? "", "VD: ten@email.com", half: true);
        var address = d.AddInput("Địa chỉ", existing?.Address ?? "", "Số nhà, đường, quận/huyện...", half: true);

        Customer? result = null;
        d.Show(this, "Lưu khách hàng", () =>
        {
            string n = name.Value.Trim();
            string ph = phone.Value.Trim();
            string em = email.Value.Trim();
            if (n.Length == 0) return "Vui lòng nhập họ tên khách hàng.";
            if (!Store.IsValidPhone(ph)) return "Số điện thoại không hợp lệ (gồm 9 đến 11 chữ số).";
            if (em.Length > 0 && (!em.Contains('@') || !em.Contains('.'))) return "Email không hợp lệ.";
            if (Store.Customers.Any(x => !ReferenceEquals(x, existing) && x.Phone == ph))
                return "Số điện thoại này đã thuộc về khách hàng khác.";

            string? err = Store.SaveCustomer(existing, n, ph, em, address.Value.Trim(), out var cus);   // ghi vào SQL Server
            if (err != null) return err;
            result = cus;
            return null;
        });
        return result;
    }
}
