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
* **Đầu phiên (08:30 - 08:59) - Gọi REST API 1 lần duy nhất**:
  * Tải danh mục Master toàn thị trường (2.166 mã từ VPS) và thông số HĐTL Phái sinh, Chứng quyền (từ SSI) lưu vào PostgreSQL.
  * Tự động đăng ký toàn bộ danh mục đang hoạt động vào kết nối WebSocket.
* **Trong phiên (09:00 - 11:30 & 13:00 - 14:45) - 100% WEBSOCKET (Zero HTTP Request)**:
  * Toàn bộ Khớp lệnh Ticks vi mô (ID `3220`), Biến động Sổ lệnh L2 Depth (ID `3210`) và Khối ngoại (ID `3250`) của **tất cả các mã trên thị trường** được máy chủ VPS **tự động PUSH về qua 1 kết nối WebSocket duy nhất**.
  * **Tuyệt đối không gửi bất kỳ HTTP request polling nào trong phiên**.
  * Chỉ kích hoạt REST Polling làm "Phao cứu sinh khẩn cấp" nếu socket đứt mạng hoàn toàn.
* **Sau phiên (Reconciliation & Full Tick Recovery cuối ngày - 15h00) - Gọi REST API 1 lần duy nhất**:
  * Đọc toàn bộ các tick đã gom trong đệm `staging/*.wal`.
  * **Tự động kéo bù Tick nguyên ngày**: Gọi trực tiếp endpoint `/getliststocktrade/{symbol}` tải lại toàn bộ lịch sử khớp lệnh từ `09:00:00` đến `14:45:00` của từng mã.
  * Hợp nhất với dữ liệu thu được trong phiên để bù đắp 100% các tick có thể bị rớt do mạng lag hoặc khởi động lại dịch vụ giữa phiên.
  * Tiến hành khử trùng lặp và nén ra định dạng chuẩn Apache Parquet vĩnh viễn.

### 3.3. Dự phòng đa nguồn (Multi-Source Fallback)
* Cấu hình danh sách nguồn dữ liệu ưu tiên dựa trên kết quả kiểm thử thực tế (09/10/2026):
  * **Primary (Nguồn chính - 85%)**: **VPS Datafeed** (Lấy toàn bộ 2.166 mã HOSE/HNX/UPCOM, snapshot sổ lệnh 57 trường L2, khớp lệnh kèm chiều `B`/`S`).
  * **Secondary (Nguồn phụ trợ - 15%)**: **DNSE Entrade** (Nạp chuỗi nến lịch sử quá khứ 1m/1D và Nến Phái sinh `VN30F1M`).
  * **Tertiary (Bổ sung thông số CW)**: **SSI iBoard** (Kéo ngày bắt đầu giao dịch `firstTradingDate` của Chứng quyền).
  * **Quaternary (Vi mô Tick-by-tick)**: **KBSec Buddy** (Dự phòng thời gian mili-giây).
* Khi nguồn chính gặp sự cố, hệ thống tự động Failover sang nguồn dự phòng.

### 3.4. Khử trùng lặp dữ liệu & An toàn sao lưu (Deduplication & Safe Atomic Storage)
* **Khóa khử trùng lặp đa thuộc tính (Composite Natural Key)**:
  Khi hợp nhất dữ liệu từ WAL và kéo bù từ API, các lệnh trùng lặp sẽ được lọc tuyệt đối bằng khóa:
  $$\text{Key} = (\text{Symbol}, \text{Timestamp}, \text{Price}, \text{Volume}, \text{Side}, \mathbf{AccumulatedVolume})$$
  * **Tại sao cần $\mathbf{AccumulatedVolume}$?** Trong cùng 1 giây, một mã có thể có 5–10 lệnh khớp cùng mức giá và khối lượng (ví dụ cùng 1.000 cổ giá 20.45). Nếu chỉ dùng Timestamp và Volume thì các lệnh này sẽ bị xóa nhầm! Nhờ có `AccumulatedVolume` (khối lượng lũy kế tăng dần sau mỗi lệnh khớp), hệ thống **phân biệt chính xác từng giao dịch riêng biệt mà không bao giờ bị xóa nhầm dữ liệu thật**.
* **Cơ chế ghi file an toàn chống sập nguồn (Atomic Write & .bak Backup)**:
  - Khi xuất dữ liệu ra Parquet, file được ghi tạm vào `.parquet.tmp`.
  - Nếu file đích đã có sẵn từ trước, hệ thống tự động copy sao lưu sang `.parquet.bak` trước khi thực hiện ghi đè nguyên tử (`Atomic File.Move`).
  - Nếu server bị mất điện hoặc crash giữa chừng trong lúc ghi đĩa, file Parquet cũ vẫn nguyên vẹn 100%, không bao giờ bị tình trạng file hỏng (Corrupted file).

