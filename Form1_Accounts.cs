using static BaiTapLon.Ui;

namespace BaiTapLon;

// TV1: hồ sơ người dùng, đổi mật khẩu, quản lý nhân viên (chỉ Quản lý), quản lý khách hàng.
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
        d.AddNote("Thông tin do Quản lý cập nhật. Cần chỉnh sửa vui lòng liên hệ Quản lý.");
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
            if (!PasswordHasher.Verify(current.Value, Me.PasswordHash)) return "Mật khẩu hiện tại không đúng.";
            string? err = ValidateNewPassword(next.Value, again.Value);
            if (err != null) return err;
            if (next.Value == current.Value) return "Mật khẩu mới phải khác mật khẩu hiện tại.";
            Me.PasswordHash = PasswordHasher.Hash(next.Value);
            return null;
        });
        if (saved)
            MessageBox.Show("Đã đổi mật khẩu thành công.", "Đổi mật khẩu", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // =====================================================================
    //  NHÂN VIÊN (chỉ Quản lý): tạo tài khoản, sửa thông tin, đặt lại mật khẩu, khóa/mở khóa.
    //  Hệ thống không có đăng ký tự do - mọi tài khoản đều do Quản lý cấp ở đây.
    // =====================================================================
    private Control BuildStaffPage()
    {
        var roles = new[] { "Tất cả vai trò", Roles.Manager, Roles.Staff };
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
            ShowRowMenu(
                ("Sửa thông tin", () => ShowStaffDialog(acc, () => page.Reload(false))),
                ("Đặt lại mật khẩu", () => ShowResetPasswordDialog(acc)),
                (acc.Active ? "Khóa tài khoản" : "Mở khóa tài khoản", () => ToggleLock(acc, () => page.Reload(false))));
        };

        page.Reload();
        return page;
    }

    private void ToggleLock(Account acc, Action onDone)
    {
        if (ReferenceEquals(acc, Me))
        {
            MessageBox.Show("Bạn không thể tự khóa tài khoản của chính mình.", "Không thể thực hiện",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (acc.Active &&
            MessageBox.Show($"Khóa tài khoản của {acc.FullName}?\nNgười này sẽ không thể đăng nhập cho đến khi được mở khóa.",
                "Khóa tài khoản", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        acc.Active = !acc.Active;
        onDone();
    }

    private void ShowStaffDialog(Account? existing, Action onSaved)
    {
        var d = new DialogBuilder(existing == null ? "Thêm nhân viên" : "Sửa thông tin nhân viên",
            existing == null ? "Tạo tài khoản nhân viên" : "Cập nhật thông tin nhân viên", 520);

        InputBox name, phone, address;
        DropBox role;
        InputBox? user = null, pass = null, pass2 = null;
        var roleItems = new[] { Roles.Staff, Roles.Manager };

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

                Store.Accounts.Add(new Account
                {
                    Code = Store.NextAccountCode(),
                    FullName = n,
                    Phone = ph,
                    Address = address.Value.Trim(),
                    UserName = u,
                    PasswordHash = PasswordHasher.Hash(pass.Value),
                    Role = role.SelectedText,
                    Active = true
                });
            }
            else
            {
                if (ReferenceEquals(existing, Me) && role.SelectedText != existing.Role)
                    return "Bạn không thể tự đổi vai trò của chính mình.";
                existing.FullName = n;
                existing.Phone = ph;
                existing.Address = address.Value.Trim();
                existing.Role = role.SelectedText;
            }
            return null;
        });

        if (!saved) return;
        if (existing != null && ReferenceEquals(existing, Me))
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
            acc.PasswordHash = PasswordHasher.Hash(next.Value);
            return null;
        });
        if (saved)
            MessageBox.Show($"Đã đặt lại mật khẩu cho {acc.FullName}.", "Đặt lại mật khẩu",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // =====================================================================
    //  KHÁCH HÀNG (cả hai vai trò)
    //  Quản lý: xem, thêm, sửa, ngừng hoạt động, xóa có điều kiện.
    //  Nhân viên: xem, tìm kiếm, thêm, sửa (không được xóa).
    // =====================================================================
    private Control BuildCustomersPage()
    {
        var statuses = new[] { "Tất cả trạng thái", "Hoạt động", "Ngừng hoạt động" };

        var page = new DataListPage(
            "Tìm kiếm theo tên, số điện thoại hoặc mã khách hàng...",
            "khách hàng",
            new[] { statuses },
            new[]
            {
                new ColumnDef("Mã khách hàng", 1.1f),
                new ColumnDef("Họ tên", 1.8f, ColKind.Link),
                new ColumnDef("Số điện thoại", 1.3f),
                new ColumnDef("Email", 2.0f),
                new ColumnDef("Trạng thái", 1.5f, ColKind.Status),
                new ColumnDef("Thao tác", 1.2f, ColKind.Action)
            },
            "Thêm khách hàng");

        page.Query = (term, f) =>
        {
            string st = f.Count > 0 ? f[0] : "";
            return Store.Customers
                .Where(c =>
                    (term.Length == 0 ||
                     c.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     c.Phone.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     c.Email.Contains(term, StringComparison.OrdinalIgnoreCase)) &&
                    (IsAll(st) || c.StatusText == st))
                .Select(c => new[] { c.Code, c.Name, c.Phone, c.Email, c.StatusText, IsManager ? "Thao tác" : "Sửa" })
                .ToList();
        };

        page.ExtraButton!.Click += (_, _) =>
        {
            if (ShowCustomerDialog(null) != null) page.Reload();
        };
        page.RowAction += row =>
        {
            var c = Store.Customers.FirstOrDefault(x => x.Code == row[0]);
            if (c == null) return;

            void Edit()
            {
                if (ShowCustomerDialog(c) != null) page.Reload(false);
            }

            if (!IsManager)
            {
                Edit();
                return;
            }
            ShowRowMenu(
                ("Xem / sửa thông tin", () => Edit()),
                (c.Active ? "Ngừng hoạt động" : "Kích hoạt lại", () =>
                {
                    c.Active = !c.Active;
                    page.Reload(false);
                }),
                ("Xóa khách hàng", () => DeleteCustomer(c, () => page.Reload(false))));
        };

        page.Reload();
        return page;
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
                c.Active = false;
                onDone();
            }
            return;
        }

        if (MessageBox.Show($"Xóa khách hàng {c.Name}?", "Xác nhận xóa",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Store.Customers.Remove(c);
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

            var cus = existing ?? new Customer { Code = Store.NextCustomerCode() };
            cus.Name = n;
            cus.Phone = ph;
            cus.Email = em;
            cus.Address = address.Value.Trim();
            if (existing == null) Store.Customers.Add(cus);
            result = cus;
            return null;
        });
        return result;
    }
}
