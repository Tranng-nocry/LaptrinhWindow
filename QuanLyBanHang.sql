-- =================================================================================
-- HỆ THỐNG QUẢN LÝ BÁN HÀNG (C# WinForms .NET 10 + SQL Server)
-- Phiên bản 2: 3 vai trò (Admin / Thủ kho / Nhân viên bán hàng), mô hình DISCONNECTED
--
-- CÁCH CHẠY: mở file này trong SSMS (File > Open > File...), kết nối vào server SQLEXPRESS
-- của bạn rồi bấm Execute (F5). Chạy lại bao nhiêu lần cũng được (script tự xóa database cũ và tạo mới).
--
-- TÀI KHOẢN ĐĂNG NHẬP THỬ:
--     admin   / admin123   -> Admin (xem và quản lý tất cả)
--     thukho  / kho123     -> Thủ kho (chỉ thấy số lượng tồn, nhập kho, điều chỉnh tồn)
--     nam     / 123456     -> Nhân viên bán hàng
--     tuan    / 123456     -> Nhân viên bán hàng
--     huy     / 123456     -> Thủ kho (đã khóa, dùng để thử thông báo tài khoản bị khóa)
--
-- Dữ liệu mẫu dùng ngày tính lùi từ hôm nay (GETDATE) nên Trang chủ luôn có số liệu
-- của tháng hiện tại và các tháng trước.
-- =================================================================================

USE master;
GO

IF DB_ID(N'QuanLyBanHang') IS NOT NULL
BEGIN
    ALTER DATABASE QuanLyBanHang SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE QuanLyBanHang;
END
GO

CREATE DATABASE QuanLyBanHang;
GO

USE QuanLyBanHang;
GO

-- =================================================================================
-- I. NHÂN VIÊN, TÀI KHOẢN, KHÁCH HÀNG
-- =================================================================================

CREATE TABLE NhanVien (
    MaNV            VARCHAR(20)     NOT NULL PRIMARY KEY,
    HoTen           NVARCHAR(100)   NOT NULL,
    ChucVu          NVARCHAR(50)    NOT NULL DEFAULT N'Nhân viên bán hàng', -- Admin, Thủ kho, Nhân viên bán hàng
    SoDienThoai     VARCHAR(15)     NOT NULL UNIQUE,
    DiaChi          NVARCHAR(255)   NULL,
    Email           VARCHAR(100)    NULL,
    NgayVaoLam      DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    TrangThai       NVARCHAR(30)    NOT NULL DEFAULT N'Đang làm',
    CONSTRAINT CK_NhanVien_TrangThai CHECK (TrangThai IN (N'Đang làm', N'Nghỉ việc'))
);
GO

-- Mật khẩu lưu dạng SHA-256 (chuỗi hex 64 ký tự), tính trên chuỗi NVARCHAR (Unicode)
CREATE TABLE TaiKhoan (
    MaTK            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    MaNV            VARCHAR(20)     NOT NULL UNIQUE,
    TenDangNhap     VARCHAR(50)     NOT NULL UNIQUE,
    MatKhauHash     VARCHAR(64)     NOT NULL,
    VaiTro          NVARCHAR(30)    NOT NULL DEFAULT N'Nhân viên bán hàng',
    TrangThaiTK     NVARCHAR(30)    NOT NULL DEFAULT N'Hoạt động',
    NgayTao         DATETIME        NOT NULL DEFAULT GETDATE(),
    LanDangNhapCuoi DATETIME        NULL,
    CONSTRAINT FK_TaiKhoan_NhanVien FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV) ON DELETE CASCADE,
    CONSTRAINT CK_TaiKhoan_VaiTro CHECK (VaiTro IN (N'Admin', N'Thủ kho', N'Nhân viên bán hàng')),
    CONSTRAINT CK_TaiKhoan_TrangThai CHECK (TrangThaiTK IN (N'Hoạt động', N'Đã khóa'))
);
GO

CREATE TABLE KhachHang (
    MaKH            VARCHAR(20)     NOT NULL PRIMARY KEY,
    HoTen           NVARCHAR(100)   NOT NULL,
    SoDienThoai     VARCHAR(15)     NOT NULL UNIQUE,
    Email           VARCHAR(100)    NULL,
    DiaChi          NVARCHAR(255)   NULL,
    NgayTao         DATETIME        NOT NULL DEFAULT GETDATE(),
    TrangThai       NVARCHAR(30)    NOT NULL DEFAULT N'Hoạt động',
    CONSTRAINT CK_KhachHang_TrangThai CHECK (TrangThai IN (N'Hoạt động', N'Ngừng hoạt động'))
);
GO

-- =================================================================================
-- II. DANH MỤC, SẢN PHẨM, KHO, NHẬP/XUẤT, KIỂM KÊ
-- =================================================================================

CREATE TABLE DanhMuc (
    MaDanhMuc       VARCHAR(20)     NOT NULL PRIMARY KEY,
    TenDanhMuc      NVARCHAR(100)   NOT NULL UNIQUE,
    MoTa            NVARCHAR(255)   NULL,
    TrangThai       NVARCHAR(30)    NOT NULL DEFAULT N'Hoạt động',
    CONSTRAINT CK_DanhMuc_TrangThai CHECK (TrangThai IN (N'Hoạt động', N'Ngừng hoạt động'))
);
GO

