using System.Security.Cryptography;

namespace BaiTapLon;

// =====================================================================
//  Models.cs - Mô hình dữ liệu + tầng nghiệp vụ dùng chung cho cả nhóm.
//  Hiện lưu trong bộ nhớ (dữ liệu mẫu). Khi chốt SQL Server chỉ cần thay phần bên trong
//  các hàm của lớp Store (các Form không phải sửa). Mỗi hàm nghiệp vụ ghi rõ chỗ cần SQL Transaction.
// =====================================================================

internal static class Roles
{
    public const string Manager = "Quản lý";
    public const string Staff = "Nhân viên";
}

/// <summary>Băm mật khẩu PBKDF2 + muối ngẫu nhiên (không lưu mật khẩu thô).</summary>
internal static class PasswordHasher
{
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 2) return false;
        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] expected = Convert.FromBase64String(parts[1]);
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

/// <summary>Bảng NhanVien + TaiKhoan gộp làm một cho gọn.</summary>
internal sealed class Account
{
    public string Code { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string UserName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Staff;
    public bool Active { get; set; } = true;

    public bool IsManager => Role == Roles.Manager;
    public string StatusText => Active ? "Đang hoạt động" : "Đã khóa";
}

internal sealed class Customer
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public bool Active { get; set; } = true;

    public string StatusText => Active ? "Hoạt động" : "Ngừng hoạt động";
}

internal sealed class Product
{
    public const int LowStockLimit = 5;

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool Discontinued { get; set; }

    public string Status => Discontinued ? "Ngừng kinh doanh"
        : Stock == 0 ? "Hết hàng"
        : Stock <= LowStockLimit ? "Sắp hết"
        : "Còn hàng";
}

internal sealed record InvoiceLine(string ProductCode, string ProductName, decimal Price, int Qty)
{
    public decimal Amount => Price * Qty;
}

internal sealed class Invoice
{
    public const string Paid = "Đã thanh toán";
    public const string Pending = "Chờ thanh toán";
    public const string Cancelled = "Đã hủy";

    public string Code { get; set; } = "";
    public string CustomerCode { get; set; } = "";          // rỗng = khách lẻ
    public string Customer { get; set; } = "Khách lẻ";
    public DateTime Date { get; set; }
    public string StaffUser { get; set; } = "";
    public string StaffName { get; set; } = "";
    public string Status { get; set; } = Pending;
    public string PayMethod { get; set; } = "";
    public List<InvoiceLine> Lines { get; } = new();
    public string CancelReason { get; set; } = "";
    public string CancelledBy { get; set; } = "";
    public DateTime? CancelledAt { get; set; }

    public decimal Total => Lines.Sum(l => l.Amount);
}

/// <summary>Bảng LichSuGiaoDich (kho). Delta &gt; 0 là tăng tồn, &lt; 0 là giảm tồn.</summary>
internal sealed record StockLog(DateTime Time, string ProductCode, string Kind, int Delta, string Note, string User)
{
    public const string In = "Nhập kho";
    public const string Sale = "Bán hàng";
    public const string Refund = "Hoàn kho";
    public const string Adjust = "Điều chỉnh";
}

internal static class Store
{
    public static readonly List<Account> Accounts = new();
    public static readonly List<Customer> Customers = new();
    public static readonly List<Product> Products = new();
    public static readonly List<Invoice> Invoices = new();
    public static readonly List<StockLog> StockLogs = new();

    static Store() => Seed();

    // ---------- Tiện ích ----------
    private static string NextCode(string prefix, IEnumerable<string> codes)
    {
        int max = 0;
        foreach (var c in codes)
            if (c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(c.AsSpan(prefix.Length), out int n))
                max = Math.Max(max, n);
        return prefix + (max + 1).ToString("D3");
    }

    public static string NextAccountCode() => NextCode("NV", Accounts.Select(a => a.Code));
    public static string NextCustomerCode() => NextCode("KH", Customers.Select(c => c.Code));
    public static string NextProductCode() => NextCode("SP", Products.Select(p => p.Code));
    private static string NextInvoiceCode() => NextCode("HD", Invoices.Select(i => i.Code));

    public static bool IsValidPhone(string phone)
    {
        string digits = phone.Trim();
        return digits.Length is >= 9 and <= 11 && digits.All(char.IsDigit);
    }

