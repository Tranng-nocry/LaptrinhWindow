namespace BaiTapLon;

/// <summary>
/// Phiên đăng nhập của ứng dụng. Không có chức năng tự đăng ký: tài khoản do Quản lý tạo trong mục "Nhân viên".
/// Khi nối SQL Server chỉ cần sửa hàm SignIn (truy vấn bảng TaiKhoan + so sánh mật khẩu đã băm).
/// </summary>
internal static class Session
{
    public static Account? Current { get; private set; }

    public static bool IsManager => Current?.IsManager ?? false;

    /// <summary>Trả về tài khoản nếu đăng nhập hợp lệ; ngược lại trả về null kèm thông báo lỗi.</summary>
    public static Account? SignIn(string userName, string password, out string error)
    {
        error = "Tên đăng nhập hoặc mật khẩu không đúng.";

        var acc = Store.Accounts.FirstOrDefault(a => a.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
        if (acc == null || !PasswordHasher.Verify(password, acc.PasswordHash))
            return null;

        if (!acc.Active)
        {
            error = "Tài khoản đã bị khóa. Vui lòng liên hệ Quản lý.";
            return null;
        }

        error = "";
        Current = acc;
        return acc;
    }

    public static void SignOut() => Current = null;
}