CREATE TABLE SanPham (
    MaSP            VARCHAR(20)     NOT NULL PRIMARY KEY,
    TenSP           NVARCHAR(150)   NOT NULL,
    MaDanhMuc       VARCHAR(20)     NOT NULL,
    DonViTinh       NVARCHAR(30)    NOT NULL DEFAULT N'Chiếc',
    GiaNhap         DECIMAL(18, 0)  NOT NULL DEFAULT 0 CHECK (GiaNhap >= 0),
    GiaBan          DECIMAL(18, 0)  NOT NULL DEFAULT 0 CHECK (GiaBan >= 0),
    TrangThaiKD     NVARCHAR(30)    NOT NULL DEFAULT N'Đang kinh doanh',
    CONSTRAINT FK_SanPham_DanhMuc FOREIGN KEY (MaDanhMuc) REFERENCES DanhMuc(MaDanhMuc),
    CONSTRAINT CK_SanPham_TrangThaiKD CHECK (TrangThaiKD IN (N'Đang kinh doanh', N'Ngừng kinh doanh'))
);
GO

CREATE TABLE Kho (
    MaSP            VARCHAR(20)     NOT NULL PRIMARY KEY,
    SoLuongTon      INT             NOT NULL DEFAULT 0 CHECK (SoLuongTon >= 0),
    MucTonToiThieu  INT             NOT NULL DEFAULT 5 CHECK (MucTonToiThieu >= 0),
    NgayCapNhat     DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Kho_SanPham FOREIGN KEY (MaSP) REFERENCES SanPham(MaSP) ON DELETE CASCADE
);
GO

CREATE TABLE PhieuNhap (
    MaPhieuNhap     VARCHAR(20)     NOT NULL PRIMARY KEY,
    NgayNhap        DATETIME        NOT NULL DEFAULT GETDATE(),
    NhaCungCap      NVARCHAR(150)   NULL,
    MaNVLap         VARCHAR(20)     NOT NULL,
    TongTienNhap    DECIMAL(18, 0)  NOT NULL DEFAULT 0 CHECK (TongTienNhap >= 0),
    TrangThai       NVARCHAR(30)    NOT NULL DEFAULT N'Đã xác nhận',
    GhiChu          NVARCHAR(255)   NULL,
    CONSTRAINT FK_PhieuNhap_NhanVien FOREIGN KEY (MaNVLap) REFERENCES NhanVien(MaNV),
    CONSTRAINT CK_PhieuNhap_TrangThai CHECK (TrangThai IN (N'Chờ xác nhận', N'Đã xác nhận', N'Đã hủy'))
);
GO

CREATE TABLE ChiTietPhieuNhap (
    MaPhieuNhap     VARCHAR(20)     NOT NULL,
    MaSP            VARCHAR(20)     NOT NULL,
    SoLuongNhap     INT             NOT NULL CHECK (SoLuongNhap > 0),
    GiaNhap         DECIMAL(18, 0)  NOT NULL CHECK (GiaNhap >= 0),
    ThanhTien       AS (SoLuongNhap * GiaNhap) PERSISTED,
    PRIMARY KEY (MaPhieuNhap, MaSP),
    CONSTRAINT FK_CTPN_PhieuNhap FOREIGN KEY (MaPhieuNhap) REFERENCES PhieuNhap(MaPhieuNhap) ON DELETE CASCADE,
    CONSTRAINT FK_CTPN_SanPham FOREIGN KEY (MaSP) REFERENCES SanPham(MaSP)
);
GO

-- PhieuXuat / ChiTietPhieuXuat: giữ lại theo thiết kế ban đầu (xuất kho không qua bán hàng). Form hiện chưa dùng.
CREATE TABLE PhieuXuat (
    MaPhieuXuat     VARCHAR(20)     NOT NULL PRIMARY KEY,
    NgayXuat        DATETIME        NOT NULL DEFAULT GETDATE(),
    NguoiNhan       NVARCHAR(100)   NULL,
    MaNVLap         VARCHAR(20)     NOT NULL,
    LyDoXuat        NVARCHAR(150)   NOT NULL DEFAULT N'Xuất bán hàng',
    TrangThai       NVARCHAR(30)    NOT NULL DEFAULT N'Đã xác nhận',
    GhiChu          NVARCHAR(255)   NULL,
    CONSTRAINT FK_PhieuXuat_NhanVien FOREIGN KEY (MaNVLap) REFERENCES NhanVien(MaNV),
    CONSTRAINT CK_PhieuXuat_TrangThai CHECK (TrangThai IN (N'Chờ xác nhận', N'Đã xác nhận', N'Đã hủy'))
);
GO

CREATE TABLE ChiTietPhieuXuat (
    MaPhieuXuat     VARCHAR(20)     NOT NULL,
    MaSP            VARCHAR(20)     NOT NULL,
    SoLuongXuat     INT             NOT NULL CHECK (SoLuongXuat > 0),
    PRIMARY KEY (MaPhieuXuat, MaSP),
    CONSTRAINT FK_CTPX_PhieuXuat FOREIGN KEY (MaPhieuXuat) REFERENCES PhieuXuat(MaPhieuXuat) ON DELETE CASCADE,
    CONSTRAINT FK_CTPX_SanPham FOREIGN KEY (MaSP) REFERENCES SanPham(MaSP)
);
GO

CREATE TABLE PhieuKiemKe (
    MaPhieuKiemKe   VARCHAR(20)     NOT NULL PRIMARY KEY,
    NgayKiemKe      DATETIME        NOT NULL DEFAULT GETDATE(),
    MaNVKiemKe      VARCHAR(20)     NOT NULL,
    GhiChu          NVARCHAR(255)   NULL,
    TrangThai       NVARCHAR(30)    NOT NULL DEFAULT N'Đã cân bằng',
    CONSTRAINT FK_PhieuKiemKe_NhanVien FOREIGN KEY (MaNVKiemKe) REFERENCES NhanVien(MaNV),
    CONSTRAINT CK_PhieuKiemKe_TrangThai CHECK (TrangThai IN (N'Nháp', N'Đã cân bằng', N'Đã hủy'))
);
GO

