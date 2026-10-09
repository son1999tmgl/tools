# BÁO CÁO TỔNG KẾT KHẢO SÁT & KIỂM THỬ DỮ LIỆU CHỨNG KHOÁN & CHỨNG QUYỀN
*(Thực hiện và kiểm thử thực tế trực tiếp trong phiên giao dịch - Cập nhật ngày 09/10/2026)*

---

## 1. Kết Luận Kiến Trúc Nguồn Dữ Liệu Tối Ưu

| Vai trò trong hệ thống | Nguồn đề xuất | Tỷ trọng | Nhiệm vụ chính phụ trách |
| :--- | :--- | :---: | :--- |
| **NGUỒN CHÍNH (Core Realtime)** | **VPS Datafeed** | **85%** | Danh mục toàn thị trường, thông số Chứng quyền (CW), sổ lệnh Level 2 realtime, toàn bộ khớp lệnh trong ngày kèm chiều Mua/Bán chủ động. |
| **NGUỒN PHỤ TRỢ (Chart & Backtest)** | **DNSE Entrade** | **15%** | Nạp chuỗi nến lịch sử quá khứ 1 phút (1m OHLCV) & nến ngày (1D) để tính chỉ báo kỹ thuật (RSI, MACD, MA) và nến Phái sinh VN30F1M. |
| **NGUỒN DỰ PHÒNG VI MÔ (Optional)** | **KBSec Buddy** | Dự phòng | Bắt thứ tự khớp lệnh chi tiết đến mili-giây (nếu cần phân tích vi mô sâu). |

---

## 2. So Sánh Chi Tiết: Điểm Mạnh Của VPS & Điểm Cần DNSE Bù Đắp

### 2.1. Tại sao VPS xứng đáng làm NGUỒN CHÍNH?
1. **Danh mục toàn diện nhất**: Lấy đầy đủ 2.166 mã giao dịch gồm HOSE, HNX, UPCOM.
2. **Thông số Chứng Quyền (CW) chuẩn và duy nhất có sẵn**:
   * `firstTradingDate`: **Ngày bắt đầu giao dịch / niêm yết** (Lấy từ SSI: `20260119` -> 19/01/2026).
   * `CWMaturityDate` (hoặc `maturityDate` trên SSI): Ngày đáo hạn / hết hạn chứng quyền (VD: `20261221` -> 21/12/2026).
   * `CWLastTradingDate` (hoặc `lastTradingDate` trên SSI): Ngày giao dịch cuối cùng trước khi huỷ niêm yết (VD: `20261217`).
   * `CWExerciseRatio` (hoặc `exerciseRatio` trên SSI): Tỷ lệ chuyển đổi (VD: `1.7245:1`, `2:1`).
   * `CWExcersisePrice` (hoặc `exercisePrice` trên SSI): Giá thực hiện (VD: `22.419` / `22419`).
   * `issuerName`: Công ty chứng khoán phát hành (VD: `TCBS`, `SSI`, `VND`).
   * `underlyingSymbol`: Mã cổ phiếu cơ sở (VD: `ACB`, `HPG`, `FPT`).
3. **Snapshot sổ lệnh 57 trường dữ liệu cực nhanh**:
   * Hỗ trợ batch 100 mã/request. Kéo toàn bộ 1.980+ mã đang giao dịch của toàn thị trường chỉ mất **1 - 1.5 giây**.
   * Đầy đủ 3 bước giá Mua và 3 bước giá Bán tốt nhất kèm khối lượng chờ (`g1` đến `g6`), giá Trần/Sàn/Tham chiếu, khối ngoại mua/bán.
4. **Chi tiết khớp lệnh trong ngày**:
   * Endpoint `/getliststocktrade/{SYMBOL}` trả về toàn bộ hàng nghìn lệnh khớp từ đầu phiên đến hiện tại.
   * Có sẵn trường `side: "B"` (Mua chủ động), `side: "S"` (Bán chủ động), `""` (Khớp ATO/ATC).

