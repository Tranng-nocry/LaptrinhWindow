using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using static BaiTapLon.Db;

namespace BaiTapLon;

// =====================================================================
//  Models.cs - Mô hình dữ liệu + tầng nghiệp vụ dùng chung.
//
//  Mô hình DISCONNECTED: Store.Load() đổ dữ liệu từ SQL Server vào DataSet (qua SqlDataAdapter),
//  đóng kết nối ngay, rồi dựng các danh sách đối tượng trong bộ nhớ cho các Form dùng.
//  Mọi thao tác ghi (thêm/sửa/xóa, lập hóa đơn, nhập kho...) đi qua các hàm của Store:
//  mở kết nối -> chạy trong SqlTransaction -> đóng ngay -> cập nhật lại danh sách trong bộ nhớ.
// =====================================================================

/// <summary>3 vai trò của hệ thống. Chuỗi này cũng chính là giá trị lưu ở cột TaiKhoan.VaiTro.</summary>
internal static class Roles
{
    public const string Admin = "Admin";
    public const string Warehouse = "Thủ kho";
    public const string Staff = "Nhân viên bán hàng";

    public static readonly string[] All = { Admin, Warehouse, Staff };
}

/// <summary>Băm mật khẩu SHA-256 (hex 64 ký tự, tính trên chuỗi Unicode) - khớp với HASHBYTES('SHA2_256', NVARCHAR) trong SQL.</summary>
internal static class PasswordHasher
{
    public static string Hash(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(password)));

    public static bool Verify(string password, string stored) =>
        string.Equals(Hash(password), stored.Trim(), StringComparison.OrdinalIgnoreCase);
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

    public bool IsAdmin => Role == Roles.Admin;
    public bool IsWarehouse => Role == Roles.Warehouse;
    public bool IsStaff => Role == Roles.Staff;
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
    public const int DefaultMinStock = 5;

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }           // giá bán
    public decimal CostPrice { get; set; }       // giá nhập (chỉ Admin được xem)
    public int Stock { get; set; }
    public int MinStock { get; set; } = DefaultMinStock;
    public bool Discontinued { get; set; }

    public string Status => Discontinued ? "Ngừng kinh doanh"
        : Stock == 0 ? "Hết hàng"
        : Stock <= MinStock ? "Sắp hết"
        : "Còn hàng";

    public bool NeedsRestock => !Discontinued && Stock <= MinStock;

    /// <summary>Gợi ý số lượng nên nhập để đưa tồn kho lên gấp 3 lần mức tối thiểu.</summary>
    public int SuggestedImport => NeedsRestock ? Math.Max(1, MinStock * 3 - Stock) : 0;
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

/// <summary>Bảng LichSuGiaoDich (kho). Delta &gt; 0 là tăng tồn, &lt; 0 là giảm tồn. User = mã nhân viên thực hiện.</summary>
internal sealed record StockLog(DateTime Time, string ProductCode, string Kind, int Delta, string Note, string User)
{
    public const string In = "Nhập kho";
    public const string Sale = "Bán hàng";
    public const string Refund = "Hoàn kho";
    public const string Adjust = "Điều chỉnh";
}

/// <summary>Bảng PhieuNhap: mỗi lần nhập kho là một phiếu, dùng để thống kê tiền nhập hàng.</summary>
internal sealed record ImportReceipt(string Code, DateTime Date, string StaffCode, decimal Total, string Status)
{
    public const string Confirmed = "Đã xác nhận";
}

internal static class Store
{
    public static readonly List<Account> Accounts = new();
    public static readonly List<Customer> Customers = new();
    public static readonly List<Product> Products = new();
    public static readonly List<Invoice> Invoices = new();
    public static readonly List<StockLog> StockLogs = new();
    public static readonly List<ImportReceipt> Imports = new();

    /// <summary>Bộ nhớ đệm DataSet (dữ liệu vừa tải từ SQL Server, không giữ kết nối).</summary>
    public static DataSet Data { get; } = new("QuanLyBanHang");

