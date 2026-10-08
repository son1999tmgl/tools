# Báo Cáo Khảo Sát & Kiểm Thử Các Nguồn Dữ Liệu Chứng Khoán & Chứng Quyền
*(Thực hiện ngày 08/10/2026 - Kiểm thử thực tế bằng C# .NET và Python)*

---

## 1. Bảng Tổng Hợp So Sánh Các Nguồn Dữ Liệu

| STT | Nguồn dữ liệu | Loại dữ liệu lấy được | Độ chi tiết | Khả năng lấy Chứng quyền (CW) | Khả năng lấy liên tục / Rate Limit | Đánh giá |
| :---: | :--- | :--- | :--- | :---: | :--- | :--- |
| **1** | **KBSec (KB Securities)** | **Từng khớp lệnh (Tick-by-tick)** | Chi tiết đến **mili-giây**, giá, KL, chiều Mua/Bán (`B`/`S`/`ATO`/`ATC`), tổng KL tích lũy | **RẤT TỐT** (Lấy được cả CP lẫn CW) | **Rất cao** (Hỗ trợ phân trang `page=...&limit=1000`, kéo hàng ngàn lệnh/request mà không cần Token) | ⭐⭐⭐⭐⭐ **(Nguồn chính cho dữ liệu Tick)** |
| **2** | **DNSE (Entrade)** | **Nến 1 phút & Daily (OHLCV)** | Thời gian timestamp, Mở, Cao, Thấp, Đóng, Khối lượng | **RẤT TỐT** (Lấy cả CP lẫn CW) | **Rất tốt** (REST API tải nhanh lịch sử nhiều ngày lùi về quá khứ) | ⭐⭐⭐⭐⭐ **(Nguồn chính cho Backtest nến 1m)** |
| **3** | **VPS Datafeed** | **Danh mục mã niêm yết (Master list)** | Toàn bộ mã sàn HOSE, HNX, UPCOM, Phái sinh, Chứng quyền | **RẤT TỐT** (339+ mã CW đang lưu hành) | **Tuyệt vời** (API tĩnh tải một lần đầu ngày) | ⭐⭐⭐⭐⭐ **(Nguồn danh mục mã chuẩn)** |
| **4** | **SSI iBoard** | **Snapshot bảng giá sàn** | Giá khớp hiện tại, 3 mức giá Mua/Bán tốt nhất (Orderbook Level 2) | Khá (Tập trung cổ phiếu cơ sở) | Khá (Có thể poll định kỳ mỗi vài giây) | ⭐⭐⭐⭐ (Dự phòng cho snapshot sổ lệnh) |
| **5** | **TCBS / FireAnt** | Yêu cầu đăng nhập / Trả phí | - | - | Thường xuyên đổi format hoặc yêu cầu Token xác thực | ⭐⭐ (Không khuyến nghị dùng cào tự do) |

---

## 2. Kết Quả Kiểm Thử Thực Tế Từng Nguồn (Live Test)

### 2.1. Nguồn KBSec - Thu thập từng lệnh khớp (Tick-by-tick)
* **Endpoint**: `https://kbbuddywts.kbsec.com.vn/iis-server/investment/trade/history/{SYMBOL}?page={PAGE}&limit={LIMIT}`
* **Dữ liệu trả về**:
  ```json
  {
    "t": "2026-10-08 13:54:09:88",  // Thời gian chính xác đến phần trăm giây
    "TD": "08/10/2026",             // Ngày giao dịch
    "SB": "CACB2515",               // Mã cổ phiếu / chứng quyền
    "FT": "13:54:09",               // Giờ khớp
    "LC": "B",                      // Chiều lệnh: 'B' (Mua chủ động), 'S' (Bán chủ động), '' (ATO/ATC)
    "FMP": 270,                     // Giá khớp
    "FV": 3000,                     // Khối lượng khớp
    "AVO": 12500,                   // Khối lượng tích lũy trong ngày
    "AVA": 3449000                  // Giá trị tích lũy trong ngày (VNĐ)
  }
  ```
* **Kết quả test**:
  - Test mã cổ phiếu `HPG`: Kéo thành công 2.500 lệnh khớp liên tục qua 5 trang mà không bị chặn.
  - Test mã chứng quyền `CACB2515`: Kéo chính xác 5 lệnh khớp trong phiên của ngày hôm nay.

---

### 2.2. Nguồn DNSE Entrade - Thu thập nến 1 phút (1m OHLCV)
* **Endpoint**: `https://services.entrade.com.vn/chart-api/v2/ohlcs/stock?from={FROM_UNIX}&to={TO_UNIX}&symbol={SYMBOL}&resolution=1`
* **Dữ liệu trả về**:
  - `t`: Mảng timestamp (giây).
  - `o`, `h`, `l`, `c`: Mảng giá Mở, Cao, Thấp, Đóng (Open, High, Low, Close).
  - `v`: Mảng khối lượng (Volume).
* **Kết quả test**:
  - Test mã `HPG`: Lấy 903 nến 1 phút của 5 ngày gần nhất.
  - Test mã chứng quyền `CACB2515`: Lấy 30 nến 1 phút giao dịch gần nhất.
* **Ứng dụng**: Rất thích hợp làm dữ liệu để backtest thuật toán nến 1 phút lùi về quá khứ mà không cần phải chờ tích luỹ tick.

---

### 2.3. Nguồn VPS - Danh mục toàn bộ mã cổ phiếu & chứng quyền
* **Endpoint**: `https://bgapidatafeed.vps.com.vn/getlistallstock`
* **Kết quả test**:
  - Trả về **2.069 mã giao dịch** toàn thị trường Việt Nam.
  - Tự động lọc được **339 mã Chứng Quyền (Covered Warrants)** đang niêm yết (mã 8 ký tự bắt đầu bằng `C`).
* **Ứng dụng**: Làm bước khởi động (Initialization) đầu mỗi ngày để lấy danh sách mã cần theo dõi.

---

## 3. Cách Tự Chạy Lại Kiểm Thử (Khi Cần Test Lại Vào Giờ Giao Dịch)

Dự án kiểm thử C# .NET đã được đóng gói sẵn tại thư mục: [`d:\tools\StockDataTester`](file:///d:/tools/StockDataTester).

### Lệnh chạy kiểm thử:
Mở Terminal (PowerShell hoặc CMD) và gõ:
```bash
cd d:\tools\StockDataTester
dotnet run
```

### File mã nguồn kiểm thử:
- Mã C# kiểm thử: [`d:\tools\StockDataTester\Program.cs`](file:///d:/tools/StockDataTester/Program.cs)
