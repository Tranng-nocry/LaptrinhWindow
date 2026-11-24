using System.Data;
using Microsoft.Data.SqlClient;

namespace BaiTapLon;

/// <summary>
/// Phiên đăng nhập của ứng dụng. Không có chức năng tự đăng ký: tài khoản do Admin tạo trong mục "Nhân viên".
/// Đăng nhập truy vấn thẳng bảng TaiKhoan trong SQL Server và so sánh mật khẩu đã băm SHA-256.
/// </summary>
internal static class Session
{
    public static Account? Current { get; private set; }

    public static bool IsAdmin => Current?.IsAdmin ?? false;
    public static bool IsWarehouse => Current?.IsWarehouse ?? false;
    public static bool IsStaff => Current?.IsStaff ?? false;

    /// <summary>Trả về tài khoản nếu đăng nhập hợp lệ; ngược lại trả về null kèm thông báo lỗi.</summary>
    public static Account? SignIn(string userName, string password, out string error)
    {
        error = "Tên đăng nhập hoặc mật khẩu không đúng.";
        try
        {
            var dt = Db.Query(
                @"SELECT nv.MaNV, nv.HoTen, nv.SoDienThoai, ISNULL(nv.DiaChi, N'') AS DiaChi, nv.TrangThai AS TrangThaiNV,
                         tk.TenDangNhap, tk.MatKhauHash, tk.VaiTro, tk.TrangThaiTK
                  FROM TaiKhoan tk INNER JOIN NhanVien nv ON nv.MaNV = tk.MaNV
                  WHERE tk.TenDangNhap = @u",
                Db.P("@u", userName));

            if (dt.Rows.Count == 0) return null;
            var r = dt.Rows[0];
            if (!PasswordHasher.Verify(password, Convert.ToString(r["MatKhauHash"]) ?? "")) return null;

            bool active = Convert.ToString(r["TrangThaiTK"]) == "Hoạt động" && Convert.ToString(r["TrangThaiNV"]) == "Đang làm";
            if (!active)
            {
                error = "Tài khoản đã bị khóa. Vui lòng liên hệ Admin.";
                return null;
            }

            string role = Convert.ToString(r["VaiTro"]) ?? Roles.Staff;
            var acc = new Account
            {
                Code = Convert.ToString(r["MaNV"]) ?? "",
                FullName = Convert.ToString(r["HoTen"]) ?? "",
                Phone = Convert.ToString(r["SoDienThoai"]) ?? "",
                Address = Convert.ToString(r["DiaChi"]) ?? "",
                UserName = Convert.ToString(r["TenDangNhap"]) ?? "",
                PasswordHash = Convert.ToString(r["MatKhauHash"]) ?? "",
                Role = Roles.All.Contains(role) ? role : Roles.Staff,
                Active = true
            };

            try { Db.Execute("UPDATE TaiKhoan SET LanDangNhapCuoi = GETDATE() WHERE MaNV = @m", Db.P("@m", acc.Code)); }
            catch { /* không quan trọng nếu ghi lần đăng nhập cuối thất bại */ }

            error = "";
            Current = acc;
            return acc;
        }
        catch (Exception ex)
        {
            error = "Không kết nối được cơ sở dữ liệu: " + ex.Message;
            return null;
        }
    }

    /// <summary>Sau khi Store.Load() nạp lại dữ liệu, trỏ tài khoản đang đăng nhập về đối tượng mới trong Store.Accounts.</summary>
    public static void Rebind()
    {
        if (Current == null) return;
        Current = Store.Accounts.FirstOrDefault(a => a.Code == Current.Code) ?? Current;
    }

    public static void SignOut() => Current = null;
}