CREATE TABLE ChiTietKiemKe (
    MaPhieuKiemKe   VARCHAR(20)     NOT NULL,
    MaSP            VARCHAR(20)     NOT NULL,
    TonHeThong      INT             NOT NULL CHECK (TonHeThong >= 0),
    TonThucTe       INT             NOT NULL CHECK (TonThucTe >= 0),
    ChenhLech       AS (TonThucTe - TonHeThong) PERSISTED,
    LyDoDieuChinh   NVARCHAR(255)   NULL,
    PRIMARY KEY (MaPhieuKiemKe, MaSP),
    CONSTRAINT FK_CTKK_PhieuKiemKe FOREIGN KEY (MaPhieuKiemKe) REFERENCES PhieuKiemKe(MaPhieuKiemKe) ON DELETE CASCADE,
    CONSTRAINT FK_CTKK_SanPham FOREIGN KEY (MaSP) REFERENCES SanPham(MaSP)
);
GO

-- =================================================================================
-- III. ĐƠN HÀNG, HÓA ĐƠN, THANH TOÁN, LỊCH SỬ GIAO DỊCH KHO
-- =================================================================================

-- DonHang / ChiTietDonHang: giữ lại theo thiết kế ban đầu. Form hiện lập thẳng HoaDon nên chưa dùng.
CREATE TABLE DonHang (
    MaDonHang       VARCHAR(20)     NOT NULL PRIMARY KEY,
    MaKH            VARCHAR(20)     NULL,
    MaNV            VARCHAR(20)     NOT NULL,
    NgayTao         DATETIME        NOT NULL DEFAULT GETDATE(),
    TongTienHang    DECIMAL(18, 0)  NOT NULL DEFAULT 0 CHECK (TongTienHang >= 0),
    GiamGia         DECIMAL(18, 0)  NOT NULL DEFAULT 0 CHECK (GiamGia >= 0),
    TrangThai       NVARCHAR(30)    NOT NULL DEFAULT N'Hoàn tất',
    CONSTRAINT FK_DonHang_KhachHang FOREIGN KEY (MaKH) REFERENCES KhachHang(MaKH),
    CONSTRAINT FK_DonHang_NhanVien FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV),
    CONSTRAINT CK_DonHang_TrangThai CHECK (TrangThai IN (N'Chờ xử lý', N'Hoàn tất', N'Đã hủy'))
);
GO

CREATE TABLE ChiTietDonHang (
    MaDonHang       VARCHAR(20)     NOT NULL,
    MaSP            VARCHAR(20)     NOT NULL,
    SoLuong         INT             NOT NULL CHECK (SoLuong > 0),
    DonGia          DECIMAL(18, 0)  NOT NULL CHECK (DonGia >= 0),
    ThanhTien       AS (SoLuong * DonGia) PERSISTED,
    PRIMARY KEY (MaDonHang, MaSP),
    CONSTRAINT FK_CTDH_DonHang FOREIGN KEY (MaDonHang) REFERENCES DonHang(MaDonHang) ON DELETE CASCADE,
    CONSTRAINT FK_CTDH_SanPham FOREIGN KEY (MaSP) REFERENCES SanPham(MaSP)
);
GO

CREATE TABLE HoaDon (
    MaHD            VARCHAR(20)     NOT NULL PRIMARY KEY,
    MaDonHang       VARCHAR(20)     NULL,
    MaKH            VARCHAR(20)     NULL,          -- NULL = khách lẻ
    MaNV            VARCHAR(20)     NOT NULL,      -- Nhân viên bán hàng lập hóa đơn
    NgayLap         DATETIME        NOT NULL DEFAULT GETDATE(),
    TongTien        DECIMAL(18, 0)  NOT NULL DEFAULT 0 CHECK (TongTien >= 0),
    GiamGia         DECIMAL(18, 0)  NOT NULL DEFAULT 0 CHECK (GiamGia >= 0),
    ThanhToan       AS (TongTien - GiamGia) PERSISTED,
    TrangThai       NVARCHAR(30)    NOT NULL DEFAULT N'Đã thanh toán',
    LyDoHuy         NVARCHAR(255)   NULL,
    MaNVHuy         VARCHAR(20)     NULL,
    NgayHuy         DATETIME        NULL,
    CONSTRAINT FK_HoaDon_DonHang FOREIGN KEY (MaDonHang) REFERENCES DonHang(MaDonHang),
    CONSTRAINT FK_HoaDon_KhachHang FOREIGN KEY (MaKH) REFERENCES KhachHang(MaKH),
    CONSTRAINT FK_HoaDon_NhanVien FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV),
    CONSTRAINT FK_HoaDon_NVHuy FOREIGN KEY (MaNVHuy) REFERENCES NhanVien(MaNV),
    CONSTRAINT CK_HoaDon_TrangThai CHECK (TrangThai IN (N'Đã thanh toán', N'Chờ thanh toán', N'Đã hủy'))
);
GO