    public static bool UserNameTaken(string userName, Account? except = null) =>
        Accounts.Any(a => !ReferenceEquals(a, except) && a.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));

    public static bool CustomerHasInvoices(Customer c) => Invoices.Any(i => i.CustomerCode == c.Code);

    public static IEnumerable<Product> LowStock() =>
        Products.Where(p => !p.Discontinued && p.Stock <= Product.LowStockLimit);

    // ---------- Nghiệp vụ bán hàng (TV3) ----------
    /// <summary>
    /// Lập hóa đơn + chi tiết + (thanh toán) + trừ kho trong MỘT lần: kiểm tra hết rồi mới ghi, lỗi thì không ghi gì.
    /// Khi nối SQL: bọc toàn bộ trong SqlTransaction (INSERT HoaDon, ChiTietHoaDon, ThanhToan, UPDATE SanPham, LichSuGiaoDich).
    /// </summary>
    public static string? CreateInvoice(Account staff, Customer? customer,
        IReadOnlyList<(Product Product, int Qty)> cart, bool paid, string payMethod, out Invoice? invoice)
    {
        invoice = null;
        if (cart.Count == 0) return "Chưa có sản phẩm trong đơn hàng.";
        foreach (var (p, q) in cart)
        {
            if (q <= 0) return "Số lượng phải là số nguyên dương.";
            if (p.Discontinued) return $"Sản phẩm \"{p.Name}\" đã ngừng kinh doanh.";
            if (q > p.Stock) return $"Sản phẩm \"{p.Name}\" không đủ hàng (còn {p.Stock}).";
        }

        var inv = new Invoice
        {
            Code = NextInvoiceCode(),
            CustomerCode = customer?.Code ?? "",
            Customer = customer?.Name ?? "Khách lẻ",
            Date = DateTime.Now,
            StaffUser = staff.UserName,
            StaffName = staff.FullName,
            Status = paid ? Invoice.Paid : Invoice.Pending,
            PayMethod = paid ? payMethod : ""
        };
        foreach (var (p, q) in cart)
        {
            p.Stock -= q;
            inv.Lines.Add(new InvoiceLine(p.Code, p.Name, p.Price, q));
            StockLogs.Add(new StockLog(inv.Date, p.Code, StockLog.Sale, -q, inv.Code, staff.UserName));
        }
        Invoices.Add(inv);
        invoice = inv;
        return null;
    }

    /// <summary>Chỉ nhân viên lập hóa đơn mới được thu tiền hóa đơn đang chờ của chính mình.</summary>
    public static string? PayInvoice(Invoice inv, Account by, string method)
    {
        if (by.IsManager) return "Quản lý chỉ xem hóa đơn, việc thanh toán do nhân viên thực hiện.";
        if (inv.StaffUser != by.UserName) return "Bạn chỉ được thanh toán hóa đơn do mình lập.";
        if (inv.Status != Invoice.Pending) return "Hóa đơn này không ở trạng thái chờ thanh toán.";
        inv.Status = Invoice.Paid;
        inv.PayMethod = method;
        return null;
    }

    /// <summary>Hủy hóa đơn: bắt buộc có lý do, lưu người hủy + thời điểm, hoàn kho đúng một lần.</summary>
    public static string? CancelInvoice(Invoice inv, Account by, string reason)
    {
        if (!by.IsManager) return "Chỉ Quản lý mới được hủy hóa đơn.";
        if (inv.Status == Invoice.Cancelled) return "Hóa đơn này đã được hủy trước đó.";
        if (reason.Trim().Length == 0) return "Vui lòng nhập lý do hủy hóa đơn.";

        var now = DateTime.Now;
        foreach (var line in inv.Lines)
        {
            var p = Products.FirstOrDefault(x => x.Code == line.ProductCode);
            if (p == null) continue;
            p.Stock += line.Qty;
            StockLogs.Add(new StockLog(now, p.Code, StockLog.Refund, line.Qty, "Hủy " + inv.Code, by.UserName));
        }
        inv.Status = Invoice.Cancelled;
        inv.CancelReason = reason.Trim();
        inv.CancelledBy = by.FullName;
        inv.CancelledAt = now;
        return null;
    }

    // ---------- Nghiệp vụ kho (TV2) ----------
    public static string? StockIn(Product p, int qty, string note, Account by)
    {
        if (!by.IsManager) return "Chỉ Quản lý mới được nhập kho.";
        if (qty <= 0) return "Số lượng nhập phải là số nguyên dương.";
        p.Stock += qty;
        StockLogs.Add(new StockLog(DateTime.Now, p.Code, StockLog.In, qty, note, by.UserName));
        return null;
    }

    /// <summary>Điều chỉnh tồn kho sau kiểm kê: bắt buộc ghi lý do.</summary>
    public static string? AdjustStock(Product p, int newQty, string reason, Account by)
    {
        if (!by.IsManager) return "Chỉ Quản lý mới được điều chỉnh tồn kho.";
        if (newQty < 0) return "Số lượng tồn không được âm.";
        if (reason.Trim().Length == 0) return "Vui lòng ghi rõ lý do điều chỉnh.";
        int delta = newQty - p.Stock;
        if (delta == 0) return "Số lượng mới trùng với tồn kho hiện tại.";
        p.Stock = newQty;
        StockLogs.Add(new StockLog(DateTime.Now, p.Code, StockLog.Adjust, delta, reason.Trim(), by.UserName));
        return null;
    }

    // ---------- Dữ liệu mẫu ----------
    private static void Seed()
    {
        void Acc(string code, string name, string phone, string addr, string user, string pass, string role, bool active = true) =>
            Accounts.Add(new Account
            {
                Code = code, FullName = name, Phone = phone, Address = addr, UserName = user,
                PasswordHash = PasswordHasher.Hash(pass), Role = role, Active = active
            });

        Acc("NV001", "Nguyễn Văn Nam", "0901111222", "12 Lê Lợi, Quận 1, TP.HCM", "nam", "123456", Roles.Staff);
        Acc("NV002", "Trần Thị Lan", "0903333444", "45 Nguyễn Huệ, Quận 1, TP.HCM", "admin", "admin123", Roles.Manager);
        Acc("NV003", "Lê Minh Tuấn", "0905555666", "88 Trần Hưng Đạo, Quận 5, TP.HCM", "tuan", "123456", Roles.Staff);
        Acc("NV004", "Phạm Thu Trang", "0907777888", "21 Võ Văn Tần, Quận 3, TP.HCM", "trang", "123456", Roles.Staff);
        Acc("NV005", "Ngô Quang Huy", "0909999000", "9 Hai Bà Trưng, Quận 1, TP.HCM", "huy", "123456", Roles.Staff, false);

        void Cus(string code, string name, string phone, string email, string addr) =>
            Customers.Add(new Customer { Code = code, Name = name, Phone = phone, Email = email, Address = addr });

        Cus("KH001", "Nguyễn Minh Anh", "0901234567", "minhanh@email.com", "15 Pasteur, Quận 1");
        Cus("KH002", "Trần Quốc Bảo", "0912345678", "quocbao@email.com", "27 Cách Mạng Tháng 8, Quận 3");
        Cus("KH003", "Lê Thu Hà", "0987654321", "thuha@email.com", "102 Nguyễn Trãi, Quận 5");
        Cus("KH004", "Phạm Hoàng Long", "0934567890", "hoanglong@email.com", "56 Lý Thường Kiệt, Quận 10");
        Cus("KH005", "Đỗ Ngọc Mai", "0966778899", "ngocmai@email.com", "8 Phan Xích Long, Phú Nhuận");
        Cus("KH006", "Vũ Hải Đăng", "0977112233", "haidang@email.com", "73 Điện Biên Phủ, Bình Thạnh");
        Cus("KH007", "Bùi Thanh Tâm", "0905998877", "thanhtam@email.com", "34 Hoàng Văn Thụ, Tân Bình");

        void Prod(string code, string name, string cat, decimal price, int stock) =>
            Products.Add(new Product { Code = code, Name = name, Category = cat, Price = price, Stock = stock });

        Prod("SP001", "Bàn phím cơ K500", "Phụ kiện", 850_000m, 18);
        Prod("SP002", "Chuột không dây M220", "Phụ kiện", 320_000m, 4);
        Prod("SP003", "Tai nghe Gaming H7", "Thiết bị âm thanh", 690_000m, 0);
        Prod("SP004", "Màn hình 24 inch", "Màn hình", 2_850_000m, 12);
        Prod("SP005", "USB 64GB", "Lưu trữ", 180_000m, 3);
        Prod("SP006", "Laptop văn phòng A15", "Máy tính", 12_500_000m, 7);
        Prod("SP007", "Webcam Full HD", "Phụ kiện", 540_000m, 9);
        Prod("SP008", "Loa Bluetooth S30", "Thiết bị âm thanh", 450_000m, 11);
        Prod("SP009", "Ổ cứng SSD 512GB", "Lưu trữ", 1_290_000m, 15);
        Prod("SP010", "Bàn di chuột cỡ lớn", "Phụ kiện", 120_000m, 25);
        Prod("SP011", "Router Wi-Fi AC1200", "Phụ kiện", 750_000m, 2);
        Prod("SP012", "Màn hình cong 27 inch", "Màn hình", 4_990_000m, 6);

        // Hóa đơn mẫu (ngày tính lùi từ hôm nay để biểu đồ luôn có số liệu). Tồn kho ở trên là tồn HIỆN TẠI,
        // nên ở đây chỉ ghi lịch sử, không trừ kho lần nữa.
        void Inv(string code, string custCode, int daysAgo, int hour, string staffUser, string status, params (string P, int Q)[] lines)
        {
            var cus = Customers.FirstOrDefault(c => c.Code == custCode);
            var st = Accounts.First(a => a.UserName == staffUser);
            var inv = new Invoice
            {
                Code = code,
                CustomerCode = cus?.Code ?? "",
                Customer = cus?.Name ?? "Khách lẻ",
                Date = DateTime.Today.AddDays(-daysAgo).AddHours(hour),
                StaffUser = st.UserName,
                StaffName = st.FullName,
                Status = status,
                PayMethod = status == Invoice.Pending ? "" : "Tiền mặt"
            };
            foreach (var (pc, q) in lines)
            {
                var p = Products.First(x => x.Code == pc);
                inv.Lines.Add(new InvoiceLine(p.Code, p.Name, p.Price, q));
                StockLogs.Add(new StockLog(inv.Date, p.Code, StockLog.Sale, -q, code, staffUser));
                if (status == Invoice.Cancelled)
                    StockLogs.Add(new StockLog(inv.Date.AddHours(5), p.Code, StockLog.Refund, q, "Hủy " + code, "admin"));
            }
            if (status == Invoice.Cancelled)
            {
                inv.CancelReason = "Khách đổi ý, không lấy hàng";
                inv.CancelledBy = "Trần Thị Lan";
                inv.CancelledAt = inv.Date.AddHours(5);
            }
            Invoices.Add(inv);
        }

        Inv("HD001", "KH001", 2, 9, "nam", Invoice.Paid, ("SP001", 1), ("SP002", 1));
        Inv("HD002", "KH002", 2, 14, "tuan", Invoice.Paid, ("SP004", 1));
        Inv("HD003", "KH003", 3, 10, "nam", Invoice.Pending, ("SP003", 1));
        Inv("HD004", "KH004", 3, 15, "tuan", Invoice.Paid, ("SP006", 1));
        Inv("HD005", "KH005", 4, 11, "nam", Invoice.Cancelled, ("SP002", 1), ("SP005", 1));
        Inv("HD006", "KH006", 4, 16, "tuan", Invoice.Paid, ("SP004", 1), ("SP008", 1));
        Inv("HD007", "KH007", 5, 9, "nam", Invoice.Pending, ("SP010", 8));
        Inv("HD008", "KH002", 5, 13, "tuan", Invoice.Paid, ("SP009", 1), ("SP007", 1));
        Inv("HD009", "", 6, 10, "nam", Invoice.Paid, ("SP010", 2), ("SP005", 1));
        Inv("HD010", "KH003", 6, 17, "tuan", Invoice.Paid, ("SP012", 1));
        Inv("HD011", "KH004", 1, 9, "nam", Invoice.Paid, ("SP001", 2));
        Inv("HD012", "", 1, 15, "tuan", Invoice.Paid, ("SP008", 2), ("SP010", 1));
        Inv("HD013", "KH005", 0, 7, "nam", Invoice.Paid, ("SP009", 1));
        Inv("HD014", "KH006", 0, 8, "tuan", Invoice.Paid, ("SP007", 1), ("SP010", 1));
        Inv("HD015", "KH001", 0, 8, "nam", Invoice.Pending, ("SP011", 1));

        // Phiếu nhập ban đầu = tồn hiện tại - biến động đã ghi, để công thức Tồn cuối = Tồn đầu + Nhập - Xuất + Điều chỉnh luôn khớp.
        foreach (var p in Products)
        {
            int net = StockLogs.Where(l => l.ProductCode == p.Code).Sum(l => l.Delta);
            int first = p.Stock - net;
            if (first > 0)
                StockLogs.Add(new StockLog(DateTime.Today.AddDays(-30), p.Code, StockLog.In, first, "Nhập kho ban đầu", "admin"));
        }
    }
}
