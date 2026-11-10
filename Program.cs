namespace BaiTapLon;


internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();


        // Vòng lặp: Đăng nhập -> Màn hình chính -> (Đăng xuất) -> quay lại Đăng nhập
        while (true)
        {
            using (var login = new LoginForm())
            {
                if (login.ShowDialog() != DialogResult.OK)
                    return;                       // tắt cửa sổ đăng nhập = thoát chương trình
            }


            Application.Run(new Form1());


            if (Session.Current != null)
                return;                           // đóng bằng nút X (chưa đăng xuất) = thoát chương trình
        }
    }
}