CREATE TABLE ChiTietHoaDon (
    MaHD            VARCHAR(20)     NOT NULL,
    MaSP            VARCHAR(20)     NOT NULL,
    SoLuong         INT             NOT NULL CHECK (SoLuong > 0),
    DonGia          DECIMAL(18, 0)  NOT NULL CHECK (DonGia >= 0),
    ThanhTien       AS (SoLuong * DonGia) PERSISTED,
    PRIMARY KEY (MaHD, MaSP),
    CONSTRAINT FK_CTHD_HoaDon FOREIGN KEY (MaHD) REFERENCES HoaDon(MaHD) ON DELETE CASCADE,
    CONSTRAINT FK_CTHD_SanPham FOREIGN KEY (MaSP) REFERENCES SanPham(MaSP)
);
GO

CREATE TABLE ThanhToan (
    MaThanhToan     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    MaHD            VARCHAR(20)     NOT NULL,
    NgayThanhToan   DATETIME        NOT NULL DEFAULT GETDATE(),
    PhuongThuc      NVARCHAR(50)    NOT NULL DEFAULT N'Tiền mặt', -- Tiền mặt, Chuyển khoản, Quẹt thẻ
    SoTien          DECIMAL(18, 0)  NOT NULL CHECK (SoTien >= 0),
    TrangThai       NVARCHAR(30)    NOT NULL DEFAULT N'Thành công',
    GhiChu          NVARCHAR(255)   NULL,
    CONSTRAINT FK_ThanhToan_HoaDon FOREIGN KEY (MaHD) REFERENCES HoaDon(MaHD) ON DELETE CASCADE
);
GO

-- Lưu vết mọi biến động kho. LoaiGiaoDich: Nhập kho / Bán hàng / Hoàn kho / Điều chỉnh
CREATE TABLE LichSuGiaoDich (
    MaGD            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ThoiGian        DATETIME        NOT NULL DEFAULT GETDATE(),
    LoaiGiaoDich    NVARCHAR(50)    NOT NULL,
    MaThamChieu     VARCHAR(20)     NOT NULL,      -- Mã HĐ, Mã phiếu nhập, Mã phiếu kiểm kê
    MaSP            VARCHAR(20)     NOT NULL,
    SoLuongBienDong INT             NOT NULL,      -- Dương (+) khi tăng kho, âm (-) khi giảm kho
    TonSauGiaoDich  INT             NOT NULL,
    MaNVThucHien    VARCHAR(20)     NOT NULL,
    GhiChu          NVARCHAR(255)   NULL,
    CONSTRAINT FK_LSGD_SanPham FOREIGN KEY (MaSP) REFERENCES SanPham(MaSP),
    CONSTRAINT FK_LSGD_NhanVien FOREIGN KEY (MaNVThucHien) REFERENCES NhanVien(MaNV)
);
GO

-- =================================================================================
-- IV. VIEW THỐNG KÊ (phục vụ Trang chủ và báo cáo, có thể SELECT trực tiếp để kiểm tra)
-- =================================================================================

CREATE VIEW vw_HangCanNhap AS
SELECT sp.MaSP, sp.TenSP, dm.TenDanhMuc AS DanhMuc, k.SoLuongTon, k.MucTonToiThieu,
       CASE WHEN k.SoLuongTon = 0 THEN N'Hết hàng' ELSE N'Sắp hết' END AS TrangThai,
       (k.MucTonToiThieu * 3 - k.SoLuongTon) AS GoiYNhap
FROM SanPham sp
INNER JOIN DanhMuc dm ON dm.MaDanhMuc = sp.MaDanhMuc
INNER JOIN Kho k ON k.MaSP = sp.MaSP
WHERE sp.TrangThaiKD = N'Đang kinh doanh' AND k.SoLuongTon <= k.MucTonToiThieu;
GO

CREATE VIEW vw_DoanhThuTheoThang AS
SELECT YEAR(NgayLap) AS Nam, MONTH(NgayLap) AS Thang, COUNT(*) AS SoHoaDon, SUM(ThanhToan) AS DoanhThu
FROM HoaDon
WHERE TrangThai = N'Đã thanh toán'
GROUP BY YEAR(NgayLap), MONTH(NgayLap);
GO

CREATE VIEW vw_TienNhapTheoThang AS
SELECT YEAR(NgayNhap) AS Nam, MONTH(NgayNhap) AS Thang, COUNT(*) AS SoPhieuNhap, SUM(TongTienNhap) AS TongTienNhap
FROM PhieuNhap
WHERE TrangThai = N'Đã xác nhận'
GROUP BY YEAR(NgayNhap), MONTH(NgayNhap);
GO

-- =================================================================================
-- V. STORED PROCEDURE (đăng nhập, đổi mật khẩu) - ứng dụng C# dùng truy vấn trực tiếp,
--    hai thủ tục này để bạn demo/kiểm tra trong SSMS.
-- =================================================================================

CREATE PROCEDURE sp_DangNhap
    @TenDangNhap VARCHAR(50),
    @MatKhau     NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hash VARCHAR(64) = CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @MatKhau), 2);

    SELECT nv.MaNV, nv.HoTen, tk.TenDangNhap, tk.VaiTro
    FROM TaiKhoan tk
    INNER JOIN NhanVien nv ON nv.MaNV = tk.MaNV
    WHERE tk.TenDangNhap = @TenDangNhap
      AND tk.MatKhauHash = @Hash
      AND tk.TrangThaiTK = N'Hoạt động'
      AND nv.TrangThai = N'Đang làm';
END;
GO

