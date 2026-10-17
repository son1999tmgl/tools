# Telegram to Excel Collector

Tool tự động theo dõi và thu thập tin nhắn từ các nhóm Telegram cá nhân hoặc channel, lọc theo từ khóa/điều kiện định sẵn và tự động đẩy dữ liệu vào file Excel.

## 1. Tính năng chính
- **Theo dõi thời gian thực (Real-time listener):** Sử dụng thư viện `Telethon` (Telegram Client API), cho phép tài khoản Telegram của bạn đọc tin nhắn từ bất kỳ nhóm/kênh nào bạn đã tham gia mà không cần quyền Admin.
- **Bộ lọc linh hoạt (Keyword & Regex Filter):** Lọc theo danh sách từ khóa hoặc biểu thức chính quy (Regex).
- **Ghi file Excel tự động:** Tự động mở file Excel có sẵn và append dòng mới (thời gian, nhóm, người gửi, nội dung tin nhắn, link tin nhắn, ...), không làm hỏng cấu trúc bảng có sẵn.
- **Xử lý an toàn & thông báo:** Lưu trữ session an toàn, có thông báo log trực quan trên terminal.

## 2. Cài đặt môi trường

1. Mở terminal và chuyển vào thư mục tool:
   ```bash
   cd d:\tools\telegram-collector
   ```

2. Tạo và kích hoạt môi trường ảo (khuyên dùng):
   ```bash
   python -m venv venv
   .\venv\Scripts\activate
   ```

3. Cài đặt các thư viện cần thiết:
   ```bash
   pip install -r requirements.txt
   ```

## 3. Cấu hình Telegram API

Telegram Client API yêu cầu `API_ID` và `API_HASH` từ tài khoản của bạn:
1. Đăng nhập vào trang: [https://my.telegram.org](https://my.telegram.org).
2. Chọn **API development tools**.
3. Điền thông tin ứng dụng (App title, short name) để nhận `api_id` và `api_hash`.
4. Copy file `.env.example` thành `.env`:
   ```bash
   cp .env.example .env
   ```
5. Mở file `.env` và điền:
   - `TELEGRAM_API_ID`: Mã số API ID của bạn
   - `TELEGRAM_API_HASH`: Mã hash API của bạn
   - `TELEGRAM_PHONE`: Số điện thoại đăng ký Telegram (kèm mã quốc gia, ví dụ `+84987654321`)
   - `EXCEL_PATH`: Đường dẫn tới file Excel đã tạo sẵn (mặc định: `data.xlsx`)
   - `KEYWORDS`: Danh sách từ khóa cần lọc, phân cách bằng dấu phẩy (hoặc để trống nếu muốn lấy tất cả)

## 4. Chạy tool
```bash
python main.py
```
*Lưu ý: Lần đầu tiên chạy, Telethon sẽ gửi mã OTP xác nhận về app Telegram của bạn trên điện thoại/máy tính để đăng nhập và tạo file session.*