    // =====================================================================
    //  NẠP DỮ LIỆU: SQL Server -> DataSet -> danh sách đối tượng
    // =====================================================================
    public static void Load()
    {
        Fill(Data, "NhanVien",
            @"SELECT nv.MaNV, nv.HoTen, nv.SoDienThoai, ISNULL(nv.DiaChi, N'') AS DiaChi,
                     tk.TenDangNhap, tk.MatKhauHash, tk.VaiTro, tk.TrangThaiTK
              FROM NhanVien nv INNER JOIN TaiKhoan tk ON tk.MaNV = nv.MaNV
              ORDER BY nv.MaNV");

        Fill(Data, "KhachHang",
            @"SELECT MaKH, HoTen, SoDienThoai, ISNULL(Email, N'') AS Email, ISNULL(DiaChi, N'') AS DiaChi, TrangThai
              FROM KhachHang ORDER BY MaKH");

        Fill(Data, "SanPham",
            @"SELECT sp.MaSP, sp.TenSP, dm.TenDanhMuc, sp.GiaNhap, sp.GiaBan,
                     ISNULL(k.SoLuongTon, 0) AS SoLuongTon, ISNULL(k.MucTonToiThieu, 5) AS MucTonToiThieu, sp.TrangThaiKD
              FROM SanPham sp
              INNER JOIN DanhMuc dm ON dm.MaDanhMuc = sp.MaDanhMuc
              LEFT JOIN Kho k ON k.MaSP = sp.MaSP
              ORDER BY sp.MaSP");

        Fill(Data, "HoaDon",
            @"SELECT hd.MaHD, ISNULL(hd.MaKH, N'') AS MaKH, ISNULL(kh.HoTen, N'Khách lẻ') AS TenKH, hd.NgayLap,
                     ISNULL(tk.TenDangNhap, N'') AS TenDangNhap, nv.HoTen AS TenNV, hd.TrangThai,
                     ISNULL((SELECT TOP 1 tt.PhuongThuc FROM ThanhToan tt WHERE tt.MaHD = hd.MaHD ORDER BY tt.MaThanhToan DESC), N'') AS PhuongThuc,
                     ISNULL(hd.LyDoHuy, N'') AS LyDoHuy, ISNULL(nvh.HoTen, N'') AS TenNVHuy, hd.NgayHuy
              FROM HoaDon hd
              LEFT JOIN KhachHang kh ON kh.MaKH = hd.MaKH
              INNER JOIN NhanVien nv ON nv.MaNV = hd.MaNV
              LEFT JOIN TaiKhoan tk ON tk.MaNV = hd.MaNV
              LEFT JOIN NhanVien nvh ON nvh.MaNV = hd.MaNVHuy
              ORDER BY hd.NgayLap, hd.MaHD");

        Fill(Data, "ChiTietHoaDon",
            @"SELECT ct.MaHD, ct.MaSP, sp.TenSP, ct.DonGia, ct.SoLuong
              FROM ChiTietHoaDon ct INNER JOIN SanPham sp ON sp.MaSP = ct.MaSP
              ORDER BY ct.MaHD, ct.MaSP");

        Fill(Data, "LichSuGiaoDich",
            @"SELECT ThoiGian, MaSP, LoaiGiaoDich, SoLuongBienDong, ISNULL(GhiChu, N'') AS GhiChu, MaNVThucHien
              FROM LichSuGiaoDich ORDER BY ThoiGian, MaGD");

        Fill(Data, "PhieuNhap",
            @"SELECT MaPhieuNhap, NgayNhap, MaNVLap, TongTienNhap, TrangThai FROM PhieuNhap ORDER BY NgayNhap");

        // ----- Dựng danh sách đối tượng từ DataTable -----
        Accounts.Clear();
        foreach (DataRow r in Data.Tables["NhanVien"]!.Rows)
            Accounts.Add(new Account
            {
                Code = Str(r, "MaNV"),
                FullName = Str(r, "HoTen"),
                Phone = Str(r, "SoDienThoai"),
                Address = Str(r, "DiaChi"),
                UserName = Str(r, "TenDangNhap"),
                PasswordHash = Str(r, "MatKhauHash"),
                Role = Roles.All.Contains(Str(r, "VaiTro")) ? Str(r, "VaiTro") : Roles.Staff,
                Active = Str(r, "TrangThaiTK") == "Hoạt động"
            });

        Customers.Clear();
        foreach (DataRow r in Data.Tables["KhachHang"]!.Rows)
            Customers.Add(new Customer
            {
                Code = Str(r, "MaKH"),
                Name = Str(r, "HoTen"),
                Phone = Str(r, "SoDienThoai"),
                Email = Str(r, "Email"),
                Address = Str(r, "DiaChi"),
                Active = Str(r, "TrangThai") == "Hoạt động"
            });

        Products.Clear();
        foreach (DataRow r in Data.Tables["SanPham"]!.Rows)
            Products.Add(new Product
            {
                Code = Str(r, "MaSP"),
                Name = Str(r, "TenSP"),
                Category = Str(r, "TenDanhMuc"),
                CostPrice = Dec(r, "GiaNhap"),
                Price = Dec(r, "GiaBan"),
                Stock = (int)Dec(r, "SoLuongTon"),
                MinStock = (int)Dec(r, "MucTonToiThieu"),
                Discontinued = Str(r, "TrangThaiKD") != "Đang kinh doanh"
            });

        Invoices.Clear();
        var byCode = new Dictionary<string, Invoice>();
        foreach (DataRow r in Data.Tables["HoaDon"]!.Rows)
        {
            var inv = new Invoice
            {
                Code = Str(r, "MaHD"),
                CustomerCode = Str(r, "MaKH"),
                Customer = Str(r, "TenKH"),
                Date = (DateTime)r["NgayLap"],
                StaffUser = Str(r, "TenDangNhap"),
                StaffName = Str(r, "TenNV"),
                Status = Str(r, "TrangThai"),
                PayMethod = Str(r, "PhuongThuc"),
                CancelReason = Str(r, "LyDoHuy"),
                CancelledBy = Str(r, "TenNVHuy"),
                CancelledAt = r["NgayHuy"] == DBNull.Value ? null : (DateTime)r["NgayHuy"]
            };
            Invoices.Add(inv);
            byCode[inv.Code] = inv;
        }
        foreach (DataRow r in Data.Tables["ChiTietHoaDon"]!.Rows)
            if (byCode.TryGetValue(Str(r, "MaHD"), out var inv))
                inv.Lines.Add(new InvoiceLine(Str(r, "MaSP"), Str(r, "TenSP"), Dec(r, "DonGia"), (int)Dec(r, "SoLuong")));

        StockLogs.Clear();
        foreach (DataRow r in Data.Tables["LichSuGiaoDich"]!.Rows)
            StockLogs.Add(new StockLog((DateTime)r["ThoiGian"], Str(r, "MaSP"), Str(r, "LoaiGiaoDich"),
                (int)Dec(r, "SoLuongBienDong"), Str(r, "GhiChu"), Str(r, "MaNVThucHien")));

        Imports.Clear();
        foreach (DataRow r in Data.Tables["PhieuNhap"]!.Rows)
            Imports.Add(new ImportReceipt(Str(r, "MaPhieuNhap"), (DateTime)r["NgayNhap"], Str(r, "MaNVLap"),
                Dec(r, "TongTienNhap"), Str(r, "TrangThai")));

        Session.Rebind();   // để tài khoản đang đăng nhập trỏ đúng đối tượng mới nạp
    }

    private static string Str(DataRow r, string col) =>
        r[col] == DBNull.Value ? "" : Convert.ToString(r[col]) ?? "";

    private static decimal Dec(DataRow r, string col) =>
        r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);

    // =====================================================================
    //  TIỆN ÍCH
    // =====================================================================
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

    public static bool IsValidPhone(string phone)
    {
        string digits = phone.Trim();
        return digits.Length is >= 9 and <= 11 && digits.All(char.IsDigit);
    }

    public static bool UserNameTaken(string userName, Account? except = null) =>
        Accounts.Any(a => !ReferenceEquals(a, except) && a.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));

    public static bool CustomerHasInvoices(Customer c) => Invoices.Any(i => i.CustomerCode == c.Code);

    /// <summary>Hàng hết hoặc sắp hết (tồn &lt;= mức tối thiểu), hết hàng lên đầu.</summary>
    public static IEnumerable<Product> LowStock() => Products.Where(p => p.NeedsRestock);

    /// <summary>Chạy một thao tác ghi CSDL; lỗi hệ thống được đổi thành thông báo tiếng Việt thay vì làm sập chương trình.</summary>
    private static string? Guard(Func<string?> action)
    {
        try { return action(); }
        catch (SqlException ex)
        {
            return ex.Number switch
            {
                2627 or 2601 => "Dữ liệu bị trùng (mã, số điện thoại hoặc tên đăng nhập đã tồn tại).",
                547 => "Dữ liệu đang được dùng ở nơi khác nên không thể thực hiện thao tác này.",
                _ => "Lỗi cơ sở dữ liệu: " + ex.Message
            };
        }
        catch (Exception ex)
        {
            return "Lỗi hệ thống: " + ex.Message;
        }
    }

    // =====================================================================
    //  KHÁCH HÀNG
    // =====================================================================
    public static string? SaveCustomer(Customer? existing, string name, string phone, string email, string address, out Customer? saved)
    {
        Customer? result = null;
        string? error = Guard(() =>
        {
            if (existing == null)
            {
                string code = NextCustomerCode();
                Execute("INSERT INTO KhachHang (MaKH, HoTen, SoDienThoai, Email, DiaChi, TrangThai) VALUES (@c, @n, @p, @e, @a, N'Hoạt động')",
                    P("@c", code), P("@n", name), P("@p", phone),
                    P("@e", email.Length == 0 ? null : email), P("@a", address.Length == 0 ? null : address));
                result = new Customer { Code = code, Name = name, Phone = phone, Email = email, Address = address };
                Customers.Add(result);
            }
            else
            {
                Execute("UPDATE KhachHang SET HoTen = @n, SoDienThoai = @p, Email = @e, DiaChi = @a WHERE MaKH = @c",
                    P("@c", existing.Code), P("@n", name), P("@p", phone),
                    P("@e", email.Length == 0 ? null : email), P("@a", address.Length == 0 ? null : address));
                existing.Name = name;
                existing.Phone = phone;
                existing.Email = email;
                existing.Address = address;
                result = existing;
            }
            return null;
        });
        saved = error == null ? result : null;
        return error;
    }

    public static string? SetCustomerActive(Customer c, bool active) => Guard(() =>
    {
        Execute("UPDATE KhachHang SET TrangThai = @s WHERE MaKH = @c",
            P("@s", active ? "Hoạt động" : "Ngừng hoạt động"), P("@c", c.Code));
        c.Active = active;
        return null;
    });

    public static string? DeleteCustomer(Customer c) => Guard(() =>
    {
        Execute("DELETE FROM KhachHang WHERE MaKH = @c", P("@c", c.Code));
        Customers.Remove(c);
        return null;
    });

    // =====================================================================
    //  SẢN PHẨM (chỉ Admin)
    // =====================================================================
    public static string? SaveProduct(Account by, Product? existing, string code, string name, string category,
        decimal price, decimal cost, int minStock)
    {
        if (!by.IsAdmin) return "Chỉ Admin mới được thêm/sửa sản phẩm và giá.";
        return Guard(() =>
        {
            string? e = InTransaction((c, t) =>
            {
                // Tìm danh mục theo tên, chưa có thì tạo mới
                string? dmCode = Convert.ToString(Scalar(c, t, "SELECT MaDanhMuc FROM DanhMuc WHERE TenDanhMuc = @n", P("@n", category)));
                if (string.IsNullOrEmpty(dmCode))
                {
                    dmCode = Db.NextCode(c, t, "DanhMuc", "MaDanhMuc", "DM");
                    Exec(c, t, "INSERT INTO DanhMuc (MaDanhMuc, TenDanhMuc) VALUES (@m, @n)", P("@m", dmCode), P("@n", category));
                }

                if (existing == null)
                {
                    Exec(c, t, "INSERT INTO SanPham (MaSP, TenSP, MaDanhMuc, GiaNhap, GiaBan) VALUES (@sp, @n, @dm, @gn, @gb)",
                        P("@sp", code), P("@n", name), P("@dm", dmCode), P("@gn", cost), P("@gb", price));
                    Exec(c, t, "INSERT INTO Kho (MaSP, SoLuongTon, MucTonToiThieu) VALUES (@sp, 0, @min)",
                        P("@sp", code), P("@min", minStock));
                }
                else
                {
                    Exec(c, t, "UPDATE SanPham SET TenSP = @n, MaDanhMuc = @dm, GiaNhap = @gn, GiaBan = @gb WHERE MaSP = @sp",
                        P("@sp", existing.Code), P("@n", name), P("@dm", dmCode), P("@gn", cost), P("@gb", price));
                    Exec(c, t, "UPDATE Kho SET MucTonToiThieu = @min, NgayCapNhat = GETDATE() WHERE MaSP = @sp",
                        P("@sp", existing.Code), P("@min", minStock));
                }
                return null;
            });
            if (e != null) return e;

            if (existing == null)
                Products.Add(new Product { Code = code, Name = name, Category = category, Price = price, CostPrice = cost, Stock = 0, MinStock = minStock });
            else
            {
                existing.Name = name;
                existing.Category = category;
                existing.Price = price;     // hóa đơn cũ giữ nguyên giá lúc bán (đã lưu trong chi tiết hóa đơn)
                existing.CostPrice = cost;
                existing.MinStock = minStock;
            }
            return null;
        });
    }

    public static string? SetProductDiscontinued(Account by, Product p, bool discontinued)
    {
        if (!by.IsAdmin) return "Chỉ Admin mới được đổi trạng thái kinh doanh.";
        return Guard(() =>
        {
            Execute("UPDATE SanPham SET TrangThaiKD = @s WHERE MaSP = @sp",
                P("@s", discontinued ? "Ngừng kinh doanh" : "Đang kinh doanh"), P("@sp", p.Code));
            p.Discontinued = discontinued;
            return null;
        });
    }

    // =====================================================================
    //  NHÂN VIÊN / TÀI KHOẢN (chỉ Admin)
    // =====================================================================
    public static string? SaveAccount(Account by, Account? existing, string name, string phone, string address,
        string userName, string password, string role)
    {
        if (!by.IsAdmin) return "Chỉ Admin mới được quản lý nhân viên.";
        if (!Roles.All.Contains(role)) return "Vai trò không hợp lệ.";
        return Guard(() =>
        {
            if (existing == null)
            {
                string code = NextAccountCode();
                string hash = PasswordHasher.Hash(password);
                string? e = InTransaction((c, t) =>
                {
                    Exec(c, t, "INSERT INTO NhanVien (MaNV, HoTen, ChucVu, SoDienThoai, DiaChi, TrangThai) VALUES (@m, @n, @cv, @p, @d, N'Đang làm')",
                        P("@m", code), P("@n", name), P("@cv", role), P("@p", phone), P("@d", address.Length == 0 ? null : address));
                    Exec(c, t, "INSERT INTO TaiKhoan (MaNV, TenDangNhap, MatKhauHash, VaiTro, TrangThaiTK) VALUES (@m, @u, @h, @r, N'Hoạt động')",
                        P("@m", code), P("@u", userName), P("@h", hash), P("@r", role));
                    return null;
                });
                if (e != null) return e;
                Accounts.Add(new Account
                {
                    Code = code, FullName = name, Phone = phone, Address = address, UserName = userName,
                    PasswordHash = hash, Role = role, Active = true
                });
            }
            else
            {
                string? e = InTransaction((c, t) =>
                {
                    Exec(c, t, "UPDATE NhanVien SET HoTen = @n, ChucVu = @cv, SoDienThoai = @p, DiaChi = @d WHERE MaNV = @m",
                        P("@m", existing.Code), P("@n", name), P("@cv", role), P("@p", phone), P("@d", address.Length == 0 ? null : address));
                    Exec(c, t, "UPDATE TaiKhoan SET VaiTro = @r WHERE MaNV = @m", P("@m", existing.Code), P("@r", role));
                    return null;
                });
                if (e != null) return e;
                existing.FullName = name;
                existing.Phone = phone;
                existing.Address = address;
                existing.Role = role;
            }
            return null;
        });
    }

    public static string? SetAccountActive(Account by, Account a, bool active)
    {
        if (!by.IsAdmin) return "Chỉ Admin mới được khóa/mở khóa tài khoản.";
        return Guard(() =>
        {
            Execute("UPDATE TaiKhoan SET TrangThaiTK = @s WHERE MaNV = @m",
                P("@s", active ? "Hoạt động" : "Đã khóa"), P("@m", a.Code));
            a.Active = active;
            return null;
        });
    }

    public static string? ResetPassword(Account by, Account a, string newPassword)
    {
        if (!by.IsAdmin) return "Chỉ Admin mới được đặt lại mật khẩu.";
        return Guard(() =>
        {
            string hash = PasswordHasher.Hash(newPassword);
            Execute("UPDATE TaiKhoan SET MatKhauHash = @h WHERE MaNV = @m", P("@h", hash), P("@m", a.Code));
            a.PasswordHash = hash;
            return null;
        });
    }

    /// <summary>Người dùng tự đổi mật khẩu: kiểm tra mật khẩu hiện tại trực tiếp trong CSDL.</summary>
    public static string? ChangePassword(Account a, string current, string next) => Guard(() =>
    {
        var dt = Query("SELECT MatKhauHash FROM TaiKhoan WHERE MaNV = @m", P("@m", a.Code));
        if (dt.Rows.Count == 0) return "Không tìm thấy tài khoản.";
        if (!PasswordHasher.Verify(current, Convert.ToString(dt.Rows[0][0]) ?? ""))
            return "Mật khẩu hiện tại không đúng.";
        string hash = PasswordHasher.Hash(next);
        Execute("UPDATE TaiKhoan SET MatKhauHash = @h WHERE MaNV = @m", P("@h", hash), P("@m", a.Code));
        a.PasswordHash = hash;
        return null;
    });

    // =====================================================================
    //  BÁN HÀNG (chỉ Nhân viên bán hàng)
    // =====================================================================
    /// <summary>
    /// Lập hóa đơn + chi tiết + (thanh toán) + trừ kho + ghi lịch sử kho trong MỘT SqlTransaction:
    /// đủ hàng mới ghi, lỗi giữa chừng thì ROLLBACK, không ghi gì.
    /// </summary>
    public static string? CreateInvoice(Account staff, Customer? customer,
        IReadOnlyList<(Product Product, int Qty)> cart, bool paid, string payMethod, out Invoice? invoice)
    {
        invoice = null;
        if (!staff.IsStaff) return "Chỉ Nhân viên bán hàng mới được lập hóa đơn.";
        if (cart.Count == 0) return "Chưa có sản phẩm trong đơn hàng.";
        foreach (var (p, q) in cart)
        {
            if (q <= 0) return "Số lượng phải là số nguyên dương.";
            if (p.Discontinued) return $"Sản phẩm \"{p.Name}\" đã ngừng kinh doanh.";
            if (q > p.Stock) return $"Sản phẩm \"{p.Name}\" không đủ hàng (còn {p.Stock}).";
        }

        Invoice? created = null;
        var newStock = new Dictionary<string, int>();
        var now = DateTime.Now;
        string? error = Guard(() => InTransaction((c, t) =>
        {
            string code = Db.NextCode(c, t, "HoaDon", "MaHD", "HD");
            decimal total = cart.Sum(x => x.Product.Price * x.Qty);
            string status = paid ? Invoice.Paid : Invoice.Pending;

            Exec(c, t, "INSERT INTO HoaDon (MaHD, MaKH, MaNV, NgayLap, TongTien, GiamGia, TrangThai) VALUES (@hd, @kh, @nv, @now, @tong, 0, @st)",
                P("@hd", code), P("@kh", customer?.Code), P("@nv", staff.Code), P("@now", now), P("@tong", total), P("@st", status));

            foreach (var (p, q) in cart)
            {
                // Đọc lại tồn kho thật trong CSDL (có khóa) để 2 máy bán cùng lúc không bị âm kho
                int stock = Convert.ToInt32(Scalar(c, t, "SELECT SoLuongTon FROM Kho WITH (UPDLOCK, ROWLOCK) WHERE MaSP = @sp", P("@sp", p.Code)) ?? 0);
                if (q > stock) return $"Sản phẩm \"{p.Name}\" không đủ hàng (còn {stock}).";
                int after = stock - q;

                Exec(c, t, "INSERT INTO ChiTietHoaDon (MaHD, MaSP, SoLuong, DonGia) VALUES (@hd, @sp, @q, @g)",
                    P("@hd", code), P("@sp", p.Code), P("@q", q), P("@g", p.Price));
                Exec(c, t, "UPDATE Kho SET SoLuongTon = @after, NgayCapNhat = @now WHERE MaSP = @sp",
                    P("@after", after), P("@now", now), P("@sp", p.Code));
                Exec(c, t, @"INSERT INTO LichSuGiaoDich (ThoiGian, LoaiGiaoDich, MaThamChieu, MaSP, SoLuongBienDong, TonSauGiaoDich, MaNVThucHien, GhiChu)
                             VALUES (@now, @loai, @hd, @sp, @delta, @after, @nv, @hd)",
                    P("@now", now), P("@loai", StockLog.Sale), P("@hd", code), P("@sp", p.Code),
                    P("@delta", -q), P("@after", after), P("@nv", staff.Code));
                newStock[p.Code] = after;
            }

            if (paid)
                Exec(c, t, "INSERT INTO ThanhToan (MaHD, NgayThanhToan, PhuongThuc, SoTien, TrangThai) VALUES (@hd, @now, @m, @tien, N'Thành công')",
                    P("@hd", code), P("@now", now), P("@m", payMethod), P("@tien", total));

            created = new Invoice
            {
                Code = code,
                CustomerCode = customer?.Code ?? "",
                Customer = customer?.Name ?? "Khách lẻ",
                Date = now,
                StaffUser = staff.UserName,
                StaffName = staff.FullName,
                Status = status,
                PayMethod = paid ? payMethod : ""
            };
            return null;
        }));
        if (error != null || created == null) return error ?? "Không thể lập hóa đơn.";

        // Cập nhật bộ nhớ sau khi COMMIT thành công
        foreach (var (p, q) in cart)
        {
            p.Stock = newStock[p.Code];
            created.Lines.Add(new InvoiceLine(p.Code, p.Name, p.Price, q));
            StockLogs.Add(new StockLog(now, p.Code, StockLog.Sale, -q, created.Code, staff.Code));
        }
        Invoices.Add(created);
        invoice = created;
        return null;
    }

    /// <summary>Chỉ nhân viên bán hàng lập hóa đơn mới được thu tiền hóa đơn đang chờ của chính mình.</summary>
    public static string? PayInvoice(Invoice inv, Account by, string method)
    {
        if (!by.IsStaff) return "Chỉ Nhân viên bán hàng mới được thu tiền hóa đơn.";
        if (inv.StaffUser != by.UserName) return "Bạn chỉ được thanh toán hóa đơn do mình lập.";
        if (inv.Status != Invoice.Pending) return "Hóa đơn này không ở trạng thái chờ thanh toán.";

        return Guard(() =>
        {
            var now = DateTime.Now;
            string? e = InTransaction((c, t) =>
            {
                int n = Exec(c, t, "UPDATE HoaDon SET TrangThai = N'Đã thanh toán' WHERE MaHD = @hd AND TrangThai = N'Chờ thanh toán'",
                    P("@hd", inv.Code));
                if (n == 0) return "Hóa đơn này đã được xử lý trước đó.";
                Exec(c, t, "INSERT INTO ThanhToan (MaHD, NgayThanhToan, PhuongThuc, SoTien, TrangThai) VALUES (@hd, @now, @m, @tien, N'Thành công')",
                    P("@hd", inv.Code), P("@now", now), P("@m", method), P("@tien", inv.Total));
                return null;
            });
            if (e != null) return e;
            inv.Status = Invoice.Paid;
            inv.PayMethod = method;
            return null;
        });
    }

    /// <summary>Hủy hóa đơn (chỉ Admin): bắt buộc có lý do, lưu người hủy + thời điểm, hoàn kho đúng một lần.</summary>
    public static string? CancelInvoice(Invoice inv, Account by, string reason)
    {
        if (!by.IsAdmin) return "Chỉ Admin mới được hủy hóa đơn.";
        if (inv.Status == Invoice.Cancelled) return "Hóa đơn này đã được hủy trước đó.";
        if (reason.Trim().Length == 0) return "Vui lòng nhập lý do hủy hóa đơn.";

        var now = DateTime.Now;
        string why = reason.Trim();
        var newStock = new Dictionary<string, int>();
        string? error = Guard(() => InTransaction((c, t) =>
        {
            string? st = Convert.ToString(Scalar(c, t, "SELECT TrangThai FROM HoaDon WITH (UPDLOCK) WHERE MaHD = @hd", P("@hd", inv.Code)));
            if (st == null) return "Không tìm thấy hóa đơn trong CSDL.";
            if (st == Invoice.Cancelled) return "Hóa đơn này đã được hủy trước đó.";

            Exec(c, t, "UPDATE HoaDon SET TrangThai = N'Đã hủy', LyDoHuy = @ly, MaNVHuy = @nv, NgayHuy = @now WHERE MaHD = @hd",
                P("@ly", why), P("@nv", by.Code), P("@now", now), P("@hd", inv.Code));

            foreach (var line in inv.Lines)
            {
                int stock = Convert.ToInt32(Scalar(c, t, "SELECT SoLuongTon FROM Kho WITH (UPDLOCK, ROWLOCK) WHERE MaSP = @sp", P("@sp", line.ProductCode)) ?? 0);
                int after = stock + line.Qty;
                Exec(c, t, "UPDATE Kho SET SoLuongTon = @after, NgayCapNhat = @now WHERE MaSP = @sp",
                    P("@after", after), P("@now", now), P("@sp", line.ProductCode));
                Exec(c, t, @"INSERT INTO LichSuGiaoDich (ThoiGian, LoaiGiaoDich, MaThamChieu, MaSP, SoLuongBienDong, TonSauGiaoDich, MaNVThucHien, GhiChu)
                             VALUES (@now, @loai, @hd, @sp, @delta, @after, @nv, @ghichu)",
                    P("@now", now), P("@loai", StockLog.Refund), P("@hd", inv.Code), P("@sp", line.ProductCode),
                    P("@delta", line.Qty), P("@after", after), P("@nv", by.Code), P("@ghichu", "Hủy " + inv.Code));
                newStock[line.ProductCode] = after;
            }
            return null;
        }));
        if (error != null) return error;

        foreach (var line in inv.Lines)
        {
            var p = Products.FirstOrDefault(x => x.Code == line.ProductCode);
            if (p != null && newStock.TryGetValue(p.Code, out int s)) p.Stock = s;
            StockLogs.Add(new StockLog(now, line.ProductCode, StockLog.Refund, line.Qty, "Hủy " + inv.Code, by.Code));
        }
        inv.Status = Invoice.Cancelled;
        inv.CancelReason = why;
        inv.CancelledBy = by.FullName;
        inv.CancelledAt = now;
        return null;
    }

    // =====================================================================
    //  KHO (Admin và Thủ kho)
    // =====================================================================
    /// <summary>Nhập kho: tạo phiếu nhập (giá nhập lấy từ sản phẩm), cộng tồn, ghi lịch sử - trong một transaction.</summary>
    public static string? StockIn(Product p, int qty, string note, Account by)
    {
        if (!(by.IsAdmin || by.IsWarehouse)) return "Chỉ Admin hoặc Thủ kho mới được nhập kho.";
        if (qty <= 0) return "Số lượng nhập phải là số nguyên dương.";

        var now = DateTime.Now;
        string receiptCode = "";
        int after = 0;
        decimal amount = p.CostPrice * qty;
        string? error = Guard(() => InTransaction((c, t) =>
        {
            receiptCode = Db.NextCode(c, t, "PhieuNhap", "MaPhieuNhap", "PN");
            Exec(c, t, "INSERT INTO PhieuNhap (MaPhieuNhap, NgayNhap, MaNVLap, TongTienNhap, TrangThai, GhiChu) VALUES (@pn, @now, @nv, @tong, N'Đã xác nhận', @gc)",
                P("@pn", receiptCode), P("@now", now), P("@nv", by.Code), P("@tong", amount), P("@gc", note.Length == 0 ? null : note));
            Exec(c, t, "INSERT INTO ChiTietPhieuNhap (MaPhieuNhap, MaSP, SoLuongNhap, GiaNhap) VALUES (@pn, @sp, @q, @g)",
                P("@pn", receiptCode), P("@sp", p.Code), P("@q", qty), P("@g", p.CostPrice));

            int stock = Convert.ToInt32(Scalar(c, t, "SELECT SoLuongTon FROM Kho WITH (UPDLOCK, ROWLOCK) WHERE MaSP = @sp", P("@sp", p.Code)) ?? 0);
            after = stock + qty;
            Exec(c, t, "UPDATE Kho SET SoLuongTon = @after, NgayCapNhat = @now WHERE MaSP = @sp",
                P("@after", after), P("@now", now), P("@sp", p.Code));
            Exec(c, t, @"INSERT INTO LichSuGiaoDich (ThoiGian, LoaiGiaoDich, MaThamChieu, MaSP, SoLuongBienDong, TonSauGiaoDich, MaNVThucHien, GhiChu)
                         VALUES (@now, @loai, @pn, @sp, @delta, @after, @nv, @gc)",
                P("@now", now), P("@loai", StockLog.In), P("@pn", receiptCode), P("@sp", p.Code),
                P("@delta", qty), P("@after", after), P("@nv", by.Code), P("@gc", note.Length == 0 ? "Nhập kho" : note));
            return null;
        }));
        if (error != null) return error;

        p.Stock = after;
        StockLogs.Add(new StockLog(now, p.Code, StockLog.In, qty, note.Length == 0 ? "Nhập kho" : note, by.Code));
        Imports.Add(new ImportReceipt(receiptCode, now, by.Code, amount, ImportReceipt.Confirmed));
        return null;
    }

    /// <summary>Điều chỉnh tồn kho sau kiểm kê: tạo phiếu kiểm kê, bắt buộc ghi lý do.</summary>
    public static string? AdjustStock(Product p, int newQty, string reason, Account by)
    {
        if (!(by.IsAdmin || by.IsWarehouse)) return "Chỉ Admin hoặc Thủ kho mới được điều chỉnh tồn kho.";
        if (newQty < 0) return "Số lượng tồn không được âm.";
        if (reason.Trim().Length == 0) return "Vui lòng ghi rõ lý do điều chỉnh.";

        var now = DateTime.Now;
        string why = reason.Trim();
        int delta = 0;
        string? error = Guard(() => InTransaction((c, t) =>
        {
            int stock = Convert.ToInt32(Scalar(c, t, "SELECT SoLuongTon FROM Kho WITH (UPDLOCK, ROWLOCK) WHERE MaSP = @sp", P("@sp", p.Code)) ?? 0);
            delta = newQty - stock;
            if (delta == 0) return "Số lượng mới trùng với tồn kho hiện tại.";

            string kk = Db.NextCode(c, t, "PhieuKiemKe", "MaPhieuKiemKe", "KK");
            Exec(c, t, "INSERT INTO PhieuKiemKe (MaPhieuKiemKe, NgayKiemKe, MaNVKiemKe, GhiChu, TrangThai) VALUES (@kk, @now, @nv, @gc, N'Đã cân bằng')",
                P("@kk", kk), P("@now", now), P("@nv", by.Code), P("@gc", why));
            Exec(c, t, "INSERT INTO ChiTietKiemKe (MaPhieuKiemKe, MaSP, TonHeThong, TonThucTe, LyDoDieuChinh) VALUES (@kk, @sp, @he, @tt, @ly)",
                P("@kk", kk), P("@sp", p.Code), P("@he", stock), P("@tt", newQty), P("@ly", why));
            Exec(c, t, "UPDATE Kho SET SoLuongTon = @q, NgayCapNhat = @now WHERE MaSP = @sp",
                P("@q", newQty), P("@now", now), P("@sp", p.Code));
            Exec(c, t, @"INSERT INTO LichSuGiaoDich (ThoiGian, LoaiGiaoDich, MaThamChieu, MaSP, SoLuongBienDong, TonSauGiaoDich, MaNVThucHien, GhiChu)
                         VALUES (@now, @loai, @kk, @sp, @delta, @q, @nv, @ly)",
                P("@now", now), P("@loai", StockLog.Adjust), P("@kk", kk), P("@sp", p.Code),
                P("@delta", delta), P("@q", newQty), P("@nv", by.Code), P("@ly", why));
            return null;
        }));
        if (error != null) return error;

        p.Stock = newQty;
        StockLogs.Add(new StockLog(now, p.Code, StockLog.Adjust, delta, why, by.Code));
        return null;
    }
}