CREATE PROCEDURE sp_DoiMatKhau
    @TenDangNhap VARCHAR(50),
    @MatKhauCu   NVARCHAR(100),
    @MatKhauMoi  NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @OldHash VARCHAR(64) = CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @MatKhauCu), 2);
    DECLARE @NewHash VARCHAR(64) = CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @MatKhauMoi), 2);

    IF NOT EXISTS (SELECT 1 FROM TaiKhoan WHERE TenDangNhap = @TenDangNhap AND MatKhauHash = @OldHash)
    BEGIN
        RAISERROR(N'Mật khẩu hiện tại không chính xác.', 16, 1);
        RETURN;
    END

    UPDATE TaiKhoan SET MatKhauHash = @NewHash WHERE TenDangNhap = @TenDangNhap;
END;
GO

-- =================================================================================
-- VI. DỮ LIỆU MẪU (ngày tính lùi từ hôm nay)
-- =================================================================================

-- Nhân viên + tài khoản
INSERT INTO NhanVien (MaNV, HoTen, ChucVu, SoDienThoai, DiaChi, Email, TrangThai) VALUES
('NV001', N'Nguyễn Văn Nam',  N'Nhân viên bán hàng', '0901111222', N'Cầu Giấy, Hà Nội',     'nam.nv@shop.vn',    N'Đang làm'),
('NV002', N'Trần Thị Lan',    N'Admin',              '0903333444', N'Đống Đa, Hà Nội',      'lan.tt@shop.vn',    N'Đang làm'),
('NV003', N'Lê Minh Tuấn',    N'Nhân viên bán hàng', '0905555666', N'Thanh Xuân, Hà Nội',   'tuan.lm@shop.vn',   N'Đang làm'),
('NV004', N'Phạm Thu Trang',  N'Thủ kho',            '0907777888', N'Hai Bà Trưng, Hà Nội', 'trang.pt@shop.vn',  N'Đang làm'),
('NV005', N'Ngô Quang Huy',   N'Thủ kho',            '0909999000', N'Hoàng Mai, Hà Nội',    'huy.nq@shop.vn',    N'Nghỉ việc');

INSERT INTO TaiKhoan (MaNV, TenDangNhap, MatKhauHash, VaiTro, TrangThaiTK) VALUES
('NV001', 'nam',    CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'123456'),   2), N'Nhân viên bán hàng', N'Hoạt động'),
('NV002', 'admin',  CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'admin123'), 2), N'Admin',              N'Hoạt động'),
('NV003', 'tuan',   CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'123456'),   2), N'Nhân viên bán hàng', N'Hoạt động'),
('NV004', 'thukho', CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'kho123'),   2), N'Thủ kho',            N'Hoạt động'),
('NV005', 'huy',    CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'123456'),   2), N'Thủ kho',            N'Đã khóa');

-- Khách hàng
INSERT INTO KhachHang (MaKH, HoTen, SoDienThoai, Email, DiaChi, TrangThai) VALUES
('KH001', N'Nguyễn Minh Anh', '0901234567', 'minhanh@email.com',   N'Ba Đình, Hà Nội',     N'Hoạt động'),
('KH002', N'Trần Quốc Bảo',   '0912345678', 'quocbao@email.com',   N'Cầu Giấy, Hà Nội',    N'Hoạt động'),
('KH003', N'Lê Thu Hà',       '0987654321', 'thuha@email.com',     N'Đống Đa, Hà Nội',     N'Hoạt động'),
('KH004', N'Phạm Hoàng Long', '0934567890', 'hoanglong@email.com', N'Hoàn Kiếm, Hà Nội',   N'Hoạt động'),
('KH005', N'Đỗ Ngọc Mai',     '0966778899', 'ngocmai@email.com',   N'Tây Hồ, Hà Nội',      N'Hoạt động'),
('KH006', N'Vũ Hải Đăng',     '0977112233', 'haidang@email.com',   N'Thanh Xuân, Hà Nội',  N'Hoạt động'),
('KH007', N'Bùi Thanh Tâm',   '0905998877', 'thanhtam@email.com',  N'Long Biên, Hà Nội',   N'Hoạt động'),
('KH008', N'Hoàng Gia Huy',   '0918223344', 'giahuy@email.com',    N'Nam Từ Liêm, Hà Nội', N'Hoạt động');

-- Danh mục, sản phẩm, tồn kho hiện tại (mức tồn tối thiểu = 5)
INSERT INTO DanhMuc (MaDanhMuc, TenDanhMuc, MoTa) VALUES
('DM01', N'Phụ kiện',          N'Bàn phím, chuột, webcam, bàn di, thiết bị mạng'),
('DM02', N'Thiết bị âm thanh', N'Tai nghe, loa Bluetooth'),
('DM03', N'Màn hình',          N'Màn hình phẳng và màn hình cong các cỡ'),
('DM04', N'Lưu trữ',           N'USB, ổ cứng SSD/HDD'),
('DM05', N'Máy tính',          N'Laptop văn phòng và máy tính để bàn');

INSERT INTO SanPham (MaSP, TenSP, MaDanhMuc, DonViTinh, GiaNhap, GiaBan) VALUES
('SP001', N'Bàn phím cơ K500',      'DM01', N'Chiếc', 600000,   850000),
('SP002', N'Chuột không dây M220',  'DM01', N'Chiếc', 200000,   320000),
('SP003', N'Tai nghe Gaming H7',    'DM02', N'Chiếc', 480000,   690000),
('SP004', N'Màn hình 24 inch',      'DM03', N'Chiếc', 2200000,  2850000),
('SP005', N'USB 64GB',              'DM04', N'Chiếc', 110000,   180000),
('SP006', N'Laptop văn phòng A15',  'DM05', N'Chiếc', 10500000, 12500000),
('SP007', N'Webcam Full HD',        'DM01', N'Chiếc', 380000,   540000),
('SP008', N'Loa Bluetooth S30',     'DM02', N'Bộ',    300000,   450000),
('SP009', N'Ổ cứng SSD 512GB',      'DM04', N'Chiếc', 950000,   1290000),
('SP010', N'Bàn di chuột cỡ lớn',   'DM01', N'Tấm',   60000,    120000),
('SP011', N'Router Wi-Fi AC1200',   'DM01', N'Chiếc', 520000,   750000),
('SP012', N'Màn hình cong 27 inch', 'DM03', N'Chiếc', 3900000,  4990000);