### 2.2. Điểm VPS KHÔNG CÓ mà DNSE bù đắp hoàn hảo:
1. **Lịch sử nến quá khứ (Historical OHLCV)**:
   * VPS là datafeed realtime cho bảng điện, **không có API nến lịch sử quá khứ** (hôm qua, tuần trước, tháng trước).
   * **DNSE** có sẵn endpoint nến 1 phút và nến ngày tải cực nhanh:
     `https://services.entrade.com.vn/chart-api/v2/ohlcs/stock?from={FROM}&to={TO}&symbol={SYMBOL}&resolution=1` (hoặc `resolution=1D`).
2. **Nến Phái sinh VN30F1M**:
   * DNSE có endpoint riêng cho phái sinh:
     `https://services.entrade.com.vn/chart-api/v2/ohlcs/derivative?symbol=VN30F1M&resolution=1`.

---

## 3. Bản Đồ Các File Log Dữ Liệu Đã Lưu (Thư Mục `logs/`)

Toàn bộ dữ liệu thực tế kéo từ phiên hôm nay đã được chuẩn hóa JSON thụt dòng đẹp mắt và lưu tại:

* [`logs/log_vps.json`](file:///d:/tools/stock-trading-system/logs/log_vps.json) (~7.3 MB):
  * `master_symbols_raw`: Danh mục toàn bộ 2.166 mã toàn thị trường.
  * `all_active_cw_specifications_full`: Toàn bộ **339 mã Chứng quyền** đang lưu hành kèm ngày đáo hạn, ngày GD cuối, tỷ lệ chuyển đổi, giá thực hiện.
  * `all_market_symbols_snapshot`: Snapshot sổ lệnh chi tiết của **1.982 mã** giao dịch.
  * `intraday_trades_stock` & `intraday_trades_cw`: Lịch sử khớp lệnh toàn bộ trong ngày của `HPG` (4.452 lệnh) và CW `CACB2603` (59 lệnh).
* [`logs/log_dnse.json`](file:///d:/tools/stock-trading-system/logs/log_dnse.json) (~151 KB): Chuỗi nến 1 phút (1.127 nến) và nến ngày 1 năm (247 nến) của Cổ phiếu và Chứng quyền.
* [`logs/log_kbsec.json`](file:///d:/tools/stock-trading-system/logs/log_kbsec.json) (~43 KB): Lịch sử khớp lệnh Tick-by-tick chi tiết đến mili-giây có cờ Mua/Bán `B`/`S`.
* [`logs/log_ssi.json`](file:///d:/tools/stock-trading-system/logs/log_ssi.json) (~3.5 MB): Bảng giá snapshot 3 sàn HOSE (409 mã), HNX (299 mã) và UPCOM (817 mã).

---

## 4. Giải Thích Các Loại Mã Trong Master List VPS

Để bot lọc chính xác sản phẩm cần giao dịch, chỉ cần kiểm tra trường **`type`**:

| Ký hiệu `type` | Loại tài sản | Ví dụ | Chiến lược xử lý của Bot |
| :---: | :--- | :--- | :--- |
| **`S`** | Cổ phiếu cơ sở (Stock) | `HPG`, `SSI`, `FPT`, `VNM` | ✅ **Lấy (Giao dịch chính)** |
| **`W`** | Chứng quyền có bảo đảm (Warrant) | `CACB2603`, `CHPG2601` | ✅ **Lấy (Giao dịch CW)** |
| **`D`** | Phái sinh chỉ số VN30 | `41I1GA000` (VN30F) | ✅ Lấy nếu bot có tính năng trade phái sinh T+0 |
| **`D`** | Phái sinh Trái phiếu TPCP | `41B5G3000` | ❌ **Loại bỏ (Thanh khoản bằng 0, chỉ dành cho tổ chức)** |
| **`R` / `T`** | Trái phiếu doanh nghiệp | `BID12101`, `BAB122030` | ❌ **Loại bỏ** |

---

## 5. Lệnh Chạy Lại Kiểm Thử Khi Cần

Bất cứ lúc nào cần chạy lại kiểm thử để cập nhật dữ liệu mới nhất:
```bash
cd d:\tools\stock-trading-system
dotnet run --project StockDataTester
```
Mã nguồn C# được cấu trúc hoàn chỉnh tại: [`StockDataTester/Program.cs`](file:///d:/tools/stock-trading-system/StockDataTester/Program.cs).
