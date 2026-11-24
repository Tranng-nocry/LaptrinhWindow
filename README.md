# BaiTapLon - Hệ thống Quản lý bán hàng (WinForms .NET 10 + SQL Server)

Xem hướng dẫn chạy chi tiết, biểu đồ use case và kịch bản demo trong file `HuongDan.md` (nằm ở thư mục ngoài project).

## Cấu trúc code
| File | Vai trò |
|---|---|
| `Db.cs` | Kết nối SQL Server theo mô hình DISCONNECTED (SqlDataAdapter/DataSet để đọc, SqlTransaction để ghi) |
| `Models.cs` | Lớp dữ liệu + `Store` (nạp DB -> DataSet -> danh sách; mọi thao tác ghi, kiểm tra quyền theo vai trò) |
| `Session.cs` | Đăng nhập (truy vấn bảng TaiKhoan, so khớp SHA-256) |
| `Form1.cs` | Khung chính: menu xổ dọc theo vai trò, trang Sản phẩm, nhập kho/điều chỉnh |
| `Form1_Home.cs` | Trang chủ riêng cho Admin / Thủ kho / Nhân viên bán hàng |
| `Form1_Warehouse.cs` | Trang Kho hàng + Lịch sử kho (Thủ kho) |
| `Form1_Sales.cs` | Bán hàng, hóa đơn |
| `Form1_Accounts.cs` | Hồ sơ, nhân viên, khách hàng |
| `Form1_Reports.cs` | Báo cáo thống kê |
| `server.txt` | Tên SQL Server (mặc định `.\SQLEXPRESS`) |
