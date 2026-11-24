using static BaiTapLon.Ui;

namespace BaiTapLon;

// Thủ kho: kho hàng (chỉ số lượng, không có giá) + lịch sử nhập/xuất kho.
public partial class Form1
{
    // =====================================================================
    //  KHO HÀNG (Thủ kho): tồn kho, mức tối thiểu, gợi ý nhập; nhập kho và điều chỉnh sau kiểm kê.
    //  Cố ý KHÔNG hiện giá nhập, giá bán, giá trị tồn kho để không lộ thông tin kinh doanh.
    // =====================================================================
    private Control BuildWarehousePage()
    {
        var categories = new[] { "Tất cả danh mục" }
            .Concat(Store.Products.Select(p => p.Category).Distinct().OrderBy(c => c)).ToArray();
        var statuses = new[] { "Tất cả trạng thái", "Còn hàng", "Sắp hết", "Hết hàng", "Ngừng kinh doanh" };

        var page = new DataListPage(
            "Tìm kiếm theo tên sản phẩm, mã hàng, danh mục...",
            "sản phẩm",
            new[] { categories, statuses },
            new[]
            {
                new ColumnDef("Mã SP", 0.9f),
                new ColumnDef("Tên sản phẩm", 2.2f, ColKind.Link),
                new ColumnDef("Danh mục", 1.5f, ColKind.Tag),
                new ColumnDef("Tồn kho", 0.9f),
                new ColumnDef("Tối thiểu", 0.9f),
                new ColumnDef("Gợi ý nhập", 1.0f),
                new ColumnDef("Trạng thái", 1.5f, ColKind.Status),
                new ColumnDef("Thao tác", 1.2f, ColKind.Action)
            });

        page.Query = (term, f) =>
        {
            string cat = f.Count > 0 ? f[0] : "";
            string st = f.Count > 1 ? f[1] : "";
            return Store.Products
                .Where(p =>
                    (p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     p.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     p.Category.Contains(term, StringComparison.OrdinalIgnoreCase)) &&
                    (IsAll(cat) || p.Category == cat) &&
                    (IsAll(st) || p.Status == st))
                .OrderByDescending(p => p.NeedsRestock)      // hàng cần nhập lên đầu
                .ThenBy(p => p.Stock)
                .Select(p => new[]
                {
                    p.Code, p.Name, p.Category, p.Stock.ToString(), p.MinStock.ToString(),
                    p.SuggestedImport > 0 ? "+" + p.SuggestedImport : "-", p.Status, "Thao tác"
                })
                .ToList();
        };

        page.RowAction += row =>
        {
            var p = Store.Products.FirstOrDefault(x => x.Code == row[0]);
            if (p == null) return;
            ShowRowMenu(
                ("Nhập kho", () => ShowStockInDialog(p, () => page.Reload(false))),
                ("Điều chỉnh tồn kho (kiểm kê)", () => ShowAdjustDialog(p, () => page.Reload(false))));
        };

        page.Reload();
        return page;
    }

    // =====================================================================
    //  LỊCH SỬ KHO: mọi biến động nhập, bán, hoàn kho, điều chỉnh (không có số tiền)
    // =====================================================================
    private Control BuildStockHistoryPage()
    {
        var kinds = new[] { "Tất cả loại", StockLog.In, StockLog.Sale, StockLog.Refund, StockLog.Adjust };

        var page = new DataListPage(
            "Tìm kiếm theo mã/tên sản phẩm hoặc ghi chú...",
            "giao dịch kho",
            new[] { kinds },
            new[]
            {
                new ColumnDef("Thời gian", 1.4f),
                new ColumnDef("Sản phẩm", 2.4f, ColKind.Link),
                new ColumnDef("Loại", 1.1f, ColKind.Tag),
                new ColumnDef("Biến động", 0.9f),
                new ColumnDef("Ghi chú", 1.8f),
                new ColumnDef("Người thực hiện", 1.5f)
            });

        page.Query = (term, f) =>
        {
            string kind = f.Count > 0 ? f[0] : "";
            var names = Store.Products.ToDictionary(p => p.Code, p => p.Name);
            var staff = Store.Accounts.ToDictionary(a => a.Code, a => a.FullName);
            string NameOf(string code) => names.TryGetValue(code, out var n) ? n : code;
            string StaffOf(string code) => staff.TryGetValue(code, out var s) ? s : code;

            return Store.StockLogs
                .OrderByDescending(l => l.Time)
                .Where(l =>
                    (IsAll(kind) || l.Kind == kind) &&
                    (term.Length == 0 ||
                     l.ProductCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     NameOf(l.ProductCode).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     l.Note.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Select(l => new[]
                {
                    FormatDate(l.Time), $"{l.ProductCode} - {NameOf(l.ProductCode)}", l.Kind,
                    l.Delta.ToString("+0;-0;0"), l.Note, StaffOf(l.User)
                })
                .ToList();
        };

        page.Reload();
        return page;
    }
}
