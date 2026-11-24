namespace BaiTapLon;


internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Kiểm tra kết nối SQL Server trước khi mở màn hình đăng nhập
        string? connError = Db.TestConnection();
        if (connError != null)
        {
            MessageBox.Show(connError, "Không kết nối được SQL Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Vòng lặp: Đăng nhập -> Màn hình chính -> (Đăng xuất) -> quay lại Đăng nhập
        while (true)
        {
            using (var login = new LoginForm())
            {
                if (login.ShowDialog() != DialogResult.OK)
                    return;                       // tắt cửa sổ đăng nhập = thoát chương trình
            }

            try
            {
                Store.Load();                     // đổ dữ liệu từ SQL Server vào DataSet rồi đóng kết nối
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được dữ liệu từ SQL Server:\n" + ex.Message, "Lỗi dữ liệu",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Session.SignOut();
                continue;
            }

            Application.Run(new Form1());

            if (Session.Current != null)
                return;                           // đóng bằng nút X (chưa đăng xuất) = thoát chương trình
        }
    }
}
