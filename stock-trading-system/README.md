# Hệ Thống Thu Thập Dữ Liệu Giao Dịch Chứng Khoán & Backtest Thuật Toán
*(Định hướng kỹ thuật song hành: Giao dịch định lượng (Quant) & Nền tảng kỹ sư MES)*

---

## 1. Mục Tiêu Dự Án
1. **Thu thập dữ liệu khớp lệnh chi tiết (Tick-by-tick & L2 Depth)**:
   - Thị trường: **Cổ phiếu cơ sở** (HOSE, HNX, UPCOM), **Chứng quyền có bảo đảm** (Covered Warrants - CW) và **Chứng khoán Phái sinh** (Hợp đồng tương lai VN30F1M, VN100).
   - Chi tiết: Từng giao dịch gồm thời gian chính xác, giá/điểm số, khối lượng, chiều Mua/Bán chủ động (`B`/`S`), vị thế mở qua đêm (OI).
2. **Lưu trữ tối ưu cho dữ liệu lớn (Big Data / Time-Series)**:
   - Tối ưu dung lượng lưu trữ dài hạn (nhiều tháng/năm) bằng chuẩn Apache Parquet.
   - Tốc độ đọc siêu nhanh phục vụ tính toán ma trận chỉ báo và backtesting đa khung thời gian.
3. **Môi trường Backtest linh hoạt & Đa tài sản**:
   - Cho phép chọn khoảng thời gian tùy biến (từ ngày X đến ngày Y).
   - Thử nghiệm các thuật toán giao dịch 1 chiều (Cổ phiếu/CW) và **2 chiều LONG / SHORT (Phái sinh)** trên dữ liệu quá khứ.
   - Thống kê xác suất đúng (Win Rate %), Tỷ lệ Lời/Lỗ (Profit Factor), Maximum Drawdown, Sharpe Ratio, MFE/MAE.
   - Hỗ trợ đa cấp độ tín hiệu: Toàn thị trường (Market Regime), Luân chuyển dòng tiền ngành (Sector Rotation), Rổ chỉ số (Index Basket) và Cổ phiếu đơn lẻ.
4. **Mục tiêu nghề nghiệp**:
   - Rèn luyện kỹ năng cốt lõi cho vị trí **Kỹ sư MES (Manufacturing Execution System)**: C# .NET đa nền tảng, kiến trúc Worker ngầm, tối ưu Database/Storage, xử lý dữ liệu thời gian thực (real-time stream).

---

## 2. Kiến Trúc Hệ Thống (System Architecture)