INSERT INTO Kho (MaSP, SoLuongTon, MucTonToiThieu) VALUES
('SP001', 18, 5), ('SP002', 4, 5),  ('SP003', 0, 5),  ('SP004', 12, 5),
('SP005', 3, 5),  ('SP006', 7, 5),  ('SP007', 9, 5),  ('SP008', 11, 5),
('SP009', 15, 5), ('SP010', 25, 5), ('SP011', 2, 5),  ('SP012', 6, 5);

-- Hóa đơn: mọi ngày tính lùi từ 0h hôm nay
DECLARE @d DATETIME = CAST(CAST(GETDATE() AS DATE) AS DATETIME);

INSERT INTO HoaDon (MaHD, MaKH, MaNV, NgayLap, TongTien, GiamGia, TrangThai, LyDoHuy, MaNVHuy, NgayHuy) VALUES
-- 7 ngày gần nhất
('HD001', 'KH001', 'NV001', DATEADD(HOUR,  9, DATEADD(DAY, -2, @d)), 0, 0, N'Đã thanh toán',  NULL, NULL, NULL),
('HD002', 'KH002', 'NV003', DATEADD(HOUR, 14, DATEADD(DAY, -2, @d)), 0, 0, N'Đã thanh toán',  NULL, NULL, NULL),
('HD003', 'KH003', 'NV001', DATEADD(HOUR, 10, DATEADD(DAY, -3, @d)), 0, 0, N'Chờ thanh toán', NULL, NULL, NULL),
('HD004', 'KH004', 'NV003', DATEADD(HOUR, 15, DATEADD(DAY, -3, @d)), 0, 0, N'Đã thanh toán',  NULL, NULL, NULL),
('HD005', 'KH005', 'NV001', DATEADD(HOUR, 11, DATEADD(DAY, -4, @d)), 0, 0, N'Đã hủy', N'Khách đổi ý, không lấy hàng', 'NV002', DATEADD(HOUR, 16, DATEADD(DAY, -4, @d))),
('HD006', 'KH006', 'NV003', DATEADD(HOUR, 16, DATEADD(DAY, -4, @d)), 0, 0, N'Đã thanh toán',  NULL, NULL, NULL),
('HD007', 'KH007', 'NV001', DATEADD(HOUR,  9, DATEADD(DAY, -5, @d)), 0, 0, N'Chờ thanh toán', NULL, NULL, NULL),
('HD008', 'KH002', 'NV003', DATEADD(HOUR, 13, DATEADD(DAY, -5, @d)), 0, 0, N'Đã thanh toán',  NULL, NULL, NULL),
('HD009', NULL,    'NV001', DATEADD(HOUR, 10, DATEADD(DAY, -6, @d)), 0, 0, N'Đã thanh toán',  NULL, NULL, NULL),
('HD010', 'KH003', 'NV003', DATEADD(HOUR, 17, DATEADD(DAY, -6, @d)), 0, 0, N'Đã thanh toán',  NULL, NULL, NULL),
('HD011', 'KH004', 'NV001', DATEADD(HOUR,  9, DATEADD(DAY, -1, @d)), 0, 0, N'Đã thanh toán',  NULL, NULL, NULL),
('HD012', NULL,    'NV003', DATEADD(HOUR, 15, DATEADD(DAY, -1, @d)), 0, 0, N'Đã thanh toán',  NULL, NULL, NULL),
('HD013', 'KH005', 'NV001', DATEADD(HOUR,  7, @d),                   0, 0, N'Đã thanh toán',  NULL, NULL, NULL),
('HD014', 'KH006', 'NV003', DATEADD(HOUR,  8, @d),                   0, 0, N'Đã thanh toán',  NULL, NULL, NULL),
('HD015', 'KH001', 'NV001', DATEADD(HOUR,  8, @d),                   0, 0, N'Chờ thanh toán', NULL, NULL, NULL),
-- Các tháng trước (để biểu đồ theo tháng có số liệu)
('HD016', 'KH002', 'NV001', DATEADD(HOUR, 10, DATEADD(DAY,  -35, @d)), 0, 0, N'Đã thanh toán', NULL, NULL, NULL),
('HD025', 'KH001', 'NV003', DATEADD(HOUR, 17, DATEADD(DAY,  -33, @d)), 0, 0, N'Đã thanh toán', NULL, NULL, NULL),
('HD017', 'KH007', 'NV003', DATEADD(HOUR, 14, DATEADD(DAY,  -40, @d)), 0, 0, N'Đã thanh toán', NULL, NULL, NULL),
('HD018', 'KH003', 'NV001', DATEADD(HOUR,  9, DATEADD(DAY,  -52, @d)), 0, 0, N'Đã thanh toán', NULL, NULL, NULL),
('HD019', NULL,    'NV003', DATEADD(HOUR, 16, DATEADD(DAY,  -63, @d)), 0, 0, N'Đã thanh toán', NULL, NULL, NULL),
('HD020', 'KH004', 'NV001', DATEADD(HOUR, 11, DATEADD(DAY,  -78, @d)), 0, 0, N'Đã thanh toán', NULL, NULL, NULL),
('HD021', 'KH005', 'NV003', DATEADD(HOUR, 15, DATEADD(DAY,  -95, @d)), 0, 0, N'Đã thanh toán', NULL, NULL, NULL),
('HD022', 'KH008', 'NV001', DATEADD(HOUR, 10, DATEADD(DAY, -110, @d)), 0, 0, N'Đã thanh toán', NULL, NULL, NULL),
('HD023', 'KH006', 'NV003', DATEADD(HOUR, 13, DATEADD(DAY, -128, @d)), 0, 0, N'Đã thanh toán', NULL, NULL, NULL),
('HD024', NULL,    'NV001', DATEADD(HOUR,  9, DATEADD(DAY, -150, @d)), 0, 0, N'Đã thanh toán', NULL, NULL, NULL);