* **Chống trùng lặp khi ghi vào Database PostgreSQL (Idempotent Upsert)**:
  - Các bảng `symbols`, `derivative_contracts`, `covered_warrants` đều được kiểm tra mã tồn tại (`HashSet`) trước khi chèn, hoặc sử dụng khóa chính tự nhiên `SymbolCode` kết hợp `TradingDate` cho bảng nến `market_daily_bars`. Không bao giờ bị lỗi trùng khóa (Duplicate Key Exception).

### 3.5. Cơ chế Phòng thủ Chống Chặn IP & Kênh WebSocket Realtime (Push-Based Feed)
Để duy trì bot hoạt động 24/7 trên Oracle Cloud mà không bao giờ bị WAF/Cloudflare của VPS/DNSE khóa IP:
1. **Chuyển đổi sang WebSocket trực tiếp (Push-based Socket.IO Stream) - Bao quát TOÀN THỊ TRƯỜNG**:
   - Thay vì Polling HTTP liên tục gửi hàng nghìn request, hệ thống duy trì **duy nhất 1 kết nối TCP WebSocket** qua `wss://bgdatafeed.vps.com.vn/socket.io/?EIO=4&transport=websocket`.
   - Sử dụng lệnh `regs` đăng ký nhận luồng cho **TOÀN BỘ 1.890+ mã đang giao dịch** (toàn bộ Cổ phiếu HOSE, HNX, UPCOM, Chứng quyền CW và Phái sinh VN30F). Gói tin đăng ký chỉ nặng ~9.5 KB!
   - Máy chủ VPS tự động đẩy (push) từng lệnh khớp về ngay lập tức với **độ trễ 0ms (Zero Polling)**, không sinh ra bất kỳ request HTTP polling nào trong phiên, **triệt tiêu 100% rủi ro bị chặn IP**.
2. **Kiến trúc Hybrid Linh Hoạt (WebSocket toàn thị trường + REST Fallback)**:
   - WebSocket là luồng chính nhận tick thời gian thực cho toàn bộ thị trường.
   - Nếu đường truyền socket rớt hoặc bảo trì, hệ thống tự động kích hoạt chế độ dự phòng REST Polling kèm Auto-Reconnect sau mỗi 5 giây.
3. **Giả lập danh tính trình duyệt thật (Browser Fingerprinting & Headers)**:
   - Gửi đầy đủ `User-Agent` Chrome mới nhất, kèm các header chuẩn CORS: `Referer` (`https://banggia.vps.com.vn/`), `Origin`, `Accept`.
4. **Kiểm soát thông lượng và hàng đợi đồng thời (Concurrency Throttling)**:
   - Giới hạn `SemaphoreSlim(5)` và đệm trễ `50ms` giữa các request khi kéo bù dữ liệu cuối ngày.
   - Giữ tốc độ ổn định ở mức an toàn **~15–20 req/s** (tương đương 1 người dùng đang mở 2 tab bảng điện).
5. **Cơ chế Backoff tự động khi gặp cảnh báo (Exponential Backoff & Circuit Breaker)**:
   - Bắt mã lỗi `HTTP 429 (Too Many Requests)` hoặc `HTTP 503`. Tự động ngắt nhịp và ngủ đông 5 – 10 giây trước khi thử lại.
6. **Tiết kiệm tài nguyên thông minh ngoài giờ (Smart Idle)**:
   - Vào ban đêm, giờ nghỉ trưa hoặc Thứ 7, Chủ Nhật khi sàn đóng cửa, bot tự động hạ tần suất lặp xuống 30–60 giây/lần để không tạo request rác lên server.

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

### Giai đoạn 2: Xây Dựng Core Crawler & Xử Lý Dữ Liệu (ĐÃ HOÀN THÀNH ✅)
- [x] Thiết kế schema DDL PostgreSQL (`sql/01_init_schema_postgres.sql`) và C# Entity Models (`TradingSystem.Core`).
- [x] Xây dựng module ghi đệm nối tiếp phi khóa WAL và nén ra Apache Parquet (`Parquet.Net` 6.1.0).
- [x] Xây dựng Background Worker (`TradingSystem.Collector`) tự động lên lịch chạy theo phiên chứng khoán Việt Nam (08:30 Master Sync -> 09:00-14:45 Intraday Ticks -> 15:00 EOD Parquet).

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