```
[Bảng điện VPS / SSI / DNSE]
               │
               ▼ (Dữ liệu Live / Intraday ticks / Sổ lệnh L2)
┌─────────────────────────────────────────────────────────────┐
│ 1. DATA COLLECTOR SERVICE (C# .NET Worker)                  │
│    - Chạy ngầm 24/7 trên Linux VM (Ubuntu)                  │
│    - Hứng dữ liệu Live (Websocket) + Fallback REST API      │
│    - Dùng System.Threading.Channels xử lý đệm bất đồng bộ   │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼ (Ghi nối tiếp liên tục)       ▼ (Dữ liệu tổng hợp / Metadata)
┌───────────────────────────────────────────┐ ┌───────────────────────────────────────────┐
│ 2. STAGING BUFFER & COLD STORAGE          │ │ 3. DATABASE QUẢN LÝ (Oracle Cloud / SQL)  │
│    - Trong phiên: Ghi append file nháp    │ │    - Lưu: Danh mục mã, Ngành, CW Specs    │
│      (Write-Ahead Log / Staging WAL)      │ │    - Lưu: Hợp đồng Phái sinh (Ký quỹ, Hạn)│
│    - Cuối ngày (15h00): Bù dữ liệu thiếu, │ │    - Lưu: Nến ngày EOD, OI, Khối ngoại    │
│      khử trùng, nén ra *.parquet          │ │    - Lưu: Tín hiệu & Lịch sử Backtest     │
│    - Nén 5x - 10x, lưu trữ an toàn lâu dài│ └───────────────────────────────────────────┘
└──────────────┬────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. BACKTEST ENGINE (C# / Python)                            │
│    - Load Parquet thẳng vào bộ nhớ                          │
│    - Chạy thuật toán qua khoảng ngày X -> Y                 │
│    - Đánh giá xác suất đúng (Win Rate %), Lãi/Lỗ 2 chiều    │
│      LONG / SHORT, MFE (Gồng lãi) & MAE (Rủi ro)            │
└─────────────────────────────────────────────────────────────┘

> 📌 **Chi tiết thiết kế Database & Lưu trữ**: Xem toàn bộ cấu trúc bảng và trường dữ liệu tại [DATABASE_DESIGN.md](DATABASE_DESIGN.md).

---

## 3. Chiến Lược An Toàn Dữ Liệu & Chịu Lỗi (Data Safety & Fault-Tolerance)

Để đảm bảo **không bao giờ bị mất dữ liệu** khi nguồn cấp bị lag, sập mạng hoặc máy chủ bị tắt đột ngột:

### 3.1. Chống mất dữ liệu khi mất điện / sập ứng dụng (Write-Ahead Logging - WAL)
* Không giữ dữ liệu tích lũy trên RAM.
* Sử dụng hàng đợi phi khóa `System.Threading.Channels` trong C#:
  * **Luồng mạng (Ingestion)**: Đẩy tick nhận được vào Channel tức thì, không làm nghẽn socket.
  * **Luồng đĩa (Flusher)**: Cứ mỗi 5-10 giây hoặc gom đủ 1.000 lệnh sẽ ghi nối tiếp (`append`) xuống file tạm `staging/YYYY-MM-DD.wal` trên ổ cứng.
  * Khi gặp sự cố đột ngột, toàn bộ dữ liệu trước thời điểm crash đã nằm an toàn trên đĩa.

### 3.2. Chống lag, rớt mạng & bù đắp dữ liệu (Stream + Reconciliation)
* **Trong phiên (Live Monitor)**:
  * Cơ chế Heartbeat/Ping phát hiện rớt mạng quá 10 giây.
  * Tự động thử lại kết nối (Auto-reconnect với Exponential Backoff).
  * Gọi REST API kéo bù những gói dữ liệu bị hổng trong khoảng thời gian mất mạng.
* **Sau phiên (Reconciliation cuối ngày - 15h00)**:
  * Gọi API tải toàn bộ lịch sử khớp lệnh nguyên ngày (Daily Intraday Snapshot) của các mã.
  * Đối soát với dữ liệu đã hứng được trong phiên. Bù đắp mọi lệnh còn thiếu.

### 3.3. Dự phòng đa nguồn (Multi-Source Fallback)
* Cấu hình danh sách nguồn dữ liệu ưu tiên dựa trên kết quả kiểm thử thực tế (09/10/2026):
  * **Primary (Nguồn chính - 85%)**: **VPS Datafeed** (Lấy toàn bộ 2.166 mã HOSE/HNX/UPCOM, snapshot sổ lệnh 57 trường L2, khớp lệnh kèm chiều `B`/`S`).
  * **Secondary (Nguồn phụ trợ - 15%)**: **DNSE Entrade** (Nạp chuỗi nến lịch sử quá khứ 1m/1D và Nến Phái sinh `VN30F1M`).
  * **Tertiary (Bổ sung thông số CW)**: **SSI iBoard** (Kéo ngày bắt đầu giao dịch `firstTradingDate` của Chứng quyền).
  * **Quaternary (Vi mô Tick-by-tick)**: **KBSec Buddy** (Dự phòng thời gian mili-giây).
* Khi nguồn chính gặp sự cố, hệ thống tự động Failover sang nguồn dự phòng.

### 3.4. Khử trùng lặp dữ liệu (Deduplication & Idempotency)
* Khi bù dữ liệu từ nhiều nguồn hoặc re-fetch sau khi rớt mạng, một số lệnh có thể bị trùng.
* Hệ thống định danh mỗi giao dịch theo khóa tự nhiên:
  $$\text{Key} = (\text{Symbol}, \text{Timestamp}, \text{Price}, \text{Volume}, \text{Side})$$
* Trước khi đóng gói nén ra file Parquet chính thức cuối ngày, tiến trình Deduplication sẽ loại bỏ 100% các bản ghi trùng lặp và sắp xếp theo trình tự thời gian tăng dần.

---

## 4. Lựa Chọn Công Nghệ & Hạ Tầng

### 4.1. Ngôn ngữ & Nền tảng cốt lõi
* **Ngôn ngữ chính**: **C# (.NET 8/9/10)**
  * **Lý do**:
    * Chạy đa nền tảng (Cross-platform) hoàn hảo trên cả Windows và Linux (Ubuntu).
    * Hiệu năng cao, kiểm soát bộ nhớ tốt, xử lý đa luồng (Multi-threading/Channels) tốt khi hứng lượng lớn lệnh giao dịch.
    * Là ngôn ngữ tiêu chuẩn của các hệ thống MES công nghiệp (kết nối PLC, OPC UA, SCADA, Backend nhà máy).
* **Ngôn ngữ bổ trợ (Tùy chọn)**: **Python 3.13** (Dùng khi cần prototype nhanh các mô hình học máy / phân tích dữ liệu chuyên sâu).

### 4.2. Hạ tầng Cloud (Oracle Cloud Always Free)
Tận dụng tối đa gói miễn phí trọn đời của Oracle Cloud:
1. **Oracle Cloud Compute (VM Ampere A1 ARM64)**:
   * Hệ điều hành: **Ubuntu 24.04 LTS (ARM64)** hoặc **Oracle Linux 9**.
   * Cấu hình linh hoạt: Lên tới 4 OCPU, 24 GB RAM, 200 GB Block Storage (miễn phí).
   * Chức năng: Máy chủ treo 24/7 để chạy Crawler tự động, không cần bật PC ở nhà.
2. **Oracle Autonomous Database (ADB)** hoặc **PostgreSQL / SQLite**:
   * Chức năng: Lưu trữ danh mục mã, thông tin chứng quyền/phái sinh, nến gộp (1 phút / Daily) và kết quả chạy test thuật toán. Hỗ trợ sẵn Oracle APEX để làm dashboard quản lý.

### 4.3. Giải pháp lưu trữ dữ liệu Tick (Parquet)
* **Định dạng**: **Apache Parquet (Columnar Storage)**.
* **Cơ chế**: Cuối mỗi phiên (15h00), toàn bộ giao dịch tick-by-tick trong ngày được nén thành file Parquet (ví dụ: `ticks/2026-10-08.parquet`).
* **Ưu điểm**:
  * Tỉ lệ nén cực cao (tiết kiệm 80-90% dung lượng ổ cứng so với CSV/JSON).
  * Khi backtest chỉ đọc đúng các cột cần thiết (ví dụ: `Price`, `Volume`, `Time`), tốc độ tính toán nhanh hơn nhiều so với việc query qua Database.

---

## 5. Kế Hoạch Triển Khai (Roadmap)

### Giai đoạn 1: Khảo sát & Test Nguồn Dữ Liệu (ĐÃ HOÀN THÀNH ✅)
- [x] Khảo sát thực tế các nguồn API/Websocket: VPS, SSI, DNSE, KBSec.
- [x] Viết console C# [`StockDataTester`](StockDataTester/Program.cs) kiểm thử trực tiếp trong phiên:
  - Lấy thành công 2.166 mã Master & 339 Chứng Quyền.
  - Snapshot sổ lệnh 1.982 mã toàn thị trường trong 1.3 giây.
  - Khớp lệnh trong ngày có cờ Mua/Bán chủ động (`B`/`S`).
  - Lấy nến lịch sử Cổ phiếu & Phái sinh `VN30F1M` từ DNSE.
- [x] Chuẩn hóa thiết kế Database & Lưu trữ Parquet tại [`DATABASE_DESIGN.md`](DATABASE_DESIGN.md).

### Giai đoạn 2: Xây Dựng Core Crawler & Xử Lý Dữ Liệu
- [ ] Thiết kế schema DDL Database và các C# Entity Models (Symbols, CW, Derivatives, Daily Bars, Signals, Trades).
- [ ] Xây dựng module ghi nối tiếp WAL và nén ra file **Parquet** (`Parquet.Net`).
- [ ] Xây dựng Worker ngầm tự động lên lịch quét đầu ngày (SSI/VPS) và thu thập trong phiên (VPS/DNSE).

### Giai đoạn 3: Xây Dựng Backtest Engine Đa Tài Sản
- [ ] Xây dựng khung kiểm thử (Backtest Framework):
  - Tham số đầu vào: Danh mục mã (Cổ phiếu, CW, Phái sinh), Khoảng ngày (X -> Y), Logic vào/ra lệnh (Strategy).
  - Quản lý vị thế 2 chiều: `LONG` và `SHORT`, đòn bẩy ký quỹ phái sinh.
  - Kết quả đầu ra: Win Rate %, Profit Factor, Max Drawdown, Sharpe Ratio, MFE/MAE.
- [ ] Viết thử nghiệm các mô hình toán học:
  - Cổ phiếu/CW: Khớp lệnh chủ động đột biến kèm khối lượng, EMA Crossover, Định giá CW Black-Scholes.
  - Phái sinh: Mất cân bằng Sổ lệnh (Order Book Imbalance), Momentum nến 1m/5m.
  - Ngành/Thị trường: Luân chuyển dòng tiền ngành (Sector Rotation).

### Giai đoạn 4: Đưa Lên Oracle Cloud & Tự Động Hóa
- [ ] Khởi tạo VM Ubuntu trên Oracle Cloud Free Tier.
- [ ] Đóng gói Crawler service thành Systemd Service hoặc Docker Container để tự động chạy trong giờ giao dịch.
- [ ] Kết nối lưu trữ metadata lên Cloud Database.
