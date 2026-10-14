# HƯỚNG DẪN QUẢN TRỊ VÀ VẬN HÀNH DỰ ÁN TRÊN ORACLE CLOUD (OCI)

Tài liệu này giải thích chi tiết cấu trúc triển khai, nguyên lý hoạt động và các lệnh quản trị hệ thống **Stock Trading System** đang chạy trực tiếp trên **Oracle Cloud VPS**.

---

## 1. THÔNG TIN MÁY CHỦ ORACLE CLOUD

- **Public IP Address:** `168.110.56.11`
- **Hệ điều hành:** Ubuntu 24.04 LTS (x86_64)
- **Cấu hình phần cứng:**
  - **CPU:** 1 OCPU AMD EPYC
  - **RAM vật lý:** 1 GB
  - **RAM ảo (Swap file):** 8 GB (đã tạo tại `/swapfile` trên SSD)
  - **Dung lượng lưu trữ:** 150 GB (trống ~142 GB)
- **File chìa khóa SSH (Private Key):** [oracle.key](file:///f:/tools/stock-trading-system/oracle.key) (ở máy local)
- **Tài khoản đăng nhập SSH:** `ubuntu`

---

## 2. NGUYÊN LÝ HOẠT ĐỘNG CỦA HỆ THỐNG TRÊN CLOUD

Hệ thống được thiết kế khép kín hoàn toàn trên VPS (All-in-one):

```
                   Internet / Sàn giao dịch
                    (VPS, DNSE, SSI APIs)
                             │
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │ Oracle Cloud VPS (168.110.56.11)                       │
 │                                                        │
 │ 1. Systemd Service (trading-collector.service)         │
 │    └── Worker.cs (.NET 10 Background Service)          │
 │         ├── Kéo dữ liệu Realtime (Ticks / OrderBook)   │
 │         ├── Kéo nến lịch sử OHLCV                      │
 │         │                                              │
 │ 2. Tầng lưu trữ (150 GB SSD):                          │
 │         ├── WAL Staging: data/staging/*.wal (ghi siêu tốc) │
 │         └── Parquet Storage: data/storage/**/*.parquet │
 │                                                        │
 │ 3. Cơ sở dữ liệu:                                      │
 │    └── PostgreSQL 16 (database: trading_system)        │
 │         ├── Tự động khởi động cùng hệ điều hành        │
 │         └── Chứa Bảng metadata, symbols, ticks, books  │
 └────────────────────────────────────────────────────────┘
```

1. **Tiến trình chạy ngầm qua `systemd`:**
   - Ứng dụng được cài đặt như một dịch vụ hệ thống Linux: `trading-collector.service`.
   - Khi server khởi động lại (reboot) hoặc khi tiến trình bị lỗi dừng bất ngờ, `systemd` sẽ **tự động kích hoạt lại sau 5 giây (`Restart=always`)**.
   - Bạn có thể tắt máy tính cá nhân ở nhà mà VPS trên mây vẫn âm thầm thu thập dữ liệu 24/7.
2. **Kiến trúc Big Data 2 tầng:**
   - **Tầng đệm (Staging):** Dữ liệu luồng tick và sổ lệnh trong phiên được ghi liên tục dạng nhị phân cực nhanh vào `data/staging/`.
   - **Tầng nén Parquet (Storage):** Định kỳ đóng gói nén (Snappy / Zstd) thành file Apache Parquet vào `data/storage/` để lưu trữ hàng chục năm tốn rất ít dung lượng.
   - **Tầng quan hệ (PostgreSQL):** Quản lý cấu trúc dữ liệu, các bảng tra cứu nhanh, index thời gian.

---

## 3. CÁCH KẾT NỐI VÀO SERVER TỪ MÁY TÍNH CỦA BẠN

Mở **PowerShell** hoặc **Terminal** tại thư mục dự án `f:\tools\stock-trading-system`, gõ:

```powershell
ssh -i oracle.key ubuntu@168.110.56.11
```

*(Hoặc dùng các phần mềm giao diện như MobaXterm, PuTTY, Termius với user `ubuntu`, IP `168.110.56.11` và trỏ file private key là `oracle.key`).*

---

## 4. CÁC LỆNH VẬN HÀNH QUAN TRỌNG TRÊN SERVER

### 4.1. Xem log hoạt động theo thời gian thực (Live Stream Logs)
Để xem bot đang làm gì, kéo mã nào, có lỗi gì không:
```bash
journalctl -u trading-collector.service -f
```
*(Bấm `Ctrl + C` để thoát màn hình xem log).*

### 4.2. Quản lý tiến trình Bot (Start / Stop / Restart)
- **Kiểm tra trạng thái:**
  ```bash
  sudo systemctl status trading-collector.service
  ```
- **Khởi động lại bot:**
  ```bash
  sudo systemctl restart trading-collector.service
  ```
- **Dừng bot:**
  ```bash
  sudo systemctl stop trading-collector.service
  ```
- **Bật lại bot:**
  ```bash
  sudo systemctl start trading-collector.service
  ```

### 4.3. Kiểm tra dữ liệu Parquet và Staging
```bash
# Xem các file Parquet đã nén lưu trữ:
ls -lh /home/ubuntu/trading-system/data/storage

# Xem thư mục đệm WAL:
ls -lh /home/ubuntu/trading-system/data/staging
```

### 4.4. Truy cập Cơ sở dữ liệu PostgreSQL
```bash
# Vào dòng lệnh tương tác psql:
sudo -u postgres psql -d trading_system

# Xem danh sách các bảng trong database:
\dt

# Xem thử 10 bản ghi ticks gần nhất:
SELECT * FROM market_ticks ORDER BY trade_time DESC LIMIT 10;

### 4.5. Kích hoạt đồng bộ bù dữ liệu độc lập (CLI On-Demand)
Nếu máy chủ bị tắt máy, mất mạng từ 15h–16h, bạn có thể chạy lệnh bù dữ liệu thủ công bất kỳ lúc nào (17h, 18h, đêm hoặc cuối tuần):
```bash
# 1. Bù dữ liệu cho ngày hôm nay:
cd /home/ubuntu/trading-system
./TradingSystem.Collector sync-eod

# 2. Hoặc bù dữ liệu cho một ngày cụ thể trong quá khứ (ví dụ: ngày 2026-10-14):
./TradingSystem.Collector sync-eod 2026-10-14
```
> 🛡️ **Cơ chế chống chặn IP (Anti-Ban / Rate Limit):** 
> Lệnh này được trang bị bộ điều tiết nhịp tự động (`SemaphoreSlim(5)` và độ trễ `50ms` giữa các request). Tốc độ gửi request được giữ cố định ở mức an toàn (~15-20 req/s), không bao giờ kích hoạt tường lửa Cloudflare/WAF của VPS, không cần tốn chi phí mua Proxy.

---

## 5. QUY TRÌNH CẬP NHẬT CODE SAU NÀY (UPDATE CODE)

Khi bạn code thêm tính năng mới ở máy local và muốn đẩy lên server:

1. **Chạy script tự động 1-click:**
   ```powershell
   ./deploy.ps1
   ```
2. **Hoặc build thủ công (hỗ trợ cả x64 và ARM64):**
   ```powershell
   # Biên dịch cho Oracle ARM64 (Ampere A1) hoặc x64 (AMD):
   dotnet publish src/TradingSystem.Collector/TradingSystem.Collector.csproj -c Release -r linux-arm64 --self-contained false -o publish-linux
   
   # Tải file lên VPS và khởi động lại dịch vụ:
   scp -i oracle.key -r publish-linux/* ubuntu@168.110.56.11:/home/ubuntu/trading-system/
   ssh -i oracle.key ubuntu@168.110.56.11 "sudo systemctl restart trading-collector.service"
   ```