INSERT INTO ChiTietHoaDon (MaHD, MaSP, SoLuong, DonGia)
SELECT v.MaHD, v.MaSP, v.SoLuong, sp.GiaBan
FROM (VALUES
    ('HD001','SP001',1), ('HD001','SP002',1),
    ('HD002','SP004',1),
    ('HD003','SP003',1),
    ('HD004','SP006',1),
    ('HD005','SP002',1), ('HD005','SP005',1),
    ('HD006','SP004',1), ('HD006','SP008',1),
    ('HD007','SP010',8),
    ('HD008','SP009',1), ('HD008','SP007',1),
    ('HD009','SP010',2), ('HD009','SP005',1),
    ('HD010','SP012',1),
    ('HD011','SP001',2),
    ('HD012','SP008',2), ('HD012','SP010',1),
    ('HD013','SP009',1),
    ('HD014','SP007',1), ('HD014','SP010',1),
    ('HD015','SP011',1),
    ('HD016','SP006',1), ('HD016','SP001',1),
    ('HD017','SP004',2),
    ('HD018','SP009',3), ('HD018','SP010',4),
    ('HD019','SP012',1), ('HD019','SP007',2),
    ('HD020','SP006',2),
    ('HD021','SP001',3), ('HD021','SP002',2),
    ('HD022','SP004',1), ('HD022','SP008',3),
    ('HD023','SP012',2),
    ('HD024','SP009',2), ('HD024','SP005',4),
    ('HD025','SP010',5), ('HD025','SP002',1)
) AS v(MaHD, MaSP, SoLuong)
INNER JOIN SanPham sp ON sp.MaSP = v.MaSP;

-- Tổng tiền hóa đơn = tổng chi tiết
UPDATE hd
SET TongTien = ISNULL((SELECT SUM(ct.ThanhTien) FROM ChiTietHoaDon ct WHERE ct.MaHD = hd.MaHD), 0)
FROM HoaDon hd;

-- Thanh toán cho các hóa đơn đã thanh toán (xoay vòng 3 hình thức)
INSERT INTO ThanhToan (MaHD, NgayThanhToan, PhuongThuc, SoTien, TrangThai)
SELECT hd.MaHD, DATEADD(MINUTE, 2, hd.NgayLap),
       CASE CAST(SUBSTRING(hd.MaHD, 3, 10) AS INT) % 3
            WHEN 0 THEN N'Chuyển khoản' WHEN 1 THEN N'Tiền mặt' ELSE N'Quẹt thẻ' END,
       hd.ThanhToan, N'Thành công'
FROM HoaDon hd
WHERE hd.TrangThai = N'Đã thanh toán';

-- Phiếu nhập (đã xác nhận): giá nhập lấy theo bảng SanPham
INSERT INTO PhieuNhap (MaPhieuNhap, NgayNhap, NhaCungCap, MaNVLap, TongTienNhap, TrangThai, GhiChu) VALUES
('PN001', DATEADD(HOUR, 8, DATEADD(DAY, -45, @d)), N'Công ty TNHH Phân phối FPT', 'NV002', 0, N'Đã xác nhận', N'Nhập hàng định kỳ'),
('PN002', DATEADD(HOUR, 9, DATEADD(DAY, -28, @d)), N'Nhà phân phối GearVN',       'NV004', 0, N'Đã xác nhận', N'Nhập laptop và SSD'),
('PN003', DATEADD(HOUR, 8, DATEADD(DAY, -16, @d)), N'Công ty TNHH Phân phối FPT', 'NV004', 0, N'Đã xác nhận', N'Bổ sung phụ kiện'),
('PN004', DATEADD(HOUR, 9, DATEADD(DAY,  -9, @d)), N'Nhà phân phối GearVN',       'NV002', 0, N'Đã xác nhận', N'Nhập loa và màn hình cong'),
('PN005', DATEADD(HOUR, 8, DATEADD(DAY,  -3, @d)), N'Công ty TNHH Phân phối FPT', 'NV004', 0, N'Đã xác nhận', N'Nhập bàn phím, webcam');

INSERT INTO ChiTietPhieuNhap (MaPhieuNhap, MaSP, SoLuongNhap, GiaNhap)
SELECT v.MaPN, v.MaSP, v.SoLuong, sp.GiaNhap
FROM (VALUES
    ('PN001','SP001',10), ('PN001','SP004',4),
    ('PN002','SP006',3),  ('PN002','SP009',5),
    ('PN003','SP002',3),  ('PN003','SP005',5), ('PN003','SP010',20),
    ('PN004','SP008',6),  ('PN004','SP012',4),
    ('PN005','SP001',5),  ('PN005','SP007',5)
) AS v(MaPN, MaSP, SoLuong)
INNER JOIN SanPham sp ON sp.MaSP = v.MaSP;

