# CW Tracker App - Ứng Dụng Theo Dõi Chứng Quyền

Đây là ứng dụng giao diện trực quan (GUI) được viết bằng Python (Tkinter) nhằm hỗ trợ theo dõi, định giá và lọc Chứng quyền có bảo đảm (Covered Warrants - CW) trên thị trường chứng khoán Việt Nam.

## 🚀 Hướng Dẫn Sử Dụng (Dành Cho Người Dùng)

### Cài đặt môi trường
Đảm bảo bạn đã cài đặt Python 3 và các thư viện cần thiết:
```bash
pip install requests tkcalendar scipy
```

### Cách chạy ứng dụng
Mở terminal hoặc command prompt trong thư mục dự án và chạy:
```bash
python cw_tracker.py
```

### Các tính năng chính
1. **Theo dõi Real-time**: Ứng dụng tự động lấy dữ liệu mới nhất từ bảng giá mỗi 2 phút.
2. **Bộ lọc thông minh (4 chế độ)**:
   - **Mặc định**: Hiển thị toàn bộ chứng quyền đang lưu hành.
   - **Mô hình 1 (Lọc An Toàn)**: Loại bỏ các CW rủi ro cao. Chỉ giữ lại CW có thanh khoản (Bán 1 >= 1000), còn thời hạn (>= 30 ngày), Premium thấp (<= 5%) và đang ở trạng thái hòa vốn/lãi.
   - **Mô hình 2 (Chấm Điểm)**: Chấm điểm dựa trên Đòn bẩy (Gearing), Độ an toàn (Moneyness) và Thanh khoản.
   - **Mô hình 3 (Black-Scholes)**: Tính toán Giá Lý Thuyết của CW dựa trên độ biến động lịch sử (90 ngày) của tài sản cơ sở. Tự động so sánh với giá thị trường và xếp hạng các mã đang bị "định giá rẻ".
3. **Lưu trữ dữ liệu ngầm**: Khi mở ứng dụng, hệ thống tự động tải dữ liệu nến 1-phút của các mã cổ phiếu trong danh sách `top_300_stocks.txt` vào cơ sở dữ liệu `intraday_data.db`. Dữ liệu này dùng để train AI hoặc xây dựng mô hình thuật toán phức tạp hơn trong tương lai.

---

## 💻 Ghi Chú Kỹ Thuật (Dành Cho AI / Lập Trình Viên Phát Triển Tiếp)

### Cấu trúc dự án
- `cw_tracker.py`: Chứa logic chính khởi tạo giao diện Tkinter, luồng lấy dữ liệu real-time từ API VPS, và cơ chế sắp xếp/hiển thị Treeview.
- `valuation_models.py`: Chứa toàn bộ các thuật toán định giá và lọc (Smart Filter, Scoring Model, Black-Scholes). Được gọi từ `cw_tracker.py` trước khi render bảng.
- `data_collector.py`: Chạy dưới dạng luồng ngầm (Daemon Thread). Sử dụng API nến 1-phút của DNSE để cào dữ liệu intraday.
- `top_300_stocks.txt`: Chứa danh sách các mã cơ sở (ví dụ: VN30) mà `data_collector.py` sẽ tự động quét và lưu dữ liệu.
- `intraday_data.db`: SQLite database chứa bảng `intraday (symbol, timestamp, open, high, low, close, volume)`. Constraint `UNIQUE(symbol, timestamp)` để chống trùng lặp.

### Những quyết định thiết kế quan trọng cần lưu ý:
1. **Nguồn Dữ Liệu**:
   - Dữ liệu Real-time (CW + Cổ phiếu) được lấy từ API public của VPS (`bgapidatafeed.vps.com.vn`). Giá trả về từ VPS đã chia 1000 (Ví dụ: 1.25 = 1250 VNĐ) nên trong code đã được nhân lại cho 1000.
   - Dữ liệu lịch sử 1-phút được lấy từ DNSE (`services.entrade.com.vn/chart-api/v2`). Lý do chọn DNSE vì API intraday của `vnstock` bị lỗi import, và API của VNDirect/SSI/TCBS yêu cầu cookie/auth khắt khe hoặc chặn bot. DNSE hiện tại ổn định định nhất cho nến 1 phút (1D độ phân giải).
2. **Xử lý Bất đồng bộ**:
   - Quá trình lấy dữ liệu real-time chạy trên thread riêng (`fetch_data`) để không làm đơ GUI.
   - `DataCollector` cũng chạy một thread ngầm vĩnh viễn (`daemon=True`) song song với vòng lặp chính của Tkinter. Các thông báo (print) trong tiến trình này sử dụng tiếng Anh tĩnh chuẩn ASCII để tránh lỗi `UnicodeEncodeError` trên console Windows.
3. **Mô hình Black-Scholes**:
   - Historical Volatility (HV) được tính động bằng cách fetch dữ liệu đóng cửa 90 ngày của tài sản cơ sở từ DNSE. Do quá trình này tốn I/O, hệ thống sử dụng cache `VOLATILITY_CACHE` trong bộ nhớ tĩnh để tránh tính lại nhiều lần trong mỗi lần refresh.
   - Risk-free rate đang được hardcode ở mức `r = 5%` (0.05).

### Hướng phát triển tiếp theo (Next Steps)
- Hiện tại AI chưa bắt đầu huấn luyện từ `intraday_data.db`. Ở giai đoạn tiếp theo có thể xây dựng pipeline Machine Learning / Deep Learning đọc dữ liệu từ DB này để dự đoán biến động giá.
- Thêm UI quản lý danh sách `top_300_stocks.txt` trực tiếp trên ứng dụng.
- Lưu lại cấu hình (lọc, hiển thị cột) của người dùng thay vì reset mỗi lần tắt app.
