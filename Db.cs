using System.Data;
using Microsoft.Data.SqlClient;

namespace BaiTapLon;

// =====================================================================
//  Db.cs - Lớp truy cập SQL Server theo mô hình DISCONNECTED (ngắt kết nối).
//
//  - ĐỌC dữ liệu : SqlDataAdapter.Fill() đổ vào DataTable/DataSet. Adapter tự mở kết nối,
//                  đọc xong tự đóng ngay; sau đó ứng dụng làm việc trên dữ liệu trong bộ nhớ.
//  - GHI dữ liệu : mở kết nối -> chạy trong một SqlTransaction -> đóng ngay (không giữ kết nối mở).
//
//  Tên server: mặc định .\SQLEXPRESS. Nếu máy bạn khác (VD DESKTOP-ABC\SQLEXPRESS) thì
//  sửa dòng đầu của file server.txt (nằm cạnh BaiTapLon.csproj), không cần sửa code.
// =====================================================================
internal static class Db
{
    public const string DatabaseName = "QuanLyBanHang_test";
    private const string DefaultServer = @".\SQLEXPRESS";

    public static string ServerName { get; } = ReadServerName();

    public static string ConnectionString { get; } = new SqlConnectionStringBuilder
    {
        DataSource = ServerName,
        InitialCatalog = DatabaseName,
        IntegratedSecurity = true,          // Windows Authentication
        TrustServerCertificate = true,      // SQL Server 2022 hay báo lỗi chứng chỉ nếu thiếu dòng này
        ConnectTimeout = 8
    }.ConnectionString;

    private static string ReadServerName()
    {
        try
        {
            string file = Path.Combine(AppContext.BaseDirectory, "server.txt");
            if (File.Exists(file))
            {
                string line = File.ReadLines(file).FirstOrDefault()?.Trim() ?? "";
                if (line.Length > 0) return line;
            }
        }
        catch { /* dùng giá trị mặc định */ }
        return DefaultServer;
    }

    public static SqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    /// <summary>Kiểm tra kết nối. Trả về null nếu được, ngược lại trả về thông báo lỗi dễ hiểu.</summary>
    public static string? TestConnection()
    {
        try
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            return null;
        }
        catch (Exception ex)
        {
            return $"Không kết nối được SQL Server \"{ServerName}\", cơ sở dữ liệu \"{DatabaseName}\".\n\n" +
                   "Hãy kiểm tra:\n" +
                   "1) Đã chạy file QuanLyBanHang.sql trong SSMS chưa.\n" +
                   "2) Tên server trong file server.txt có đúng với tên hiển thị khi kết nối SSMS không.\n" +
                   "3) Dịch vụ SQL Server (SQLEXPRESS) đang chạy.\n\nChi tiết lỗi: " + ex.Message;
        }
    }

    /// <summary>Đổ kết quả truy vấn vào một DataTable (kết nối tự mở rồi tự đóng).</summary>
    public static DataTable Query(string sql, params SqlParameter[] ps)
    {
        var table = new DataTable();
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        if (ps.Length > 0) cmd.Parameters.AddRange(ps);
        using var adapter = new SqlDataAdapter(cmd);
        adapter.Fill(table);
        return table;
    }

    /// <summary>Đổ kết quả truy vấn vào một bảng có tên trong DataSet.</summary>
    public static void Fill(DataSet ds, string tableName, string sql)
    {
        if (ds.Tables.Contains(tableName)) ds.Tables.Remove(tableName);
        using var conn = new SqlConnection(ConnectionString);
        using var adapter = new SqlDataAdapter(sql, conn);
        adapter.Fill(ds, tableName);
    }

    /// <summary>
    /// Chạy một nhóm lệnh trong MỘT transaction. Hàm work trả về null = thành công (COMMIT),
    /// trả về chuỗi = lỗi nghiệp vụ (ROLLBACK, không ghi gì). Lỗi hệ thống sẽ được ném ra ngoài sau khi ROLLBACK.
    /// </summary>
    public static string? InTransaction(Func<SqlConnection, SqlTransaction, string?> work)
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            string? error = work(conn, tx);
            if (error != null)
            {
                tx.Rollback();
                return error;
            }
            tx.Commit();
            return null;
        }
        catch
        {
            try { tx.Rollback(); } catch { /* giao dịch đã bị hủy */ }
            throw;
        }
    }

    /// <summary>Chạy một lệnh đơn lẻ (mở kết nối, thực thi, đóng ngay). Trả về số dòng bị ảnh hưởng.</summary>
    public static int Execute(string sql, params SqlParameter[] ps)
    {
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        if (ps.Length > 0) cmd.Parameters.AddRange(ps);
        conn.Open();
        return cmd.ExecuteNonQuery();
    }

    // ----- Tiện ích dùng bên trong transaction -----
    public static int Exec(SqlConnection c, SqlTransaction t, string sql, params SqlParameter[] ps)
    {
        using var cmd = new SqlCommand(sql, c, t);
        if (ps.Length > 0) cmd.Parameters.AddRange(ps);
        return cmd.ExecuteNonQuery();
    }

    public static object? Scalar(SqlConnection c, SqlTransaction t, string sql, params SqlParameter[] ps)
    {
        using var cmd = new SqlCommand(sql, c, t);
        if (ps.Length > 0) cmd.Parameters.AddRange(ps);
        object? v = cmd.ExecuteScalar();
        return v == DBNull.Value ? null : v;
    }

    /// <summary>Sinh mã tiếp theo cho bảng (VD HD015 -> HD016), có khóa để 2 máy không trùng mã.</summary>
    public static string NextCode(SqlConnection c, SqlTransaction t, string table, string column, string prefix)
    {
        string sql = $"SELECT ISNULL(MAX(TRY_CAST(SUBSTRING({column}, {prefix.Length + 1}, 20) AS INT)), 0) + 1 " +
                     $"FROM {table} WITH (UPDLOCK, HOLDLOCK) WHERE {column} LIKE '{prefix}%'";
        int next = Convert.ToInt32(Scalar(c, t, sql) ?? 1);
        return prefix + next.ToString("D3");
    }
}