UPDATE pn
SET TongTienNhap = ISNULL((SELECT SUM(ct.ThanhTien) FROM ChiTietPhieuNhap ct WHERE ct.MaPhieuNhap = pn.MaPhieuNhap), 0)
FROM PhieuNhap pn;

-- Một phiếu kiểm kê mẫu: bàn di chuột hỏng 1 tấm
INSERT INTO PhieuKiemKe (MaPhieuKiemKe, NgayKiemKe, MaNVKiemKe, GhiChu, TrangThai) VALUES
('KK001', DATEADD(HOUR, 17, DATEADD(DAY, -8, @d)), 'NV004', N'Hỏng 1 tấm trưng bày', N'Đã cân bằng');
INSERT INTO ChiTietKiemKe (MaPhieuKiemKe, MaSP, TonHeThong, TonThucTe, LyDoDieuChinh) VALUES
('KK001', 'SP010', 26, 25, N'Hỏng 1 tấm trưng bày');

-- Lịch sử giao dịch kho, dựng từ các chứng từ ở trên
INSERT INTO LichSuGiaoDich (ThoiGian, LoaiGiaoDich, MaThamChieu, MaSP, SoLuongBienDong, TonSauGiaoDich, MaNVThucHien, GhiChu)
SELECT pn.NgayNhap, N'Nhập kho', pn.MaPhieuNhap, ct.MaSP, ct.SoLuongNhap, 0, pn.MaNVLap, N'Nhập kho'
FROM PhieuNhap pn INNER JOIN ChiTietPhieuNhap ct ON ct.MaPhieuNhap = pn.MaPhieuNhap
WHERE pn.TrangThai = N'Đã xác nhận';

INSERT INTO LichSuGiaoDich (ThoiGian, LoaiGiaoDich, MaThamChieu, MaSP, SoLuongBienDong, TonSauGiaoDich, MaNVThucHien, GhiChu)
SELECT hd.NgayLap, N'Bán hàng', hd.MaHD, ct.MaSP, -ct.SoLuong, 0, hd.MaNV, hd.MaHD
FROM HoaDon hd INNER JOIN ChiTietHoaDon ct ON ct.MaHD = hd.MaHD;

INSERT INTO LichSuGiaoDich (ThoiGian, LoaiGiaoDich, MaThamChieu, MaSP, SoLuongBienDong, TonSauGiaoDich, MaNVThucHien, GhiChu)
SELECT hd.NgayHuy, N'Hoàn kho', hd.MaHD, ct.MaSP, ct.SoLuong, 0, hd.MaNVHuy, N'Hủy ' + hd.MaHD
FROM HoaDon hd INNER JOIN ChiTietHoaDon ct ON ct.MaHD = hd.MaHD
WHERE hd.TrangThai = N'Đã hủy';

INSERT INTO LichSuGiaoDich (ThoiGian, LoaiGiaoDich, MaThamChieu, MaSP, SoLuongBienDong, TonSauGiaoDich, MaNVThucHien, GhiChu)
SELECT kk.NgayKiemKe, N'Điều chỉnh', kk.MaPhieuKiemKe, ct.MaSP, ct.ChenhLech, 0, kk.MaNVKiemKe, ct.LyDoDieuChinh
FROM PhieuKiemKe kk INNER JOIN ChiTietKiemKe ct ON ct.MaPhieuKiemKe = kk.MaPhieuKiemKe;

-- Nhập kho ban đầu = tồn hiện tại - tổng biến động đã ghi, để Tồn cuối = Tồn đầu + Nhập - Xuất + Điều chỉnh luôn khớp
INSERT INTO LichSuGiaoDich (ThoiGian, LoaiGiaoDich, MaThamChieu, MaSP, SoLuongBienDong, TonSauGiaoDich, MaNVThucHien, GhiChu)
SELECT DATEADD(DAY, -200, GETDATE()), N'Nhập kho', 'TON-DAU', k.MaSP, k.SoLuongTon - ISNULL(n.Net, 0), 0, 'NV002', N'Nhập kho ban đầu'
FROM Kho k
LEFT JOIN (SELECT MaSP, SUM(SoLuongBienDong) AS Net FROM LichSuGiaoDich GROUP BY MaSP) n ON n.MaSP = k.MaSP
WHERE k.SoLuongTon - ISNULL(n.Net, 0) > 0;

-- Tính cột "Tồn sau giao dịch" theo thứ tự thời gian
UPDATE l
SET TonSauGiaoDich = r.TonLuyKe
FROM LichSuGiaoDich l
INNER JOIN (
    SELECT MaGD, SUM(SoLuongBienDong) OVER (PARTITION BY MaSP ORDER BY ThoiGian, MaGD ROWS UNBOUNDED PRECEDING) AS TonLuyKe
    FROM LichSuGiaoDich
) r ON r.MaGD = l.MaGD;
GO

-- =================================================================================
-- VII. KIỂM TRA NHANH (bôi đen từng câu rồi F5 nếu muốn xem)
-- =================================================================================
-- SELECT * FROM vw_HangCanNhap;
-- SELECT * FROM vw_DoanhThuTheoThang ORDER BY Nam, Thang;
-- SELECT * FROM vw_TienNhapTheoThang ORDER BY Nam, Thang;
-- EXEC sp_DangNhap 'admin', N'admin123';
PRINT N'Đã tạo xong database QuanLyBanHang.';
GO
